using System;
using System.Collections.Generic;

namespace VMUnityAutomation.Editor
{
    /// <summary>Canonical package and contract identity for CLI consumers.</summary>
    internal static class VmAutomationIdentity
    {
        internal const string PackageVersion = "0.1.3";
        internal const string PackageId = "com.vm233.unity-automation";
        internal static string BuildDigest =>
            typeof(VmAutomationIdentity).Assembly.ManifestModule.ModuleVersionId.ToString("N");

        internal static Dictionary<string, object> ToDictionary()
        {
            return new Dictionary<string, object>
            {
                { "packageVersion", PackageVersion },
                { "packageId", PackageId },
                { "buildDigest", BuildDigest },
                { "toolMetadataSchemaVersion", VmAutomationContractMetadata.ToolMetadataSchemaVersion },
            };
        }
    }
}
