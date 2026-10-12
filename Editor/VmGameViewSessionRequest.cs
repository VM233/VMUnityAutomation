using System.ComponentModel;

namespace VMUnityAutomation.Editor
{
    public sealed class VmGameViewSessionRequest
    {
        [VmRequired, VmJsonProperty("action")]
        public VmGameViewSessionAction Action { get; set; }

        [VmJsonProperty("sessionId")]
        [Description("Begin returns this capability. Resize and End require it. End belongs in the caller's finally block.")]
        public string SessionId { get; set; }

        [VmRange(10, 8192), VmJsonProperty("width")]
        public int? Width { get; set; }

        [VmRange(10, 8192), VmJsonProperty("height")]
        public int? Height { get; set; }
    }
}
