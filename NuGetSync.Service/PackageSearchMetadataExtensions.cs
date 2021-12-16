using NuGet.Protocol.Core.Types;
using System.Linq;

namespace NuGetSync.Service
{
    internal static class PackageSearchMetadataExtensions
    {
        internal static bool HasDependency(this IPackageSearchMetadata metaData, string packageIdPrefix) =>
            metaData.DependencySets != null &&
            metaData.DependencySets
                .Any(x => x.Packages
                    .Any(y => y.Id.StartsWith(packageIdPrefix)));
    }
}
