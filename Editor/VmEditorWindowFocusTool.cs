using System;
using System.Reflection;
using UnityEditor;

namespace VMUnityAutomation.Editor
{
    [VmProjectTool("editor/focus-window",
        Description = "Activate and focus one existing native EditorWindow by exact instanceId. Optionally set its native maximized state and return the previous state for restoration. Omission preserves maximization. Does not create or close a window, change Game View presets, enter Play Mode, or save assets.",
        MutatesEditorView = true,
        SideEffects = VmProjectToolSideEffect.ReadsProjectState | VmProjectToolSideEffect.ChangesEditorView,
        ErrorCodes = new[] { "editor_window_not_found", "editor_window_focus_incomplete", "editor_window_maximize_incomplete" },
        Preconditions = new[] { "Existing EditorWindow identity from uitoolkit/windows" },
        CompletionEvidence = "EditorWindow.focusedWindow equals the requested existing EditorWindow, and its native maximized property equals the requested state when supplied. PreviousMaximized supports exact restoration.")]
    public sealed class VmEditorWindowFocusTool : IVmProjectTool<VmEditorWindowFocusRequest, VmEditorWindowFocusResult>
    {
        public VmEditorWindowFocusResult Execute(VmEditorWindowFocusRequest request)
        {
            var window = VmObjectId.ToObject(request.WindowInstanceId) as EditorWindow;
            if (window == null)
                throw new VmProjectToolException("editor_window_not_found", "The identity must resolve to an existing EditorWindow.");

            var previous = EditorWindow.focusedWindow;
            string previousIdentity = previous == null ? "" : VmObjectId.Get(previous);
            string previousType = previous == null ? "" : previous.GetType().FullName;
            string previousTitle = previous == null ? "" : previous.titleContent.text;
            float[] previousPosition = previous == null ? new float[0] : PositionOf(previous);
            object floatingContainer = FloatingContainerOf(window);
            bool previousMaximized = IsMaximized(window, floatingContainer);
            window.Focus();
            if (request.Maximized.HasValue)
            {
                if (floatingContainer == null)
                    window.maximized = request.Maximized.Value;
                else if (previousMaximized != request.Maximized.Value)
                {
                    var toggle = floatingContainer.GetType().GetMethod("ToggleMaximize",
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance,
                        null, Type.EmptyTypes, null);
                    if (toggle == null)
                        throw new VmProjectToolException("editor_window_maximize_incomplete", "The floating view's native container has no maximize operation.");
                    toggle.Invoke(floatingContainer, null);
                }
                window.Focus();
                if (IsMaximized(window, floatingContainer) != request.Maximized.Value)
                    throw new VmProjectToolException("editor_window_maximize_incomplete", "The existing EditorWindow did not adopt the requested native maximized state.");
            }
            if (EditorWindow.focusedWindow != window)
                throw new VmProjectToolException("editor_window_focus_incomplete", "EditorWindow.Focus did not focus the requested existing window.");

            return new VmEditorWindowFocusResult
            {
                WindowInstanceId = VmObjectId.Get(window),
                WindowType = window.GetType().FullName,
                PreviousFocusedWindowInstanceId = previousIdentity,
                PreviousFocusedWindowType = previousType,
                PreviousFocusedWindowTitle = previousTitle,
                PreviousFocusedWindowPosition = previousPosition,
                WindowPosition = PositionOf(window),
                Focused = true,
                PreviousMaximized = previousMaximized,
                Maximized = IsMaximized(window, floatingContainer),
                MaximizationTarget = floatingContainer == null ? "EditorWindow" : "FloatingContainerWindow"
            };
        }

        private static object FloatingContainerOf(EditorWindow window)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var docked = typeof(EditorWindow).GetProperty("docked", flags);
            if (docked == null || !(docked.GetValue(window, null) is bool isDocked))
                throw new VmProjectToolException("editor_window_maximize_incomplete", "Cannot establish whether the exact view is docked or floating.");
            if (isDocked)
                return null;
            var parent = typeof(EditorWindow).GetField("m_Parent", flags)?.GetValue(window);
            var container = parent?.GetType().GetProperty("window", flags)?.GetValue(parent, null);
            if (container == null)
                throw new VmProjectToolException("editor_window_maximize_incomplete", "The floating view has no existing native container.");
            return container;
        }

        private static bool IsMaximized(EditorWindow window, object floatingContainer)
        {
            if (floatingContainer == null)
                return window.maximized;
            var property = floatingContainer.GetType().GetProperty("maximized",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property?.GetValue(floatingContainer, null) is bool maximized)
                return maximized;
            throw new VmProjectToolException("editor_window_maximize_incomplete", "Cannot read the exact floating view's native maximized state.");
        }

        private static float[] PositionOf(EditorWindow window)
        {
            var position = window.position;
            return new[] { position.x, position.y, position.width, position.height };
        }
    }
}
