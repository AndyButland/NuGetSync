using System.Collections.Generic;
using System.Threading.Tasks;

namespace NuGetSync.Service
{
    public interface IPackageService
    {
        Task<int> GetPackageCountForSearchTerm(string searchTerm);

        Task<IReadOnlyCollection<Core.PackageIdentity>> GetTaggedUmbracoPackageIdentities(string tag, int skip, int take);

        Task<Core.Package> GetPackageDetails(Core.PackageIdentity packageIdentity);
    }
}
