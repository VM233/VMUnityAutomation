using System.Collections.Generic;

namespace VMUnityAutomation.Editor
{
    /// <summary>
    /// Thread-safe status boundary for the latest immutable persistent-job snapshot.
    /// It never enters Unity's main-thread request queue. An authorized read of a
    /// newly queued workspace Job also publishes the durable client-adoption marker
    /// that releases later main-thread execution.
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
            object snapshot =
                VmAutomationJobHistory.GetPublishedSnapshot(invocationArguments);
            VmAutomationWorkspaceJobAdoptionStore.PublishFromSnapshot(snapshot);
            return snapshot;
        }
    }
}
