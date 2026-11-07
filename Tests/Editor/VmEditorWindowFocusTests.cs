using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace VMUnityAutomation.Editor.Tests
{
    [Category(VmAutomationPackageTestCommands.DefaultPackageSmokeCategory)]
    [Category(VmAutomationPackageTestCommands.FullPackageRegressionCategory)]
    public sealed class VmEditorWindowFocusTests
    {
        [Test]
        public void FocusStateObservesTheActualWindowWithoutCreatingOrFocusing()
        {
            var focused = EditorWindow.focusedWindow;
            var windows = Resources.FindObjectsOfTypeAll<EditorWindow>();
            Assert.That(windows.Length, Is.LessThanOrEqualTo(64), "The frozen focus-state fixture accepts at most 64 existing windows.");
            var actual = new VmEditorWindowFocusStateTool().Execute(new VmEditorWindowFocusStateRequest());
            Assert.That(actual.WindowInstanceId, Is.EqualTo(focused == null ? "" : VmObjectId.Get(focused)));
            Assert.That(actual.WindowType, Is.EqualTo(focused == null ? "" : focused.GetType().FullName));
            Assert.That(actual.WindowTitle, Is.EqualTo(focused == null ? "" : focused.titleContent.text));
            Assert.That(EditorWindow.focusedWindow, Is.SameAs(focused));
            Assert.That(Resources.FindObjectsOfTypeAll<EditorWindow>(), Is.EquivalentTo(windows));
            var attribute = (VmProjectToolAttribute)Attribute.GetCustomAttribute(typeof(VmEditorWindowFocusStateTool), typeof(VmProjectToolAttribute));
            Assert.That(attribute.ReadOnly, Is.True);
            Assert.That(attribute.SideEffects, Is.EqualTo(VmProjectToolSideEffect.ReadsProjectState));
            Assert.That(VmProjectToolDescriptor.FromType(attribute, typeof(VmEditorWindowFocusStateTool)).ValidationError, Is.Null.Or.Empty);
        }

        [Test]
        public void MissingIdentityDoesNotCreateOrFocusAWindow()
        {
            var focused = EditorWindow.focusedWindow;
            var windows = Resources.FindObjectsOfTypeAll<EditorWindow>();
            var error = Assert.Throws<VmProjectToolException>(() =>
                new VmEditorWindowFocusTool().Execute(new VmEditorWindowFocusRequest
                {
                    WindowInstanceId = "0", Maximized = true
                }));
            Assert.That(error.ErrorCode, Is.EqualTo("editor_window_not_found"));
            Assert.That(EditorWindow.focusedWindow, Is.SameAs(focused));
            Assert.That(Resources.FindObjectsOfTypeAll<EditorWindow>(), Is.EquivalentTo(windows));
        }

        [TestCase(typeof(VmEditorWindowFocusTool))]
        [TestCase(typeof(VmEditorWindowFocusClearTool))]
        [TestCase(typeof(VmEditorWindowCloseTool))]
        public void WindowMutationMetadataDoesNotClaimAssetOrProjectFileWrites(Type owner)
        {
            var attribute = (VmProjectToolAttribute)Attribute.GetCustomAttribute(owner, typeof(VmProjectToolAttribute));
            var descriptor = VmProjectToolDescriptor.FromType(attribute, owner);
            Assert.That(descriptor.ValidationError, Is.Null.Or.Empty);
            var effects = ((IEnumerable)descriptor.ToDetailDictionary()["sideEffects"])
                .Cast<string>().ToArray();
            Assert.That(effects, Does.Contain("changesEditorView"));
            Assert.That(effects, Does.Not.Contain("writesProjectFiles"));
            Assert.That(effects, Does.Not.Contain("writesAssets"));
        }

#if UNITY_EDITOR_WIN
        [Test]
        public void NativeNullFocusAndExistingWindowRoundTripPreserveTheWorkspace()
        {
            var previous = EditorWindow.focusedWindow;
            var windows = Resources.FindObjectsOfTypeAll<EditorWindow>();
            Assert.That(windows.Length, Is.LessThanOrEqualTo(64));
            var target = previous != null ? previous : windows.FirstOrDefault(window => window.GetType().FullName == "UnityEditor.GameView");
            if (target == null)
                Assert.Ignore("The native focus round trip requires an existing focused window or Game View; it never creates one.");
            var positions = windows.Select(window => window.position).ToArray();
            try
            {
                new VmEditorWindowFocusTool().Execute(new VmEditorWindowFocusRequest { WindowInstanceId = VmObjectId.Get(target) });
                var cleared = new VmEditorWindowFocusClearTool().Execute(new VmEditorWindowFocusClearRequest());
                Assert.That(cleared.Cleared, Is.True);
                Assert.That(cleared.Previous.WindowInstanceId, Is.EqualTo(VmObjectId.Get(target)));
                Assert.That(cleared.Previous.NativeViewInstanceId, Is.Not.Empty);
                Assert.That(cleared.Current.SystemFocusAvailable, Is.True);
                Assert.That(cleared.Current.NativeKeyboardFocusHandle, Is.EqualTo("0"));
                Assert.That(cleared.Current.NativeViewInstanceId, Is.Empty);
                Assert.That(EditorWindow.focusedWindow, Is.Null);
                var observed = new VmEditorWindowFocusStateTool().Execute(new VmEditorWindowFocusStateRequest());
                Assert.That(observed.WindowInstanceId, Is.Empty);
                Assert.That(observed.NativeViewInstanceId, Is.Empty);
                Assert.That(observed.NativeKeyboardFocusHandle, Is.EqualTo("0"));
                new VmEditorWindowFocusTool().Execute(new VmEditorWindowFocusRequest { WindowInstanceId = VmObjectId.Get(target) });
                Assert.That(EditorWindow.focusedWindow, Is.SameAs(target));
                Assert.That(Resources.FindObjectsOfTypeAll<EditorWindow>(), Is.EquivalentTo(windows));
                Assert.That(windows.Select(window => window.position), Is.EqualTo(positions));
            }
            finally
            {
                if (previous != null)
                    previous.Focus();
                else
                    new VmEditorWindowFocusClearTool().Execute(new VmEditorWindowFocusClearRequest());
            }
        }
#endif
    }
}
