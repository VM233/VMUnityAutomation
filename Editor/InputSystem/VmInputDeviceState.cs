namespace VMUnityAutomation.Editor.InputSystem
{
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
