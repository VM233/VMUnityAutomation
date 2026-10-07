using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace VMUnityAutomation.Editor.Tests
{
    [Category(VmAutomationPackageTestCommands.DefaultPackageSmokeCategory)]
    [Category(VmAutomationPackageTestCommands.FullPackageRegressionCategory)]
    public sealed class VmEditorViewCaptureTests
    {
#if UNITY_EDITOR_WIN
        private sealed class CaptureWindow : EditorWindow { }

        [UnityTest]
        public IEnumerator SelectedNativeViewCapturesCurrentRetainedColorsAndRestoresTheRenderTarget()
        {
            var window = ScriptableObject.CreateInstance<CaptureWindow>();
            string path = Path.Combine(Path.GetTempPath(), "vm-editor-view-" + System.Guid.NewGuid().ToString("N") + ".png");
            var control = new VisualElement();
            control.style.position = Position.Absolute;
            control.style.left = 10;
            control.style.top = 10;
            control.style.width = 30;
            control.style.height = 30;
            control.style.backgroundColor = Color.blue;
            window.rootVisualElement.Add(control);
            window.position = new Rect(100, 100, 160, 120);
            window.Show();
            try
            {
                yield return null;
                yield return null;
                CaptureAndAssert(window, control, Color.red, path);
                CaptureAndAssert(window, control, Color.green, path);
            }
            finally
            {
                window.Close();
                if (File.Exists(path))
                    File.Delete(path);
            }
        }

        private static void CaptureAndAssert(EditorWindow window, VisualElement control, Color expected, string path)
        {
            window.rootVisualElement.style.backgroundColor = expected;
            RenderTexture previousActive = RenderTexture.active;
            var result = (Dictionary<string, object>)VmAutomationScreenshotCommands.CaptureEditorWindow(
                new Dictionary<string, object>
                {
                    { "window", window.GetType().FullName }, { "captureMode", "view" },
                    { "path", path }, { "maxDimension", 1024 },
                });
            Assert.That(result["success"], Is.True, result.TryGetValue("error", out object error) ? error.ToString() : "");
            Assert.That(result["captureMethod"], Is.EqualTo("editor-view"));
            Assert.That(result["targetWindowVerified"], Is.True);
            Assert.That(RenderTexture.active, Is.SameAs(previousActive));
            var geometry = (Dictionary<string, object>)result["captureGeometry"];
            Assert.That(geometry["editorWindowInstanceId"], Is.EqualTo(VmObjectId.Get(window)));
            var image = new Texture2D(2, 2);
            try
            {
                Assert.That(image.LoadImage(File.ReadAllBytes(path)), Is.True);
                AssertColor(image.GetPixel(image.width / 2, image.height / 2), expected);
                Rect root = window.rootVisualElement.worldBound;
                Vector2 point = control.worldBound.center;
                var content = (Dictionary<string, object>)result["contentRect"];
                int x = (int)content["x"] + Mathf.RoundToInt((point.x - root.x) / root.width * (int)content["width"]);
                int y = image.height - 1 - (int)content["y"] - Mathf.RoundToInt((point.y - root.y) / root.height * (int)content["height"]);
                AssertColor(image.GetPixel(x, y), Color.blue);
            }
            finally
            {
                Object.DestroyImmediate(image);
            }
        }

        private static void AssertColor(Color actual, Color expected)
        {
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(1f / 255));
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(1f / 255));
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(1f / 255));
        }
#endif
    }
}
