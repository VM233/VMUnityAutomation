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
            bool previousMaximized = window.maximized;
            window.Focus();
            if (request.Maximized.HasValue)
            {
                window.maximized = request.Maximized.Value;
                window.Focus();
                if (window.maximized != request.Maximized.Value)
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
                Maximized = window.maximized
            };
        }

        private static float[] PositionOf(EditorWindow window)
        {
            var position = window.position;
            return new[] { position.x, position.y, position.width, position.height };
        }
    }
}
