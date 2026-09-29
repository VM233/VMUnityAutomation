using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace VMUnityAutomation.Editor.Tests
{
    [Category(VmAutomationPackageTestCommands.DefaultPackageSmokeCategory)]
    [Category(VmAutomationPackageTestCommands.FullPackageRegressionCategory)]
    public sealed class VmAutomationAssetRefreshTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void MetadataRefreshPreservesFolderAndChildIdentity(bool forceUpdate)
        {
            string folder = CreateTestFolder();
            string childPath = folder + "/Child.asset";
            try
            {
                var child = ScriptableObject.CreateInstance<VmAutomationInspectionTestAsset>();
                AssetDatabase.CreateAsset(child, childPath);
                AssetDatabase.SaveAssets();
                string folderGuid = AssetDatabase.AssetPathToGUID(folder);
                string childGuid = AssetDatabase.AssetPathToGUID(childPath);
                byte[] folderMetadata = File.ReadAllBytes(folder + ".meta");
                byte[] childMetadata = File.ReadAllBytes(childPath + ".meta");
                var result = (Dictionary<string, object>)
                    VmAutomationAssetCommands.ExecuteRefreshImmediate(
                        new Dictionary<string, object>
                        {
                            { "assetPaths", new[] { folder + ".meta", folder,
                                childPath + ".meta", childPath } },
                            { "forceUpdate", forceUpdate },
                        });

                Assert.That(result["success"], Is.True);
                Assert.That(result["refreshMode"], Is.EqualTo("targeted"));
                Assert.That(result["importedPaths"], Is.EquivalentTo(new[] { folder, childPath }));
                Assert.That(AssetDatabase.IsValidFolder(folder), Is.True);
                Assert.That(AssetDatabase.AssetPathToGUID(folder), Is.EqualTo(folderGuid));
                Assert.That(AssetDatabase.AssetPathToGUID(childPath), Is.EqualTo(childGuid));
                Assert.That(File.ReadAllBytes(folder + ".meta"), Is.EqualTo(folderMetadata));
                Assert.That(File.ReadAllBytes(childPath + ".meta"), Is.EqualTo(childMetadata));
                Assert.That(AssetDatabase.LoadAssetAtPath<VmAutomationInspectionTestAsset>(childPath),
                    Is.Not.Null);
            }
            finally
            {
                AssetDatabase.DeleteAsset(folder);
            }
        }

        [TestCase(".cs")]
        [TestCase(".asmdef")]
        [TestCase(".asmref")]
        [TestCase(".rsp")]
        public void CompilationMetadataUsesCompilationImportPolicy(string extension)
        {
            ImportAssetOptions options = VmAutomationAssetCommands.GetTargetedImportOptions(
                "Assets/Compilation Asset" + extension + ".meta", true);
            Assert.That(options & ImportAssetOptions.ForceUpdate,
                Is.EqualTo(ImportAssetOptions.Default));
            Assert.That(options & ImportAssetOptions.ForceSynchronousImport,
                Is.EqualTo(ImportAssetOptions.ForceSynchronousImport));
        }

        [Test]
        public void LoadedSceneMetadataIsRejectedBeforeImport()
        {
            string folder = CreateTestFolder();
            string scenePath = folder + "/Loaded Scene.unity";
            Scene previousActiveScene = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            try
            {
                Assert.That(EditorSceneManager.SaveScene(scene, scenePath), Is.True);
                string sceneGuid = AssetDatabase.AssetPathToGUID(scenePath);
                byte[] metadata = File.ReadAllBytes(scenePath + ".meta");
                var result = (Dictionary<string, object>)
                    VmAutomationAssetCommands.ExecuteRefreshImmediate(
                        new Dictionary<string, object> { { "assetPaths", new[] { scenePath + ".meta" } } });
                Assert.That(result["success"], Is.False);
                Assert.That(result["errorCode"], Is.EqualTo("loaded_scene_asset_mutation_blocked"));
                Assert.That(AssetDatabase.AssetPathToGUID(scenePath), Is.EqualTo(sceneGuid));
                Assert.That(File.ReadAllBytes(scenePath + ".meta"), Is.EqualTo(metadata));
            }
            finally
            {
                SceneManager.SetActiveScene(previousActiveScene);
                EditorSceneManager.CloseScene(scene, true);
                AssetDatabase.DeleteAsset(folder);
            }
        }

        private static string CreateTestFolder()
        {
            string folderName = "Asset Refresh Test " + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", folderName);
            return "Assets/" + folderName;
        }
    }
}
