using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using TestJob = VMUnityAutomation.Editor.VmAutomationTestRunnerCommands.TestJob;
using TestJobStatus = VMUnityAutomation.Editor.VmAutomationTestRunnerCommands.TestJobStatus;
using TestResult = VMUnityAutomation.Editor.VmAutomationTestRunnerCommands.TestResult;

namespace VMUnityAutomation.Editor.Tests
{
    [Category(VmAutomationPackageTestCommands.DefaultPackageSmokeCategory)]
    [Category(VmAutomationPackageTestCommands.FullPackageRegressionCategory)]
    public sealed class VmTestJobSessionTests
    {
        private string prefix;
        private VmAutomationTestJobSession session;
        private readonly List<TestJob> jobs = new List<TestJob>();

        [SetUp]
        public void SetUp()
        {
            prefix = "VmTestJobSessionTests/" + Guid.NewGuid().ToString("N");
            session = new VmAutomationTestJobSession(prefix);
        }

        [TearDown]
        public void TearDown()
        {
            foreach (TestJob job in jobs)
                session.RetireJob(job.JobId, job.AllResults.Count);
            session.RetireIndex();
            jobs.Clear();
        }

        [Test]
        public void ReloadPreservesAllStatusesBeyondTheFailureSummaryLimitAndPagesExactResults()
        {
            TestJob job = CreateJob("all-statuses", 100);
            Publish(job);
            var reloaded = new VmAutomationTestJobSession(prefix);
            Assert.That(reloaded.ReadMembership(), Is.EqualTo(new[] { job.JobId }));
            Assert.That(reloaded.CurrentJobId, Is.EqualTo(job.JobId));
            TestJob restored = reloaded.ReadJob(job.JobId);
            Assert.That(restored.AllResults.Count, Is.EqualTo(100));
            Assert.That(restored.FailuresSoFar.Count, Is.EqualTo(50));
            Assert.That(restored.LastUpdatedAt, Is.EqualTo(job.LastUpdatedAt));
            Assert.That(restored.CurrentTestName, Is.EqualTo(job.CurrentTestName));
            Assert.That(restored.CurrentTestStartedAt, Is.EqualTo(job.CurrentTestStartedAt));
            Assert.That(restored.Assemblies, Is.EqualTo(job.Assemblies));
            for (int index = 0; index < job.AllResults.Count; index++)
                AssertSameResult(restored.AllResults[index], job.AllResults[index]);

            Dictionary<string, object> before = VmAutomationTestRunnerCommands.SerializeJob(
                job, true, false, true, 49, 7, 20);
            Dictionary<string, object> after = VmAutomationTestRunnerCommands.SerializeJob(
                restored, true, false, true, 49, 7, 20);
            Assert.That(MiniJson.Serialize(after["tests"]), Is.EqualTo(MiniJson.Serialize(before["tests"])));
            Assert.That(MiniJson.Serialize(after["summary"]), Is.EqualTo(MiniJson.Serialize(before["summary"])));
            Assert.That(after["totalResults"], Is.EqualTo(100));
            Assert.That(after["nextResultOffset"], Is.EqualTo(56));
            var failures = VmAutomationTestRunnerCommands.SerializeJob(
                restored, false, true, true, 50, 100, 20);
            Assert.That(failures["totalResults"], Is.EqualTo(70));
            Assert.That(failures["returnedResults"], Is.EqualTo(20));
            Assert.That(failures["hasMoreResults"], Is.False);
            var page = (List<Dictionary<string, object>>)after["tests"];
            Assert.That(page[0]["duration"], Is.EqualTo(job.AllResults[49].Duration));
            Assert.That(page[0]["stackTrace"], Is.EqualTo(job.AllResults[49].StackTrace));
        }

        [Test]
        public void CanonicalCollectionReplacesCallbackOrderBeforeTerminalPublication()
        {
            TestJob job = CreateJob("canonical", 100);
            Publish(job);
            List<TestResult> canonical = job.AllResults.Take(99).Reverse().ToList();
            session.ReplaceResults(job.JobId, job.AllResults.Count, canonical);
            job.AllResults = canonical;
            job.CompletedTests = canonical.Count;
            job.Status = TestJobStatus.Failed;
            job.CompletedAt = DateTime.UtcNow;
            session.PublishJob(job, null);
            TestJob restored = new VmAutomationTestJobSession(prefix).ReadJob(job.JobId);
            Assert.That(restored.Status, Is.EqualTo(TestJobStatus.Failed));
            Assert.That(restored.CompletedAt, Is.EqualTo(job.CompletedAt));
            Assert.That(restored.AllResults.Select(result => result.FullName),
                Is.EqualTo(canonical.Select(result => result.FullName)));
            Assert.That(SessionState.GetString(prefix + "/job/canonical/result/99", "missing"),
                Is.EqualTo("missing"));
            Assert.That(session.CurrentJobId, Is.Empty);
        }

        [Test]
        public void ACompletedCountWithoutItsPublishedLeafIsAnExplicitPersistenceError()
        {
            TestJob job = CreateJob("missing-leaf", 1);
            session.PublishJob(job, job.JobId);
            var error = Assert.Throws<InvalidOperationException>(() =>
                new VmAutomationTestJobSession(prefix).ReadJob(job.JobId));
            Assert.That(error.Message, Does.Contain("missing-leaf/result/0"));
            session.PublishResult(job.JobId, 0, job.AllResults[0]);
            Assert.That(session.ReadJob(job.JobId).AllResults.Count, Is.EqualTo(1));
        }

        [Test]
        public void ProgressAndRetirementDoNotRewriteOrRemoveAnotherJobsEvidence()
        {
            TestJob first = CreateJob("first", 50);
            TestJob second = CreateJob("second", 50);
            Publish(first);
            Publish(second);
            string secondKey = prefix + "/job/second";
            string secondMetadata = SessionState.GetString(secondKey, "");
            string secondLastResult = SessionState.GetString(secondKey + "/result/49", "");
            first.CurrentTestName = "Another.Test";
            session.PublishJob(first, first.JobId);
            Assert.That(SessionState.GetString(secondKey, ""), Is.EqualTo(secondMetadata));
            Assert.That(SessionState.GetString(secondKey + "/result/49", ""),
                Is.EqualTo(secondLastResult));
            session.PublishMembership(new[] { second.JobId });
            session.RetireJob(first.JobId, first.AllResults.Count);
            Assert.That(session.ReadMembership(), Is.EqualTo(new[] { second.JobId }));
            Assert.That(session.ReadJob(second.JobId).AllResults.Count, Is.EqualTo(50));
            Assert.That(SessionState.GetString(prefix + "/job/first", "missing"), Is.EqualTo("missing"));
            for (int index = 0; index < first.AllResults.Count; index++)
                Assert.That(SessionState.GetString(prefix + "/job/first/result/" + index, "missing"),
                    Is.EqualTo("missing"));
        }

        private TestJob CreateJob(string id, int count)
        {
            var job = new TestJob
            {
                JobId = id, AgentId = "session-test", RunGuid = "run-guid", Mode = TestMode.EditMode,
                Status = TestJobStatus.Running, StartedAt = DateTime.UtcNow,
                LastUpdatedAt = DateTime.UtcNow, CurrentTestName = "Current.Test",
                CurrentTestStartedAt = DateTime.UtcNow, TotalTests = count, CompletedTests = count,
                TestNames = new[] { "Selected.Test" }, Categories = new[] { "SelectedCategory" },
                Assemblies = new[] { "Selected.Assembly" }, HasExplicitFilters = true
            };
            for (int index = 0; index < count; index++)
            {
                string status = index < 60 ? "Failed" : index < 80 ? "Passed" :
                    index < 90 ? "Skipped" : "Inconclusive";
                job.AllResults.Add(new TestResult("Fixture.Test" + index, "Test" + index, status,
                    0.123456789012345 + index, "Message\n" + index, "Stack\nAt " + index));
            }
            job.PassedCount = job.AllResults.Count(result => result.Status == "Passed");
            job.FailedCount = job.AllResults.Count(result => result.Status == "Failed");
            job.SkippedCount = count - job.PassedCount - job.FailedCount;
            job.FailuresSoFar = job.AllResults.Where(result => result.IsFailure).Take(50).ToList();
            jobs.Add(job);
            return job;
        }

        private void Publish(TestJob job)
        {
            for (int index = 0; index < job.AllResults.Count; index++)
                session.PublishResult(job.JobId, index, job.AllResults[index]);
            session.PublishJob(job, job.JobId);
            session.PublishMembership(jobs.Select(value => value.JobId));
        }

        private static void AssertSameResult(TestResult actual, TestResult expected)
        {
            Assert.That(actual.FullName, Is.EqualTo(expected.FullName));
            Assert.That(actual.Name, Is.EqualTo(expected.Name));
            Assert.That(actual.Status, Is.EqualTo(expected.Status));
            Assert.That(actual.Duration, Is.EqualTo(expected.Duration));
            Assert.That(actual.Message, Is.EqualTo(expected.Message));
            Assert.That(actual.StackTrace, Is.EqualTo(expected.StackTrace));
        }
    }
}
