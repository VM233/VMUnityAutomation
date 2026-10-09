using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine.UIElements;

namespace VMUnityAutomation.Editor.Tests
{
    [Category(VmAutomationPackageTestCommands.DefaultPackageSmokeCategory)]
    [Category(VmAutomationPackageTestCommands.FullPackageRegressionCategory)]
    public sealed class VmEditorWindowCaptureContractTests
    {
        [Test]
        public void AutoReadsCurrentRetainedContentWithoutTitleHeuristics()
        {
            var window = UnityEngine.ScriptableObject.CreateInstance<EditorWindow>();
            try
            {
                window.titleContent = new UnityEngine.GUIContent("UI Builder");
                window.rootVisualElement.Clear();
                Assert.That(VmAutomationScreenshotCommands.ResolveEditorWindowCaptureMode("auto", window),
                    Is.EqualTo("print-window"));
                window.titleContent.text = "Custom retained window";
                window.rootVisualElement.Add(new VisualElement());
                Assert.That(VmAutomationScreenshotCommands.ResolveEditorWindowCaptureMode("auto", window),
                    Is.EqualTo("screen"));
                window.rootVisualElement.Clear();
                Assert.That(VmAutomationScreenshotCommands.ResolveEditorWindowCaptureMode("auto", window),
                    Is.EqualTo("print-window"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(window);
            }
        }

        [Test]
        public void ExplicitCaptureModeDoesNotInspectOrRetryAnotherSurface()
        {
            Assert.That(VmAutomationScreenshotCommands.ResolveEditorWindowCaptureMode("screen", null),
                Is.EqualTo("screen"));
            Assert.That(VmAutomationScreenshotCommands.ResolveEditorWindowCaptureMode("print-window", null),
                Is.EqualTo("print-window"));
            Assert.That(VmAutomationScreenshotCommands.ResolveEditorWindowCaptureMode("view", null),
                Is.EqualTo("view"));
            Assert.That(VmAutomationScreenshotCommands.ResolveEditorWindowCaptureMode("offscreen", null),
                Is.Empty);
        }

        [Test]
        public void ScreenEvidenceRequiresTheExactForegroundWindowAndConsumerReceipt()
        {
            var target = new System.IntPtr(17);
            Assert.That(VmAutomationScreenshotCommands.IsScreenCaptureTargetForeground(target, target), Is.True);
            Assert.That(VmAutomationScreenshotCommands.IsScreenCaptureTargetForeground(target,
                new System.IntPtr(18)), Is.False);
            Assert.That(VmAutomationScreenshotCommands.IsScreenCaptureTargetForeground(System.IntPtr.Zero,
                System.IntPtr.Zero), Is.False);

            Assert.That(VmAutomationUIBuilderPreviewCommands.HasVerifiedTargetWindow(
                new Dictionary<string, object> { { "targetWindowVerified", true } }), Is.True);
            Assert.That(VmAutomationUIBuilderPreviewCommands.HasVerifiedTargetWindow(
                new Dictionary<string, object>()), Is.False);
            Assert.That(VmAutomationUIBuilderPreviewCommands.HasVerifiedTargetWindow(
                new Dictionary<string, object> { { "targetWindowVerified", false } }), Is.False);
        }

        [Test]
        public void BuilderPreviewDetectsTextCrossingIntoTheFollowingEntry()
        {
            var first = new UnityEngine.Rect(0, 0, 200, 100);
            var next = new UnityEngine.Rect(0, 110, 200, 100);

            Assert.That(VmAutomationUIBuilderPreviewCommands.TryMeasurePreviewTextOverlap(
                first, new UnityEngine.Rect(10, 80, 180, 50), next, out float overlap), Is.True);
            Assert.That(overlap, Is.EqualTo(20).Within(0.01f));
            Assert.That(VmAutomationUIBuilderPreviewCommands.TryMeasurePreviewTextOverlap(
                first, new UnityEngine.Rect(10, 60, 180, 40), next, out _), Is.False);
            Assert.That(VmAutomationUIBuilderPreviewCommands.TryMeasurePreviewTextOverlap(
                first, new UnityEngine.Rect(210, 80, 20, 50), next, out _), Is.False);
        }

        [Test]
        public void BuilderPreviewContractExposesLayoutOverlapEvidence()
        {
            Assert.That(VmAutomationGeneratedRouteContracts.TryGetOutput(
                "uitoolkit/builder-preview", out var output), Is.True);
            var properties = (Dictionary<string, object>)output["properties"];
            var preview = (Dictionary<string, object>)properties["preview"];
            var previewProperties = (Dictionary<string, object>)preview["properties"];
            Assert.That(previewProperties.Keys, Does.Contain("previewTextOverlapCount"));
            Assert.That(previewProperties.Keys, Does.Contain("previewTextOverlaps"));
            Assert.That((System.Collections.IEnumerable)preview["required"],
                Does.Contain("previewTextOverlapCount"));
        }

        [Test]
        public void PublicContractDescribesCaptureModesAndActualWindowsResult()
        {
            var input = VmAutomationToolInputSchemaCatalog.Get("screenshot/editor-window");
            var properties = (Dictionary<string, object>)input["properties"];
            var mode = (Dictionary<string, object>)properties["captureMode"];
            Assert.That(mode["enum"], Is.EquivalentTo(new[] { "auto", "print-window", "screen", "view" }));
            Assert.That(VmAutomationGeneratedRouteContracts.TryGetOutput("screenshot/editor-window", out var output), Is.True);
            var result = (Dictionary<string, object>)output["properties"];
            Assert.That(result.Keys, Is.EquivalentTo(new[]
            {
                "path", "window", "floating", "width", "height", "sizeBytes", "captureMethod",
                "targetWindowVerified", "coordinateMode", "captureGeometry", "contentRect", "centerColorRange", "centerDistinctColorBuckets",
                "centerVisuallyBlank", "warning"
            }));
            Assert.That(output["required"], Is.EquivalentTo(result.Keys));
            Assert.That(output["additionalProperties"], Is.EqualTo(false));
        }

        [Test]
        public void CaptureDeclaresFileAndEditorViewMutations()
        {
            var profile = VmAutomationToolProfileCatalog.Get("screenshot/editor-window");
            Assert.That(profile.ReadOnly, Is.False);
            Assert.That(profile.SideEffects, Does.Contain("writesScreenshotFiles"));
            Assert.That(profile.SideEffects, Does.Contain("changesEditorView"));
        }

        [Test]
        public void ForegroundObservationIsClosedAndOptionalForOtherCaptureSurfaces()
        {
            VmAutomationGeneratedRouteContracts.TryGetOutput("screenshot/editor-window", out var output);
            var result = (Dictionary<string, object>)output["properties"];
            var geometry = (Dictionary<string, object>)result["captureGeometry"];
            var surfaces = (System.Collections.IList)geometry["oneOf"];
            geometry = (Dictionary<string, object>)surfaces[0];
            var properties = (Dictionary<string, object>)geometry["properties"];
            foreach (string phase in new[] { "foregroundBeforeCapture", "foregroundAfterCapture" })
            {
                var observation = (Dictionary<string, object>)properties[phase];
                var fields = (Dictionary<string, object>)observation["properties"];
                Assert.That(fields.Keys, Is.EquivalentTo(new[] { "nativeWindow", "processId", "title" }));
                Assert.That(observation["required"], Is.EquivalentTo(fields.Keys));
                Assert.That(observation["additionalProperties"], Is.EqualTo(false));
                Assert.That((System.Collections.IEnumerable)geometry["required"], Does.Not.Contain(phase));
            }
        }

        [Test]
        public void BuilderPreservesRejectedNativeCaptureWithoutClaimingBlankPixels()
        {
            const string message = "The target Editor window could not be verified as the foreground window.";
            var screenshot = VmAutomationResponse.Error(message, "target_window_unverified");
            var foreground = new Dictionary<string, object>
            {
                { "nativeWindow", "17" }, { "processId", 42 }, { "title", "Observed foreground" }
            };
            var geometry = new Dictionary<string, object>
            {
                { "processId", 74896 }, { "foregroundBeforeCapture", foreground }
            };
            screenshot["captureGeometry"] = geometry;
            var analysis = new Dictionary<string, object>
            {
                { "visualValid", false }, { "documentVisuallyBlank", null },
                { "conclusive", false }, { "reason", "screenshot_capture_failed" }
            };
            var result = new Dictionary<string, object> { { "success", true }, { "screenshot", screenshot } };

            VmAutomationUIBuilderPreviewCommands.ApplyScreenshotFailure(result, screenshot, analysis);

            Assert.That(result["success"], Is.False);
            Assert.That(result["errorCode"], Is.EqualTo("target_window_unverified"));
            Assert.That(result["error"], Is.EqualTo(message));
            Assert.That(result["screenshot"], Is.SameAs(screenshot));
            Assert.That(screenshot["captureGeometry"], Is.SameAs(geometry));
            Assert.That(geometry["foregroundBeforeCapture"], Is.SameAs(foreground));
            Assert.That(analysis["documentVisuallyBlank"], Is.Null);
        }

        [Test]
        public void InsufficientDocumentPixelsProduceNoBlanknessConclusion()
        {
            var pixels = new UnityEngine.Color32[64 * 64];
            var analysis = VmAutomationUIBuilderPreviewCommands.AnalyzeUIBuilderPixels(
                pixels, 64, 64, new UnityEngine.RectInt(0, 0, 1, 1),
                new UnityEngine.RectInt(0, 0, 64, 64));
            Assert.That(analysis["conclusive"], Is.False);
            Assert.That(analysis["documentVisuallyBlank"], Is.Null);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void MeasuredBlanknessRetainsItsBooleanMeaning(bool visibleDocument)
        {
            var pixels = new UnityEngine.Color32[64 * 64];
            for (int y = 0; y < 64; y++)
            for (int x = 0; x < 64; x++)
                pixels[y * 64 + x] = visibleDocument && x >= 16 && x < 48 && y >= 16 && y < 48
                    ? new UnityEngine.Color32(255, 0, 0, 255)
                    : new UnityEngine.Color32(100, 100, 100, 255);
            var analysis = VmAutomationUIBuilderPreviewCommands.AnalyzeUIBuilderPixels(
                pixels, 64, 64, new UnityEngine.RectInt(16, 16, 32, 32),
                new UnityEngine.RectInt(0, 0, 64, 64));

            Assert.That(analysis["conclusive"], Is.True);
            Assert.That(analysis["documentVisuallyBlank"], Is.EqualTo(!visibleDocument));
            Assert.That(analysis["visualValid"], Is.EqualTo(visibleDocument));
        }

        [Test]
        public void BuilderContractDeclaresMutationsRequiredHostAndNullableBlankness()
        {
            var profile = VmAutomationToolProfileCatalog.Get("uitoolkit/builder-preview");
            Assert.That(profile.ReadOnly, Is.False);
            Assert.That(profile.SideEffects, Does.Contain("writesScreenshotFiles"));
            Assert.That(profile.SideEffects, Does.Contain("changesEditorView"));
            var input = VmAutomationToolInputSchemaCatalog.Get("uitoolkit/builder-preview");
            Assert.That((System.Collections.IEnumerable)input["required"], Does.Contain("uxmlPath"));

            VmAutomationGeneratedRouteContracts.TryGetOutput("uitoolkit/builder-preview", out var output);
            var properties = (Dictionary<string, object>)output["properties"];
            var visualAnalysis = (Dictionary<string, object>)properties["visualAnalysis"];
            var visualProperties = (Dictionary<string, object>)visualAnalysis["properties"];
            var blankness = (Dictionary<string, object>)visualProperties["documentVisuallyBlank"];
            Assert.That(blankness["oneOf"], Is.Not.Null);
        }

        [TestCase("auto")]
        [TestCase("screen")]
        [TestCase("print-window")]
        [TestCase("view")]
        public void BuilderPassesTheDeclaredCaptureSurfaceToTheScreenshotOwner(string mode)
        {
            var input = VmAutomationToolInputSchemaCatalog.Get("uitoolkit/builder-preview");
            var properties = (Dictionary<string, object>)input["properties"];
            var captureMode = (Dictionary<string, object>)properties["captureMode"];
            Assert.That(captureMode["enum"], Does.Contain(mode));
            var args = new Dictionary<string, object> { { "captureMode", mode }, { "maxDimension", 1024 } };
            var screenshot = VmAutomationUIBuilderPreviewCommands.BuildScreenshotArguments(args, "preview.png");
            Assert.That(screenshot["captureMode"], Is.EqualTo(mode));
            Assert.That(screenshot["window"], Is.EqualTo("UI Builder"));
            Assert.That(screenshot["path"], Is.EqualTo("preview.png"));
            Assert.That(screenshot["maxDimension"], Is.EqualTo(1024));
        }

        [Test]
        public void BuilderPreservesScreenCaptureWhenTheSurfaceIsOmitted()
        {
            var screenshot = VmAutomationUIBuilderPreviewCommands.BuildScreenshotArguments(
                new Dictionary<string, object>(), "preview.png");
            Assert.That(screenshot["captureMode"], Is.EqualTo("screen"));
        }

        [Test]
        public void BuilderRejectsTheClippedNativeFitPredecessor()
        {
            var viewport = new UnityEngine.Rect(301, 46, 858, 773);
            var predecessor = new UnityEngine.Rect(515, 77, 395.3424f, 869.7533f);
            var framed = new UnityEngine.Rect(570, 91, 320, 704);
            Assert.That(VmAutomationUIBuilderPreviewCommands.IsViewportFramed(predecessor, viewport, 1), Is.False);
            Assert.That(VmAutomationUIBuilderPreviewCommands.IsViewportFramed(framed, viewport, 1), Is.True);
            Assert.That(VmAutomationUIBuilderPreviewCommands.WorldRectsAgree(framed, predecessor, 1), Is.False);
            Assert.That(VmAutomationUIBuilderPreviewCommands.WorldRectsAgree(framed, framed, 1), Is.True);
        }

        [Test]
        public void BuilderRejectsViewportMotionAndUnpublishedBounds()
        {
            var bounds = new UnityEngine.Rect(301, 46, 858, 773);
            var moving = new UnityEngine.Rect(301, 51, 858, 773);
            Assert.That(VmAutomationUIBuilderPreviewCommands.WorldRectsAgree(bounds, moving, 1), Is.False);
            Assert.That(VmAutomationUIBuilderPreviewCommands.WorldRectsAgree(bounds, default, 1), Is.False);
            Assert.That(VmAutomationUIBuilderPreviewCommands.IsViewportFramed(bounds, default, 1), Is.False);
        }

        [TestCase(0.5f, true)]
        [TestCase(2f, false)]
        public void BuilderUsesOnlyPhysicalPixelTolerance(float offset, bool accepted)
        {
            var viewport = new UnityEngine.Rect(0, 0, 400, 800);
            var document = new UnityEngine.Rect(offset, 0, 400, 800);
            Assert.That(VmAutomationUIBuilderPreviewCommands.IsViewportFramed(document, viewport, 1), Is.EqualTo(accepted));
            Assert.That(VmAutomationUIBuilderPreviewCommands.WorldRectsAgree(document, viewport, 1), Is.EqualTo(accepted));
        }
    }
}
