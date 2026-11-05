using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace VMUnityAutomation.Editor
{
    [VmProjectTool("uitoolkit/editor-pointer", MutatesEditorView = true,
        Description = "Publish a native pointer phase or short Click to one exact existing EditorWindow at native panel coordinates. Initial input must hit that root; owned capture permits outside Move/Up. Shares runtime-pointer's native dispatch. Does not create/focus windows, invoke callbacks or assign values. Native controls own resulting Editor/asset changes. Read resulting product state and pixels separately.",
        SideEffects = VmProjectToolSideEffect.ReadsProjectState | VmProjectToolSideEffect.ChangesEditorView |
            VmProjectToolSideEffect.ChangesRuntimeState | VmProjectToolSideEffect.WritesAssets,
        ErrorCodes = new[] { "ui_editor_pointer_window_unavailable", "ui_editor_pointer_outside_root", "ui_editor_pointer_target_mismatch" },
        CompletionEvidence = "Native event count, exact window/picked identity, root attachment and mouse capture. Separate Down requires paired Up or native root retirement; no tool resources persist.")]
    public sealed class VmEditorPointerTool : IVmProjectTool<VmEditorPointerRequest, VmEditorPointerResult>
    {
        public VmEditorPointerResult Execute(VmEditorPointerRequest request)
        {
            var window = VmObjectId.ToObject(request.WindowInstanceId) as EditorWindow;
            var root = window == null ? null : window.rootVisualElement;
            if (root?.panel == null)
                throw new VmProjectToolException("ui_editor_pointer_window_unavailable", "The exact existing EditorWindow must have an attached native root.");
            var position = new Vector2(request.X, request.Y);
            var phase = request.Phase.ToString();
            if (VmAutomationUIToolkitCommands.HasForeignMouseCapture(root, out bool ownedCapture))
                throw new VmProjectToolException("ui_editor_pointer_target_mismatch", "Native mouse capture belongs outside the exact EditorWindow root.");
            bool continuation = ownedCapture && request.Phase is VmEditorPointerPhase.Move or VmEditorPointerPhase.Up;
            if (!continuation && !root.worldBound.Contains(position))
                throw new VmProjectToolException("ui_editor_pointer_outside_root", "Initial input must lie inside the exact EditorWindow root.");
            var target = root.panel.Pick(position);
            if (!continuation && (target == null || target != root && !root.Contains(target)))
                throw new VmProjectToolException("ui_editor_pointer_target_mismatch", "Another native root owns the hit-tested pointer position.");
            var result = new VmEditorPointerResult { WindowInstanceId = request.WindowInstanceId, Phase = request.Phase,
                PickedName = target?.name ?? "", PickedType = target?.GetType().FullName ?? "",
                DispatchedEvents = request.Phase == VmEditorPointerPhase.Click ? 2 : 1 };
            VmAutomationUIToolkitCommands.DispatchNativePointer(continuation ? root : target, position, phase);
            result.RootAttached = root.panel != null;
            result.MouseCaptured = root.panel?.GetCapturingElement(PointerId.mousePointerId) != null;
            return result;
        }
    }
}
