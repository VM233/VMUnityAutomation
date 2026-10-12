namespace VMUnityAutomation.Editor
{
    [VmProjectTool("editor/background-session",
        Description = "Temporarily run the actual PlayerLoop while the Editor is unfocused. Begin enables Application.runInBackground and native player updates; Inspect and End require the returned sessionId. No window is created or focused, no Player Settings are saved, and project focus policies remain explicit consumers of Active.",
        RequiresPlayMode = true, MutatesRuntime = true,
        SideEffects = VmProjectToolSideEffect.ReadsProjectState | VmProjectToolSideEffect.ChangesRuntimeState,
        ErrorCodes = new[] { "play_mode_required", "background_session_active", "background_session_identity", "invalid_arguments" },
        CompletionEvidence = "Reports native frame counters, actual application and Editor focus, runtime background state and expiry. End, expiry, Play Mode exit, reload and Editor quit restore the original runtime setting.")]
    public sealed class VmEditorBackgroundSessionTool : IVmProjectTool<VmEditorBackgroundSessionRequest, VmEditorBackgroundSessionResult>
    {
        public VmEditorBackgroundSessionResult Execute(VmEditorBackgroundSessionRequest request) =>
            VmEditorBackgroundExecution.Execute(request);
    }
}
