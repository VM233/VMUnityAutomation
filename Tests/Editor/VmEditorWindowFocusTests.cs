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
    }
}
