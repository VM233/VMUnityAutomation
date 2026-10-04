using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace VMUnityAutomation.Editor
{
    [VmProjectTool("simulator/pointer",
        Description = "Dispatch a native mouse phase to the hit-tested Device Simulator DeviceView. Unity owns screen transforms, cutout checks and touch publication; inspect the running game to verify its response.",
        MutatesRuntime = true,
        RequiresPlayMode = true,
        SideEffects = VmProjectToolSideEffect.ReadsProjectState | VmProjectToolSideEffect.ChangesRuntimeState,
        ErrorCodes = new[] { "invalid_simulator_pointer", "simulator_input_not_active", "simulator_state_unavailable" },
        Preconditions = new[] { "editor-connected", "Playing and unpaused Editor", "Existing native Device Simulator window and hit-tested DeviceView" },
        CompletionEvidence = "Reports the native hit target, phase, transformed touch position, screen admission, active touch, actual player focus and frame. A resulting game interaction requires separate runtime state or visual verification.")]
    public sealed class VmSimulatorPointerTool : IVmProjectTool<VmSimulatorPointerRequest, VmSimulatorPointerResult>
    {
        public VmSimulatorPointerResult Execute(VmSimulatorPointerRequest request)
        {
            if (!EditorApplication.isPlaying || EditorApplication.isPaused ||
                EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlaying != EditorApplication.isPlayingOrWillChangePlaymode)
                throw new VmProjectToolException("simulator_input_not_active",
                    "Device Simulator input requires a stable, playing and unpaused Editor.");
            if (!Enum.IsDefined(typeof(VmSimulatorPointerPhase), request.Phase) ||
                float.IsNaN(request.X) || float.IsInfinity(request.X) ||
                float.IsNaN(request.Y) || float.IsInfinity(request.Y) || request.X < 0 || request.Y < 0)
                throw Invalid("The pointer phase and finite, nonnegative window coordinates are required.");

            var window = VmObjectId.ToObject(request.WindowInstanceId) as EditorWindow;
            if (window == null || window.GetType().FullName != "UnityEditor.DeviceSimulation.SimulatorWindow")
                throw Invalid("The window identity must resolve to the existing native Device Simulator.");
            var position = new Vector2(request.X, request.Y);
            var panel = window.rootVisualElement.panel;
            if (panel == null)
                throw Invalid("The Device Simulator UI panel is not attached.");
            var target = panel.Pick(position);
            if (target == null || target.GetType().FullName != "UnityEditor.DeviceSimulation.DeviceView")
                throw Invalid("The pointer must hit the native Device Simulator DeviceView.");

            var main = window.GetType().GetProperty("main", BindingFlags.Instance | BindingFlags.Public)?.GetValue(window);
            var touch = main?.GetType().GetField("m_TouchInput", BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(main);
            if (touch == null)
                throw new VmProjectToolException("simulator_state_unavailable", "The native Simulator touch owner is unavailable.");
            var positionProperty = touch.GetType().GetProperty("pointerPosition", BindingFlags.Instance | BindingFlags.Public);
            var insideProperty = touch.GetType().GetProperty("isPointerInsideDeviceScreen", BindingFlags.Instance | BindingFlags.Public);
            var activeField = touch.GetType().GetField("m_TouchFromMouseActive", BindingFlags.Instance | BindingFlags.NonPublic);
            if (positionProperty == null || insideProperty == null || activeField == null)
                throw new VmProjectToolException("simulator_state_unavailable", "The native Simulator touch observation contract is unavailable.");
            window.Focus();
            var nativeEvent = new Event { mousePosition = position, button = 0, clickCount = 1 };
            switch (request.Phase)
            {
                case VmSimulatorPointerPhase.Down:
                    nativeEvent.type = EventType.MouseDown;
                    using (var evt = MouseDownEvent.GetPooled(nativeEvent))
                    {
                        evt.target = target;
                        target.SendEvent(evt);
                    }
                    break;
                case VmSimulatorPointerPhase.Move:
                    nativeEvent.type = EventType.MouseMove;
                    using (var evt = MouseMoveEvent.GetPooled(nativeEvent))
                    {
                        evt.target = target;
                        target.SendEvent(evt);
                    }
                    break;
                case VmSimulatorPointerPhase.Up:
                    nativeEvent.type = EventType.MouseUp;
                    using (var evt = MouseUpEvent.GetPooled(nativeEvent))
                    {
                        evt.target = target;
                        target.SendEvent(evt);
                    }
                    break;
            }
            var touchPosition = (Vector2)positionProperty.GetValue(touch);
            return new VmSimulatorPointerResult
            {
                WindowInstanceId = request.WindowInstanceId,
                TargetType = target.GetType().FullName,
                Phase = request.Phase,
                X = request.X,
                Y = request.Y,
                TouchX = touchPosition.x,
                TouchY = touchPosition.y,
                NativeTouchActive = (bool)activeField.GetValue(touch),
                PointerInsideScreen = (bool)insideProperty.GetValue(touch),
                PlayerFocused = Application.isFocused,
                Frame = Time.frameCount
            };
        }

        private static VmProjectToolException Invalid(string message) =>
            new("invalid_simulator_pointer", message);
    }
}
