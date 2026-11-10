#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.TestTools;

namespace VMUnityAutomation.Editor.Tests
{
    public sealed class VmAutomationAutomaticAuditImportTests
    {
        private const string Root = "Assets/VM Unity Automation Audit Import Tests";
        private const string StylePath = Root + "/Audit Style.uss";
        private const string LayoutPath = Root + "/Audit Layout.uxml";
        private string configurationPath;
        private byte[] originalConfiguration;
        private bool folderOwned;
        private bool configurationOwned;

        [SetUp]
        public void CreateFixture()
        {
            Assert.That(Directory.Exists(Root), Is.False, "The fixture folder must not pre-exist.");
            configurationPath = Path.Combine(VmAutomationUIToolkitAuditUtility.GetProjectRoot(),
                VmAutomationUIToolkitAuditProjectSettings.ConfigPath);
            if (File.Exists(configurationPath))
            {
                Assert.That(new FileInfo(configurationPath).Length, Is.LessThanOrEqualTo(65536));
                originalConfiguration = File.ReadAllBytes(configurationPath);
            }
            AssetDatabase.CreateFolder("Assets", "VM Unity Automation Audit Import Tests");
            folderOwned = true;
            configurationOwned = true;
            Configure(true);
        }

        [TearDown]
        public void RetireFixture()
        {
            if (folderOwned)
                Assert.That(AssetDatabase.DeleteAsset(Root), Is.True);
            if (configurationOwned)
            {
                if (originalConfiguration == null)
                    File.Delete(configurationPath);
                else
                    File.WriteAllBytes(configurationPath, originalConfiguration);
            }
        }

        [UnityTest]
        public IEnumerator ImportedAssetsDriveAuditsAcrossUpdatesAndEnableChanges()
        {
            int before = RunCount();
            Assert.That(Status()["changeSource"], Is.EqualTo("asset-import"));
            Assert.That(Status().ContainsKey("watcherActive"), Is.False);
            File.WriteAllText(LayoutPath,
                "<ui:UXML xmlns:ui=\"UnityEngine.UIElements\"><Style src=\"Audit Style.uss\"/>" +
                "<ui:VisualElement class=\"shared-entry\"/><ui:VisualElement class=\"shared-entry\"/></ui:UXML>\n");
            WriteStyle(3);
            AssetDatabase.ImportAsset(StylePath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            AssetDatabase.ImportAsset(LayoutPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            yield return AwaitRun(before + 1);
            Assert.That((string[])Status()["lastPaths"], Is.EqualTo(new[] { StylePath }));
            Assert.That(Status()["lastErrorCount"], Is.EqualTo(0));
            Assert.That(Status()["lastWarningCount"], Is.EqualTo(0));

            WriteStyle(6);
            AssetDatabase.ImportAsset(StylePath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            yield return AwaitRun(before + 2);
            AssetDatabase.ImportAsset(StylePath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            for (int frame = 0; frame < 20; frame++) yield return null;
            Assert.That(RunCount(), Is.EqualTo(before + 2), "An unchanged import must not rerun the audit.");

            Configure(false);
            WriteStyle(9);
            AssetDatabase.ImportAsset(StylePath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            for (int frame = 0; frame < 20; frame++) yield return null;
            Assert.That(RunCount(), Is.EqualTo(before + 2), "Disabled imports must not run an audit.");

            Configure(true);
            WriteStyle(12);
            AssetDatabase.ImportAsset(StylePath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            yield return AwaitRun(before + 3);
            Assert.That(Status()["lastErrorCount"], Is.EqualTo(0));
            Assert.That(Status()["lastWarningCount"], Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator PrefabImpactRetainsBindingsAndTheirRemovalWithoutAuditingPureGeometry()
        {
            var settings = new VmAutomationUIToolkitAuditProjectSettings
            {
                AutomaticUssSingleUseStyles = false,
                AutomaticUxmlLayoutContracts = true,
                UxmlLocalizedText = false
            };
            settings.AssetRoots.Clear(); settings.AssetRoots.Add(Root);
            settings.RuntimeSourceRoots.Clear(); settings.RuntimeSourceRoots.Add(Root);
            settings.Save();
            int initialLayoutRuns = LayoutRunCount();
            File.WriteAllText(LayoutPath,
                "<ui:UXML xmlns:ui=\"UnityEngine.UIElements\"><ui:VisualElement/></ui:UXML>\n");
            AssetDatabase.ImportAsset(LayoutPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            yield return AwaitLayoutRun(initialLayoutRuns + 1);
            string prefabPath = Root + "/Stock Box.prefab";
            var fixture = new GameObject("Stock Box");
            try
            {
                fixture.AddComponent<BoxCollider>();
                PrefabUtility.SaveAsPrefabAsset(fixture, prefabPath);
            }
            finally { UnityEngine.Object.DestroyImmediate(fixture); }
            for (int frame = 0; frame < 30; frame++) yield return null;
            int before = LayoutRunCount();
            var contents = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                contents.GetComponent<BoxCollider>().center = new Vector3(1, 2, 3);
                PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
            for (int frame = 0; frame < 30; frame++) yield return null;
            Assert.That(LayoutRunCount(), Is.EqualTo(before), "Pure stock bounds must not audit all layouts.");

            contents = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                contents.AddComponent<UIDocument>().visualTreeAsset =
                    AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(LayoutPath);
                PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
            yield return AwaitLayoutRun(before + 1);
            before = LayoutRunCount();
            contents = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                UnityEngine.Object.DestroyImmediate(contents.GetComponent<UIDocument>());
                PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
            yield return AwaitLayoutRun(before + 1);

            before = LayoutRunCount();
            contents = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                contents.AddComponent<UIDocument>().visualTreeAsset =
                    AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(LayoutPath);
                PrefabUtility.SaveAsPrefabAsset(contents, prefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
            yield return AwaitLayoutRun(before + 1);
            before = LayoutRunCount();
            string movedPath = Root + "/Moved Panel.prefab";
            Assert.That(AssetDatabase.MoveAsset(prefabPath, movedPath), Is.Empty);
            yield return AwaitLayoutRun(before + 1);
            before = LayoutRunCount();
            Assert.That(AssetDatabase.DeleteAsset(movedPath), Is.True);
            yield return AwaitLayoutRun(before + 1);
        }

        [Test]
        public void ReusedNameIndexRetiresOnlyReferencesAndPreservesDefinitions()
        {
            var names = new VmAutomationUxmlLayoutModels.UxmlElementNameReferenceIndex(true);
            names.AddDefinition("InventoryList"); names.AddDefinition("ActionLabel");
            VmAutomationUxmlLayoutContractIndexer.IndexTextNameReferences(
                "  names:\n    - InventoryList\n", names, true);
            Assert.That(names.HasReferences, Is.True);
            Assert.That(names.IsReferenced("InventoryList"), Is.True);
            names.ClearReferences();
            VmAutomationUxmlLayoutContractIndexer.IndexTextNameReferences(
                "  m_Center: {x: 1, y: 2, z: 3}\n", names, true);
            Assert.That(names.HasReferences, Is.False);
            Assert.That(names.DefinitionCount, Is.EqualTo(2));
            VmAutomationUxmlLayoutContractIndexer.IndexTextNameReferences(
                "  target: \"ActionLabel\"\n", names, true);
            Assert.That(names.IsReferenced("ActionLabel"), Is.True);
            Assert.That(names.IsReferenced("InventoryList"), Is.False);
        }

        private static int LayoutRunCount() => Convert.ToInt32(
            VmAutomationUIToolkitAutomaticAuditCoordinator.GetStatus(".uxml")["runCount"]);

        private static IEnumerator AwaitLayoutRun(int expected)
        {
            double started = EditorApplication.timeSinceStartup;
            for (int frame = 0; frame < 240; frame++)
            {
                if (LayoutRunCount() >= expected) yield break;
                yield return null;
            }
            Assert.Fail("The native prefab import did not reach its automatic layout audit owner. " +
                "elapsed=" + (EditorApplication.timeSinceStartup - started) + "; updating=" + EditorApplication.isUpdating +
                "; status=" + MiniJson.Serialize(VmAutomationUIToolkitAutomaticAuditCoordinator.GetStatus(".uxml")));
        }

        private static System.Collections.Generic.Dictionary<string, object> Status() =>
            VmAutomationUIToolkitAutomaticAuditCoordinator.GetStatus(".uss");

        private static int RunCount() => Convert.ToInt32(Status()["runCount"]);

        private static IEnumerator AwaitRun(int expected)
        {
            double started = EditorApplication.timeSinceStartup;
            for (int frame = 0; frame < 240; frame++)
            {
                if (RunCount() >= expected) yield break;
                yield return null;
            }
            Assert.Fail("The native import did not reach its automatic audit owner. " +
                "elapsed=" + (EditorApplication.timeSinceStartup - started) + "; updating=" + EditorApplication.isUpdating +
                "; status=" + MiniJson.Serialize(Status()));
        }

        private static void WriteStyle(int margin) =>
            File.WriteAllText(StylePath, ".shared-entry { margin-left: " + margin + "px; }\n");

        private static void Configure(bool enabled)
        {
            var settings = new VmAutomationUIToolkitAuditProjectSettings
            {
                AutomaticUssSingleUseStyles = enabled,
                AutomaticUxmlLayoutContracts = false
            };
            settings.AssetRoots.Clear();
            settings.AssetRoots.Add(Root);
            settings.RuntimeSourceRoots.Clear();
            settings.RuntimeSourceRoots.Add(Root);
            settings.Save();
        }
    }
}
#endif
