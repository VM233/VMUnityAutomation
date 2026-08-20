using System;

namespace VMUnityAutomation.Editor
{
    /// <summary>
    /// Domain-local lifecycle state shared by automation owners and the CLI facade.
    /// </summary>
    public static class VmAutomationRuntimeState
    {
        private static string s_BusyReason;

        public static bool IsBusy => !string.IsNullOrEmpty(s_BusyReason);

        public static string BusyReason => s_BusyReason ?? "";

        internal static void SetBusyReason(string reason)
        {
            s_BusyReason = string.IsNullOrWhiteSpace(reason)
                ? null
                : reason.Trim();
        }
    }
}
