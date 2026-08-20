#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace VMUnityAutomation.Editor
{
    public static class MCPUIToolkitUssAuditCommands
    {
        public static object AuditUssStyles(Dictionary<string, object> args)
        {
            args = args ?? new Dictionary<string, object>();
            var options = MCPUIToolkitAuditOptions.FromArguments(args);
            var report = MCPUssStyleAuditor.Audit(
                MCPUIToolkitAuditUtility.GetStringList(args, "paths"),
                MCPUIToolkitAuditUtility.GetBool(args, "includeSuppressed"),
                Mathf.Clamp(MCPUIToolkitAuditUtility.GetInt(args, "maxIssues", 200), 1, 5000),
                options);

            if (MCPUIToolkitAuditUtility.GetBool(args, "logWarnings"))
                MCPUssStyleAuditConsoleReporter.Log(report, false);

            var result = report.ToDictionary();
            result["scope"] = options.ToDictionary();
            result["automaticAudit"] =
                MCPUIToolkitAutomaticAuditCoordinator.GetStatus(".uss");
            if (MCPUIToolkitAuditUtility.GetBool(args, "runSelfTests"))
            {
                var selfTests = MCPUssStyleAuditor.RunSelfTests();
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
            var options = MCPUIToolkitAuditOptions.FromProjectSettings(
                MCPUIToolkitAuditProjectSettings.Load());
            var report = MCPUssStyleAuditor.Audit(
                Array.Empty<string>(), true, 5000, options);
            MCPUssStyleAuditConsoleReporter.Log(report, false);
        }
    }
}
#endif
