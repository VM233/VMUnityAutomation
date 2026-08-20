#if UNITY_EDITOR
namespace VMUnityAutomation.Editor
{
    /// <summary>
    /// A source-located finding produced by a UI Toolkit static audit.
    /// </summary>
    public sealed class UIToolkitAuditFinding
    {
        internal UIToolkitAuditFinding(string assetPath, int line, string ruleId,
            string severity, string message, bool isSuppressed,
            string suppressionReason)
        {
            AssetPath = assetPath;
            Line = line;
            RuleId = ruleId;
            Severity = severity;
            Message = message;
            IsSuppressed = isSuppressed;
            SuppressionReason = suppressionReason;
        }

        /// <summary>
        /// Project-relative path of the audited UXML or USS asset.
        /// </summary>
        public string AssetPath { get; }

        /// <summary>
        /// One-based source line containing the finding.
        /// </summary>
        public int Line { get; }

        /// <summary>
        /// Stable rule identifier emitted by the authoritative auditor.
        /// </summary>
        public string RuleId { get; }

        /// <summary>
        /// Finding severity: error for hard contract violations, otherwise warning.
        /// </summary>
        public string Severity { get; }

        /// <summary>
        /// Actionable explanation of the finding.
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// Whether a reasoned source suppression matched this finding.
        /// </summary>
        public bool IsSuppressed { get; }

        /// <summary>
        /// Reason authored with the matching suppression, or an empty string.
        /// </summary>
        public string SuppressionReason { get; }
    }
}
#endif
