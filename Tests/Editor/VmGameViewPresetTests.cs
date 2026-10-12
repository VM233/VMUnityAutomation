using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace VMUnityAutomation.Editor.Tests
{
    internal sealed class VmGameViewPresetTests
    {
        [SetUp]
        public void AdoptSavedNativePreferences() => VmGameViewPreferencesSource.instance.Reload();

        [Test]
        public void RepeatedSessionResizesRetireOnePresetAndRestoreNativeSelection()
        {
            var native = new VmGameViewNative();
            var window = native.SingleWindow();
            object selection = native.SelectedSize(window);
            Vector2 zoom = native.Zoom(window);
            string before = native.Inventory().Fingerprint;
            var windows = Resources.FindObjectsOfTypeAll<EditorWindow>();
            var focus = EditorWindow.focusedWindow;
            var session = VmGameViewResolutionSession.instance;
            try
            {
                var started = session.Execute(new VmGameViewSessionRequest
                { Action = VmGameViewSessionAction.Begin, Width = 1237, Height = 2469 });
                Assert.That(started.Active, Is.True);
                for (int i = 0; i < 16; i++)
                {
                    var resized = session.Execute(new VmGameViewSessionRequest
                    { Action = VmGameViewSessionAction.Resize, SessionId = started.SessionId, Width = 1237 + i, Height = 2469 + i });
                    Assert.That(resized.View.Width, Is.EqualTo(1237 + i));
                    Assert.That(resized.View.Height, Is.EqualTo(2469 + i));
                    Assert.That(native.Count, Is.LessThanOrEqualTo(started.Inventory.Presets.Length));
                }
                session.Execute(new VmGameViewSessionRequest
                { Action = VmGameViewSessionAction.End, SessionId = started.SessionId });
            }
            finally { if (session.Active) session.End(); }
            Assert.That(native.Inventory().Fingerprint, Is.EqualTo(before));
            Assert.That(native.SelectedSize(window), Is.SameAs(selection));
            Assert.That(native.Zoom(window), Is.EqualTo(zoom));
            CollectionAssert.AreEquivalent(windows, Resources.FindObjectsOfTypeAll<EditorWindow>());
            Assert.That(EditorWindow.focusedWindow, Is.SameAs(focus));
        }

        [Test]
        public void FailedResizeRetiresSessionAndLifecycleRetirementUsesTheSameOwner()
        {
            var native = new VmGameViewNative();
            var window = native.SingleWindow();
            object selection = native.SelectedSize(window);
            Vector2 zoom = native.Zoom(window);
            string before = native.Inventory().Fingerprint;
            var session = VmGameViewResolutionSession.instance;
            try
            {
                var started = session.Execute(new VmGameViewSessionRequest
                { Action = VmGameViewSessionAction.Begin, Width = 1243, Height = 2471 });
                var error = Assert.Throws<VmProjectToolException>(() => session.Execute(new VmGameViewSessionRequest
                { Action = VmGameViewSessionAction.Resize, SessionId = started.SessionId }));
                Assert.That(error.ErrorCode, Is.EqualTo("invalid_arguments"));
                Assert.That(session.Active, Is.False);
                session.Execute(new VmGameViewSessionRequest
                { Action = VmGameViewSessionAction.Begin, Width = 1243, Height = 2471 });
                VmGameViewResolutionSession.Retire();
            }
            finally { if (session.Active) session.End(); }
            Assert.That(native.Inventory().Fingerprint, Is.EqualTo(before));
            Assert.That(native.SelectedSize(window), Is.SameAs(selection));
            Assert.That(native.Zoom(window), Is.EqualTo(zoom));
        }

        [Test]
        public void ExistingResolutionReusesBuiltinAndUnknownResolutionDoesNotCreate()
        {
            var native = new VmGameViewNative();
            var window = native.SingleWindow();
            object selection = native.SelectedSize(window);
            Vector2 zoom = native.Zoom(window);
            var inventory = native.Inventory();
            VmGameViewPreset preset = Array.Find(inventory.Presets, value => value.Builtin && value.SizeType == "FixedResolution");
            Assert.That(preset, Is.Not.Null);
            try
            {
                VmGameViewCommands.SetResolution(new Dictionary<string, object> { { "width", preset.Width }, { "height", preset.Height } });
                Assert.That(native.SelectedIndex(window), Is.EqualTo(preset.Index));
                var missing = (Dictionary<string, object>)VmGameViewCommands.SetResolution(
                    new Dictionary<string, object> { { "width", 1213 }, { "height", 2437 } });
                Assert.That(missing["errorCode"], Is.EqualTo("game_view_resolution_unavailable"));
            }
            finally
            {
                native.Select(window, native.Indices()[selection]);
                native.RestoreZoom(window, zoom);
            }
            Assert.That(native.Inventory().Fingerprint, Is.EqualTo(inventory.Fingerprint));
        }

        [TestCase("stale", "game_view_presets_changed")]
        [TestCase("builtin", "game_view_builtin_preset")]
        [TestCase("selected", "game_view_selected_preset")]
        public void RejectedRemovalLeavesTheWholeTableAndWindowUnchanged(string kind, string code)
        {
            var native = new VmGameViewNative();
            var window = native.SingleWindow();
            var inventory = native.Inventory();
            int selectedIndex = native.SelectedIndex(window);
            int index = kind == "builtin" ? 0 : selectedIndex;
            Assert.That(kind != "selected" || index >= native.BuiltinCount, Is.True,
                "The selected-preset witness requires an existing custom selection.");
            var result = (Dictionary<string, object>)VmGameViewCommands.RemovePresets(new Dictionary<string, object>
            {
                { "expectedFingerprint", kind == "stale" ? "stale" : inventory.Fingerprint },
                { "expectedPreferenceFileHash", inventory.PreferenceFileHash },
                { "presets", new[] { VmJsonContract.ToTransportValue(inventory.Presets[index]) } }
            });
            Assert.That(result["errorCode"], Is.EqualTo(code));
            Assert.That(native.Inventory().Fingerprint, Is.EqualTo(inventory.Fingerprint));
            Assert.That(native.SelectedIndex(window), Is.EqualTo(selectedIndex));
        }

        [Test]
        public void ResolutionInputRejectsTaskLabelsAndFractionalPixels()
        {
            var schema = VmAutomationToolInputSchemaCatalog.Get("gameview/set-resolution");
            var properties = (Dictionary<string, object>)schema["properties"];
            Assert.That(properties.ContainsKey("label"), Is.False);
            var scaleSchema = VmAutomationToolInputSchemaCatalog.Get("gameview/set-scale");
            Assert.That(((Dictionary<string, object>)scaleSchema["properties"]).ContainsKey("fallbackScale"), Is.False);
            Assert.That(((Dictionary<string, object>)properties["width"])["type"], Is.EqualTo("integer"));
            Assert.That(VmAutomationGeneratedRouteContracts.TryGetOutput("gameview/resolution-session", out var output), Is.True);
            Assert.That(output["additionalProperties"], Is.False);
            Assert.That(VmAutomationToolDescriptionCatalog.Get("gameview/remove-presets"), Does.Contain("OS-user-wide"));
        }
    }
}
