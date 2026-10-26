using System.ComponentModel;

namespace VMUnityAutomation.Editor
{
    public sealed class VmPlayerPointerRequest
    {
        [VmRequired, VmMinLength(1), VmJsonProperty("executablePath")]
        public string ExecutablePath { get; set; }

        [VmRequired, VmRange(1, int.MaxValue), VmJsonProperty("processId")]
        public int ProcessId { get; set; }

        [VmRequired, VmMinLength(1), VmJsonProperty("startedAt")]
        [Description("Exact round-trip UTC OS creation time returned by Player launch.")]
        public string StartedAt { get; set; }

        [VmRequired, VmJsonProperty("action")]
        public VmPlayerPointerAction Action { get; set; }

        [VmRequired, VmJsonProperty("x")]
        [Description("Desktop-pixel X coordinate; negative monitor coordinates are supported.")]
        public int X { get; set; }

        [VmRequired, VmJsonProperty("y")]
        [Description("Desktop-pixel Y coordinate, measured from the virtual desktop top.")]
        public int Y { get; set; }
    }
}
