using System.Linq;
using UnityEditor;
using UnityEngine;

namespace VMUnityAutomation.Editor
{
    [VmProjectTool("shader/diagnostics",
        Description = "Read Unity's authoritative ShaderUtil compiler diagnostics for one imported Shader, including hand-written shaders and compiled Shader Graph assets. Shader.isSupported alone does not prove error-free compilation. This reads existing native diagnostics; it does not compile a pass, reimport, clear messages or infer success from the Console.",
        SideEffects = VmProjectToolSideEffect.ReadsProjectState,
        ErrorCodes = new[] { "shader_asset_not_found" },
        CompletionEvidence = "Exact imported shader identity, native support and error state, native message count, explicit truncation and bounded compiler messages with file, line, severity and platform.")]
    public sealed class VmShaderDiagnosticsTool :
        IVmProjectTool<VmShaderDiagnosticsRequest, VmShaderDiagnosticsResult>
    {
        public VmShaderDiagnosticsResult Execute(VmShaderDiagnosticsRequest request)
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(request.AssetPath);
            if (shader == null)
                throw new VmProjectToolException("shader_asset_not_found",
                    $"No imported Shader exists at '{request.AssetPath}'.");
            var messages = ShaderUtil.GetShaderMessages(shader);
            return new VmShaderDiagnosticsResult
            {
                AssetPath = AssetDatabase.GetAssetPath(shader),
                ShaderName = shader.name,
                IsSupported = shader.isSupported,
                HasErrors = ShaderUtil.ShaderHasError(shader),
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
