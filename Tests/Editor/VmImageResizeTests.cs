using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;

namespace VMUnityAutomation.Editor.Tests
{
    [Category(VmAutomationPackageTestCommands.DefaultPackageSmokeCategory)]
    [Category(VmAutomationPackageTestCommands.FullPackageRegressionCategory)]
    public sealed class VmImageResizeTests
    {
        private string directory;
        private string sourcePath;
        private string outputPath;

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetDirectoryName(Application.dataPath),
                "Temp", "VmImageResizeTests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            sourcePath = Path.Combine(directory, "Source.png");
            outputPath = Path.Combine(directory, "Output.png");
        }

        [TearDown]
        public void TearDown()
        {
            foreach (string path in Directory.GetFiles(directory)) File.Delete(path);
            Directory.Delete(directory);
        }

        [Test]
        public void BilinearPreservesColorAtTransparentEdge()
        {
            byte[] before = WriteSource(2, 1, new[]
            {
                new Color32(255, 0, 0, 255), new Color32(0, 0, 255, 0)
            });
            VmImageResizeResult result = Execute(1, null);
            Texture2D output = ReadOutput();
            try
            {
                Assert.That(output.GetPixels32()[0], Is.EqualTo(new Color32(255, 0, 0, 128)));
                Assert.That(result.Width, Is.EqualTo(1));
                Assert.That(result.Height, Is.EqualTo(1));
                Assert.That(result.Verified, Is.True);
                Assert.That(result.OutputBytes, Is.EqualTo(new FileInfo(outputPath).Length));
                Assert.That(result.SourceSha256, Has.Length.EqualTo(64));
                Assert.That(result.OutputSha256, Has.Length.EqualTo(64));
                Assert.That(File.ReadAllBytes(sourcePath), Is.EqualTo(before));
                Assert.That(Directory.GetFiles(directory), Has.Length.EqualTo(2));
            }
            finally { UnityEngine.Object.DestroyImmediate(output); }
        }

        [Test]
        public void NearestEnlargementPreservesOrientationAndExactSamples()
        {
            var colors = new[] { new Color32(255, 0, 0, 255), new Color32(0, 255, 0, 255),
                new Color32(0, 0, 255, 255), new Color32(255, 255, 255, 0) };
            WriteSource(2, 2, colors);
            Execute(4, 4, VmImageResizeFilter.Nearest);
            Texture2D output = ReadOutput();
            try
            {
                Color32[] pixels = output.GetPixels32();
                Assert.That(pixels[0], Is.EqualTo(colors[0]));
                Assert.That(pixels[3], Is.EqualTo(colors[1]));
                Assert.That(pixels[12], Is.EqualTo(colors[2]));
                Assert.That(pixels[15], Is.EqualTo(colors[3]));
            }
            finally { UnityEngine.Object.DestroyImmediate(output); }
        }

        [TestCase(5, 5, 5, 3)]
        [TestCase(3, 9, 3, 2)]
        [TestCase(0, 3, 5, 3)]
        [TestCase(4, 0, 4, 3)]
        public void DimensionsPreserveAspectRatioWithinRequestedBox(int width, int height,
            int expectedWidth, int expectedHeight)
        {
            WriteSource(8, 5, new Color32[40]);
            VmImageResizeResult result = Execute(width == 0 ? (int?)null : width,
                height == 0 ? (int?)null : height);
            Assert.That(result.Width, Is.EqualTo(expectedWidth));
            Assert.That(result.Height, Is.EqualTo(expectedHeight));
        }

        [Test]
        public void OriginalDimensionsPreserveEncodedBytes()
        {
            byte[] before = WriteSource(2, 1, new Color32[2]);
            VmImageResizeResult result = Execute(2, 1);
            Assert.That(File.ReadAllBytes(outputPath), Is.EqualTo(before));
            Assert.That(result.OutputSha256, Is.EqualTo(result.SourceSha256));
        }

        [Test]
        public void DryRunCreatesNoFilesAndDoesNotClaimVerification()
        {
            WriteSource(2, 1, new Color32[2]);
            VmImageResizeResult result = Execute(10, 10, dryRun: true);
            Assert.That(result.DryRun, Is.True);
            Assert.That(result.Verified, Is.False);
            Assert.That(result.Width, Is.EqualTo(10));
            Assert.That(result.Height, Is.EqualTo(5));
            Assert.That(result.OutputSha256, Is.Empty);
            Assert.That(File.Exists(outputPath), Is.False);
            Assert.That(Directory.GetFiles(directory), Has.Length.EqualTo(1));
        }

        [Test]
        public void ExistingOutputRequiresExplicitOverwrite()
        {
            WriteSource(2, 1, new Color32[2]);
            var previous = new byte[] { 1, 2, 3 };
            File.WriteAllBytes(outputPath, previous);
            AssertError(() => Execute(1, 1), "image_output_exists");
            Assert.That(File.ReadAllBytes(outputPath), Is.EqualTo(previous));
            Assert.That(Execute(1, 1, overwrite: true).Verified, Is.True);
        }

        [Test]
        public void SourceCannotBeOverwritten()
        {
            byte[] original = WriteSource(2, 1, new Color32[2]);
            outputPath = sourcePath;
            AssertError(() => Execute(1, 1, overwrite: true), "invalid_resize_arguments");
            Assert.That(File.ReadAllBytes(sourcePath), Is.EqualTo(original));
        }

        [TestCase(0, 1)]
        [TestCase(-1, 1)]
        [TestCase(4097, 1)]
        [TestCase(1, -1)]
        public void InvalidDimensionsFailBeforePublication(int width, int height)
        {
            WriteSource(2, 1, new Color32[2]);
            AssertError(() => Execute(width, height), "invalid_resize_arguments");
            Assert.That(File.Exists(outputPath), Is.False);
        }

        [Test]
        public void MissingDimensionsAndInvalidFilterAreRejected()
        {
            WriteSource(2, 1, new Color32[2]);
            AssertError(() => Execute(null, null), "invalid_resize_arguments");
            AssertError(() => Execute(1, 1, (VmImageResizeFilter)100), "invalid_resize_arguments");
        }

        [Test]
        public void MissingAndInvalidPngAreRejected()
        {
            AssertError(() => Execute(1, 1), "image_source_not_found");
            File.WriteAllBytes(sourcePath, new byte[] { 1, 2, 3 });
            AssertError(() => Execute(1, 1), "image_format_unsupported");
            Assert.That(File.Exists(outputPath), Is.False);
        }

        [Test]
        public void HeaderAndOutputPixelLimitsAreRejectedBeforeDecode()
        {
            byte[] png = WriteSource(2, 1, new Color32[2]);
            png[16] = 0x7f;
            File.WriteAllBytes(sourcePath, png);
            AssertError(() => Execute(1, 1), "image_size_limit");
            WriteSource(1, 1, new Color32[1]);
            AssertError(() => Execute(4096, 4096, dryRun: true), "image_size_limit");
            Assert.That(File.Exists(outputPath), Is.False);
        }

        [TestCase("Assets")]
        [TestCase("Packages")]
        public void UnityAssetOutputsAreRejectedWithoutImporterChanges(string protectedFolder)
        {
            WriteSource(2, 1, new Color32[2]);
            outputPath = Path.Combine(Path.GetDirectoryName(Application.dataPath),
                protectedFolder, "Vm Image Resize Must Not Exist.png");
            AssertError(() => Execute(1, 1), "image_output_is_unity_asset");
            Assert.That(File.Exists(outputPath), Is.False);
        }

        [Test]
        public void TransportPublishesDimensionsHashesAndVerification()
        {
            WriteSource(2, 1, new Color32[2]);
            var result = (Dictionary<string, object>)VmJsonContract.ToTransportValue(Execute(1, 1));
            Assert.That(result["width"], Is.EqualTo(1));
            Assert.That(result["height"], Is.EqualTo(1));
            Assert.That(result["sourceWidth"], Is.EqualTo(2));
            Assert.That(result["sourceHeight"], Is.EqualTo(1));
            Assert.That(result["verified"], Is.True);
            Assert.That(result["sourceSha256"], Has.Length.EqualTo(64));
            Assert.That(result["outputSha256"], Has.Length.EqualTo(64));
            Assert.That(result["outputBytes"], Is.EqualTo(new FileInfo(outputPath).Length));

            var schema = VmJsonContract.CreateSchema(typeof(VmImageResizeResult));
            var properties = (Dictionary<string, object>)schema["properties"];
            Assert.That(properties.Keys, Is.EquivalentTo(result.Keys));
            Assert.That(schema["required"], Is.EquivalentTo(result.Keys));
            Assert.That(schema["additionalProperties"], Is.False);
        }

        [Test]
        public void CatalogUsesTypedPackageContractWithoutPpuArgument()
        {
            VmProjectToolRegistry.ResetCacheForTests();
            Assert.That(VmAutomationCatalog.TryGetTool("vm_pt_image_resize", false,
                out Dictionary<string, object> tool), Is.True);
            Assert.That(tool["package"], Is.EqualTo("com.vm233.unity-automation"));
            var schema = VmJsonContract.CreateSchema(typeof(VmImageResizeRequest));
            Assert.That(schema["additionalProperties"], Is.False);
            var properties = (Dictionary<string, object>)schema["properties"];
            Assert.That(properties.ContainsKey("pixelsPerUnit"), Is.False);
            Assert.That(properties.ContainsKey("width"), Is.True);
        }

        private VmImageResizeResult Execute(int? width, int? height,
            VmImageResizeFilter filter = VmImageResizeFilter.Bilinear,
            bool overwrite = false, bool dryRun = false) => new VmImageResizeTool().Execute(
            new VmImageResizeRequest { SourcePath = sourcePath, OutputPath = outputPath,
                Width = width, Height = height, Filter = filter, Overwrite = overwrite, DryRun = dryRun });

        private byte[] WriteSource(int width, int height, Color32[] pixels)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
            try
            {
                texture.SetPixels32(pixels);
                byte[] bytes = ImageConversion.EncodeToPNG(texture);
                File.WriteAllBytes(sourcePath, bytes);
                return bytes;
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
        }

        private Texture2D ReadOutput()
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            Assert.That(ImageConversion.LoadImage(texture, File.ReadAllBytes(outputPath)), Is.True);
            return texture;
        }

        private static void AssertError(TestDelegate action, string code) =>
            Assert.That(Assert.Throws<VmProjectToolException>(action).ErrorCode, Is.EqualTo(code));
    }
}
