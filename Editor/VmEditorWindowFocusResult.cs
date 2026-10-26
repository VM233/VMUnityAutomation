namespace VMUnityAutomation.Editor
{
    public sealed class VmEditorWindowFocusResult
    {
        public string WindowInstanceId { get; set; }
        public string WindowType { get; set; }
        public string PreviousFocusedWindowInstanceId { get; set; }
        public string PreviousFocusedWindowType { get; set; }
        public string PreviousFocusedWindowTitle { get; set; }
        public float[] PreviousFocusedWindowPosition { get; set; }
        public float[] WindowPosition { get; set; }
        public bool Focused { get; set; }
        public bool PreviousMaximized { get; set; }
        public bool Maximized { get; set; }
    }
}
