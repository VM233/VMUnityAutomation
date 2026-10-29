namespace VMUnityAutomation.Runtime
{
    public sealed class VmFrameQuantileInterval
    {
        public double LowerExclusiveMs { get; }
        public double? UpperInclusiveMs { get; }

        public VmFrameQuantileInterval(double lowerExclusiveMs, double? upperInclusiveMs)
        {
            LowerExclusiveMs = lowerExclusiveMs;
            UpperInclusiveMs = upperInclusiveMs;
        }
    }
}
