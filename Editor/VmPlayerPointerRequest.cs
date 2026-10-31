using System.ComponentModel;

namespace VMUnityAutomation.Editor
{
    public sealed class VmPlayerPointerRequest : VmPlayerIdentityRequest
    {
        [VmRequired, VmJsonProperty("action")]
        public VmPlayerPointerAction Action { get; set; }

        [VmRequired, VmJsonProperty("x")]
        [Description("Desktop-pixel X coordinate; negative monitor coordinates are supported.")]
        public int X { get; set; }

        [VmRequired, VmJsonProperty("y")]
        [Description("Desktop-pixel Y coordinate, measured from the virtual desktop top.")]
        public int Y { get; set; }

        [VmJsonProperty("endX")]
        [Description("Required for Drag: destination desktop-pixel X coordinate.")]
        public int? EndX { get; set; }

        [VmJsonProperty("endY")]
        [Description("Required for Drag: destination desktop-pixel Y coordinate.")]
        public int? EndY { get; set; }
    }
}
