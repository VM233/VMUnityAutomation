using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace VMUnityAutomation.Editor.Tests
{
    [Category(VmAutomationPackageTestCommands.DefaultPackageSmokeCategory)]
    [Category(VmAutomationPackageTestCommands.FullPackageRegressionCategory)]
    public sealed class VmShaderVariantLayoutTests
    {
        const string ShaderPath = "Packages/com.vm233.unity-automation/Tests/Fixtures/Test Variant Layout.shader";
        [Test]
        public void ContractIsDiscoverable()
        {
            Assert.That(VmProjectToolRegistry.TryGetToolDetailForDirectRoute("project-tools/call/shader/variant-layout", out _), Is.True);
        }
        [Test]
        public void ExactVariantMatchesNativeBufferLayout()
        {
            var request = new VmShaderVariantLayoutRequest { AssetPath = ShaderPath, MaxBuffers = 1, MaxFieldsPerBuffer = 1 };
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(ShaderPath);
            var native = ShaderUtil.GetShaderData(shader).GetSubshader(0).GetPass(0).CompileVariant(request.Stage, request.Keywords, request.Platform, request.BuildTarget);
            var result = new VmShaderVariantLayoutTool().Execute(request);
            Assert.That(result.Compiled, Is.EqualTo(native.Success));
            Assert.That(result.Compiled, Is.True);
            Assert.That(result.ProgramBytes, Is.EqualTo(native.ShaderData.Length));
            Assert.That(result.BufferCount, Is.EqualTo(native.ConstantBuffers.Length));
            Assert.That(result.ConstantBufferBytes, Is.EqualTo(native.ConstantBuffers.Sum(buffer => (long)buffer.Size)));
            Assert.That(result.Buffers.Length, Is.EqualTo(System.Math.Min(1, native.ConstantBuffers.Length)));
            Assert.That(result.Truncated, Is.EqualTo(native.ConstantBuffers.Length > 1 || native.ConstantBuffers.Take(1).Any(buffer => buffer.Fields.Length > 1) || (native.Messages?.Length ?? 0) > request.MaxDiagnostics));
        }
        [Test]
        public void InvalidPassAndKeywordSetRejectBeforeNativeCompile()
        {
            var tool = new VmShaderVariantLayoutTool();
            var pass = Assert.Throws<VmProjectToolException>(() => tool.Execute(new VmShaderVariantLayoutRequest { AssetPath = ShaderPath, Pass = 255 }));
            Assert.That(pass.ErrorCode, Is.EqualTo("shader_variant_invalid_request"));
            var keywords = Assert.Throws<VmProjectToolException>(() => tool.Execute(new VmShaderVariantLayoutRequest { AssetPath = ShaderPath, Keywords = new[] { "DUPLICATE", "DUPLICATE" } }));
            Assert.That(keywords.ErrorCode, Is.EqualTo("shader_variant_invalid_request"));
        }
    }
}
