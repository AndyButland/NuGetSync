using Moq;
using NuGet.Frameworks;
using NuGet.Packaging;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;
using NuGet.Versioning;
using NUnit.Framework;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace NuGetSync.Service.Tests
{
    public class NuGetPackageServiceTests
    {
        private Mock<INuGetClient> _mockNuGetClient;
        private Mock<IHttpClientFactory> _mockHttpClientFactory;
        private NuGetPackageService _sut;

        private const string IdentifyingTag = "umbraco-marketplace";

        [SetUp]
        public void Setup()
        {
            _mockNuGetClient = new Mock<INuGetClient>();
            _mockHttpClientFactory = new Mock<IHttpClientFactory>();
            _sut = new NuGetPackageService(_mockNuGetClient.Object, _mockHttpClientFactory.Object);
        }

        [Test]
        public async Task GetUmbracoPackages_GetsDirectDependencies()
        {
            var testPackage1 = new PackageMetaDataOptions { Id = "TestPackage1" };
            var testPackage2 = new PackageMetaDataOptions { Id = "TestPackage2", WithIdentifyingTag = false };
            var testPackage3 = new PackageMetaDataOptions { Id = "TestPackage3", WithDirectDependency = false };

            _mockNuGetClient
                .Setup(x => x.SearchAsync(It.Is<string>(y => y == IdentifyingTag), It.IsAny<SearchFilter>(), It.IsAny<int>(), It.IsAny<int>()))
                .ReturnsAsync(new List<IPackageSearchMetadata>
                {
                    MockPackageSearchMetaData(testPackage1),
                    MockPackageSearchMetaData(testPackage2),
                    MockPackageSearchMetaData(testPackage3),
                });
            _mockNuGetClient
                .Setup(x => x.GetMetadataAsync(It.Is<PackageIdentity>(y => y.Id == testPackage1.Id)))
                .ReturnsAsync(MockPackageSearchMetaData(testPackage1));

            var results = await _sut.GetTaggedUmbracoPackageIdentities(IdentifyingTag, 0, 50);

            Assert.AreEqual(1, results.Count);

            var packageIdentity = results.First();
            Assert.AreEqual("TestPackage1", packageIdentity.Id);
        }

        [Test]
        public async Task GetUmbracoPackages_GetsIndirectDependencies()
        {
            var testPackage1 = new PackageMetaDataOptions { Id = "TestPackage1" }; // Directly dependent.
            var testPackage2 = new PackageMetaDataOptions { Id = "TestPackage2", WithIdentifyingTag = false };
            var testPackage3 = new PackageMetaDataOptions { Id = "TestPackage3", WithDirectDependency = false };
            var testPackage4 = new PackageMetaDataOptions { Id = "TestPackage4", WithDirectDependency = false, WithDependencyOn = testPackage1.Id };  // Indirectly dependent, once removed.
            var testPackage5 = new PackageMetaDataOptions { Id = "TestPackage5", WithDirectDependency = false, WithDependencyOn = "UmbracoCms.Core" };
            var testPackage6 = new PackageMetaDataOptions { Id = "TestPackage6", WithDirectDependency = false, WithDependencyOn = testPackage4.Id };  // Indirectly dependent, twice removed.

            _mockNuGetClient
                .Setup(x => x.SearchAsync(It.Is<string>(y => y == IdentifyingTag), It.IsAny<SearchFilter>(), It.IsAny<int>(), It.IsAny<int>()))
                .ReturnsAsync(new List<IPackageSearchMetadata>
                {
                    MockPackageSearchMetaData(testPackage1),
                    MockPackageSearchMetaData(testPackage2),
                    MockPackageSearchMetaData(testPackage3),
                    MockPackageSearchMetaData(testPackage4),
                    MockPackageSearchMetaData(testPackage5),
                    MockPackageSearchMetaData(testPackage6),
                });
            _mockNuGetClient
                .Setup(x => x.GetMetadataAsync(It.Is<PackageIdentity>(y => y.Id == testPackage1.Id)))
                .ReturnsAsync(MockPackageSearchMetaData(testPackage1));
            _mockNuGetClient
                .Setup(x => x.GetMetadataAsync(It.Is<PackageIdentity>(y => y.Id == testPackage4.Id)))
                .ReturnsAsync(MockPackageSearchMetaData(testPackage4));
            _mockNuGetClient
                .Setup(x => x.GetMetadataAsync(It.Is<PackageIdentity>(y => y.Id == testPackage5.Id)))
                .ReturnsAsync(MockPackageSearchMetaData(testPackage5));
            _mockNuGetClient
                .Setup(x => x.GetMetadataAsync(It.Is<PackageIdentity>(y => y.Id == testPackage6.Id)))
                .ReturnsAsync(MockPackageSearchMetaData(testPackage6));

            var results = await _sut.GetTaggedUmbracoPackageIdentities(IdentifyingTag, 0, 50);

            Assert.AreEqual(3, results.Count);

            var directDependencyPackageIdentity = results.First();
            Assert.AreEqual("TestPackage1", directDependencyPackageIdentity.Id);

            var indirectDependencyPackageIdentity = results.Skip(1).First();
            Assert.AreEqual("TestPackage4", indirectDependencyPackageIdentity.Id);

            var secondLevelIndirectDependencyPackageIdentity = results.Skip(2).First();
            Assert.AreEqual("TestPackage6", secondLevelIndirectDependencyPackageIdentity.Id);
        }

        private static IPackageSearchMetadata MockPackageSearchMetaData(PackageMetaDataOptions options)
        {
            var mockMetaData = new Mock<IPackageSearchMetadata>();
            mockMetaData
                .SetupGet(x => x.Identity)
                .Returns(new PackageIdentity(options.Id, new NuGetVersion("1.0.0")));
            mockMetaData
                .SetupGet(x => x.Tags)
                .Returns($"foo, {(options.WithIdentifyingTag ? IdentifyingTag : "bar")}, baz");
            mockMetaData
                .SetupGet(x => x.DependencySets)
                .Returns(new List<PackageDependencyGroup>
                {
                    new PackageDependencyGroup(NuGetFramework.AnyFramework, new List<PackageDependency>
                    {
                        new PackageDependency(options.WithDirectDependency ? "Umbraco.Cms.Core" : "Some.Other.Dependency", new VersionRange(new NuGetVersion("9.0.0"))),
                        new PackageDependency(!string.IsNullOrEmpty(options.WithDependencyOn) ? options.WithDependencyOn : "Yet.Other.Dependency", new VersionRange(new NuGetVersion("1.0.0")))
                    })
                });
            return mockMetaData.Object;
        }

        private class PackageMetaDataOptions
        {
            public string Id { get; set; }

            public bool WithIdentifyingTag { get; set; } = true;

            public bool WithDirectDependency { get; set; } = true;

            public string WithDependencyOn { get; set; }
        }
    }
}

