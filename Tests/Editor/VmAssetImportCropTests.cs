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
    public sealed class VmAssetImportCropTests
    {
        private string directory;
        private string source;
        private string folder;
        private static readonly Color32[] Pixels = Enumerable.Range(0, 12)
            .Select(index => new Color32((byte)(index * 17), (byte)(index * 11),
                (byte)(255 - index * 13), (byte)(index * 23))).ToArray();

        [SetUp]
        public void SetUp()
        {
            string id = Guid.NewGuid().ToString("N");
            directory = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Temp", "VmCropTests-" + id);
            Directory.CreateDirectory(directory);
            source = Path.Combine(directory, "Source.png");
            File.WriteAllBytes(source, VmPngImage.Encode(Pixels, 4, 3, source));
            folder = "Assets/VmCropTests-" + id;
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(folder);
            File.Delete(source);
            Directory.Delete(directory);
        }

        [Test]
        public void BottomLeftCropKeepsAsymmetricRgbaAndPaddingExactly()
        {
            var image = Prepare(Crop(0, 0, 2, 3));
            Texture2D decoded = VmPngImage.Decode(image.Bytes, source, 2, 3);
            try
            {
                Assert.That(decoded.GetPixels32(), Is.EqualTo(new[]
                    { Pixels[0], Pixels[1], Pixels[4], Pixels[5], Pixels[8], Pixels[9] }));
                Assert.That(image.Crop, Is.EqualTo(new RectInt(0, 0, 2, 3)));
                Assert.That(image.SourceHash, Is.EqualTo(VmPngImage.Hash(File.ReadAllBytes(source))));
                Assert.That(image.Filter, Is.Null);
            }
            finally { UnityEngine.Object.DestroyImmediate(decoded); }
        }

        [Test]
        public void NativeSheetExportsTwoIndependentSingleSpritesWithoutChangingSource()
        {
            byte[] original = File.ReadAllBytes(source);
            var request = Request(Item("Left", Crop(0, 0, 2, 3)), Item("Right", Crop(2, 0, 2, 3)));
            var result = Import(request);
            Assert.That(result["success"], Is.True);
            Assert.That(File.ReadAllBytes(source), Is.EqualTo(original));
            var outputs = (List<Dictionary<string, object>>)result["imports"];
            Assert.That(outputs.Count, Is.EqualTo(2));
            foreach (var output in outputs)
            {
                string path = (string)output["destinationPath"];
                Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                Assert.That(sprite.rect, Is.EqualTo(new Rect(0, 0, 2, 3)));
                Assert.That(sprite.name, Is.EqualTo(Path.GetFileNameWithoutExtension(path)));
                Assert.That(sprite.pixelsPerUnit, Is.EqualTo(100));
                Assert.That(sprite.pivot, Is.EqualTo(new Vector2(1, 1.5f)));
                Assert.That(AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().Count(), Is.EqualTo(1));
                var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                Assert.That(importer.spriteImportMode, Is.EqualTo(SpriteImportMode.Single));
                Assert.That(settings.spriteMeshType, Is.EqualTo(SpriteMeshType.FullRect));
                var receipt = (Dictionary<string, object>)output["crop"];
                Assert.That(receipt["verified"], Is.True);
                Assert.That(receipt["outputSha256"], Is.EqualTo(VmPngImage.Hash(File.ReadAllBytes(Absolute(path)))));
                Assert.That(output["resize"], Is.Null);
            }
            Assert.That(outputs[0]["currentGuid"], Is.Not.EqualTo(outputs[1]["currentGuid"]));
        }

        [Test]
        public void InvalidLaterCropRejectsWholeBatchBeforePublishingFirstOutput()
        {
            var result = Import(Request(Item("Valid", Crop(0, 0, 2, 3)), Item("Invalid", Crop(3, 0, 2, 3))));
            Assert.That(result["success"], Is.False);
            Assert.That(result["errorCode"], Is.EqualTo("invalid_image_crop"));
            Assert.That(File.Exists(Absolute(folder + "/Valid.png")), Is.False);
            Assert.That(File.Exists(Absolute(folder + "/Invalid.png")), Is.False);
        }

        [TestCase("resize")]
        [TestCase("spriteGrid")]
        public void ConflictingImageOperationsHaveNoPublication(string operation)
        {
            var settings = new Dictionary<string, object>
                { { "crop", Crop(0, 0, 2, 3) }, { operation, new Dictionary<string, object>() } };
            long pixels = 64L * 1024 * 1024, bytes = 256L * 1024 * 1024;
            int cells = 4096;
            var exception = Assert.Throws<VmProjectToolException>(() => VmAssetImportImagePreparation.Prepare(
                settings, source, ref pixels, ref bytes, ref cells, out _));
            Assert.That(exception.ErrorCode, Is.EqualTo(operation == "spriteGrid" ? "invalid_sprite_grid" : "invalid_image_crop"));
        }

        [TestCase(-1)]
        [TestCase(0.5)]
        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        public void InvalidCoordinatesRejectWithoutDecoding(double x)
        {
            var crop = Crop(0, 0, 2, 3);
            crop["x"] = x;
            Assert.That(Assert.Throws<VmProjectToolException>(() => Prepare(crop)).ErrorCode,
                Is.EqualTo("invalid_image_crop"));
        }

        [Test]
        public void WorkBudgetRejectsBeforeDecode()
        {
            Assert.That(Assert.Throws<VmProjectToolException>(() => VmPngCropPreparation.Prepare(
                new Dictionary<string, object> { { "crop", Crop(0, 0, 2, 3) } }, source, 17, 1024)).ErrorCode,
                Is.EqualTo("image_size_limit"));
        }

        [Test]
        public void DryRunLeavesOnlyOriginalSource()
        {
            var request = Request(Item("Preview", Crop(0, 0, 2, 3)));
            request["dryRun"] = true;
            var result = Import(request);
            Assert.That(result["success"], Is.True);
            Assert.That(File.Exists(Absolute(folder + "/Preview.png")), Is.False);
            Assert.That(Directory.GetFiles(directory), Is.EqualTo(new[] { source }));
        }

        [Test]
        public void CatalogPublishesCropSchemaAndActualImageRejectionCodes()
        {
            Assert.That(VmAutomationCatalog.TryGetTool("asset/import", true, out var tool), Is.True);
            Assert.That((System.Collections.IEnumerable)tool["errorCodes"],
                Does.Contain("invalid_image_crop").And.Contain("image_size_limit").And.Contain("invalid_sprite_grid"));
            var schema = (Dictionary<string, object>)tool["inputSchema"];
            var properties = (Dictionary<string, object>)schema["properties"];
            var defaults = (Dictionary<string, object>)properties["defaults"];
            Assert.That((Dictionary<string, object>)defaults["properties"], Contains.Key("crop"));
        }

        [Test]
        public void NativeSheetRectHasOnlyItsFourDeclaredCoordinates()
        {
            Assert.That(Import(Request(Item("Rect", Crop(0, 0, 2, 3))))["success"], Is.True);
            var sheet = (Dictionary<string, object>)VmAutomationSpriteSheetCommands.GetSheetInfo(
                new Dictionary<string, object> { { "texturePath", folder + "/Rect.png" } });
            var sprites = (List<Dictionary<string, object>>)sheet["sprites"];
            var rect = (Dictionary<string, object>)sprites[0]["rect"];
            Assert.That(rect.Keys, Is.EquivalentTo(new[] { "x", "y", "width", "height" }));
            Assert.That(rect["width"], Is.EqualTo(2));
            Assert.That(rect["height"], Is.EqualTo(3));
        }

        private VmPreparedPng Prepare(Dictionary<string, object> crop) => VmPngCropPreparation.Prepare(
            new Dictionary<string, object> { { "crop", crop } }, source, 64L * 1024 * 1024, 256L * 1024 * 1024);
        private static Dictionary<string, object> Crop(int x, int y, int width, int height) =>
            new() { { "x", x }, { "y", y }, { "width", width }, { "height", height } };
        private Dictionary<string, object> Item(string name, Dictionary<string, object> crop) =>
            new() { { "sourcePath", source }, { "destinationPath", folder + "/" + name + ".png" }, { "crop", crop } };
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
