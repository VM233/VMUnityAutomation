using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace VMUnityAutomation.Editor.Tests
{
    [Category(VmAutomationPackageTestCommands.DefaultPackageSmokeCategory)]
    [Category(VmAutomationPackageTestCommands.FullPackageRegressionCategory)]
    public sealed class VmAutomationBuiltInRejectionTests
    {
        [TestCase("runSeconds", -0.1d)]
        [TestCase("logTailLines", 2001d)]
        [TestCase("windowWaitMs", 60001d)]
        public async Task BuildAdmissionPreservesOwnerRejectionWithoutConsoleException(string key, double value)
        {
            var result = await VmAutomationExecutor.ExecuteAsync("build/start",
                new Dictionary<string, object>
                {
                    { "outputPath", "Temp/RejectedPlayer.exe" },
                    { key, value },
                }, expectedProjectPath: System.IO.Path.GetDirectoryName(Application.dataPath));
            Assert.That(result.Ok, Is.False);
            Assert.That(result.Error.Code, Is.EqualTo("invalid_player_launch_arguments"));
            Assert.That(result.Error.Message, Does.Contain(key));
            Assert.That(result.Error.Retryable, Is.False);
            Assert.That(result.Error.Details.ContainsKey("exceptionType"), Is.False);
            Assert.That(result.Error.Details.ContainsKey("jobId"), Is.False);
            LogAssert.NoUnexpectedReceived();
        }
    }
}
