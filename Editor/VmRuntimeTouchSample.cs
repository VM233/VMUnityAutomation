namespace VMUnityAutomation.Editor
{
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
}
