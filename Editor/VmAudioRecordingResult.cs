namespace VMUnityAutomation.Editor
{
    public sealed class VmAudioRecordingResult
    {
        public string SessionId { get; set; }
        public string State { get; set; }
        public string OutputPath { get; set; }
        public string Error { get; set; }
        public string ListenerInstanceId { get; set; }
        public int SampleRate { get; set; }
        public int Channels { get; set; }
        public int SampleCount { get; set; }
        public double CapturedSeconds { get; set; }
        public double Peak { get; set; }
        public double Rms { get; set; }
        public float ListenerVolumeAtStart { get; set; }
        public long OutputBytes { get; set; }
        public bool ResourcesReleased { get; set; }
    }
}
