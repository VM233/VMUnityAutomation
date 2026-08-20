using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace VMUnityAutomation.Editor
{
    internal static class VmProjectToolCatalogMetadata
    {
        private static readonly string[] CapabilityVerbPrefixes =
        {
            "activate-", "add-", "create-", "delete-", "find-", "get-",
            "inspect-", "list-", "remove-", "set-", "spawn-", "teleport-",
            "trace-", "update-", "upsert-", "validate-",
        };

        public static string ResolveModuleId(string declaredModuleId, string toolName,
            Assembly assembly)
        {
            if (string.IsNullOrWhiteSpace(declaredModuleId) == false)
                return declaredModuleId.Trim();

            string[] segments = SplitPath(toolName);
            if (segments.Length > 0)
                return segments[0];

            return (assembly?.GetName().Name ?? "project").ToLowerInvariant();
        }

        public static string ResolveOwnerPackage(Assembly assembly, string moduleId)
        {
            VmProjectToolPackageAttribute declaration = assembly?
                .GetCustomAttribute<VmProjectToolPackageAttribute>();
            if (string.IsNullOrWhiteSpace(declaration?.PackageId) == false)
                return declaration.PackageId;

            string projectModule = string.IsNullOrWhiteSpace(moduleId)
                ? (assembly?.GetName().Name ?? "project").ToLowerInvariant()
                : moduleId.Trim().ToLowerInvariant();
            return "project:" + projectModule;
        }

        public static string ResolveCapability(string declaredCapability, string toolName)
        {
            if (string.IsNullOrWhiteSpace(declaredCapability) == false)
                return declaredCapability.Trim();

            string[] segments = SplitPath(toolName);
            string capability = segments.Length > 1
                ? segments[1]
                : segments.FirstOrDefault() ?? "project";
            foreach (string prefix in CapabilityVerbPrefixes)
            {
                if (capability.StartsWith(prefix, StringComparison.Ordinal) &&
                    capability.Length > prefix.Length)
                {
                    return capability.Substring(prefix.Length);
                }
            }

            return capability;
        }

        public static string ResolveOperationKind(string declaredOperationKind, bool readOnly,
            bool longRunning)
        {
            if (string.IsNullOrWhiteSpace(declaredOperationKind) == false)
                return declaredOperationKind.Trim();
            if (longRunning)
                return "job";
            return readOnly ? "inspect" : "mutate";
        }

        public static List<string> NormalizeStringList(IEnumerable<string> values)
        {
            return values == null
                ? new List<string>()
                : values.Where(value => string.IsNullOrWhiteSpace(value) == false)
                    .Select(value => value.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
                    .ToList();
        }

        public static List<string> NormalizeSearchTerms(IEnumerable<string> declaredTerms,
            string toolName, string description)
        {
            var terms = NormalizeStringList(declaredTerms);
            terms.AddRange(SplitToolName(toolName));
            if (string.IsNullOrWhiteSpace(description) == false)
                terms.Add(description.Trim());
            return NormalizeStringList(terms);
        }

        public static List<string> NormalizePreconditions(IEnumerable<string> declaredPreconditions,
            bool requiresPlayMode)
        {
            var preconditions = NormalizeStringList(declaredPreconditions);
            if (requiresPlayMode)
                preconditions.Add("playMode");
            return NormalizeStringList(preconditions);
        }

        private static string[] SplitToolName(string toolName)
        {
            return (toolName ?? "")
                .Split(new[] { '/', '-', '_', '.', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(segment => segment.Trim().ToLowerInvariant())
                .Where(segment => segment.Length > 0)
                .ToArray();
        }

        private static string[] SplitPath(string toolName)
        {
            return (toolName ?? "")
                .Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(segment => segment.Trim().ToLowerInvariant())
                .Where(segment => segment.Length > 0)
                .ToArray();
        }
    }
}
