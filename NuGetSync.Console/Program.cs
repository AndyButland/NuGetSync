using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NuGetSync.Service;
using System;
using System.Threading.Tasks;

namespace NuGetSync.Console
{
    class Program
    {
        static async Task Main(string[] args)
        {
            using var host = CreateHostBuilder(args).Build();

            await ListUmbracoPackages(host.Services);

            await host.RunAsync();
        }

        private static IHostBuilder CreateHostBuilder(string[] args)
        {
            return Host.CreateDefaultBuilder(args)
                .ConfigureServices((_, services) =>
                    services
                        .AddTransient<IPackageService, NuGetPackageService>()
                        .AddTransient<INuGetClient, NuGetClient>());
        }

        private static async Task ListUmbracoPackages(IServiceProvider services)
        {
            using var serviceScope = services.CreateScope();
            var provider = serviceScope.ServiceProvider;

            var packageService = provider.GetRequiredService<IPackageService>();

            var packages = await packageService.GetUmbracoPackages();

            System.Console.WriteLine($"{packages.Count} package{(packages.Count == 1 ? string.Empty : "s")} found: ");
            foreach (var package in packages)
            {
                System.Console.WriteLine($" - {package.Id}");
            }
        }
    }
}