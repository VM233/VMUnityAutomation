namespace VMUnityAutomation.Editor
{
    public sealed class VmUiTransitionSourceCleanupRequest
    {
        [VmRequired, VmJsonProperty("action")] public string Action { get; set; } = "cleanup";
        [VmRequired, VmMinLength(1), VmJsonProperty("cleanupToken")] public string CleanupToken { get; set; }
    }
}
