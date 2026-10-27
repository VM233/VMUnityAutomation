namespace VMUnityAutomation.Editor.InputSystem
{
    public sealed class VmRuntimeKeyboardResult
    {
        public string ActionName { get; set; }
        public string[] Keys { get; set; }
        public int PerformedCount { get; set; }
        public string FinalActionPhase { get; set; }
        public bool FinalActionPressed { get; set; }
        public int FrameBefore { get; set; }
        public int FrameAfter { get; set; }
        public string DispatchUpdateBuffer { get; set; }
        public bool TemporaryKeyboardRemoved { get; set; }
        public bool CurrentKeyboardRestored { get; set; }
        public bool SettingsRestored { get; set; }
        public bool PauseRestored { get; set; }
    }
}
