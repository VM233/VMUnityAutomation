namespace VMUnityAutomation.Editor
{
    public sealed class VmRuntimeTouchResult
    {
        public string DocumentInstanceId { get; set; }
        public int DispatchedEvents { get; set; }
        public int Frame { get; set; }
        public int[] CapturedFingers { get; set; }
    }
}
