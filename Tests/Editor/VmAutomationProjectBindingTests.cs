using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;

namespace VMUnityAutomation.Editor.Tests
{
    [Category(VmAutomationPackageTestCommands.DefaultPackageSmokeCategory)]
    public sealed class VmAutomationProjectBindingTests
    {
        private static string ProjectRoot => Directory.GetParent(Application.dataPath).FullName;

        [Test]
        public async Task EquivalentBindingsUseOneRequestIdentity()
        {
            string requestId = Guid.NewGuid().ToString("N");
            var arguments = new Dictionary<string, object>
            {
                { "expectedProjectPath", ProjectRoot.Replace('\\', '/') + "/" }
            };
            var first = await VmAutomationExecutor.ExecuteAsync(
                "editor/state", arguments, requestId: requestId,
                expectedProjectPath: ProjectRoot);
            Assert.That(first.Ok, Is.True, first.Error?.Message);
            Assert.That(arguments["expectedProjectPath"],
                Is.EqualTo(ProjectRoot.Replace('\\', '/') + "/"));

            string alias = Path.Combine(ProjectRoot, "..", Path.GetFileName(ProjectRoot));
            if (Application.platform == RuntimePlatform.WindowsEditor)
                alias = alias.ToUpperInvariant();
            var second = await VmAutomationExecutor.ExecuteAsync(
                "editor/state",
                new Dictionary<string, object> { { "expectedProjectPath", alias } },
                requestId: requestId, expectedProjectPath: ProjectRoot + "/");
            Assert.That(second.Ok, Is.True, second.Error?.Message);
            Assert.That(second, Is.SameAs(first));
        }

        [Test]
        public async Task DifferentBindingSourcesFailBeforeOwnerExecution()
        {
            var result = await VmAutomationExecutor.ExecuteAsync("editor/state",
                new Dictionary<string, object>
                {
                    { "expectedProjectPath", Path.Combine(ProjectRoot, "Other Checkout") }
                }, expectedProjectPath: ProjectRoot);
            Assert.That(result.Ok, Is.False);
            Assert.That(result.Error.Code, Is.EqualTo("argument_conflict"));
        }

        [TestCase("relative-project")]
        [TestCase("D:relative-project")]
        public async Task RelativeBindingsAreRejected(string path)
        {
            if (path.StartsWith("D:", StringComparison.Ordinal) &&
                Application.platform != RuntimePlatform.WindowsEditor)
                Assert.Ignore("Drive-relative paths are a Windows path form.");
            var result = await VmAutomationExecutor.ExecuteAsync("editor/state",
                expectedProjectPath: path);
            Assert.That(result.Ok, Is.False);
            Assert.That(result.Error.Code, Is.EqualTo("invalid_project_path"));
        }

        [Test]
        public async Task AConsistentDifferentProjectStillFailsBinding()
        {
            string otherRoot = Path.Combine(ProjectRoot, "Other Checkout");
            var result = await VmAutomationExecutor.ExecuteAsync("editor/state",
                new Dictionary<string, object> { { "expectedProjectPath", otherRoot } },
                expectedProjectPath: otherRoot);
            Assert.That(result.Ok, Is.False);
            Assert.That(result.Error.Code, Is.EqualTo("project_mismatch"));
        }
    }
}
