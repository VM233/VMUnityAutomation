namespace VMUnityAutomation.Runtime
{
    public sealed class VmFrameCaptureStatistics
    {
        public int Count { get; }
        public int OverflowCount { get; }
        public double SumMs { get; }
        public double? MeanMs { get; }
        public double? MinimumMs { get; }
        public double? MaximumMs { get; }
        public VmFrameQuantileInterval P50 { get; }
        public VmFrameQuantileInterval P90 { get; }
        public VmFrameQuantileInterval P99 { get; }

        internal VmFrameCaptureStatistics(int count, int overflowCount, double sumMs,
            double minimumMs, double maximumMs, VmFrameQuantileInterval[] quantiles)
        {
            Count = count;
            OverflowCount = overflowCount;
            SumMs = sumMs;
            MeanMs = count == 0 ? (double?)null : sumMs / count;
            MinimumMs = count == 0 ? (double?)null : minimumMs;
            MaximumMs = count == 0 ? (double?)null : maximumMs;
            P50 = quantiles[0];
            P90 = quantiles[1];
            P99 = quantiles[2];
        }
    }
}
