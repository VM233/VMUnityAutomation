using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace VMUnityAutomation.Editor
{
    [VmProjectTool("player/focus-window",
        Description = "Request native foreground activation of one exact running Windows Unity Player and read back the actual foreground window. Reject mismatched process identity and Windows focus refusal. Does not change topmost state, send keys, attach input threads or activate another application.",
        MutatesRuntime = true,
        SideEffects = VmProjectToolSideEffect.ChangesRuntimeState | VmProjectToolSideEffect.PerformsExternalIO,
        ErrorCodes = new[] { "player_focus_platform_unsupported", "invalid_player_identity_arguments",
            "player_process_not_running", "player_identity_mismatch", "player_window_unavailable", "player_focus_rejected" },
        CompletionEvidence = "The exact held Player's main window equals the actual Win32 foreground window.")]
    public sealed class VmPlayerFocusTool : IVmProjectTool<VmPlayerIdentityRequest, VmPlayerFocusResult>
    {
        public VmPlayerFocusResult Execute(VmPlayerIdentityRequest request)
        {
#if !UNITY_EDITOR_WIN
            throw new VmProjectToolException("player_focus_platform_unsupported", "Native Player focus requires Windows.");
#else
            using Process process = VmPlayerProcessIdentity.Open(request);
            IntPtr window = VmPlayerProcessIdentity.MainWindow(process);
            IntPtr previous = GetForegroundWindow();
            if (previous != window) SetForegroundWindow(window);
            IntPtr actual = GetForegroundWindow();
            if (actual != window)
                throw new VmProjectToolException("player_focus_rejected",
                    $"Windows did not activate Player window {window}; actual foreground is {actual}.");
            return new VmPlayerFocusResult
            {
                PlayerWindow = window.ToString(), PreviousForegroundWindow = previous.ToString(),
                ForegroundWindow = actual.ToString(), Focused = true
            };
#endif
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr window);
    }
}
