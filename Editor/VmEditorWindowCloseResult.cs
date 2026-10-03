namespace VMUnityAutomation.Editor
{
    public sealed class VmEditorWindowCloseResult
    {
        public string WindowInstanceId { get; set; }
        public string WindowType { get; set; }
        public bool Closed { get; set; }
    }
}
