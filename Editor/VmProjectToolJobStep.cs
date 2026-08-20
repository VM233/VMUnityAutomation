using System;
using System.Collections.Generic;

namespace VMUnityAutomation.Editor
{
    public sealed class VmProjectToolJobStep
    {
        public bool IsComplete { get; }

        public object Result { get; }

        public Dictionary<string, object> State { get; }

        public double? Progress { get; }

        public string StatusMessage { get; }

        public int DelayMilliseconds { get; }

        public string CleanupToken { get; }

        private VmProjectToolJobStep(bool isComplete, object result,
            Dictionary<string, object> state, double? progress, string statusMessage,
            int delayMilliseconds, string cleanupToken)
        {
            IsComplete = isComplete;
            Result = result;
            State = state ?? new Dictionary<string, object>();
            Progress = progress.HasValue
                ? Math.Max(0d, Math.Min(1d, progress.Value))
                : null;
            StatusMessage = statusMessage ?? "";
            DelayMilliseconds = Math.Max(0, Math.Min(60_000, delayMilliseconds));
            CleanupToken = cleanupToken ?? "";
        }

        public static VmProjectToolJobStep Complete(object result, string cleanupToken = "")
        {
            return new VmProjectToolJobStep(true, result, null, 1d, "Completed.", 0,
                cleanupToken);
        }

        public static VmProjectToolJobStep Pending(Dictionary<string, object> state,
            double? progress = null, string statusMessage = "", int delayMilliseconds = 0,
            string cleanupToken = "")
        {
            return new VmProjectToolJobStep(false, null, state, progress, statusMessage,
                delayMilliseconds, cleanupToken);
        }
    }
}
