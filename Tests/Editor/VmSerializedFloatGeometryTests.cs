using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace VMUnityAutomation.Editor.Tests
{
    public sealed class VmSerializedFloatGeometryTests
    {
        [TestCase("tint", 4)]
        [TestCase("point", 2)]
        [TestCase("position", 3)]
        [TestCase("vector", 4)]
        [TestCase("rotation", 4)]
        [TestCase("rectangle", 4)]
        public void ArrayWritesPersistAfterNativeSaveAndImport(string field, int count)
        {
            string path = AssetDatabase.GenerateUniqueAssetPath("Assets/Float Geometry.asset");
            var asset = ScriptableObject.CreateInstance<VmSerializedFloatGeometryTestAsset>();
            AssetDatabase.CreateAsset(asset, path);
            object[] values = new object[count];
            for (int index = 0; index < count; index++) values[index] = (index + 1) * .125;
            try
            {
                var written = (Dictionary<string, object>)VmAutomationSerializedObjectCommands.Set(new Dictionary<string, object>
                { ["assetPath"] = path, ["propertyPath"] = field, ["value"] = values });
                Assert.That(written["success"], Is.True);
                Resources.UnloadAsset(asset);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                asset = AssetDatabase.LoadAssetAtPath<VmSerializedFloatGeometryTestAsset>(path);
                var serialized = new SerializedObject(asset);
                var property = serialized.FindProperty(field);
                var expected = new float[] { .125f, .25f, .375f, .5f };
                object actual = field switch
                {
                    "tint" => property.colorValue,
                    "point" => property.vector2Value,
                    "position" => property.vector3Value,
                    "vector" => property.vector4Value,
                    "rotation" => property.quaternionValue,
                    _ => property.rectValue
                };
                object desired = field switch
                {
                    "tint" => new Color(expected[0], expected[1], expected[2], expected[3]),
                    "point" => new Vector2(expected[0], expected[1]),
                    "position" => new Vector3(expected[0], expected[1], expected[2]),
                    "vector" => new Vector4(expected[0], expected[1], expected[2], expected[3]),
                    "rotation" => new Quaternion(expected[0], expected[1], expected[2], expected[3]),
                    _ => new Rect(expected[0], expected[1], expected[2], expected[3])
                };
                Assert.That(actual, Is.EqualTo(desired));
            }
            finally { AssetDatabase.DeleteAsset(path); }
        }

        [TestCase("tint")]
        [TestCase("point")]
        [TestCase("position")]
        [TestCase("vector")]
        [TestCase("rotation")]
        [TestCase("rectangle")]
        public void InvalidShapeRejectsWithoutChangingNativeState(string field)
        {
            var asset = ScriptableObject.CreateInstance<VmSerializedFloatGeometryTestAsset>();
            try
            {
                var serialized = new SerializedObject(asset);
                string before = EditorJsonUtility.ToJson(asset);
                Assert.Throws<ArgumentException>(() => VmAutomationComponentCommands.SetSerializedValue(
                    serialized.FindProperty(field), new object[] { 1 }));
                serialized.ApplyModifiedPropertiesWithoutUndo();
                Assert.That(EditorJsonUtility.ToJson(asset), Is.EqualTo(before));
            }
            finally { UnityEngine.Object.DestroyImmediate(asset); }
        }
    }
}
