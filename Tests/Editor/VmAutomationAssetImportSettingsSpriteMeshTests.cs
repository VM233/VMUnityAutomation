using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace VMUnityAutomation.Editor.Tests
{
    [Category(VmAutomationPackageTestCommands.DefaultPackageSmokeCategory)]
    [Category(VmAutomationPackageTestCommands.FullPackageRegressionCategory)]
    public sealed class VmAutomationAssetImportSettingsSpriteMeshTests
    {
        private string folder;
        private string assetPath;

        [SetUp]
        public void SetUp()
        {
            folder = "Assets/VmImportSettingsSpriteMeshTests-" +
                     Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
            assetPath = folder + "/Sprite.png";
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                File.WriteAllBytes(Absolute(assetPath), texture.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(assetPath);
            importer.textureType = TextureImporterType.Sprite;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.Tight;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(folder);
        }

        [Test]
        public void SemanticSettingsReadAndWriteSpriteMeshType()
        {
            var request = new Dictionary<string, object>
            {
                { "assetPath", assetPath },
                { "settings", new Dictionary<string, object>
                    {
                        { "spriteMeshType", "FullRect" }
                    }
                }
            };
            var result = (Dictionary<string, object>)
                VmAutomationAssetImportSettingsCommands.Set(request);

            Assert.That(result["success"], Is.True);
            var after = (Dictionary<string, object>)result["after"];
            Assert.That(after["spriteMeshType"], Is.EqualTo("FullRect"));
            var importer = (TextureImporter)AssetImporter.GetAtPath(assetPath);
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            Assert.That(settings.spriteMeshType, Is.EqualTo(SpriteMeshType.FullRect));
        }

        [Test]
        public void SemanticSettingsSchemaExposesSpriteMeshType()
        {
            Dictionary<string, object> schema =
                VmAutomationToolInputSchemaCatalog.Get("asset/import-settings/set");
            var properties = (Dictionary<string, object>)schema["properties"];
            var settings = (Dictionary<string, object>)properties["settings"];
            var settingsProperties =
                (Dictionary<string, object>)settings["properties"];

            Assert.That(settingsProperties.ContainsKey("spriteMeshType"), Is.True);
        }

        private static string Absolute(string path)
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            return Path.Combine(projectRoot, path.Replace('/', Path.DirectorySeparatorChar));
        }
    }
}
