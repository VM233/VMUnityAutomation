using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace VMUnityAutomation.Editor.Tests
{
    public sealed class VmEditorKeyboardTests
    {
        private sealed class KeyboardWindow : EditorWindow { }

        [Test]
        public void SharesBoundedNativeSchemaAndDeclaresEditorEffects()
        {
            Assert.That(VmAutomationCatalog.TryGetTool("uitoolkit/editor-keyboard", true, out var contract), Is.True);
            Assert.That(contract["errorCodes"], Does.Contain("ui_keyboard_window_unavailable"));
            var schema = VmAutomationToolInputSchemaCatalog.Get("uitoolkit/editor-keyboard");
            var events = new List<object> { Key("Down", "字") };
            Assert.That(Valid(schema, events), Is.True);
            events[0] = Key("Down", "two"); Assert.That(Valid(schema, events), Is.False);
            events[0] = Key("Down"); ((Dictionary<string, object>)events[0])["keyCode"] = "UnknownKey";
            Assert.That(Valid(schema, events), Is.False);
            events.Clear(); for (int i = 0; i < 64; i++) events.Add(Key("Down"));
            Assert.That(Valid(schema, events), Is.True);
            events.Add(Key("Up")); Assert.That(Valid(schema, events), Is.False);
        }

        [UnityTest]
        public IEnumerator EditsNativeFocusedFieldAndRejectsWrongObjectWithoutInput()
        {
            var prior = EditorWindow.focusedWindow;
            var window = ScriptableObject.CreateInstance<KeyboardWindow>();
            var texture = new Texture2D(1, 1);
            window.position = new Rect(100, 100, 420, 220);
            var field = new TextField { name = "NativeEditorText", value = "seed" }; field.style.height = 60;
            window.rootVisualElement.Add(field);
            int changes = 0; field.RegisterValueChangedCallback(_ => changes++);
            window.Show();
            try
            {
                yield return null; yield return null;
                var events = new List<object> { Key("Down", "", "A", true), Key("Up", "", "A", true), Key("Down", "b", "B"), Key("Up", "", "B") };
                var args = new Dictionary<string, object> { { "windowInstanceId", VmObjectId.Get(texture) }, { "events", events } };
                var rejected = (Dictionary<string, object>)VmAutomationUIToolkitCommands.DispatchEditorKeyboard(args);
                Assert.That(rejected["errorCode"], Is.EqualTo("ui_keyboard_window_unavailable")); Assert.That(changes, Is.Zero);
                var point = field.worldBound.center;
                new VmEditorPointerTool().Execute(new VmEditorPointerRequest { WindowInstanceId = VmObjectId.Get(window), Phase = VmEditorPointerPhase.Click, X = point.x, Y = point.y });
                args["windowInstanceId"] = VmObjectId.Get(window);
                var result = (Dictionary<string, object>)VmAutomationUIToolkitCommands.DispatchEditorKeyboard(args);
                Assert.That(result["dispatchedEvents"], Is.EqualTo(4)); Assert.That(field.value, Is.EqualTo("b")); Assert.That(changes, Is.GreaterThan(0));
                field.Blur(); Assert.That(window.rootVisualElement.focusController.focusedElement, Is.Null);
                rejected = (Dictionary<string, object>)VmAutomationUIToolkitCommands.DispatchEditorKeyboard(args);
                Assert.That(rejected["errorCode"], Is.EqualTo("ui_keyboard_focus_unavailable")); Assert.That(rejected["dispatchedEvents"], Is.Zero);
            }
            finally { Object.DestroyImmediate(texture); window.Close(); if (prior != null) prior.Focus(); }
        }

        private static bool Valid(Dictionary<string, object> schema, List<object> events) =>
            VmAutomationInputValidator.TryValidate(new Dictionary<string, object> { { "windowInstanceId", "0" }, { "events", events } }, schema, out _, out _, out _);

        private static Dictionary<string, object> Key(string phase, string character = "", string key = "None", bool control = false) =>
            new() { { "phase", phase }, { "character", character }, { "keyCode", key },
                { "modifiers", control ? new List<object> { "Control" } : new List<object>() } };
    }
}
