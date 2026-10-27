using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using NativeInputSystem = UnityEngine.InputSystem.InputSystem;

namespace VMUnityAutomation.Editor.InputSystem
{
    [VmProjectTool("input/runtime-keyboard",
        Description = "Send one bounded chord through a temporary Input System Keyboard to an enabled project-wide Button action. Observe its real performed callback; normal matching gameplay/save effects apply. Release keys and restore keyboard, input routing and pause synchronously in finally. No OS input, callback invocation, asset/binding edits or game-frame advance. Observe the product separately.",
        RequiresPlayMode = true, MutatesRuntime = true, MutatesEditorView = true,
        SideEffects = VmProjectToolSideEffect.ReadsProjectState | VmProjectToolSideEffect.ChangesRuntimeState,
        ErrorCodes = new[] { "invalid_runtime_keyboard", "runtime_keyboard_not_active" },
        Preconditions = new[] { "editor-connected", "stable Play Mode", "enabled project-wide Input System Button action" },
        CompletionEvidence = "Actual performed count, final released action state, native input buffer and exact temporary-device/settings/pause restoration; product effects require separate readback.",
        TransactionScope = "synchronous-runtime-keyboard-input",
        TransactionAtomicity = "admission-before-input",
        TransactionIsolation = "editor-main-thread",
        TransactionDurability = "runtime-session",
        TransactionRollbackKind = "temporary-input-state-only")]
    public sealed class VmRuntimeKeyboardTool : IVmProjectTool<VmRuntimeKeyboardRequest, VmRuntimeKeyboardResult>
    {
        public VmRuntimeKeyboardResult Execute(VmRuntimeKeyboardRequest request)
        {
            var keys = ParseKeys(request.Keys);
            if (string.IsNullOrEmpty(request.ActionName) ||
                (!request.ActionName.Contains("/") && !Guid.TryParse(request.ActionName, out _)))
                throw Invalid("Select an exact map/action path or action GUID.");
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlaying != EditorApplication.isPlayingOrWillChangePlaymode)
                throw new VmProjectToolException("runtime_keyboard_not_active", "A stable Play Mode session is required.");
            var action = NativeInputSystem.actions?.FindAction(request.ActionName);
            if (action == null || !action.enabled || action.type != InputActionType.Button)
                throw Invalid("The selected project-wide action must exist, be enabled and have Button type.");

            var settings = NativeInputSystem.settings;
            var previousMode = settings.updateMode;
            var previousBackground = settings.backgroundBehavior;
            var previousRouting = settings.editorInputBehaviorInPlayMode;
            var previousKeyboard = Keyboard.current;
            bool previousPause = EditorApplication.isPaused;
            var result = new VmRuntimeKeyboardResult
            {
                ActionName = action.actionMap.name + "/" + action.name,
                Keys = request.Keys, FrameBefore = Time.frameCount, DispatchUpdateBuffer = "NoCallback"
            };
            Keyboard keyboard = null;
            void Performed(InputAction.CallbackContext context)
            {
                result.PerformedCount++;
                result.DispatchUpdateBuffer = InputState.currentUpdateType.ToString();
            }
            try
            {
                settings.updateMode = InputSettings.UpdateMode.ProcessEventsManually;
                settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                settings.editorInputBehaviorInPlayMode =
                    InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                // The public Update owner selects the player buffer outside paused Editor state.
                EditorApplication.isPaused = false;
                keyboard = NativeInputSystem.AddDevice<Keyboard>();
                action.performed += Performed;
                NativeInputSystem.QueueStateEvent(keyboard, new KeyboardState());
                NativeInputSystem.Update();
                NativeInputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
                NativeInputSystem.Update();
            }
            finally
            {
                try
                {
                    if (keyboard != null)
                    {
                        NativeInputSystem.QueueStateEvent(keyboard, new KeyboardState());
                        NativeInputSystem.Update();
                        result.FinalActionPhase = action.phase.ToString();
                        result.FinalActionPressed = action.IsPressed();
                    }
                }
                finally
                {
                    action.performed -= Performed;
                    if (keyboard != null)
                        NativeInputSystem.RemoveDevice(keyboard);
                    if (previousKeyboard != null)
                        previousKeyboard.MakeCurrent();
                    EditorApplication.isPaused = previousPause;
                    settings.editorInputBehaviorInPlayMode = previousRouting;
                    settings.backgroundBehavior = previousBackground;
                    settings.updateMode = previousMode;
                    result.FrameAfter = Time.frameCount;
                    result.TemporaryKeyboardRemoved = keyboard == null || !keyboard.added;
                    result.CurrentKeyboardRestored = Keyboard.current == previousKeyboard;
                    result.SettingsRestored = settings.updateMode == previousMode &&
                        settings.backgroundBehavior == previousBackground &&
                        settings.editorInputBehaviorInPlayMode == previousRouting;
                    result.PauseRestored = EditorApplication.isPaused == previousPause;
                }
            }
            return result;
        }

        private static Key[] ParseKeys(string[] names)
        {
            if (names == null || names.Length < 1 || names.Length > 4)
                throw Invalid("A chord requires one through four unique Key enum names.");
            var keys = new Key[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                if (!Enum.TryParse(names[i], true, out Key key) || key == Key.None ||
                    !string.Equals(Enum.GetName(typeof(Key), key), names[i], StringComparison.OrdinalIgnoreCase))
                    throw Invalid("Unknown Key enum name: " + names[i]);
                for (int j = 0; j < i; j++)
                    if (keys[j] == key)
                        throw Invalid("Duplicate Key enum name: " + names[i]);
                keys[i] = key;
            }
            return keys;
        }

        private static VmProjectToolException Invalid(string message) =>
            new("invalid_runtime_keyboard", message);
    }
}
