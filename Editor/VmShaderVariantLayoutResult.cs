namespace VMUnityAutomation.Editor
{
    public sealed class VmShaderVariantLayoutResult
    {
        [VmRequired, VmJsonProperty("assetPath")] public string AssetPath { get; set; }
        [VmRequired, VmJsonProperty("shaderName")] public string ShaderName { get; set; }
        [VmRequired, VmJsonProperty("passName")] public string PassName { get; set; }
        [VmRequired, VmJsonProperty("request")] public VmShaderVariantLayoutRequest Request { get; set; }
        [VmRequired, VmJsonProperty("compiled")] public bool Compiled { get; set; }
        [VmRequired, VmJsonProperty("programBytes")] public int ProgramBytes { get; set; }
        [VmRequired, VmJsonProperty("bufferCount")] public int BufferCount { get; set; }
        [VmRequired, VmJsonProperty("constantBufferBytes")] public long ConstantBufferBytes { get; set; }
        [VmRequired, VmJsonProperty("buffers")] public VmShaderConstantBufferLayout[] Buffers { get; set; }
        [VmRequired, VmJsonProperty("diagnosticCount")] public int DiagnosticCount { get; set; }
        [VmRequired, VmJsonProperty("diagnostics")] public VmShaderDiagnostic[] Diagnostics { get; set; }
        [VmRequired, VmJsonProperty("truncated")] public bool Truncated { get; set; }
    }
}
