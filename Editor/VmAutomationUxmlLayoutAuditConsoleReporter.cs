#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace VMUnityAutomation.Editor
{
    internal static class VmAutomationUxmlLayoutAuditConsoleReporter
    {
        internal static void Log(VmAutomationUxmlLayoutAuditReport report, bool automatic)
        {
            foreach (var error in report.Errors)
            {
                Debug.LogError($"[UXML Layout Audit] {error}");
            }

            foreach (var issue in report.Issues.Where(issue => issue.Suppressed == false))
            {
                var context = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(issue.AssetPath);
                Debug.LogWarning(
                    $"[UXML Layout Audit] {issue.AssetPath}:{issue.Line} {issue.Element}: {issue.Message}",
                    context);
            }

            if (automatic == false || report.Errors.Count > 0 || report.WarningCount > 0)
            {
                var mode = automatic ? "automatic import audit" : "requested audit";
                Debug.Log(
                    $"[UXML Layout Audit] {mode}: scanned={report.ScannedUxmlCount}, " +
                    $"warnings={report.WarningCount}, suppressed={report.SuppressedCount}, " +
                    $"errors={report.Errors.Count}, passed={report.Passed}.");
            }
        }
    }
}
#endif
