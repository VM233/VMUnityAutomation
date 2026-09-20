using System;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace VMUnityAutomation.Editor.Tests
{
    [Category(VmAutomationPackageTestCommands.DefaultPackageSmokeCategory)]
    [Category(VmAutomationPackageTestCommands.FullPackageRegressionCategory)]
    public sealed class VmSpriteMeshReviewTests
    {
        private string folder;
        private string fullRectPath;
        private string tightPath;

        [SetUp]
        public void SetUp()
        {
            folder = "Assets/VmSpriteMeshReviewTests-" +
                     Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
            fullRectPath = folder + "/FullRect.png";
            tightPath = folder + "/Tight.png";
            CreateSprite(fullRectPath, SpriteMeshType.FullRect);
            CreateSprite(tightPath, SpriteMeshType.Tight);
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(folder);
        }

        [Test]
        public void ReportsTightAndPassesAfterImporterCorrection()
        {
            var tool = new VmSpriteMeshReviewTool();
            VmSpriteMeshReviewResult failed = tool.Execute(
                new VmSpriteMeshReviewRequest
                {
                    AssetRoots = new[] { folder },
                    MaxIssues = 10
                });

            Assert.That(failed.Passed, Is.False);
            Assert.That(failed.SpriteCount, Is.EqualTo(2));
            Assert.That(failed.FullRectCount, Is.EqualTo(1));
            Assert.That(failed.TightCount, Is.EqualTo(1));
            Assert.That(failed.TotalIssues, Is.EqualTo(1));
            Assert.That(failed.Issues, Has.Length.EqualTo(1));
            Assert.That(failed.Issues[0].AssetPath, Is.EqualTo(tightPath));
            Assert.That(failed.Issues[0].ActualMeshType, Is.EqualTo("Tight"));

            SetMeshType(tightPath, SpriteMeshType.FullRect);
            VmSpriteMeshReviewResult passed = tool.Execute(
                new VmSpriteMeshReviewRequest
                {
                    AssetRoots = new[] { folder },
                    MaxIssues = 10
                });
            Assert.That(passed.Passed, Is.True);
            Assert.That(passed.SpriteCount, Is.EqualTo(2));
            Assert.That(passed.FullRectCount, Is.EqualTo(2));
            Assert.That(passed.TotalIssues, Is.Zero);
        }

        private static void CreateSprite(string path, SpriteMeshType meshType)
        {
            var texture = new Texture2D(4, 4, TextureFormat.RGBA32, false);
            try
            {
                var pixels = new Color32[16];
                for (int index = 0; index < pixels.Length; index++)
                    pixels[index] = new Color32(255, 255, 255, 255);
                texture.SetPixels32(pixels);
                texture.Apply();
                File.WriteAllBytes(Absolute(path), texture.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            SetMeshType(path, meshType);
        }

        private static void SetMeshType(string path, SpriteMeshType meshType)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = meshType;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }

        private static string Absolute(string path)
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            return Path.Combine(projectRoot, path.Replace('/', Path.DirectorySeparatorChar));
        }
    }
}
