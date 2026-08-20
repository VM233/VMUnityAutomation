#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;

namespace VMUnityAutomation.Editor
{
    internal sealed class MCPUssStyleAuditReport
    {
        private readonly int maxIssues;
        private int activeErrorCount;
        private int activeWarningCount;
        private int suppressedIssueCount;
        private bool truncated;

        public readonly List<MCPUssStyleAuditIssue> Issues =
            new List<MCPUssStyleAuditIssue>();
        public readonly List<string> Errors = new List<string>();
        public int ScannedStyleSheetCount;
        public int IndexedStyleSheetCount;
        public int IndexedUxmlCount;
        public int IndexedRuntimeSourceCount;

        public MCPUssStyleAuditReport(int maxIssues)
        {
            this.maxIssues = maxIssues;
        }

        public int ErrorCount => activeErrorCount;
        public int WarningCount => activeWarningCount;
        public int SuppressedCount => suppressedIssueCount;
        internal bool Truncated => truncated;
        public bool Passed => Errors.Count == 0 && ErrorCount == 0 &&
                              WarningCount == 0;

        public void Record(MCPUssStyleAuditIssue issue, bool includeSuppressed)
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
                if (issue.IsError)
                {
                    activeErrorCount++;
                }
                else
                {
                    activeWarningCount++;
                }
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
                    StringComparison.Ordinal);
                if (pathComparison != 0)
                {
                    return pathComparison;
                }

                var lineComparison = left.Line.CompareTo(right.Line);
                if (lineComparison != 0)
                {
                    return lineComparison;
                }

                var selectorComparison = string.Compare(left.Selector, right.Selector,
                    StringComparison.Ordinal);
                return selectorComparison != 0
                    ? selectorComparison
                    : string.Compare(left.Kind, right.Kind, StringComparison.Ordinal);
            });
        }

        public Dictionary<string, object> ToDictionary()
        {
            return new Dictionary<string, object>
            {
                { "success", Errors.Count == 0 },
                { "passed", Passed },
                { "scannedStyleSheets", ScannedStyleSheetCount },
                { "indexedStyleSheets", IndexedStyleSheetCount },
                { "indexedUxmlFiles", IndexedUxmlCount },
                { "indexedRuntimeSources", IndexedRuntimeSourceCount },
                { "errorCount", ErrorCount },
                { "warningCount", WarningCount },
                { "suppressedCount", SuppressedCount },
                { "truncated", truncated },
                { "issues", Issues.Select(issue => issue.ToDictionary()).ToList() },
                { "errors", Errors.ToList() },
                { "suppressionSyntax",
                    $"/* {MCPUssStyleAuditor.SUPPRESSION_MARKER} <reason> */" },
                { "redundantDeclarationSuppressionSyntax",
                    $"/* {MCPUssStyleAuditor.REDUNDANT_DECLARATION_SUPPRESSION_MARKER} " +
                    "<reason> */" },
                { "ancestorDefaultResetSuppressionSyntax",
                    $"/* {MCPUssStyleAuditor.ANCESTOR_DEFAULT_RESET_SUPPRESSION_MARKER} " +
                    "<reason> */" },
                { "pixelGridSuppressionSyntax",
                    $"/* {MCPUssStyleAuditor.PIXEL_GRID_SUPPRESSION_MARKER} <reason> */" },
                { "textStyleContractSuppressionSyntax",
                    $"/* {MCPUssStyleAuditor.TEXT_STYLE_CONTRACT_SUPPRESSION_MARKER} " +
                    "<reason> */" }
            };
        }
    }
}
#endif
