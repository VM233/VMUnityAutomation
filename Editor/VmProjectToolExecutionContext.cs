using System;
using System.Threading.Tasks;

namespace VMUnityAutomation.Editor
{
    public static class VmProjectToolExecutionContext
    {
        public static string JobId => VmAutomationPersistentJobRunner.CurrentJobId;

        /// <summary>
        /// Reads the actual originating agent of the current running project-tool step.
        /// Capture it during that step when an owned callback invokes the public executor.
        /// Execution identity is not part of the tool's business arguments.
        /// </summary>
        public static string OriginatingAgentId =>
            VmAutomationPersistentJobRunner.CurrentOriginatingAgentId;

        public static bool IsCancellationRequested =>
            VmAutomationPersistentJobRunner.IsCurrentJobCancellationRequested;

        /// <summary>
        /// Captures the running job's cancellation identity for cooperative Editor callbacks.
        /// The predicate also becomes true when that job is terminal. It does not survive reload.
        /// </summary>
        public static Func<bool> CaptureCancellationCheck() =>
            VmAutomationPersistentJobRunner.CaptureCurrentCancellationCheck();

        public static void PublishCleanupToken(string actualToken,
            Task<VmProjectToolCleanupReceipt> actualRetirement) =>
            VmAutomationPersistentJobRunner.PublishCurrentCleanupToken(actualToken, actualRetirement);

        public static void ThrowIfCancellationRequested()
        {
            if (IsCancellationRequested)
                throw new OperationCanceledException($"Project tool job '{JobId}' was canceled.");
        }
    }
}
