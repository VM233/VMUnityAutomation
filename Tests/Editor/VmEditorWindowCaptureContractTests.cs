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
                "coordinateMode", "contentRect", "centerColorRange", "centerDistinctColorBuckets",
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
