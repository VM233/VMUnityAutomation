using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using NUnit.Framework;

namespace VMUnityAutomation.Editor.Tests
{
    [Category(VmAutomationPackageTestCommands.DefaultPackageSmokeCategory)]
    [Category(VmAutomationPackageTestCommands.FullPackageRegressionCategory)]
    public sealed class VmBurstCompilationDiagnosticsTests
    {
        private string logPath;
        private static readonly Encoding Utf8 = new UTF8Encoding(false);
        private const string MissingMethod = "Assets/Scripts/Runtime/Diagnostics/RiverContinuityVerification.cs(102,47): " +
            "Burst error BC1055: Unable to resolve the definition of the method StarsToShape.Voxels.WatershedLayout.DrainageEnvelope(double, int)\n";

        [SetUp]
        public void CreateLog()
        {
            logPath = Path.GetTempFileName();
            File.WriteAllText(logPath, "Previous compilation\n" + MissingMethod, Utf8);
        }

        [TearDown]
        public void RemoveLog() => File.Delete(logPath);

        [Test]
        public void FullWindowCapturesEagerAndPostReloadErrorsWithoutPreviousCycleDiagnostics()
        {
            var window = Begin();
            File.AppendAllText(logPath, "Managed compilation started\n" + MissingMethod + "Managed compilation finished\n", Utf8);
            string serialized = MiniJson.Serialize(window.ToDictionary());
            window = VmBurstCompilationDiagnostics.FromDictionary((Dictionary<string, object>)MiniJson.Deserialize(serialized));
            File.AppendAllText(logPath, "Reload completed\n" + MissingMethod + "Native recompilation completed\n", Utf8);
            window.Complete(logPath);
            Assert.That(window.CaptureComplete, Is.True, window.CaptureIssue);
            Assert.That(window.ErrorCount, Is.EqualTo(2));
            Assert.That(window.Entries[0]["file"], Is.EqualTo("Assets/Scripts/Runtime/Diagnostics/RiverContinuityVerification.cs"));
            Assert.That(window.Entries[0]["line"], Is.EqualTo(102));
            Assert.That(window.Entries[0]["column"], Is.EqualTo(47));
            Assert.That(window.Entries[0]["message"], Is.EqualTo(MissingMethod.TrimEnd('\n')));
            Assert.That((long)window.Entries[0]["logByteOffset"], Is.GreaterThanOrEqualTo(window.StartOffset));
            Assert.That(window.BuildFailure()["errorCode"], Is.EqualTo("burst_compilation_failed"));
        }

        [TestCase("Burst error BC1055: missing generated method\n", "error", "", 0)]
        [TestCase("/outside/project/generated.cs(9,2): Burst warning BC1370: exception text\n", "warning", "/outside/project/generated.cs", 9)]
        [TestCase("error BC1045: unsupported native operation\n", "error", "", 0)]
        public void NativeDiagnosticsDoNotDependOnTheManagedSourceAssemblyMap(string diagnostic, string severity, string file, int line)
        {
            var window = Begin();
            File.AppendAllText(logPath, diagnostic, Utf8);
            window.Complete(logPath);
            Assert.That(window.CaptureComplete, Is.True, window.CaptureIssue);
            Assert.That(window.Entries.Count, Is.EqualTo(1));
            Assert.That(window.Entries[0]["severity"], Is.EqualTo(severity));
            Assert.That(window.Entries[0]["file"], Is.EqualTo(file));
            Assert.That(window.Entries[0]["line"], Is.EqualTo(line));
            Assert.That(window.ErrorCount, Is.EqualTo(severity == "error" ? 1 : 0));
            Assert.That(window.WarningCount, Is.EqualTo(severity == "warning" ? 1 : 0));
        }

        [Test]
        public void CompletedQueueWithCleanManagedSnapshotCannotHideANativeError()
        {
            var job = new VmAutomationWorkspaceJob { BurstDiagnostics = Begin(), CompilerErrorCount = 0 };
            File.AppendAllText(logPath, MissingMethod, Utf8);
            var error = VmAutomationBurstCompilation.CompleteDiagnostics(job, logPath);
            Assert.That(error["errorCode"], Is.EqualTo("burst_compilation_failed"));
            Assert.That(job.CompilerErrorCount, Is.Zero);
            Assert.That(job.BurstDiagnostics.ErrorCount, Is.EqualTo(1));
        }

        [Test]
        public void MissingWindowCannotBeInferredFromNativeCompletion()
        {
            Assert.That(VmAutomationBurstCompilation.CompleteDiagnostics(new(), logPath)["errorCode"],
                Is.EqualTo("burst_compilation_diagnostics_incomplete"));
        }

        [Test]
        public void HalfLineAtBeginCannotEstablishCompleteDiagnosticCoverage()
        {
            File.AppendAllText(logPath, "Partially logged message", Utf8);
            var window = Begin();
            Assert.That(window.CaptureIssue, Does.Contain("window start"));
            AssertIncomplete(window);
        }

        [Test]
        public void MultilineNativeDetailsRemainVerbatimFollowingLogContext()
        {
            var window = Begin();
            string firstContext = "While compiling job:\r\n  Generated.Job.Execute()\r\n\r\nUnrelated Editor progress\n";
            string secondContext = "  Full native stack frame\n";
            File.AppendAllText(logPath, MissingMethod + firstContext + MissingMethod + secondContext, Utf8);
            window.Complete(logPath);
            Assert.That(window.CaptureComplete, Is.True, window.CaptureIssue);
            Assert.That(window.Entries[0]["followingLogText"], Is.EqualTo(firstContext));
            Assert.That(window.Entries[1]["followingLogText"], Is.EqualTo(secondContext));
        }

        [Test]
        public void CleanWindowAndWarningOnlyWindowCompleteWithoutAnError()
        {
            var window = Begin();
            File.AppendAllText(logPath, "C# warning CS0618: obsolete method\nNative progress 100%\n", Utf8);
            window.Complete(logPath);
            Assert.That(window.BuildFailure(), Is.Null);
            Assert.That(window.ErrorCount, Is.Zero);
            Assert.That(window.WarningCount, Is.Zero);
            Assert.Throws<InvalidOperationException>(() => window.Complete(logPath));
        }

        [Test]
        public void DurableWorkspaceSerializationRetainsTheWindowAndCompletedFailure()
        {
            var job = new VmAutomationWorkspaceJob
            {
                JobId = "burst-window", JobAccessToken = "fixture", JobType = "asset-refresh",
                Operation = "asset/refresh", OwnerAgentId = "fixture", Status = "running", Phase = "compiling",
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, BurstDiagnostics = Begin(),
            };
            string serialized = MiniJson.Serialize(job.ToDictionary());
            var restored = VmAutomationWorkspaceJob.FromDictionary((Dictionary<string, object>)MiniJson.Deserialize(serialized));
            Assert.That(restored.BurstDiagnostics.Revision, Is.EqualTo(job.BurstDiagnostics.Revision));
            File.AppendAllText(logPath, MissingMethod, Utf8);
            VmAutomationBurstCompilation.CompleteDiagnostics(restored, logPath);
            serialized = MiniJson.Serialize(restored.ToDictionary());
            restored = VmAutomationWorkspaceJob.FromDictionary((Dictionary<string, object>)MiniJson.Deserialize(serialized));
            Assert.That(restored.BurstDiagnostics.CaptureComplete, Is.True);
            Assert.That(restored.BurstDiagnostics.ErrorCount, Is.EqualTo(1));
            Assert.That(restored.BurstDiagnostics.BuildFailure()["errorCode"], Is.EqualTo("burst_compilation_failed"));
        }

        [TestCase("Burst error BC1055: unfinished")]
        [TestCase("Burst internal compiler error: unclassified failure\n")]
        [TestCase("Burst error NEW_FORMAT: changed native format\n")]
        public void IncompleteOrUnrecognizedDiagnosticsCannotBeAccepted(string text)
        {
            var window = Begin();
            File.AppendAllText(logPath, text, Utf8);
            window.Complete(logPath);
            AssertIncomplete(window);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void TruncatedOrRewrittenBoundaryFailsEvenAfterRegrowing(bool regrow)
        {
            var window = Begin();
            File.WriteAllText(logPath, regrow ? new string('x', (int)window.StartOffset + 50) + "\n" : "", Utf8);
            window.Complete(logPath);
            AssertIncomplete(window);
        }

        [Test]
        public void ChangedLogPathAndMissingFileFail()
        {
            var window = Begin();
            window.Complete(logPath + ".different");
            AssertIncomplete(window);
            window = Begin();
            File.Delete(logPath);
            window.Complete(logPath);
            AssertIncomplete(window);
            AssertIncomplete(VmBurstCompilationDiagnostics.Begin(logPath, DateTime.UtcNow));
        }

        [TestCase(1000, true)]
        [TestCase(1001, false)]
        public void RetentionBoundaryNeverReportsPartialCountsAsComplete(int count, bool complete)
        {
            var window = Begin();
            using (var writer = new StreamWriter(logPath, true, Utf8))
                for (int index = 0; index < count; index++)
                    writer.Write(MissingMethod);
            window.Complete(logPath);
            Assert.That(window.CaptureComplete, Is.EqualTo(complete), window.CaptureIssue);
            Assert.That(window.ErrorCount, Is.EqualTo(Math.Min(count, 1000)));
            if (!complete) AssertIncomplete(window);
        }

        [Test]
        public void ByteLineAndTextBudgetsHaveExplicitIncompleteOutcomes()
        {
            var window = Begin();
            using (var stream = new FileStream(logPath, FileMode.Open, FileAccess.Write))
                stream.SetLength(window.StartOffset + VmBurstCompilationDiagnostics.MaximumLogBytes + 1);
            window.Complete(logPath);
            AssertIncomplete(window);

            File.WriteAllText(logPath, "Initial\n", Utf8);
            window = Begin();
            File.AppendAllText(logPath, new string('x', VmBurstCompilationDiagnostics.MaximumLineBytes + 1) + "\n", Utf8);
            window.Complete(logPath);
            AssertIncomplete(window);

            File.WriteAllText(logPath, "Initial\n", Utf8);
            window = Begin();
            using (var writer = new StreamWriter(logPath, true, Utf8))
                for (int index = 0; index < 1000; index++)
                    writer.Write("Burst error BC1055: " + new string('x', 1100) + "\n");
            window.Complete(logPath);
            AssertIncomplete(window);
            Assert.That(window.Entries.Count, Is.LessThan(1000));
        }

        [Test]
        public void ExcessiveLineCountAndInvalidUtf8FailExplicitly()
        {
            var window = Begin();
            File.AppendAllText(logPath, new string('\n', VmBurstCompilationDiagnostics.MaximumLines + 1), Utf8);
            window.Complete(logPath);
            AssertIncomplete(window);
            File.WriteAllText(logPath, "Initial\n", Utf8);
            window = Begin();
            using (var stream = new FileStream(logPath, FileMode.Append, FileAccess.Write))
                stream.Write(new byte[] { 0xFF, (byte)'\n' }, 0, 2);
            window.Complete(logPath);
            AssertIncomplete(window);
        }

        [Test]
        public void Utf8AndCrLfAcrossBlockBoundariesPreserveOffsetsAndDiagnosticText()
        {
            var window = Begin();
            string prefix = new string('x', VmBurstCompilationDiagnostics.MaximumLineBytes - 10) + "\n";
            string diagnostic = "Burst error BC1055: 方法 unavailable\r\n";
            File.AppendAllText(logPath, prefix + diagnostic, Utf8);
            window.Complete(logPath);
            Assert.That(window.CaptureComplete, Is.True, window.CaptureIssue);
            Assert.That(window.Entries[0]["message"], Is.EqualTo(diagnostic.TrimEnd('\r', '\n')));
            Assert.That(window.Entries[0]["logByteOffset"], Is.EqualTo(window.StartOffset + Utf8.GetByteCount(prefix)));
        }

        private VmBurstCompilationDiagnostics Begin() => VmBurstCompilationDiagnostics.Begin(logPath, DateTime.UtcNow);

        private static void AssertIncomplete(VmBurstCompilationDiagnostics window)
        {
            Assert.That(window.CaptureComplete, Is.False);
            Assert.That(window.BuildFailure()["errorCode"], Is.EqualTo("burst_compilation_diagnostics_incomplete"));
        }
    }
}
