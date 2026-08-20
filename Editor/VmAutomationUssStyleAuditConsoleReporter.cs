#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace VMUnityAutomation.Editor
{
    internal static class VmAutomationUssStyleAuditConsoleReporter
    {
        internal static void Log(VmAutomationUssStyleAuditReport report, bool automatic)
        {
            foreach (var error in report.Errors)
            {
                Debug.LogError($"[USS Style Audit] {error}");
            }

            foreach (var issue in report.Issues.Where(issue => issue.Suppressed == false))
            {
                var context = AssetDatabase.LoadAssetAtPath<StyleSheet>(issue.AssetPath);
                var message =
                    $"[USS Style Audit] {issue.AssetPath}:{issue.Line} {issue.Selector}: {issue.Message}";
                if (issue.IsError)
                {
                    Debug.LogError(message, context);
                }
                else
                {
                    Debug.LogWarning(message, context);
                }
            }

            if (automatic == false || report.Errors.Count > 0 ||
                report.ErrorCount > 0 || report.WarningCount > 0)
            {
                var mode = automatic ? "automatic import audit" : "requested audit";
                Debug.Log(
                    $"[USS Style Audit] {mode}: scanned={report.ScannedStyleSheetCount}, " +
                    $"errors={report.ErrorCount}, warnings={report.WarningCount}, " +
                    $"suppressed={report.SuppressedCount}, toolErrors={report.Errors.Count}, " +
                    $"passed={report.Passed}.");
            }
        }
    }
}
#endif
