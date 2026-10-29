using System;
using System.Collections.ObjectModel;

namespace VMUnityAutomation.Runtime
{
    public sealed class VmFrameCaptureSession
    {
        public const int MaximumIntervals = 120000;
        public const double BinWidthMs = 0.1;
        public const double MaximumBinnedMs = 10000;
        public const int BinCount = 100001;

        public VmFrameCaptureContext Context { get; }
        public int RequestedIntervals { get; }
        public bool IsTerminal => terminalProgress != null;

        private readonly int[] bins = new int[BinCount];
        private readonly ReadOnlyCollection<int> histogram;
        private int count, overflow;
        private int? baselineFrame, lastFrame;
        private long? baselineTimestamp, lastTimestamp;
        private double sum, minimum = double.PositiveInfinity, maximum;
        private VmFrameCaptureProgress terminalProgress;
        private VmFrameCaptureStatistics statistics;
        private VmFrameCaptureRejectedSample rejectedSample;

        public VmFrameCaptureSession(VmFrameCaptureContext context, int requestedIntervals)
        {
            Context = context ?? throw new ArgumentNullException(nameof(context));
            if (requestedIntervals < 1 || requestedIntervals > MaximumIntervals)
                throw new ArgumentOutOfRangeException(nameof(requestedIntervals));
            RequestedIntervals = requestedIntervals;
            histogram = Array.AsReadOnly(bins);
        }

        public void Observe(int frame, long timestamp, float timeScale, int width, int height,
            DateTime utcNow)
        {
            if (IsTerminal) throw new InvalidOperationException("A terminal capture cannot observe more frames.");
            if (timeScale != Context.TimeScale || width != Context.Width || height != Context.Height)
            {
                Reject("frame_capture_context_changed", frame, timestamp, timeScale, width, height, utcNow);
                return;
            }
            if (!baselineFrame.HasValue)
            {
                baselineFrame = lastFrame = frame;
                baselineTimestamp = lastTimestamp = timestamp;
                return;
            }
            if ((long)frame != (long)lastFrame.Value + 1)
            {
                Reject("frame_capture_frame_discontinuity", frame, timestamp, timeScale, width, height, utcNow);
                return;
            }
            if (timestamp <= lastTimestamp.Value)
            {
                Reject("frame_capture_clock_discontinuity", frame, timestamp, timeScale, width, height, utcNow);
                return;
            }
            long duration = checked(timestamp - lastTimestamp.Value);
            double milliseconds = duration * 1000d / Context.ClockFrequency;
            lastFrame = frame;
            lastTimestamp = timestamp;
            count++;
            sum += milliseconds;
            minimum = Math.Min(minimum, milliseconds);
            maximum = Math.Max(maximum, milliseconds);
            if (milliseconds > MaximumBinnedMs) overflow++;
            else
            {
                int bin = (int)Math.Ceiling(milliseconds * 10);
                if (bin * BinWidthMs < milliseconds) bin++;
                bins[bin]++;
            }
            if (count == RequestedIntervals) Finish("completed", null, utcNow);
        }

        public void Cancel(DateTime utcNow) => EndIncomplete("canceled", "frame_capture_canceled", utcNow);

        public void Interrupt(string reason, DateTime utcNow)
        {
            if (reason != "application-exit" && reason != "disabled" && reason != "destroyed")
                throw new ArgumentException("Unknown capture lifecycle boundary.", nameof(reason));
            EndIncomplete("interrupted", "frame_capture_" + reason, utcNow);
        }

        public VmFrameCaptureResult Get(bool includeHistogram = false)
        {
            return new VmFrameCaptureResult(terminalProgress ?? Progress("sampling", null, null),
                statistics, IsTerminal && includeHistogram ? histogram : null);
        }

        private void EndIncomplete(string status, string failure, DateTime utcNow)
        {
            if (!IsTerminal) Finish(status, failure, utcNow);
        }

        private VmFrameCaptureProgress Progress(string status, string failure, DateTime? end) =>
            new VmFrameCaptureProgress(Context, status, failure, RequestedIntervals, count,
                baselineFrame, lastFrame, baselineTimestamp, lastTimestamp, end, IsTerminal || end.HasValue,
                rejectedSample);

        private void Reject(string code, int frame, long timestamp, float timeScale, int width,
            int height, DateTime utcNow)
        {
            rejectedSample = new VmFrameCaptureRejectedSample(frame, timestamp, timeScale, width, height);
            Finish("failed", code, utcNow);
        }

        private void Finish(string status, string failure, DateTime utcNow)
        {
            if (utcNow.Kind != DateTimeKind.Utc) throw new ArgumentException("Terminal time must be UTC.", nameof(utcNow));
            var quantiles = new VmFrameQuantileInterval[3];
            if (count > 0)
            {
                int[] ranks = { (int)Math.Ceiling(count * .5), (int)Math.Ceiling(count * .9), (int)Math.Ceiling(count * .99) };
                int cumulative = 0, next = 0;
                for (int bin = 0; bin < bins.Length; bin++)
                {
                    cumulative += bins[bin];
                    while (next < ranks.Length && cumulative >= ranks[next])
                        quantiles[next++] = new VmFrameQuantileInterval((bin - 1) * BinWidthMs, bin * BinWidthMs);
                }
                while (next < ranks.Length)
                    quantiles[next++] = new VmFrameQuantileInterval(MaximumBinnedMs, null);
            }
            statistics = new VmFrameCaptureStatistics(count, overflow, sum, minimum, maximum, quantiles);
            terminalProgress = Progress(status, failure, utcNow);
        }
    }
}
