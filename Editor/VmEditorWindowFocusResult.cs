namespace VMUnityAutomation.Editor
{
    public sealed class VmEditorWindowFocusResult
    {
        public string WindowInstanceId { get; set; }
        public string WindowType { get; set; }
        public string PreviousFocusedWindowInstanceId { get; set; }
        public bool Focused { get; set; }
    }
}
