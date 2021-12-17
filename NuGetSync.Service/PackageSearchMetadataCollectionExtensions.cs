using NuGet.Protocol.Core.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NuGetSync.Service
{
    internal static class PackageSearchMetadataCollectionExtensions
    {
        internal static IEnumerable<IPackageSearchMetadata> FilterByTag(
            this IEnumerable<IPackageSearchMetadata> searchResults,
            string tag)
            => searchResults.Where(x => FilterByTag(x, tag));

        private static bool FilterByTag(IPackageSearchMetadata searchResult, string tag) =>
            !string.IsNullOrEmpty(searchResult.Tags) &&
                searchResult.Tags.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => x.ToLower().Trim())
                    .Contains(tag.ToLower());

        internal static IAsyncEnumerable<IPackageSearchMetadata> FilterByUmbracoMinimumVersion(
            this IEnumerable<IPackageSearchMetadata> searchResults,
            INuGetClient nuGetClient,
            HashSet<IPackageSearchMetadata> packageIdsFilteredOutByVersion)
            => searchResults
                .ToAsyncEnumerable()
                .WhereAwait(async x => await FilterByUmbracoMinimumVersion(x, nuGetClient, packageIdsFilteredOutByVersion));

        private static async Task<bool> FilterByUmbracoMinimumVersion(
            IPackageSearchMetadata searchResult,
            INuGetClient nuGetClient,
            HashSet<IPackageSearchMetadata> packageIdsFilteredOutByVersion)
        {
            // Dependency information is not available in the search results, so we need to make an additional meta data request.
            // See: https://github.com/NuGet/Home/issues/8462
            var metaData = await nuGetClient.GetMetadataAsync(searchResult.Identity);
            if (metaData == null)
            {
                return false;
            }

            // We can make the dependency checks simpler, avoiding checking the version, just because we know that the
            // package Id prefix changed for V9.
            const string UmbracoV9AndAboveDependencyPrefix = "Umbraco.Cms.";
            const string UmbracoV8AndBelowDependencyPrefix = "UmbracoCms.";

            var hasUmbraco9OrAboveDependency = metaData.HasDependency(UmbracoV9AndAboveDependencyPrefix);

            // If we don't find a dependency on the Umbraco version provided, it could be still a package we want to include due
            // to it having a indirect Umbraco dependency (i.e. it depends on a package that depends on Umbraco).
            // So track the packages we are filtering out to potentially restore in a second pass.
            if (!hasUmbraco9OrAboveDependency)
            {
                // No Umbraco 9+ dependency, but do we have one on Umbraco 8 or lower?  If so, then it's a package only
                // for older versions, so we aren't interested.
                var hasUmbraco8OrBelowDependency = metaData.HasDependency(UmbracoV8AndBelowDependencyPrefix);
                if (!hasUmbraco8OrBelowDependency)
                {
                    packageIdsFilteredOutByVersion.Add(metaData);
                }
            }

            return hasUmbraco9OrAboveDependency;
        }
    }
}
