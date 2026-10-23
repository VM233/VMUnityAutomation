using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace VMUnityAutomation.Editor.Tests
{
    public sealed class VmGameViewZoomTests
    {
        [Test]
        public void ReadReportsExistingWindowZoomWithoutCreatingOrFocusingWindows()
        {
            var windows = Resources.FindObjectsOfTypeAll<EditorWindow>();
            var focus = EditorWindow.focusedWindow;
            var native = new VmGameViewNative();
            var window = native.SingleWindow();
            var view = native.Read(window);
            Assert.That(view.ScaleX, Is.GreaterThan(0));
            Assert.That(view.ScaleY, Is.GreaterThan(0));
            var info = (System.Collections.Generic.Dictionary<string, object>)
                VmAutomationScreenshotCommands.GetGameViewInfo(null);
            Assert.That(info["scale"], Is.EqualTo(native.Zoom(window).ToString()));
            CollectionAssert.AreEquivalent(windows, Resources.FindObjectsOfTypeAll<EditorWindow>());
            Assert.That(EditorWindow.focusedWindow, Is.SameAs(focus));
        }

        [Test]
        public void TemporaryDimensionsRestoreOriginalSelectionZoomAndPreferenceFile()
        {
            var native = new VmGameViewNative();
            var window = native.SingleWindow();
            object original = native.SelectedSize(window);
            Vector2 zoom = native.Zoom(window);
            var inventory = native.Inventory();
            var session = VmGameViewResolutionSession.instance;
            try
            {
                var started = session.Execute(new VmGameViewSessionRequest
                {
                    Action = VmGameViewSessionAction.Begin, Width = 1920, Height = 1080
                });
                Assert.That(started.View.RenderWidth, Is.EqualTo(1920));
                Assert.That(started.View.RenderHeight, Is.EqualTo(1080));
                var resized = session.Execute(new VmGameViewSessionRequest
                {
                    Action = VmGameViewSessionAction.Resize, SessionId = started.SessionId,
                    Width = 1600, Height = 1200
                });
                Assert.That(resized.TemporaryPresetOwned, Is.True);
                Assert.That(resized.View.RenderWidth, Is.EqualTo(1600));
                Assert.That(resized.View.RenderHeight, Is.EqualTo(1200));
                session.Execute(new VmGameViewSessionRequest
                {
                    Action = VmGameViewSessionAction.End, SessionId = started.SessionId
                });
            }
            finally
            {
                if (session.Active) session.End();
            }
            Assert.That(native.SelectedSize(window), Is.SameAs(original));
            Assert.That(native.Zoom(window), Is.EqualTo(zoom));
            Assert.That(native.Inventory().Fingerprint, Is.EqualTo(inventory.Fingerprint));
            Assert.That(native.Inventory().PreferenceFileHash, Is.EqualTo(inventory.PreferenceFileHash));
        }
    }
}
