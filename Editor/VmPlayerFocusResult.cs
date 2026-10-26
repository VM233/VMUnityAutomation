namespace VMUnityAutomation.Editor
{
    public sealed class VmPlayerFocusResult
    {
        public string PlayerWindow { get; set; }
        public string PreviousForegroundWindow { get; set; }
        public string ForegroundWindow { get; set; }
        public bool Focused { get; set; }
    }
}
