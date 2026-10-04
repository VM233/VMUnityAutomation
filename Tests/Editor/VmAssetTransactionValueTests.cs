using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace VMUnityAutomation.Editor.Tests
{
    [Category(VmAutomationPackageTestCommands.DefaultPackageSmokeCategory)]
    [Category(VmAutomationPackageTestCommands.FullPackageRegressionCategory)]
    public sealed class VmAssetTransactionValueTests
    {
        [TestCase(null)]
        [TestCase(false)]
        [TestCase(0)]
        [TestCase("")]
        public void PresentJsonValueIsAdmitted(object value)
        {
            WithMaterial(path =>
            {
                var operation = Operation(path);
                operation["value"] = value;
                var plan = VmAutomationAssetTransactionPlan.Build(Request(operation));
                Assert.That(plan.Operations, Has.Count.EqualTo(1));
                Assert.That(plan.Operations[0].ContainsKey("value"), Is.True);
                Assert.That(plan.Operations[0]["value"], Is.EqualTo(value));
            });
        }

        [Test]
        public void OmittedValueIsRejectedBeforeMutation()
        {
            WithMaterial(path =>
            {
                var error = Assert.Throws<VmAutomationAssetTransactionPlan.ValidationException>(
                    () => VmAutomationAssetTransactionPlan.Build(Request(Operation(path))));
                Assert.That(error.Message, Does.Contain("requires 'value'"));
                Assert.That(AssetDatabase.LoadAssetAtPath<Material>(path).shader, Is.Not.Null);
            });
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase(" ")]
        public void InvalidPropertyPathRemainsRejected(string propertyPath)
        {
            WithMaterial(path =>
            {
                var operation = Operation(path);
                operation["propertyPath"] = propertyPath;
                operation["value"] = null;
                var error = Assert.Throws<VmAutomationAssetTransactionPlan.ValidationException>(
                    () => VmAutomationAssetTransactionPlan.Build(Request(operation)));
                Assert.That(error.Message, Does.Contain("propertyPath"));
                Assert.That(AssetDatabase.LoadAssetAtPath<Material>(path).shader, Is.Not.Null);
            });
        }

        private static Dictionary<string, object> Operation(string path) => new()
        {
            { "type", "serialized-set" }, { "assetPath", path }, { "propertyPath", "m_Shader" }
        };

        private static Dictionary<string, object> Request(Dictionary<string, object> operation) => new()
        {
            { "operations", new List<object> { operation } }
        };

        private static void WithMaterial(System.Action<string> test)
        {
            string path = AssetDatabase.GenerateUniqueAssetPath("Assets/Asset Transaction Value Test.mat");
            var material = new Material(Shader.Find("Hidden/InternalErrorShader"));
            AssetDatabase.CreateAsset(material, path);
            try { test(path); }
            finally { AssetDatabase.DeleteAsset(path); }
        }
    }
}
