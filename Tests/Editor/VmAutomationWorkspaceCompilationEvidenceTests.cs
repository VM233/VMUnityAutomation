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
        public void CompilationLifecycleWithoutPerAssemblyEvidenceIsRejected()
        {
            VmAutomationWorkspaceJob job = CreateJob(
                new[] { "Assembly-CSharp", "VMUnityAutomation.Editor" },
                Array.Empty<string>(),
                Array.Empty<string>(),
                Array.Empty<string>());

            Assert.That(
                VmAutomationCompilationEvidence.IsComplete(job),
                Is.False);

            Dictionary<string, object> error =
                VmAutomationCompilationEvidence.BuildFailure(job);
            Assert.That(error["errorCode"], Is.EqualTo("compilation_evidence_incomplete"));
            Assert.That(error["startedCompilationAssemblyCount"], Is.EqualTo(0));
            Assert.That(error["terminalCompilationAssemblyCount"], Is.EqualTo(0));
        }

        [Test]
        public void NotRequiredTerminalEvidenceWithoutStartedCallbackIsAcceptedPrecisely()
        {
            VmAutomationWorkspaceJob job = CreateJob(
                new[] { "Assembly-CSharp", "VMUnityAutomation.Editor" },
                Array.Empty<string>(),
                Array.Empty<string>(),
                new[] { "Assembly-CSharp", "VMUnityAutomation.Editor" });

            Assert.That(
                VmAutomationCompilationEvidence.IsComplete(job), Is.True);
            Assert.That(
                VmAutomationCompilationEvidence.FindMissingStartedAssemblies(job),
                Is.EqualTo(new[] { "Assembly-CSharp", "VMUnityAutomation.Editor" }));
            Assert.That(VmAutomationCompilationEvidence.BuildFailure(job), Is.Null);
        }

        [Test]
        public void BuildStartWithoutTerminalEvidenceIsRejected()
        {
            VmAutomationWorkspaceJob job = CreateJob(
                new[] { "Assembly-CSharp", "VMUnityAutomation.Editor" },
                new[] { "Assembly-CSharp", "VMUnityAutomation.Editor" },
                new[] { "Assembly-CSharp" },
                Array.Empty<string>());

            Assert.That(
                VmAutomationCompilationEvidence.IsComplete(job),
                Is.False);
            Assert.That(
                VmAutomationCompilationEvidence.FindMissingTerminalAssemblies(job),
                Is.EqualTo(new[] { "VMUnityAutomation.Editor" }));
        }

        [Test]
        public void FinishedAndNotRequiredTerminalEvidenceAreAcceptedTogether()
        {
            VmAutomationWorkspaceJob job = CreateJob(
                new[] { "Assembly-CSharp", "VMUnityAutomation.Editor", "Game.Editor" },
                new[] { "Game.Editor", "VMUnityAutomation.Editor", "Assembly-CSharp" },
                new[] { "VMUnityAutomation.Editor" },
                new[] { "Game.Editor", "Assembly-CSharp" });

            Assert.That(
                VmAutomationCompilationEvidence.IsComplete(job),
                Is.True);
            Assert.That(
                VmAutomationCompilationEvidence.BuildFailure(job),
                Is.Null);
        }

        [Test]
        public void CleanBuildCacheNotRequiredCallbacksAreReportedPrecisely()
        {
            VmAutomationWorkspaceJob job = CreateJob(
                new[] { "Assembly-CSharp", "VMUnityAutomation.Editor" },
                Array.Empty<string>(),
                Array.Empty<string>(),
                new[] { "Assembly-CSharp", "VMUnityAutomation.Editor" });

            Dictionary<string, object> evidence =
                VmAutomationCompilationEvidence.Build(job);

            Assert.That(evidence["cleanBuildCacheCallbackLimitationObserved"], Is.True);
            Assert.That(evidence["startedCompilationAssemblyCount"], Is.EqualTo(0));
            Assert.That(evidence["finishedCompilationAssemblyCount"], Is.EqualTo(0));
            Assert.That(evidence["notRequiredCompilationAssemblyCount"], Is.EqualTo(2));
            Assert.That(evidence["terminalCompilationAssemblyCount"], Is.EqualTo(2));
        }

        [Test]
        public void AssemblyIdentityPreservesDottedNamesAndStripsOutputExtension()
        {
            Assert.That(
                VmAutomationWorkspaceJobRunner.NormalizeCompilationAssemblyName(
                    "Unity.2D.Animation.Runtime"),
                Is.EqualTo("Unity.2D.Animation.Runtime"));
            Assert.That(
                VmAutomationWorkspaceJobRunner.NormalizeCompilationAssemblyName(
                    "Library/ScriptAssemblies/Unity.2D.Animation.Runtime.dll"),
                Is.EqualTo("Unity.2D.Animation.Runtime"));
        }

        [Test]
        public void CompilationAssemblyEvidenceSurvivesJobPersistence()
        {
            VmAutomationWorkspaceJob original = CreateJob(
                new[] { "Assembly-CSharp", "VMUnityAutomation.Editor" },
                new[] { "Assembly-CSharp", "VMUnityAutomation.Editor" },
                new[] { "Assembly-CSharp" },
                new[] { "VMUnityAutomation.Editor" });

            VmAutomationWorkspaceJob restored =
                VmAutomationWorkspaceJob.FromDictionary(original.ToDictionary());

            Assert.That(restored.ExpectedCompilationAssemblies,
                Is.EqualTo(original.ExpectedCompilationAssemblies));
            Assert.That(restored.StartedCompilationAssemblies,
                Is.EqualTo(original.StartedCompilationAssemblies));
            Assert.That(restored.FinishedCompilationAssemblies,
                Is.EqualTo(original.FinishedCompilationAssemblies));
            Assert.That(restored.NotRequiredCompilationAssemblies,
                Is.EqualTo(original.NotRequiredCompilationAssemblies));
            Assert.That(
                VmAutomationCompilationEvidence.IsComplete(restored),
                Is.True);
        }

        [Test]
        public void CompletedCallbacksWaitForThePublishedNativeOutcomeAcrossPersistence()
        {
            VmAutomationWorkspaceJob job = CreateCompleteJob();

            VmAutomationWorkspaceJobRunner.RecordCompilationCompletion(job);

            Assert.That(job.CompilationFinished, Is.True);
            Assert.That(job.CompilationSucceeded, Is.Null);
            Assert.That(job.Phase,
                Is.EqualTo(VmAutomationWorkspaceJobRunner.AwaitingCompilationOutcomePhase));
            VmAutomationWorkspaceJob restored =
                VmAutomationWorkspaceJob.FromDictionary(job.ToDictionary());
            Assert.That(restored.CompilationSucceeded, Is.Null);
            Assert.That(restored.Phase, Is.EqualTo(job.Phase));
            Assert.That(restored.CompilationFinishedAt, Is.EqualTo(job.CompilationFinishedAt));

            Assert.That(VmAutomationWorkspaceJobRunner.ResolveCompilationOutcome(
                restored, unityScriptCompilationFailed: false), Is.Null);
            Assert.That(restored.CompilationSucceeded, Is.True);
        }

        [Test]
        public void PublishedPipelineFailureWithoutCompilerMessagesRemainsAFailure()
        {
            VmAutomationWorkspaceJob job = CreateCompleteJob();
            VmAutomationWorkspaceJobRunner.RecordCompilationCompletion(job);

            Dictionary<string, object> error =
                VmAutomationWorkspaceJobRunner.ResolveCompilationOutcome(
                    job, unityScriptCompilationFailed: true);

            Assert.That(job.CompilationSucceeded, Is.False);
            Assert.That(error["errorCode"], Is.EqualTo("compilation_failed"));
            Assert.That(error["unityScriptCompilationFailed"], Is.True);
            Assert.That(error["perAssemblyCompilerErrorCount"], Is.EqualTo(0));
        }

        [Test]
        public void CompilerErrorsAreRejectedWhenTheNativeFlagIsClear()
        {
            VmAutomationWorkspaceJob job = CreateCompleteJob();
            job.CompilerErrorCount = 1;
            VmAutomationWorkspaceJobRunner.RecordCompilationCompletion(job);

            Dictionary<string, object> error =
                VmAutomationWorkspaceJobRunner.ResolveCompilationOutcome(
                    job, unityScriptCompilationFailed: false);

            Assert.That(job.CompilationSucceeded, Is.False);
            Assert.That(error["errorCode"], Is.EqualTo("compilation_failed"));
            Assert.That(error["perAssemblyCompilerErrorCount"], Is.EqualTo(1));
        }

        [Test]
        public void ClearNativeOutcomeDoesNotReplaceMissingAssemblyEvidence()
        {
            VmAutomationWorkspaceJob job = CreateCompleteJob();
            job.FinishedCompilationAssemblies.Clear();
            VmAutomationWorkspaceJobRunner.RecordCompilationCompletion(job);

            Dictionary<string, object> error =
                VmAutomationWorkspaceJobRunner.ResolveCompilationOutcome(
                    job, unityScriptCompilationFailed: false);

            Assert.That(job.CompilationSucceeded, Is.False);
            Assert.That(error["errorCode"], Is.EqualTo("compilation_evidence_incomplete"));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ManifestRestorationChecksThePublishedPipelineOutcome(bool pipelineFailed)
        {
            var diagnostics = new Dictionary<string, object>
            {
                { "counts", new Dictionary<string, object> { { "errors", 0 } } },
                { "entries", new List<object>() },
            };

            bool failed = VmAutomationPackageTestCommands.TryBuildAuthoritativeCompilationFailure(
                diagnostics, pipelineFailed, out string error);

            Assert.That(failed, Is.EqualTo(pipelineFailed));
            if (pipelineFailed)
                Assert.That(error, Does.Contain("pipeline-level"));
        }

        private static VmAutomationWorkspaceJob CreateCompleteJob()
        {
            return CreateJob(new[] { "Assembly-CSharp" },
                new[] { "Assembly-CSharp" }, new[] { "Assembly-CSharp" },
                Array.Empty<string>());
        }

        private static VmAutomationWorkspaceJob CreateJob(
            IEnumerable<string> expectedAssemblies,
            IEnumerable<string> startedAssemblies,
            IEnumerable<string> finishedAssemblies,
            IEnumerable<string> notRequiredAssemblies)
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
                StartedCompilationAssemblies = new List<string>(startedAssemblies),
                FinishedCompilationAssemblies = new List<string>(finishedAssemblies),
                NotRequiredCompilationAssemblies = new List<string>(notRequiredAssemblies),
            };
        }
    }
}
#endif
