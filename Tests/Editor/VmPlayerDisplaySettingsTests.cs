using NUnit.Framework;
using UnityEditor;

namespace VMUnityAutomation.Editor.Tests
{
    [Category(VmAutomationPackageTestCommands.DefaultPackageSmokeCategory)]
    [Category(VmAutomationPackageTestCommands.FullPackageRegressionCategory)]
    public sealed class VmPlayerDisplaySettingsTests
    {
        private readonly VmPlayerDisplaySettingsTool tool = new();

        [Test]
        public void StateReadsNativeValues()
        {
            var result = tool.Execute(new VmPlayerDisplaySettingsRequest
                { Action = VmPlayerDisplaySettingsAction.State });
            Assert.That(result.DefaultInterfaceOrientation, Is.EqualTo(PlayerSettings.defaultInterfaceOrientation));
            Assert.That(result.DefaultScreenWidth, Is.EqualTo(PlayerSettings.defaultScreenWidth));
            Assert.That(result.DefaultScreenHeight, Is.EqualTo(PlayerSettings.defaultScreenHeight));
        }

        [Test]
        public void PortraitConfigurationReportsAppliedNativeValuesAndPreservesOmittedFlags()
        {
            var before = tool.Execute(new VmPlayerDisplaySettingsRequest
                { Action = VmPlayerDisplaySettingsAction.State });
            try
            {
                var result = tool.Execute(new VmPlayerDisplaySettingsRequest
                {
                    Action = VmPlayerDisplaySettingsAction.Configure,
                    DefaultInterfaceOrientation = UIOrientation.Portrait,
                    DefaultScreenWidth = 1080,
                    DefaultScreenHeight = 1920
                });
                Assert.That(result.DefaultInterfaceOrientation, Is.EqualTo(UIOrientation.Portrait));
                Assert.That(result.DefaultScreenWidth, Is.EqualTo(1080));
                Assert.That(result.DefaultScreenHeight, Is.EqualTo(1920));
                Assert.That(result.AllowedAutorotateToLandscapeLeft, Is.EqualTo(before.AllowedAutorotateToLandscapeLeft));
                Assert.That(result.AllowedAutorotateToLandscapeRight, Is.EqualTo(before.AllowedAutorotateToLandscapeRight));
                Assert.That(PlayerSettings.defaultInterfaceOrientation, Is.EqualTo(UIOrientation.Portrait));
                Assert.That(PlayerSettings.defaultScreenWidth, Is.EqualTo(1080));
                Assert.That(PlayerSettings.defaultScreenHeight, Is.EqualTo(1920));
            }
            finally
            {
                PlayerSettings.defaultInterfaceOrientation = before.DefaultInterfaceOrientation;
                PlayerSettings.defaultScreenWidth = before.DefaultScreenWidth;
                PlayerSettings.defaultScreenHeight = before.DefaultScreenHeight;
                AssetDatabase.SaveAssets();
            }
        }

        [TestCase(0)]
        [TestCase(-1)]
        public void InvalidDimensionsRejectBeforeChangingOrientation(int width)
        {
            var before = PlayerSettings.defaultInterfaceOrientation;
            Assert.Throws<VmProjectToolException>(() => tool.Execute(new VmPlayerDisplaySettingsRequest
            {
                Action = VmPlayerDisplaySettingsAction.Configure,
                DefaultInterfaceOrientation = UIOrientation.Portrait,
                DefaultScreenWidth = width
            }));
            Assert.That(PlayerSettings.defaultInterfaceOrientation, Is.EqualTo(before));
        }

        [Test]
        public void StateRejectsWritesWithoutChangingDimensions()
        {
            var before = PlayerSettings.defaultScreenWidth;
            Assert.Throws<VmProjectToolException>(() => tool.Execute(new VmPlayerDisplaySettingsRequest
            {
                Action = VmPlayerDisplaySettingsAction.State,
                DefaultScreenWidth = 1080
            }));
            Assert.That(PlayerSettings.defaultScreenWidth, Is.EqualTo(before));
        }
    }
}
