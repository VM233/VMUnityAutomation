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
            Assert.That(mode["enum"], Is.EquivalentTo(new[] { "auto", "print-window", "screen" }));
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
    }
}
