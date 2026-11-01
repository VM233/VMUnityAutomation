namespace VMUnityAutomation.Editor
{
    public sealed class VmEditorOverlayResult
    {
        public string WindowInstanceId { get; set; }
        public string WindowType { get; set; }
        public string OverlayId { get; set; }
        public string DisplayName { get; set; }
        public bool PreviousDisplayed { get; set; }
        public bool Displayed { get; set; }
        public bool Changed { get; set; }
    }
}
