using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;

namespace VMUnityAutomation.Editor.Tests
{
    [Category(VmAutomationPackageTestCommands.DefaultPackageSmokeCategory)]
    [Category(VmAutomationPackageTestCommands.FullPackageRegressionCategory)]
    public sealed class VmPlayerBuildSelectionTests
    {
        [TestCase("Player", StandaloneBuildSubtarget.Player)]
        [TestCase("Server", StandaloneBuildSubtarget.Server)]
        public void StandaloneSubtargetReachesTheNativeSelectionAndRestoresIt(string requested, StandaloneBuildSubtarget expected)
        {
            var before = EditorUserBuildSettings.standaloneBuildSubtarget;
            int selected = VmPlayerBuildSelection.ReadSubtarget(BuildTarget.StandaloneWindows64,
                new Dictionary<string, object> { { "standaloneSubtarget", requested } });
            using (var scope = new VmPlayerBuildSelection(BuildTarget.StandaloneWindows64, EditorUserBuildSettings.development, selected))
            {
                Assert.That(selected, Is.EqualTo((int)expected));
                Assert.That(EditorUserBuildSettings.standaloneBuildSubtarget, Is.EqualTo(expected));
                Assert.That(scope.Receipt["requestedStandaloneSubtarget"], Is.EqualTo(requested));
            }
            Assert.That(EditorUserBuildSettings.standaloneBuildSubtarget, Is.EqualTo(before));
        }

        [Test]
        public void OmittedSubtargetKeepsTheExistingPlayerBuildContract()
        {
            Assert.That(VmPlayerBuildSelection.ReadSubtarget(BuildTarget.StandaloneWindows64, new Dictionary<string, object>()),
                Is.EqualTo((int)StandaloneBuildSubtarget.Player));
            Assert.That(VmPlayerBuildSelection.ReadSubtarget(BuildTarget.Android, new Dictionary<string, object>()), Is.Zero);
        }

        [TestCase("StandaloneWindows64", "invalid")]
        [TestCase("StandaloneWindows64", "1")]
        [TestCase("Android", "Server")]
        public void InvalidSubtargetsAreRejectedBeforeJobAdmissionOrOutputRemoval(string target, string subtarget)
        {
            Assert.Throws<ArgumentException>(() => VmAutomationBuildCommands.StartBuild(new Dictionary<string, object>
                { { "target", target }, { "standaloneSubtarget", subtarget }, { "outputPath", "Library/Rejected.exe" }, { "run", false } }));
        }

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
