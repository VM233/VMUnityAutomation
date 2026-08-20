using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace VMUnityAutomation.Editor
{
    public sealed class VmAutomationInvocationResult
    {
        [JsonProperty("ok")]
        public bool Ok { get; }

        [JsonProperty("command")]
        public string Command { get; }

        [JsonProperty("route")]
        public string Route { get; }

        [JsonProperty("requestId")]
        public string RequestId { get; }

        [JsonProperty("status")]
        public string Status { get; }

        [JsonProperty("result", NullValueHandling = NullValueHandling.Ignore)]
        public object Result { get; }

        [JsonProperty("error", NullValueHandling = NullValueHandling.Ignore)]
        public VmAutomationError Error { get; }

        [JsonProperty("warnings")]
        public IReadOnlyList<object> Warnings { get; }

        [JsonProperty("executionTimeMs")]
        public long ExecutionTimeMs { get; }

        [JsonProperty("catalogRevision")]
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
