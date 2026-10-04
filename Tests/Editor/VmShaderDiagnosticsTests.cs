using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace VMUnityAutomation.Editor.Tests
{
    [Category(VmAutomationPackageTestCommands.DefaultPackageSmokeCategory)]
    public sealed class VmShaderDiagnosticsTests
    {
        [Test]
        public void ImportedShaderReportsTheNativeCompilerState()
        {
            const string path = "Packages/com.vm233.unity-automation/Tests/Fixtures/Test Empty.shader";
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
            Assert.That(shader, Is.Not.Null);
            var result = new VmShaderDiagnosticsTool().Execute(new VmShaderDiagnosticsRequest { AssetPath = path });
            Assert.That(result.AssetPath, Is.EqualTo(path));
            Assert.That(result.ShaderName, Is.EqualTo(shader.name));
            Assert.That(result.IsSupported, Is.EqualTo(shader.isSupported));
            Assert.That(result.HasErrors, Is.EqualTo(ShaderUtil.ShaderHasError(shader)));
            Assert.That(result.Diagnostics.Length, Is.EqualTo(ShaderUtil.GetShaderMessages(shader).Length));
            Assert.That(result.DiagnosticCount, Is.EqualTo(result.Diagnostics.Length));
            Assert.That(result.Truncated, Is.False);
        }

        [Test]
        public void MissingShaderRejectsAtTheNativeAssetBoundary()
        {
            Assert.Throws<VmProjectToolException>(() => new VmShaderDiagnosticsTool().Execute(
                new VmShaderDiagnosticsRequest { AssetPath = "Assets/No Such Shader.shader" }));
        }
    }
}
