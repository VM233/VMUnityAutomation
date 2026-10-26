using System;
using System.ComponentModel;

namespace VMUnityAutomation.Editor
{
    public sealed class VmFolderOrganizationRequest
    {
        [VmJsonProperty("paths")]
        [Description("Optional existing project-relative directories inside the configured folder policy. Narrows scope only; at most 64 paths.")]
        public string[] Paths { get; set; } = Array.Empty<string>();

        [VmJsonProperty("maxIssues"), VmRange(1, 5000)]
        [Description("Maximum findings returned. Defaults to 200. Counts cover the complete scan; this does not limit coverage.")]
        public int MaxIssues { get; set; } = 200;
    }
}
