#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace VMUnityAutomation.Editor
{
    /// <summary>
    /// Central registry for safe cross-tool defaults. A request value always wins.
    /// Only response-budget fields are injected here; mutation choices, selectors,
    /// paths, dry-run flags and safety caps remain owned by each request/tool.
    /// </summary>
    internal static class VmAutomationToolConfigurationPolicy
    {
        // Updating either authoritative route manifest requires re-running the
        // configuration audit and updating its owner-specific fingerprint. Core and
        // optional-provider routes are audited separately so installing an optional
        // package cannot change the expected core manifest.
        internal const string AuditedCoreRouteManifestSha256 =
            "f57ba17160d466579aa6a21eb484c1004e059d1cc6d370da20b9d35abf36b9e5";

        internal const string AuditedLocalizationRouteManifestSha256 =
            "529ee6b1ed8b861605b09776fe1883a9d5b9cb054455691ea029568a7b8bcda0";

        private static readonly Dictionary<string, string> ResultLimitArguments =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "addressables/info", "limit" },
                { "asset/dependencies", "limit" },
                { "asset/list", "limit" },
                { "build/profile", "limit" },
                { "cinemachine/info", "limit" },
                { "console/query", "count" },
                { "debug/stack-trace", "maxFrames" },
                { "editor/execute-code", "maxResultItems" },
                { "jobs/list", "limit" },
                { "localization/entries", "limit" },
                { "localization/validate", "maxIssues" },
                { "material/properties/get", "limit" },
                { "packages/lint-metas", "maxResults" },
                { "packages/list", "limit" },
                { "packages/search", "limit" },
                { "physics/overlap-box", "maxResults" },
                { "physics/overlap-sphere", "maxResults" },
                { "physics/raycast", "maxResults" },
                { "prefab-asset/find", "maxResults" },
                { "prefab-asset/hierarchy", "maxNodes" },
                { "profiler/frame-data", "maxItems" },
                { "profiler/memory-breakdown", "maxPerCategory" },
                { "profiler/memory-top-assets", "count" },
                { "project-auditor/audit", "limit" },
                { "scene/hierarchy", "maxNodes" },
                { "search/missing-references", "limit" },
                { "search/scene", "limit" },
                { "serialized-object/get", "maxProperties" },
                { "shadergraph/get-node-types", "maxResults" },
                { "shadergraph/list", "maxResults" },
                { "shadergraph/list-shaders", "maxResults" },
                { "terrain/get-tree-instances", "limit" },
                { "testing/get-job", "limit" },
                { "testing/list-tests", "maxResults" },
                { "texture/find-duplicates", "maxGroups" },
                { "uitoolkit/asset-inspect", "maxResults" },
                { "uitoolkit/audit-uss-styles", "maxIssues" },
                { "uitoolkit/audit-uxml-layout", "maxIssues" },
                { "uitoolkit/query", "maxResults" },
                { "uitoolkit/runtime-query", "maxResults" },
                { "uitoolkit/runtime-tree", "maxNodes" },
                { "uitoolkit/tree", "maxNodes" }
            };

        private static readonly Dictionary<string, string> ProjectDefaultArguments =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "physics/overlap-box", "dimension" },
                { "physics/overlap-sphere", "dimension" },
                { "physics/raycast", "dimension" },
                { "screenshot/editor-window", "path" },
                { "screenshot/game", "path" },
                { "screenshot/scene", "path" },
                { "uitoolkit/builder-preview", "screenshotPath" }
            };

        private static readonly HashSet<string> PrefabDiffRoutes =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "prefab-asset/add-component",
                "prefab-asset/add-gameobject",
                "prefab-asset/cleanup-missing-overrides",
                "prefab-asset/configure-component",
                "prefab-asset/move-component",
                "prefab-asset/remove-component",
                "prefab-asset/remove-gameobject",
                "prefab-asset/set-property",
                "prefab-asset/set-reference",
                "prefab-asset/transaction-edit"
            };

        internal static IReadOnlyDictionary<string, string>
            ConfigurableResultLimitArguments => ResultLimitArguments;

        internal static void ApplyDefaults(
            string route, IDictionary<string, object> arguments)
        {
            if (arguments == null ||
                !VmAutomationSettings.OverrideDefaultResultLimit ||
                !ResultLimitArguments.TryGetValue(
                    (route ?? "").Trim('/'), out string argumentName) ||
                arguments.ContainsKey(argumentName))
            {
                return;
            }

            arguments[argumentName] = VmAutomationSettings.DefaultResultLimit;
        }

        internal static void AnnotateInputSchema(
            string route, IDictionary<string, object> schema)
        {
            if (schema == null ||
                !schema.TryGetValue("properties", out object propertiesValue) ||
                !(propertiesValue is IDictionary<string, object> properties))
            {
                return;
            }

            route = (route ?? "").Trim('/');
            if (ResultLimitArguments.TryGetValue(
                    route, out string resultArgument))
            {
                AnnotateProperty(
                    properties,
                    resultArgument,
                    "VM Unity Automation user default result limit (when override is enabled)");
            }

            if (ProjectDefaultArguments.TryGetValue(
                    route, out string projectArgument))
            {
                AnnotateProperty(
                    properties,
                    projectArgument,
                    "ProjectSettings/VMUnityAutomationSettings.json toolDefaults");
            }

            if (PrefabDiffRoutes.Contains(route))
            {
                AnnotateProperty(
                    properties,
                    "includePrefabFileDiff",
                    "VM Unity Automation user preference for prefab YAML diffs");
            }
        }

        internal static string ComputeRouteManifestSha256(
            IEnumerable<string> routes)
        {
            string canonical = string.Join(
                "\n",
                (routes ?? Enumerable.Empty<string>())
                    .Where(route => !string.IsNullOrWhiteSpace(route))
                    .Select(route => route.Trim('/'))
                    .OrderBy(route => route, StringComparer.Ordinal));
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(canonical));
                return BitConverter.ToString(hash).Replace("-", "")
                    .ToLowerInvariant();
            }
        }

        private static void AnnotateProperty(
            IDictionary<string, object> properties,
            string propertyName,
            string source)
        {
            if (!properties.TryGetValue(propertyName, out object propertyValue) ||
                !(propertyValue is IDictionary<string, object> property))
            {
                return;
            }

            property["x-vmAutomationDefaultSource"] = source;
            property["x-vmAutomationExplicitValueWins"] = true;
        }
    }
}
#endif
