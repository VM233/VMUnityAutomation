namespace VMUnityAutomation.Editor
{
    [VmProjectTool("editor/record-window",
        Description = "Record one existing visible Editor tab to MP4 through native Editor-view frames and an explicitly installed FFmpeg encoder. Begin returns a capability sessionId; Inspect reads completion, End finishes early. Main-thread native RGB readback is followed by at most two PNG/file workers with four admitted frame buffers. Native PNG pixels, separate capture/encoding times and monotonic timestamps remain as evidence. This does not create, focus, resize or enter Play Mode. End, expiry, failure, reload and Editor quit join owned frame workers before retiring the recorder and encoder.",
        MutatesEditorView = true,
        SideEffects = VmProjectToolSideEffect.ChangesEditorView | VmProjectToolSideEffect.PerformsExternalIO,
        ErrorCodes = new[] { "recording_invalid_arguments", "recording_session_identity", "recording_already_active" },
        CompletionEvidence = "Inspect reports State=Succeeded, EncoderExitCode=0, PendingFrameEncodes=0, a nonempty MP4 and ResourcesReleased=true. FinishingFrames is not completion. Failed or interrupted recordings join all owned PNG workers and retain frame/time evidence and an explicit error.")]
    public sealed class VmEditorWindowRecordingTool : IVmProjectTool<VmEditorWindowRecordingRequest, VmEditorWindowRecordingResult>
    {
        public VmEditorWindowRecordingResult Execute(VmEditorWindowRecordingRequest request) =>
            VmEditorWindowRecording.Execute(request);
    }
}
