#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace VMUnityAutomation.Editor.Tests
{
    [Category(VmAutomationPackageTestCommands.DefaultPackageSmokeCategory)]
    [Category(VmAutomationPackageTestCommands.FullPackageRegressionCategory)]
    public sealed class VmAutomationWorkspaceCompilationEvidenceTests
    {
        [Test]
        public void CompilationLifecycleWithoutCompletedAssemblyIsRejected()
        {
            VmAutomationWorkspaceJob job = CreateJob(
                new[] { "Assembly-CSharp", "VMUnityAutomation.Editor" },
                Array.Empty<string>());

            Assert.That(
                VmAutomationWorkspaceJobRunner
                    .HasCompleteCompilationAssemblyEvidence(job),
                Is.False);

            Dictionary<string, object> error =
                VmAutomationWorkspaceJobRunner
                    .BuildCompilationAssemblyEvidenceFailure(job);
            Assert.That(error["errorCode"], Is.EqualTo("compilation_evidence_incomplete"));
            Assert.That(error["compiledAssemblyCount"], Is.EqualTo(0));
        }

        [Test]
        public void MissingExpectedAssemblyIsRejected()
        {
            VmAutomationWorkspaceJob job = CreateJob(
                new[] { "Assembly-CSharp", "VMUnityAutomation.Editor" },
                new[] { "Assembly-CSharp" });

            Assert.That(
                VmAutomationWorkspaceJobRunner
                    .HasCompleteCompilationAssemblyEvidence(job),
                Is.False);
            Assert.That(
                VmAutomationWorkspaceJobRunner.FindMissingCompilationAssemblies(job),
                Is.EqualTo(new[] { "VMUnityAutomation.Editor" }));
        }

        [Test]
        public void EveryExpectedCompletedAssemblyIsAcceptedRegardlessOfCallbackOrder()
        {
            VmAutomationWorkspaceJob job = CreateJob(
                new[] { "Assembly-CSharp", "VMUnityAutomation.Editor" },
                new[] { "VMUnityAutomation.Editor", "Assembly-CSharp" });

            Assert.That(
                VmAutomationWorkspaceJobRunner
                    .HasCompleteCompilationAssemblyEvidence(job),
                Is.True);
            Assert.That(
                VmAutomationWorkspaceJobRunner
                    .BuildCompilationAssemblyEvidenceFailure(job),
                Is.Null);
        }

        [Test]
        public void CompilationAssemblyEvidenceSurvivesJobPersistence()
        {
            VmAutomationWorkspaceJob original = CreateJob(
                new[] { "Assembly-CSharp", "VMUnityAutomation.Editor" },
                new[] { "Assembly-CSharp", "VMUnityAutomation.Editor" });

            VmAutomationWorkspaceJob restored =
                VmAutomationWorkspaceJob.FromDictionary(original.ToDictionary());

            Assert.That(restored.ExpectedCompilationAssemblies,
                Is.EqualTo(original.ExpectedCompilationAssemblies));
            Assert.That(restored.CompiledAssemblies,
                Is.EqualTo(original.CompiledAssemblies));
            Assert.That(
                VmAutomationWorkspaceJobRunner
                    .HasCompleteCompilationAssemblyEvidence(restored),
                Is.True);
        }

        private static VmAutomationWorkspaceJob CreateJob(
            IEnumerable<string> expectedAssemblies,
            IEnumerable<string> compiledAssemblies)
        {
            DateTime now = DateTime.UtcNow;
            return new VmAutomationWorkspaceJob
            {
                JobId = "compilation-evidence-test",
                JobAccessToken = "test-token",
                JobType = VmAutomationWorkspaceJobRunner.AssetRefreshJobType,
                Operation = "asset/refresh",
                OwnerAgentId = "test-agent",
                Status = "running",
                Phase = VmAutomationWorkspaceJobRunner.CompilingPhase,
                CreatedAt = now,
                UpdatedAt = now,
                ExpectedCompilationAssemblies =
                    new List<string>(expectedAssemblies),
                CompiledAssemblies = new List<string>(compiledAssemblies),
            };
        }
    }
}
#endif
