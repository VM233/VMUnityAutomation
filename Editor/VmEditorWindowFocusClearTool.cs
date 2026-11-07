using System.Collections.Generic;

namespace VMUnityAutomation.Editor
{
    [VmProjectTool("editor/clear-window-focus",
        Description = "Restore actual null Editor window focus on Windows through one native main-thread SetFocus(NULL). Verify the OS keyboard recipient, Unity GUIView and EditorWindow are all empty. Does not create, close or activate another window, change layout/presets, synthesize focus events or blur UI elements.",
        MutatesEditorView = true,
        SideEffects = VmProjectToolSideEffect.ReadsProjectState | VmProjectToolSideEffect.ChangesEditorView,
        ErrorCodes = new[] { "capability_unavailable", "editor_window_focus_unavailable", "editor_window_focus_ownership_mismatch", "editor_window_focus_incomplete" },
        Preconditions = new[] { "Windows Editor main thread" },
        CompletionEvidence = "Current native keyboard focus handle is zero and both GUIView.focusedView and EditorWindow.focusedWindow are null; Previous and Current retain exact observations.")]
    public sealed class VmEditorWindowFocusClearTool : IVmProjectTool<VmEditorWindowFocusClearRequest, VmEditorWindowFocusClearResult>
    {
        public VmEditorWindowFocusClearResult Execute(VmEditorWindowFocusClearRequest request)
        {
            var previous = VmEditorNativeFocus.Read();
            VmEditorNativeFocus.Clear();
            var current = VmEditorNativeFocus.Read();
            if (current.WindowInstanceId.Length != 0 || current.NativeViewInstanceId.Length != 0 || current.NativeKeyboardFocusHandle != "0")
                throw new VmProjectToolException("editor_window_focus_incomplete", "The native clear did not remove all Editor window focus; no alternative view or managed state was substituted.",
                    details: new Dictionary<string, object> { { "previous", previous }, { "current", current } });
            return new VmEditorWindowFocusClearResult { Cleared = true, Previous = previous, Current = current };
        }
    }
}
