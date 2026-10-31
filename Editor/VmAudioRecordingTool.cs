namespace VMUnityAutomation.Editor
{
    [VmProjectTool("audio/record-output", MutatesRuntime = true,
        Description = "Passively record an existing listener's real interleaved native DSP filter output to IEEE-float WAV. Begin returns a capability; Inspect observes; End finishes early; Cancel writes no WAV. At most15 seconds/4million samples. Does not synthesize samples, change the mix, create a listener/window or write player preferences. Play exit, reload and shutdown retire the filter.",
        SideEffects = VmProjectToolSideEffect.ReadsProjectState | VmProjectToolSideEffect.CreatesTemporaryObjects |
            VmProjectToolSideEffect.CapturesArtifacts | VmProjectToolSideEffect.WritesProjectFiles | VmProjectToolSideEffect.ChangesRuntimeState,
        ErrorCodes = new[] { "audio_recording_arguments", "audio_recording_identity", "audio_recording_active", "audio_recording_context" },
        CompletionEvidence = "Inspect reports State=Succeeded, native sample count/rate/channels, actual PCM peak/RMS, nonempty WAV and ResourcesReleased=true. This observes the listener filter stage; it does not measure speakers or room acoustics.")]
    public sealed class VmAudioRecordingTool : IVmProjectTool<VmAudioRecordingRequest, VmAudioRecordingResult>
    {
        public VmAudioRecordingResult Execute(VmAudioRecordingRequest request) => VmAudioRecording.Execute(request);
    }
}
