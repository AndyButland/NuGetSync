using Newtonsoft.Json.Linq;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using NuGetSync.Core;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace NuGetSync.Service
{
    public class NuGetPackageService : IPackageService
    {
        private readonly INuGetClient _nugetClient;
        private readonly IHttpClientFactory _httpClientFactory;

        public NuGetPackageService(INuGetClient nugetClient, IHttpClientFactory httpClientFactory)
        {
            _nugetClient = nugetClient;
            _httpClientFactory = httpClientFactory;
        }

        public async Task<int> GetPackageCountForSearchTerm(string searchTerm)
        {
            // Total hits isn't available from the NuGet Client SDK search results,
            // so we need to fall-back to a direct API request.
            var httpClient = _httpClientFactory.CreateClient("NuGetSearch");
            var httpResponseMessage = await httpClient.GetAsync("?q=umbraco&skip=0&take=0");

            httpResponseMessage.EnsureSuccessStatusCode();

            var responseContent = await httpResponseMessage.Content.ReadAsStringAsync();
            var jsonResponse = JToken.Parse(responseContent);
            return int.Parse(jsonResponse["totalHits"].ToString());
        }

        public async Task<IReadOnlyCollection<Core.PackageIdentity>> GetTaggedUmbracoPackageIdentities(string tag, int skip, int take)
        {
            // Get all packages that directly depend on Umbraco CMS V9+.
            var packagesFromSearch = await GetDirectlyDependentUmbracoPackages(tag, skip, take);

            // For those packages that matched by tag but were filtered out by version, check for any
            // dependencies they have on directly dependent packages we've found, and include them.
            var results = packagesFromSearch.PackageIdentities;
            results.AddRange(GetIndirectlyDependentUmbracoPackages(packagesFromSearch));

            return packagesFromSearch.PackageIdentities
                .Select(x => new Core.PackageIdentity(x.Id, x.Version.ToNormalizedString()))
                .OrderBy(x => x.Id)
                .ToList()
                .AsReadOnly();
        }

        private async Task<PackageSearchResults> GetDirectlyDependentUmbracoPackages(string tag, int skip, int take)
        {
            var result = new PackageSearchResults();
            
            var searchFilter = new SearchFilter(includePrerelease: false);

            var searchResults = (await _nugetClient.SearchAsync(
                tag,
                searchFilter,
                skip: skip,
                take: take)).ToList();

            // Search results return matches on Id and Description as well as tags, so given we only want packages that are
            // explicitly tagged, we'll filter out any that aren't.
            // Also filtering out any that don't support Umbraco 9 or above (requires an additional request, as the
            // search results don't contain any dependency information, so we'll do this filter last).
            searchResults = await searchResults
                .FilterByTag(tag)
                .FilterByUmbracoMinimumVersion(_nugetClient, result.PackagesFilteredOutByVersion)
                .ToListAsync();

            result.PackageIdentities.AddRange(searchResults.Select(x => x.Identity));

            return result;
        }

        private static IEnumerable<NuGet.Packaging.Core.PackageIdentity> GetIndirectlyDependentUmbracoPackages(PackageSearchResults packagesFromSearch)
        {
            var foundPackage = true;
            var results = new List<NuGet.Packaging.Core.PackageIdentity>();
            while (foundPackage)
            {
                foundPackage = false;
                var packagesToAdd = new HashSet<IPackageSearchMetadata>();

                foreach (var packageMetadata in packagesFromSearch.PackagesFilteredOutByVersion)
                {
                    // Check if the dependencies of the previously filtered out package include a package
                    // we've already included (either via a direct or indirect dependency).
                    if (DoesPackageHaveDependency(packageMetadata, packagesFromSearch.PackageIdentities) ||
                        DoesPackageHaveDependency(packageMetadata, results))
                    {
                        packagesToAdd.Add(packageMetadata);
                        foundPackage = true; // Flag that we've found one to add, so need to do another pass.
                    }
                }

                if (foundPackage)
                {
                    // Any packages we've found to include should be removed from the list of ones to check.
                    foreach (var packageToAdd in packagesToAdd)
                    {
                        packagesFromSearch.PackagesFilteredOutByVersion.Remove(packageToAdd);
                    }

                    // And added to the result list of indirectly dependendent packages.
                    results.AddRange(packagesToAdd.Select(x => x.Identity));
                }
            }

            return results;
        }

        private static bool DoesPackageHaveDependency(IPackageSearchMetadata packageMetadata, IEnumerable<NuGet.Packaging.Core.PackageIdentity> packageIds) =>
            packageMetadata.DependencySets.SelectMany(x => x.Packages.Select(x => x.Id)).Intersect(packageIds.Select(x => x.Id)).Any();

        private class PackageSearchResults
        {
            public List<NuGet.Packaging.Core.PackageIdentity> PackageIdentities { get; } = new List<NuGet.Packaging.Core.PackageIdentity>();

            public HashSet<IPackageSearchMetadata> PackagesFilteredOutByVersion { get; set; } = new HashSet<IPackageSearchMetadata>();
        }

        public async Task<Package> GetPackageDetails(Core.PackageIdentity packageIdentity)
        {
            var nuGetIdentity = new NuGet.Packaging.Core.PackageIdentity(packageIdentity.Id, new NuGetVersion(packageIdentity.Version));
            var metadata = await _nugetClient.GetMetadataAsync(nuGetIdentity);
            return MapPackage(metadata);
        }

        private static Package MapPackage(IPackageSearchMetadata packageMetadata) =>
            new Package
            {
                Id = packageMetadata.Identity.Id,
                Authors = packageMetadata.Authors,
                Description = packageMetadata.Description,
                DownloadCount = packageMetadata.DownloadCount,
                LicenseUrl = packageMetadata.LicenseUrl,
                ProjectUrl = packageMetadata.ProjectUrl,
                Summary = packageMetadata.Summary,
                Tags = packageMetadata.Tags,
            };
    }
}
