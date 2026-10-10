#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEditor;
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
            AssetDatabase.ImportAsset(LayoutPath, ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset(StylePath, ImportAssetOptions.ForceUpdate);
            yield return AwaitRun(before + 1);
            Assert.That((string[])Status()["lastPaths"], Is.EqualTo(new[] { StylePath }));
            Assert.That(Status()["lastErrorCount"], Is.EqualTo(0));
            Assert.That(Status()["lastWarningCount"], Is.EqualTo(0));

            WriteStyle(6);
            AssetDatabase.ImportAsset(StylePath, ImportAssetOptions.ForceUpdate);
            yield return AwaitRun(before + 2);
            AssetDatabase.ImportAsset(StylePath, ImportAssetOptions.ForceUpdate);
            for (int frame = 0; frame < 20; frame++) yield return null;
            Assert.That(RunCount(), Is.EqualTo(before + 2), "An unchanged import must not rerun the audit.");

            Configure(false);
            WriteStyle(9);
            AssetDatabase.ImportAsset(StylePath, ImportAssetOptions.ForceUpdate);
            for (int frame = 0; frame < 20; frame++) yield return null;
            Assert.That(RunCount(), Is.EqualTo(before + 2), "Disabled imports must not run an audit.");

            Configure(true);
            WriteStyle(12);
            AssetDatabase.ImportAsset(StylePath, ImportAssetOptions.ForceUpdate);
            yield return AwaitRun(before + 3);
            Assert.That(Status()["lastErrorCount"], Is.EqualTo(0));
            Assert.That(Status()["lastWarningCount"], Is.EqualTo(0));
        }

        private static System.Collections.Generic.Dictionary<string, object> Status() =>
            VmAutomationUIToolkitAutomaticAuditCoordinator.GetStatus(".uss");

        private static int RunCount() => Convert.ToInt32(Status()["runCount"]);

        private static IEnumerator AwaitRun(int expected)
        {
            for (int frame = 0; frame < 240; frame++)
            {
                if (RunCount() >= expected) yield break;
                yield return null;
            }
            Assert.Fail("The native import did not reach its automatic audit owner.");
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
