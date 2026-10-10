using NUnit.Framework;
using UnityEditor;

namespace VMUnityAutomation.Editor.Tests
{
    [Category(VmAutomationPackageTestCommands.DefaultPackageSmokeCategory)]
    [Category(VmAutomationPackageTestCommands.FullPackageRegressionCategory)]
    public sealed class VmTestPlayModeOptionsTests
    {
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
