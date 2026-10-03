using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace VMUnityAutomation.Editor.Tests
{
    [Category(VmAutomationPackageTestCommands.DefaultPackageSmokeCategory)]
    [Category(VmAutomationPackageTestCommands.FullPackageRegressionCategory)]
    public sealed class VmSerializedIntegerVectorTests
    {
        [TestCase("resolution")]
        [TestCase("position")]
        public void StructuredCoordinatesPersistAndReadBackAfterImport(string propertyPath)
        {
            string path = AssetDatabase.GenerateUniqueAssetPath("Assets/Integer Vector Test.asset");
            var asset = ScriptableObject.CreateInstance<VmSerializedIntegerVectorTestAsset>();
            AssetDatabase.CreateAsset(asset, path);
            var expected = new Dictionary<string, object> { { "x", 1920L }, { "y", 1080L } };
            if (propertyPath == "position")
                expected.Add("z", -17L);
            try
            {
                var write = (Dictionary<string, object>)VmAutomationSerializedObjectCommands.Set(
                    new Dictionary<string, object>
                    {
                        { "assetPath", path }, { "propertyPath", propertyPath }, { "value", expected }
                    });
                Assert.That(write["success"], Is.True);
                Resources.UnloadAsset(asset);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                var reopened = AssetDatabase.LoadAssetAtPath<VmSerializedIntegerVectorTestAsset>(path);
                Assert.That(reopened.resolution, Is.EqualTo(propertyPath == "resolution"
                    ? new Vector2Int(1920, 1080) : new Vector2Int(1200, 800)));
                Assert.That(reopened.position, Is.EqualTo(propertyPath == "position"
                    ? new Vector3Int(1920, 1080, -17) : new Vector3Int(1, 2, 3)));

                var read = (Dictionary<string, object>)VmAutomationSerializedObjectCommands.Get(
                    new Dictionary<string, object> { { "assetPath", path }, { "propertyPath", propertyPath } });
                var property = (Dictionary<string, object>)read["property"];
                var actual = (Dictionary<string, object>)property["value"];
                Assert.That(actual.Count, Is.EqualTo(expected.Count));
                Assert.That(actual["x"], Is.EqualTo(1920));
                Assert.That(actual["y"], Is.EqualTo(1080));
                if (propertyPath == "position")
                    Assert.That(actual["z"], Is.EqualTo(-17));
            }
            finally
            {
                AssetDatabase.DeleteAsset(path);
            }
        }

        [TestCase("resolution", "missing")]
        [TestCase("resolution", "fraction")]
        [TestCase("resolution", "extra")]
        [TestCase("position", "fraction")]
        public void InvalidCoordinatesLeaveTheSerializedVectorUnchanged(string propertyPath, string invalidKind)
        {
            var asset = ScriptableObject.CreateInstance<VmSerializedIntegerVectorTestAsset>();
            try
            {
                var value = new Dictionary<string, object> { { "x", 1920L }, { "y", 1080L } };
                if (propertyPath == "position")
                    value.Add("z", 0.5);
                else if (invalidKind == "missing")
                    value.Remove("y");
                else if (invalidKind == "fraction")
                    value["x"] = 1920.5;
                else
                    value.Add("z", 10L);

                using (var serialized = new SerializedObject(asset))
                {
                    var property = serialized.FindProperty(propertyPath);
                    Assert.Throws<ArgumentException>(() => VmAutomationComponentCommands.SetSerializedValue(property, value));
                    serialized.ApplyModifiedProperties();
                }
                Assert.That(asset.resolution, Is.EqualTo(new Vector2Int(1200, 800)));
                Assert.That(asset.position, Is.EqualTo(new Vector3Int(1, 2, 3)));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(asset);
            }
        }
    }
}
