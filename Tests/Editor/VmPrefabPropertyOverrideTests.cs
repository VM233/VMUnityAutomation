using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace VMUnityAutomation.Editor.Tests
{
    [Category(VmAutomationPackageTestCommands.DefaultPackageSmokeCategory)]
    [Category(VmAutomationPackageTestCommands.FullPackageRegressionCategory)]
    public sealed class VmPrefabPropertyOverrideTests
    {
#if UNITY_2022_1_OR_NEWER
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        [TestCase(3)]
        public void RevertRemovedOverridesPersistsAndPreservesUnmatchedState(int selection)
        {
            string basePath = AssetDatabase.GenerateUniqueAssetPath("Assets/Removed Override Source.prefab");
            string variantPath = AssetDatabase.GenerateUniqueAssetPath("Assets/Removed Override Variant.prefab");
            Scene active = SceneManager.GetActiveScene();
            bool dirty = active.isDirty;
            int sceneCount = SceneManager.sceneCount;
            var preview = EditorSceneManager.NewPreviewScene();
            GameObject root = null;
            GameObject instance = null;
            try
            {
                root = new GameObject("Removed Override Source");
                SceneManager.MoveGameObjectToScene(root, preview);
                foreach (string name in new[] { "Removed Node", "Other Removed Node", "Components" })
                {
                    var child = new GameObject(name);
                    child.transform.SetParent(root.transform, false);
                }
                root.transform.Find("Components").gameObject.AddComponent<BoxCollider2D>();
                PrefabUtility.SaveAsPrefabAsset(root, basePath);
                Object.DestroyImmediate(root);
                root = null;
                string baseText = System.IO.File.ReadAllText(basePath);
                instance = (GameObject)PrefabUtility.InstantiatePrefab(
                    AssetDatabase.LoadAssetAtPath<GameObject>(basePath), preview);
                Object.DestroyImmediate(instance.transform.Find("Removed Node").gameObject);
                Object.DestroyImmediate(instance.transform.Find("Other Removed Node").gameObject);
                GameObject components = instance.transform.Find("Components").gameObject;
                Object.DestroyImmediate(components.GetComponent<BoxCollider2D>());
                components.AddComponent<CapsuleCollider2D>();
                instance.transform.localRotation = Quaternion.Euler(0, 0, 90);
                PrefabUtility.RecordPrefabInstancePropertyModifications(instance.transform);
                PrefabUtility.SaveAsPrefabAsset(instance, variantPath);
                Object.DestroyImmediate(instance);
                instance = null;

                var args = new Dictionary<string, object> { { "assetPath", variantPath } };
                if (selection == 0) args["targetGameObject"] = "Removed Node";
                if (selection == 1 || selection == 3)
                {
                    args["targetComponentType"] = nameof(BoxCollider2D);
                    args["targetGameObject"] = selection == 1 ? "Components" : "Other Removed Node";
                }
                if (selection == 2) args["revertAll"] = true;
                object result = VmAutomationPrefabVariantCommands.RevertVariantOverride(args);
                Assert.That(result, Is.InstanceOf<Dictionary<string, object>>(), result.ToString());
                var receipt = (Dictionary<string, object>)result;
                Assert.That(receipt["success"], Is.True);
                Assert.That(receipt["revertedCount"], Is.EqualTo(selection == 2 ? (object)"all" : selection == 3 ? 0 : 1));
                var reopened = PrefabUtility.LoadPrefabContents(variantPath);
                try
                {
                    Assert.That(reopened.transform.Find("Removed Node") != null, Is.EqualTo(selection == 0 || selection == 2));
                    Assert.That(reopened.transform.Find("Other Removed Node") != null, Is.EqualTo(selection == 2));
                    components = reopened.transform.Find("Components").gameObject;
                    Assert.That(components.GetComponent<BoxCollider2D>() != null, Is.EqualTo(selection == 1 || selection == 2));
                    Assert.That(components.GetComponent<CapsuleCollider2D>() != null, Is.EqualTo(selection != 2));
                    var rotation = selection == 2 ? Quaternion.identity : Quaternion.Euler(0, 0, 90);
                    Assert.That(Quaternion.Angle(reopened.transform.localRotation, rotation), Is.LessThan(.001f));
                }
                finally { PrefabUtility.UnloadPrefabContents(reopened); }
                Assert.That(System.IO.File.ReadAllText(basePath), Is.EqualTo(baseText));
                Assert.That(PrefabUtility.GetPrefabAssetType(AssetDatabase.LoadAssetAtPath<GameObject>(variantPath)),
                    Is.EqualTo(PrefabAssetType.Variant));
                Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(active));
                Assert.That(active.isDirty, Is.EqualTo(dirty));
                Assert.That(SceneManager.sceneCount, Is.EqualTo(sceneCount));
            }
            finally
            {
                Object.DestroyImmediate(instance);
                Object.DestroyImmediate(root);
                EditorSceneManager.ClosePreviewScene(preview);
                AssetDatabase.DeleteAsset(variantPath);
                AssetDatabase.DeleteAsset(basePath);
            }
        }

#endif

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
                    { "operations", new List<object> { new Dictionary<string, object>
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
