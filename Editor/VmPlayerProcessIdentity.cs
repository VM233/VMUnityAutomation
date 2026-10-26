using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;

namespace VMUnityAutomation.Editor
{
    internal static class VmPlayerProcessIdentity
    {
        internal static Process Open(VmPlayerIdentityRequest request)
        {
            if (request.ProcessId < 1 || string.IsNullOrEmpty(request.ExecutablePath) ||
                !Path.IsPathRooted(request.ExecutablePath) || request.StartedAt == null ||
                request.StartedAt.Length != 28 || !DateTime.TryParseExact(request.StartedAt, "O",
                    CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime startedAt) ||
                startedAt.Kind != DateTimeKind.Utc)
                throw new VmProjectToolException("invalid_player_identity_arguments", "Supply an exact Player launch identity.");
            string path = Path.GetFullPath(request.ExecutablePath);
            if (!File.Exists(path) || !File.Exists(Path.Combine(Path.GetDirectoryName(path),
                    Path.GetFileNameWithoutExtension(path) + "_Data", "globalgamemanagers")))
                throw new VmProjectToolException("invalid_player_identity_arguments", "The executable must name an existing Windows Unity Player.");
            Process process;
            try { process = Process.GetProcessById(request.ProcessId); }
            catch (ArgumentException)
            {
                throw new VmProjectToolException("player_process_not_running", "The requested Player PID no longer runs.");
            }
            try
            {
                _ = process.Handle;
                if (process.WaitForExit(0))
                    throw new VmProjectToolException("player_process_not_running", "The requested Player already exited.");
                Verify(process, path, startedAt);
                return process;
            }
            catch { process.Dispose(); throw; }
        }

        internal static IntPtr MainWindow(Process process)
        {
            IntPtr window = process.MainWindowHandle;
            GetWindowThreadProcessId(window, out uint owner);
            if (window == IntPtr.Zero || owner != (uint)process.Id)
                throw new VmProjectToolException("player_window_unavailable", "The verified Player has no owned main window.");
            return window;
        }

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

        internal static void Verify(Process process, string executablePath, DateTime startedAt)
        {
            string actualPath = Path.GetFullPath(process.MainModule.FileName);
            DateTime actualStart = process.StartTime.ToUniversalTime();
            if (!string.Equals(actualPath, executablePath, StringComparison.OrdinalIgnoreCase) ||
                actualStart != startedAt)
                throw new VmProjectToolException("player_identity_mismatch",
                    "The live process path or OS creation time differs from the requested Player identity.",
                    false, new Dictionary<string, object>
                    {
                        { "processId", process.Id }, { "executablePath", actualPath },
                        { "startedAt", actualStart.ToString("O") }
                    });
        }
    }
}
