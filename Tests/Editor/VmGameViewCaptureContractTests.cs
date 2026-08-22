using System.Collections.Generic;
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
}
