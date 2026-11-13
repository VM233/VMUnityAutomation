using System;
using System.Reflection;
using NUnit.Framework;

namespace VMUnityAutomation.Editor.Tests
{
    [Category(VmAutomationPackageTestCommands.DefaultPackageSmokeCategory)]
    [Category(VmAutomationPackageTestCommands.FullPackageRegressionCategory)]
    public sealed class VmAddressablesBuildPresentationTests
    {
        private static class ReportConfig
        {
            public static bool UserHasBeenInformedAboutBuildReportSettingPreBuild { get; set; }
            public static bool AutoOpenAddressablesReport { get; set; }
        }

        private static class FailingReportConfig
        {
            public static bool UserHasBeenInformedAboutBuildReportSettingPreBuild { get; set; }
            public static bool AutoOpenAddressablesReport
            {
                get => true;
                set { if (!value) throw new InvalidOperationException("Presentation write failed."); }
            }
        }

        [Test]
        public void FailedPresentationWriteRestoresTheEarlierNativeSetting()
        {
            FailingReportConfig.UserHasBeenInformedAboutBuildReportSettingPreBuild = false;
            Assert.Throws<TargetInvocationException>(() => VmAddressablesBuildPresentation.Begin(typeof(FailingReportConfig)));
            Assert.That(FailingReportConfig.UserHasBeenInformedAboutBuildReportSettingPreBuild, Is.False);
            Assert.That(FailingReportConfig.AutoOpenAddressablesReport, Is.True);
        }

        [Test]
        public void InstalledNativeReportSettingsAreRestoredAfterColdPresentationScope()
        {
            if (!VmAutomationCapabilityRegistry.IsCapabilityAvailable("addressables"))
                Assert.Ignore("Addressables is not installed.");
            Type config = VmAutomationAssetGraphUtility.FindType("UnityEditor.AddressableAssets.Settings.ProjectConfigData");
            var informed = config.GetProperty("UserHasBeenInformedAboutBuildReportSettingPreBuild", BindingFlags.Static | BindingFlags.NonPublic);
            if (informed == null)
                Assert.Ignore("The installed Addressables has no first-build report notification.");
            var autoOpen = config.GetProperty("AutoOpenAddressablesReport", BindingFlags.Static | BindingFlags.NonPublic);
            bool originalInformed = (bool)informed.GetValue(null);
            bool originalAutoOpen = (bool)autoOpen.GetValue(null);
            try
            {
                informed.SetValue(null, false);
                autoOpen.SetValue(null, true);
                using (VmAddressablesBuildPresentation.BeginIfAvailable())
                {
                    Assert.That(informed.GetValue(null), Is.True);
                    Assert.That(autoOpen.GetValue(null), Is.False);
                }
                Assert.That(informed.GetValue(null), Is.False);
                Assert.That(autoOpen.GetValue(null), Is.True);
            }
            finally
            {
                informed.SetValue(null, originalInformed);
                autoOpen.SetValue(null, originalAutoOpen);
            }
        }

        [TestCase(false, false)]
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public void ColdAndPreviouslyAcknowledgedBuildsDoNotPresentUiAndRestoreSettings(bool informed, bool autoOpen)
        {
            ReportConfig.UserHasBeenInformedAboutBuildReportSettingPreBuild = informed;
            ReportConfig.AutoOpenAddressablesReport = autoOpen;
            using (VmAddressablesBuildPresentation.Begin(typeof(ReportConfig)))
            {
                Assert.That(ReportConfig.UserHasBeenInformedAboutBuildReportSettingPreBuild, Is.True,
                    "An unacknowledged native report notification blocks synchronous builds.");
                Assert.That(ReportConfig.AutoOpenAddressablesReport, Is.False,
                    "An automated build must not create a report window.");
            }
            Assert.That(ReportConfig.UserHasBeenInformedAboutBuildReportSettingPreBuild, Is.EqualTo(informed));
            Assert.That(ReportConfig.AutoOpenAddressablesReport, Is.EqualTo(autoOpen));
        }

        [Test]
        public void NativeBuildFailureStillRestoresReportPresentation()
        {
            ReportConfig.UserHasBeenInformedAboutBuildReportSettingPreBuild = false;
            ReportConfig.AutoOpenAddressablesReport = true;
            Assert.Throws<InvalidOperationException>(() =>
            {
                using var scope = VmAddressablesBuildPresentation.Begin(typeof(ReportConfig));
                throw new InvalidOperationException("Native build failed.");
            });
            Assert.That(ReportConfig.UserHasBeenInformedAboutBuildReportSettingPreBuild, Is.False);
            Assert.That(ReportConfig.AutoOpenAddressablesReport, Is.True);
        }
    }
}
