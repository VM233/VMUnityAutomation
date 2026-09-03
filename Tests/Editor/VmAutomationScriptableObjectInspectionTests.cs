using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace VMUnityAutomation.Editor.Tests
{
    public sealed class VmAutomationScriptableObjectInspectionTests
    {
        [Test]
        public void InspectionPreservesTypedValuesCollectionsAndReferenceIdentity()
        {
            string path = AssetDatabase.GenerateUniqueAssetPath("Assets/Inspection Test.asset");
            var asset = ScriptableObject.CreateInstance<VmAutomationInspectionTestAsset>();
            asset.label = "Preset";
            asset.enabledValue = true;
            asset.point = new Vector3(1, 2, 3);
            asset.reference = asset;
            asset.entries.Add(new VmAutomationInspectionTestAsset.Entry { label = "HUD", value = 100 });
            AssetDatabase.CreateAsset(asset, path);
            try
            {
                var result = (Dictionary<string, object>)VmAutomationScriptableObjectCommands.GetScriptableObjectInfo(
                    new Dictionary<string, object> { { "path", path } });
                var fields = ((List<Dictionary<string, object>>)result["properties"])
                    .ToDictionary(field => (string)field["name"], field => field["value"]);

                Assert.That(fields["label"], Is.EqualTo("Preset"));
                Assert.That(fields["enabledValue"], Is.True);
                Assert.That(((Dictionary<string, object>)fields["point"])["z"], Is.EqualTo(3f));
                Assert.That(((Dictionary<string, object>)fields["reference"])["assetPath"], Is.EqualTo(path));

                var array = (Dictionary<string, object>)fields["entries"];
                Assert.That(array["arraySize"], Is.EqualTo(1));
                Assert.That(array["truncated"], Is.False);
                var entry = (Dictionary<string, object>)((List<object>)array["items"])[0];
                Assert.That(entry["label"], Is.EqualTo("HUD"));
                Assert.That(entry["value"], Is.EqualTo(100));

                var changed = (Dictionary<string, object>)VmAutomationScriptableObjectCommands.SetScriptableObjectField(
                    new Dictionary<string, object> { { "path", path }, { "field", "label" }, { "value", "Changed" } });
                Assert.That(changed["value"], Is.EqualTo("Changed"));
            }
            finally
            {
                AssetDatabase.DeleteAsset(path);
            }
        }

        [TestCase("scriptableobject/info")]
        [TestCase("scriptableobject/set-field")]
        public void ReadbackSchemaAcceptsStructuredJsonValues(string route)
        {
            Assert.That(VmAutomationGeneratedRouteContracts.TryGetOutput(route, out var schema), Is.True);
            var fields = (Dictionary<string, object>)schema["properties"];
            if (route == "scriptableobject/info")
            {
                var properties = (Dictionary<string, object>)fields["properties"];
                var item = (Dictionary<string, object>)properties["items"];
                fields = (Dictionary<string, object>)item["properties"];
            }

            var value = (Dictionary<string, object>)fields["value"];
            Assert.That(value["$ref"], Is.EqualTo("#/$defs/unityJsonValue"));
        }
    }
}
