#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace VMUnityAutomation.Editor
{
    /// <summary>
    /// Typed programmatic entry point for the package's authoritative UXML and USS audits.
    /// </summary>
    public static class UIToolkitAuditor
    {
        /// <summary>
        /// Maximum finding count accepted by one audit invocation.
        /// </summary>
        public const int MaximumFindingCount = 5000;

        /// <summary>
        /// Audits USS assets against the project-owned UI Toolkit audit settings.
        /// Pass an empty collection to audit every configured USS asset.
        /// </summary>
        public static UIToolkitAuditReport AuditUssStyles(
            IEnumerable<string> assetPaths, bool includeSuppressed, int maxFindings)
        {
            string[] paths = PrepareRequest(assetPaths, maxFindings);
            MCPUIToolkitAuditOptions options = LoadProjectOptions();
            MCPUssStyleAuditReport report = MCPUssStyleAuditor.Audit(paths,
                includeSuppressed, maxFindings, options);

            return new UIToolkitAuditReport(
                report.Issues.Select(issue => new UIToolkitAuditFinding(
                    issue.AssetPath, issue.Line, issue.Kind, issue.Severity,
                    issue.Message, issue.Suppressed,
                    issue.SuppressionReason ?? string.Empty)),
                report.Errors, report.ErrorCount, report.WarningCount,
                report.SuppressedCount, report.Truncated);
        }

        /// <summary>
        /// Audits UXML assets against the project-owned UI Toolkit audit settings.
        /// Pass an empty collection to audit every configured UXML asset.
        /// </summary>
        public static UIToolkitAuditReport AuditUxmlLayouts(
            IEnumerable<string> assetPaths, bool includeSuppressed, int maxFindings)
        {
            string[] paths = PrepareRequest(assetPaths, maxFindings);
            MCPUIToolkitAuditOptions options = LoadProjectOptions();
            MCPUxmlLayoutAuditReport report = MCPUxmlLayoutAuditor.Audit(paths,
                includeSuppressed, maxFindings, options);

            return new UIToolkitAuditReport(
                report.Issues.Select(issue => new UIToolkitAuditFinding(
                    issue.AssetPath, issue.Line, issue.Kind, "warning",
                    issue.Message, issue.Suppressed,
                    issue.SuppressionReason ?? string.Empty)),
                report.Errors, 0, report.WarningCount, report.SuppressedCount,
                report.Truncated);
        }

        private static string[] PrepareRequest(IEnumerable<string> assetPaths,
            int maxFindings)
        {
            if (assetPaths == null)
                throw new ArgumentNullException(nameof(assetPaths));
            if (maxFindings < 1 || maxFindings > MaximumFindingCount)
            {
                throw new ArgumentOutOfRangeException(nameof(maxFindings), maxFindings,
                    $"Finding count must be between 1 and {MaximumFindingCount}.");
            }

            string[] paths = assetPaths.ToArray();
            if (paths.Any(string.IsNullOrWhiteSpace))
            {
                throw new ArgumentException(
                    "Asset paths cannot contain null, empty, or whitespace values.",
                    nameof(assetPaths));
            }

            return paths;
        }

        private static MCPUIToolkitAuditOptions LoadProjectOptions()
        {
            MCPUIToolkitAuditProjectSettings settings =
                MCPUIToolkitAuditProjectSettings.Load();
            if (settings.Valid == false)
            {
                throw new InvalidDataException(
                    $"Invalid {MCPUIToolkitAuditProjectSettings.ConfigPath}: " +
                    settings.Error);
            }

            return MCPUIToolkitAuditOptions.FromProjectSettings(settings);
        }
    }
}
#endif
