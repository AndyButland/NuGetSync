using NuGetSync.Core;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace NuGetSync.Service
{
    public interface IPackageService
    {
        Task<IReadOnlyCollection<Package>> GetUmbracoPackages();
    }
}
