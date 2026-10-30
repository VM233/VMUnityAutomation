using System;

namespace VMUnityAutomation.Runtime
{
    public sealed class VmUiAuditLimitException : Exception
    {
        public VmUiAuditLimitException(string message) : base(message) { }
    }
}
