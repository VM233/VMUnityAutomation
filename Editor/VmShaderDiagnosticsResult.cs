namespace VMUnityAutomation.Editor
{
    public sealed class VmShaderDiagnosticsResult
    {
        [VmRequired, VmJsonProperty("assetPath")] public string AssetPath { get; set; }
        [VmRequired, VmJsonProperty("shaderName")] public string ShaderName { get; set; }
        [VmRequired, VmJsonProperty("assetType")] public VmShaderAssetType AssetType { get; set; }
        [VmJsonProperty("isSupported")] public bool? IsSupported { get; set; }
        [VmRequired, VmJsonProperty("hasErrors")] public bool HasErrors { get; set; }
        [VmRequired, VmJsonProperty("diagnosticCount")] public int DiagnosticCount { get; set; }
        [VmRequired, VmJsonProperty("truncated")] public bool Truncated { get; set; }
        [VmRequired, VmJsonProperty("diagnostics")] public VmShaderDiagnostic[] Diagnostics { get; set; }
    }
}
