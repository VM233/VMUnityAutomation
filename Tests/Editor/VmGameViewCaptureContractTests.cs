using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace VMUnityAutomation.Editor.Tests
{
    [Category(VmAutomationPackageTestCommands.DefaultPackageSmokeCategory)]
    [Category(VmAutomationPackageTestCommands.FullPackageRegressionCategory)]
    internal sealed class VmGameViewCaptureContractTests
    {
        [TestCase("info")]
        [TestCase("resolution")]
        [TestCase("scale")]
        public void MissingGameViewDoesNotCreateOrFocusAWindow(string operation)
        {
            var gameViewType = typeof(UnityEditor.Editor).Assembly.GetType("UnityEditor.GameView");
            Assert.That(gameViewType, Is.Not.Null);
            Assert.That(Resources.FindObjectsOfTypeAll(gameViewType), Is.Empty,
                "The regression witness requires a workspace with no Game View.");
            var windowsBefore = Resources.FindObjectsOfTypeAll<EditorWindow>();
            var focusBefore = EditorWindow.focusedWindow;
            var arguments = new Dictionary<string, object>
            {
                { "width", 1200 }, { "height", 2640 }, { "scale", 1f }
            };
            object result;
            switch (operation)
            {
                case "info": result = VmAutomationScreenshotCommands.GetGameViewInfo(arguments); break;
                case "resolution": result = VmAutomationScreenshotCommands.SetGameViewResolution(arguments); break;
                case "scale": result = VmAutomationScreenshotCommands.SetGameViewScale(arguments); break;
                default: throw new System.ArgumentOutOfRangeException(nameof(operation));
            }
            var error = (Dictionary<string, object>)result;
            Assert.That(error["success"], Is.False);
            Assert.That(error["errorCode"], Is.EqualTo("game_view_unavailable"));
            CollectionAssert.AreEquivalent(windowsBefore, Resources.FindObjectsOfTypeAll<EditorWindow>());
            Assert.That(EditorWindow.focusedWindow, Is.SameAs(focusBefore));
            Assert.That(Resources.FindObjectsOfTypeAll(gameViewType), Is.Empty);
        }

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

    [Category(VmAutomationPackageTestCommands.DefaultPackageSmokeCategory)]
    [Category(VmAutomationPackageTestCommands.FullPackageRegressionCategory)]
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
