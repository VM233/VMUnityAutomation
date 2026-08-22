using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace VMUnityAutomation.Editor.Tests
{
    internal sealed class VmGameViewCaptureContractTests
    {
        private static readonly string[] RunningCaptureFields =
        {
            "path",
            "fullPath",
            "superSize",
            "width",
            "height",
            "sizeBytes",
            "waitFrames",
            "stableFrames",
            "elapsedMs",
            "fileReady",
            "editorOverlayMode",
            "editorOverlaysSuppressed",
            "gameViewGizmosSuppressed",
            "gameViewStatsSuppressed",
            "sanitizedGameViewCount",
            "editorOverlayStateRestored",
        };

        private static readonly string[] PausedCaptureFields =
        {
            "paused",
            "window",
            "floating",
            "coordinateMode",
            "captureMethod",
            "contentRect",
            "warning",
        };

        [Test]
        public void ScreenshotGameOutputSchema_DeclaresEverySuccessField()
        {
            Assert.That(
                VmAutomationGeneratedRouteContracts.TryGetOutput(
                    "screenshot/game", out Dictionary<string, object> schema),
                Is.True);
            Assert.That(schema["additionalProperties"], Is.False);

            var properties =
                (Dictionary<string, object>)schema["properties"];
            var required = (List<string>)schema["required"];
            foreach (string field in RunningCaptureFields)
            {
                Assert.That(properties.ContainsKey(field), Is.True,
                    $"Running capture field '{field}' is absent from the " +
                    "closed screenshot/game output schema.");
                Assert.That(required, Does.Contain(field),
                    $"Running capture field '{field}' must be required.");
            }

            foreach (string field in PausedCaptureFields)
            {
                Assert.That(properties.ContainsKey(field), Is.True,
                    $"Paused capture field '{field}' is absent from the " +
                    "closed screenshot/game output schema.");
            }
        }
    }

    internal sealed class VmPackageTestContractTests
    {
        private static readonly string[] JobFields =
        {
            "jobId",
            "jobAccessToken",
            "jobType",
            "status",
            "pollRoute",
            "pollArgs",
            "packageName",
            "mode",
            "assemblies",
            "startedAt",
            "updatedAt",
            "compilationDiagnostics",
            "tags",
            "testJobId",
            "error",
            "testResult",
        };

        [Test]
        public void PackageTestOutputSchemas_DeclareDurableJobFields()
        {
            Assert.That(
                VmAutomationGeneratedRouteContracts.TryGetOutput(
                    "testing/run-package-tests",
                    out Dictionary<string, object> runSchema),
                Is.True);
            var variants = (List<object>)runSchema["oneOf"];
            Dictionary<string, object> runJobSchema = variants
                .Cast<Dictionary<string, object>>()
                .Single(candidate =>
                    ((Dictionary<string, object>)candidate["properties"])
                    .ContainsKey("jobId"));
            AssertJobFields(runJobSchema);

            Assert.That(
                VmAutomationGeneratedRouteContracts.TryGetOutput(
                    "testing/get-package-job",
                    out Dictionary<string, object> statusSchema),
                Is.True);
            AssertJobFields(statusSchema);
            var statusProperties =
                (Dictionary<string, object>)statusSchema["properties"];
            Assert.That(statusProperties.ContainsKey("cleared"), Is.True);
        }

        private static void AssertJobFields(
            Dictionary<string, object> schema)
        {
            Assert.That(schema["additionalProperties"], Is.False);
            var properties =
                (Dictionary<string, object>)schema["properties"];
            foreach (string field in JobFields)
            {
                Assert.That(properties.ContainsKey(field), Is.True,
                    $"Package-test job field '{field}' is absent from its " +
                    "closed output schema.");
            }
        }
    }
}
