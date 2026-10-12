namespace VMUnityAutomation.Editor
{
    public sealed class VmEditorBackgroundSessionResult
    {
        public bool Active { get; set; }
        public string SessionId { get; set; }
        public bool OriginalRunInBackground { get; set; }
        public bool RunInBackground { get; set; }
        public bool ApplicationFocused { get; set; }
        public bool EditorActive { get; set; }
        public int FrameBefore { get; set; }
        public int Frame { get; set; }
        public string DeadlineUtc { get; set; }
    }
}
