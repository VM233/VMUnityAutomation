#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace VMUnityAutomation.Editor
{
    public static class VmAutomationUIToolkitUssAuditCommands
    {
        public static object AuditUssStyles(Dictionary<string, object> args)
        {
            args = args ?? new Dictionary<string, object>();
            var options = VmAutomationUIToolkitAuditOptions.FromArguments(args);
            var report = VmAutomationUssStyleAuditor.Audit(
                VmAutomationUIToolkitAuditUtility.GetStringList(args, "paths"),
                VmAutomationUIToolkitAuditUtility.GetBool(args, "includeSuppressed"),
                Mathf.Clamp(VmAutomationUIToolkitAuditUtility.GetInt(args, "maxIssues", 200), 1, 5000),
                options);

            if (VmAutomationUIToolkitAuditUtility.GetBool(args, "logWarnings"))
                VmAutomationUssStyleAuditConsoleReporter.Log(report, false);

            var result = report.ToDictionary();
            result["scope"] = options.ToDictionary();
            result["automaticAudit"] =
                VmAutomationUIToolkitAutomaticAuditCoordinator.GetStatus(".uss");
            if (VmAutomationUIToolkitAuditUtility.GetBool(args, "runSelfTests"))
            {
                var selfTests = VmAutomationUssStyleAuditor.RunSelfTests();
                result["selfTests"] = selfTests;
                if (selfTests.TryGetValue("passed", out var passed) &&
                    passed is bool passedValue && passedValue == false)
                    result["success"] = false;
            }

            return result;
        }

        [MenuItem("Tools/UI Toolkit/Audit USS Styles")]
        private static void AuditAllFromMenu()
        {
            var options = VmAutomationUIToolkitAuditOptions.FromProjectSettings(
                VmAutomationUIToolkitAuditProjectSettings.Load());
            var report = VmAutomationUssStyleAuditor.Audit(
                Array.Empty<string>(), true, 5000, options);
            VmAutomationUssStyleAuditConsoleReporter.Log(report, false);
        }
    }
}
#endif
