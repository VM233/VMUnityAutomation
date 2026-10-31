using System.ComponentModel;

namespace VMUnityAutomation.Editor
{
    public sealed class VmAudioRecordingRequest
    {
        [VmJsonProperty("action"), Description("Begin, Inspect, End or Cancel. End publishes samples actually received; Cancel publishes no WAV.")]
        public string Action { get; set; } = "Begin";
        [VmJsonProperty("sessionId"), Description("Exact capability identity returned by Begin.")]
        public string SessionId { get; set; }
        [VmJsonProperty("listenerInstanceId"), Description("Exact existing AudioListener component identity. No listener or window is created.")]
        public string ListenerInstanceId { get; set; }
        [VmJsonProperty("outputPath"), Description("New absolute .wav destination outside Assets, below an existing directory. Never overwritten.")]
        public string OutputPath { get; set; }
        [VmJsonProperty("durationSeconds"), VmRange(.01, 15), Description("Real DSP sample duration, at most15 seconds and4million interleaved samples.")]
        public double DurationSeconds { get; set; } = 8;
    }
}
