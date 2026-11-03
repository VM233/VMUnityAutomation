using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;

namespace VMUnityAutomation.Editor.Tests
{
    [Category(VmAutomationPackageTestCommands.DefaultPackageSmokeCategory)]
    [Category(VmAutomationPackageTestCommands.FullPackageRegressionCategory)]
    public sealed class VmTestPlayModeOptionsTests
    {
        [TestCase(true, false, false)]
        [TestCase(true, true, false)]
        [TestCase(true, false, true)]
        [TestCase(true, true, true)]
        [TestCase(false, false, false)]
        public void CommandPublishesNativeOptionsAndVerifiesTheSavedOwner(
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

        [TestCase(false)]
        [TestCase(true)]
        public void GuardPreservesUnitySemanticsAndRestoresOriginalSettings(bool originallyEnabled)
        {
            bool previousEnabled = EditorSettings.enterPlayModeOptionsEnabled;
            EnterPlayModeOptions previousOptions = EditorSettings.enterPlayModeOptions;
            const EnterPlayModeOptions dormantOptions = EnterPlayModeOptions.DisableSceneReload;
            try
            {
                EditorSettings.enterPlayModeOptionsEnabled = originallyEnabled;
                EditorSettings.enterPlayModeOptions = dormantOptions;
                VmAutomationTestRunnerCommands.SaveAndDisableDomainReload();
                Assert.That(EditorSettings.enterPlayModeOptionsEnabled, Is.True);
                var expected = EnterPlayModeOptions.DisableDomainReload |
                               (originallyEnabled ? dormantOptions : EnterPlayModeOptions.None);
                Assert.That(EditorSettings.enterPlayModeOptions, Is.EqualTo(expected));
                VmAutomationTestRunnerCommands.RestorePlayModeOptions();
                Assert.That(EditorSettings.enterPlayModeOptionsEnabled, Is.EqualTo(originallyEnabled));
                Assert.That(EditorSettings.enterPlayModeOptions, Is.EqualTo(dormantOptions));
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
