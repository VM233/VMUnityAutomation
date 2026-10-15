using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;

namespace VMUnityAutomation.Editor.Tests
{
    [Category(VmAutomationPackageTestCommands.DefaultPackageSmokeCategory)]
    [Category(VmAutomationPackageTestCommands.FullPackageRegressionCategory)]
    public sealed class VmProjectToolAsyncCleanupTests
    {
        [Test]
        public void RetiredActualOwnerReportsItsInterruptionWithoutInventingADomainReload()
        {
            using var fixture = new RunnerFixture();
            var actual = new TaskCompletionSource<VmProjectToolCleanupReceipt>();
            fixture.Admit(actual.Task);
            actual.SetResult(fixture.Receipt(true));
            fixture.Tick();
            Assert.That(fixture.Job["status"], Is.EqualTo("interrupted"));
            Assert.That(fixture.Job["statusMessage"], Is.EqualTo("Interrupted."));
            var error = (Dictionary<string, object>)fixture.Job["error"];
            Assert.That(error["errorCode"], Is.EqualTo("job_interrupted"));
            Assert.That(error["message"], Is.EqualTo("The actual owner retired before publishing a source result."));
            Assert.That(fixture.Job["cleanupStatus"], Is.EqualTo("succeeded"));
            Assert.That(fixture.Released, Is.True);
        }

        [Test]
        public void ActualTaskIsPublishedBeforeSourceAndCannotBeReplaced()
        {
            using var fixture = new RunnerFixture();
            var actual = new TaskCompletionSource<VmProjectToolCleanupReceipt>();
            fixture.Admit(actual.Task);
            var different = new TaskCompletionSource<VmProjectToolCleanupReceipt>();
            var failure = Assert.Throws<VmProjectToolException>(() =>
                fixture.Publish(different.Task));
            Assert.That(failure.ErrorCode, Is.EqualTo("project_tool_cleanup_task_mismatch"));
            fixture.CompleteSource("succeeded");
            Assert.That(fixture.Job["status"], Is.EqualTo("running"));
            Assert.That(fixture.Job["cleanupStatus"], Is.EqualTo("running"));
            Assert.That(fixture.Job.ContainsKey("cleanupResult"), Is.False);
            actual.SetResult(fixture.Receipt(true));
            fixture.Tick();
            Assert.That(fixture.Job["status"], Is.EqualTo("succeeded"));
            Assert.That(fixture.Released, Is.True);
        }

        [Test]
        public void RunningCleanupCancelsAndJoinsTheSameActualTask()
        {
            using var fixture = new RunnerFixture();
            var actual = new TaskCompletionSource<VmProjectToolCleanupReceipt>();
            fixture.Admit(actual.Task);
            VmAutomationPersistentJobRunner.RequestCleanup(fixture.Arguments);
            Assert.That(fixture.Job["cancellationRequested"], Is.True);
            Assert.That(fixture.Job["status"], Is.EqualTo("running"));
            Assert.That(fixture.Job["cleanupStatus"], Is.EqualTo("running"));
            VmAutomationPersistentJobRunner.RequestCleanup(fixture.Arguments);
            Assert.That(FixtureTool.ActualTask, Is.SameAs(actual.Task));
            actual.SetResult(fixture.Receipt(true));
            fixture.Tick();
            Assert.That(fixture.Job["status"], Is.EqualTo("canceled"));
            Assert.That(fixture.Job["cleanupStatus"], Is.EqualTo("succeeded"));
            Assert.That(fixture.Released, Is.True);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ConstructorFailureAfterAdmissionRetainsCleanupUntilOwnedSourceEnds(bool createStream)
        {
            using var fixture = new RunnerFixture();
            var actual = new TaskCompletionSource<VmProjectToolCleanupReceipt>();
            fixture.Admit(actual.Task);
            using var stream = createStream ? new MemoryStream(new byte[16]) : null;
            var primary = new InvalidOperationException("constructor-prefix");
            fixture.CompleteSource("failed", VmProjectToolCleanupReceipt.FailureResponse(primary));
            Assert.That(fixture.Job["status"], Is.EqualTo("running"));
            Assert.That(fixture.Job.ContainsKey("cleanupResult"), Is.False);
            stream?.Dispose();
            actual.SetResult(fixture.Receipt(true, primary));
            fixture.Tick();
            Assert.That(fixture.Job["status"], Is.EqualTo("failed"));
            Assert.That(fixture.Job["cleanupStatus"], Is.EqualTo("failed"));
            Assert.That(fixture.Released, Is.True);
            Assert.That(actual.Task.Result.OriginalFailure, Is.SameAs(primary));
            Assert.That(Assert.Throws<InvalidOperationException>(actual.Task.Result.ThrowIfFailed), Is.SameAs(primary));
        }

        [Test]
        public void TerminalActualOwnerNeedsNoLiveScopeLookup()
        {
            using var fixture = new RunnerFixture();
            var actual = new TaskCompletionSource<VmProjectToolCleanupReceipt>();
            fixture.Admit(actual.Task);
            actual.SetResult(fixture.Receipt(true));
            int before = FixtureTool.CleanupCalls;
            fixture.CompleteSource("succeeded");
            Assert.That(FixtureTool.CleanupCalls, Is.EqualTo(before));
            Assert.That(fixture.Released, Is.True);
            VmAutomationPersistentJobRunner.RequestCleanup(fixture.Arguments);
            Assert.That(FixtureTool.CleanupCalls, Is.EqualTo(before));
            Assert.That(fixture.Job["cleanupStatus"], Is.EqualTo("succeeded"));
        }

        [Test]
        public void PendingFirstOwnerKeepsSecondAcceptedRequestQueued()
        {
            using var fixture = new RunnerFixture();
            var second = fixture.AddJob("queued");
            var actual = new TaskCompletionSource<VmProjectToolCleanupReceipt>();
            fixture.Admit(actual.Task);
            fixture.CompleteSource("succeeded");
            fixture.Tick();
            Assert.That(second["status"], Is.EqualTo("queued"));
            Assert.That(second.ContainsKey("startedAt"), Is.False);
            actual.SetResult(fixture.Receipt(true));
            fixture.Tick();
            Assert.That(fixture.Job["status"], Is.EqualTo("succeeded"));
            Assert.That(second["status"], Is.EqualTo("queued"), "Terminal consumption must not execute a second request in that callback.");
        }

        [Test]
        public void LateCancellationDoesNotRewritePendingSourceAsCompletedSuccess()
        {
            using var fixture = new RunnerFixture();
            var actual = new TaskCompletionSource<VmProjectToolCleanupReceipt>();
            fixture.Admit(actual.Task);
            fixture.CompleteSource("succeeded");
            fixture.Job["cancellationRequested"] = true;
            actual.SetResult(fixture.Receipt(true));
            fixture.Tick();
            Assert.That(fixture.Job["status"], Is.EqualTo("canceled"));
            Assert.That(fixture.Released, Is.True);
        }

        [Test]
        public void CurrentLifetimeReloadGateUsesReleaseReceiptAndNeverRetroactivelyClassifiesHistory()
        {
            using var fixture = new RunnerFixture();
            var historical = fixture.AddJob("succeeded");
            historical["resourceOwnerLifetime"] = "earlier-domain";
            historical["startedAt"] = DateTime.UtcNow.ToString("O");
            var unstarted = fixture.AddJob("queued");
            var actual = new TaskCompletionSource<VmProjectToolCleanupReceipt>();
            fixture.Admit(actual.Task);
            var blocked = VmAutomationPersistentJobRunner.QualifyDomainReload();
            Assert.That(VmAutomationResponse.TryGetError(blocked, out _, out var code, out _), Is.True);
            Assert.That(code, Is.EqualTo("project_tool_resources_not_retired"));
            actual.SetResult(fixture.Receipt(true));
            fixture.CompleteSource("succeeded");
            var qualified = VmAutomationPersistentJobRunner.QualifyDomainReload();
            Assert.That(VmAutomationResponse.TryGetError(qualified, out _, out _, out _), Is.False);
            Assert.That((List<object>)qualified["retiredProjectToolJobs"], Does.Contain(fixture.Job["jobId"]));
            Assert.That((List<object>)qualified["historicalUnknownOwnerJobs"], Does.Contain(historical["jobId"]));
            Assert.That((List<object>)qualified["unstartedProjectToolJobs"], Does.Contain(unstarted["jobId"]));
        }

        [Test]
        public void ForeignReceiptRetainsItsPrimaryFailureWithoutAdoptingItsReleaseFact()
        {
            using var fixture = new RunnerFixture();
            var actual = new TaskCompletionSource<VmProjectToolCleanupReceipt>();
            fixture.Admit(actual.Task);
            var primary = new InvalidOperationException("original-foreign-source");
            var foreign = new VmProjectToolCleanupReceipt("foreign-job", "foreign-token", true,
                new Dictionary<string, object> { ["ResourcesReleased"] = true }, primary);
            actual.SetResult(foreign);
            fixture.CompleteSource("succeeded");
            Assert.That(fixture.Job["status"], Is.EqualTo("failed"));
            Assert.That(fixture.Job["cleanupResult"], Is.Null);
            AssertCleanupFailures(fixture, "original-foreign-source", "project_tool_cleanup_identity_mismatch");
            Assert.That(foreign.OriginalFailure, Is.SameAs(primary));
            Assert.That(VmAutomationResponse.TryGetError(VmAutomationPersistentJobRunner.QualifyDomainReload(),
                out _, out var code, out _), Is.True);
            Assert.That(code, Is.EqualTo("project_tool_resources_not_retired"));
        }

        [Test]
        public void InvalidResultSchemaPreservesPrimaryFailureAndIndependentActualRelease()
        {
            using var fixture = new RunnerFixture();
            var actual = new TaskCompletionSource<VmProjectToolCleanupReceipt>();
            fixture.Admit(actual.Task);
            var primary = new InvalidOperationException("original-schema-source");
            var receipt = new VmProjectToolCleanupReceipt((string)fixture.Job["jobId"], fixture.Token, true,
                new Dictionary<string, object> { ["ResourcesReleased"] = "invalid-boolean" }, primary);
            actual.SetResult(receipt);
            fixture.CompleteSource("succeeded");
            Assert.That(fixture.Job["status"], Is.EqualTo("failed"));
            Assert.That(fixture.Released, Is.True);
            AssertCleanupFailures(fixture, "original-schema-source", "project_tool_cleanup_output_schema_mismatch");
            Assert.That(receipt.OriginalFailure, Is.SameAs(primary));
            Assert.That(VmAutomationResponse.TryGetError(VmAutomationPersistentJobRunner.QualifyDomainReload(),
                out _, out _, out _), Is.False);
        }

        [Test]
        public void RequestTaskMismatchIsSecondaryToTheActualReceiptFailure()
        {
            using var fixture = new RunnerFixture();
            var actual = new TaskCompletionSource<VmProjectToolCleanupReceipt>();
            fixture.Admit(actual.Task);
            var different = new TaskCompletionSource<VmProjectToolCleanupReceipt>();
            FixtureTool.ActualTask = different.Task;
            fixture.CompleteSource("succeeded");
            var primary = new InvalidOperationException("original-request-source");
            var receipt = fixture.Receipt(true, primary);
            actual.SetResult(receipt);
            fixture.Tick();
            Assert.That(fixture.Job["status"], Is.EqualTo("failed"));
            Assert.That(fixture.Released, Is.True);
            AssertCleanupFailures(fixture, "original-request-source", "project_tool_cleanup_task_mismatch");
            Assert.That(receipt.OriginalFailure, Is.SameAs(primary));
            different.SetCanceled();
        }

        private static void AssertCleanupFailures(RunnerFixture fixture, string primary, string secondaryCode)
        {
            var error = (Dictionary<string, object>)fixture.Job["cleanupError"];
            var failures = (List<object>)error["failures"];
            Assert.That(failures.Count, Is.EqualTo(2));
            Assert.That(((Dictionary<string, object>)failures[0])["message"].ToString(), Does.Contain(primary));
            Assert.That(((Dictionary<string, object>)failures[1])["errorCode"], Is.EqualTo(secondaryCode));
        }

        private static readonly string InputSchema = "{\"type\":\"object\",\"properties\":{\"cleanupToken\":{\"type\":\"string\"}},\"required\":[\"cleanupToken\"],\"additionalProperties\":false}";
        private static readonly string OutputSchema = "{\"type\":\"object\",\"properties\":{\"ResourcesReleased\":{\"type\":\"boolean\"}},\"required\":[\"ResourcesReleased\"],\"additionalProperties\":false}";
        private static object Cleanup(Dictionary<string, object> request) => new Dictionary<string, object> { ["ResourcesReleased"] = true };

        private sealed class FixtureTool : IVmProjectToolAsyncCleanup
        {
            internal static Task<VmProjectToolCleanupReceipt> ActualTask;
            internal static int CleanupCalls;
            public FixtureTool() { }
            public object Execute(Dictionary<string, object> arguments) => throw new InvalidOperationException("The fixture requires its admitted task.");
            public VmProjectToolJobStep ExecuteJobStep(Dictionary<string, object> args, Dictionary<string, object> state) =>
                throw new InvalidOperationException("No queued fixture source may execute in this qualification.");
            public Task<VmProjectToolCleanupReceipt> CleanupJobAsync(string jobID, string token)
            { CleanupCalls++; return ActualTask; }
        }

        private sealed class RunnerFixture : IDisposable
        {
            private readonly string directory = Path.Combine(Path.GetTempPath(), "VMAsyncCleanup", Guid.NewGuid().ToString("N"));
            private readonly Dictionary<FieldInfo, object> originals = new();
            private readonly List<Dictionary<string, object>> jobs;
            private readonly Dictionary<string, object>[] originalJobs;
            private readonly Task<VmProjectToolCleanupReceipt> originalFixtureTask;
            private readonly int originalCleanupCalls;
            public Dictionary<string, object> Job { get; }
            public string Token { get; } = Guid.NewGuid().ToString("N");
            public Dictionary<string, object> Arguments => new() { ["jobId"] = Job["jobId"] };
            public bool Released => (bool)((Dictionary<string, object>)Job["cleanupResult"])["ResourcesReleased"];
            public RunnerFixture()
            {
                Assert.That(Field(typeof(VmAutomationPersistentJobRunner), "cleanupWork").GetValue(null), Is.Null,
                    "The focused fixture requires an idle actual project-tool resource owner.");
                jobs = (List<Dictionary<string, object>>)Field(typeof(VmAutomationPersistentJobRunner), "Jobs").GetValue(null);
                originalJobs = jobs.ToArray();
                originalFixtureTask = FixtureTool.ActualTask; originalCleanupCalls = FixtureTool.CleanupCalls;
                try
                {
                    Set(typeof(VmAutomationPersistentJobRunner), "ticking", true);
                    Set(typeof(VmAutomationPersistentJobRunner), "loaded", true);
                    Set(typeof(VmAutomationPersistentJobRunner), "recordStore", new VmAutomationJobRecordStore(Path.Combine(directory, "runner.json")));
                    Set(typeof(VmAutomationPersistentJobRunner), "cleanupWork", null);
                    Set(typeof(VmAutomationPersistentJobRunner), "currentJobId", null);
                    Set(typeof(VmAutomationJobHistory), "entries", new List<Dictionary<string, object>>());
                    Set(typeof(VmAutomationJobHistory), "recordStore", new VmAutomationJobRecordStore(Path.Combine(directory, "history.json")));
                    Set(typeof(VmAutomationJobHistory), "publishedState", null);
                    var cleanup = VmProjectToolDescriptor.FromMethod(new VmProjectToolAttribute("fixture/cleanup")
                        { ReadOnly = true, InputSchemaJson = InputSchema, OutputSchemaJson = OutputSchema },
                        typeof(VmProjectToolAsyncCleanupTests).GetMethod(nameof(Cleanup), BindingFlags.Static | BindingFlags.NonPublic));
                    var tool = VmProjectToolDescriptor.FromType(new VmProjectToolAttribute("fixture/owner")
                        { ReadOnly = true, CleanupToolName = "fixture/cleanup", LongRunning = true, InputSchemaJson = "{\"type\":\"object\",\"additionalProperties\":false}", OutputSchemaJson = OutputSchema }, typeof(FixtureTool));
                    Assert.That(cleanup.ValidationError, Is.Null.Or.Empty);
                    Assert.That(tool.ValidationError, Is.Null.Or.Empty);
                    Set(typeof(VmProjectToolRegistry), "_cachedProjectTools", new List<VmProjectToolDescriptor> { tool, cleanup });
                    jobs.Clear();
                    Job = AddJob("running");
                    Job["startedAt"] = DateTime.UtcNow.ToString("O");
                }
                catch (Exception primary)
                {
                    try { Dispose(); }
                    catch (Exception cleanup) { throw new AggregateException(primary, cleanup); }
                    throw;
                }
            }
            public Dictionary<string, object> AddJob(string status)
            {
                var job = new Dictionary<string, object>
                {
                    ["jobId"] = Guid.NewGuid().ToString("N"), ["jobType"] = "project-tool", ["operation"] = "fixture/owner",
                    ["ownerAgentId"] = "anonymous", ["status"] = status,
                    ["createdAt"] = DateTime.UtcNow.ToString("O"), ["updatedAt"] = DateTime.UtcNow.ToString("O"),
                    ["cleanupToolName"] = "fixture/cleanup", ["cleanupDeclared"] = true, ["incremental"] = true,
                    ["cleanupStatus"] = "none", ["cancellationRequested"] = false,
                    ["asyncCleanupContract"] = VmProjectToolCleanupReceipt.Contract,
                    ["resourceOwnerLifetime"] = Field(typeof(VmAutomationPersistentJobRunner), "OwnerLifetime").GetValue(null)
                };
                jobs.Add(job); return job;
            }
            public void Admit(Task<VmProjectToolCleanupReceipt> task)
            {
                FixtureTool.ActualTask = task;
                Publish(task);
            }
            public void Publish(Task<VmProjectToolCleanupReceipt> task)
            {
                Field(typeof(VmAutomationPersistentJobRunner), "currentJobId").SetValue(null, Job["jobId"]);
                try { VmProjectToolExecutionContext.PublishCleanupToken(Token, task); }
                finally { Field(typeof(VmAutomationPersistentJobRunner), "currentJobId").SetValue(null, null); }
            }
            public VmProjectToolCleanupReceipt Receipt(bool released, Exception failure = null) =>
                new((string)Job["jobId"], Token, released, new Dictionary<string, object> { ["ResourcesReleased"] = released }, failure);
            public void CompleteSource(string status, object error = null) => Invoke("CompleteJob", Job, status,
                new Dictionary<string, object> { ["ResourcesReleased"] = true }, error);
            public void Tick()
            {
                Field(typeof(VmAutomationPersistentJobRunner), "ticking").SetValue(null, false);
                try { Invoke("Tick"); }
                finally { Field(typeof(VmAutomationPersistentJobRunner), "ticking").SetValue(null, true); }
            }
            private static void Invoke(string method, params object[] args)
            {
                try { typeof(VmAutomationPersistentJobRunner).GetMethod(method, BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, args); }
                catch (TargetInvocationException failure) { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure.InnerException).Throw(); }
            }
            private static FieldInfo Field(Type type, string name) => type.GetField(name, BindingFlags.Static | BindingFlags.NonPublic);
            private void Set(Type type, string name, object value)
            {
                var field = Field(type, name); originals.Add(field, field.GetValue(null)); field.SetValue(null, value);
            }
            public void Dispose()
            {
                jobs.Clear(); jobs.AddRange(originalJobs);
                foreach (var value in originals) value.Key.SetValue(null, value.Value);
                FixtureTool.ActualTask = originalFixtureTask; FixtureTool.CleanupCalls = originalCleanupCalls;
                if (Directory.Exists(directory)) Directory.Delete(directory, true);
            }
        }
    }
}
