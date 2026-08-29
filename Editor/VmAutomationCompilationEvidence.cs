using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEditor.Compilation;

namespace VMUnityAutomation.Editor
{
    internal static class VmAutomationCompilationEvidence
    {
        internal static Dictionary<string, object> Build(VmAutomationWorkspaceJob job)
        {
            Dictionary<string, object> result = BuildAssemblyDetails(job);
            result["compilationRequested"] = true;
            result["compilationStartedAt"] = FormatDate(job.CompilationStartedAt);
            result["compilationFinishedAt"] = FormatDate(job.CompilationFinishedAt);
            result["assemblyReloadObserved"] = true;
            result["compilerErrorCount"] = job.CompilerErrorCount;
            result["compilerWarningCount"] = job.CompilerWarningCount;
            result["compilerMessages"] = job.CompilerMessages.Cast<object>().ToList();
            return result;
        }

        internal static bool IsComplete(VmAutomationWorkspaceJob job)
        {
            return job != null && job.ExpectedCompilationAssemblies.Count > 0 &&
                   job.StartedCompilationAssemblies.Count > 0 &&
                   GetTerminalAssemblies(job).Count > 0 &&
                   FindMissingStartedAssemblies(job).Count == 0 &&
                   FindMissingTerminalAssemblies(job).Count == 0;
        }

        internal static List<string> FindMissingStartedAssemblies(
            VmAutomationWorkspaceJob job)
        {
            if (job == null)
                return new List<string>();

            var started = new HashSet<string>(job.StartedCompilationAssemblies,
                StringComparer.Ordinal);
            return job.ExpectedCompilationAssemblies
                .Where(assemblyName => !started.Contains(assemblyName))
                .OrderBy(assemblyName => assemblyName, StringComparer.Ordinal)
                .ToList();
        }

        internal static List<string> FindMissingTerminalAssemblies(
            VmAutomationWorkspaceJob job)
        {
            if (job == null)
                return new List<string>();

            var terminal = new HashSet<string>(GetTerminalAssemblies(job),
                StringComparer.Ordinal);
            return job.ExpectedCompilationAssemblies
                .Where(assemblyName => !terminal.Contains(assemblyName))
                .OrderBy(assemblyName => assemblyName, StringComparer.Ordinal)
                .ToList();
        }

        internal static Dictionary<string, object> BuildFailure(
            VmAutomationWorkspaceJob job)
        {
            List<string> missingStartedAssemblies = FindMissingStartedAssemblies(job);
            List<string> missingTerminalAssemblies = FindMissingTerminalAssemblies(job);
            int expectedCount = job?.ExpectedCompilationAssemblies.Count ?? 0;
            int startedCount = job?.StartedCompilationAssemblies.Count ?? 0;
            int terminalCount = GetTerminalAssemblies(job).Count;
            if (expectedCount > 0 && startedCount > 0 && terminalCount > 0 &&
                missingStartedAssemblies.Count == 0 && missingTerminalAssemblies.Count == 0)
                return null;

            string message = expectedCount == 0
                ? "Unity exposed no expected Editor script assemblies for the requested clean compilation."
                : startedCount == 0
                    ? "Unity finished the compilation lifecycle without starting any expected script assembly build."
                    : missingStartedAssemblies.Count > 0
                        ? $"Unity started {startedCount} of {expectedCount} expected script assembly builds."
                        : terminalCount == 0
                            ? "Unity finished the compilation lifecycle without reporting a terminal state for any expected script assembly."
                            : $"Unity reported terminal evidence for {terminalCount} of {expectedCount} expected script assemblies.";
            return VmAutomationResponse.Error(message, "compilation_evidence_incomplete", false,
                BuildAssemblyDetails(job));
        }

        internal static void CaptureCompilerDiagnostics(VmAutomationWorkspaceJob job,
            IEnumerable<Dictionary<string, object>> fallbackMessages)
        {
            object product = VmAutomationConsoleCommands.GetCompilationErrors(
                new Dictionary<string, object>
                {
                    { "count", 200 },
                    { "severity", "all" },
                });
            if (!VmAutomationResponse.TryGetError(product, out _, out _, out _))
            {
                Dictionary<string, object> values = VmAutomationResponse.ToDictionary(product);
                Dictionary<string, object> counts = values != null &&
                    values.TryGetValue("counts", out object countValue)
                        ? VmAutomationResponse.ToDictionary(countValue)
                        : null;
                job.CompilerErrorCount = GetInt(counts, "errors");
                job.CompilerWarningCount = GetInt(counts, "warnings");
                job.CompilerMessages = values != null &&
                    values.TryGetValue("entries", out object entriesValue) &&
                    entriesValue is IList entries
                        ? entries.Cast<object>()
                            .Select(VmAutomationResponse.ToDictionary)
                            .Where(entry => entry != null)
                            .Select(AddCompilerMessageType)
                            .ToList()
                        : new List<Dictionary<string, object>>();
                return;
            }

            job.CompilerMessages = (fallbackMessages ??
                Array.Empty<Dictionary<string, object>>())
                .Select(message => new Dictionary<string, object>(message)).ToList();
            job.CompilerErrorCount = job.CompilerMessages.Count(message =>
                string.Equals(GetString(message, "type"), CompilerMessageType.Error.ToString(),
                    StringComparison.Ordinal));
            job.CompilerWarningCount = job.CompilerMessages.Count(message =>
                string.Equals(GetString(message, "type"), CompilerMessageType.Warning.ToString(),
                    StringComparison.Ordinal));
        }

        private static Dictionary<string, object> BuildAssemblyDetails(
            VmAutomationWorkspaceJob job)
        {
            List<string> terminalAssemblies = GetTerminalAssemblies(job);
            return new Dictionary<string, object>
            {
                { "cleanBuildCacheRequested", true },
                { "expectedCompilationAssemblyCount",
                    job?.ExpectedCompilationAssemblies.Count ?? 0 },
                { "startedCompilationAssemblyCount",
                    job?.StartedCompilationAssemblies.Count ?? 0 },
                { "finishedCompilationAssemblyCount",
                    job?.FinishedCompilationAssemblies.Count ?? 0 },
                { "notRequiredCompilationAssemblyCount",
                    job?.NotRequiredCompilationAssemblies.Count ?? 0 },
                { "terminalCompilationAssemblyCount", terminalAssemblies.Count },
                { "expectedCompilationAssemblies", (job?.ExpectedCompilationAssemblies ??
                    new List<string>()).Cast<object>().ToList() },
                { "startedCompilationAssemblies", (job?.StartedCompilationAssemblies ??
                    new List<string>()).Cast<object>().ToList() },
                { "finishedCompilationAssemblies", (job?.FinishedCompilationAssemblies ??
                    new List<string>()).Cast<object>().ToList() },
                { "notRequiredCompilationAssemblies", (job?.NotRequiredCompilationAssemblies ??
                    new List<string>()).Cast<object>().ToList() },
                { "terminalCompilationAssemblies", terminalAssemblies.Cast<object>().ToList() },
                { "missingStartedCompilationAssemblies",
                    FindMissingStartedAssemblies(job).Cast<object>().ToList() },
                { "missingTerminalCompilationAssemblies",
                    FindMissingTerminalAssemblies(job).Cast<object>().ToList() },
                { "cleanBuildCacheFinishedCallbackIssueObserved",
                    HasCleanBuildCacheFinishedCallbackIssue(job) },
            };
        }

        private static List<string> GetTerminalAssemblies(VmAutomationWorkspaceJob job)
        {
            if (job == null)
                return new List<string>();
            return job.FinishedCompilationAssemblies
                .Concat(job.NotRequiredCompilationAssemblies)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(assemblyName => assemblyName, StringComparer.Ordinal)
                .ToList();
        }

        private static bool HasCleanBuildCacheFinishedCallbackIssue(
            VmAutomationWorkspaceJob job)
        {
            return job != null && job.ExpectedCompilationAssemblies.Count > 0 &&
                   job.FinishedCompilationAssemblies.Count == 0 &&
                   job.NotRequiredCompilationAssemblies.Count > 0 &&
                   FindMissingTerminalAssemblies(job).Count == 0;
        }

        private static Dictionary<string, object> AddCompilerMessageType(
            Dictionary<string, object> message)
        {
            var result = new Dictionary<string, object>(message);
            if (!result.ContainsKey("type"))
            {
                result["type"] = string.Equals(GetString(result, "severity"), "error",
                    StringComparison.Ordinal)
                    ? CompilerMessageType.Error.ToString()
                    : CompilerMessageType.Warning.ToString();
            }
            return result;
        }

        private static string GetString(Dictionary<string, object> values, string key)
        {
            return values != null && values.TryGetValue(key, out object value) && value != null
                ? value.ToString()
                : "";
        }

        private static int GetInt(Dictionary<string, object> values, string key)
        {
            return values != null && values.TryGetValue(key, out object value) &&
                   value != null && int.TryParse(value.ToString(), NumberStyles.Integer,
                       CultureInfo.InvariantCulture, out int result)
                ? result
                : 0;
        }

        private static string FormatDate(DateTime? value)
        {
            return value.HasValue
                ? value.Value.ToString("O", CultureInfo.InvariantCulture)
                : "";
        }
    }
}
