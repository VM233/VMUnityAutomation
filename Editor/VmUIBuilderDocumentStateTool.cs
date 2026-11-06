using UnityEditor;

namespace VMUnityAutomation.Editor
{
    public static class VmUIBuilderDocumentStateTool
    {
        [VmProjectTool("editor/ui-builder-document-state",
            Description = "Read one exact existing native UI Builder document identity, asset path and independent document/window unsaved flags. Never opens, focuses, loads, saves or closes a window or document.",
            ReadOnly = true,
            SideEffects = VmProjectToolSideEffect.ReadsProjectState,
            ErrorCodes = new[] { "editor_window_not_found", "ui_builder_window_required" },
            Preconditions = new[] { "Exact existing native UI Builder window identity" },
            CompletionEvidence = "The snapshot reads the actual native Builder document and window flags through the same binding used by preview/close preflight, without retaining or changing either owner.")]
        public static Result Inspect(Request request)
        {
            var window = VmObjectId.ToObject(request.WindowInstanceId) as EditorWindow;
            if (window == null)
                throw new VmProjectToolException("editor_window_not_found", "The identity must resolve to an existing EditorWindow.");
            var binding = new VmUIBuilderDocumentBinding();
            if (!binding.WindowType.IsInstanceOfType(window))
                throw new VmProjectToolException("ui_builder_window_required", "The exact window must be a native UI Builder; its title is not an identity.");
            var state = binding.Read(window);
            return new Result
            {
                WindowInstanceId = VmObjectId.Get(window),
                DocumentInstanceId = VmObjectId.Get(state.Document),
                AssetPath = state.Path,
                DocumentHasUnsavedChanges = state.HasUnsavedChanges,
                WindowHasUnsavedChanges = state.WindowHasUnsavedChanges
            };
        }

        public sealed class Request
        {
            [VmRequired, VmMinLength(1)] public string WindowInstanceId { get; set; }
        }

        public sealed class Result
        {
            public string WindowInstanceId { get; set; }
            public string DocumentInstanceId { get; set; }
            public string AssetPath { get; set; }
            public bool DocumentHasUnsavedChanges { get; set; }
            public bool WindowHasUnsavedChanges { get; set; }
        }
    }
}
