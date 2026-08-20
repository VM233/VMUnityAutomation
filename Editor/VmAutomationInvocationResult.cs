using System;
using System.Collections.Generic;

namespace VMUnityAutomation.Editor
{
    public sealed class VmAutomationInvocationResult
    {
        public bool Ok { get; }

        public string Command { get; }

        public string Route { get; }

        public string RequestId { get; }

        public string Status { get; }

        public object Result { get; }

        public VmAutomationError Error { get; }

        public IReadOnlyList<object> Warnings { get; }

        public long ExecutionTimeMs { get; }

        public string CatalogRevision { get; }

        private VmAutomationInvocationResult(
            bool ok,
            string command,
            string route,
            string requestId,
            string status,
            object result,
            VmAutomationError error,
            IReadOnlyList<object> warnings,
            long executionTimeMs)
        {
            Ok = ok;
            Command = command ?? "";
            Route = route ?? "";
            RequestId = requestId ?? "";
            Status = status ?? (ok ? "completed" : "failed");
            Result = result;
            Error = error;
            Warnings = warnings ?? Array.Empty<object>();
            ExecutionTimeMs = executionTimeMs;
            CatalogRevision = VmAutomationCatalog.CatalogRevision;
        }

        internal static VmAutomationInvocationResult Success(
            string command,
            string route,
            string requestId,
            object result,
            long executionTimeMs)
        {
            return new VmAutomationInvocationResult(
                true,
                command,
                route,
                requestId,
                "completed",
                result,
                null,
                null,
                executionTimeMs);
        }

        internal static VmAutomationInvocationResult Failure(
            string command,
            string route,
            string requestId,
            string code,
            string message,
            bool retryable = false,
            IReadOnlyDictionary<string, object> details = null,
            long executionTimeMs = 0)
        {
            return new VmAutomationInvocationResult(
                false,
                command,
                route,
                requestId,
                "failed",
                null,
                new VmAutomationError(code, message, retryable, details),
                null,
                executionTimeMs);
        }
    }
}
