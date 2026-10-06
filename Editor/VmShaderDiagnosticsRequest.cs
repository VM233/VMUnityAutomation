using System.ComponentModel;

namespace VMUnityAutomation.Editor
{
    public sealed class VmShaderDiagnosticsRequest
    {
        [VmRequired, VmJsonProperty("assetPath"), Description("Exact imported Shader or ComputeShader asset path. Supports .shader, compiled .shadergraph and .compute assets.")]
        public string AssetPath { get; set; }

        [VmRange(1, 200), VmJsonProperty("maxDiagnostics"), Description("Maximum native messages to include. The total count and truncation remain explicit.")]
        public int MaxDiagnostics { get; set; } = 100;
    }
}
