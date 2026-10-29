using System;

namespace VMUnityAutomation.Runtime
{
    public sealed class VmFrameCaptureProgress
    {
        public VmFrameCaptureContext Context { get; }
        public string Status { get; }
        public string FailureCode { get; }
        public int RequestedIntervals { get; }
        public int ObservedIntervals { get; }
        public int? BaselineFrame { get; }
        public int? LastFrame { get; }
        public long? BaselineTimestamp { get; }
        public long? LastTimestamp { get; }
        public DateTime? EndedAtUtc { get; }
        public bool Terminal { get; }
        public VmFrameCaptureRejectedSample RejectedSample { get; }

        internal VmFrameCaptureProgress(VmFrameCaptureContext context, string status,
            string failureCode, int requested, int observed, int? baselineFrame, int? lastFrame,
            long? baselineTimestamp, long? lastTimestamp, DateTime? endedAtUtc, bool terminal,
            VmFrameCaptureRejectedSample rejectedSample)
        {
            Context = context;
            Status = status;
            FailureCode = failureCode;
            RequestedIntervals = requested;
            ObservedIntervals = observed;
            BaselineFrame = baselineFrame;
            LastFrame = lastFrame;
            BaselineTimestamp = baselineTimestamp;
            LastTimestamp = lastTimestamp;
            EndedAtUtc = endedAtUtc;
            Terminal = terminal;
            RejectedSample = rejectedSample;
        }
    }
}
