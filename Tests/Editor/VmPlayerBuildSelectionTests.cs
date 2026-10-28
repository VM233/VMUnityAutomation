using System;
using NUnit.Framework;
using UnityEditor;

namespace VMUnityAutomation.Editor.Tests
{
    [Category(VmAutomationPackageTestCommands.DefaultPackageSmokeCategory)]
    [Category(VmAutomationPackageTestCommands.FullPackageRegressionCategory)]
    public sealed class VmPlayerBuildSelectionTests
    {
        [TestCase(true)]
        [TestCase(false)]
        public void RequestedBuildSelectionOverridesAnUnrelatedSelectedPlatformAndRestoresIt(bool requestedDevelopment)
        {
            var originalGroup = EditorUserBuildSettings.selectedBuildTargetGroup;
            var originalStandalone = EditorUserBuildSettings.selectedStandaloneTarget;
            var originalDevelopment = EditorUserBuildSettings.development;
            try
            {
                EditorUserBuildSettings.selectedBuildTargetGroup = BuildTargetGroup.WebGL;
                EditorUserBuildSettings.development = !requestedDevelopment;
                var scope = new VmPlayerBuildSelection(BuildTarget.StandaloneWindows64, requestedDevelopment);
                try
                {
                    Assert.That(EditorUserBuildSettings.selectedBuildTargetGroup, Is.EqualTo(BuildTargetGroup.Standalone));
                    Assert.That(EditorUserBuildSettings.selectedStandaloneTarget, Is.EqualTo(BuildTarget.StandaloneWindows64));
                    Assert.That(scope.Receipt["selectedGroupBefore"], Is.EqualTo("WebGL"));
                    Assert.That(scope.Receipt["restored"], Is.False);
                    Assert.That(EditorUserBuildSettings.development, Is.EqualTo(requestedDevelopment));
                    Assert.That(scope.Receipt["developmentBefore"], Is.EqualTo(!requestedDevelopment));
                    Assert.That(scope.Receipt["developmentDuringBuild"], Is.EqualTo(requestedDevelopment));
                }
                finally { scope.Dispose(); }
                Assert.That(EditorUserBuildSettings.selectedBuildTargetGroup, Is.EqualTo(BuildTargetGroup.WebGL));
                Assert.That(EditorUserBuildSettings.selectedStandaloneTarget, Is.EqualTo(originalStandalone));
                Assert.That(scope.Receipt["restored"], Is.True);
                Assert.That(EditorUserBuildSettings.development, Is.EqualTo(!requestedDevelopment));
                Assert.That(scope.Receipt["developmentAfter"], Is.EqualTo(!requestedDevelopment));
            }
            finally
            {
                EditorUserBuildSettings.development = originalDevelopment;
                EditorUserBuildSettings.selectedStandaloneTarget = originalStandalone;
                EditorUserBuildSettings.selectedBuildTargetGroup = originalGroup;
            }
        }

        [Test]
        public void BuildExceptionStillRestoresTheExactOriginalSelection()
        {
            var originalGroup = EditorUserBuildSettings.selectedBuildTargetGroup;
            var originalStandalone = EditorUserBuildSettings.selectedStandaloneTarget;
            var originalDevelopment = EditorUserBuildSettings.development;
            Assert.Throws<InvalidOperationException>(() =>
            {
                using var scope = new VmPlayerBuildSelection(BuildTarget.StandaloneWindows64, !originalDevelopment);
                Assert.That(EditorUserBuildSettings.development, Is.EqualTo(!originalDevelopment));
                throw new InvalidOperationException("Rejected native build");
            });
            Assert.That(EditorUserBuildSettings.development, Is.EqualTo(originalDevelopment));
            Assert.That(EditorUserBuildSettings.selectedBuildTargetGroup, Is.EqualTo(originalGroup));
            Assert.That(EditorUserBuildSettings.selectedStandaloneTarget, Is.EqualTo(originalStandalone));
        }
    }
}
