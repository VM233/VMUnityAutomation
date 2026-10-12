using System.ComponentModel;

namespace VMUnityAutomation.Editor
{
    public sealed class VmEditorBackgroundSessionRequest
    {
        [VmRequired, VmJsonProperty("action")]
        public VmEditorBackgroundSessionAction Action { get; set; }

        [VmJsonProperty("sessionId")]
        [Description("Begin creates this capability. Inspect and End require it; End belongs in finally.")]
        public string SessionId { get; set; }

        [VmRange(1, 3600), VmJsonProperty("durationSeconds")]
        [Description("Begin-only lifetime. The session expires automatically; default is 600 seconds.")]
        public int? DurationSeconds { get; set; }
    }
}
