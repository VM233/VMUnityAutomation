using System;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace VMUnityAutomation.Editor.Tests
{
    public sealed class VmAssetReimportTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void ReimportAdoptsCurrentFileAndPreservesMetadata(bool throughMetadata)
        {
            string folder = CreateFolder();
            string path = folder + "/Value.asset";
            try
            {
                var value = ScriptableObject.CreateInstance<VmAutomationInspectionTestAsset>();
                value.label = "before";
                AssetDatabase.CreateAsset(value, path);
                AssetDatabase.SaveAssetIfDirty(value);
                string guid = AssetDatabase.AssetPathToGUID(path);
                byte[] metadata = File.ReadAllBytes(path + ".meta");
                string current = File.ReadAllText(path);
                Assert.That(current, Does.Contain("label: before"));
                File.WriteAllText(path, current.Replace("label: before", "label: after"));

                VmAssetReimportResult result = new VmAssetReimportTool().Execute(
                    new VmAssetReimportRequest { AssetPath = path + (throughMetadata ? ".meta" : "") });
                Assert.That(result.Imported, Is.True);
                Assert.That(result.AssetPath, Is.EqualTo(path));
                Assert.That(result.GuidBefore, Is.EqualTo(guid));
                Assert.That(result.GuidAfter, Is.EqualTo(guid));
                Assert.That(File.ReadAllBytes(path + ".meta"), Is.EqualTo(metadata));
                Assert.That(AssetDatabase.LoadAssetAtPath<VmAutomationInspectionTestAsset>(path).label,
                    Is.EqualTo("after"));
            }
            finally { AssetDatabase.DeleteAsset(folder); }
        }

        [TestCase(".cs")]
        [TestCase(".asmdef")]
        [TestCase(".asmref")]
        [TestCase(".rsp")]
        [TestCase(".dll")]
        [TestCase(".exe")]
        [TestCase(".winmd")]
        public void CompilationInputsUseTheSharedImportPolicy(string extension)
        {
            Assert.That(VmAutomationAssetCommands.IsCompilationAssetPath("Assets/Code" + extension), Is.True);
            Assert.That(VmAutomationAssetCommands.GetTargetedImportOptions("Assets/Code" + extension + ".meta", true)
                & ImportAssetOptions.ForceUpdate, Is.EqualTo(ImportAssetOptions.Default));
        }

        [Test]
        public void InvalidFolderAndCompilationFileRejectBeforeImport()
        {
            string folder = CreateFolder();
            string path = folder + "/Invalid.dll";
            try
            {
                var tool = new VmAssetReimportTool();
                Assert.That(Assert.Throws<VmProjectToolException>(() => tool.Execute(
                    new VmAssetReimportRequest { AssetPath = folder })).ErrorCode,
                    Is.EqualTo("asset_reimport_invalid_path"));
                File.WriteAllText(path, "No importer may run on this invalid compilation fixture.");
                Assert.That(Assert.Throws<VmProjectToolException>(() => tool.Execute(
                    new VmAssetReimportRequest { AssetPath = path })).ErrorCode,
                    Is.EqualTo("asset_reimport_compilation_asset"));
                Assert.That(File.Exists(path + ".meta"), Is.False);
                File.Delete(path);
            }
            finally
            {
                if (File.Exists(path)) File.Delete(path);
                AssetDatabase.DeleteAsset(folder);
            }
        }

        [Test]
        public void LoadedSceneMetadataRejectsBeforeMutation()
        {
            string folder = CreateFolder();
            string path = folder + "/Loaded.unity";
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            try
            {
                Assert.That(EditorSceneManager.SaveScene(scene, path), Is.True);
                byte[] before = File.ReadAllBytes(path + ".meta");
                Assert.That(Assert.Throws<VmProjectToolException>(() => new VmAssetReimportTool().Execute(
                    new VmAssetReimportRequest { AssetPath = path + ".meta" })).ErrorCode,
                    Is.EqualTo("loaded_scene_asset_mutation_blocked"));
                Assert.That(File.ReadAllBytes(path + ".meta"), Is.EqualTo(before));
                Assert.That(scene.isDirty, Is.False);
            }
            finally
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                AssetDatabase.DeleteAsset(folder);
            }
        }

        private static string CreateFolder()
        {
            string name = "__VmAssetReimport_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", name);
            return "Assets/" + name;
        }
    }
}
