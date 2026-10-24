using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;

namespace VMUnityAutomation.Editor.Tests
{
    [Category(VmAutomationPackageTestCommands.DefaultPackageSmokeCategory)]
    [Category(VmAutomationPackageTestCommands.FullPackageRegressionCategory)]
    public sealed class VmPendingCheckpointTests
    {
        private const BindingFlags PrivateStatic = BindingFlags.Static | BindingFlags.NonPublic;
        private readonly Dictionary<FieldInfo, object> replaced = new();
        private List<Dictionary<string, object>> jobs;
        private Dictionary<string, object>[] originalJobs;
        private Dictionary<string, object> job;
        private VmAutomationJobRecordStore executions;
        private VmAutomationJobRecordStore history;
        private string directory;
        private string executionPath;
        private string historyPath;

        [SetUp]
        public void SetUp()
        {
            directory = Path.Combine(Path.GetTempPath(), "VmPendingCheckpoint-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            executionPath = Path.Combine(directory, "execution.json");
            historyPath = Path.Combine(directory, "history.json");
            executions = new VmAutomationJobRecordStore(executionPath);
            history = new VmAutomationJobRecordStore(historyPath);
            executions.Load();
            history.Load();
            jobs = (List<Dictionary<string, object>>)RunnerField("Jobs").GetValue(null);
            originalJobs = jobs.ToArray();
            jobs.Clear();
            Replace(RunnerField("recordStore"), executions);
            Replace(RunnerField("loaded"), true);
            Replace(HistoryField("recordStore"), history);
            Replace(HistoryField("entries"), new List<Dictionary<string, object>>());
            Replace(HistoryField("publishedState"), null);
            InvokeHistory("PublishCurrentEntries");
            job = new Dictionary<string, object>
            {
                ["jobId"] = Guid.NewGuid().ToString("N"), ["jobType"] = "project-tool",
                ["ownerAgentId"] = "checkpoint-test", ["jobAccessToken"] = "owned-capability",
                ["status"] = "running", ["cleanupStatus"] = "none", ["cleanupToken"] = "",
                ["jobState"] = new Dictionary<string, object>(), ["progress"] = null,
                ["statusMessage"] = "Running.", ["stepCount"] = 0,
                ["updatedAt"] = "2000-01-01T00:00:00.0000000Z", ["nextRunAt"] = "2000-01-01T00:00:00.0000000Z",
            };
            jobs.Add(job);
            InvokeRunner("Save", job);
            InvokeRunner("Record", job);
        }

        [TearDown]
        public void TearDown()
        {
            jobs.Clear();
            jobs.AddRange(originalJobs);
            foreach (var entry in replaced) entry.Key.SetValue(null, entry.Value);
            replaced.Clear();
            Directory.Delete(directory, true);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void IdenticalPendingDoesNotReadOrWriteAnyRecordOrIndex(bool hasProgress)
        {
            double? progress = hasProgress ? 0.25 : null;
            Pending(State(1L, false), progress, "Waiting", "retained-owner");
            byte[] executionBefore = File.ReadAllBytes(executions.RecordPath(job));
            byte[] historyBefore = File.ReadAllBytes(HistoryRecordPath());
            string nextRunBefore = (string)job["nextRunAt"];
            using (Lock(executions.RecordPath(job)))
            using (Lock(HistoryRecordPath()))
            using (Lock(IndexPath(executionPath)))
            using (Lock(IndexPath(historyPath)))
                Pending(State(1d, true), progress, "Waiting", "", 60000);
            Assert.That(job["stepCount"], Is.EqualTo(2));
            Assert.That((string)job["nextRunAt"], Is.Not.EqualTo(nextRunBefore));
            Assert.That(job["cleanupToken"], Is.EqualTo("retained-owner"));
            Assert.That(File.ReadAllBytes(executions.RecordPath(job)), Is.EqualTo(executionBefore));
            Assert.That(File.ReadAllBytes(HistoryRecordPath()), Is.EqualTo(historyBefore));
            Assert.That(ReadExecution()["stepCount"], Is.EqualTo(1L));
        }

        [TestCase("state")]
        [TestCase("progress")]
        [TestCase("message")]
        [TestCase("token")]
        [TestCase("cleanup")]
        public void ChangedBusinessCheckpointIsImmediatelyDurable(string change)
        {
            Pending(State(1L, false), null, "Waiting", "first-owner");
            if (change == "cleanup") job["cleanupStatus"] = "none";
            Pending(State(change == "state" ? 2L : 1L, true),
                change == "progress" ? 0.5 : null,
                change == "message" ? "Progressed" : "Waiting",
                change == "token" ? "second-owner" : "first-owner");
            var restored = ReadExecution();
            Assert.That(VmAutomationCanonicalJson.Serialize(restored), Is.EqualTo(VmAutomationCanonicalJson.Serialize(job)));
            var snapshot = (Dictionary<string, object>)ReadHistory()["snapshot"];
            Assert.That(snapshot["stepCount"], Is.EqualTo(2L));
            Assert.That(snapshot["cleanupToken"], Is.EqualTo(job["cleanupToken"]));
            Assert.That(snapshot.ContainsKey("jobAccessToken"), Is.False);
            if (change == "progress") Assert.That(snapshot["progress"], Is.EqualTo(0.5));
            if (change == "message") Assert.That(snapshot["statusMessage"], Is.EqualTo("Progressed"));
        }

        [Test]
        public void CapturedStateDoesNotAliasTheCallbackAndFirstEmptyCheckpointIsDurable()
        {
            job.Remove("jobState");
            Pending(new Dictionary<string, object>(), null, "");
            Assert.That(ReadExecution()["jobState"], Is.InstanceOf<Dictionary<string, object>>());
            using (Lock(executions.RecordPath(job)))
            using (Lock(HistoryRecordPath()))
                Pending(new Dictionary<string, object>(), null, " ");
            var source = State(1L, false);
            Pending(source, null, "Waiting");
            string captured = VmAutomationCanonicalJson.Serialize(job["jobState"]);
            ((Dictionary<string, object>)source["nested"])["value"] = 9L;
            Assert.That(VmAutomationCanonicalJson.Serialize(job["jobState"]), Is.EqualTo(captured));
            Assert.That(VmAutomationCanonicalJson.Serialize(ReadExecution()["jobState"]), Is.EqualTo(captured));
        }

        [Test]
        public void IdenticalCheckpointStillRequiresTheActualAsyncCleanupAdmission()
        {
            Pending(State(1L, false), null, "Waiting", "owner-token");
            job["asyncCleanupContract"] = VmProjectToolCleanupReceipt.Contract;
            var failure = Assert.Throws<TargetInvocationException>(() =>
                Pending(State(1L, false), null, "Waiting", "owner-token"));
            Assert.That(failure.InnerException, Is.InstanceOf<VmProjectToolException>());
            Assert.That(job["stepCount"], Is.EqualTo(1));
            Assert.That(ReadExecution().ContainsKey("asyncCleanupContract"), Is.False);
        }

        [Test]
        public void ReloadInterruptsTheLastCheckpointWithoutReplayingLivePulses()
        {
            Pending(State(1L, false), null, "Waiting");
            Pending(State(1d, true), null, "Waiting");
            jobs.Clear();
            jobs.AddRange(new VmAutomationJobRecordStore(executionPath).Load());
            InvokeRunner("RecoverInterruptedJobs");
            var restored = ReadExecution();
            Assert.That(restored["status"], Is.EqualTo("interrupted"));
            Assert.That(restored["stepCount"], Is.EqualTo(1L));
            Assert.That(VmAutomationCanonicalJson.Serialize(restored["jobState"]), Is.EqualTo(VmAutomationCanonicalJson.Serialize(State(1L, false))));
            Assert.That(((Dictionary<string, object>)ReadHistory()["snapshot"])["status"], Is.EqualTo("interrupted"));
        }

        [Test]
        public void CancellationCompletionAndCleanupRemainImmediatelyDurable()
        {
            Pending(State(1L, false), null, "Waiting");
            Pending(State(1d, true), null, "Waiting");
            VmAutomationPersistentJobRunner.Cancel(new Dictionary<string, object>
                { ["jobId"] = job["jobId"], ["jobAccessToken"] = job["jobAccessToken"] });
            Assert.That(ReadExecution()["cancellationRequested"], Is.True);
            InvokeRunner("CompleteJobImmediately", job, "canceled", null, null);
            Assert.That(ReadExecution()["status"], Is.EqualTo("canceled"));
            InvokeRunner("CompleteCleanup", job, "succeeded", new Dictionary<string, object> { ["released"] = true }, null);
            Assert.That(ReadExecution()["cleanupStatus"], Is.EqualTo("succeeded"));
            var snapshot = (Dictionary<string, object>)ReadHistory()["snapshot"];
            Assert.That(snapshot["status"], Is.EqualTo("canceled"));
            Assert.That(((Dictionary<string, object>)snapshot["cleanupResult"])["released"], Is.True);
        }

        private void Pending(Dictionary<string, object> state, double? progress, string message, string token = "", int delay = 0)
            => InvokeRunner("PersistPendingStep", job, VmProjectToolJobStep.Pending(state, progress, message, delay, token));
        private Dictionary<string, object> ReadExecution() => new VmAutomationJobRecordStore(executionPath).Load()[0];
        private Dictionary<string, object> ReadHistory() => new VmAutomationJobRecordStore(historyPath).Load()[0];
        private string HistoryRecordPath() => history.RecordPath(ReadHistory());
        private static string IndexPath(string path) => Path.Combine(Path.ChangeExtension(path, "records"), "index.json");
        private static FileStream Lock(string path) => new(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        private void Replace(FieldInfo field, object value) { replaced.Add(field, field.GetValue(null)); field.SetValue(null, value); }
        private static FieldInfo RunnerField(string name) => typeof(VmAutomationPersistentJobRunner).GetField(name, PrivateStatic);
        private static FieldInfo HistoryField(string name) => typeof(VmAutomationJobHistory).GetField(name, PrivateStatic);
        private static void InvokeRunner(string name, params object[] args) => typeof(VmAutomationPersistentJobRunner).GetMethod(name, PrivateStatic).Invoke(null, args);
        private static void InvokeHistory(string name) => typeof(VmAutomationJobHistory).GetMethod(name, PrivateStatic).Invoke(null, null);
        private static Dictionary<string, object> State(object value, bool reverse)
        {
            var nested = new Dictionary<string, object> { ["value"] = value };
            return reverse ? new Dictionary<string, object> { ["ordered"] = new object[] { 1L, 2L }, ["nested"] = nested }
                : new Dictionary<string, object> { ["nested"] = nested, ["ordered"] = new object[] { 1L, 2L } };
        }
    }
}
