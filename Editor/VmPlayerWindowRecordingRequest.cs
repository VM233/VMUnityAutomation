using System.ComponentModel;

namespace VMUnityAutomation.Editor
{
    public sealed class VmPlayerWindowRecordingRequest : VmPlayerIdentityRequest
    {
        [Description("Begin, Inspect or End. All actions retain the exact original Player identity; Inspect/End also require sessionId.")]
        [VmJsonProperty("action")]
        public string Action { get; set; } = "Begin";
        [VmJsonProperty("sessionId")]
        public string SessionId { get; set; }
        [Description("New absolute .mp4 destination outside Assets, with no existing recording receipt or encoder log.")]
        [VmJsonProperty("outputPath")]
        public string OutputPath { get; set; }
        [Description("Absolute path to an installed FFmpeg executable supporting Windows gdigrab hwnd capture.")]
        [VmJsonProperty("encoderPath")]
        public string EncoderPath { get; set; }
        [Description("Finite native recording duration in (0,30] seconds.")]
        [VmRange(.01, 30), VmJsonProperty("durationSeconds")]
        public double DurationSeconds { get; set; } = 8;
        [VmRange(1, 60), VmJsonProperty("framesPerSecond")]
        public int FramesPerSecond { get; set; } = 60;
        [Description("Client-pixel region origin. The complete region must stay within the verified Player client.")]
        [VmRange(0, int.MaxValue), VmJsonProperty("x")]
        public int X { get; set; }
        [VmRange(0, int.MaxValue), VmJsonProperty("y")]
        public int Y { get; set; }
        [Description("Explicit client region width/height, each 2..4096; total area at most 4194304 pixels.")]
        [VmRange(2, 4096), VmJsonProperty("width")]
        public int Width { get; set; }
        [VmRange(2, 4096), VmJsonProperty("height")]
        public int Height { get; set; }
    }
}
