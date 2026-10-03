using System.ComponentModel;

namespace VMUnityAutomation.Editor
{
    public sealed class VmSimulatorPointerRequest
    {
        [VmRequired, VmJsonProperty("windowInstanceId")]
        [Description("Existing native Simulator window instanceId from uitoolkit/windows or query.")]
        public string WindowInstanceId { get; set; }

        [VmRequired, VmJsonProperty("phase")]
        public VmSimulatorPointerPhase Phase { get; set; }

        [VmRequired, VmRange(0, int.MaxValue), VmJsonProperty("x")]
        [Description("UI Toolkit window X in points, origin top left.")]
        public float X { get; set; }

        [VmRequired, VmRange(0, int.MaxValue), VmJsonProperty("y")]
        [Description("UI Toolkit window Y in points, origin top left.")]
        public float Y { get; set; }
    }
}
