using System;
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
            result["compilationRequestedAt"] = FormatDate(job.CompilationRequestedAt);
            result["compilationStartedAt"] = FormatDate(job.CompilationStartedAt);
            result["compilationFinishedAt"] = FormatDate(job.CompilationFinishedAt);
            result["assemblyReloadObserved"] = true;
            result["compilerErrorCount"] = job.CompilerErrorCount;
            result["compilerWarningCount"] = job.CompilerWarningCount;
            result["compilerDeprecatedWarningCount"] = job.CompilerDeprecatedWarningCount;
            result["compilerSnapshotRevision"] = job.CompilerSnapshotRevision;
            result["compilerDiagnosticsComplete"] = BuildDiagnosticFailure(job) == null;
            result["compilerMessages"] = job.CompilerMessages.Cast<object>().ToList();
            return result;
        }

        internal static bool IsComplete(VmAutomationWorkspaceJob job)
        {
            return job != null && job.ExpectedCompilationAssemblies.Count > 0 &&
                   GetTerminalAssemblies(job).Count > 0 &&
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
            List<string> missingTerminalAssemblies = FindMissingTerminalAssemblies(job);
            int expectedCount = job?.ExpectedCompilationAssemblies.Count ?? 0;
            int terminalCount = GetTerminalAssemblies(job).Count;
            if (expectedCount > 0 && terminalCount > 0 &&
                missingTerminalAssemblies.Count == 0)
                return null;

            string message = expectedCount == 0
                ? "Unity exposed no expected Editor script assemblies for the requested clean compilation."
                : terminalCount == 0
                    ? "Unity finished the compilation lifecycle without reporting a terminal state for any expected script assembly."
                    : $"Unity reported terminal evidence for {terminalCount} of {expectedCount} expected script assemblies.";
            return VmAutomationResponse.Error(message, "compilation_evidence_incomplete", false,
                BuildAssemblyDetails(job));
        }

        internal static Dictionary<string, object> BuildDiagnosticFailure(
            VmAutomationWorkspaceJob job)
        {
            if (job.CompilerDiagnosticsError != null)
                return job.CompilerDiagnosticsError;
            return string.IsNullOrEmpty(job.CompilerSnapshotRevision)
                ? VmAutomationResponse.Error(
                    "Workspace compilation has no completed compiler diagnostic snapshot.",
                    "compilation_diagnostics_incomplete", false)
                : null;
        }

        internal static void CaptureCompilerDiagnostics(VmAutomationWorkspaceJob job)
        {
            ApplyCompilerDiagnostics(job,
                VmAutomationConsoleCommands.ReadCompleteCompilationDiagnostics());
        }

        internal static void ApplyCompilerDiagnostics(VmAutomationWorkspaceJob job,
            VmCompilationDiagnosticsProduct product)
        {
            job.CompilerSnapshotRevision = product.Revision;
            job.CompilerDiagnosticsError = product.BuildFailure();
            job.CompilerErrorCount = product.ErrorCount;
            job.CompilerWarningCount = product.WarningCount;
            job.CompilerDeprecatedWarningCount = product.DeprecatedWarningCount;
            job.CompilerMessages = product.Entries.Select(entry =>
            {
                var message = entry.ToDictionary(pair => pair.Key, pair => pair.Value);
                message["type"] = (string)entry["severity"] == "error"
                    ? CompilerMessageType.Error.ToString()
                    : CompilerMessageType.Warning.ToString();
                return message;
            }).ToList();
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
                { "assembliesWithoutStartedCallback",
                    FindMissingStartedAssemblies(job).Cast<object>().ToList() },
                { "missingTerminalCompilationAssemblies",
                    FindMissingTerminalAssemblies(job).Cast<object>().ToList() },
                { "cleanBuildCacheCallbackLimitationObserved",
                    HasCleanBuildCacheCallbackLimitation(job) },
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

        private static bool HasCleanBuildCacheCallbackLimitation(
            VmAutomationWorkspaceJob job)
        {
            return job != null && job.ExpectedCompilationAssemblies.Count > 0 &&
                   job.NotRequiredCompilationAssemblies.Count > 0 &&
                   (job.StartedCompilationAssemblies.Count <
                        job.ExpectedCompilationAssemblies.Count ||
                    job.FinishedCompilationAssemblies.Count <
                        job.ExpectedCompilationAssemblies.Count) &&
                   FindMissingTerminalAssemblies(job).Count == 0;
        }

        private static string FormatDate(DateTime? value)
        {
            return value.HasValue
                ? value.Value.ToString("O", CultureInfo.InvariantCulture)
                : "";
        }
    }
}
