#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;

namespace VMUnityAutomation.Editor
{
    internal sealed class VmAutomationUxmlLayoutAuditReport
    {
        private readonly int maxIssues;
        private int activeIssueCount;
        private int suppressedIssueCount;
        private bool truncated;

        public readonly List<VmAutomationUxmlLayoutAuditIssue> Issues =
            new List<VmAutomationUxmlLayoutAuditIssue>();
        public readonly List<string> Errors = new List<string>();
        public int ScannedUxmlCount;
        public int IndexedUxmlCount;
        public int IndexedStyleSheetCount;
        public int IndexedRuntimeSourceCount;
        public int IndexedSerializedAssetCount;

        public VmAutomationUxmlLayoutAuditReport(int maxIssues)
        {
            this.maxIssues = maxIssues;
        }

        public int WarningCount => activeIssueCount;
        public int SuppressedCount => suppressedIssueCount;
        internal bool Truncated => truncated;
        public bool Passed => Errors.Count == 0 && WarningCount == 0;

        public void Record(VmAutomationUxmlLayoutAuditIssue issue, bool includeSuppressed)
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
                        $"<!-- {VmAutomationUxmlLayoutAuditor.SUPPRESSION_MARKER} <reason> -->",
                        $"<!-- {VmAutomationUxmlLayoutAuditor.REPEATED_INLINE_SUPPRESSION_MARKER} <reason> -->",
                        $"<!-- {VmAutomationUxmlLayoutAuditor.REDUNDANT_INLINE_SUPPRESSION_MARKER} <reason> -->",
                        $"<!-- {VmAutomationUxmlLayoutAuditor.INERT_TEXT_STRETCH_SUPPRESSION_MARKER} <reason> -->",
                        $"<!-- {VmAutomationUxmlLayoutAuditor.INERT_TEXT_GROW_SUPPRESSION_MARKER} <reason> -->",
                        $"<!-- {VmAutomationUxmlLayoutAuditor.SINGLE_CHILD_CENTERING_WRAPPER_SUPPRESSION_MARKER} <reason> -->",
                        $"<!-- {VmAutomationUxmlLayoutAuditor.FIXED_SCROLL_CROSS_AXIS_SIZE_SUPPRESSION_MARKER} <reason> -->",
                        $"<!-- {VmAutomationUxmlLayoutAuditor.UNCONSUMED_ELEMENT_NAME_SUPPRESSION_MARKER} <reason> -->",
                        $"<!-- {VmAutomationUxmlLayoutAuditor.FIXED_FLEX_PARTITION_SUPPRESSION_MARKER} <reason> -->",
                        $"<!-- {VmAutomationUxmlNaturalFlowLayoutAuditor.SUPPRESSION_MARKER} <reason> -->",
                        $"<!-- {VmAutomationUxmlNaturalFlowLayoutAuditor.FIXED_NATURAL_CROSS_SIZE_SUPPRESSION_MARKER} <reason> -->",
                        $"<!-- {VmAutomationUxmlNaturalFlowLayoutAuditor.SCROLL_AXIS_FLEX_SHRINK_SUPPRESSION_MARKER} <reason> -->",
                        $"<!-- {VmAutomationUxmlLayoutAuditor.PIXEL_GRID_SUPPRESSION_MARKER} <reason> -->",
                        $"<!-- {VmAutomationUxmlLayoutAuditor.TOOLTIP_ATTRIBUTE_SUPPRESSION_MARKER} <reason> -->"
                    }
                }
            };
        }
    }
}
#endif
