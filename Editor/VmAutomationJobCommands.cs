using System.Collections.Generic;

namespace VMUnityAutomation.Editor
{
    /// <summary>Public Jobs routes backed by the canonical owner registry.</summary>
    internal static class VmAutomationJobCommands
    {
        public static object Get(Dictionary<string, object> args)
        {
            return VmAutomationJobOwnerRegistry.Get(args);
        }

        public static object Cancel(Dictionary<string, object> args)
        {
            return VmAutomationJobOwnerRegistry.Cancel(args);
        }

        public static object Cleanup(Dictionary<string, object> args)
        {
            return VmAutomationJobOwnerRegistry.Cleanup(args);
        }
    }
}
