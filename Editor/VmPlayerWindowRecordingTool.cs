using System;
using UnityEditor;

namespace VMUnityAutomation.Editor
{
    [InitializeOnLoad]
    [VmProjectTool("player/record-window",
        Description = "Record an explicit client-pixel region of one exact running Windows Unity Player to a new MP4 using installed FFmpeg native hwnd capture. Begin returns a session capability; Inspect reads completion and End finalizes early. Does not activate, move, resize or send gameplay input. Reload, Editor quit, target exit and native geometry changes retire the recorder and encoder.",
        MutatesProjectFiles = true,
        SideEffects = VmProjectToolSideEffect.PerformsExternalIO | VmProjectToolSideEffect.CapturesArtifacts,
        ErrorCodes = new[] { "player_recording_invalid_arguments", "player_recording_platform_unsupported",
            "player_recording_session_identity", "player_recording_already_active", "invalid_player_identity_arguments",
            "player_process_not_running", "player_identity_mismatch", "player_window_unavailable" },
        CompletionEvidence = "Inspect reports State=Succeeded, EncoderExitCode=0, OutputBytes>0 and ResourcesReleased=true. Receipt identifies the exact process, HWND and client region; visual content and encoded cadence require independent frame inspection.")]
    public sealed class VmPlayerWindowRecordingTool : IVmProjectTool<VmPlayerWindowRecordingRequest, VmPlayerWindowRecordingResult>
    {
        private static VmPlayerWindowRecordingSession current;

        static VmPlayerWindowRecordingTool()
        {
            AssemblyReloadEvents.beforeAssemblyReload += Interrupt;
            EditorApplication.quitting += Interrupt;
        }

        public VmPlayerWindowRecordingResult Execute(VmPlayerWindowRecordingRequest request)
        {
#if !UNITY_EDITOR_WIN
            throw new VmProjectToolException("player_recording_platform_unsupported", "Native Player recording requires Windows.");
#else
            if (request.Action == "Begin")
            {
                if (current != null && !current.Result.ResourcesReleased)
                    throw new VmProjectToolException("player_recording_already_active", "Finish the current native Player recording first.");
                current = new VmPlayerWindowRecordingSession(request);
                current.Start();
            }
            else
            {
                if (request.Action != "Inspect" && request.Action != "End")
                    throw new VmProjectToolException("player_recording_invalid_arguments", "Action must be Begin, Inspect or End.");
                if (current == null || !current.Matches(request))
                    throw new VmProjectToolException("player_recording_session_identity", "Use the original recording capability and exact Player identity.");
                if (request.Action == "End") current.End();
            }
            return current.Result;
#endif
        }

        private static void Interrupt() => current?.Interrupt();
    }
}
