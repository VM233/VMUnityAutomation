namespace VMUnityAutomation.Editor
{
    public class VmPlayerIdentityRequest
    {
        [VmRequired, VmMinLength(1), VmJsonProperty("executablePath")]
        public string ExecutablePath { get; set; }

        [VmRequired, VmRange(1, int.MaxValue), VmJsonProperty("processId")]
        public int ProcessId { get; set; }

        [VmRequired, VmMinLength(1), VmJsonProperty("startedAt")]
        [System.ComponentModel.Description("Exact round-trip UTC OS creation time returned by Player launch.")]
        public string StartedAt { get; set; }
    }
}
