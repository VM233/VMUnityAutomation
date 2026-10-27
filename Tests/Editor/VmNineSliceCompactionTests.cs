using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace VMUnityAutomation.Editor.Tests
{
    [Category(VmAutomationPackageTestCommands.DefaultPackageSmokeCategory)]
    [Category(VmAutomationPackageTestCommands.FullPackageRegressionCategory)]
    public sealed class VmNineSliceCompactionTests
    {
        private string folder;

        [SetUp]
        public void SetUp()
        {
            folder = "Assets/VmNineSliceCompactionTests-" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
        }

        [TearDown]
        public void TearDown() => AssetDatabase.DeleteAsset(folder);

        [TestCase(19, 17, 3, 3)]
        [TestCase(3, 17, 3, 3)]
        [TestCase(19, 3, 3, 3)]
        public void ReviewIsReadOnlyAndApplyPreservesNativeSpriteIdentity(int width, int height, int expectedWidth, int expectedHeight)
        {
            string path = Publish(width, height, false);
            string guid = AssetDatabase.AssetPathToGUID(path);
            Sprite before = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(before, out string _, out long fileId);
            byte[] source = File.ReadAllBytes(Absolute(path));
            byte[] meta = File.ReadAllBytes(Absolute(path) + ".meta");
            VmNineSliceCompactPlan plan = Review(path);
            Assert.That(plan.CompactWidth, Is.EqualTo(expectedWidth));
            Assert.That(plan.CompactHeight, Is.EqualTo(expectedHeight));
            var expectedBorder = new Vector4(width > 3 ? 1 : 0, height > 3 ? 1 : 0,
                width > 3 ? 1 : 0, height > 3 ? 1 : 0);
            Assert.That(plan.Border, Is.EqualTo(new[] { (int)expectedBorder.x, (int)expectedBorder.y,
                (int)expectedBorder.z, (int)expectedBorder.w }));
            Assert.That(plan.ReconstructionVerified, Is.True);
            Assert.That(File.ReadAllBytes(Absolute(path)), Is.EqualTo(source));
            Assert.That(File.ReadAllBytes(Absolute(path) + ".meta"), Is.EqualTo(meta));
            var result = new VmNineSliceCompactTool().Execute(new VmNineSliceCompactRequest
                { TexturePath = path, ExpectedSha256 = plan.SourceSha256 });
            Assert.That(result.Applied, Is.True);
            Sprite after = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(after, out string savedGuid, out long savedId);
            Assert.That(savedGuid, Is.EqualTo(guid));
            Assert.That(savedId, Is.EqualTo(fileId));
            Assert.That(after.rect.size, Is.EqualTo(new Vector2(expectedWidth, expectedHeight)));
            Assert.That(after.border, Is.EqualTo(expectedBorder));
            Assert.That(((TextureImporter)AssetImporter.GetAtPath(path)).spritePixelsPerUnit, Is.EqualTo(32));
            Assert.That(Review(path).Eligible, Is.False);
        }

        [TestCase(true)]
        [TestCase(false)]
        public void UniformAxisCollapsesWithoutInventingBorders(bool entireImage)
        {
            var pixels = Enumerable.Range(0, 13 * 11).Select(i => new Color32(
                (byte)(entireImage ? 80 : i % 13), 20, 30, 255)).ToArray();
            string path = Publish(13, 11, false, pixels);
            var plan = Review(path);
            Assert.That(plan.CompactWidth, Is.EqualTo(entireImage ? 1 : 13));
            Assert.That(plan.CompactHeight, Is.EqualTo(1));
            Assert.That(plan.ReconstructionVerified, Is.True);
        }

        [Test]
        public void PublicMutationDeclaresValidCompleteTransactionMetadata()
        {
            var attribute = (VmProjectToolAttribute)Attribute.GetCustomAttribute(
                typeof(VmNineSliceCompactTool), typeof(VmProjectToolAttribute));
            Assert.That(VmProjectToolTransactionMetadata.Validate(
                VmProjectToolTransactionMetadata.Build(attribute), false, attribute.ToolName), Is.Null);
        }

        [Test]
        public void StaleHashRejectsBeforeAnyMutation()
        {
            string path = Publish(19, 17, false);
            byte[] source = File.ReadAllBytes(Absolute(path));
            var error = Assert.Throws<VmProjectToolException>(() => new VmNineSliceCompactTool().Execute(
                new VmNineSliceCompactRequest { TexturePath = path, ExpectedSha256 = new string('0', 64) }));
            Assert.That(error.Message, Does.Contain("changed"));
            Assert.That(File.ReadAllBytes(Absolute(path)), Is.EqualTo(source));
        }

        [Test]
        public void FullSourceMultipleSpritePreservesIds()
        {
            string path = Publish(19, 17, true);
            var plan = Review(path);
            Assert.That(plan.SpriteId, Is.Not.Empty);
            var result = new VmNineSliceCompactTool().Execute(new VmNineSliceCompactRequest
                { TexturePath = path, ExpectedSha256 = plan.SourceSha256 });
            Assert.That(result.SpriteId, Is.EqualTo(plan.SpriteId));
            Assert.That(result.LocalFileId, Is.EqualTo(plan.LocalFileId));
        }

        [Test]
        public void AuthoredStretchBandCannotBeReplacedByAnotherRepeatedBand()
        {
            var edge = new Color32(0, 0, 0, 255);
            var fill = new Color32(80, 20, 30, 255);
            var accent = new Color32(10, 90, 30, 255);
            string path = Publish(9, 1, false,
                new[] { edge, fill, fill, fill, accent, fill, fill, fill, edge });
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.spriteBorder = new Vector4(1, 0, 1, 0);
            importer.SaveAndReimport();
            Assert.That(Review(path).Eligible, Is.False);
            importer.spriteBorder = new Vector4(1, 0, 5, 0);
            importer.SaveAndReimport();
            var plan = Review(path);
            Assert.That(plan.CompactWidth, Is.EqualTo(7));
            Assert.That(plan.Border, Is.EqualTo(new[] { 1, 0, 5, 0 }));
            Assert.That(plan.ReconstructionVerified, Is.True);
        }

        [Test]
        public void TwoChangedInteriorBandsRemainDistinctAndReconstructExactly()
        {
            var edge = new Color32(0, 0, 0, 255);
            var fill = new Color32(80, 20, 30, 255);
            var accent = new Color32(10, 90, 30, 255);
            string path = Publish(9, 1, false,
                new[] { edge, fill, fill, fill, accent, fill, fill, fill, edge });
            var plan = Review(path);
            Assert.That(plan.CompactWidth, Is.EqualTo(7));
            Assert.That(plan.Border, Is.EqualTo(new[] { 1, 0, 5, 0 }));
            Assert.That(plan.ReconstructionVerified, Is.True);
        }

        private VmNineSliceCompactPlan Review(string path) => new VmNineSliceReviewTool().Execute(
            new VmNineSliceReviewRequest { TexturePaths = new[] { path } }).Plans.Single();

        private string Publish(int width, int height, bool multiple, Color32[] pixels = null)
        {
            string path = folder + "/Panel.png";
            pixels ??= Enumerable.Range(0, width * height).Select(i =>
                i % width == 0 || i % width == width - 1 || i / width == 0 || i / width == height - 1
                    ? new Color32(10, 20, 30, 255) : new Color32(80, 20, 30, 255)).ToArray();
            File.WriteAllBytes(Absolute(path), VmPngImage.Encode(pixels, width, height, path));
            AssetDatabase.ImportAsset(path);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.spritePixelsPerUnit = 32;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.SaveAndReimport();
            if (multiple)
            {
                importer.spriteImportMode = SpriteImportMode.Multiple;
                var provider = VmAutomationSpriteSheetCommands.GetSpriteDataProvider(importer);
                provider.SetSpriteRects(new[] { new UnityEditor.U2D.Sprites.SpriteRect
                {
                    name = "Panel", spriteID = GUID.Generate(), rect = new Rect(0, 0, width, height),
                    alignment = SpriteAlignment.Center, pivot = new Vector2(0.5f, 0.5f)
                } });
                provider.Apply();
                importer.SaveAndReimport();
            }
            return path;
        }

        private static string Absolute(string path) => Path.Combine(Path.GetDirectoryName(Application.dataPath), path);
    }
}
