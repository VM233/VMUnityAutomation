using System.Diagnostics;
using System.IO;
using UnityEditor;

namespace VMUnityAutomation.Editor
{
    [VmProjectTool("player/launch",
        Description = "Launch one existing Windows Unity Player with an explicit argument vector and log destination. Return its actual process identity without waiting, sampling or terminating it. Use player/quit with that identity for durable normal shutdown.",
        MutatesRuntime = true,
        SideEffects = VmProjectToolSideEffect.ChangesRuntimeState | VmProjectToolSideEffect.PerformsExternalIO,
        ErrorCodes = new[] { "invalid_player_launch_arguments", "player_launch_editor_not_idle",
            "player_launch_platform_unsupported", "player_launch_failed" },
        Preconditions = new[] { "Stable Edit Mode", "Windows Editor", "Existing Windows Unity Player",
            "Existing Player log directory" },
        CompletionEvidence = "An OS process was created. The result contains its actual PID and start time; startup, log creation and shutdown require separate evidence.")]
    public sealed class VmPlayerLaunchTool : IVmProjectTool<VmPlayerLaunchRequest, VmPlayerLaunchResult>
    {
        public VmPlayerLaunchResult Execute(VmPlayerLaunchRequest request)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling ||
                EditorApplication.isUpdating)
                throw new VmProjectToolException("player_launch_editor_not_idle", "Player launch requires stable Edit Mode.");
#if !UNITY_EDITOR_WIN
            throw new VmProjectToolException("player_launch_platform_unsupported", "This contract launches Windows Players only.");
#else
            string executablePath = VmPlayerLaunchArguments.AbsolutePath(request.ExecutablePath, "executablePath");
            string logPath = VmPlayerLaunchArguments.AbsolutePath(request.PlayerLogPath, "playerLogPath");
            if (!File.Exists(executablePath) || !string.Equals(Path.GetExtension(executablePath), ".exe",
                    System.StringComparison.OrdinalIgnoreCase) ||
                !File.Exists(Path.Combine(Path.GetDirectoryName(executablePath),
                    Path.GetFileNameWithoutExtension(executablePath) + "_Data", "globalgamemanagers")))
                throw VmPlayerLaunchArguments.Invalid("executablePath must name an existing Windows Unity Player.");
            if (!Directory.Exists(Path.GetDirectoryName(logPath)))
                throw VmPlayerLaunchArguments.Invalid("playerLogPath requires an existing parent directory.");
            string arguments = VmPlayerLaunchArguments.Encode(executablePath, request.PlayerArguments, logPath);
            using Process process = Process.Start(new ProcessStartInfo
            {
                FileName = executablePath,
                WorkingDirectory = Path.GetDirectoryName(executablePath),
                Arguments = arguments,
                UseShellExecute = false
            });
            if (process == null)
                throw new VmProjectToolException("player_launch_failed", $"The OS did not create '{executablePath}'.");
            return new VmPlayerLaunchResult
            {
                ExecutablePath = executablePath,
                ProcessId = process.Id,
                StartedAt = process.StartTime.ToUniversalTime().ToString("O"),
                PlayerLogPath = logPath,
                PlayerArguments = (string[])request.PlayerArguments.Clone()
            };
#endif
        }
    }
}
