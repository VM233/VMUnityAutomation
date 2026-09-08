using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace VMUnityAutomation.Editor.Tests
{
    [Category(VmAutomationPackageTestCommands.DefaultPackageSmokeCategory)]
    [Category(VmAutomationPackageTestCommands.FullPackageRegressionCategory)]
    public sealed class VmAutomationQuaternionPropertyTests
    {
        [TestCase("")]
        [TestCase("Graphic")]
        public void PrefabRotationIsWrittenAsOneQuaternion(string childPath)
        {
            string path = AssetDatabase.GenerateUniqueAssetPath("Assets/Quaternion Test.prefab");
            var root = new GameObject("Quaternion Test");
            var child = new GameObject("Graphic");
            child.transform.SetParent(root.transform, false);
            root.transform.localRotation = Quaternion.Euler(0, 0, -140);
            child.transform.localRotation = Quaternion.Euler(0, 0, -140);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            UnityEngine.Object.DestroyImmediate(root);
            Quaternion expected = Quaternion.Euler(17, 31, -135);
            try
            {
                object result = VmAutomationPrefabComponentCommands.SetComponentProperty(
                    new Dictionary<string, object>
                    {
                        { "assetPath", path }, { "prefabPath", childPath },
                        { "componentType", "Transform" }, { "propertyName", "m_LocalRotation" },
                        { "value", new Dictionary<string, object>
                            { { "x", expected.x }, { "y", expected.y }, { "z", expected.z }, { "w", expected.w } } }
                    });
                Assert.That(result, Is.InstanceOf<Dictionary<string, object>>());
                Assert.That(((Dictionary<string, object>)result)["persisted"], Is.True);
                var reopened = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    Transform target = childPath.Length == 0 ? reopened.transform : reopened.transform.Find(childPath);
                    Assert.That(Mathf.Abs(Quaternion.Dot(expected, target.localRotation)),
                        Is.EqualTo(1f).Within(0.000001f));
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(reopened);
                }
            }
            finally
            {
                AssetDatabase.DeleteAsset(path);
            }
        }

        [Test]
        public void MissingQuaternionComponentFailsBeforeMutation()
        {
            var target = new GameObject("Quaternion Input Test");
            try
            {
                using (var serialized = new SerializedObject(target.transform))
                {
                    var rotation = serialized.FindProperty("m_LocalRotation");
                    Assert.Throws<ArgumentException>(() => VmAutomationComponentCommands.SetSerializedValue(rotation,
                        new Dictionary<string, object> { { "x", 0f }, { "y", 0f }, { "z", 0f } }));
                    serialized.ApplyModifiedProperties();
                    Assert.That(target.transform.localRotation, Is.EqualTo(Quaternion.identity));
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(target);
            }
        }
    }
}
