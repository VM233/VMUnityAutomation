using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace VMUnityAutomation.Editor.Tests
{
    [Category(VmAutomationPackageTestCommands.DefaultPackageSmokeCategory)]
    [Category(VmAutomationPackageTestCommands.FullPackageRegressionCategory)]
    public sealed class VmTestPlayModeOptionsTests
    {
        [TestCase(true, true, false)]
        [TestCase(true, false, true)]
        [TestCase(true, true, true)]
        [TestCase(false, false, false)]
        public void CommandPublishesSupportedOptionsAndVerifiesTheSavedOwner(
            bool enabled, bool disableDomainReload, bool disableSceneReload)
        {
            bool previousEnabled = EditorSettings.enterPlayModeOptionsEnabled;
            EnterPlayModeOptions previousOptions = EditorSettings.enterPlayModeOptions;
            try
            {
                var response = (Dictionary<string, object>)VmAutomationPlayModeOptionsCommands.Execute(
                    new Dictionary<string, object>
                    {
                        { "enabled", enabled },
                        { "disableDomainReload", disableDomainReload },
                        { "disableSceneReload", disableSceneReload }
                    });
                Assert.That(response.ContainsKey("error"), Is.False,
                    "The exact native/persisted readback must pass; transport success is insufficient.");
                var current = (Dictionary<string, object>)response["current"];
                var expected = EnterPlayModeOptions.None;
                if (disableDomainReload) expected |= EnterPlayModeOptions.DisableDomainReload;
                if (disableSceneReload) expected |= EnterPlayModeOptions.DisableSceneReload;
                Assert.That(EditorSettings.enterPlayModeOptionsEnabled, Is.EqualTo(enabled));
                Assert.That(EditorSettings.enterPlayModeOptions, Is.EqualTo(expected));
                Assert.That(current["enabled"], Is.EqualTo(enabled));
                Assert.That(current["optionsValue"], Is.EqualTo((int)expected));
            }
            finally
            {
                VmAutomationPlayModeOptionsCommands.Execute(new Dictionary<string, object>
                {
                    { "enabled", previousEnabled },
                    { "disableDomainReload", previousEnabled && (previousOptions & EnterPlayModeOptions.DisableDomainReload) != 0 },
                    { "disableSceneReload", previousEnabled && (previousOptions & EnterPlayModeOptions.DisableSceneReload) != 0 }
                });
            }
        }

        [Test]
        public void EnabledWithoutAnySkippedReloadIsRejectedBeforeMutation()
        {
            bool previousEnabled = EditorSettings.enterPlayModeOptionsEnabled;
            EnterPlayModeOptions previousOptions = EditorSettings.enterPlayModeOptions;
            string path = Path.Combine(Directory.GetParent(Application.dataPath).FullName,
                "ProjectSettings/EditorSettings.asset");
            byte[] before = File.ReadAllBytes(path);
            var result = (Dictionary<string, object>)VmAutomationPlayModeOptionsCommands.Execute(
                new Dictionary<string, object>
                {
                    { "enabled", true }, { "disableDomainReload", false }, { "disableSceneReload", false }
                });
            Assert.That(result["errorCode"], Is.EqualTo("invalid_arguments"));
            Assert.That(EditorSettings.enterPlayModeOptionsEnabled, Is.EqualTo(previousEnabled));
            Assert.That(EditorSettings.enterPlayModeOptions, Is.EqualTo(previousOptions));
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(before));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void GuardPreservesUnitySemanticsAndRestoresOriginalSettings(bool originallyEnabled)
        {
            bool previousEnabled = EditorSettings.enterPlayModeOptionsEnabled;
            EnterPlayModeOptions previousOptions = EditorSettings.enterPlayModeOptions;
            EnterPlayModeOptions originalOptions = originallyEnabled
                ? EnterPlayModeOptions.DisableSceneReload
                : EnterPlayModeOptions.None;
            try
            {
                EditorSettings.enterPlayModeOptionsEnabled = originallyEnabled;
                EditorSettings.enterPlayModeOptions = originalOptions;
                Assert.That(EditorSettings.enterPlayModeOptionsEnabled, Is.EqualTo(originallyEnabled));
                VmAutomationTestRunnerCommands.SaveAndDisableDomainReload();
                Assert.That(EditorSettings.enterPlayModeOptionsEnabled, Is.True);
                var expected = EnterPlayModeOptions.DisableDomainReload |
                               originalOptions;
                Assert.That(EditorSettings.enterPlayModeOptions, Is.EqualTo(expected));
                VmAutomationTestRunnerCommands.RestorePlayModeOptions();
                Assert.That(EditorSettings.enterPlayModeOptionsEnabled, Is.EqualTo(originallyEnabled));
                Assert.That(EditorSettings.enterPlayModeOptions, Is.EqualTo(originalOptions));
            }
            finally
            {
                VmAutomationTestRunnerCommands.RestorePlayModeOptions();
                EditorSettings.enterPlayModeOptionsEnabled = previousEnabled;
                EditorSettings.enterPlayModeOptions = previousOptions;
            }
        }
    }
}
