using System;
using System.Collections.Generic;

namespace VMUnityAutomation.Editor
{
    internal static class VmAutomationExceptionEvidence
    {
        internal static Dictionary<string, object> Capture(Exception exception)
        {
            Exception cause = exception.GetBaseException();
            return new Dictionary<string, object>
            {
                { "exceptionType", cause.GetType().FullName },
                { "stackTrace", cause.StackTrace },
                { "exceptionContext", exception.ToString() }
            };
        }
    }
}
