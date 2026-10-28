using System;
using NUnit.Framework;
using UnityEditor;

namespace VMUnityAutomation.Editor.Tests
{
    [Category(VmAutomationPackageTestCommands.DefaultPackageSmokeCategory)]
    [Category(VmAutomationPackageTestCommands.FullPackageRegressionCategory)]
    public sealed class VmPlayerBuildSelectionTests
    {
        [Test]
        public void RequestedBuildSelectionOverridesAnUnrelatedSelectedPlatformAndRestoresIt()
        {
            var originalGroup = EditorUserBuildSettings.selectedBuildTargetGroup;
            var originalStandalone = EditorUserBuildSettings.selectedStandaloneTarget;
            try
            {
                EditorUserBuildSettings.selectedBuildTargetGroup = BuildTargetGroup.WebGL;
                var scope = new VmPlayerBuildSelection(BuildTarget.StandaloneWindows64);
                try
                {
                    Assert.That(EditorUserBuildSettings.selectedBuildTargetGroup, Is.EqualTo(BuildTargetGroup.Standalone));
                    Assert.That(EditorUserBuildSettings.selectedStandaloneTarget, Is.EqualTo(BuildTarget.StandaloneWindows64));
                    Assert.That(scope.Receipt["selectedGroupBefore"], Is.EqualTo("WebGL"));
                    Assert.That(scope.Receipt["restored"], Is.False);
                }
                finally { scope.Dispose(); }
                Assert.That(EditorUserBuildSettings.selectedBuildTargetGroup, Is.EqualTo(BuildTargetGroup.WebGL));
                Assert.That(EditorUserBuildSettings.selectedStandaloneTarget, Is.EqualTo(originalStandalone));
                Assert.That(scope.Receipt["restored"], Is.True);
            }
            finally
            {
                EditorUserBuildSettings.selectedStandaloneTarget = originalStandalone;
                EditorUserBuildSettings.selectedBuildTargetGroup = originalGroup;
            }
        }

        [Test]
        public void BuildExceptionStillRestoresTheExactOriginalSelection()
        {
            var originalGroup = EditorUserBuildSettings.selectedBuildTargetGroup;
            var originalStandalone = EditorUserBuildSettings.selectedStandaloneTarget;
            Assert.Throws<InvalidOperationException>(() =>
            {
                using var scope = new VmPlayerBuildSelection(BuildTarget.StandaloneWindows64);
                throw new InvalidOperationException("Rejected native build");
            });
            Assert.That(EditorUserBuildSettings.selectedBuildTargetGroup, Is.EqualTo(originalGroup));
            Assert.That(EditorUserBuildSettings.selectedStandaloneTarget, Is.EqualTo(originalStandalone));
        }
    }
}
