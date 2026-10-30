using System;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace VMUnityAutomation.Editor.Tests
{
    [Category(VmAutomationPackageTestCommands.DefaultPackageSmokeCategory)]
    [Category(VmAutomationPackageTestCommands.FullPackageRegressionCategory)]
    public sealed class VmRenderTextureSnapshotTests
    {
        [TestCase(1, false)]
        [TestCase(1, true)]
        [TestCase(4, false)]
        [TestCase(4, true)]
        public void SnapshotPreservesNativeRgbaAndPriorActiveTarget(int msaa, bool transparent)
        {
            var source = new RenderTexture(8, 8, 0, RenderTextureFormat.ARGB32) { antiAliasing = msaa };
            var sentinel = new RenderTexture(2, 2, 0, RenderTextureFormat.ARGB32);
            Texture2D decoded = null;
            RenderTexture previous = RenderTexture.active;
            string path = Path.Combine(Application.dataPath, "../Temp/NativeRenderSnapshot-" + Guid.NewGuid().ToString("N") + ".png");
            try
            {
                Assert.That(source.Create(), Is.True);
                Assert.That(source.antiAliasing, Is.EqualTo(msaa), "MSAA control must be actually supported.");
                sentinel.Create();
                RenderTexture.active = source;
                GL.Clear(false, true, transparent ? Color.clear : Color.red);
                RenderTexture.active = sentinel;
                var result = new VmRenderTextureSnapshotTool().Execute(new VmRenderTextureSnapshotRequest
                    { RenderTextureInstanceId = VmObjectId.Get(source), OutputPath = Path.GetFullPath(path) });
                Assert.That(RenderTexture.active, Is.SameAs(sentinel));
                Assert.That(result.NonzeroAlphaPixels, Is.EqualTo(transparent ? 0 : 64));
                Assert.That(result.OpaquePixels, Is.EqualTo(transparent ? 0 : 64));
                Assert.That(result.AlphaBoundsBottomLeft, Is.EqualTo(transparent ? new int[0] : new[] { 0, 0, 8, 8 }));
                Assert.That(result.Sha256.Length, Is.EqualTo(64));
                decoded = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                Assert.That(decoded.LoadImage(File.ReadAllBytes(path)), Is.True);
                var color = decoded.GetPixels32()[0];
                Assert.That(color.a, Is.EqualTo(transparent ? 0 : 255));
                Assert.That(color.r, Is.EqualTo(transparent ? 0 : 255));
                Assert.That(color.g, Is.Zero);
                Assert.That(color.b, Is.Zero);
                Assert.That(new FileInfo(path).Length, Is.EqualTo(result.PngBytes));
            }
            finally
            {
                RenderTexture.active = previous;
                if (decoded != null) UnityEngine.Object.DestroyImmediate(decoded);
                source.Release(); sentinel.Release();
                UnityEngine.Object.DestroyImmediate(source); UnityEngine.Object.DestroyImmediate(sentinel);
                if (File.Exists(path)) File.Delete(path);
            }
        }

        [TestCase("missing", "render_snapshot_source")]
        [TestCase("wrong-type", "render_snapshot_source")]
        [TestCase("uncreated", "render_snapshot_source")]
        [TestCase("released", "render_snapshot_source")]
        [TestCase("size", "render_snapshot_limit")]
        [TestCase("format", "render_snapshot_format")]
        [TestCase("outside", "render_snapshot_path")]
        [TestCase("overwrite", "render_snapshot_path")]
        public void RejectsInvalidSourceOrPublicationBeforeReplacingState(string scenario, string code)
        {
            var source = new RenderTexture(scenario == "size" ? 1025 : 8, 8, 0,
                scenario == "format" ? RenderTextureFormat.ARGBHalf : RenderTextureFormat.ARGB32);
            var wrong = new Texture2D(2, 2);
            RenderTexture previous = RenderTexture.active;
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../Temp/NativeRenderReject-" + Guid.NewGuid().ToString("N") + ".png"));
            try
            {
                if (scenario != "uncreated") source.Create();
                if (scenario == "released") source.Release();
                if (scenario == "overwrite") File.WriteAllText(path, "preserve-existing");
                string identity = scenario == "missing" ? "0" : VmObjectId.Get(scenario == "wrong-type" ? (UnityEngine.Object)wrong : source);
                string output = scenario == "outside" ? Path.GetFullPath(Path.Combine(Application.dataPath, "Rejected.png")) : path;
                var error = Assert.Throws<VmProjectToolException>(() => new VmRenderTextureSnapshotTool().Execute(
                    new VmRenderTextureSnapshotRequest { RenderTextureInstanceId = identity, OutputPath = output }));
                Assert.That(error.ErrorCode, Is.EqualTo(code));
                Assert.That(RenderTexture.active, Is.SameAs(previous));
                if (scenario == "overwrite") Assert.That(File.ReadAllText(path), Is.EqualTo("preserve-existing"));
                else Assert.That(File.Exists(path), Is.False);
            }
            finally
            {
                source.Release(); UnityEngine.Object.DestroyImmediate(source); UnityEngine.Object.DestroyImmediate(wrong);
                if (File.Exists(path)) File.Delete(path);
            }
        }
    }
}
