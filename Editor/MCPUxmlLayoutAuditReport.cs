#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;

namespace VMUnityAutomation.Editor
{
    internal sealed class MCPUxmlLayoutAuditReport
    {
        private readonly int maxIssues;
        private int activeIssueCount;
        private int suppressedIssueCount;
        private bool truncated;

        public readonly List<MCPUxmlLayoutAuditIssue> Issues =
            new List<MCPUxmlLayoutAuditIssue>();
        public readonly List<string> Errors = new List<string>();
        public int ScannedUxmlCount;
        public int IndexedUxmlCount;
        public int IndexedStyleSheetCount;
        public int IndexedRuntimeSourceCount;
        public int IndexedSerializedAssetCount;

        public MCPUxmlLayoutAuditReport(int maxIssues)
        {
            this.maxIssues = maxIssues;
        }

        public int WarningCount => activeIssueCount;
        public int SuppressedCount => suppressedIssueCount;
        internal bool Truncated => truncated;
        public bool Passed => Errors.Count == 0 && WarningCount == 0;

        public void Record(MCPUxmlLayoutAuditIssue issue, bool includeSuppressed)
        {
            if (issue.Suppressed)
            {
                suppressedIssueCount++;
                if (includeSuppressed == false)
                {
                    return;
                }
            }
            else
            {
                activeIssueCount++;
            }

            if (Issues.Count < maxIssues)
            {
                Issues.Add(issue);
            }
            else
            {
                truncated = true;
            }
        }

        public void SortIssues()
        {
            Issues.Sort((left, right) =>
            {
                var pathComparison = string.Compare(left.AssetPath, right.AssetPath,
                    System.StringComparison.Ordinal);
                if (pathComparison != 0)
                {
                    return pathComparison;
                }

                return left.Line.CompareTo(right.Line);
            });
        }

        public Dictionary<string, object> ToDictionary()
        {
            return new Dictionary<string, object>
            {
                { "success", Errors.Count == 0 },
                { "passed", Passed },
                { "scannedUxmlFiles", ScannedUxmlCount },
                { "indexedUxmlFiles", IndexedUxmlCount },
                { "indexedStyleSheets", IndexedStyleSheetCount },
                { "indexedRuntimeSourceFiles", IndexedRuntimeSourceCount },
                { "indexedSerializedAssetFiles", IndexedSerializedAssetCount },
                { "warningCount", WarningCount },
                { "suppressedCount", SuppressedCount },
                { "truncated", truncated },
                { "issues", Issues.Select(issue => issue.ToDictionary()).ToList() },
                { "errors", Errors.ToList() },
                {
                    "suppressionSyntax",
                    new[]
                    {
                        $"<!-- {MCPUxmlLayoutAuditor.SUPPRESSION_MARKER} <reason> -->",
                        $"<!-- {MCPUxmlLayoutAuditor.REPEATED_INLINE_SUPPRESSION_MARKER} <reason> -->",
                        $"<!-- {MCPUxmlLayoutAuditor.REDUNDANT_INLINE_SUPPRESSION_MARKER} <reason> -->",
                        $"<!-- {MCPUxmlLayoutAuditor.INERT_TEXT_STRETCH_SUPPRESSION_MARKER} <reason> -->",
                        $"<!-- {MCPUxmlLayoutAuditor.INERT_TEXT_GROW_SUPPRESSION_MARKER} <reason> -->",
                        $"<!-- {MCPUxmlLayoutAuditor.SINGLE_CHILD_CENTERING_WRAPPER_SUPPRESSION_MARKER} <reason> -->",
                        $"<!-- {MCPUxmlLayoutAuditor.FIXED_SCROLL_CROSS_AXIS_SIZE_SUPPRESSION_MARKER} <reason> -->",
                        $"<!-- {MCPUxmlLayoutAuditor.UNCONSUMED_ELEMENT_NAME_SUPPRESSION_MARKER} <reason> -->",
                        $"<!-- {MCPUxmlLayoutAuditor.FIXED_FLEX_PARTITION_SUPPRESSION_MARKER} <reason> -->",
                        $"<!-- {MCPUxmlNaturalFlowLayoutAuditor.SUPPRESSION_MARKER} <reason> -->",
                        $"<!-- {MCPUxmlNaturalFlowLayoutAuditor.FIXED_NATURAL_CROSS_SIZE_SUPPRESSION_MARKER} <reason> -->",
                        $"<!-- {MCPUxmlNaturalFlowLayoutAuditor.SCROLL_AXIS_FLEX_SHRINK_SUPPRESSION_MARKER} <reason> -->",
                        $"<!-- {MCPUxmlLayoutAuditor.PIXEL_GRID_SUPPRESSION_MARKER} <reason> -->",
                        $"<!-- {MCPUxmlLayoutAuditor.TOOLTIP_ATTRIBUTE_SUPPRESSION_MARKER} <reason> -->"
                    }
                }
            };
        }
    }
}
#endif
