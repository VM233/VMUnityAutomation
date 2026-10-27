using UnityEditor;

namespace VMUnityAutomation.Editor
{
    [VmProjectTool("editor/window-focus-state",
        Description = "Read the exact native focused EditorWindow identity, type and title without activating, creating or changing any window. Empty identity means Unity has no focused EditorWindow.",
        ReadOnly = true,
        SideEffects = VmProjectToolSideEffect.ReadsProjectState,
        CompletionEvidence = "The product observes EditorWindow.focusedWindow directly and owns no retained focus or window state.")]
    public sealed class VmEditorWindowFocusStateTool : IVmProjectTool<VmEditorWindowFocusStateRequest, VmEditorWindowFocusStateResult>
    {
        public VmEditorWindowFocusStateResult Execute(VmEditorWindowFocusStateRequest request)
        {
            var focused = EditorWindow.focusedWindow;
            return new VmEditorWindowFocusStateResult
            {
                WindowInstanceId = focused == null ? "" : VmObjectId.Get(focused),
                WindowType = focused == null ? "" : focused.GetType().FullName,
                WindowTitle = focused == null ? "" : focused.titleContent.text
            };
        }
    }
}
