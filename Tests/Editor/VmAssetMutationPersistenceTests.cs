using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace VMUnityAutomation.Editor.Tests
{
    [Category(VmAutomationPackageTestCommands.DefaultPackageSmokeCategory)]
    [Category(VmAutomationPackageTestCommands.FullPackageRegressionCategory)]
    public sealed class VmAssetMutationPersistenceTests
    {
        [UnityTest]
        public IEnumerator RenamePreservesUnrelatedDirtyAsset() => Observe((folder, target) =>
        {
            string guid = AssetDatabase.AssetPathToGUID(target);
            var result = (Dictionary<string, object>)VmAutomationAssetCommands.Rename(
                new Dictionary<string, object> { { "path", target }, { "newName", "Renamed" } });
            string renamed = folder + "/Renamed.asset";
            Assert.That(result["success"], Is.True);
            Assert.That(AssetDatabase.AssetPathToGUID(renamed), Is.EqualTo(guid));
            AssetDatabase.ImportAsset(renamed, ImportAssetOptions.ForceSynchronousImport);
            Assert.That(AssetDatabase.LoadMainAssetAtPath(renamed).name, Is.EqualTo("Renamed"));
        });

        [UnityTest]
        public IEnumerator MovePreservesUnrelatedDirtyAsset() => Observe((folder, target) =>
        {
            string guid = AssetDatabase.AssetPathToGUID(target);
            string moved = folder + "/Moved.asset";
            var result = (Dictionary<string, object>)VmAutomationAssetCommands.Move(
                new Dictionary<string, object> {
                    { "moves", new List<Dictionary<string, object>> {
                        new() { { "path", target }, { "destinationPath", moved } }
                    } }
                });
            Assert.That(result["success"], Is.True);
            AssetDatabase.ImportAsset(moved, ImportAssetOptions.ForceSynchronousImport);
            Assert.That(AssetDatabase.AssetPathToGUID(moved), Is.EqualTo(guid));
            Assert.That(AssetDatabase.LoadMainAssetAtPath(moved), Is.Not.Null);
        });

        [UnityTest]
        public IEnumerator DeletePreservesUnrelatedDirtyAsset() => Observe((_, target) =>
        {
            var result = (Dictionary<string, object>)VmAutomationAssetCommands.Delete(
                new Dictionary<string, object> { { "path", target } });
            Assert.That(result["success"], Is.True);
            Assert.That(result["savedAssets"], Is.False);
            Assert.That(File.Exists(Absolute(target)), Is.False);
            Assert.That(AssetDatabase.LoadMainAssetAtPath(target), Is.Null);
        });

        [UnityTest]
        public IEnumerator MaterialCreationPreservesUnrelatedDirtyAsset() => Observe((folder, _) =>
        {
            string path = folder + "/Created.mat";
            VmAutomationAssetCommands.CreateMaterial(new Dictionary<string, object> {
                { "path", path }, { "shader", "Hidden/Internal-Colored" }
            });
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            Assert.That(AssetDatabase.LoadAssetAtPath<Material>(path), Is.Not.Null);
            Assert.That(File.Exists(Absolute(path)), Is.True);
        });

        private static IEnumerator Observe(Action<string, string> mutate)
        {
            string folder = AssetDatabase.GenerateUniqueAssetPath("Assets/Asset Mutation Persistence Tests");
            AssetDatabase.CreateFolder("Assets", Path.GetFileName(folder));
            string unrelatedPath = folder + "/Unrelated.asset";
            string targetPath = folder + "/Target.asset";
            var unrelated = ScriptableObject.CreateInstance<VmAutomationInspectionTestAsset>();
            var target = ScriptableObject.CreateInstance<VmAutomationInspectionTestAsset>();
            AssetDatabase.CreateAsset(unrelated, unrelatedPath);
            AssetDatabase.CreateAsset(target, targetPath);
            try
            {
                byte[] original = File.ReadAllBytes(Absolute(unrelatedPath));
                unrelated.point = new Vector3(7, 11, 13);
                EditorUtility.SetDirty(unrelated);
                Assert.That(EditorUtility.IsDirty(unrelated), Is.True);
                mutate(folder, targetPath);
                AssertUnrelated(unrelated, unrelatedPath, original);
                yield return null;
                yield return null;
                AssertUnrelated(unrelated, unrelatedPath, original);
            }
            finally { AssetDatabase.DeleteAsset(folder); }
        }

        private static void AssertUnrelated(VmAutomationInspectionTestAsset asset, string path, byte[] original)
        {
            Assert.That(EditorUtility.IsDirty(asset), Is.True, "An unrelated pending edit was saved or discarded.");
            Assert.That(asset.point, Is.EqualTo(new Vector3(7, 11, 13)));
            Assert.That(File.ReadAllBytes(Absolute(path)), Is.EqualTo(original), "An unrelated dirty asset was persisted.");
        }

        private static string Absolute(string path) => Path.Combine(Directory.GetParent(Application.dataPath).FullName, path);
    }
}
