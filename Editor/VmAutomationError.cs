using System.Collections.Generic;
using Newtonsoft.Json;

namespace VMUnityAutomation.Editor
{
    public sealed class VmAutomationError
    {
        [JsonProperty("code")]
        public string Code { get; }

        [JsonProperty("message")]
        public string Message { get; }

        [JsonProperty("retryable")]
        public bool Retryable { get; }

        [JsonProperty("details", NullValueHandling = NullValueHandling.Ignore)]
        public IReadOnlyDictionary<string, object> Details { get; }

        internal VmAutomationError(
            string code,
            string message,
            bool retryable,
            IReadOnlyDictionary<string, object> details)
        {
            Code = string.IsNullOrEmpty(code) ? "automation_failed" : code;
            Message = message ?? "Automation command failed.";
            Retryable = retryable;
            Details = details;
        }
    }
}
