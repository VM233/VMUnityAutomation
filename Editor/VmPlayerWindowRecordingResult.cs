namespace VMUnityAutomation.Editor
{
    public sealed class VmPlayerWindowRecordingResult
    {
        public string SessionId { get; set; }
        public string State { get; set; }
        public string ExecutablePath { get; set; }
        public int ProcessId { get; set; }
        public string StartedAt { get; set; }
        public string WindowHandle { get; set; }
        public int ClientWidth { get; set; }
        public int ClientHeight { get; set; }
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public int FramesPerSecond { get; set; }
        public double RequestedSeconds { get; set; }
        public double ElapsedSeconds { get; set; }
        public string OutputPath { get; set; }
        public string ManifestPath { get; set; }
        public string EncoderLogPath { get; set; }
        public int EncoderExitCode { get; set; } = -1;
        public long OutputBytes { get; set; }
        public string Error { get; set; } = "";
        public bool ResourcesReleased { get; set; }
    }
}
