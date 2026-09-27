using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace VMUnityAutomation.Editor.Tests
{
    [Category(VmAutomationPackageTestCommands.DefaultPackageSmokeCategory)]
    [Category(VmAutomationPackageTestCommands.FullPackageRegressionCategory)]
    public sealed class VmAutomationSelectionAssetTests
    {
        [Test]
        public void SelectionSetAndGetIncludeProjectAssetsAndSceneObjects()
        {
            string path = AssetDatabase.GenerateUniqueAssetPath("Assets/Selection Asset Test.asset");
            var asset = ScriptableObject.CreateInstance<VmAutomationInspectionTestAsset>();
            AssetDatabase.CreateAsset(asset, path);
            var gameObject = new GameObject("Selection Scene Test");
            Object[] previousSelection = Selection.objects;

            try
            {
                var assetResult = (Dictionary<string, object>)VmAutomationSelectionCommands.SetSelection(
                    new Dictionary<string, object> { { "path", path } });
                Assert.That(assetResult["selectedCount"], Is.EqualTo(1));
                Assert.That(Selection.activeObject, Is.EqualTo(asset));

                var assetReadback = (Dictionary<string, object>)VmAutomationSelectionCommands.GetSelection(
                    new Dictionary<string, object>());
                Assert.That(assetReadback["count"], Is.EqualTo(1));
                var selectedAsset = ((List<Dictionary<string, object>>)assetReadback["selected"])[0];
                Assert.That(selectedAsset["path"], Is.EqualTo(path));
                Assert.That(assetReadback["activeObject"], Is.EqualTo(asset.name));

                var sceneResult = (Dictionary<string, object>)VmAutomationSelectionCommands.SetSelection(
                    new Dictionary<string, object> { { "path", gameObject.name } });
                Assert.That(sceneResult["selectedCount"], Is.EqualTo(1));
                Assert.That(Selection.activeObject, Is.EqualTo(gameObject));

                var sceneReadback = (Dictionary<string, object>)VmAutomationSelectionCommands.GetSelection(
                    new Dictionary<string, object>());
                var selectedSceneObject = ((List<Dictionary<string, object>>)sceneReadback["selected"])[0];
                Assert.That(selectedSceneObject["path"], Is.EqualTo(gameObject.name));
            }
            finally
            {
                Selection.objects = previousSelection;
                Object.DestroyImmediate(gameObject);
                AssetDatabase.DeleteAsset(path);
            }
        }
    }
}
