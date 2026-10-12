using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace VMUnityAutomation.Editor.Tests
{
    [Category(VmAutomationPackageTestCommands.DefaultPackageSmokeCategory)]
    [Category(VmAutomationPackageTestCommands.FullPackageRegressionCategory)]
    public sealed class VmSpriteGridImportTests
    {
        private string directory;
        private string source;
        private string folder;
        private string destination;
        private static readonly Color32[] Colors =
        {
            new Color32(200, 30, 70, 255), new Color32(20, 180, 50, 127),
            new Color32(30, 70, 220, 255), new Color32(250, 160, 10, 200),
        };

        [SetUp]
        public void SetUp()
        {
            string id = Guid.NewGuid().ToString("N");
            directory = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Temp", "VmSpriteGridTests-" + id);
            Directory.CreateDirectory(directory);
            source = Path.Combine(directory, "Source.png");
            folder = "Assets/VmSpriteGridTests-" + id;
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
            destination = folder + "/Icons.png";
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(folder);
            foreach (string file in Directory.GetFiles(directory)) File.Delete(file);
            Directory.Delete(directory);
        }

        [Test]
        public void IntegerPlacementPreservesFourOriginalSubjectsAndTopToBottomOrder()
        {
            Color32[] pixels = Subjects(60, 60);
            var layout = new VmSpriteGridLayout(pixels, 60, 60, GridSettings());
            for (int index = 0; index < 4; index++)
            {
                int left = index % 2 * 10 + 3, top = index / 2 * 10 + 2;
                for (int y = 0; y < 5; y++)
                    for (int x = 0; x < 3; x++)
                        Assert.That(layout.Pixels[(20 - top - y - 1) * 20 + left + x], Is.EqualTo(Colors[index]));
            }
            Assert.That(layout.ArtworkPixels, Is.EqualTo(60));
            Assert.That(layout.MaxCenterErrorPixels, Is.EqualTo(0.5));
            Assert.That(layout.Pixels.Count(pixel => pixel.a != 0), Is.EqualTo(60));
        }

        [TestCase(true)]
        [TestCase(false)]
        public void RealEmptySeamKeepsArtworkCrossingNominalDividerWhole(bool horizontal)
        {
            var pixels = new Color32[3600];
            Fill(pixels, 60, 60, horizontal ? 1 : 26, horizontal ? 26 : 1,
                horizontal ? 3 : 8, horizontal ? 8 : 3, Colors[0]);
            Fill(pixels, 60, 60, horizontal ? 35 : 42, horizontal ? 42 : 35,
                3, 5, Colors[1]);
            var values = new Dictionary<string, object>
            {
                { "columns", horizontal ? 1 : 2 }, { "rows", horizontal ? 2 : 1 }, { "cellSize", 10 },
            };
            var layout = new VmSpriteGridLayout(pixels, 60, 60, new VmSpriteGridSettings(values));
            Assert.That((horizontal ? layout.YPartitions : layout.XPartitions)[1], Is.EqualTo(34));
            Assert.That(layout.ArtworkPixels, Is.EqualTo(39));
            Assert.That(layout.Pixels.Count(pixel => pixel.Equals(Colors[0])), Is.EqualTo(24));
        }

        [Test]
        public void NonDivisibleSourceStillPublishesExactSquareGridAndPreservesSource()
        {
            Write(61, 63, Subjects(61, 63));
            byte[] original = File.ReadAllBytes(source);
            Dictionary<string, object> result = Import(Request());
            Assert.That(result["success"], Is.True);
            Assert.That(File.ReadAllBytes(source), Is.EqualTo(original));
            Sprite[] sprites = Sprites();
            Assert.That(sprites, Has.Length.EqualTo(4));
            foreach (Sprite sprite in sprites)
            {
                Assert.That(sprite.rect.width, Is.EqualTo(10));
                Assert.That(sprite.rect.height, Is.EqualTo(10));
                Assert.That(sprite.pivot, Is.EqualTo(new Vector2(5, 5)));
                Assert.That(sprite.pixelsPerUnit, Is.EqualTo(100));
            }
            var importer = (TextureImporter)AssetImporter.GetAtPath(destination);
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            Assert.That(settings.spriteMeshType, Is.EqualTo(SpriteMeshType.FullRect));
            Assert.That(First(result)["resize"], Is.Null);
            Assert.That(((Dictionary<string, object>)First(result)["spriteGrid"])["verified"], Is.True);
        }

        [Test]
        public void DryRunPerformsPixelAdmissionWithoutWritingOrClaimingVerification()
        {
            Write(60, 60, Subjects(60, 60));
            var request = Request();
            request["dryRun"] = true;
            var result = Import(request);
            Assert.That(result["success"], Is.True);
            Assert.That(((Dictionary<string, object>)First(result)["spriteGrid"])["verified"], Is.False);
            Assert.That(File.Exists(Absolute(destination)), Is.False);
            Assert.That(Directory.GetFiles(directory), Has.Length.EqualTo(1));
        }

        [Test]
        public void OverwriteKeepsExactGuidAndEverySpriteLocalFileId()
        {
            Write(60, 60, Subjects(60, 60));
            Import(Request());
            var before = Identities();
            var changed = Subjects(60, 60);
            Fill(changed, 60, 60, 4, 3, 1, 1, new Color32(70, 90, 110, 255));
            Write(60, 60, changed);
            var request = Request();
            ((Dictionary<string, object>)request["defaults"])["overwrite"] = true;
            Assert.That(Import(request)["success"], Is.True);
            Assert.That(Identities(), Is.EquivalentTo(before));
        }

        [Test]
        public void DedupeUsesCenteredOutputInsteadOfOriginalPadding()
        {
            Write(60, 60, Subjects(60, 60));
            Import(Request());
            Write(80, 80, Subjects(80, 80));
            var request = Request(folder + "/Duplicate.png");
            var defaults = (Dictionary<string, object>)request["defaults"];
            defaults["dedupeMode"] = "decodedPixels";
            defaults["dedupeScope"] = "destinationFolder";
            var result = Import(request);
            Assert.That(result["success"], Is.True);
            Assert.That(result["skippedCount"], Is.EqualTo(1));
            Assert.That(First(result)["duplicateAssetPath"], Is.EqualTo(destination));
            Assert.That(File.Exists(Absolute(folder + "/Duplicate.png")), Is.False);
        }

        [Test]
        public void ExplicitFaintBackgroundAdmissionNeverChangesSubjectPixels()
        {
            var pixels = Subjects(60, 60);
            for (int index = 0; index < pixels.Length; index++)
                if (pixels[index].a == 0) pixels[index] = new Color32(30, 20, 10, 1);
            Assert.That(() => new VmSpriteGridLayout(pixels, 60, 60, GridSettings()),
                Throws.TypeOf<VmProjectToolException>().With.Property("ErrorCode").EqualTo("sprite_grid_inseparable"));
            var values = GridValues();
            values["backgroundAlphaMax"] = 1;
            var layout = new VmSpriteGridLayout(pixels, 60, 60, new VmSpriteGridSettings(values));
            Assert.That(layout.ArtworkPixels, Is.EqualTo(60));
            Assert.That(layout.BackgroundPixelsRemoved, Is.GreaterThan(3000));
            Assert.That(layout.Pixels.Count(pixel => pixel.Equals(Colors[1])), Is.EqualTo(15));
        }

        [TestCase("resize")]
        [TestCase("spriteSlice")]
        public void ConflictingImageOwnersAreRejectedBeforePublication(string field)
        {
            Write(60, 60, Subjects(60, 60));
            var request = Request();
            ((Dictionary<string, object>)request["defaults"])[field] = new Dictionary<string, object>
                { { "width", 10 }, { "frameWidth", 10 }, { "frameHeight", 10 } };
            var result = Import(request);
            Assert.That(result["success"], Is.False);
            Assert.That(File.Exists(Absolute(destination)), Is.False);
        }

        [TestCase("textureType", "Default")]
        [TestCase("spriteMode", "Single")]
        [TestCase("meshType", "Tight")]
        public void ConflictingImporterContractIsRejectedBeforePublication(string field, string value)
        {
            Write(60, 60, Subjects(60, 60));
            var request = Request();
            ((Dictionary<string, object>)request["defaults"])[field] = value;
            var result = Import(request);
            Assert.That(result["errorCode"], Is.EqualTo("invalid_sprite_grid"));
            Assert.That(File.Exists(Absolute(destination)), Is.False);
        }

        [Test]
        public void InvalidLaterEntryLeavesEarlierExistingAtlasUntouched()
        {
            Write(60, 60, Subjects(60, 60));
            Import(Request());
            byte[] original = File.ReadAllBytes(Absolute(destination));
            var request = Request();
            ((Dictionary<string, object>)request["defaults"])["overwrite"] = true;
            ((List<Dictionary<string, object>>)request["imports"]).Add(new Dictionary<string, object>
            {
                { "sourcePath", source }, { "destinationPath", folder + "/Invalid.png" },
                { "spriteGrid", new Dictionary<string, object> { { "columns", 2 }, { "rows", 2 }, { "cellSize", 1 } } },
            });
            var result = Import(request);
            Assert.That(result["errorCode"], Is.EqualTo("sprite_grid_subject_too_large"));
            Assert.That(File.ReadAllBytes(Absolute(destination)), Is.EqualTo(original));
            Assert.That(File.Exists(Absolute(folder + "/Invalid.png")), Is.False);
        }

        private Dictionary<string, object> Request(string target = null) => new Dictionary<string, object>
        {
            { "defaults", new Dictionary<string, object>
                {
                    { "spriteGrid", GridValues() }, { "pixelsPerUnit", 100 }, { "dedupeMode", "none" },
                    { "compression", "uncompressed" }, { "mipmapEnabled", false },
                } },
            { "imports", new List<Dictionary<string, object>> { new Dictionary<string, object>
                { { "sourcePath", source }, { "destinationPath", target ?? destination } } } },
        };

        private static Dictionary<string, object> GridValues() => new Dictionary<string, object>
            { { "columns", 2 }, { "rows", 2 }, { "cellSize", 10 } };
        private static VmSpriteGridSettings GridSettings() => new VmSpriteGridSettings(GridValues());
        private static Dictionary<string, object> Import(Dictionary<string, object> request) =>
            (Dictionary<string, object>)VmAutomationAssetImportCommands.Import(request);
        private static Dictionary<string, object> First(Dictionary<string, object> result) =>
            ((List<Dictionary<string, object>>)result["imports"])[0];
        private Sprite[] Sprites() => AssetDatabase.LoadAllAssetsAtPath(destination).OfType<Sprite>().ToArray();
        private Dictionary<string, string> Identities() => Sprites().ToDictionary(sprite => sprite.name, sprite =>
        {
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sprite, out string guid, out long id);
            return guid + ":" + id.ToString(System.Globalization.CultureInfo.InvariantCulture);
        });
        private static string Absolute(string path) => Path.Combine(Path.GetDirectoryName(Application.dataPath), path);

        private void Write(int width, int height, Color32[] pixels) =>
            File.WriteAllBytes(source, VmPngImage.Encode(pixels, width, height, source));
        private static Color32[] Subjects(int width, int height)
        {
            var pixels = new Color32[width * height];
            for (int index = 0; index < 4; index++)
                Fill(pixels, width, height, index % 2 * (width / 2) + 1,
                    index / 2 * (height / 2) + 2, 3, 5, Colors[index]);
            return pixels;
        }
        private static void Fill(Color32[] pixels, int width, int height, int left, int top,
            int subjectWidth, int subjectHeight, Color32 color)
        {
            for (int y = top; y < top + subjectHeight; y++)
                for (int x = left; x < left + subjectWidth; x++)
                    pixels[(height - y - 1) * width + x] = color;
        }
    }
}
