using System.ComponentModel;

namespace VMUnityAutomation.Editor
{
    public sealed class VmRenderTextureSnapshotRequest
    {
        [VmRequired, VmMinLength(1), Description("Exact decimal native RenderTexture identity; no asset/path guessing.")]
        public string RenderTextureInstanceId { get; set; }
        [VmRequired, VmMinLength(1), Description("Create-new absolute PNG path in an existing project Temp or Tools directory.")]
        public string OutputPath { get; set; }
    }
}
