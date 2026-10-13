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
    public sealed class VmAssetImportCenterTests
    {
        private string directory;
        private string source;
        private string folder;
        private Color32[] pixels;

        [SetUp]
        public void SetUp()
        {
            string id = Guid.NewGuid().ToString("N");
            directory = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Temp", "VmCenterTests-" + id);
            Directory.CreateDirectory(directory);
            source = Path.Combine(directory, "Source.png");
            folder = "Assets/VmCenterTests-" + id;
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
            pixels = new Color32[32 * 32];
            pixels[4 * 32 + 3] = new Color32(203, 71, 9, 255);
            pixels[4 * 32 + 4] = new Color32(53, 29, 211, 0);
            pixels[6 * 32 + 5] = new Color32(4, 167, 19, 127);
            pixels[12 * 32 + 22] = new Color32(13, 44, 209, 255);
            Write();
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(folder);
            File.Delete(source);
            Directory.Delete(directory);
        }

        [Test]
        public void DisconnectedPartsAndTransparentRgbKeepExactOrientationAndHalfPixelCenter()
        {
            var image = Prepare(Center());
            Texture2D decoded = VmPngImage.Decode(image.Bytes, source, 8, 8);
            try
            {
                Color32[] output = decoded.GetPixels32();
                for (int y = 0; y < 3; y++)
                    for (int x = 0; x < 3; x++)
                        Assert.That(output[(2 + y) * 8 + 2 + x], Is.EqualTo(pixels[(4 + y) * 32 + 3 + x]));
                Assert.That(output.Count(pixel => pixel.a != 0), Is.EqualTo(2));
                Assert.That(image.Center["sourceRect"], Is.EqualTo(Rect(3, 4, 3, 3)));
                Assert.That(image.Center["centerErrorX"], Is.EqualTo(-0.5));
                Assert.That(image.Center["centerErrorY"], Is.EqualTo(-0.5));
            }
            finally { UnityEngine.Object.DestroyImmediate(decoded); }
        }

        [Test]
        public void ExplicitFaintThresholdRemovesOnlyOutsideBounds()
        {
            pixels[0] = new Color32(11, 22, 33, 1);
            pixels[5 * 32 + 4] = new Color32(44, 55, 66, 1);
            Write();
            var values = Center();
            values["backgroundAlphaMax"] = 1;
            var image = Prepare(values);
            Assert.That(image.Center["backgroundPixelsRemoved"], Is.EqualTo(1));
            Assert.That(image.Center["artworkPixels"], Is.EqualTo(2));
            Texture2D decoded = VmPngImage.Decode(image.Bytes, source, 8, 8);
            try { Assert.That(decoded.GetPixels32()[3 * 8 + 3], Is.EqualTo(pixels[5 * 32 + 4])); }
            finally { UnityEngine.Object.DestroyImmediate(decoded); }
        }

        [Test]
        public void DefaultThresholdKeepsEveryNontransparentPixel()
        {
            pixels[0] = new Color32(11, 22, 33, 1);
            Write();
            var image = Prepare(Center());
            Assert.That(image.Center["sourceRect"], Is.EqualTo(Rect(0, 0, 6, 7)));
            Assert.That(image.Center["backgroundPixelsRemoved"], Is.EqualTo(0));
        }

        [Test]
        public void NativeSheetPublishesIndependentSingleSpritesAndVerifiedClosedReceipts()
        {
            byte[] original = File.ReadAllBytes(source);
            var second = Center();
            second["sourceRect"] = Rect(16, 0, 16, 32);
            var result = Import(Request(Item("Left", Center()), Item("Right", second)));
            Assert.That(result["success"], Is.True);
            Assert.That(File.ReadAllBytes(source), Is.EqualTo(original));
            var rows = (List<Dictionary<string, object>>)result["imports"];
            foreach (var row in rows)
            {
                string path = (string)row["destinationPath"];
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Single));
                Assert.That(settings.spriteMeshType, Is.EqualTo(SpriteMeshType.FullRect));
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                Assert.That(sprite.rect, Is.EqualTo(new Rect(0, 0, 8, 8)));
                Assert.That(sprite.pixelsPerUnit, Is.EqualTo(100));
                Assert.That(sprite.pivot, Is.EqualTo(new Vector2(4, 4)));
                Assert.That(sprite.name, Is.EqualTo(Path.GetFileNameWithoutExtension(path)));
                Assert.That(AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().Count(), Is.EqualTo(1));
                var receipt = (Dictionary<string, object>)row["center"];
                Assert.That(receipt["verified"], Is.True);
                Assert.That(receipt["outputSha256"], Is.EqualTo(VmPngImage.Hash(File.ReadAllBytes(Absolute(path)))));
                Assert.That(((Dictionary<string, object>)receipt["sourceRect"]).Keys,
                    Is.EquivalentTo(new[] { "x", "y", "width", "height" }));
            }
            Assert.That(rows[0]["currentGuid"], Is.Not.EqualTo(rows[1]["currentGuid"]));
        }

        [TestCase("crop")]
        [TestCase("resize")]
        [TestCase("spriteGrid")]
        public void ExclusiveOperationsRejectBeforePublication(string operation)
        {
            var item = Item("Conflict", Center());
            item[operation] = new Dictionary<string, object>();
            AssertRejected(Request(item), "invalid_image_center");
        }

        [TestCase("spriteMode", "Multiple")]
        [TestCase("meshType", "Tight")]
        public void ConflictingImporterSettingsRejectBeforePublication(string key, string value)
        {
            var item = Item("Conflict", Center());
            item[key] = value;
            AssertRejected(Request(item), "invalid_image_center");
        }

        [Test]
        public void SpriteSliceConflictReachesCenterOwnerWithValidTypedSlice()
        {
            var item = Item("Conflict", Center());
            item["spriteSlice"] = new Dictionary<string, object> { { "frameWidth", 8 }, { "frameHeight", 8 } };
            AssertRejected(Request(item), "invalid_image_center");
        }

        [TestCase("empty", "image_empty_subject")]
        [TestCase("oversized", "image_subject_too_large")]
        [TestCase("outside", "invalid_image_center")]
        public void InvalidLaterSubjectRejectsWholeBatch(string condition, string code)
        {
            var invalid = Center();
            if (condition == "empty") invalid["sourceRect"] = Rect(8, 0, 8, 8);
            if (condition == "oversized") invalid["width"] = 2;
            if (condition == "outside") invalid["sourceRect"] = Rect(31, 0, 2, 2);
            AssertRejected(Request(Item("First", Center()), Item("Invalid", invalid)), code);
        }

        [Test]
        public void MissingProjectPpuAndFractionalDimensionsReject()
        {
            var request = Request(Item("Ppu", Center()));
            ((Dictionary<string, object>)request["defaults"]).Remove("pixelsPerUnit");
            AssertRejected(request, "invalid_image_center");
            var values = Center();
            values["width"] = 3.5;
            Assert.That(Assert.Throws<VmProjectToolException>(() => Prepare(values)).ErrorCode,
                Is.EqualTo("invalid_image_center"));
        }

        [Test]
        public void BudgetRejectsBeforeDecodeAndDryRunHasNoPublication()
        {
            var settings = Settings(Center());
            Assert.That(Assert.Throws<VmProjectToolException>(() => VmPngCenterPreparation.Prepare(
                settings, source, 1024, 1024 * 1024)).ErrorCode, Is.EqualTo("image_size_limit"));
            var request = Request(Item("Preview", Center()));
            request["dryRun"] = true;
            Assert.That(Import(request)["success"], Is.True);
            Assert.That(Directory.GetFiles(directory), Is.EqualTo(new[] { source }));
            Assert.That(Directory.GetFiles(Absolute(folder)), Is.Empty);
        }

        [Test]
        public void CatalogExposesCenterAndSubjectRejectionCodes()
        {
            Assert.That(VmAutomationCatalog.TryGetTool("asset/import", true, out var tool), Is.True);
            Assert.That((System.Collections.IEnumerable)tool["errorCodes"],
                Does.Contain("invalid_image_center").And.Contain("image_empty_subject").And.Contain("image_subject_too_large"));
            var properties = (Dictionary<string, object>)((Dictionary<string, object>)tool["inputSchema"])["properties"];
            var defaults = (Dictionary<string, object>)properties["defaults"];
            Assert.That((Dictionary<string, object>)defaults["properties"], Contains.Key("center"));
        }

        private void AssertRejected(Dictionary<string, object> request, string code)
        {
            var result = Import(request);
            Assert.That(result["success"], Is.False);
            Assert.That(result["errorCode"], Is.EqualTo(code));
            Assert.That(Directory.GetFiles(Absolute(folder)), Is.Empty);
        }

        private void Write() => File.WriteAllBytes(source, VmPngImage.Encode(pixels, 32, 32, source));
        private VmPreparedPng Prepare(Dictionary<string, object> center) => VmPngCenterPreparation.Prepare(
            Settings(center), source, 64L * 1024 * 1024, 256L * 1024 * 1024);
        private static Dictionary<string, object> Settings(Dictionary<string, object> center) => new()
            { { "center", center }, { "pixelsPerUnit", 100 } };
        private static Dictionary<string, object> Rect(int x, int y, int width, int height) => new()
            { { "x", x }, { "y", y }, { "width", width }, { "height", height } };
        private static Dictionary<string, object> Center() => new()
            { { "width", 8 }, { "height", 8 }, { "sourceRect", Rect(0, 0, 16, 32) } };
        private Dictionary<string, object> Item(string name, Dictionary<string, object> center) => new()
            { { "sourcePath", source }, { "destinationPath", folder + "/" + name + ".png" }, { "center", center } };
        private static Dictionary<string, object> Request(params Dictionary<string, object>[] items) => new()
        {
            { "execution", new Dictionary<string, object> { { "mode", "immediate" } } },
            { "defaults", new Dictionary<string, object>
                {
                    { "textureType", "Sprite" }, { "spriteMode", "Single" }, { "pixelsPerUnit", 100 },
                    { "meshType", "FullRect" }, { "dedupeMode", "none" }, { "compression", "uncompressed" },
                    { "mipmapEnabled", false }, { "filterMode", "Bilinear" }, { "alphaIsTransparency", true },
                } },
            { "imports", items.ToList() },
        };
        private static Dictionary<string, object> Import(Dictionary<string, object> request) =>
            (Dictionary<string, object>)VmAutomationAssetImportCommands.Import(request);
        private static string Absolute(string path) => Path.Combine(Path.GetDirectoryName(Application.dataPath), path);
    }
}
