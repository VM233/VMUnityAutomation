namespace VMUnityAutomation.Editor
{
    public sealed class VmPlayerLaunchResult
    {
        public string ExecutablePath { get; set; }
        public int ProcessId { get; set; }
        public string StartedAt { get; set; }
        public string PlayerLogPath { get; set; }
        public string[] PlayerArguments { get; set; }
    }
}
