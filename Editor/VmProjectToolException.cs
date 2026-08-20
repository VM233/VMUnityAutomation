using System;
using System.Collections.Generic;

namespace VMUnityAutomation.Editor
{
    public sealed class VmProjectToolException : Exception
    {
        public string ErrorCode { get; }

        public bool Retryable { get; }

        public Dictionary<string, object> Details { get; }

        public VmProjectToolException(string errorCode, string message, bool retryable = false,
            Dictionary<string, object> details = null) : base(message)
        {
            ErrorCode = string.IsNullOrWhiteSpace(errorCode) ? "project_tool_failed" : errorCode;
            Retryable = retryable;
            Details = details;
        }
    }
}
