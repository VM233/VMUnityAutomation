using Newtonsoft.Json;

namespace VMUnityAutomation.Editor
{
    public sealed class VmUiTransitionSourceCleanupRequest
    {
        [JsonProperty("action")] public string Action { get; set; } = "cleanup";
        [JsonProperty("cleanupToken")] public string CleanupToken { get; set; }
    }
}
