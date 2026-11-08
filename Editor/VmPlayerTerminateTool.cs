using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace VMUnityAutomation.Editor
{
    [VmProjectTool("player/terminate",
        Description = "Force termination of one explicitly identified failed or disposable Windows Unity Player. Requires confirmation and the exact executable path, PID and OS creation time from launch. Verifies a held process handle before one native termination request. Does not save game state, close a window, terminate descendants or wait. Use player/quit for normal shutdown.",
        Dangerous = true, MutatesRuntime = true,
        SideEffects = VmProjectToolSideEffect.ChangesRuntimeState | VmProjectToolSideEffect.PerformsExternalIO,
        ErrorCodes = new[] { "invalid_player_identity_arguments", "player_process_not_running", "player_identity_mismatch",
            "player_terminate_platform_unsupported", "player_terminate_native_failed" },
        Preconditions = new[] { "Windows Editor", "Explicit authorization to discard this Player process state",
            "Exact launch process identity" },
        CompletionEvidence = "One native termination request was accepted for the verified process handle. ExitObserved and ExitCode report only the immediate nonblocking observation; a false value requires separate exit readback. Game shutdown callbacks and saving are not performed.")]
    public sealed class VmPlayerTerminateTool : IVmProjectTool<VmPlayerIdentityRequest, VmPlayerTerminateResult>
    {
        public VmPlayerTerminateResult Execute(VmPlayerIdentityRequest request)
        {
#if !UNITY_EDITOR_WIN
            throw new VmProjectToolException("player_terminate_platform_unsupported", "Player termination requires a Windows Editor.");
#else
            // One identity verification and one kernel request; no enumeration or process-tree traversal.
            using Process process = VmPlayerProcessIdentity.Open(request);
            if (!TerminateProcess(process.Handle, 137))
                throw new VmProjectToolException("player_terminate_native_failed", new Win32Exception(Marshal.GetLastWin32Error()).Message);
            bool exited = process.WaitForExit(0);
            return new VmPlayerTerminateResult
            {
                ExecutablePath = request.ExecutablePath, ProcessId = request.ProcessId, StartedAt = request.StartedAt,
                TerminationRequested = true, ExitObserved = exited, ExitCode = exited ? process.ExitCode : (int?)null
            };
#endif
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool TerminateProcess(System.IntPtr process, uint exitCode);
    }
}
