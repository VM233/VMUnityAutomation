using System;
using System.Collections.Generic;

namespace VMUnityAutomation.Editor
{
    public sealed class VmFolderOrganizationResult
    {
        public bool Success { get; set; }
        public bool Passed { get; set; }
        public bool RequiresReview { get; set; }
        public string ConfigurationPath { get; set; }
        public string ConfigurationSource { get; set; }
        public string[] Roots { get; set; } = Array.Empty<string>();
        public int ScannedDirectories { get; set; }
        public int ScannedEntries { get; set; }
        public int IgnoredMetaFiles { get; set; }
        public int ExcludedDirectories { get; set; }
        public int SkippedLinks { get; set; }
        public int IssueCount { get; set; }
        public bool Truncated { get; set; }
        public List<Finding> Issues { get; set; } = new List<Finding>();

        public sealed class Finding
        {
            public string Path { get; set; }
            public string PolicyFolder { get; set; }
            public string Severity { get; set; } = "warning";
            public int DirectFiles { get; set; }
            public int DirectSubfolders { get; set; }
            public int MaxDirectFiles { get; set; }
            public int MaxDirectSubfolders { get; set; }
            public string[] SampleFiles { get; set; } = Array.Empty<string>();
            public string Message { get; set; }
        }
    }
}
