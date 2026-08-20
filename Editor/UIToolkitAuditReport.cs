#if UNITY_EDITOR
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace VMUnityAutomation.Editor
{
    /// <summary>
    /// Immutable result of one UI Toolkit audit family.
    /// </summary>
    public sealed class UIToolkitAuditReport
    {
        internal UIToolkitAuditReport(IEnumerable<UIToolkitAuditFinding> findings,
            IEnumerable<string> errors, int errorCount, int warningCount,
            int suppressedCount, bool isTruncated)
        {
            Findings = new ReadOnlyCollection<UIToolkitAuditFinding>(
                findings.ToList());
            Errors = new ReadOnlyCollection<string>(errors.ToList());
            ErrorCount = errorCount;
            WarningCount = warningCount;
            SuppressedCount = suppressedCount;
            IsTruncated = isTruncated;
        }

        /// <summary>
        /// Findings retained by the requested suppression and result-limit policy.
        /// </summary>
        public IReadOnlyList<UIToolkitAuditFinding> Findings { get; }

        /// <summary>
        /// Source or configuration errors encountered during the audit.
        /// </summary>
        public IReadOnlyList<string> Errors { get; }

        /// <summary>
        /// Total number of active hard-contract findings before result truncation.
        /// </summary>
        public int ErrorCount { get; }

        /// <summary>
        /// Total number of active findings before result truncation.
        /// </summary>
        public int WarningCount { get; }

        /// <summary>
        /// Total number of findings matched by reasoned suppressions.
        /// </summary>
        public int SuppressedCount { get; }

        /// <summary>
        /// Whether the finding collection reached the requested maximum.
        /// </summary>
        public bool IsTruncated { get; }

        /// <summary>
        /// Whether the audit completed without errors or active findings.
        /// </summary>
        public bool Passed => Errors.Count == 0 && ErrorCount == 0 &&
                              WarningCount == 0;
    }
}
#endif
