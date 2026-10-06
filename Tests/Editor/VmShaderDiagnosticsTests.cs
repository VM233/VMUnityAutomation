using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace VMUnityAutomation.Editor.Tests
{
    [Category(VmAutomationPackageTestCommands.DefaultPackageSmokeCategory)]
    public sealed class VmShaderDiagnosticsTests
    {
        [Test]
        public void CompilerDiagnosticsHasAValidNativeCatalogContract()
        {
            Assert.That(VmProjectToolRegistry.TryGetToolDetailForDirectRoute(
                "project-tools/call/shader/diagnostics", out _), Is.True);
        }

        [Test]
        public void ImportedShaderReportsTheNativeCompilerState()
        {
            const string path = "Packages/com.vm233.unity-automation/Tests/Fixtures/Test Empty.shader";
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
            Assert.That(shader, Is.Not.Null);
            var result = new VmShaderDiagnosticsTool().Execute(new VmShaderDiagnosticsRequest { AssetPath = path });
            Assert.That(result.AssetPath, Is.EqualTo(path));
            Assert.That(result.ShaderName, Is.EqualTo(shader.name));
            Assert.That(result.AssetType, Is.EqualTo(VmShaderAssetType.Shader));
            Assert.That(result.IsSupported, Is.EqualTo(shader.isSupported));
            Assert.That(result.HasErrors, Is.EqualTo(ShaderUtil.ShaderHasError(shader)));
            Assert.That(result.Diagnostics.Length, Is.EqualTo(ShaderUtil.GetShaderMessages(shader).Length));
            Assert.That(result.DiagnosticCount, Is.EqualTo(result.Diagnostics.Length));
            Assert.That(result.Truncated, Is.False);
        }

        [Test]
        public void ImportedComputeReportsNativeMessagesWithoutInventingSupport()
        {
            const string path = "Packages/com.vm233.unity-automation/Tests/Fixtures/Test Compute.compute";
            var compute = AssetDatabase.LoadAssetAtPath<ComputeShader>(path);
            Assert.That(compute, Is.Not.Null);
            Assert.That(compute.IsSupported(compute.FindKernel("Main")), Is.True);
            var result = new VmShaderDiagnosticsTool().Execute(new VmShaderDiagnosticsRequest { AssetPath = path });
            Assert.That(result.AssetPath, Is.EqualTo(path));
            Assert.That(result.ShaderName, Is.EqualTo(compute.name));
            Assert.That(result.AssetType, Is.EqualTo(VmShaderAssetType.ComputeShader));
            Assert.That(result.IsSupported, Is.Null);
            Assert.That(result.HasErrors, Is.False);
            Assert.That(result.DiagnosticCount, Is.EqualTo(ShaderUtil.GetComputeShaderMessages(compute).Length));
            Assert.That(result.Truncated, Is.False);
        }

        [Test]
        public void BrokenComputeRetainsErrorEvidenceAndExplicitTruncation()
        {
            const string path = "Assets/__VMUnityAutomationComputeDiagnosticsTest.compute";
            Assert.That(File.Exists(path), Is.False, "The temporary diagnostic fixture must not overwrite an asset.");
            try
            {
                File.WriteAllText(path,
                    "#pragma kernel First\n#pragma kernel Second\n" +
                    "[numthreads(1,1,1)] void First(uint3 id:SV_DispatchThreadID){UndefinedFirst(id);}\n" +
                    "[numthreads(1,1,1)] void Second(uint3 id:SV_DispatchThreadID){UndefinedSecond(id);}\n");
                var firstKernelError = new Regex(
                    "^Shader error in '__VMUnityAutomationComputeDiagnosticsTest': " +
                    "undeclared identifier 'UndefinedFirst' at kernel First at " +
                    "__VMUnityAutomationComputeDiagnosticsTest\\.compute\\(\\d+\\) \\(on [^)]+\\)$");
                LogAssert.Expect(LogType.Error, firstKernelError);
                LogAssert.Expect(LogType.Error, new Regex(
                    "^Shader error in '__VMUnityAutomationComputeDiagnosticsTest': " +
                    "undeclared identifier 'UndefinedSecond' at kernel Second at " +
                    "__VMUnityAutomationComputeDiagnosticsTest\\.compute\\(\\d+\\) \\(on [^)]+\\)$"));
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                Assert.DoesNotThrow(LogAssert.NoUnexpectedReceived,
                    "Synchronous compute import emitted an unexpected diagnostic log.");
                var compute = AssetDatabase.LoadAssetAtPath<ComputeShader>(path);
                Assert.That(compute, Is.Not.Null);
                Assert.That(compute.IsSupported(compute.FindKernel("First")), Is.False);
                Assert.DoesNotThrow(LogAssert.NoUnexpectedReceived,
                    "The first native kernel request emitted an unexpected diagnostic log.");
                // Requesting another native program also re-emits the first kernel's error.
                LogAssert.Expect(LogType.Error, firstKernelError);
                Assert.That(compute.IsSupported(compute.FindKernel("Second")), Is.False);
                Assert.DoesNotThrow(LogAssert.NoUnexpectedReceived,
                    "The second native kernel request emitted an unexpected diagnostic log.");
                int nativeCount = ShaderUtil.GetComputeShaderMessageCount(compute);
                var native = ShaderUtil.GetComputeShaderMessages(compute);
                Assert.That(native.Length, Is.EqualTo(nativeCount));
                Assert.That(native.Length, Is.GreaterThan(1));
                var result = new VmShaderDiagnosticsTool().Execute(new VmShaderDiagnosticsRequest
                    { AssetPath = path, MaxDiagnostics = 1 });
                Assert.That(result.HasErrors, Is.True);
                Assert.That(result.DiagnosticCount, Is.EqualTo(native.Length));
                Assert.That(result.Truncated, Is.True);
                Assert.That(result.Diagnostics.Single().Message, Is.EqualTo(native[0].message));
                Assert.That(result.Diagnostics.Single().Line, Is.EqualTo(native[0].line));
                Assert.That(result.Diagnostics.Single().Severity, Is.EqualTo(native[0].severity.ToString()));
            }
            finally
            {
                AssetDatabase.DeleteAsset(path);
            }
        }

        [Test]
        public void MissingShaderRejectsAtTheNativeAssetBoundary()
        {
            Assert.Throws<VmProjectToolException>(() => new VmShaderDiagnosticsTool().Execute(
                new VmShaderDiagnosticsRequest { AssetPath = "Assets/No Such Shader.shader" }));
        }
    }
}
