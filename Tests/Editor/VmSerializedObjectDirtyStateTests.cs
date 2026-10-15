using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace VMUnityAutomation.Editor.Tests
{
    [Category(VmAutomationPackageTestCommands.DefaultPackageSmokeCategory)]
    [Category(VmAutomationPackageTestCommands.FullPackageRegressionCategory)]
    public sealed class VmSerializedObjectDirtyStateTests
    {
        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void ReadReportsNativeDirtyStateWithoutSavingOrChangingIt(bool dirty, bool selectedProperty)
        {
            string path = AssetDatabase.GenerateUniqueAssetPath("Assets/Serialized Dirty State Test.mat");
            var material = new Material(Shader.Find("Hidden/InternalErrorShader"));
            AssetDatabase.CreateAsset(material, path);
            string diskPath = Path.Combine(Directory.GetParent(Application.dataPath).FullName, path);
            try
            {
                AssetDatabase.SaveAssetIfDirty(material);
                byte[] persisted = File.ReadAllBytes(diskPath);
                int queue = material.renderQueue;
                if (dirty) EditorUtility.SetDirty(material);
                bool before = EditorUtility.IsDirty(material);
                Assert.That(before, Is.EqualTo(dirty));
                var arguments = new Dictionary<string, object> {
                    { "assetPath", path }, { "maxProperties", 500 },
                    { "maxDepth", 4 }, { "maxArrayElements", 16 }
                };
                if (selectedProperty) arguments["propertyPath"] = "m_CustomRenderQueue";
                var result = (Dictionary<string, object>)VmAutomationSerializedObjectCommands.Get(arguments);
                Assert.That(result["isDirtyBeforeRead"], Is.EqualTo(before));
                Assert.That(result["isDirtyAfterRead"], Is.EqualTo(EditorUtility.IsDirty(material)));
                Assert.That(EditorUtility.IsDirty(material), Is.EqualTo(before));
                Assert.That(material.renderQueue, Is.EqualTo(queue));
                Assert.That(File.ReadAllBytes(diskPath), Is.EqualTo(persisted));
                Assert.That(VmAutomationGeneratedRouteContracts.TryGetOutput("serialized-object/get", out var schema), Is.True);
                var fields = (Dictionary<string, object>)schema["properties"];
                foreach (string name in new[] { "isDirtyBeforeRead", "isDirtyAfterRead" })
                    Assert.That(((Dictionary<string, object>)fields[name])["type"], Is.EqualTo("boolean"));
            }
            finally { AssetDatabase.DeleteAsset(path); }
        }
    }
}
