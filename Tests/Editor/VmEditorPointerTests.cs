using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace VMUnityAutomation.Editor.Tests
{
    public sealed class VmEditorPointerTests
    {
        private sealed class PointerWindow : EditorWindow { }

        [Test]
        public void PublishesOneEditorOperationAndNativeEffects()
        {
            var type = typeof(VmEditorPointerTool);
            var attribute = type.GetCustomAttribute<VmProjectToolAttribute>();
            var descriptor = VmProjectToolDescriptor.FromType(attribute, type);
            Assert.That(descriptor.ValidationError, Is.Null.Or.Empty);
            Assert.That(descriptor.MutatesEditorView, Is.True);
            Assert.That(descriptor.MutatesRuntime, Is.False);
            Assert.That(attribute.SideEffects.HasFlag(VmProjectToolSideEffect.WritesAssets), Is.True);
        }

        [UnityTest]
        public IEnumerator ExecutesNativeClickableAndSliderWithoutValueAssignment()
        {
            var prior = EditorWindow.focusedWindow;
            var window = ScriptableObject.CreateInstance<PointerWindow>();
            window.position = new Rect(100, 100, 420, 220);
            int clicks = 0;
            var button = new Button(() => clicks++) { text = "Action", name = "NativeAction" };
            button.style.height = 60;
            var slider = new Slider(0, 100); slider.style.height = 80;
            window.rootVisualElement.Add(button); window.rootVisualElement.Add(slider);
            window.Show();
            try
            {
                yield return null; yield return null;
                var result = Send(window, button.worldBound.center, VmEditorPointerPhase.Click);
                Assert.That(result.PickedName, Is.EqualTo("NativeAction"));
                Assert.That(result.DispatchedEvents, Is.EqualTo(2));
                Assert.That(clicks, Is.EqualTo(1));
                Assert.That(result.MouseCaptured, Is.False);
                var track = slider.Q(className: "unity-base-slider__tracker").worldBound;
                var start = new Vector2(track.xMin + track.width * .2f, track.center.y);
                var end = new Vector2(track.xMin + track.width * .8f, track.center.y);
                Send(window, start, VmEditorPointerPhase.Down);
                Send(window, end, VmEditorPointerPhase.Move);
                Send(window, end, VmEditorPointerPhase.Up);
                Assert.That(slider.value, Is.GreaterThan(50));
                Assert.That(slider.HasPointerCapture(PointerId.mousePointerId), Is.False);
            }
            finally { window.Close(); if (prior != null) prior.Focus(); }
        }

        [UnityTest]
        public IEnumerator OwnedCaptureContinuesOutsideAndRetiresOnUp()
        {
            var prior = EditorWindow.focusedWindow;
            var window = ScriptableObject.CreateInstance<PointerWindow>();
            window.position = new Rect(100, 100, 420, 220);
            var root = window.rootVisualElement;
            var surface = new VisualElement { name = "NativeCaptureSurface", pickingMode = PickingMode.Position };
            surface.style.height = 160; root.Add(surface);
            int downs = 0, moves = 0, ups = 0;
            root.RegisterCallback<PointerDownEvent>(evt => { downs++; root.CapturePointer(evt.pointerId); evt.StopPropagation(); });
            root.RegisterCallback<PointerMoveEvent>(evt => { moves++; evt.StopPropagation(); });
            root.RegisterCallback<PointerUpEvent>(evt => { ups++; root.ReleasePointer(evt.pointerId); evt.StopPropagation(); });
            window.Show();
            try
            {
                yield return null; yield return null;
                Assert.That(root.panel.Pick(surface.worldBound.center), Is.SameAs(surface));
                downs = moves = ups = 0;
                Assert.That(Send(window, surface.worldBound.center, VmEditorPointerPhase.Down).MouseCaptured, Is.True);
                var outside = root.worldBound.max + Vector2.one * 20;
                Send(window, outside, VmEditorPointerPhase.Move);
                Assert.That(Send(window, outside, VmEditorPointerPhase.Up).MouseCaptured, Is.False);
                Assert.That(downs, Is.EqualTo(1)); Assert.That(moves, Is.EqualTo(1)); Assert.That(ups, Is.EqualTo(1));
            }
            finally { window.Close(); if (prior != null) prior.Focus(); }
        }

        [UnityTest]
        public IEnumerator RejectsWrongObjectAndOutsideInitialHitBeforeNativeInput()
        {
            var prior = EditorWindow.focusedWindow;
            var window = ScriptableObject.CreateInstance<PointerWindow>();
            var texture = new Texture2D(1, 1);
            int downs = 0;
            window.position = new Rect(100, 100, 420, 220);
            window.rootVisualElement.RegisterCallback<PointerDownEvent>(_ => downs++);
            window.Show();
            try
            {
                yield return null; yield return null;
                var wrong = new VmEditorPointerRequest { WindowInstanceId = VmObjectId.Get(texture), Phase = VmEditorPointerPhase.Down };
                var rejectedObject = Assert.Throws<VmProjectToolException>(() => new VmEditorPointerTool().Execute(wrong));
                StringAssert.Contains("exact existing EditorWindow", rejectedObject.Message);
                var outside = window.rootVisualElement.worldBound.max + Vector2.one;
                var rejectedHit = Assert.Throws<VmProjectToolException>(() => Send(window, outside, VmEditorPointerPhase.Down));
                StringAssert.Contains("inside the exact EditorWindow", rejectedHit.Message);
                Assert.That(downs, Is.Zero);
                Assert.That(window.rootVisualElement.HasPointerCapture(PointerId.mousePointerId), Is.False);
            }
            finally { Object.DestroyImmediate(texture); window.Close(); if (prior != null) prior.Focus(); }
        }

        private static VmEditorPointerResult Send(EditorWindow window, Vector2 point, VmEditorPointerPhase phase) =>
            new VmEditorPointerTool().Execute(new VmEditorPointerRequest
            { WindowInstanceId = VmObjectId.Get(window), Phase = phase, X = point.x, Y = point.y });
    }
}
