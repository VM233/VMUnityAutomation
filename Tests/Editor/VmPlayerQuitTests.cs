using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using NUnit.Framework;

namespace VMUnityAutomation.Editor.Tests
{
    [Category(VmAutomationPackageTestCommands.DefaultPackageSmokeCategory)]
    [Category(VmAutomationPackageTestCommands.FullPackageRegressionCategory)]
    public sealed class VmPlayerQuitTests
    {
        [Test]
        public void ContractPublishesIdentityAndDurableCompletion()
        {
            Assert.That(VmAutomationBuiltInRouteDescriptorRegistry.TryGet("player/quit", out var descriptor), Is.True);
            Assert.That(descriptor.IsDeferred, Is.False);
            Assert.That(descriptor.Profile.LongRunning, Is.True);
            Assert.That(descriptor.Profile.MutatesRuntime, Is.True);
            Assert.That(descriptor.InputSchema["additionalProperties"], Is.False);
            Assert.That((IList)descriptor.InputSchema["required"],
                Is.EquivalentTo(new[] { "executablePath", "processId", "startedAt" }));
            Assert.That(descriptor.Profile.Transaction.RollbackKind, Is.EqualTo(VmTransactionMechanics.RollbackKind.None));
            var properties = (Dictionary<string, object>)descriptor.OutputSchema["properties"];
            Assert.That(properties, Contains.Key("jobId"));
            Assert.That(properties, Contains.Key("jobAccessToken"));
            Assert.That(properties, Contains.Key("pollRoute"));
        }

#if UNITY_EDITOR_WIN
        [TestCase("processId", 0)]
        [TestCase("processId", -1)]
        [TestCase("timeoutMs", 99)]
        [TestCase("timeoutMs", 60001)]
        [TestCase("timeoutMs", 1.5)]
        [TestCase("startedAt", "2026-10-08T01:02:03Z")]
        [TestCase("startedAt", "2026-10-08T01:02:03.0000000+08:00")]
        public void InvalidIdentityAndDeadlineFailBeforeAdmission(string field, object value)
        {
            using Process process = Process.GetCurrentProcess();
            var request = Request(process);
            request[field] = value;
            var result = VmAutomationResponse.ToDictionary(VmAutomationPlayerQuitCommands.Start(request));
            Assert.That(result["errorCode"], Is.EqualTo("invalid_player_quit_arguments"));
            Assert.That(result, Does.Not.ContainKey("jobId"));
        }

        [Test]
        public void IdentityReadsTheActualOsCreationTimeAndPath()
        {
            using Process process = Process.GetCurrentProcess();
            string path = Path.GetFullPath(process.MainModule.FileName);
            DateTime startedAt = process.StartTime.ToUniversalTime();
            Assert.DoesNotThrow(() => VmAutomationPlayerQuitCommands.VerifyIdentity(process, path, startedAt));
            var wrongPath = Assert.Throws<VmProjectToolException>(() =>
                VmAutomationPlayerQuitCommands.VerifyIdentity(process, path + ".other", startedAt));
            Assert.That(wrongPath.ErrorCode, Is.EqualTo("player_quit_identity_mismatch"));
            var wrongStart = Assert.Throws<VmProjectToolException>(() =>
                VmAutomationPlayerQuitCommands.VerifyIdentity(process, path, startedAt.AddTicks(1)));
            Assert.That(wrongStart.ErrorCode, Is.EqualTo("player_quit_identity_mismatch"));
            Assert.That(process.HasExited, Is.False);
        }

        [Test]
        public void NativeIdentityMismatchFailsBeforeCloseBoundary()
        {
            using Process process = Process.GetCurrentProcess();
            var request = Request(process);
            request["startedAt"] = process.StartTime.ToUniversalTime().AddTicks(1).ToString("O");
            var receipt = (Dictionary<string, object>)VmAutomationWorkspaceJobRunner.StartPlayerQuit(request);
            VmAutomationWorkspaceJob job = VmAutomationWorkspaceJobStore.Find((string)receipt["jobId"]);
            try
            {
                Assert.That(VmAutomationPlayerQuitCommands.ExecutePhase(job), Is.True);
                Assert.That(job.Status, Is.EqualTo("failed"));
                Assert.That(job.Error["errorCode"], Is.EqualTo("player_quit_identity_mismatch"));
                Assert.That(job.TransactionState, Is.Null);
                Assert.That(process.HasExited, Is.False);
            }
            finally { CancelQueued(job, receipt); }
        }

        [Test]
        public void ReceiptIsQueuedAndCancelableUntilCloseRequest()
        {
            using Process process = Process.GetCurrentProcess();
            var request = Request(process);
            request["idempotencyKey"] = Guid.NewGuid().ToString("N");
            var receipt = (Dictionary<string, object>)VmAutomationWorkspaceJobRunner.StartPlayerQuit(request);
            VmAutomationWorkspaceJob job = VmAutomationWorkspaceJobStore.Find((string)receipt["jobId"]);
            try
            {
                Assert.That(receipt["pollRoute"], Is.EqualTo("jobs/get"));
                Assert.That(job.ClientAdopted, Is.False);
                Assert.That(job.TransactionState, Is.Null);
                var reused = (Dictionary<string, object>)VmAutomationWorkspaceJobRunner.StartPlayerQuit(request);
                Assert.That(reused["jobId"], Is.EqualTo(job.JobId));
                CancelQueued(job, receipt);
                Assert.That(job.Status, Is.EqualTo("canceled"));
                Assert.That(process.HasExited, Is.False);
            }
            finally { CancelQueued(job, receipt); }
        }

        [Test]
        public void RecordedCloseRequestCannotBeCanceledOrReplayedAfterReload()
        {
            using Process process = Process.GetCurrentProcess();
            var receipt = (Dictionary<string, object>)VmAutomationWorkspaceJobRunner.StartPlayerQuit(Request(process));
            VmAutomationWorkspaceJob job = VmAutomationWorkspaceJobStore.Find((string)receipt["jobId"]);
            job.TransactionState = new Dictionary<string, object>
            {
                { "quitRequestedAt", DateTime.UtcNow.ToString("O") }, { "closeRequestAccepted", true },
            };
            job.Phase = VmAutomationPlayerQuitCommands.WaitingForExitPhase;
            try
            {
                var cancel = VmAutomationResponse.ToDictionary(VmAutomationWorkspaceJobRunner.Cancel(Access(receipt)));
                Assert.That(cancel["errorCode"], Is.EqualTo("job_not_cancellable"));
                VmAutomationPlayerQuitCommands.RecoverAfterReload(job);
                Assert.That(job.Status, Is.EqualTo("failed"));
                Assert.That(job.Error["errorCode"], Is.EqualTo("player_quit_outcome_uncertain_after_reload"));
                Assert.That(job.Result, Is.Null);
                Assert.That(process.HasExited, Is.False);
            }
            finally
            {
                if (!job.IsTerminal)
                    VmAutomationWorkspaceJobRunner.Fail(job, VmAutomationResponse.Error("Fixture retired.", "fixture_retired"));
            }
        }

        private static Dictionary<string, object> Request(Process process) => new()
        {
            { "executablePath", Path.GetFullPath(process.MainModule.FileName) },
            { "processId", process.Id }, { "startedAt", process.StartTime.ToUniversalTime().ToString("O") },
        };

        private static Dictionary<string, object> Access(Dictionary<string, object> receipt) => new()
        {
            { "jobId", receipt["jobId"] }, { "jobAccessToken", receipt["jobAccessToken"] },
        };

        private static void CancelQueued(VmAutomationWorkspaceJob job, Dictionary<string, object> receipt)
        {
            if (!job.IsTerminal) VmAutomationWorkspaceJobRunner.Cancel(Access(receipt));
        }
#endif
    }
}
