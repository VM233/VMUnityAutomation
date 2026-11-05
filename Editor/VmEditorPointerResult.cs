namespace VMUnityAutomation.Editor
{
    public sealed class VmEditorPointerResult
    {
        public string WindowInstanceId { get; set; }
        public VmEditorPointerPhase Phase { get; set; }
        public string PickedName { get; set; }
        public string PickedType { get; set; }
        public int DispatchedEvents { get; set; }
        public bool RootAttached { get; set; }
        public bool MouseCaptured { get; set; }
    }
}
