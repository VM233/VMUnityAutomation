using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace VMUnityAutomation.Editor.Tests
{
    [Category(VmAutomationPackageTestCommands.DefaultPackageSmokeCategory)]
    [Category(VmAutomationPackageTestCommands.FullPackageRegressionCategory)]
    public sealed class VmSerializedFloatingTests
    {
        [TestCase("precise", 0.88, false)]
        [TestCase("precise", 1.0000000000000002, false)]
        [TestCase("precise", 1e100, false)]
        [TestCase("ordinary", 0.88, false)]
        [TestCase("ordinary", 2.5, false)]
        [TestCase("precise", 0.88, true)]
        [TestCase("precise", 1.0000000000000002, true)]
        [TestCase("precise", 1e100, true)]
        [TestCase("ordinary", 0.88, true)]
        [TestCase("ordinary", 2.5, true)]
        public void PublicSettersPersistTheDestinationPrecision(string field, double input, bool scriptableRoute)
        {
            string path = AssetDatabase.GenerateUniqueAssetPath("Assets/Floating Scalar Test.asset");
            var asset = ScriptableObject.CreateInstance<VmSerializedFloatingTestAsset>();
            AssetDatabase.CreateAsset(asset, path);
            try
            {
                if (scriptableRoute)
                    VmAutomationScriptableObjectCommands.SetScriptableObjectField(new Dictionary<string, object>
                        { { "path", path }, { "field", field }, { "value", input } });
                else
                    VmAutomationSerializedObjectCommands.Set(new Dictionary<string, object>
                        { { "assetPath", path }, { "propertyPath", field }, { "value", input } });
                Resources.UnloadAsset(asset);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                var reopened = AssetDatabase.LoadAssetAtPath<VmSerializedFloatingTestAsset>(path);
                if (field == "precise") Assert.That(reopened.precise, Is.EqualTo(input));
                else Assert.That(reopened.ordinary, Is.EqualTo((float)input));
                var read = (Dictionary<string, object>)VmAutomationSerializedObjectCommands.Get(
                    new Dictionary<string, object> { { "assetPath", path }, { "propertyPath", field } });
                var property = (Dictionary<string, object>)read["property"];
                if (field == "precise")
                {
                    Assert.That(property["value"], Is.TypeOf<double>());
                    Assert.That(property["value"], Is.EqualTo(input));
                }
                else
                {
                    Assert.That(property["value"], Is.TypeOf<float>());
                    Assert.That(property["value"], Is.EqualTo((float)input));
                }
            }
            finally { AssetDatabase.DeleteAsset(path); }
        }

        [TestCase("ordinary", 1e100)]
        [TestCase("precise", double.PositiveInfinity)]
        [TestCase("precise", double.NaN)]
        public void NonfiniteConversionCannotOverwriteThePreviousValue(string field, double input)
        {
            var asset = ScriptableObject.CreateInstance<VmSerializedFloatingTestAsset>();
            asset.precise = 17;
            asset.ordinary = 17;
            try
            {
                using (var serialized = new SerializedObject(asset))
                {
                    Assert.Throws<ArgumentOutOfRangeException>(() => VmAutomationComponentCommands.SetSerializedValue(
                        serialized.FindProperty(field), input));
                    serialized.ApplyModifiedProperties();
                }
                Assert.That(asset.precise, Is.EqualTo(17));
                Assert.That(asset.ordinary, Is.EqualTo(17));
            }
            finally { UnityEngine.Object.DestroyImmediate(asset); }
        }
    }
}
