namespace VMUnityAutomation.Editor
{
    [VmProjectTool("editor/record-window",
        Description = "Record one existing visible Editor tab to MP4 through native Editor-view frames and an explicitly installed FFmpeg encoder. Begin returns a capability sessionId; Inspect reads completion, End finishes early. Native PNG frames and monotonic timestamps remain as evidence. This does not create, focus, resize or enter Play Mode. Expiry, reload and Editor quit retire the recorder and encoder.",
        MutatesEditorView = true,
        SideEffects = VmProjectToolSideEffect.ChangesEditorView | VmProjectToolSideEffect.PerformsExternalIO,
        ErrorCodes = new[] { "recording_invalid_arguments", "recording_session_identity", "recording_already_active" },
        CompletionEvidence = "Inspect reports State=Succeeded, EncoderExitCode=0, a nonempty MP4 and ResourcesReleased=true. Failed or interrupted recordings retain frame/time evidence and an explicit error.")]
    public sealed class VmEditorWindowRecordingTool : IVmProjectTool<VmEditorWindowRecordingRequest, VmEditorWindowRecordingResult>
    {
        public VmEditorWindowRecordingResult Execute(VmEditorWindowRecordingRequest request) =>
            VmEditorWindowRecording.Execute(request);
    }
}
