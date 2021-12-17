using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NuGetSync.Core;
using NuGetSync.Service;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace NuGetSync.Console
{
    class Program
    {
        static async Task Main(string[] args)
        {
            using var host = CreateHostBuilder(args).Build();

            using var serviceScope = host.Services.CreateScope();
            var provider = serviceScope.ServiceProvider;

            var packageService = provider.GetRequiredService<IPackageService>();

            // Expected to be "umbraco-marketplace", but using "umbraco" for now.
            const string IdentifyingTag = "umbraco";

            // Get total packages matching search term.
            var totalPackagesMatchingSearchTerm = await GetTotalPackagesMatchingSearchTerm(packageService, IdentifyingTag);
            System.Console.WriteLine($"Total number of packages found: {totalPackagesMatchingSearchTerm}");

            // Initiate fan-out/fan-in operation retrieving packages from NuGet.
            const int ChunkSize = 50;
            var retrievePackageIdentitiesTasks = new List<Task<IReadOnlyCollection<PackageIdentity>>>();
            for (int i = 0; i < totalPackagesMatchingSearchTerm; i+= ChunkSize)
            {
                retrievePackageIdentitiesTasks.Add(GetPackageIdentities(packageService, IdentifyingTag, i, ChunkSize));
            }

            await Task.WhenAll(retrievePackageIdentitiesTasks);

            var packageIdentities = retrievePackageIdentitiesTasks
                .SelectMany(x => x.Result)
                .Distinct()
                .OrderBy(x => x.Id)
                .ToList();

            System.Console.WriteLine($"Number of filtered package identities: {packageIdentities.Count}");

            // Initiate fan-out/fan-in operation to retrieve full metadata for each package.
            var retrievePackageDetailsTasks = new List<Task<Package>>();
            foreach (var packageId in packageIdentities)
            {
                retrievePackageDetailsTasks.Add(GetPackageDetails(packageService, packageId));
            }

            await Task.WhenAll(retrievePackageDetailsTasks);

            var packages = retrievePackageDetailsTasks
                .Select(x => x.Result)
                .OrderBy(x => x.Id)
                .ToList();

            System.Console.WriteLine($"Number of packages retrieved: {packages.Count}");

            foreach (var package in packages)
            {
                System.Console.WriteLine($" - {package.Id}");
                System.Console.WriteLine($"      Description: {package.Description.Truncate(50)}");
                System.Console.WriteLine($"      Authors: {package.Authors}");
                System.Console.WriteLine($"      Tags: {package.Tags}");
                System.Console.WriteLine();
            }

            await host.RunAsync();
        }

        private static IHostBuilder CreateHostBuilder(string[] args)
            => Host.CreateDefaultBuilder(args)
                .ConfigureServices((_, services) =>
                    services
                        .AddTransient<IPackageService, NuGetPackageService>()
                        .AddTransient<INuGetClient, NuGetClient>()
                        .AddHttpClient("NuGetSearch", httpClient =>
                        {
                            httpClient.BaseAddress = new Uri("https://azuresearch-usnc.nuget.org/query");
                        }));

        private static async Task<int> GetTotalPackagesMatchingSearchTerm(IPackageService packageService, string identifyingTag) =>
            await packageService.GetPackageCountForSearchTerm(identifyingTag);

        private static async Task<IReadOnlyCollection<PackageIdentity>> GetPackageIdentities(IPackageService packageService, string tag, int skip, int chunkSize)
            => await packageService.GetTaggedUmbracoPackageIdentities(tag, skip, chunkSize);

        private static async Task<Package> GetPackageDetails(IPackageService packageService, PackageIdentity packageId)
            => await packageService.GetPackageDetails(packageId);
    }
}