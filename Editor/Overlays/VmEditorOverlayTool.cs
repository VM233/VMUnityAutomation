using UnityEditor;

namespace VMUnityAutomation.Editor
{
    [VmProjectTool("editor/overlay",
        Description = "Observe or explicitly set one existing native Overlay's displayed state by exact EditorWindow identity and Overlay id. Uses public EditorWindow.TryGetOverlay and Overlay.displayed (Unity 2021.3+), including Unity 6 main toolbar elements. Changes can persist in Unity's user Editor layout. No window/overlay creation, focus, docking, preset reset, asset save or gameplay mutation.",
        MutatesEditorView = true,
        SideEffects = VmProjectToolSideEffect.ReadsProjectState | VmProjectToolSideEffect.ChangesEditorView,
        ErrorCodes = new[] { "editor_not_idle", "editor_window_not_found", "editor_overlay_not_found", "editor_overlay_display_incomplete" },
        Preconditions = new[] { "Stable Edit Mode", "Existing EditorWindow identity and registered Overlay id" },
        CompletionEvidence = "Native Overlay.displayed equals the explicit requested value. PreviousDisplayed supports exact restoration; actual rendered bounds remain independently observable with uitoolkit/query.")]
    public sealed class VmEditorOverlayTool : IVmProjectTool<VmEditorOverlayRequest, VmEditorOverlayResult>
    {
        public VmEditorOverlayResult Execute(VmEditorOverlayRequest request)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new VmProjectToolException("editor_not_idle", "Native Overlay access requires stable Edit Mode.");
            var window = VmObjectId.ToObject(request.WindowInstanceId) as EditorWindow;
            if (window == null)
                throw new VmProjectToolException("editor_window_not_found", "The identity must resolve to an existing EditorWindow.");
            if (string.IsNullOrWhiteSpace(request.OverlayId) || !window.TryGetOverlay(request.OverlayId, out var overlay))
                throw new VmProjectToolException("editor_overlay_not_found", "The exact Overlay id is not registered in the selected existing window.");
            bool previous = overlay.displayed;
            if (request.Displayed.HasValue)
            {
                overlay.displayed = request.Displayed.Value;
                if (overlay.displayed != request.Displayed.Value)
                    throw new VmProjectToolException("editor_overlay_display_incomplete", "The native Overlay did not adopt the requested displayed state.");
            }
            return new VmEditorOverlayResult
            {
                WindowInstanceId = VmObjectId.Get(window), WindowType = window.GetType().FullName,
                OverlayId = overlay.id, DisplayName = overlay.displayName,
                PreviousDisplayed = previous, Displayed = overlay.displayed,
                Changed = previous != overlay.displayed
            };
        }
    }
}
