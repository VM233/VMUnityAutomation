using System;

namespace VMUnityAutomation.Editor
{
    public static class VmProjectToolExecutionContext
    {
        public static string JobId => VmAutomationPersistentJobRunner.CurrentJobId;

        public static bool IsCancellationRequested =>
            VmAutomationPersistentJobRunner.IsCurrentJobCancellationRequested;

        public static void ThrowIfCancellationRequested()
        {
            if (IsCancellationRequested)
                throw new OperationCanceledException($"Project tool job '{JobId}' was canceled.");
        }
    }
}
