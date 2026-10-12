using System.Collections.Generic;

namespace VMUnityAutomation.Editor
{
    internal static class VmAutomationInvocationArguments
    {
        private static readonly string[] MetadataNames =
        {
            "expectedProjectPath", "targetProjectPath", "unityProjectPath",
            "_agentId", "_requestId", "_jobId", "idempotencyKey", "runAsJob", "jobAccessToken"
        };

        internal static Dictionary<string, object> ToolPayload(Dictionary<string, object> arguments)
        {
            var payload = arguments != null
                ? new Dictionary<string, object>(arguments) : new Dictionary<string, object>();
            foreach (string name in MetadataNames) payload.Remove(name);
            return payload;
        }

        internal static T Bind<T>(Dictionary<string, object> arguments) =>
            (T)VmJsonContract.Bind(ToolPayload(arguments), typeof(T));
    }
}
