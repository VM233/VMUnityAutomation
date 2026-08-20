#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace VMUnityAutomation.Editor
{
    public static class VmAutomationUIToolkitUxmlAuditCommands
    {
        public static object AuditUxmlLayout(Dictionary<string, object> args)
        {
            args = args ?? new Dictionary<string, object>();
            var options = VmAutomationUIToolkitAuditOptions.FromArguments(args);
            var report = VmAutomationUxmlLayoutAuditor.Audit(
                VmAutomationUIToolkitAuditUtility.GetStringList(args, "paths"),
                VmAutomationUIToolkitAuditUtility.GetBool(args, "includeSuppressed"),
                Mathf.Clamp(VmAutomationUIToolkitAuditUtility.GetInt(args, "maxIssues", 200), 1, 5000),
                options);

            if (VmAutomationUIToolkitAuditUtility.GetBool(args, "logWarnings"))
                VmAutomationUxmlLayoutAuditConsoleReporter.Log(report, false);

            var result = report.ToDictionary();
            result["scope"] = options.ToDictionary();
            result["automaticAudit"] =
                VmAutomationUIToolkitAutomaticAuditCoordinator.GetStatus(".uxml");
            if (VmAutomationUIToolkitAuditUtility.GetBool(args, "runSelfTests"))
            {
                var selfTests = VmAutomationUxmlLayoutAuditSelfTests.RunSelfTests();
                result["selfTests"] = selfTests;
                object passed;
                if (selfTests.TryGetValue("passed", out passed) &&
                    passed is bool && !(bool)passed)
                    result["success"] = false;
            }

            return result;
        }

        [MenuItem("Tools/UI Toolkit/Audit UXML Layout Contracts")]
        private static void AuditAllFromMenu()
        {
            var options = VmAutomationUIToolkitAuditOptions.FromProjectSettings(
                VmAutomationUIToolkitAuditProjectSettings.Load());
            var report = VmAutomationUxmlLayoutAuditor.Audit(
                Array.Empty<string>(), true, 5000, options);
            VmAutomationUxmlLayoutAuditConsoleReporter.Log(report, false);
        }
    }
}
#endif
