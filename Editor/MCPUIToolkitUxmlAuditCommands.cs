#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace VMUnityAutomation.Editor
{
    public static class MCPUIToolkitUxmlAuditCommands
    {
        public static object AuditUxmlLayout(Dictionary<string, object> args)
        {
            args = args ?? new Dictionary<string, object>();
            var options = MCPUIToolkitAuditOptions.FromArguments(args);
            var report = MCPUxmlLayoutAuditor.Audit(
                MCPUIToolkitAuditUtility.GetStringList(args, "paths"),
                MCPUIToolkitAuditUtility.GetBool(args, "includeSuppressed"),
                Mathf.Clamp(MCPUIToolkitAuditUtility.GetInt(args, "maxIssues", 200), 1, 5000),
                options);

            if (MCPUIToolkitAuditUtility.GetBool(args, "logWarnings"))
                MCPUxmlLayoutAuditConsoleReporter.Log(report, false);

            var result = report.ToDictionary();
            result["scope"] = options.ToDictionary();
            result["automaticAudit"] =
                MCPUIToolkitAutomaticAuditCoordinator.GetStatus(".uxml");
            if (MCPUIToolkitAuditUtility.GetBool(args, "runSelfTests"))
            {
                var selfTests = MCPUxmlLayoutAuditor.RunSelfTests();
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
            var options = MCPUIToolkitAuditOptions.FromProjectSettings(
                MCPUIToolkitAuditProjectSettings.Load());
            var report = MCPUxmlLayoutAuditor.Audit(
                Array.Empty<string>(), true, 5000, options);
            MCPUxmlLayoutAuditConsoleReporter.Log(report, false);
        }
    }
}
#endif
