namespace VMUnityAutomation.Editor
{
    public sealed class VmSimulatorPointerResult
    {
        public string WindowInstanceId { get; set; }
        public string TargetType { get; set; }
        public VmSimulatorPointerPhase Phase { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
        public float TouchX { get; set; }
        public float TouchY { get; set; }
        public bool NativeTouchActive { get; set; }
        public bool PointerInsideScreen { get; set; }
        public bool ApplicationFocused { get; set; }
        public int Frame { get; set; }
    }
}
