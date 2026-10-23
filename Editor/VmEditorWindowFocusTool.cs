using UnityEditor;

namespace VMUnityAutomation.Editor
{
    [VmProjectTool("editor/focus-window",
        Description = "Activate the tab and focus one existing native EditorWindow by exact instanceId. Does not create, close or resize a window, change Game View presets, enter Play Mode, or save assets.",
        MutatesEditorView = true,
        SideEffects = VmProjectToolSideEffect.ReadsProjectState | VmProjectToolSideEffect.ChangesEditorView,
        ErrorCodes = new[] { "editor_window_not_found", "editor_window_focus_incomplete" },
        Preconditions = new[] { "Existing EditorWindow identity from uitoolkit/windows" },
        CompletionEvidence = "EditorWindow.focusedWindow equals the requested existing EditorWindow after EditorWindow.Focus.")]
    public sealed class VmEditorWindowFocusTool : IVmProjectTool<VmEditorWindowFocusRequest, VmEditorWindowFocusResult>
    {
        public VmEditorWindowFocusResult Execute(VmEditorWindowFocusRequest request)
        {
            var window = VmObjectId.ToObject(request.WindowInstanceId) as EditorWindow;
            if (window == null)
                throw new VmProjectToolException("editor_window_not_found", "The identity must resolve to an existing EditorWindow.");

            var previous = EditorWindow.focusedWindow;
            string previousIdentity = previous == null ? "" : VmObjectId.Get(previous);
            window.Focus();
            if (EditorWindow.focusedWindow != window)
                throw new VmProjectToolException("editor_window_focus_incomplete", "EditorWindow.Focus did not focus the requested existing window.");

            return new VmEditorWindowFocusResult
            {
                WindowInstanceId = VmObjectId.Get(window),
                WindowType = window.GetType().FullName,
                PreviousFocusedWindowInstanceId = previousIdentity,
                Focused = true
            };
        }
    }
}
