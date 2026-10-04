namespace VMUnityAutomation.Editor.InputSystem
{
    public sealed class VmInputRuntimeStateResult
    {
        public int Frame { get; set; }
        public string UpdateBuffer { get; set; }
        public bool ApplicationFocused { get; set; }
        public bool GameHasFocus { get; set; }
        public bool GameIsPlaying { get; set; }
        public bool IsProjectWideAsset { get; set; }
        public string UpdateMode { get; set; }
        public string BackgroundBehavior { get; set; }
        public string EditorInputBehavior { get; set; }
        public string AssetPath { get; set; }
        public VmInputActionState[] Actions { get; set; }
        public VmInputDeviceState[] Devices { get; set; }
    }

}
