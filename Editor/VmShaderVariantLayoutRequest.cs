using System;
using System.ComponentModel;
using UnityEditor;
using UnityEditor.Rendering;

namespace VMUnityAutomation.Editor
{
    public sealed class VmShaderVariantLayoutRequest
    {
        [VmRequired, VmJsonProperty("assetPath")] public string AssetPath { get; set; }
        [VmRange(0, 255), VmJsonProperty("subshader")] public int Subshader { get; set; }
        [VmRange(0, 255), VmJsonProperty("pass")] public int Pass { get; set; }
        [VmJsonProperty("stage")] public ShaderType Stage { get; set; } = ShaderType.Vertex;
        [VmJsonProperty("platform")] public ShaderCompilerPlatform Platform { get; set; } = ShaderCompilerPlatform.D3D;
        [VmJsonProperty("buildTarget")] public BuildTarget BuildTarget { get; set; } = BuildTarget.StandaloneWindows64;
        [VmJsonProperty("keywords"), Description("Exact keyword set for one variant. At most 64 distinct, non-empty keywords, each at most 128 characters; never enumerates variants.")]
        public string[] Keywords { get; set; } = Array.Empty<string>();
        [VmRange(1, 128), VmJsonProperty("maxBuffers")] public int MaxBuffers { get; set; } = 64;
        [VmRange(1, 256), VmJsonProperty("maxFieldsPerBuffer")] public int MaxFieldsPerBuffer { get; set; } = 64;
        [VmRange(1, 200), VmJsonProperty("maxDiagnostics")] public int MaxDiagnostics { get; set; } = 100;
    }
}
