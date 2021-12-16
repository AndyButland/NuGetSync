using NuGet.Common;
using NuGet.Packaging.Core;
using NuGet.Protocol;
using NuGet.Protocol.Core.Types;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NuGetSync.Service
{
    public class NuGetClient : INuGetClient
    {
        private readonly SourceRepository _repository;
        private PackageSearchResource _searchResource;
        private PackageMetadataResource _metaDataResource;

        public NuGetClient()
        {
            _repository = Repository.Factory.GetCoreV3("https://api.nuget.org/v3/index.json");
        }

        public async Task<IEnumerable<IPackageSearchMetadata>> SearchAsync(string searchTerm, SearchFilter filters, int skip, int take)
        {
            if (_searchResource == null)
            {
                _searchResource = await _repository.GetResourceAsync<PackageSearchResource>();
            }

            return await _searchResource.SearchAsync(
                    searchTerm,
                    filters,
                    skip,
                    take,
                    NullLogger.Instance,
                    CancellationToken.None);
        }

        public async Task<IPackageSearchMetadata> GetMetadataAsync(PackageIdentity packageIdentity)
        {
            if (_metaDataResource == null)
            {
                _metaDataResource = await _repository.GetResourceAsync<PackageMetadataResource>();
            }

            return await _metaDataResource.GetMetadataAsync(
                packageIdentity,
                new SourceCacheContext(),
                NullLogger.Instance,
                CancellationToken.None);
        }
    }
}
