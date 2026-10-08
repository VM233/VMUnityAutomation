using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace VMUnityAutomation.Editor.Tests
{
    [Category(VmAutomationPackageTestCommands.DefaultPackageSmokeCategory)]
    [Category(VmAutomationPackageTestCommands.FullPackageRegressionCategory)]
    public sealed class VmSpriteRectUpdateTests
    {
        private string folder;
        private string path;

        [SetUp]
        public void SetUp()
        {
            folder = "Assets/VmSpriteRectUpdateTests-" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
            path = folder + "/Sheet.png";
            var texture = new Texture2D(64, 32, TextureFormat.RGBA32, false);
            try
            {
                File.WriteAllBytes(Absolute(path), texture.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = 100;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            var provider = VmAutomationSpriteSheetCommands.GetSpriteDataProvider(importer);
            provider.SetSpriteRects(new[]
            {
                new SpriteRect { name = "Left", rect = new Rect(0, 0, 32, 32), pivot = new Vector2(.5f,.5f), spriteID = GUID.Generate() },
                new SpriteRect { name = "Right", rect = new Rect(32, 0, 32, 32), pivot = new Vector2(.5f,.5f), spriteID = GUID.Generate() }
            });
            var names = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
            names.SetNameFileIdPairs(provider.GetSpriteRects().Select(rect => new SpriteNameFileIdPair(rect.name, rect.spriteID)));
            provider.Apply();
            importer.SaveAndReimport();
        }

        [TearDown]
        public void TearDown() => AssetDatabase.DeleteAsset(folder);

        [Test]
        public void IrregularUpdatePersistsWithoutReplacingUntouchedSliceOrIdentities()
        {
            byte[] pixels = File.ReadAllBytes(Absolute(path));
            string guid = AssetDatabase.AssetPathToGUID(path);
            var before = Ids();
            VmSpriteRectUpdateResult result = new VmSpriteRectUpdateTool().Execute(Request(Update("Left", 2, 3, 24, 26)));
            Assert.That(result.UpdatedCount, Is.EqualTo(1));
            Assert.That(result.TextureGuid, Is.EqualTo(guid));
            Assert.That(result.PixelsPerUnit, Is.EqualTo(100));
            Assert.That(result.SpriteMeshType, Is.Zero);
            Assert.That(File.ReadAllBytes(Absolute(path)), Is.EqualTo(pixels));
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
            Assert.That(Ids(), Is.EquivalentTo(before));
            var saved = VmAutomationSpriteSheetCommands.GetSpriteRects((TextureImporter)AssetImporter.GetAtPath(path));
            Assert.That(saved.Single(rect => rect.name == "Left").rect, Is.EqualTo(new Rect(2, 3, 24, 26)));
            Assert.That(saved.Single(rect => rect.name == "Left").pivot, Is.EqualTo(new Vector2(.5f,.5f)));
            Assert.That(saved.Single(rect => rect.name == "Right").rect, Is.EqualTo(new Rect(32, 0, 32, 32)));
        }

        [TestCase("outside")]
        [TestCase("duplicate")]
        [TestCase("unknown")]
        [TestCase("nonfinite")]
        [TestCase("pivot")]
        public void InvalidLastEntryRejectsBeforeWritingAnyMetadata(string kind)
        {
            var invalid = Update("Right", 32, 0, 32, 32);
            switch (kind)
            {
                case "outside": invalid.Width = 33; break;
                case "duplicate": invalid.Name = "Left"; break;
                case "unknown": invalid.Name = "Missing"; break;
                case "nonfinite": invalid.X = float.NaN; break;
                case "pivot": invalid.PivotX = 1.1f; break;
            }
            byte[] metadata = File.ReadAllBytes(Absolute(path) + ".meta");
            var error = Assert.Throws<VmProjectToolException>(() =>
                new VmSpriteRectUpdateTool().Execute(Request(Update("Left", 2, 3, 24, 26), invalid)));
            Assert.That(error.Message, Does.Contain("domain").Or.Contain("name"));
            Assert.That(File.ReadAllBytes(Absolute(path) + ".meta"), Is.EqualTo(metadata));
        }

        private VmSpriteRectUpdateRequest Request(params VmSpriteRectUpdate[] rects) =>
            new() { TexturePath = path, Rects = rects };

        private static VmSpriteRectUpdate Update(string name, float x, float y, float width, float height) =>
            new() { Name = name, X = x, Y = y, Width = width, Height = height, PivotX = .5f, PivotY = .5f };

        private Dictionary<string, long> Ids() => VmAutomationSpriteSheetCommands.LoadSprites(path).ToDictionary(
            sprite => sprite.name, sprite => { AssetDatabase.TryGetGUIDAndLocalFileIdentifier(sprite, out string _, out long id); return id; });

        private static string Absolute(string assetPath) => Path.Combine(Path.GetDirectoryName(Application.dataPath), assetPath);
    }
}
