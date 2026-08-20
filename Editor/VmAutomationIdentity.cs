using System;
using System.Collections.Generic;
using UnityEditor.PackageManager;

namespace VMUnityAutomation.Editor
{
    /// <summary>Canonical package and contract identity for CLI consumers.</summary>
    internal static class VmAutomationIdentity
    {
        internal static string PackageVersion => ResolvePackage().version;
        internal static string PackageId => ResolvePackage().packageId;
        internal static string BuildDigest =>
            typeof(VmAutomationIdentity).Assembly.ManifestModule.ModuleVersionId.ToString("N");

        internal static Dictionary<string, object> ToDictionary()
        {
            PackageInfo package = ResolvePackage();
            return new Dictionary<string, object>
            {
                { "packageVersion", package.version },
                { "packageId", package.packageId },
                { "buildDigest", BuildDigest },
                { "toolMetadataSchemaVersion", MCPContractMetadata.ToolMetadataSchemaVersion },
            };
        }

        private static PackageInfo ResolvePackage()
        {
            PackageInfo package = PackageInfo.FindForAssembly(typeof(VmAutomationIdentity).Assembly);
            if (package == null)
                throw new InvalidOperationException(
                    "VMUnityAutomation assembly is not owned by a registered Unity package.");
            return package;
        }
    }
}
