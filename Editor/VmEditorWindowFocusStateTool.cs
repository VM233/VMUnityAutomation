namespace VMUnityAutomation.Editor
{
    [VmProjectTool("editor/window-focus-state",
        Description = "Read the exact native focused EditorWindow, underlying GUIView and Windows main-thread keyboard recipient without activating, creating or changing a window. An empty EditorWindow identity alone does not prove an empty GUIView.",
        ReadOnly = true,
        SideEffects = VmProjectToolSideEffect.ReadsProjectState,
        ErrorCodes = new[] { "editor_window_focus_unavailable" },
        CompletionEvidence = "The product observes EditorWindow.focusedWindow, GUIView.focusedView and Windows GetFocus directly and owns no retained focus or window state.")]
    public sealed class VmEditorWindowFocusStateTool : IVmProjectTool<VmEditorWindowFocusStateRequest, VmEditorWindowFocusStateResult>
    {
        public VmEditorWindowFocusStateResult Execute(VmEditorWindowFocusStateRequest request)
        {
            return VmEditorNativeFocus.Read();
        }
    }
}
