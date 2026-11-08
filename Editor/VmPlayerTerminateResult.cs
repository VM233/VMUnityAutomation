namespace VMUnityAutomation.Editor
{
    public sealed class VmPlayerTerminateResult
    {
        public string ExecutablePath { get; set; }
        public int ProcessId { get; set; }
        public string StartedAt { get; set; }
        public bool TerminationRequested { get; set; }
        public bool ExitObserved { get; set; }
        public int? ExitCode { get; set; }
    }
}
