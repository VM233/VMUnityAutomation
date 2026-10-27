using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace VMUnityAutomation.Editor.Tests
{
    [Category(VmAutomationPackageTestCommands.DefaultPackageSmokeCategory)]
    [Category(VmAutomationPackageTestCommands.FullPackageRegressionCategory)]
    public sealed class VmNineSliceDuplicateTests
    {
        private string folder;
        private string sources;
        private static readonly Color32 Fill = new Color32(47, 38, 55, 255);
        private static readonly Color32 Edge = new Color32(22, 20, 30, 255);

        [SetUp]
        public void SetUp()
        {
            string identity = Guid.NewGuid().ToString("N");
            folder = "Assets/VmNineSliceDuplicateTests-" + identity;
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
            sources = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Temp",
                "VmNineSliceDuplicateTests-" + identity);
            Directory.CreateDirectory(sources);
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(folder);
            foreach (string file in Directory.GetFiles(sources)) File.Delete(file);
            Directory.Delete(sources);
        }

        [TestCase(25, 19, true, true)]
        [TestCase(25, 5, true, false)]
        [TestCase(5, 19, false, true)]
        public void OrdinaryRasterMatchesCompactPanelWithExactReconstruction(int width, int height,
            bool horizontal, bool vertical)
        {
            string canonical = Publish("Compact", 5, 5);
            Publish("Expanded", width, height);
            var result = Audit();
            Assert.That(result["success"], Is.True);
            Assert.That(result["nineSlicePairCount"], Is.EqualTo(1));
            var group = Groups(result).Single();
            Assert.That(group["canonicalAssetPath"], Is.EqualTo(canonical));
            var proof = (Dictionary<string, object>)group["match"];
            Assert.That(proof["stretchX"], Is.EqualTo(horizontal));
            Assert.That(proof["stretchY"], Is.EqualTo(vertical));
            var border = (Dictionary<string, object>)proof["duplicateBorder"];
            Assert.That(border["left"], Is.EqualTo(horizontal ? 1 : 0));
            Assert.That(border["bottom"], Is.EqualTo(vertical ? 1 : 0));
            AssertReconstruction(width, height, 5, 5, border, horizontal, vertical);
        }

        [Test]
        public void DifferentImporterBordersDoNotHideEquivalentArtwork()
        {
            string compact = Publish("Compact", 7, 7);
            string expanded = Publish("Expanded", 25, 19);
            SetBorder(compact, Vector4.one);
            SetBorder(expanded, Vector4.one * 2);
            Assert.That(Audit()["nineSlicePairCount"], Is.EqualTo(1));
        }

        [TestCase("corner")]
        [TestCase("color")]
        [TestCase("thickness")]
        [TestCase("decoration")]
        public void RejectsChangedFixedArtwork(string change)
        {
            Publish("Compact", 7, 7);
            Color32[] pixels = Frame(25, 19, change == "thickness" ? 2 : 1);
            if (change == "corner") pixels[0] = Fill;
            if (change == "color") pixels[25 + 1] = new Color32(48, 38, 55, 255);
            if (change == "decoration") pixels[3 * 25 + 4] = new Color32(220, 10, 10, 255);
            Publish("Changed", 25, 19, pixels);
            Assert.That(Audit()["duplicateGroupCount"], Is.EqualTo(0));
        }

        [Test]
        public void MultipleChangedInteriorRunsAreNotOneNineSlice()
        {
            Color32 red = new Color32(230, 10, 10, 255);
            Color32 blue = new Color32(10, 10, 230, 255);
            Publish("First", 5, 1, new[] { Edge, red, blue, red, Edge });
            Publish("Second", 7, 1, new[] { Edge, red, red, blue, red, red, Edge });
            Assert.That(Audit()["duplicateGroupCount"], Is.EqualTo(0));
        }

        [Test]
        public void InvisibleRgbDoesNotPreventExactRenderingMatch()
        {
            Publish("Compact", 5, 5);
            var pixels = Frame(25, 19);
            pixels[0] = new Color32(230, 10, 150, 0);
            Publish("Expanded", 25, 19, pixels);
            Assert.That(Audit()["nineSlicePairCount"], Is.EqualTo(1));
            Assert.That(Audit(false)["duplicateGroupCount"], Is.EqualTo(0));
        }

        [Test]
        public void ExactPixelOptOutAndFileModeKeepDimensions()
        {
            Publish("Compact", 5, 5);
            Publish("Expanded", 25, 19);
            Assert.That(Audit(false)["duplicateGroupCount"], Is.EqualTo(0));
            var file = Audit(true, "fileBytes");
            Assert.That(file["duplicateGroupCount"], Is.EqualTo(0));
            Assert.That(file["includeNineSlice"], Is.False);
        }

        [Test]
        public void ExactDuplicatesRemainOneGroupAndUniqueAssetCount()
        {
            Publish("A", 5, 5);
            Publish("B", 5, 5);
            Assert.That(Audit()["duplicateGroupCount"], Is.EqualTo(1));
            Publish("Expanded", 25, 19);
            var result = Audit();
            Assert.That(result["duplicateAssetCount"], Is.EqualTo(3));
            Assert.That(result["nineSlicePairCount"], Is.EqualTo(2));
        }

        [TestCase("skip", true, 0)]
        [TestCase("error", false, 0)]
        [TestCase("report", true, 1)]
        public void ImportHandlesExactNineSliceMatchBeforePublication(string action, bool success, int imported)
        {
            string canonical = Publish("Compact", 5, 5);
            string source = WriteSource("Expanded", 25, 19);
            var request = ImportRequest(source, "Result");
            ((Dictionary<string, object>)request["defaults"])["onDuplicate"] = action;
            var result = (Dictionary<string, object>)VmAutomationAssetImportCommands.Import(request);
            Assert.That(result["success"], Is.EqualTo(success));
            Assert.That(File.Exists(Absolute(folder + "/Result.png")), Is.EqualTo(imported == 1));
            if (!success) return;
            Assert.That(result["importedCount"], Is.EqualTo(imported));
            var receipt = Imports(result).Single();
            Assert.That(receipt["duplicateAssetPath"], Is.EqualTo(canonical));
            Assert.That(((Dictionary<string, object>)receipt["duplicateMatch"])["kind"], Is.EqualTo("nineSlice"));
            Assert.That(receipt["imageWidth"], Is.EqualTo(25));
        }

        [Test]
        public void BatchSkippedSourcesResolveDirectlyToPlannedCanonical()
        {
            string first = WriteSource("First", 5, 5);
            string second = WriteSource("Second", 15, 12);
            string third = WriteSource("Third", 25, 19);
            var request = ImportRequest(first, "First");
            var imports = (List<Dictionary<string, object>>)request["imports"];
            imports.Add(ImportItem(second, "Second"));
            imports.Add(ImportItem(third, "Third"));
            request["dryRun"] = true;
            var result = (Dictionary<string, object>)VmAutomationAssetImportCommands.Import(request);
            Assert.That(result["success"], Is.True);
            Assert.That(Imports(result)[1]["duplicateSourceIndex"], Is.EqualTo(0));
            Assert.That(Imports(result)[2]["duplicateSourceIndex"], Is.EqualTo(0));
            Assert.That(Directory.GetFiles(Absolute(folder)), Is.Empty);
        }

        [Test]
        public void OverwriteDoesNotDedupeAgainstItsOwnDestination()
        {
            string destination = Publish("Panel", 25, 19);
            string guid = AssetDatabase.AssetPathToGUID(destination);
            var request = ImportRequest(WriteSource("Compact", 5, 5), "Panel");
            ((Dictionary<string, object>)request["defaults"])["overwrite"] = true;
            var result = (Dictionary<string, object>)VmAutomationAssetImportCommands.Import(request);
            Assert.That(result["success"], Is.True);
            Assert.That(result["importedCount"], Is.EqualTo(1));
            Assert.That(AssetDatabase.AssetPathToGUID(destination), Is.EqualTo(guid));
            Assert.That(AssetDatabase.LoadAssetAtPath<Sprite>(destination).rect.width, Is.EqualTo(5));
        }

        [Test]
        public void SourceRewriteCannotReuseStaleTimestampCache()
        {
            string source = WriteSource("Changing", 5, 5);
            var before = VmAutomationImageDuplicateCommands.CreateFingerprint(source, "decodedPixels");
            DateTime timestamp = File.GetLastWriteTimeUtc(source);
            var pixels = Frame(5, 5);
            pixels[0] = Fill;
            Write(source, 5, 5, pixels);
            File.SetLastWriteTimeUtc(source, timestamp);
            var after = VmAutomationImageDuplicateCommands.CreateFingerprint(source, "decodedPixels");
            Assert.That(after.Hash, Is.Not.EqualTo(before.Hash));
        }

        [Test]
        public void ResourceBudgetsRejectBeforeNativeDecodeOrComparison()
        {
            var budget = new VmImageFingerprintBudget();
            Assert.Throws<VmImageDuplicateBudgetException>(() => budget.AdmitFile(64L * 1024 * 1024 + 1));
            Assert.Throws<VmImageDuplicateBudgetException>(() => budget.AdmitRuns(4 * 1024 * 1024 + 1));
            Assert.Throws<VmImageDuplicateBudgetException>(() => budget.AdmitComparison(16 * 1024 * 1024 + 1));
            byte[] header = new byte[33];
            header[0] = 137; header[1] = 80; header[2] = 78; header[3] = 71;
            header[12] = (byte)'I'; header[13] = (byte)'H'; header[14] = (byte)'D'; header[15] = (byte)'R';
            header[16] = 127; header[20] = 127;
            Assert.Throws<VmImageDuplicateBudgetException>(() => budget.AdmitImage(header, "Large.png"));
        }

        private Dictionary<string, object> Audit(bool include = true, string mode = "decodedPixels") =>
            (Dictionary<string, object>)VmAutomationImageDuplicateCommands.FindDuplicates(
                new Dictionary<string, object> { { "folder", folder }, { "mode", mode }, { "includeNineSlice", include } });

        private static List<Dictionary<string, object>> Groups(Dictionary<string, object> result) =>
            (List<Dictionary<string, object>>)result["groups"];

        private static List<Dictionary<string, object>> Imports(Dictionary<string, object> result) =>
            (List<Dictionary<string, object>>)result["imports"];

        private string Publish(string name, int width, int height, Color32[] pixels = null)
        {
            string path = folder + "/" + name + ".png";
            Write(Absolute(path), width, height, pixels ?? Frame(width, height));
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
            return path;
        }

        private static void SetBorder(string path, Vector4 border)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.spriteBorder = border;
            importer.SaveAndReimport();
        }

        private string WriteSource(string name, int width, int height)
        {
            string path = Path.Combine(sources, name + ".png");
            Write(path, width, height, Frame(width, height));
            return path;
        }

        private Dictionary<string, object> ImportRequest(string source, string destination) =>
            new Dictionary<string, object>
            {
                { "defaults", new Dictionary<string, object>
                    {
                        { "textureType", "Sprite" }, { "spriteMode", "Single" }, { "meshType", "FullRect" },
                        { "pixelsPerUnit", 32 }, { "dedupeMode", "decodedPixels" },
                        { "dedupeScope", "destinationFolder" }, { "filterMode", "Point" },
                        { "mipmapEnabled", false }, { "compression", "uncompressed" },
                    } },
                { "imports", new List<Dictionary<string, object>> { ImportItem(source, destination) } },
            };

        private Dictionary<string, object> ImportItem(string source, string destination) =>
            new Dictionary<string, object>
                { { "sourcePath", source }, { "destinationPath", folder + "/" + destination + ".png" } };

        private static string Absolute(string path) => Path.Combine(Path.GetDirectoryName(Application.dataPath), path);

        private static Color32[] Frame(int width, int height, int border = 1)
        {
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                pixels[y * width + x] = x < border || x >= width - border || y < border || y >= height - border
                    ? Edge : Fill;
            pixels[0] = pixels[width - 1] = pixels[(height - 1) * width] = pixels[pixels.Length - 1] = default;
            return pixels;
        }

        private static void Write(string path, int width, int height, Color32[] pixels)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
            try
            {
                texture.SetPixels32(pixels);
                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
        }

        private static void AssertReconstruction(int width, int height, int compactWidth, int compactHeight,
            Dictionary<string, object> border, bool horizontal, bool vertical)
        {
            var compact = Frame(compactWidth, compactHeight);
            var expanded = Frame(width, height);
            int left = (int)border["left"], right = (int)border["right"];
            int bottom = (int)border["bottom"], top = (int)border["top"];
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                int sx = !horizontal ? x : x < left ? x : x >= width - right ? compactWidth - width + x : left;
                int sy = !vertical ? y : y < bottom ? y : y >= height - top ? compactHeight - height + y : bottom;
                Assert.That(compact[sy * compactWidth + sx], Is.EqualTo(expanded[y * width + x]));
            }
        }
    }
}
