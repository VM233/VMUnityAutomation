namespace VMUnityAutomation.Editor
{
    public sealed class VmPlayerPointerResult
    {
        public string PlayerWindow { get; set; }
        public int PlayerExtendedStyle { get; set; }
        public bool PlayerIsClickThrough { get; set; }
        public string ReceiverWindow { get; set; }
        public uint ReceiverProcessId { get; set; }
        public bool ReceiverIsPlayer { get; set; }
        public int CursorX { get; set; }
        public int CursorY { get; set; }
        public int InsertedInputEvents { get; set; }
    }
}
