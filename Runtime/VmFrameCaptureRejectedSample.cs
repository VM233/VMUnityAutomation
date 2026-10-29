namespace VMUnityAutomation.Runtime
{
    public sealed class VmFrameCaptureRejectedSample
    {
        public int Frame { get; }
        public long Timestamp { get; }
        public float TimeScale { get; }
        public int Width { get; }
        public int Height { get; }

        internal VmFrameCaptureRejectedSample(int frame, long timestamp, float timeScale, int width, int height)
        {
            Frame = frame;
            Timestamp = timestamp;
            TimeScale = timeScale;
            Width = width;
            Height = height;
        }
    }
}
