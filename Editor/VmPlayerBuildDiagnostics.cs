using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace VMUnityAutomation.Editor
{
    internal sealed class VmPlayerBuildDiagnostics : IDisposable
    {
        internal const int MaximumEntries = 64;
        internal const int MaximumCharacters = 65536;
        private readonly object gate = new object();
        private readonly List<Dictionary<string, object>> entries = new List<Dictionary<string, object>>();
        private int observedErrors;
        private int retainedCharacters;
        private bool disposed;

        internal VmPlayerBuildDiagnostics() => Application.logMessageReceivedThreaded += Observe;

        private void Observe(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert) return;
            lock (gate)
            {
                if (disposed) return;
                observedErrors++;
                long characters = (long)condition.Length + stackTrace.Length;
                if (entries.Count == MaximumEntries || characters > MaximumCharacters - retainedCharacters) return;
                entries.Add(new Dictionary<string, object>
                {
                    { "type", type.ToString() }, { "message", condition }, { "stackTrace", stackTrace },
                });
                retainedCharacters += (int)characters;
            }
        }

        internal Dictionary<string, object> Complete(BuildReport report, BuildPlayerOptions options,
            Exception exception = null)
        {
            Dispose();
            bool complete = observedErrors == entries.Count;
            var diagnostics = new Dictionary<string, object>
            {
                { "complete", complete }, { "observedErrors", observedErrors },
                { "retainedCharacters", retainedCharacters }, { "entries", entries },
            };
            Dictionary<string, object> result;
            if (exception != null)
            {
                result = VmAutomationResponse.UnexpectedError(exception, "build_pipeline_exception");
            }
            else if (report == null)
            {
                string reason = entries.Count > 0 ? (string)entries[0]["message"] :
                    "Unity BuildPipeline returned no BuildReport and emitted no captured native error.";
                result = VmAutomationResponse.Error(reason, "build_report_unavailable");
            }
            else
            {
                var summary = report.summary;
                result = new Dictionary<string, object>
                {
                    { "success", summary.result == BuildResult.Succeeded && complete },
                    { "result", summary.result.ToString() },
                    { "totalErrors", summary.totalErrors }, { "totalWarnings", summary.totalWarnings },
                    { "totalTime", summary.totalTime.TotalSeconds }, { "totalSize", summary.totalSize },
                    { "outputPath", summary.outputPath }, { "platform", summary.platform.ToString() },
                    { "compilationDiagnostics", VmAutomationConsoleCommands.GetCompilationDiagnosticsSummary() },
                };
                if (summary.result != BuildResult.Succeeded || !complete)
                {
                    result["errorCode"] = !complete ? "build_diagnostics_incomplete" : "build_failed";
                    result["error"] = entries.Count > 0 ? entries[0]["message"] :
                        "Unity BuildPipeline completed with result " + summary.result + ".";
                }
            }
            result["requestedOutputPath"] = options.locationPathName;
            result["requestedTarget"] = options.target.ToString();
            result["requestedScriptingDefines"] = options.extraScriptingDefines;
            result["nativeDiagnostics"] = diagnostics;
            return result;
        }

        public void Dispose()
        {
            lock (gate)
            {
                if (disposed) return;
                disposed = true;
                Application.logMessageReceivedThreaded -= Observe;
            }
        }
    }
}
