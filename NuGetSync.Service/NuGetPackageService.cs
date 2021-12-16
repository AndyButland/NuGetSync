using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using NuGetSync.Core;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NuGetSync.Service
{
    public class NuGetPackageService : IPackageService
    {
        public const string IdentifyingTag = "umbraco";

        private readonly INuGetClient _nugetClient;

        public NuGetPackageService(INuGetClient nugetClient)
        {
            _nugetClient = nugetClient;
        }

        public async Task<IReadOnlyCollection<Package>> GetUmbracoPackages()
        {
            // Get all packages that directly depend on Umbraco CMS V9+.
            var packagesFromSearch = await GetDirectlyDependentUmbracoPackages();

            // For those packages that matched by tag but were filtered out by version, check for any
            // dependencies they have on directly dependent packages we've found, and include them.
            var results = packagesFromSearch.Packages;
            results.AddRange(GetIndirectlyDependentUmbracoPackages(packagesFromSearch));

            return packagesFromSearch.Packages
                .OrderBy(x => x.Id)
                .ToList()
                .AsReadOnly();
        }

        private async Task<PackageSearchResults> GetDirectlyDependentUmbracoPackages()
        {
            var searchFilter = new SearchFilter(includePrerelease: false);

            const int PageSize = 100;

            var result = new PackageSearchResults();
            var skip = 0;
            while (true)
            {
                var searchResults = (await _nugetClient.SearchAsync(
                    IdentifyingTag,
                    searchFilter,
                    skip: skip,
                    take: PageSize)).ToList();

                if (!searchResults.Any())
                {
                    break;
                }

                // Search results return matches on Id and Description as well as tags, so given we only want packages that are
                // explicitly tagged, we'll filter out any that aren't.
                // Also filtering out any that don't support Umbraco 9 or above (requires an additional request, as the
                // search results don't contain any dependency information, so we'll do this filter last).
                searchResults = await searchResults
                    .FilterByTag(IdentifyingTag)
                    .FilterByUmbracoMinimumVersion(_nugetClient, result.PackagesFilteredOutByVersion)
                    .ToListAsync();

                result.Packages.AddRange(searchResults.Select(MapPackage));

                skip += PageSize;

                // Temp
                break;
            }

            return result;
        }

        private static Package MapPackage(IPackageSearchMetadata searchResult) => new()
        {
            Id = searchResult.Identity.Id,
            Description = searchResult.Description
        };

        private static IEnumerable<Package> GetIndirectlyDependentUmbracoPackages(PackageSearchResults packagesFromSearch)
        {
            var foundPackage = true;
            var results = new List<Package>();
            while (foundPackage)
            {
                foundPackage = false;
                var packagesToAdd = new HashSet<IPackageSearchMetadata>();

                foreach (var packageMetadata in packagesFromSearch.PackagesFilteredOutByVersion)
                {
                    // Check if the dependencies of the previously filtered out package include a package
                    // we've already included.
                    if (packageMetadata.DependencySets.SelectMany(x => x.Packages.Select(x => x.Id))
                        .Intersect(packagesFromSearch.Packages.Select(x => x.Id)).Any())
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
                    results.AddRange(packagesToAdd.Select(MapPackage));
                }
            }

            return results;
        }

        private class PackageSearchResults
        {
            public List<Package> Packages { get; } = new List<Package>();

            public HashSet<IPackageSearchMetadata> PackagesFilteredOutByVersion { get; set; } = new HashSet<IPackageSearchMetadata>();
        }
    }
}
