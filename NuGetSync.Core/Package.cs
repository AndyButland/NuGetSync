using System;

namespace NuGetSync.Core
{
    public class Package
    {
        public string Id { get; set; }

        public string Authors { get; set; }

        public string Description { get; set; }

        public long? DownloadCount { get; set; }

        public Uri LicenseUrl { get; set; }

        public Uri ProjectUrl { get; set; }

        public string Summary { get; set; }

        public string Tags { get; set; }
    }
}
