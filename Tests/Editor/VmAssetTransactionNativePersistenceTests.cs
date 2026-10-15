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
    public sealed class VmAssetTransactionNativePersistenceTests
    {
        [UnityTest]
        public IEnumerator TransactionWriteRemainsSavedAcrossNativeEditorPublication()
        {
            string path = AssetDatabase.GenerateUniqueAssetPath("Assets/Transaction Native Persistence Test.asset");
            var asset = ScriptableObject.CreateInstance<VmAutomationInspectionTestAsset>();
            AssetDatabase.CreateAsset(asset, path);
            string diskPath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, path);
            try
            {
                var result = (Dictionary<string, object>)VmAutomationSerializedObjectCommands.SetForTransaction(
                    new Dictionary<string, object> {
                        { "assetPath", path }, { "propertyPath", "point" },
                        { "value", new Dictionary<string, object> { { "x", 1f }, { "y", 2f }, { "z", 3f } } }
                    });
                Assert.That(result["success"], Is.True);
                Assert.That(asset.point, Is.EqualTo(new Vector3(1, 2, 3)));
                Assert.That(EditorUtility.IsDirty(asset), Is.False, "Target remains dirty immediately after its scoped save.");
                byte[] saved = File.ReadAllBytes(diskPath);
                yield return null;
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                yield return null;
                Assert.That(EditorUtility.IsDirty(asset), Is.False, "A later native Editor publication re-dirtied the saved target.");
                Assert.That(File.ReadAllBytes(diskPath), Is.EqualTo(saved));
                var read = (Dictionary<string, object>)VmAutomationSerializedObjectCommands.Get(
                    new Dictionary<string, object> { { "assetPath", path }, { "propertyPath", "point" } });
                Assert.That(read["isDirtyBeforeRead"], Is.False);
                Assert.That(read["isDirtyAfterRead"], Is.False);
                Assert.That(asset.point, Is.EqualTo(new Vector3(1, 2, 3)));
            }
            finally { AssetDatabase.DeleteAsset(path); }
        }
    }
}
