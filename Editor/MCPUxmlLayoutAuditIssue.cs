#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;

namespace VMUnityAutomation.Editor
{
    internal sealed class MCPUxmlLayoutAuditIssue
    {
        public string AssetPath;
        public int Line;
        public string Element;
        public string ElementName;
        public string Kind;
        public string Axis;
        public List<string> FixedProperties = new List<string>();
        public float ParentSize;
        public float Offset;
        public float Size;
        public int GridStep;
        public string BaseClass;
        public int AuthoredUsageCount;
        public string AttributeName;
        public string AttributeValue;
        public Dictionary<string, string> InlineDeclarations =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public List<string> RelatedVariantClasses = new List<string>();
        public List<Dictionary<string, object>> UsageLocations =
            new List<Dictionary<string, object>>();
        public List<Dictionary<string, object>> StylesheetRules =
            new List<Dictionary<string, object>>();
        public bool Suppressed;
        public string SuppressionReason;
        public string Message;

        public Dictionary<string, object> ToDictionary()
        {
            var result = new Dictionary<string, object>
            {
                { "assetPath", AssetPath },
                { "line", Line },
                { "element", Element },
                { "elementName", ElementName ?? "" },
                { "kind", Kind },
                { "axis", Axis },
                { "fixedProperties", FixedProperties.ToList() },
                { "suppressed", Suppressed },
                { "suppressionReason", SuppressionReason ?? "" },
                { "message", Message }
            };
            if (string.Equals(Kind, "manual-centered-layout-box",
                    StringComparison.Ordinal))
            {
                result["parentSize"] = ParentSize;
                result["offset"] = Offset;
                result["size"] = Size;
            }
            else if (string.Equals(Kind, "repeated-inline-layout-variant",
                    StringComparison.Ordinal))
            {
                result["baseClass"] = BaseClass ?? "";
                result["authoredUsageCount"] = AuthoredUsageCount;
                result["inlineDeclarations"] =
                    new Dictionary<string, string>(InlineDeclarations,
                        StringComparer.OrdinalIgnoreCase);
                result["relatedVariantClasses"] = RelatedVariantClasses.ToList();
                result["usageLocations"] = UsageLocations.ToList();
            }
            else if (string.Equals(Kind, "manual-absolute-sibling-layout",
                         StringComparison.Ordinal))
            {
                result["baseClass"] = BaseClass ?? "";
                result["authoredUsageCount"] = AuthoredUsageCount;
                result["size"] = Size;
                result["usageLocations"] = UsageLocations.ToList();
            }
            else if (string.Equals(Kind, "fixed-natural-flow-cross-size",
                         StringComparison.Ordinal))
            {
                result["authoredUsageCount"] = AuthoredUsageCount;
                result["size"] = Size;
            }
            else if (string.Equals(Kind, "redundant-inline-declaration",
                         StringComparison.Ordinal))
            {
                result["inlineDeclarations"] =
                    new Dictionary<string, string>(InlineDeclarations,
                        StringComparer.OrdinalIgnoreCase);
                result["stylesheetRules"] = StylesheetRules.ToList();
            }
            else if (string.Equals(Kind, "ineffective-scroll-axis-flex-shrink",
                         StringComparison.Ordinal))
            {
                result["declarations"] =
                    new Dictionary<string, string>(InlineDeclarations,
                        StringComparer.OrdinalIgnoreCase);
            }
            else if (string.Equals(Kind, "visually-inert-text-stretch",
                         StringComparison.Ordinal) ||
                     string.Equals(Kind, "visually-inert-text-grow",
                         StringComparison.Ordinal) ||
                     string.Equals(Kind, "single-child-centering-wrapper",
                         StringComparison.Ordinal) ||
                     string.Equals(Kind, "fixed-scroll-cross-axis-content-size",
                         StringComparison.Ordinal) ||
                     string.Equals(Kind, "unconsumed-element-name",
                         StringComparison.Ordinal) ||
                     string.Equals(Kind, "fixed-flex-partition",
                         StringComparison.Ordinal))
            {
                result["inlineDeclarations"] =
                    new Dictionary<string, string>(InlineDeclarations,
                        StringComparer.OrdinalIgnoreCase);
                if (string.Equals(Kind, "fixed-flex-partition",
                        StringComparison.Ordinal))
                {
                    result["size"] = Size;
                }
                else if (string.Equals(Kind, "fixed-scroll-cross-axis-content-size",
                             StringComparison.Ordinal))
                {
                    result["parentSize"] = ParentSize;
                    result["size"] = Size;
                }
            }
            else if (string.Equals(Kind, "off-grid-pixel-declarations",
                          StringComparison.Ordinal))
            {
                result["gridStep"] = GridStep;
                result["inlineDeclarations"] =
                    new Dictionary<string, string>(InlineDeclarations,
                        StringComparer.OrdinalIgnoreCase);
            }
            else if (string.Equals(Kind, "authored-tooltip-attribute",
                         StringComparison.Ordinal))
            {
                result["attributeName"] = AttributeName ?? "";
                result["attributeValue"] = AttributeValue ?? "";
            }

            return result;
        }
    }
}
#endif
