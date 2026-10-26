namespace VMUnityAutomation.Editor
{
    public sealed class VmEditorWindowRecordingResult
    {
        public string SessionId { get; set; }
        public string State { get; set; }
        public string OutputPath { get; set; }
        public string FrameDirectory { get; set; }
        public string ManifestPath { get; set; }
        public int FrameCount { get; set; }
        public double CapturedSeconds { get; set; }
        public int RequestedFramesPerSecond { get; set; }
        public int EncoderExitCode { get; set; }
        public string Error { get; set; }
        public bool ResourcesReleased { get; set; }
    }
}
