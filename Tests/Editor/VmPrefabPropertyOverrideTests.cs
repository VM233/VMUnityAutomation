using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace VMUnityAutomation.Editor.Tests
{
    [Category(VmAutomationPackageTestCommands.DefaultPackageSmokeCategory)]
    [Category(VmAutomationPackageTestCommands.FullPackageRegressionCategory)]
    public sealed class VmPrefabPropertyOverrideTests
    {
        [Test]
        public void RevertScaleRemovesOverridesAndKeepsRotationAfterReopening()
        {
            string basePath = AssetDatabase.GenerateUniqueAssetPath("Assets/Override Source.prefab");
            string variantPath = AssetDatabase.GenerateUniqueAssetPath("Assets/Override Variant.prefab");
            var root = new GameObject("Override Source");
            var child = new GameObject("Main");
            child.transform.SetParent(root.transform, false);
            child.transform.localScale = Vector3.one * .4225f;
            PrefabUtility.SaveAsPrefabAsset(root, basePath);
            Object.DestroyImmediate(root);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(basePath));
            Transform main = instance.transform.Find("Main");
            main.localScale = Vector3.one * .105625f;
            main.localRotation = Quaternion.Euler(0, 0, 225);
            PrefabUtility.RecordPrefabInstancePropertyModifications(main);
            PrefabUtility.SaveAsPrefabAsset(instance, variantPath);
            Object.DestroyImmediate(instance);
            try
            {
                object result = VmAutomationPrefabTransactionCommands.TransactionEdit(new Dictionary<string, object>
                {
                    { "assetPath", variantPath },
                    { "execution", new Dictionary<string, object> { { "mode", "immediate" } } },
                    { "operations", new object[] { new Dictionary<string, object>
                        {
                            { "type", "revertProperty" }, { "prefabPath", "Main" },
                            { "componentType", "Transform" }, { "propertyName", "m_LocalScale" }
                        } } }
                });
                Assert.That(result, Is.InstanceOf<Dictionary<string, object>>());
                Assert.That(((Dictionary<string, object>)result)["saved"], Is.True);
                GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(variantPath);
                Assert.That(PrefabUtility.GetPropertyModifications(asset)
                    .Any(mod => mod.propertyPath.StartsWith("m_LocalScale")), Is.False);
                var reopened = PrefabUtility.LoadPrefabContents(variantPath);
                try
                {
                    main = reopened.transform.Find("Main");
                    Assert.That(main.localScale, Is.EqualTo(Vector3.one * .4225f));
                    Assert.That(Quaternion.Angle(main.localRotation, Quaternion.Euler(0, 0, 225)), Is.LessThan(.001f));
                }
                finally { PrefabUtility.UnloadPrefabContents(reopened); }
            }
            finally
            {
                AssetDatabase.DeleteAsset(variantPath);
                AssetDatabase.DeleteAsset(basePath);
            }
        }
    }
}
