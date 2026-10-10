using System;
using System.Collections.Generic;
using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace VMUnityAutomation.Editor.Tests
{
    [Category(VmAutomationPackageTestCommands.DefaultPackageSmokeCategory)]
    [Category(VmAutomationPackageTestCommands.FullPackageRegressionCategory)]
    public sealed class VmUIToolkitNativeHierarchyTests
    {
        private sealed class PointerWindow : EditorWindow { }

        [UnityTest]
        public IEnumerator PointerDispatchRunsNativeClickableAndSliderCapture()
        {
            var window = ScriptableObject.CreateInstance<PointerWindow>();
            window.titleContent = new GUIContent("Native UI Pointer Test");
            window.position = new Rect(100, 100, 420, 220);
            int clicks = 0;
            var button = new Button(() => clicks++) { text = "Action" };
            button.style.height = 60;
            var slider = new Slider(0, 100);
            slider.style.height = 80;
            window.rootVisualElement.Add(button);
            window.rootVisualElement.Add(slider);
            window.Show();
            try
            {
                yield return null;
                yield return null;
                var root = window.rootVisualElement;
                var point = button.worldBound.center;
                Assert.That(root.panel.Pick(point), Is.SameAs(button));
                VmAutomationUIToolkitCommands.DispatchNativePointer(button, point, "Down");
                VmAutomationUIToolkitCommands.DispatchNativePointer(button, point, "Up");
                Assert.That(clicks, Is.EqualTo(1));
                var track = slider.Q(className: "unity-base-slider__tracker").worldBound;
                var start = new Vector2(track.xMin + track.width * .2f, track.center.y);
                var end = new Vector2(track.xMin + track.width * .8f, track.center.y);
                VmAutomationUIToolkitCommands.DispatchNativePointer(root.panel.Pick(start), start, "Down");
                VmAutomationUIToolkitCommands.DispatchNativePointer(root.panel.Pick(end), end, "Move");
                VmAutomationUIToolkitCommands.DispatchNativePointer(root.panel.Pick(end), end, "Up");
                Assert.That(slider.value, Is.GreaterThan(50));
            }
            finally { window.Close(); }
        }

        [UnityTest]
        public IEnumerator ClickPairsNativeCaptureAndReleasesAfterTargetRemoval()
        {
            var window = ScriptableObject.CreateInstance<PointerWindow>();
            window.position = new Rect(100, 100, 420, 220);
            int clicks = 0;
            int down = 0;
            int up = 0;
            var button = new Button(() => clicks++) { text = "Short click" };
            button.style.height = 60;
            var root = window.rootVisualElement;
            root.RegisterCallback<PointerDownEvent>(_ => down++, TrickleDown.TrickleDown);
            root.RegisterCallback<PointerUpEvent>(_ => up++, TrickleDown.TrickleDown);
            root.Add(button);
            window.Show();
            try
            {
                yield return null;
                yield return null;
                VmAutomationUIToolkitCommands.DispatchNativePointer(button, button.worldBound.center, "Click");
                Assert.That(clicks, Is.EqualTo(1));
                Assert.That(down, Is.EqualTo(1));
                Assert.That(up, Is.EqualTo(1));
                Assert.That(button.HasPointerCapture(PointerId.mousePointerId), Is.False);
                VmAutomationUIToolkitCommands.DispatchNativePointer(button, button.worldBound.center, "Click");
                Assert.That(clicks, Is.EqualTo(2));
                Assert.That(down, Is.EqualTo(2));
                Assert.That(up, Is.EqualTo(2));
                int removedUp = 0;
                button.RegisterCallback<PointerUpEvent>(_ => removedUp++);
                button.RegisterCallback<PointerDownEvent>(_ => button.RemoveFromHierarchy());
                VmAutomationUIToolkitCommands.DispatchNativePointer(button, button.worldBound.center, "Click");
                Assert.That(button.panel, Is.Null);
                Assert.That(down, Is.EqualTo(3));
                Assert.That(up, Is.EqualTo(3));
                Assert.That(removedUp, Is.Zero, "Release must use the current attached panel target.");
                Assert.That(root.panel.GetCapturingElement(PointerId.mousePointerId), Is.Not.SameAs(button));
            }
            finally { window.Close(); }
        }

        [UnityTest]
        public IEnumerator PublicPointerPreservesForeignDocumentCapture()
        {
            var active = SceneManager.GetActiveScene();
            bool dirty = active.isDirty;
            int sceneCount = SceneManager.sceneCount;
            var preview = EditorSceneManager.NewPreviewScene();
            var window = ScriptableObject.CreateInstance<PointerWindow>();
            window.position = new Rect(100, 100, 420, 220);
            GameObject firstObject = null;
            GameObject secondObject = null;
            try
            {
                firstObject = new GameObject("Captured Document") { hideFlags = HideFlags.HideAndDontSave };
                secondObject = new GameObject("Requested Document") { hideFlags = HideFlags.HideAndDontSave };
                SceneManager.MoveGameObjectToScene(firstObject, preview);
                SceneManager.MoveGameObjectToScene(secondObject, preview);
                var firstDocument = firstObject.AddComponent<UIDocument>();
                var secondDocument = secondObject.AddComponent<UIDocument>();
                var first = new Button { text = "Captured" };
                var second = new Button { text = "Requested" };
                first.style.height = second.style.height = 60;
                window.Show();
                Assert.That(firstDocument.rootVisualElement, Is.Not.Null);
                Assert.That(secondDocument.rootVisualElement, Is.Not.Null);
                firstDocument.rootVisualElement.Add(first);
                secondDocument.rootVisualElement.Add(second);
                window.rootVisualElement.Add(firstDocument.rootVisualElement);
                window.rootVisualElement.Add(secondDocument.rootVisualElement);
                yield return null;
                yield return null;
                var panel = window.rootVisualElement.panel;
                var firstPoint = first.worldBound.center;
                var secondPoint = second.worldBound.center;
                Assert.That(panel.Pick(firstPoint), Is.SameAs(first));
                Assert.That(panel.Pick(secondPoint), Is.SameAs(second));
                var down = (Dictionary<string, object>)VmAutomationUIToolkitCommands.DispatchRuntimePointer(
                    Pointer(firstDocument, firstPoint, "Down"));
                Assert.That(down.ContainsKey("errorCode"), Is.False);
                var captured = panel.GetCapturingElement(PointerId.mousePointerId);
                Assert.That(captured, Is.Not.Null);
                Assert.That(firstDocument.rootVisualElement.Contains(captured as VisualElement) ||
                    captured == firstDocument.rootVisualElement, Is.True);
                foreach (string phase in new[] { "Down", "Move", "Up", "Click" })
                {
                    var result = (Dictionary<string, object>)VmAutomationUIToolkitCommands.DispatchRuntimePointer(
                        Pointer(secondDocument, secondPoint, phase));
                    Assert.That(result.ContainsKey("errorCode"), Is.True, phase + " must reject foreign capture.");
                    Assert.That(result["errorCode"], Is.EqualTo("ui_pointer_target_mismatch"));
                    Assert.That(panel.GetCapturingElement(PointerId.mousePointerId), Is.SameAs(captured));
                }
                var up = (Dictionary<string, object>)VmAutomationUIToolkitCommands.DispatchRuntimePointer(
                    Pointer(firstDocument, firstPoint, "Up"));
                Assert.That(up.ContainsKey("errorCode"), Is.False);
                Assert.That(panel.GetCapturingElement(PointerId.mousePointerId), Is.Null);
            }
            finally
            {
                window.Close();
                UnityEngine.Object.DestroyImmediate(firstObject);
                UnityEngine.Object.DestroyImmediate(secondObject);
                EditorSceneManager.ClosePreviewScene(preview);
                Assert.That(SceneManager.GetActiveScene(), Is.EqualTo(active));
                Assert.That(active.isDirty, Is.EqualTo(dirty));
                Assert.That(SceneManager.sceneCount, Is.EqualTo(sceneCount));
            }
        }

        [UnityTest]
        public IEnumerator KeyboardEditsNativeSliderInputAndUnicodeText()
        {
            var window = ScriptableObject.CreateInstance<PointerWindow>();
            window.position = new Rect(100, 100, 420, 220);
            var slider = new SliderInt(8, 85) { value = 28, showInputField = true };
            slider.style.height = 60;
            var name = new TextField { name = "Name" };
            var root = window.rootVisualElement;
            root.Add(slider);
            root.Add(name);
            var ages = new List<int>();
            int textChanges = 0;
            slider.RegisterValueChangedCallback(evt => ages.Add(evt.newValue));
            name.RegisterValueChangedCallback(_ => textChanges++);
            window.Show();
            try
            {
                yield return null;
                yield return null;
                var textInput = slider.Q<TextField>();
                Assert.That(textInput, Is.Not.Null);
                var point = textInput.worldBound.center;
                VmAutomationUIToolkitCommands.DispatchNativePointer(root.panel.Pick(point), point, "Click");
                var events = new List<object>
                {
                    Key("Down", KeyCode.A, "", EventModifiers.Control),
                    Key("Up", KeyCode.A, "", EventModifiers.Control),
                    Key("Down", KeyCode.Alpha6, "6"), Key("Up", KeyCode.Alpha6),
                    Key("Down", KeyCode.Alpha5, "5"), Key("Up", KeyCode.Alpha5),
                    Key("Down", KeyCode.Return, "\n"), Key("Up", KeyCode.Return)
                };
                AssertValidKeyboard(events);
                var error = VmAutomationUIToolkitCommands.DispatchNativeKeyboard(root,
                    VmAutomationUIToolkitCommands.ParseNativeKeyboardInputs(events), out var firstFocus, out _);
                Assert.That(error, Is.Null);
                Assert.That(firstFocus, Is.Not.Null);
                Assert.That(slider.value, Is.EqualTo(65));
                Assert.That(ages, Does.Contain(65), "The native text input must publish a SliderInt ChangeEvent.");
                name.Focus();
                yield return null;
                events = new List<object>
                {
                    Key("Down", KeyCode.None, "林"), Key("Up", KeyCode.None),
                    Key("Down", KeyCode.None, "姜"), Key("Up", KeyCode.None)
                };
                AssertValidKeyboard(events);
                error = VmAutomationUIToolkitCommands.DispatchNativeKeyboard(root,
                    VmAutomationUIToolkitCommands.ParseNativeKeyboardInputs(events), out _, out _);
                Assert.That(error, Is.Null);
                Assert.That(name.value, Is.EqualTo("林姜"));
                Assert.That(textChanges, Is.EqualTo(2));
            }
            finally { window.Close(); }
        }

        [UnityTest]
        public IEnumerator KeyboardRejectsForeignFocusAtThePartialSequenceBoundary()
        {
            var window = ScriptableObject.CreateInstance<PointerWindow>();
            window.position = new Rect(100, 100, 420, 220);
            var scope = new VisualElement();
            var first = new TextField { name = "InDocument" };
            var foreign = new TextField { name = "OtherDocument" };
            scope.Add(first);
            window.rootVisualElement.Add(scope);
            window.rootVisualElement.Add(foreign);
            first.RegisterCallback<KeyDownEvent>(evt =>
            {
                if (evt.keyCode == KeyCode.F2) foreign.Focus();
            });
            window.Show();
            try
            {
                yield return null;
                yield return null;
                first.Focus();
                yield return null;
                var inputs = VmAutomationUIToolkitCommands.ParseNativeKeyboardInputs(new List<object>
                {
                    Key("Down", KeyCode.F2), Key("Up", KeyCode.F2), Key("Down", KeyCode.Alpha6, "6")
                });
                var error = VmAutomationUIToolkitCommands.DispatchNativeKeyboard(scope, inputs, out _, out _);
                AssertKeyboardError(error, "ui_keyboard_focus_mismatch", 1);
                Assert.That(foreign.value, Is.Empty);
                Assert.That(foreign.Contains(scope.focusController.focusedElement as VisualElement) ||
                    scope.focusController.focusedElement == foreign, Is.True);
                error = VmAutomationUIToolkitCommands.DispatchNativeKeyboard(scope, inputs, out _, out _);
                AssertKeyboardError(error, "ui_keyboard_focus_mismatch", 0);
                scope.focusController.focusedElement.Blur();
                yield return null;
                error = VmAutomationUIToolkitCommands.DispatchNativeKeyboard(scope, inputs, out _, out _);
                AssertKeyboardError(error, "ui_keyboard_focus_unavailable", 0);
            }
            finally { window.Close(); }
        }

        [Test]
        public void KeyboardRejectsDetachedDocumentBeforeDispatch()
        {
            var inputs = VmAutomationUIToolkitCommands.ParseNativeKeyboardInputs(new List<object>
            {
                Key("Down", KeyCode.Alpha6, "6")
            });
            var error = VmAutomationUIToolkitCommands.DispatchNativeKeyboard(new VisualElement(), inputs, out _, out _);
            AssertKeyboardError(error, "ui_keyboard_document_unavailable", 0);
        }

        [Test]
        public void KeyboardSchemaClosesTheWholeBoundedNativeBatch()
        {
            var schema = VmAutomationToolInputSchemaCatalog.Get("uitoolkit/runtime-keyboard");
            var properties = (Dictionary<string, object>)schema["properties"];
            var eventsSchema = (Dictionary<string, object>)properties["events"];
            var eventSchema = (Dictionary<string, object>)eventsSchema["items"];
            var eventProperties = (Dictionary<string, object>)eventSchema["properties"];
            var keySchema = (Dictionary<string, object>)eventProperties["keyCode"];
            CollectionAssert.AreEquivalent(Enum.GetNames(typeof(KeyCode)), (IEnumerable)keySchema["enum"]);
            AssertValidKeyboard(new List<object> { Key("Down", KeyCode.None, "字") });
            var validMaximum = new List<object>();
            for (int index = 0; index < VmAutomationUIToolkitCommands.MaximumKeyboardEvents; index++)
                validMaximum.Add(Key("Down", KeyCode.Alpha6, "6"));
            AssertValidKeyboard(validMaximum);
            var overMaximum = new List<object>(validMaximum) { Key("Up", KeyCode.Alpha6) };
            AssertInvalidKeyboard(overMaximum);
            AssertInvalidKeyboard(new List<object>());
            AssertInvalidKeyboard(new List<object> { Key("Down", KeyCode.None, "😀") });
            var duplicate = Key("Down", KeyCode.A);
            duplicate["modifiers"] = new List<object> { "Control", "Control" };
            AssertInvalidKeyboard(new List<object> { duplicate });
            var none = Key("Down", KeyCode.A);
            none["modifiers"] = new List<object> { "None", "Control" };
            AssertInvalidKeyboard(new List<object> { none });
            var unknown = Key("Down", KeyCode.A);
            unknown["keyCode"] = "UnknownKey";
            AssertInvalidKeyboard(new List<object> { unknown });
            var numeric = Key("Down", KeyCode.A);
            numeric["keyCode"] = "97";
            AssertInvalidKeyboard(new List<object> { numeric });
            AssertInvalidKeyboard(new List<object> { Key("Click", KeyCode.A) });
            var extra = Key("Down", KeyCode.A);
            extra["value"] = 65;
            AssertInvalidKeyboard(new List<object> { extra });
            var missing = Key("Down", KeyCode.A);
            missing.Remove("character");
            AssertInvalidKeyboard(new List<object> { Key("Down", KeyCode.Alpha6, "6"), missing });
        }

        private static Dictionary<string, object> Key(string phase, KeyCode code, string character = "",
            EventModifiers modifier = EventModifiers.None)
        {
            return new Dictionary<string, object>
            {
                { "phase", phase }, { "keyCode", code.ToString() }, { "character", character },
                { "modifiers", modifier == EventModifiers.None
                    ? new List<object>() : new List<object> { modifier.ToString() } }
            };
        }

        private static Dictionary<string, object> Pointer(UIDocument document, Vector2 point, string phase)
        {
            return new Dictionary<string, object>
            {
                { "documentInstanceId", VmObjectId.Get(document) },
                { "phase", phase }, { "x", point.x }, { "y", point.y }
            };
        }

        private static void AssertValidKeyboard(List<object> events)
        {
            Assert.That(VmAutomationInputValidator.TryValidate(new Dictionary<string, object>
            {
                { "documentInstanceId", "0" }, { "events", events }
            }, VmAutomationToolInputSchemaCatalog.Get("uitoolkit/runtime-keyboard"),
                out _, out string message, out _), Is.True, message);
        }

        private static void AssertInvalidKeyboard(List<object> events)
        {
            Assert.That(VmAutomationInputValidator.TryValidate(new Dictionary<string, object>
            {
                { "documentInstanceId", "0" }, { "events", events }
            }, VmAutomationToolInputSchemaCatalog.Get("uitoolkit/runtime-keyboard"),
                out string code, out _, out _), Is.False);
            Assert.That(code, Is.EqualTo("invalid_arguments"));
        }

        private static void AssertKeyboardError(Dictionary<string, object> error, string code, int eventIndex)
        {
            Assert.That(error, Is.Not.Null);
            Assert.That(error["errorCode"], Is.EqualTo(code));
            Assert.That(error["dispatchedEvents"], Is.EqualTo(eventIndex));
            Assert.That(error["eventIndex"], Is.EqualTo(eventIndex));
        }

        [Test]
        public void GeneratedScrollPartsPublishResolvableNativePaths()
        {
            var root = new VisualElement();
            var scroll = new ScrollView();
            root.Add(scroll);
            scroll.Add(new Label("Authored content"));
            var entries = new List<Dictionary<string,object>>();
            VmAutomationUIToolkitElementUtility.CollectGeneratedChildren(root,scroll,"root/0",0,6,
                true,new List<string>(),new List<string>(),entries);
            var lowPath = VmAutomationUIToolkitElementUtility.GetElementPath(root,scroll.verticalScroller.lowButton);
            Assert.That(lowPath,Is.Not.Empty);
            Assert.That(VmAutomationUIToolkitElementUtility.GetElementByPath(root,lowPath),Is.SameAs(scroll.verticalScroller.lowButton));
            Assert.That(entries.Exists(entry => (string)entry["path"] == lowPath),Is.True);
            Assert.That(entries.Exists(entry => (string)entry["type"] == nameof(Scroller)),Is.True);
        }

        [Test]
        public void QueriesAndNamedPathsReachGeneratedSliderParts()
        {
            var root = new VisualElement();
            var scroll = new ScrollView { name = "Scroll" };
            scroll.verticalScroller.lowButton.name = "NativeLow";
            root.Add(scroll);
            var query = new Dictionary<string,object> { { "name","NativeLow" } };
            var found = VmAutomationUIToolkitElementUtility.FindEditorElement(root,query,out var path,out var error);
            Assert.That(error,Is.Empty);
            Assert.That(found,Is.SameAs(scroll.verticalScroller.lowButton));
            Assert.That(VmAutomationUIToolkitElementUtility.GetElementByPath(root,path),Is.SameAs(found));
            Assert.That(VmAutomationUIToolkitElementUtility.GetElementByVisualElementPath(root,
                new List<string> { "Scroll","NativeLow" }),Is.SameAs(found));
        }

        [Test]
        public void TreeCountsAndNodeBoundsDescribeTheSameNativeGraph()
        {
            var scroll = new ScrollView();
            int count = 0;
            bool truncated = false;
            var tree = VmAutomationUIToolkitElementUtility.BuildElementTree(scroll,"root",0,8,4,false,
                ref count,ref truncated);
            Assert.That((int)tree["childCount"],Is.EqualTo(scroll.hierarchy.childCount));
            Assert.That(scroll.hierarchy.childCount,Is.GreaterThan(scroll.childCount));
            Assert.That(count,Is.EqualTo(4));
            Assert.That(truncated,Is.True);
        }

        [Test]
        public void ObservationPreservesAuthoredContentOwnership()
        {
            var scroll = new ScrollView();
            var content = new Label("Content");
            scroll.Add(content);
            var authoredParent = content.parent;
            var entries = new List<Dictionary<string,object>>();
            VmAutomationUIToolkitElementUtility.QueryElements(scroll,"root","","",nameof(Label),"Content",
                false,1,entries);
            Assert.That(entries.Count,Is.EqualTo(1));
            Assert.That(VmAutomationUIToolkitElementUtility.GetElementByPath(scroll,(string)entries[0]["path"]),Is.SameAs(content));
            Assert.That(scroll.childCount,Is.EqualTo(1));
            Assert.That(content.parent,Is.SameAs(authoredParent));
            Assert.That(content.hierarchy.parent,Is.SameAs(scroll.contentContainer));
        }
    }
}
