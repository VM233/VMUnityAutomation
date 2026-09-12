using System;

namespace VMUnityAutomation.Editor
{
    public static class VmProjectToolExecutionContext
    {
        public static string JobId => VmAutomationPersistentJobRunner.CurrentJobId;

        public static bool IsCancellationRequested =>
            VmAutomationPersistentJobRunner.IsCurrentJobCancellationRequested;

        /// <summary>
        /// Captures the running job's cancellation identity for cooperative Editor callbacks.
        /// The predicate also becomes true when that job is terminal. It does not survive reload.
        /// </summary>
        public static Func<bool> CaptureCancellationCheck() =>
            VmAutomationPersistentJobRunner.CaptureCurrentCancellationCheck();

        public static void ThrowIfCancellationRequested()
        {
            if (IsCancellationRequested)
                throw new OperationCanceledException($"Project tool job '{JobId}' was canceled.");
        }
    }
}
