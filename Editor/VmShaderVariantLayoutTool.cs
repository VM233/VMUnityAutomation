using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace VMUnityAutomation.Editor
{
    [VmProjectTool("shader/variant-layout", LongRunning = true, MutatesRuntime = true,
        Description = "Compile exactly one imported Shader subshader/pass/stage/keyword variant with Unity ShaderData. Return native constant-buffer byte sizes and bounded top-level field names and array sizes, native program byte count and compiler messages. No bytecode, source, recursive fields or variant enumeration is returned. This may compile and populate native caches/diagnostics; it does not render, change materials, reimport or write Assets. Layout size is not measured per-draw upload traffic or proof that a renderer selects this variant. Native compilation is synchronous and cannot be interrupted mid-compile; the durable job completes after native return.",
        SideEffects = VmProjectToolSideEffect.ReadsProjectState | VmProjectToolSideEffect.ChangesRuntimeState,
        Preconditions = new[] { "editor-connected", "stable-edit-mode" },
        ErrorCodes = new[] { "shader_asset_not_found", "shader_variant_invalid_request", "shader_variant_edit_mode_required" },
        CompletionEvidence = "Exact shader, subshader/pass/stage/platform/build target/keyword set, native compile result, complete total counts/byte sums, bounded constant layouts and diagnostics with explicit truncation.")]
    public sealed class VmShaderVariantLayoutTool : IVmProjectTool<VmShaderVariantLayoutRequest, VmShaderVariantLayoutResult>
    {
        public VmShaderVariantLayoutResult Execute(VmShaderVariantLayoutRequest request)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new VmProjectToolException("shader_variant_edit_mode_required", "Shader variant compilation requires stable Edit Mode.");
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(request.AssetPath);
            if (shader == null) throw new VmProjectToolException("shader_asset_not_found", "No imported Shader exists at '" + request.AssetPath + "'.");
            var keywords = request.Keywords;
            if (keywords == null || keywords.Length > 64 || keywords.Any(value => string.IsNullOrWhiteSpace(value) || value.Length > 128) ||
                keywords.Distinct(StringComparer.Ordinal).Count() != keywords.Length ||
                request.MaxBuffers < 1 || request.MaxBuffers > 128 || request.MaxFieldsPerBuffer < 1 || request.MaxFieldsPerBuffer > 256 ||
                request.MaxDiagnostics < 1 || request.MaxDiagnostics > 200)
                throw new VmProjectToolException("shader_variant_invalid_request", "Invalid keyword set or output bounds.");
            var data = ShaderUtil.GetShaderData(shader);
            if (request.Subshader < 0 || request.Subshader >= data.SubshaderCount)
                throw new VmProjectToolException("shader_variant_invalid_request", "Subshader index is outside the imported shader.");
            var subshader = data.GetSubshader(request.Subshader);
            if (request.Pass < 0 || request.Pass >= subshader.PassCount)
                throw new VmProjectToolException("shader_variant_invalid_request", "Pass index is outside the imported subshader.");
            var pass = subshader.GetPass(request.Pass);
            if (!pass.HasShaderStage(request.Stage))
                throw new VmProjectToolException("shader_variant_invalid_request", "The selected pass does not have the requested shader stage.");
            var compiled = pass.CompileVariant(request.Stage, keywords, request.Platform, request.BuildTarget);
            var buffers = compiled.ConstantBuffers ?? Array.Empty<ShaderData.ConstantBufferInfo>();
            var messages = compiled.Messages ?? Array.Empty<ShaderMessage>();
            var layouts = buffers.Take(request.MaxBuffers).Select(buffer =>
            {
                var fields = buffer.Fields ?? Array.Empty<ShaderData.ConstantInfo>();
                return new VmShaderConstantBufferLayout
                {
                    Name = buffer.Name, SizeBytes = buffer.Size, FieldCount = fields.Length,
                    FieldNames = fields.Take(request.MaxFieldsPerBuffer).Select(field => field.Name).ToArray(),
                    FieldArraySizes = fields.Take(request.MaxFieldsPerBuffer).Select(field => field.ArraySize).ToArray(),
                    FieldsTruncated = fields.Length > request.MaxFieldsPerBuffer
                };
            }).ToArray();
            return new VmShaderVariantLayoutResult
            {
                AssetPath = AssetDatabase.GetAssetPath(shader), ShaderName = shader.name, PassName = pass.Name, Request = request,
                Compiled = compiled.Success, ProgramBytes = compiled.ShaderData?.Length ?? 0,
                BufferCount = buffers.Length, ConstantBufferBytes = buffers.Sum(buffer => (long)buffer.Size), Buffers = layouts,
                DiagnosticCount = messages.Length,
                Diagnostics = messages.Take(request.MaxDiagnostics).Select(message => new VmShaderDiagnostic
                { Severity = message.severity.ToString(), Message = message.message, File = message.file, Line = message.line, Platform = message.platform.ToString() }).ToArray(),
                Truncated = buffers.Length > request.MaxBuffers || messages.Length > request.MaxDiagnostics || layouts.Any(layout => layout.FieldsTruncated)
            };
        }
    }
}
