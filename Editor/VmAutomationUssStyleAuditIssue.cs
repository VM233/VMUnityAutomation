#if UNITY_EDITOR
using System;
using System.Collections.Generic;

namespace VMUnityAutomation.Editor
{
    internal sealed class VmAutomationUssStyleAuditIssue
    {
        public string AssetPath;
        public int Line;
        public string Selector;
        public string Token;
        public string Kind;
        public string Severity = "warning";
        public string Property;
        public string Value;
        public int GridStep;
        public Dictionary<string, string> OffGridDeclarations =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> RelatedDeclarations =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public int AuthoredUsageCount;
        public int RuntimeReferenceCount;
        public List<Dictionary<string, object>> UsageLocations =
            new List<Dictionary<string, object>>();
        public List<Dictionary<string, object>> StylesheetRules =
            new List<Dictionary<string, object>>();
        public List<string> RelatedSelectors = new List<string>();
        public bool Suppressed;
        public string SuppressionReason;
        public string Message;

        public bool IsError => string.Equals(Severity, "error",
            StringComparison.Ordinal);

        public Dictionary<string, object> ToDictionary()
        {
            var result = new Dictionary<string, object>
            {
                { "assetPath", AssetPath },
                { "line", Line },
                { "selector", Selector },
                { "token", Token },
                { "kind", Kind },
                { "severity", Severity },
                { "authoredUsageCount", AuthoredUsageCount },
                { "runtimeReferenceCount", RuntimeReferenceCount },
                { "usageLocations", UsageLocations },
                { "suppressed", Suppressed },
                { "suppressionReason", SuppressionReason ?? "" },
                { "message", Message }
            };
            if (RelatedSelectors.Count > 0)
            {
                result["relatedSelectors"] = new List<string>(RelatedSelectors);
            }

            if (string.IsNullOrWhiteSpace(Property) == false)
            {
                result["property"] = Property;
                result["value"] = Value ?? "";
                result["stylesheetRules"] = StylesheetRules;
            }
            else if (RelatedDeclarations.Count > 0)
            {
                result["declarations"] =
                    new Dictionary<string, string>(RelatedDeclarations,
                        StringComparer.OrdinalIgnoreCase);
            }
            else if (string.Equals(Kind, "off-grid-pixel-declarations",
                         StringComparison.Ordinal))
            {
                result["gridStep"] = GridStep;
                result["offGridDeclarations"] =
                    new Dictionary<string, string>(OffGridDeclarations,
                        StringComparer.OrdinalIgnoreCase);
            }

            return result;
        }
    }
}
#endif
