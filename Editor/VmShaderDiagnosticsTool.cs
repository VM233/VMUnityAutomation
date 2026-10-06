using System.Linq;
using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;

namespace VMUnityAutomation.Editor
{
    [VmProjectTool("shader/diagnostics",
        Description = "Read Unity's authoritative ShaderUtil compiler diagnostics for one imported Shader or ComputeShader, including hand-written shaders, compiled Shader Graph assets and compute kernels. Shader.isSupported alone does not prove error-free compilation; ComputeShader has no native asset-wide support flag, so isSupported is null. This reads existing native diagnostics; it does not compile, dispatch, reimport, clear messages or infer success from the Console.",
        ReadOnly = true,
        SideEffects = VmProjectToolSideEffect.ReadsProjectState,
        Preconditions = new[] { "editor-connected" },
        ErrorCodes = new[] { "shader_asset_not_found" },
        CompletionEvidence = "Exact imported shader identity, native support and error state, native message count, explicit truncation and bounded compiler messages with file, line, severity and platform.")]
    public sealed class VmShaderDiagnosticsTool :
        IVmProjectTool<VmShaderDiagnosticsRequest, VmShaderDiagnosticsResult>
    {
        public VmShaderDiagnosticsResult Execute(VmShaderDiagnosticsRequest request)
        {
            var asset = AssetDatabase.LoadMainAssetAtPath(request.AssetPath);
            ShaderMessage[] messages;
            VmShaderAssetType assetType;
            bool? supported;
            bool hasErrors;
            switch (asset)
            {
                case Shader shader:
                    assetType = VmShaderAssetType.Shader;
                    messages = ShaderUtil.GetShaderMessages(shader);
                    supported = shader.isSupported;
                    hasErrors = ShaderUtil.ShaderHasError(shader);
                    break;
                case ComputeShader compute:
                    assetType = VmShaderAssetType.ComputeShader;
                    messages = ShaderUtil.GetComputeShaderMessages(compute);
                    supported = null;
                    hasErrors = messages.Any(message => message.severity == ShaderCompilerMessageSeverity.Error);
                    break;
                default:
                    throw new VmProjectToolException("shader_asset_not_found",
                        $"No imported Shader or ComputeShader exists at '{request.AssetPath}'.");
            }
            return new VmShaderDiagnosticsResult
            {
                AssetPath = AssetDatabase.GetAssetPath(asset),
                ShaderName = asset.name,
                AssetType = assetType,
                IsSupported = supported,
                HasErrors = hasErrors,
                DiagnosticCount = messages.Length,
                Truncated = messages.Length > request.MaxDiagnostics,
                Diagnostics = messages.Take(request.MaxDiagnostics).Select(message => new VmShaderDiagnostic
                {
                    Severity = message.severity.ToString(),
                    Message = message.message,
                    File = message.file,
                    Line = message.line,
                    Platform = message.platform.ToString()
                }).ToArray()
            };
        }
    }
}
