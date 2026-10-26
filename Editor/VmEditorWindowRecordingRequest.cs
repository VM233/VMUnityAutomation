using System.ComponentModel;

namespace VMUnityAutomation.Editor
{
    public sealed class VmEditorWindowRecordingRequest
    {
        [Description("Begin, Inspect or End. Begin records an existing visible Editor tab; End requests early completion.")]
        [VmJsonProperty("action")]
        public string Action { get; set; } = "Begin";
        [VmJsonProperty("sessionId")]
        public string SessionId { get; set; }
        [VmJsonProperty("windowInstanceId")]
        public string WindowInstanceId { get; set; }
        [Description("New absolute .mp4 destination outside Assets. Existing recordings are never overwritten.")]
        [VmJsonProperty("outputPath")]
        public string OutputPath { get; set; }
        [Description("Absolute path to an installed FFmpeg executable. No encoder is downloaded by Unity.")]
        [VmJsonProperty("encoderPath")]
        public string EncoderPath { get; set; }
        [Description("Finite capture duration in seconds, greater than zero and at most 30.")]
        [VmRange(.01, 30), VmJsonProperty("durationSeconds")]
        public double DurationSeconds { get; set; } = 8;
        [Description("Native capture sampling frequency, 1 through 15. Actual monotonic frame times are preserved.")]
        [VmRange(1, 15), VmJsonProperty("framesPerSecond")]
        public int FramesPerSecond { get; set; } = 10;
        [Description("Schedule each next native sample one period after the actual previous sample instead of the absolute frame grid. Useful for bounded consumer state sequences; actual timing remains in the receipt. Defaults to false.")]
        [VmJsonProperty("relativeCadence")]
        public bool RelativeCadence { get; set; }
        [Description("Maximum native Editor view dimension, 256 through 4096; default 2048. This is an admission cap, not image resizing or window creation.")]
        [VmRange(256, 4096), VmJsonProperty("maxDimension")]
        public int MaxDimension { get; set; } = 2048;
    }
}
