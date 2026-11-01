using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace VMUnityAutomation.Editor.Tests
{
    public sealed class VmEditorOverlayTests
    {
        [Test]
        public void MissingWindowCannotCreateOrFocusAnyWindow()
        {
            var before = Resources.FindObjectsOfTypeAll<EditorWindow>();
            var focus = EditorWindow.focusedWindow;
            var error = Assert.Throws<VmProjectToolException>(() => new VmEditorOverlayTool().Execute(
                new VmEditorOverlayRequest { WindowInstanceId = "0", OverlayId = "missing", Displayed = true }));
            Assert.That(error.ErrorCode, Is.EqualTo("editor_window_not_found"));
            CollectionAssert.AreEquivalent(before, Resources.FindObjectsOfTypeAll<EditorWindow>());
            Assert.That(EditorWindow.focusedWindow, Is.SameAs(focus));
        }

        [Test]
        public void MissingOverlayCannotCreateOrFocusAnyWindow()
        {
            var windows = Resources.FindObjectsOfTypeAll<EditorWindow>();
            Assert.That(windows, Is.Not.Empty, "The native Editor test workspace must contain an existing view.");
            var focus = EditorWindow.focusedWindow;
            var error = Assert.Throws<VmProjectToolException>(() => new VmEditorOverlayTool().Execute(
                new VmEditorOverlayRequest { WindowInstanceId = VmObjectId.Get(windows[0]), OverlayId = "VMUnityAutomation.Tests.AbsentOverlay", Displayed = true }));
            Assert.That(error.ErrorCode, Is.EqualTo("editor_overlay_not_found"));
            CollectionAssert.AreEquivalent(windows, Resources.FindObjectsOfTypeAll<EditorWindow>());
            Assert.That(EditorWindow.focusedWindow, Is.SameAs(focus));
        }
    }
}
