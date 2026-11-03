using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace VMUnityAutomation.Editor.Tests
{
    [Category(VmAutomationPackageTestCommands.DefaultPackageSmokeCategory)]
    [Category(VmAutomationPackageTestCommands.FullPackageRegressionCategory)]
    public sealed class VmPrefabOverrideCleanupTests
    {
        [TestCase(0)] // Nested instance: preserve unselected missing field and valid references.
        [TestCase(1)] // Dry-run: byte-for-byte no publication.
        [TestCase(2)] // Original root Variant scope.
        [TestCase(3)] // A descendant is not an instance root; reject before write.
        public void ExactScopeRetiresOnlySelectedMissingField(int scenario)
        {
            string sourcePath = AssetDatabase.GenerateUniqueAssetPath("Assets/Cleanup Source.prefab");
            string assetPath = AssetDatabase.GenerateUniqueAssetPath("Assets/Cleanup Consumer.prefab");
            Scene active = SceneManager.GetActiveScene();
            bool dirty = active.isDirty;
            int sceneCount = SceneManager.sceneCount;
            Scene preview = EditorSceneManager.NewPreviewScene();
            GameObject source = null;
            GameObject outer = null;
            GameObject instance = null;
            try
            {
                source = new GameObject("Nested");
                SceneManager.MoveGameObjectToScene(source, preview);
                source.AddComponent<BoxCollider2D>();
                source.AddComponent<Rigidbody2D>();
                source.AddComponent<DistanceJoint2D>();
                var child = new GameObject("Body");
                child.transform.SetParent(source.transform, false);
                child.AddComponent<Rigidbody2D>();
                PrefabUtility.SaveAsPrefabAsset(source, sourcePath);
                Object.DestroyImmediate(source);
                source = null;
                string sourceText = File.ReadAllText(sourcePath);
                GameObject sourceAsset = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
                instance = (GameObject)PrefabUtility.InstantiatePrefab(sourceAsset, preview);
                if (scenario != 2)
                {
                    outer = new GameObject("Consumer");
                    SceneManager.MoveGameObjectToScene(outer, preview);
                    instance.transform.SetParent(outer.transform, false);
                }
                var collider = instance.GetComponent<BoxCollider2D>();
                collider.offset = new Vector2(.375f, 0);
                PrefabUtility.RecordPrefabInstancePropertyModifications(collider);
                var joint = instance.GetComponent<DistanceJoint2D>();
                joint.connectedBody = instance.transform.Find("Body").GetComponent<Rigidbody2D>();
                PrefabUtility.RecordPrefabInstancePropertyModifications(joint);
                var modifications = (PrefabUtility.GetPropertyModifications(instance) ??
                                     System.Array.Empty<PropertyModification>()).ToList();
                modifications.Add(new PropertyModification { target = sourceAsset.GetComponent<BoxCollider2D>(),
                    propertyPath = "retiredDelay", value = "1" });
                modifications.Add(new PropertyModification { target = sourceAsset.GetComponent<BoxCollider2D>(),
                    propertyPath = "unselectedRetired", value = "7" });
                PrefabUtility.SetPropertyModifications(instance, modifications.ToArray());
                GameObject saved = PrefabUtility.SaveAsPrefabAsset(outer != null ? outer : instance, assetPath);
                string scopePath = scenario == 2 ? "" : saved.transform.GetChild(0).name;
                Object.DestroyImmediate(outer != null ? outer : instance);
                outer = null;
                instance = null;
                byte[] before = File.ReadAllBytes(assetPath);
                var arguments = new Dictionary<string, object>
                {
                    ["assetPath"] = assetPath, ["prefabPath"] = scenario == 3 ? scopePath + "/Body" : scopePath,
                    ["propertyPaths"] = new List<object> { "retiredDelay" }, ["dryRun"] = scenario == 1
                };
                var result = (Dictionary<string, object>)VmPrefabOverrideCleanupCommands.Cleanup(arguments);
                Assert.That(result["success"], Is.EqualTo(scenario != 3), MiniJson.Serialize(result));
                if (scenario == 3)
                {
                    Assert.That(result["errorCode"], Is.EqualTo("invalid_arguments"));
                    Assert.That(File.ReadAllBytes(assetPath), Is.EqualTo(before));
                }
                else
                {
                    Assert.That(result["removedCount"], Is.EqualTo(1));
                    if (scenario == 1) Assert.That(File.ReadAllBytes(assetPath), Is.EqualTo(before));
                    GameObject reopened = PrefabUtility.LoadPrefabContents(assetPath);
                    try
                    {
                        GameObject nested = scenario == 2 ? reopened : reopened.transform.Find(scopePath).gameObject;
                        var persisted = PrefabUtility.GetPropertyModifications(nested);
                        Assert.That(persisted.Any(mod => mod.propertyPath == "retiredDelay"), Is.EqualTo(scenario == 1));
                        Assert.That(persisted.Any(mod => mod.propertyPath == "unselectedRetired" && mod.value == "7"), Is.True);
                        Assert.That(nested.GetComponent<BoxCollider2D>().offset.x, Is.EqualTo(.375f));
                        Assert.That(nested.GetComponent<DistanceJoint2D>().connectedBody,
                            Is.EqualTo(nested.transform.Find("Body").GetComponent<Rigidbody2D>()));
                    }
                    finally { PrefabUtility.UnloadPrefabContents(reopened); }
                }
                Assert.That(File.ReadAllText(sourcePath), Is.EqualTo(sourceText));
                Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(active));
                Assert.That(active.isDirty, Is.EqualTo(dirty));
                Assert.That(SceneManager.sceneCount, Is.EqualTo(sceneCount));
            }
            finally
            {
                Object.DestroyImmediate(instance);
                Object.DestroyImmediate(outer);
                Object.DestroyImmediate(source);
                EditorSceneManager.ClosePreviewScene(preview);
                AssetDatabase.DeleteAsset(assetPath);
                AssetDatabase.DeleteAsset(sourcePath);
            }
        }
    }
}
