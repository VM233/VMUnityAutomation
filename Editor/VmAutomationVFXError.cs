using System;

namespace VMUnityAutomation.Editor
{
    internal static class VmAutomationVFXError
    {
        internal sealed class Failure : ArgumentException
        {
            internal Failure(string errorCode, string message) : base(message)
            {
                ErrorCode = errorCode;
            }

            internal string ErrorCode { get; }
        }

        internal static Failure Create(string errorCode, string message)
        {
            return new Failure(errorCode, message);
        }

        internal static string Code(Exception exception, string defaultCode)
        {
            Exception failure = VmAutomationVFXReflection.Unwrap(exception);
            if (failure is Failure typed)
                return typed.ErrorCode;
            if (failure is ArgumentException || failure is FormatException ||
                failure is OverflowException || failure is InvalidCastException)
                return "invalid_arguments";
            if (failure is MissingMemberException ||
                failure is MissingMethodException ||
                failure is TypeLoadException)
                return "unsupported_vfx_version";
            return defaultCode;
        }

        internal static object Response(Exception exception, string defaultCode)
        {
            Exception failure = VmAutomationVFXReflection.Unwrap(exception);
            return VmAutomationResponse.Error(failure.Message, Code(failure, defaultCode));
        }
    }
}
