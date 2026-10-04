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

    public sealed class VmInputActionState
    {
        public string Name { get; set; }
        public bool Enabled { get; set; }
        public string Phase { get; set; }
        public string ActiveControl { get; set; }
    }

    public sealed class VmInputDeviceState
    {
        public int ID { get; set; }
        public string Name { get; set; }
        public string Layout { get; set; }
        public bool Enabled { get; set; }
        public bool Native { get; set; }
        public bool Added { get; set; }
        public bool IsTouchscreen { get; set; }
        public int TouchID { get; set; }
        public string TouchPhase { get; set; }
        public bool TouchPressed { get; set; }
        public float TouchX { get; set; }
        public float TouchY { get; set; }
    }
}
