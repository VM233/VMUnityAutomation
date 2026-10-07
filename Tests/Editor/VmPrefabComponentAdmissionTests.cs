using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace VMUnityAutomation.Editor.Tests
{
    [Category(VmAutomationPackageTestCommands.DefaultPackageSmokeCategory)]
    public sealed class VmPrefabComponentAdmissionTests
    {
        private const string MissingType = "VMUnityAutomation.Tests.NoSuchComponent";
        private const string AssetPath = "Assets/No Such Prefab.prefab";

        [TestCase("add")]
        [TestCase("configure")]
        [TestCase("transaction")]
        public void MissingTypesCompleteBeforeAnyPrefabAccessOrDeferredWork(string entry)
        {
            var arguments = Arguments(entry);
            int completionCount = 0;
            int progressCount = 0;
            object response = null;
            Action<object> complete = value => { completionCount++; response = value; };
            Action<object> progress = _ => progressCount++;
            switch (entry)
            {
                case "add":
                    VmAutomationPrefabComponentCommands.AddComponentDeferred(
                        arguments, complete, progress);
                    break;
                case "configure":
                    VmAutomationPrefabTransactionCommands.ConfigureComponentDeferred(
                        arguments, complete, progress);
                    break;
                case "transaction":
                    VmAutomationPrefabTransactionCommands.TransactionEditDeferred(
                        arguments, complete, progress);
                    break;
            }
            Assert.That(completionCount, Is.EqualTo(1));
            Assert.That(progressCount, Is.Zero);
            AssertMissingType(response);
        }

        [TestCase("add")]
        [TestCase("configure")]
        [TestCase("transaction")]
        public void ImmediateEntryUsesTheSameAdmission(string entry)
        {
            var arguments = Arguments(entry);
            object response = entry == "add"
                ? VmAutomationPrefabComponentCommands.AddComponent(arguments)
                : entry == "configure"
                    ? VmAutomationPrefabTransactionCommands.ConfigureComponent(arguments)
                    : VmAutomationPrefabTransactionCommands.TransactionEdit(arguments);
            AssertMissingType(response);
        }

        [Test]
        public void ReferencedComponentTypesAreAdmittedBeforeTransaction()
        {
            var arguments = Arguments("configure");
            arguments["componentType"] = "UnityEngine.Transform";
            arguments["references"] = new List<object>
            {
                new Dictionary<string, object>
                {
                    { "propertyName", "reference" },
                    { "referencePrefabPath", "" },
                    { "referenceComponentType", MissingType }
                }
            };
            AssertMissingType(VmAutomationPrefabTransactionCommands.ConfigureComponent(arguments));
        }

        [Test]
        public void ComponentContractsNoLongerAdvertiseImplicitImportOrTypeWaitOptions()
        {
            foreach (string route in new[] { "prefab-asset/add-component",
                         "prefab-asset/configure-component", "prefab-asset/transaction-edit" })
            {
                Assert.That(VmAutomationCatalog.TryGetTool(route, true, out var tool), Is.True);
                var schema = (Dictionary<string, object>)tool["inputSchema"];
                var properties = (Dictionary<string, object>)schema["properties"];
                foreach (string option in new[] { "refreshAssets", "waitForType", "waitForTypes",
                             "typeResolveTimeoutMs", "typeResolveStableMs" })
                    Assert.That(properties.ContainsKey(option), Is.False, route + ": " + option);
            }
        }

        [TestCase("add")]
        [TestCase("configure")]
        [TestCase("transaction")]
        public void ImportedTypesPersistThroughDeferredEntryAndReopen(string entry)
        {
            var active = SceneManager.GetActiveScene();
            bool dirty = active.isDirty;
            int sceneCount = SceneManager.sceneCount;
            string path = AssetDatabase.GenerateUniqueAssetPath("Assets/Component Admission Test.prefab");
            var preview = EditorSceneManager.NewPreviewScene();
            GameObject root = null;
            try
            {
                root = new GameObject("Component Admission Test");
                SceneManager.MoveGameObjectToScene(root, preview);
                Assert.That(PrefabUtility.SaveAsPrefabAsset(root, path), Is.Not.Null);
                UnityEngine.Object.DestroyImmediate(root);
                root = null;
                var arguments = new Dictionary<string, object>
                {
                    { "assetPath", path }, { "componentType", typeof(Rigidbody2D).FullName },
                    { "properties", new Dictionary<string, object> { { "m_GravityScale", 3.25f } } },
                    { "execution", new Dictionary<string, object> { { "mode", "immediate" } } }
                };
                if (entry == "transaction")
                {
                    arguments.Remove("componentType");
                    arguments.Remove("properties");
                    arguments["operations"] = new List<object>
                    {
                        new Dictionary<string, object>
                        {
                            { "type", "addComponent" }, { "componentType", typeof(Rigidbody2D).FullName },
                            { "properties", new Dictionary<string, object> { { "m_GravityScale", 3.25f } } }
                        }
                    };
                }
                object response = null;
                Action<object> complete = value => response = value;
                switch (entry)
                {
                    case "add":
                        VmAutomationPrefabComponentCommands.AddComponentDeferred(arguments, complete, null);
                        break;
                    case "configure":
                        VmAutomationPrefabTransactionCommands.ConfigureComponentDeferred(arguments, complete, null);
                        break;
                    case "transaction":
                        VmAutomationPrefabTransactionCommands.TransactionEditDeferred(arguments, complete, null);
                        break;
                }
                Assert.That(response, Is.InstanceOf<Dictionary<string, object>>());
                Assert.That(((Dictionary<string, object>)response)["success"], Is.True);
                var reopened = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    Assert.That(reopened.GetComponents<Rigidbody2D>(), Has.Length.EqualTo(1));
                    Assert.That(reopened.GetComponent<Rigidbody2D>().gravityScale, Is.EqualTo(3.25f));
                }
                finally { PrefabUtility.UnloadPrefabContents(reopened); }
                Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(active));
                Assert.That(active.isDirty, Is.EqualTo(dirty));
                Assert.That(SceneManager.sceneCount, Is.EqualTo(sceneCount));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                EditorSceneManager.ClosePreviewScene(preview);
                AssetDatabase.DeleteAsset(path);
            }
        }

        [TestCase(null)]
        [TestCase(false)]
        [TestCase(true)]
        public void PropertyDiscoveryReadsDisabledNativeStateWithoutSaving(bool? includeHidden)
        {
            var active = SceneManager.GetActiveScene();
            bool dirty = active.isDirty;
            int sceneCount = SceneManager.sceneCount;
            string path = AssetDatabase.GenerateUniqueAssetPath("Assets/Prefab Property Discovery Test.prefab");
            var preview = EditorSceneManager.NewPreviewScene();
            GameObject root = null;
            try
            {
                root = new GameObject("Prefab Property Discovery Test");
                SceneManager.MoveGameObjectToScene(root, preview);
                root.AddComponent<MeshRenderer>().enabled = false;
                Assert.That(PrefabUtility.SaveAsPrefabAsset(root, path), Is.Not.Null);
                UnityEngine.Object.DestroyImmediate(root);
                root = null;
                byte[] before = File.ReadAllBytes(path);
                var arguments = new Dictionary<string, object>
                {
                    { "assetPath", path }, { "componentType", typeof(MeshRenderer).FullName }
                };
                if (includeHidden.HasValue)
                    arguments["includeHidden"] = includeHidden.Value;
                var response = (Dictionary<string, object>)
                    VmAutomationPrefabComponentCommands.GetComponentProperties(arguments);
                var properties = (List<Dictionary<string, object>>)response["properties"];
                Assert.That(properties.All(p => (string)p["propertyPath"] == (string)p["name"]), Is.True);
                Assert.That(properties.Any(p => (string)p["propertyPath"] == "m_ObjectHideFlags"),
                    Is.EqualTo(includeHidden == true));
                if (includeHidden == true)
                    Assert.That(properties.Single(p => (string)p["propertyPath"] == "m_Enabled")["value"], Is.False);
                Assert.That(File.ReadAllBytes(path), Is.EqualTo(before));
                var reopened = PrefabUtility.LoadPrefabContents(path);
                try { Assert.That(reopened.GetComponent<MeshRenderer>().enabled, Is.False); }
                finally { PrefabUtility.UnloadPrefabContents(reopened); }
                Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(active));
                Assert.That(active.isDirty, Is.EqualTo(dirty));
                Assert.That(SceneManager.sceneCount, Is.EqualTo(sceneCount));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                EditorSceneManager.ClosePreviewScene(preview);
                AssetDatabase.DeleteAsset(path);
            }
        }

        [Test]
        public void PrefabPropertyDiscoveryPublishesHiddenSelectionAndExactPaths()
        {
            Assert.That(VmAutomationCatalog.TryGetTool("prefab-asset/get-properties", true, out var tool), Is.True);
            var input = (Dictionary<string, object>)tool["inputSchema"];
            var fields = (Dictionary<string, object>)input["properties"];
            Assert.That(((Dictionary<string, object>)fields["includeHidden"])["type"], Is.EqualTo("boolean"));
            var index = (Dictionary<string, object>)fields["componentIndex"];
            Assert.That(index["type"], Is.EqualTo("integer"));
            Assert.That(index["minimum"], Is.EqualTo(0));
            Assert.That(index["default"], Is.EqualTo(0));
            var output = (Dictionary<string, object>)tool["outputSchema"];
            var outputFields = (Dictionary<string, object>)output["properties"];
            var propertyArray = (Dictionary<string, object>)outputFields["properties"];
            var record = (Dictionary<string, object>)propertyArray["items"];
            var recordFields = (Dictionary<string, object>)record["properties"];
            Assert.That(((Dictionary<string, object>)recordFields["propertyPath"])["type"], Is.EqualTo("string"));
            Assert.That(((Dictionary<string, object>)outputFields["componentIndex"])["type"], Is.EqualTo("integer"));
            Assert.That(((Dictionary<string, object>)outputFields["componentCount"])["type"], Is.EqualTo("integer"));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RepeatedPrefabComponentsReadExactIndicesWithoutChangingAssets(bool includeHidden)
        {
            var active = SceneManager.GetActiveScene();
            bool dirty = active.isDirty;
            int sceneCount = SceneManager.sceneCount;
            string path = AssetDatabase.GenerateUniqueAssetPath("Assets/Repeated Component Read Test.prefab");
            var preview = EditorSceneManager.NewPreviewScene();
            GameObject root = null;
            try
            {
                root = new GameObject("Repeated Component Read Test");
                SceneManager.MoveGameObjectToScene(root, preview);
                root.AddComponent<BoxCollider2D>().isTrigger = false;
                var second = root.AddComponent<BoxCollider2D>();
                second.isTrigger = true;
                second.enabled = false;
                Assert.That(PrefabUtility.SaveAsPrefabAsset(root, path), Is.Not.Null);
                UnityEngine.Object.DestroyImmediate(root);
                root = null;
                byte[] before = File.ReadAllBytes(path);
                var arguments = new Dictionary<string, object>
                {
                    { "assetPath", path }, { "componentType", typeof(BoxCollider2D).FullName },
                    { "includeHidden", includeHidden }
                };
                for (int index = 0; index < 2; index++)
                {
                    arguments["componentIndex"] = index;
                    var result = (Dictionary<string, object>)
                        VmAutomationPrefabComponentCommands.GetComponentProperties(arguments);
                    Assert.That(result["componentIndex"], Is.EqualTo(index));
                    Assert.That(result["componentCount"], Is.EqualTo(2));
                    var properties = (List<Dictionary<string, object>>)result["properties"];
                    Assert.That(properties.Single(p => (string)p["propertyPath"] == "m_IsTrigger")["value"],
                        Is.EqualTo(index == 1));
                    if (includeHidden)
                        Assert.That(properties.Single(p => (string)p["propertyPath"] == "m_Enabled")["value"],
                            Is.EqualTo(index == 0));
                }
                arguments.Remove("componentIndex");
                var omitted = (Dictionary<string, object>)
                    VmAutomationPrefabComponentCommands.GetComponentProperties(arguments);
                Assert.That(omitted["componentIndex"], Is.EqualTo(0));

                foreach (int invalidIndex in new[] { -1, 2 })
                {
                    arguments["componentIndex"] = invalidIndex;
                    var failure = (Dictionary<string, object>)
                        VmAutomationPrefabComponentCommands.GetComponentProperties(arguments);
                    Assert.That(failure["success"], Is.False);
                    Assert.That(failure["errorCode"], Is.EqualTo(
                        invalidIndex < 0 ? "invalid_arguments" : "component_not_found"));
                    Assert.That(failure.ContainsKey("properties"), Is.False);
                }

                var findArguments = new Dictionary<string, object>
                {
                    { "assetPath", path }, { "propertyName", "m_IsTrigger" }, { "propertyValue", true }
                };
                foreach (bool typed in new[] { false, true })
                {
                    if (typed) findArguments["componentType"] = typeof(BoxCollider2D).FullName;
                    var found = (Dictionary<string, object>)VmAutomationPrefabAssetCommands.Find(findArguments);
                    var matches = (List<Dictionary<string, object>>)found["results"];
                    Assert.That(matches, Has.Count.EqualTo(1));
                    Assert.That(matches[0]["componentIndex"], Is.EqualTo(1));
                }
                Assert.That(File.ReadAllBytes(path), Is.EqualTo(before));
                Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(active));
                Assert.That(active.isDirty, Is.EqualTo(dirty));
                Assert.That(SceneManager.sceneCount, Is.EqualTo(sceneCount));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                EditorSceneManager.ClosePreviewScene(preview);
                AssetDatabase.DeleteAsset(path);
            }
        }

        private static Dictionary<string, object> Arguments(string entry)
        {
            var arguments = new Dictionary<string, object>
            {
                { "assetPath", AssetPath }, { "componentType", MissingType }
            };
            if (entry == "transaction")
            {
                arguments.Remove("componentType");
                arguments["operations"] = new List<object>
                {
                    new Dictionary<string, object>
                    {
                        { "type", "addComponent" }, { "componentType", MissingType }
                    }
                };
            }
            return arguments;
        }

        private static void AssertMissingType(object response)
        {
            var result = (Dictionary<string, object>)response;
            Assert.That(result["success"], Is.False);
            Assert.That(result["errorCode"], Is.EqualTo("component_type_not_found"));
            Assert.That(result["componentType"], Is.EqualTo(MissingType));
            Assert.That(result["stage"], Is.EqualTo("component-type-admission"));
        }
    }
}
