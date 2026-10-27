using System.ComponentModel;

namespace VMUnityAutomation.Editor
{
    public sealed class VmNineSliceCompactRequest
    {
        [VmRequired, VmMinLength(1), VmJsonProperty("texturePath")]
        public string TexturePath { get; set; }

        [VmRequired, VmMinLength(64), VmJsonProperty("expectedSha256")]
        [Description("Exact sourceSha256 from review. A stale source is rejected before any write.")]
        public string ExpectedSha256 { get; set; }
    }
}
