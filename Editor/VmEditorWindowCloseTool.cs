using UnityEditor;

namespace VMUnityAutomation.Editor
{
    [VmProjectTool("editor/close-window",
        Description = "Close one existing native EditorWindow in stable Edit Mode. Reject windows with unsaved changes; do not resolve Save/Discard dialogs.",
        MutatesProjectFiles = true,
        SideEffects = VmProjectToolSideEffect.ReadsProjectState | VmProjectToolSideEffect.WritesProjectFiles,
        ErrorCodes = new[] { "editor_not_idle", "editor_window_not_found", "editor_window_unsaved", "editor_window_close_incomplete" },
        Preconditions = new[] { "Stable Edit Mode", "Existing EditorWindow identity", "Window has no unsaved changes" },
        CompletionEvidence = "The native EditorWindow has been destroyed by EditorWindow.Close.")]
    public sealed class VmEditorWindowCloseTool : IVmProjectTool<VmEditorWindowCloseRequest, VmEditorWindowCloseResult>
    {
        public VmEditorWindowCloseResult Execute(VmEditorWindowCloseRequest request)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new VmProjectToolException("editor_not_idle", "Closing an Editor window requires stable Edit Mode.");

            var window = VmObjectId.ToObject(request.WindowInstanceId) as EditorWindow;
            if (window == null)
                throw new VmProjectToolException("editor_window_not_found", "The identity must resolve to an existing EditorWindow.");
            if (window.hasUnsavedChanges)
                throw new VmProjectToolException("editor_window_unsaved", "The window has unsaved changes. Resolve them before requesting closure.");

            string windowType = window.GetType().FullName;
            window.Close();
            if (window != null)
                throw new VmProjectToolException("editor_window_close_incomplete", "EditorWindow.Close did not destroy the requested window.");

            return new VmEditorWindowCloseResult
            {
                WindowInstanceId = request.WindowInstanceId,
                WindowType = windowType,
                Closed = true
            };
        }
    }
}
