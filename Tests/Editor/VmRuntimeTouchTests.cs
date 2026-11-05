using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace VMUnityAutomation.Editor.Tests
{
    public sealed class VmRuntimeTouchTests
    {
        private sealed class TouchWindow : EditorWindow { }
        private static VmRuntimeTouchSample Sample(int finger, VmRuntimeTouchPhase phase, Vector2 point) =>
            new() { Finger = finger, Phase = phase, X = point.x, Y = point.y };

        [Test]
        public void RejectsUnboundedDuplicateAndInvalidSamplesBeforeDispatch()
        {
            var first = Sample(0, VmRuntimeTouchPhase.Down, Vector2.one);
            Assert.Throws<VmProjectToolException>(() => VmRuntimeTouchTool.ValidateSamples(null));
            Assert.Throws<VmProjectToolException>(() => VmRuntimeTouchTool.ValidateSamples(new[] { first, first, first }));
            Assert.Throws<VmProjectToolException>(() => VmRuntimeTouchTool.ValidateSamples(new[] { first, first }));
            Assert.Throws<VmProjectToolException>(() => VmRuntimeTouchTool.ValidateSamples(new[] { Sample(10, VmRuntimeTouchPhase.Down, Vector2.one) }));
            Assert.Throws<VmProjectToolException>(() => VmRuntimeTouchTool.ValidateSamples(new[] { Sample(0, (VmRuntimeTouchPhase)7, Vector2.one) }));
            Assert.Throws<VmProjectToolException>(() => VmRuntimeTouchTool.ValidateSamples(new[] { Sample(0, VmRuntimeTouchPhase.Move, new Vector2(float.NaN, 0)) }));
            Assert.DoesNotThrow(() => VmRuntimeTouchTool.ValidateSamples(new[] { first, Sample(9, VmRuntimeTouchPhase.Down, Vector2.one) }));
        }

        [UnityTest]
        public IEnumerator NativeTwoContactCaptureContinuesOutsideAndRetiresOnUpCancel()
        {
            var window = ScriptableObject.CreateInstance<TouchWindow>();
            window.position = new Rect(100, 100, 500, 300);
            var root = new VisualElement(); root.style.width = 240; root.style.height = 160;
            window.rootVisualElement.Add(root);
            var peer = new VisualElement(); peer.style.position = Position.Absolute;
            peer.style.left = 260; peer.style.top = 0; peer.style.width = 200; peer.style.height = 160;
            window.rootVisualElement.Add(peer);
            peer.RegisterCallback<PointerDownEvent>(evt => { peer.CapturePointer(evt.pointerId); evt.StopPropagation(); });
            peer.RegisterCallback<PointerUpEvent>(evt => { peer.ReleasePointer(evt.pointerId); evt.StopPropagation(); });
            int down = 0, move = 0, up = 0, canceled = 0;
            root.RegisterCallback<PointerDownEvent>(evt => { down++; root.CapturePointer(evt.pointerId); evt.StopPropagation(); });
            root.RegisterCallback<PointerMoveEvent>(evt => { move++; evt.StopPropagation(); });
            root.RegisterCallback<PointerUpEvent>(evt => { up++; root.ReleasePointer(evt.pointerId); evt.StopPropagation(); });
            root.RegisterCallback<PointerCancelEvent>(evt => { canceled++; root.ReleasePointer(evt.pointerId); evt.StopPropagation(); });
            window.Show();
            try
            {
                yield return null; yield return null;
                var point = root.worldBound.center;
                VmRuntimeTouchTool.Dispatch(root, new[] { Sample(0, VmRuntimeTouchPhase.Down, point - Vector2.right * 30),
                    Sample(1, VmRuntimeTouchPhase.Down, point + Vector2.right * 30) });
                Assert.That(down, Is.EqualTo(2));
                Assert.That(root.HasPointerCapture(PointerId.touchPointerIdBase), Is.True);
                Assert.That(root.HasPointerCapture(PointerId.touchPointerIdBase + 1), Is.True);
                var outside = root.worldBound.max + Vector2.one * 20;
                VmRuntimeTouchTool.Dispatch(root, new[] { Sample(0, VmRuntimeTouchPhase.Move, outside),
                    Sample(1, VmRuntimeTouchPhase.Move, outside + Vector2.right * 10) });
                Assert.That(move, Is.EqualTo(2));
                VmRuntimeTouchTool.Dispatch(root, new[] { Sample(0, VmRuntimeTouchPhase.Up, outside),
                    Sample(1, VmRuntimeTouchPhase.Cancel, outside) });
                Assert.That(up, Is.EqualTo(1)); Assert.That(canceled, Is.EqualTo(1));
                Assert.That(root.HasPointerCapture(PointerId.touchPointerIdBase), Is.False);
                Assert.That(root.HasPointerCapture(PointerId.touchPointerIdBase + 1), Is.False);
                Assert.Throws<VmProjectToolException>(() => VmRuntimeTouchTool.Dispatch(root,
                    new[] { Sample(0, VmRuntimeTouchPhase.Down, outside) }));
                VmRuntimeTouchTool.Dispatch(peer, new[] { Sample(0, VmRuntimeTouchPhase.Down, peer.worldBound.center) });
                Assert.Throws<VmProjectToolException>(() => VmRuntimeTouchTool.Dispatch(root,
                    new[] { Sample(0, VmRuntimeTouchPhase.Move, point) }));
                VmRuntimeTouchTool.Dispatch(peer, new[] { Sample(0, VmRuntimeTouchPhase.Up, peer.worldBound.center) });
            }
            finally { window.Close(); }
        }
    }
}
