using System.Collections.Generic;

namespace VMUnityAutomation.Editor
{
    public sealed class VmAutomationError
    {
        public string Code { get; }

        public string Message { get; }

        public bool Retryable { get; }

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
