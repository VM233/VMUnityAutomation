namespace VMUnityAutomation.Editor
{
    public sealed class VmSimulatorPointerResult
    {
        public string WindowInstanceId { get; set; }
        public string TargetType { get; set; }
        public VmSimulatorPointerPhase Phase { get; set; }
        public float X { get; set; }
        public float Y { get; set; }
    }
}
