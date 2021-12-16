using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NuGetSync.Service
{
    public interface INuGetClient
    {
        Task<IEnumerable<IPackageSearchMetadata>> SearchAsync(string searchTerm, SearchFilter filters, int skip, int take);

        Task<IPackageSearchMetadata> GetMetadataAsync(PackageIdentity packageIdentity);
    }
}
