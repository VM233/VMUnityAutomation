using System.Collections.Generic;

namespace VMUnityAutomation.Editor
{
    /// <summary>
    /// Thread-safe read boundary for the latest immutable persistent-job snapshot.
    /// It never enters Unity's main-thread request queue and is suitable for CLI
    /// polling while the Editor is importing, compiling, or building.
    /// </summary>
    public static class VmAutomationPublishedJobReader
    {
        public static object Get(
            IDictionary<string, object> arguments = null,
            string agentId = null)
        {
            var invocationArguments = arguments != null
                ? new Dictionary<string, object>(arguments)
                : new Dictionary<string, object>();
            invocationArguments["_agentId"] =
                string.IsNullOrWhiteSpace(agentId) ? "cli" : agentId.Trim();
            return VmAutomationJobHistory.GetPublishedSnapshot(invocationArguments);
        }
    }
}
