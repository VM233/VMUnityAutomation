using System.ComponentModel;

namespace VMUnityAutomation.Editor
{
    public sealed class VmRuntimeTouchRequest
    {
        [VmRequired, VmJsonProperty("documentInstanceId")]
        [Description("Exact attached runtime UIDocument identity from uitoolkit/runtime-documents.")]
        public string DocumentInstanceId { get; set; }

        [VmRequired, VmJsonProperty("samples")]
        [Description("One or two native touch samples in this frame; distinct fingers in slots 0 through 9.")]
        public VmRuntimeTouchSample[] Samples { get; set; }
    }

    public sealed class VmRuntimeTouchSample
    {
        [VmRequired, VmRange(0, 9), VmJsonProperty("finger")]
        public int Finger { get; set; }
        [VmRequired, VmJsonProperty("phase")]
        public VmRuntimeTouchPhase Phase { get; set; }
        [VmRequired, VmJsonProperty("x")]
        public float X { get; set; }
        [VmRequired, VmJsonProperty("y")]
        public float Y { get; set; }
    }

    public enum VmRuntimeTouchPhase { Down, Move, Up, Cancel }
}
