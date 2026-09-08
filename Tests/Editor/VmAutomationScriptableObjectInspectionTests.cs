using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace VMUnityAutomation.Editor.Tests
{
    public sealed class VmAutomationScriptableObjectInspectionTests
    {
        [Test]
        public void InspectionPreservesTypedValuesCollectionsAndReferenceIdentity()
        {
            string path = AssetDatabase.GenerateUniqueAssetPath("Assets/Inspection Test.asset");
            var asset = ScriptableObject.CreateInstance<VmAutomationInspectionTestAsset>();
            asset.label = "Preset";
            asset.enabledValue = true;
            asset.point = new Vector3(1, 2, 3);
            asset.reference = asset;
            asset.entries.Add(new VmAutomationInspectionTestAsset.Entry { label = "HUD", value = 100 });
            AssetDatabase.CreateAsset(asset, path);
            try
            {
                var result = (Dictionary<string, object>)VmAutomationScriptableObjectCommands.GetScriptableObjectInfo(
                    new Dictionary<string, object> { { "path", path } });
                var fields = ((List<Dictionary<string, object>>)result["properties"])
                    .ToDictionary(field => (string)field["name"], field => field["value"]);

                Assert.That(fields["label"], Is.EqualTo("Preset"));
                Assert.That(fields["enabledValue"], Is.True);
                Assert.That(((Dictionary<string, object>)fields["point"])["z"], Is.EqualTo(3f));
                Assert.That(((Dictionary<string, object>)fields["reference"])["assetPath"], Is.EqualTo(path));

                var array = (Dictionary<string, object>)fields["entries"];
                Assert.That(array["arraySize"], Is.EqualTo(1));
                Assert.That(array["truncated"], Is.False);
                var entry = (Dictionary<string, object>)((List<object>)array["items"])[0];
                Assert.That(entry["label"], Is.EqualTo("HUD"));
                Assert.That(entry["value"], Is.EqualTo(100));

                var changed = (Dictionary<string, object>)VmAutomationScriptableObjectCommands.SetScriptableObjectField(
                    new Dictionary<string, object> { { "path", path }, { "field", "label" }, { "value", "Changed" } });
                Assert.That(changed["value"], Is.EqualTo("Changed"));
            }
            finally
            {
                AssetDatabase.DeleteAsset(path);
            }
        }

        [Test]
        public void SerializedObjectSetResolvesCompatibleSpriteSubAssetFromTexturePath()
        {
            string targetPath = AssetDatabase.GenerateUniqueAssetPath("Assets/Serialized Object Sprite Target.asset");
            string texturePath = AssetDatabase.GenerateUniqueAssetPath("Assets/Serialized Object Sprite.png");
            var target = ScriptableObject.CreateInstance<VmAutomationInspectionTestAsset>();
            AssetDatabase.CreateAsset(target, targetPath);
            CreateSingleSpriteTexture(texturePath);

            try
            {
                var result = (Dictionary<string, object>)VmAutomationSerializedObjectCommands.Set(
                    new Dictionary<string, object>
                    {
                        { "assetPath", targetPath },
                        { "propertyPath", "spriteReference" },
                        { "value", new Dictionary<string, object> { { "assetPath", texturePath } } },
                    });

                Assert.That(result.ContainsKey("error"), Is.False,
                    result.TryGetValue("error", out var error) ? error?.ToString() : "");
                target = AssetDatabase.LoadAssetAtPath<VmAutomationInspectionTestAsset>(targetPath);
                Assert.That(target.spriteReference, Is.Not.Null);
                Assert.That(target.spriteReference, Is.TypeOf<Sprite>());
                Assert.That(AssetDatabase.GetAssetPath(target.spriteReference), Is.EqualTo(texturePath));

                var afterValue = (Dictionary<string, object>)result["afterValue"];
                Assert.That(afterValue["type"], Is.EqualTo(nameof(Sprite)));
                Assert.That(afterValue["assetPath"], Is.EqualTo(texturePath));
                Assert.That(afterValue["localFileId"], Is.Not.Empty);
            }
            finally
            {
                AssetDatabase.DeleteAsset(targetPath);
                AssetDatabase.DeleteAsset(texturePath);
            }
        }

        private static void CreateSingleSpriteTexture(string assetPath)
        {
            var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();

            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            Assert.That(projectRoot, Is.Not.Null.And.Not.Empty);
            string fullPath = Path.Combine(projectRoot,
                assetPath.Replace('/', Path.DirectorySeparatorChar));
            File.WriteAllBytes(fullPath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(assetPath);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.SaveAndReimport();
        }

        [TestCase("scriptableobject/info", "properties.items.properties.value")]
        [TestCase("scriptableobject/set-field", "value")]
        [TestCase("serialized-object/get", "property.properties.value")]
        [TestCase("serialized-object/get", "properties.items.properties.value")]
        [TestCase("serialized-object/set", "beforeValue")]
        [TestCase("serialized-object/set", "afterValue")]
        [TestCase("component/get-properties", "properties.items.properties.value")]
        [TestCase("prefab-asset/get-properties", "properties.items.properties.value")]
        public void ReadbackSchemaAcceptsStructuredJsonValues(string route, string fieldPath)
        {
            Assert.That(VmAutomationGeneratedRouteContracts.TryGetOutput(route, out var schema), Is.True);
            object field = schema["properties"];
            foreach (string segment in fieldPath.Split('.'))
            {
                field = ((Dictionary<string, object>)field)[segment];
            }

            var value = (Dictionary<string, object>)field;
            Assert.That(value["$ref"], Is.EqualTo("#/$defs/unityJsonValue"));
        }
    }
}
