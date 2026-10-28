#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace VMUnityAutomation.Editor.Tests
{
    [Category(VmAutomationPackageTestCommands.DefaultPackageSmokeCategory)]
    [Category(VmAutomationPackageTestCommands.FullPackageRegressionCategory)]
    public sealed class VmAutomationBurstCompilationTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void UnrequestedRefreshDoesNotMutateNativeLifecycle(bool explicitFalse)
        {
            var job = Job();
            if (explicitFalse) job.Request["recompileBurst"] = false;
            bool persisted = false;
            Assert.That(VmAutomationBurstCompilation.Advance(job, () => persisted = true,
                out bool completed), Is.Null);
            Assert.That(completed, Is.True);
            Assert.That(persisted, Is.False);
            Assert.That(job.BurstCompilationStartedAt, Is.Null);
        }

        [Test]
        public void OriginalRequestAndNativePhaseSurvivePersistence()
        {
            var job = Job();
            job.Request["recompileBurst"] = true;
            Assert.That(job.RequiresStableEditMode, Is.True);
            job.Phase = VmAutomationBurstCompilation.Phase;
            job.BurstCompilationStartedAt = job.CreatedAt;
            job.BurstCompilationFinishedAt = job.CreatedAt.AddSeconds(2);
            job.BurstCompilationDomainRevision = 3;
            var restored = VmAutomationWorkspaceJob.FromDictionary(job.ToDictionary());
            Assert.That(VmAutomationBurstCompilation.Requested(restored.Request), Is.True);
            Assert.That(restored.Phase, Is.EqualTo(job.Phase));
            Assert.That(restored.BurstCompilationStartedAt, Is.EqualTo(job.BurstCompilationStartedAt));
            Assert.That(restored.BurstCompilationFinishedAt, Is.EqualTo(job.BurstCompilationFinishedAt));
            Assert.That(restored.BurstCompilationDomainRevision, Is.EqualTo(3));
            Assert.That(restored.RequiresStableEditMode, Is.True);
        }

        [Test]
        public void NewDomainCannotAcceptAnUnobservedNativeRequest()
        {
            var job = Started();
            job.DomainReloadCount++;
            Assert.That(VmAutomationBurstCompilation.ObservationError(job, job.CreatedAt)["errorCode"],
                Is.EqualTo("burst_compilation_interrupted"));
        }

        [TestCase(599, null)]
        [TestCase(600, "burst_compilation_timeout")]
        [TestCase(601, "burst_compilation_timeout")]
        public void NativeObservationHasATerminalDeadline(int seconds, string expected)
        {
            var job = Started();
            var error = VmAutomationBurstCompilation.ObservationError(job, job.CreatedAt.AddSeconds(seconds));
            Assert.That(error == null ? null : error["errorCode"], Is.EqualTo(expected));
        }

        [Test]
        public void NativeObservationRequiresItsPublishedRequest()
        {
            var job = Job();
            Assert.Throws<InvalidOperationException>(() =>
                VmAutomationBurstCompilation.ObservationError(job, job.CreatedAt));
        }

        [Test]
        public void RefreshContractExposesExplicitNativeCompilation()
        {
            var schema = VmAutomationToolInputSchemaCatalog.Get("asset/refresh");
            var properties = (Dictionary<string, object>)schema["properties"];
            var option = (Dictionary<string, object>)properties["recompileBurst"];
            Assert.That(option["type"], Is.EqualTo("boolean"));
            Assert.That(VmAutomationToolProfileCatalog.Get("asset/refresh").MutatesRuntime, Is.True);
            Assert.That(VmAutomationCatalog.TryGetTool("vm_auto_asset_refresh", true, out var contract), Is.True);
            Assert.That((System.Collections.IEnumerable)contract["errorCodes"], Does.Contain("burst_compilation_failed"));
            Assert.That((System.Collections.IEnumerable)contract["errorCodes"], Does.Contain("burst_compilation_diagnostics_incomplete"));
        }

        static VmAutomationWorkspaceJob Started()
        {
            var job = Job();
            job.BurstCompilationStartedAt = job.CreatedAt;
            job.BurstCompilationDomainRevision = job.DomainReloadCount;
            return job;
        }

        static VmAutomationWorkspaceJob Job() => new()
        {
            JobId = "native-burst-lifecycle", JobType = "asset-refresh", Operation = "asset/refresh",
            JobAccessToken = "fixture-capability", OwnerAgentId = "fixture-owner", Status = "running",
            Phase = VmAutomationWorkspaceJobRunner.VerifyingPhase,
            CreatedAt = new DateTime(2026, 10, 26, 0, 0, 0, DateTimeKind.Utc),
            UpdatedAt = new DateTime(2026, 10, 26, 0, 0, 0, DateTimeKind.Utc), DomainReloadCount = 3,
        };
    }
}
#endif
