namespace VMUnityAutomation.Editor
{
    public sealed class VmEditorWindowFocusClearResult
    {
        public bool Cleared { get; set; }
        public VmEditorWindowFocusStateResult Previous { get; set; }
        public VmEditorWindowFocusStateResult Current { get; set; }
    }
}
