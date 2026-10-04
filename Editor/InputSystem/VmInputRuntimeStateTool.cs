using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using NativeInputSystem = UnityEngine.InputSystem.InputSystem;

[assembly: VMUnityAutomation.Editor.VmProjectToolPackage("com.vm233.unity-automation")]

namespace VMUnityAutomation.Editor.InputSystem
{
    [VmProjectTool("input/runtime-state",
        Description = "Observe Input System device admission, native game focus and selected action states in the named current state buffer. Editor-buffer values do not prove player consumption.",
        ReadOnly = true,
        SideEffects = VmProjectToolSideEffect.ReadsProjectState,
        ErrorCodes = new[] { "invalid_input_observation", "input_observation_unavailable", "input_observation_limit" },
        Preconditions = new[] { "editor-connected", "Unity 6 and Input System 1.11 or later" },
        CompletionEvidence = "Reports actual focus owner, update buffer, device states and selected action enable/phase/control without injecting or consuming input.")]
    public sealed class VmInputRuntimeStateTool : IVmProjectTool<VmInputRuntimeStateRequest, VmInputRuntimeStateResult>
    {
        public VmInputRuntimeStateResult Execute(VmInputRuntimeStateRequest request)
        {
            if (request.ActionNames.Length > 16 || NativeInputSystem.devices.Count > 32)
                throw new VmProjectToolException("input_observation_limit", "Observation accepts at most sixteen actions and thirty-two devices.");
            var asset = AssetDatabase.LoadAssetAtPath<InputActionAsset>(request.AssetPath);
            if (asset == null)
                throw new VmProjectToolException("invalid_input_observation", "The selected path must resolve to an imported InputActionAsset.");
            var manager = typeof(NativeInputSystem).GetField("s_Manager", BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null);
            var focus = manager?.GetType().GetProperty("gameHasFocus", BindingFlags.Instance | BindingFlags.NonPublic);
            var playing = manager?.GetType().GetProperty("gameIsPlaying", BindingFlags.Instance | BindingFlags.NonPublic);
            if (focus == null || playing == null)
                throw new VmProjectToolException("input_observation_unavailable", "The installed Input System game focus observation contract is unavailable.");
            var actions = new VmInputActionState[request.ActionNames.Length];
            for (int i = 0; i < actions.Length; i++)
            {
                var action = asset.FindAction(request.ActionNames[i], true);
                actions[i] = new VmInputActionState
                {
                    Name = action.actionMap.name + "/" + action.name,
                    Enabled = action.enabled,
                    Phase = action.phase.ToString(),
                    ActiveControl = action.activeControl?.path
                };
            }
            var devices = new VmInputDeviceState[NativeInputSystem.devices.Count];
            for (int i = 0; i < devices.Length; i++)
            {
                var device = NativeInputSystem.devices[i];
                var state = new VmInputDeviceState
                {
                    ID = device.deviceId, Name = device.name, Layout = device.layout,
                    Enabled = device.enabled, Native = device.native, Added = device.added,
                    IsTouchscreen = device is Touchscreen
                };
                if (device is Touchscreen touchscreen)
                {
                    var touch = touchscreen.primaryTouch;
                    var position = touch.position.ReadValue();
                    state.TouchID = touch.touchId.ReadValue();
                    state.TouchPhase = touch.phase.ReadValue().ToString();
                    state.TouchPressed = touch.press.isPressed;
                    state.TouchX = position.x;
                    state.TouchY = position.y;
                }
                devices[i] = state;
            }
            return new VmInputRuntimeStateResult
            {
                Frame = Time.frameCount,
                UpdateBuffer = InputState.currentUpdateType.ToString(),
                ApplicationFocused = Application.isFocused,
                GameHasFocus = (bool)focus.GetValue(manager),
                GameIsPlaying = (bool)playing.GetValue(manager),
                IsProjectWideAsset = asset == NativeInputSystem.actions,
                UpdateMode = NativeInputSystem.settings.updateMode.ToString(),
                BackgroundBehavior = NativeInputSystem.settings.backgroundBehavior.ToString(),
                EditorInputBehavior = NativeInputSystem.settings.editorInputBehaviorInPlayMode.ToString(),
                AssetPath = request.AssetPath, Actions = actions, Devices = devices
            };
        }
    }
}
