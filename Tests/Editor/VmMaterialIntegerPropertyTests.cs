using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

namespace VMUnityAutomation.Editor.Tests
{
    [Category(VmAutomationPackageTestCommands.DefaultPackageSmokeCategory)]
    [Category(VmAutomationPackageTestCommands.FullPackageRegressionCategory)]
    public sealed class VmMaterialIntegerPropertyTests
    {
        [TestCase(0)]
        [TestCase(16777217)]
        [TestCase(-16777217)]
        [TestCase(int.MaxValue)]
        public void IntegerCommandsPreserveNativeStorageAcrossAssetReload(int expected)
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(
                "Packages/com.vm233.unity-automation/Tests/Editor/Material Integer Properties.shader");
            Assert.That(shader, Is.Not.Null);
            Assert.That(shader.GetPropertyType(shader.FindPropertyIndex("_Count")), Is.EqualTo(ShaderPropertyType.Int));
            string path = AssetDatabase.GenerateUniqueAssetPath("Assets/Material Integer Test.mat");
            var material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
            try
            {
                var write = (Dictionary<string, object>)VmAutomationMaterialCommands.SetProperties(
                    new Dictionary<string, object>
                    {
                        { "assetPath", path },
                        { "properties", new Dictionary<string, object> { { "_Count", expected }, { "_Ratio", 3.75f } } }
                    });
                Assert.That(write["success"], Is.True);
                Assert.That(material.GetInteger("_Count"), Is.EqualTo(expected));
                Assert.That(material.GetFloat("_Ratio"), Is.EqualTo(3.75f));

                var read = (Dictionary<string, object>)VmAutomationMaterialCommands.GetProperties(
                    new Dictionary<string, object>
                    {
                        { "assetPath", path },
                        { "propertyNames", new List<object> { "_Count", "_Ratio" } }
                    });
                Assert.That(read["success"], Is.True);
                var product = (Dictionary<string, object>)read["material"];
                var properties = (Dictionary<string, object>)product["properties"];
                var count = (Dictionary<string, object>)properties["_Count"];
                Assert.That(count["type"], Is.EqualTo("Int"));
                Assert.That(Convert.ToInt32(count["value"]), Is.EqualTo(expected));

                var info = (Dictionary<string, object>)VmAutomationGraphicsCommands.GetMaterialInfo(
                    new Dictionary<string, object> { { "assetPath", path } });
                var rows = (List<Dictionary<string, object>>)info["properties"];
                Assert.That(Convert.ToInt32(rows.Single(row => (string)row["name"] == "_Count")["value"]), Is.EqualTo(expected));

                Resources.UnloadAsset(material);
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
                material = AssetDatabase.LoadAssetAtPath<Material>(path);
                Assert.That(material.GetInteger("_Count"), Is.EqualTo(expected));
                Assert.That(material.GetFloat("_Ratio"), Is.EqualTo(3.75f));
                LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                AssetDatabase.DeleteAsset(path);
            }
        }
    }
}
