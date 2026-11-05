using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace VMUnityAutomation.Editor
{
    [VmProjectTool("uitoolkit/runtime-touch", RequiresPlayMode = true, MutatesRuntime = true,
        Description = "Publish one or two native UI Toolkit touch samples in one frame to an exact attached runtime UIDocument. Distinct fingers use slots0 through9. Down requires an inside hit; captured Move/Up/Cancel may leave the root. Unity owns dispatch targets and capture. No hardware injection, callbacks or product assignments. Read actual product state and pixels separately.",
        SideEffects = VmProjectToolSideEffect.ReadsProjectState | VmProjectToolSideEffect.ChangesRuntimeState,
        ErrorCodes = new[] { "invalid_touch_samples", "ui_touch_document_unavailable", "ui_touch_target_mismatch" },
        CompletionEvidence = "Dispatched native event count, frame and native captured finger slots. Caller retires Down through Up/Cancel or production-root retirement; no tool resources persist.")]
    public sealed class VmRuntimeTouchTool : IVmProjectTool<VmRuntimeTouchRequest, VmRuntimeTouchResult>
    {
        public VmRuntimeTouchResult Execute(VmRuntimeTouchRequest request)
        {
            var document = VmObjectId.ToObject(request.DocumentInstanceId) as UIDocument;
            var root = document?.rootVisualElement;
            Dispatch(root, request.Samples);
            var captures = new List<int>(10);
            for (int finger = 0; finger < 10; finger++)
            {
                var capture = root.panel?.GetCapturingElement(PointerId.touchPointerIdBase + finger) as VisualElement;
                if (capture != null && (capture == root || root.Contains(capture))) captures.Add(finger);
            }
            return new VmRuntimeTouchResult { DocumentInstanceId = request.DocumentInstanceId,
                DispatchedEvents = request.Samples.Length, Frame = Time.frameCount, CapturedFingers = captures.ToArray() };
        }

        internal static void ValidateSamples(VmRuntimeTouchSample[] samples)
        {
            if (samples == null || samples.Length < 1 || samples.Length > 2)
                throw new VmProjectToolException("invalid_touch_samples", "Provide one or two touch samples.");
            for (int index = 0; index < samples.Length; index++)
            {
                var sample = samples[index];
                if (sample == null || sample.Finger < 0 || sample.Finger > 9 ||
                    !Enum.IsDefined(typeof(VmRuntimeTouchPhase), sample.Phase) ||
                    float.IsNaN(sample.X) || float.IsInfinity(sample.X) ||
                    float.IsNaN(sample.Y) || float.IsInfinity(sample.Y) ||
                    (index == 1 && sample.Finger == samples[0].Finger))
                    throw new VmProjectToolException("invalid_touch_samples", "Touch samples require distinct slots0 through9, a declared phase and finite panel coordinates.");
            }
        }

        internal static void Dispatch(VisualElement root, VmRuntimeTouchSample[] samples)
        {
            ValidateSamples(samples);
            for (int index = 0; index < samples.Length; index++)
            {
                if (root?.panel == null)
                    throw new VmProjectToolException("ui_touch_document_unavailable", "The runtime document must remain attached at sample " + index + ".");
                var sample = samples[index];
                var point = new Vector2(sample.X, sample.Y);
                var capture = root.panel.GetCapturingElement(PointerId.touchPointerIdBase + sample.Finger);
                bool owned = capture is VisualElement element && (element == root || root.Contains(element));
                if (capture != null && !owned)
                    throw Mismatch(index, "Native touch capture belongs to another document.");
                if (!owned || sample.Phase == VmRuntimeTouchPhase.Down)
                {
                    var picked = root.panel.Pick(point);
                    if (!root.worldBound.Contains(point) || picked == null || (picked != root && !root.Contains(picked)))
                        throw Mismatch(index, "The initial touch must hit the exact document root.");
                }
                var touch = new Touch { fingerId = sample.Finger, position = point, rawPosition = point, tapCount = 1,
                    phase = sample.Phase == VmRuntimeTouchPhase.Down ? TouchPhase.Began :
                        sample.Phase == VmRuntimeTouchPhase.Move ? TouchPhase.Moved :
                        sample.Phase == VmRuntimeTouchPhase.Up ? TouchPhase.Ended : TouchPhase.Canceled };
                using (EventBase evt = sample.Phase switch
                {
                    VmRuntimeTouchPhase.Down => PointerDownEvent.GetPooled(touch, EventModifiers.None),
                    VmRuntimeTouchPhase.Move => PointerMoveEvent.GetPooled(touch, EventModifiers.None),
                    VmRuntimeTouchPhase.Up => PointerUpEvent.GetPooled(touch, EventModifiers.None),
                    _ => PointerCancelEvent.GetPooled(touch, EventModifiers.None)
                }) root.SendEvent(evt);
            }
        }
        private static VmProjectToolException Mismatch(int index, string reason) =>
            new("ui_touch_target_mismatch", "Touch sample " + index + ": " + reason);
    }
}
