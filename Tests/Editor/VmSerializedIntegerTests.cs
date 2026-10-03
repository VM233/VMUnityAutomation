using System;
using System.Collections.Generic;
using System.Globalization;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace VMUnityAutomation.Editor.Tests
{
    [Category(VmAutomationPackageTestCommands.DefaultPackageSmokeCategory)]
    [Category(VmAutomationPackageTestCommands.FullPackageRegressionCategory)]
    public sealed class VmSerializedIntegerTests
    {
        [TestCase("9007199254740993")]
        [TestCase("9223372036854775807")]
        [TestCase("-9223372036854775808")]
        public void Int64PersistsWithoutJsonPrecisionLoss(string input) => VerifyPersistence("counter", input, input);

        [TestCase(int.MinValue)]
        [TestCase(0)]
        [TestCase(int.MaxValue)]
        public void Int32RetainsNativeNumericValues(int input) => VerifyPersistence("budget", input, input);

        private static void VerifyPersistence(string propertyPath, object input, object expected)
        {
            string path = AssetDatabase.GenerateUniqueAssetPath("Assets/Integer Scalar Test.asset");
            var asset = ScriptableObject.CreateInstance<VmSerializedIntegerTestAsset>();
            AssetDatabase.CreateAsset(asset, path);
            try
            {
                var write = (Dictionary<string, object>)VmAutomationSerializedObjectCommands.Set(
                    new Dictionary<string, object>
                    {
                        { "assetPath", path }, { "propertyPath", propertyPath }, { "value", input }
                    });
                Assert.That(write["success"], Is.True);
                Resources.UnloadAsset(asset);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                var reopened = AssetDatabase.LoadAssetAtPath<VmSerializedIntegerTestAsset>(path);
                if (propertyPath == "counter")
                    Assert.That(reopened.counter, Is.EqualTo(long.Parse((string)input, CultureInfo.InvariantCulture)));
                else
                    Assert.That(reopened.budget, Is.EqualTo(input));
                var read = (Dictionary<string, object>)VmAutomationSerializedObjectCommands.Get(
                    new Dictionary<string, object> { { "assetPath", path }, { "propertyPath", propertyPath } });
                var property = (Dictionary<string, object>)read["property"];
                Assert.That(property["value"], Is.EqualTo(expected));
            }
            finally { AssetDatabase.DeleteAsset(path); }
        }

        [TestCase("9223372036854775808")]
        [TestCase("-9223372036854775809")]
        public void Int64OverflowDoesNotChangeTheProperty(string input)
        {
            var asset = ScriptableObject.CreateInstance<VmSerializedIntegerTestAsset>();
            asset.counter = 17;
            try
            {
                using (var serialized = new SerializedObject(asset))
                {
                    Assert.Throws<OverflowException>(() => VmAutomationComponentCommands.SetSerializedValue(
                        serialized.FindProperty("counter"), input));
                    serialized.ApplyModifiedProperties();
                }
                Assert.That(asset.counter, Is.EqualTo(17));
            }
            finally { UnityEngine.Object.DestroyImmediate(asset); }
        }
    }
}
