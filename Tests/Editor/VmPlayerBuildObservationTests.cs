using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using NUnit.Framework;

namespace VMUnityAutomation.Editor.Tests
{
    [Category(VmAutomationPackageTestCommands.DefaultPackageSmokeCategory)]
    [Category(VmAutomationPackageTestCommands.FullPackageRegressionCategory)]
    public sealed class VmPlayerBuildObservationTests
    {
        [TestCase("runSeconds", -1d)]
        [TestCase("runSeconds", double.NaN)]
        [TestCase("runSeconds", double.PositiveInfinity)]
        [TestCase("runSeconds", 86401d)]
        [TestCase("windowWaitMs", 0.5d)]
        [TestCase("windowWaitMs", 60001d)]
        [TestCase("logTailLines", 0d)]
        [TestCase("logTailLines", 2001d)]
        public void InvalidObservationArgumentsFailAdmission(string key, double value) =>
            Assert.Throws<VmProjectToolException>(() => new VmPlayerBuildObservationOptions(
                new Dictionary<string, object> { { key, value } }));

        [Test]
        public void FractionalSampleDeadlineYieldsWithoutPublishingUnchangedTicks()
        {
            double now = 0;
            var options = new VmPlayerBuildObservationOptions(new Dictionary<string, object> { { "runSeconds", 0.25d } });
            using var observation = new VmPlayerBuildObservation(Process.GetCurrentProcess(), "test", "", options,
                false, false, _ => throw new AssertionException("Capture was not requested."), () => now);
            for (int i = 0; i < 100; i++) Assert.That(observation.Advance(), Is.False);
            now = 0.249;
            Assert.That(observation.Advance(), Is.False);
            now = 0.25;
            Assert.That(observation.Advance(), Is.True);
            Assert.That(observation.IsTerminal, Is.True);
            Assert.That(observation.Result["success"], Is.True);
            Assert.That(observation.Result["runSeconds"], Is.EqualTo(0.25));
            Assert.That(observation.Result["terminatedAfter"], Is.False);
            Assert.That(observation.Advance(), Is.False);
        }

        [Test]
        public void CancellationReleasesOnlyItsHandleAndRetainsExactIdentity()
        {
            using var process = Process.GetCurrentProcess();
            var observation = new VmPlayerBuildObservation(Process.GetCurrentProcess(), "test", "",
                new VmPlayerBuildObservationOptions(new Dictionary<string, object>()), false, false, null, () => 0);
            observation.Fail("Canceled by owner.");
            observation.Dispose();
            Assert.That(process.HasExited, Is.False);
            Assert.That(observation.Result["processId"], Is.EqualTo(process.Id));
            Assert.That(observation.Result["startedAt"], Is.EqualTo(process.StartTime.ToUniversalTime().ToString("O")));
            Assert.That(observation.Result["success"], Is.False);
        }

#if UNITY_EDITOR_WIN
        [Test]
        public void MissingWindowYieldsThenFailsAndStillHonorsExplicitTermination()
        {
            double now = 0;
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),
                    "WindowsPowerShell", "v1.0", "powershell.exe"),
                Arguments = "-NoProfile -NonInteractive -Command Start-Sleep -Seconds 60",
                UseShellExecute = false, CreateNoWindow = true,
            });
            using var observation = new VmPlayerBuildObservation(process, "test", "",
                new VmPlayerBuildObservationOptions(new Dictionary<string, object>
                { { "runSeconds", 0d }, { "windowWaitMs", 1000 } }), true, true,
                _ => throw new AssertionException("A headless process has no window."), () => now);
            try
            {
                Assert.That(observation.Advance(), Is.True);
                Assert.That(observation.Result["phase"], Is.EqualTo("waiting-for-window"));
                Assert.That(observation.Advance(), Is.False);
                now = 1;
                Assert.That(observation.Advance(), Is.True);
                Assert.That(observation.Result["phase"], Is.EqualTo("terminating"));
                Assert.That(process.WaitForExit(10000), Is.True);
                Assert.That(observation.Advance(), Is.True);
                Assert.That(observation.IsTerminal, Is.True);
                Assert.That(observation.Result["success"], Is.False);
                Assert.That(observation.Result["terminatedAfter"], Is.True);
            }
            finally { if (!process.HasExited) process.Kill(); }
        }

        [Test]
        public void EarlyNativeNonzeroExitCannotBecomeSuccessfulObservation()
        {
            var process = Process.Start(new ProcessStartInfo
            {
                FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe"),
                Arguments = "/d /c exit 7", UseShellExecute = false, CreateNoWindow = true,
            });
            using var observation = new VmPlayerBuildObservation(process, "test", "",
                new VmPlayerBuildObservationOptions(new Dictionary<string, object>()), false, false, null, () => 0);
            Assert.That(process.WaitForExit(10000), Is.True);
            observation.Advance();
            Assert.That(observation.IsTerminal, Is.True);
            Assert.That(observation.Result["success"], Is.False);
            Assert.That(observation.Result["exitCode"], Is.EqualTo("7"));
            Assert.That((string)observation.Result["error"], Does.Contain("native exit code 7"));
        }
#endif

        [Test]
        public void LogTailReadsOnlyItsBoundAndReportsTruncation()
        {
            string path = Path.GetTempFileName();
            try
            {
                File.WriteAllText(path, new string('x', VmPlayerLogTail.MaximumBytes + 20) + "\nlast 中文\n", new UTF8Encoding(false));
                var result = VmPlayerLogTail.Read(path, 2);
                Assert.That(result["text"], Is.EqualTo("last 中文"));
                Assert.That(result["bytesRead"], Is.EqualTo(VmPlayerLogTail.MaximumBytes));
                Assert.That(result["truncated"], Is.True);
                File.WriteAllText(path, "one\ntwo\n", new UTF8Encoding(false));
                Assert.That(VmPlayerLogTail.Read(path, 2)["truncated"], Is.False);
                Assert.That(VmPlayerLogTail.Read(path, 1)["truncated"], Is.True);
            }
            finally { File.Delete(path); }
        }
    }
}
