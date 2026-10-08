using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using UnityEditor;

namespace VMUnityAutomation.Editor
{
    [InitializeOnLoad]
    internal static class VmAutomationPlayerQuitCommands
    {
        internal const string JobType = "player-quit";
        internal const string Operation = "player/quit";
        internal const string WaitingForExitPhase = "waiting-for-player-exit";
        private static Process ownedProcess;
        private static string ownedJobId;
        private static long deadlineTimestamp;
        private static long nextObservationTimestamp;

        static VmAutomationPlayerQuitCommands()
        {
            AssemblyReloadEvents.beforeAssemblyReload += RetireDomain;
            EditorApplication.quitting += RetireDomain;
        }

        internal static object Start(Dictionary<string, object> arguments)
        {
#if !UNITY_EDITOR_WIN
            return VmAutomationResponse.Error("Normal Player shutdown requires a Windows Editor.",
                "player_quit_platform_unsupported");
#else
            try
            {
                var request = new Dictionary<string, object>(arguments);
                int processId = ReadInteger(request, "processId", 0);
                int timeoutMs = ReadInteger(request, "timeoutMs", 15000);
                if (processId < 1 || timeoutMs < 100 || timeoutMs > 60000)
                    throw Invalid("processId must be positive; timeoutMs must be 100 through 60000.");
                DateTime startedAt = ReadStartedAt(request);
                string path = ReadString(request, "executablePath");
                if (path.Length == 0 || path.Length > 4096 || path.IndexOf('\0') >= 0 ||
                    !Path.IsPathRooted(path) || Path.GetPathRoot(path).Length < 3)
                    throw Invalid("executablePath requires an absolute drive or UNC path of at most 4096 code units.");
                path = Path.GetFullPath(path);
                if (!File.Exists(path) || !string.Equals(Path.GetExtension(path), ".exe",
                        StringComparison.OrdinalIgnoreCase) ||
                    !File.Exists(Path.Combine(Path.GetDirectoryName(path),
                        Path.GetFileNameWithoutExtension(path) + "_Data", "globalgamemanagers")))
                    throw Invalid("executablePath must name an existing Windows Unity Player.");
                request["executablePath"] = path;
                request["startedAt"] = startedAt.ToString("O");
                request["processId"] = processId;
                request["timeoutMs"] = timeoutMs;
                return VmAutomationWorkspaceJobRunner.StartPlayerQuit(request);
            }
            catch (VmProjectToolException exception)
            {
                return VmAutomationResponse.Error(exception.Message, exception.ErrorCode);
            }
            catch (ArgumentException exception)
            {
                return VmAutomationResponse.Error(exception.Message, "invalid_player_quit_arguments");
            }
#endif
        }

        internal static bool ExecutePhase(VmAutomationWorkspaceJob job)
        {
            if (job.JobType != JobType) return false;
            try
            {
                switch (job.Phase)
                {
                    case VmAutomationWorkspaceJobRunner.WaitingForEditorPhase:
                        BeginQuit(job);
                        break;
                    case WaitingForExitPhase:
                        ObserveExit(job);
                        break;
                    default:
                        throw new InvalidOperationException($"Player quit job '{job.JobId}' has unknown phase '{job.Phase}'.");
                }
            }
            catch (VmProjectToolException exception)
            {
                VmAutomationWorkspaceJobRunner.Fail(job, VmAutomationResponse.Error(
                    exception.Message, exception.ErrorCode, false, exception.Details));
            }
            catch (Win32Exception exception)
            {
                VmAutomationWorkspaceJobRunner.Fail(job, VmAutomationResponse.Error(
                    exception.Message, "player_quit_native_failed"));
            }
            return true;
        }

        internal static void RecoverAfterReload(VmAutomationWorkspaceJob job)
        {
            if (job.TransactionState != null)
            {
                VmAutomationWorkspaceJobRunner.Fail(job, VmAutomationResponse.Error(
                    "The Player close request crossed an assembly reload. Its process handle is unavailable; " +
                    "exit was not confirmed and the close request will not be replayed.",
                    "player_quit_outcome_uncertain_after_reload", false,
                    new Dictionary<string, object> { { "identity", job.Request }, { "closeRequest", job.TransactionState } }));
            }
            else
            {
                job.StatusMessage = "Recovered before requesting normal Player shutdown.";
                VmAutomationWorkspaceJobRunner.Persist(job);
            }
        }

        internal static void Retire(string jobId)
        {
            if (!string.Equals(ownedJobId, jobId, StringComparison.Ordinal) || ownedProcess == null) return;
            ownedProcess.Dispose();
            ownedProcess = null;
            ownedJobId = null;
        }

        internal static void VerifyIdentity(Process process, string executablePath, DateTime startedAt)
        {
            string actualPath = Path.GetFullPath(process.MainModule.FileName);
            DateTime actualStart = process.StartTime.ToUniversalTime();
            if (!string.Equals(actualPath, executablePath, StringComparison.OrdinalIgnoreCase) ||
                actualStart != startedAt)
                throw new VmProjectToolException("player_quit_identity_mismatch",
                    "The live process path or OS creation time differs from the requested Player identity.",
                    false, new Dictionary<string, object>
                    {
                        { "processId", process.Id }, { "executablePath", actualPath },
                        { "startedAt", actualStart.ToString("O") },
                    });
        }

        private static void BeginQuit(VmAutomationWorkspaceJob job)
        {
            if (ownedProcess != null)
                throw new InvalidOperationException($"Player quit job '{ownedJobId}' already owns the native process lease.");
            Process process;
            try { process = Process.GetProcessById(ReadInteger(job.Request, "processId", 0)); }
            catch (ArgumentException)
            {
                throw new VmProjectToolException("player_quit_process_not_running", "The requested Player PID is no longer running.");
            }
            try
            {
                // Hold this OS process object before reading identity, so PID reuse
                // cannot redirect a later close request or exit observation.
                _ = process.Handle;
                if (process.WaitForExit(0))
                    throw new VmProjectToolException("player_quit_process_not_running", "The requested Player has already exited.");
                VerifyIdentity(process, ReadString(job.Request, "executablePath"), ReadStartedAt(job.Request));
            }
            catch
            {
                process.Dispose();
                throw;
            }
            ownedProcess = process;
            ownedJobId = job.JobId;
            long now = Stopwatch.GetTimestamp();
            deadlineTimestamp = now + (long)ReadInteger(job.Request, "timeoutMs", 15000) * Stopwatch.Frequency / 1000;
            nextObservationTimestamp = now;
            job.TransactionState = new Dictionary<string, object>
            {
                { "quitRequestedAt", DateTime.UtcNow.ToString("O") },
                { "closeRequestAccepted", false },
            };
            job.Phase = WaitingForExitPhase;
            job.StatusMessage = "Requesting normal Player shutdown.";
            VmAutomationWorkspaceJobRunner.Persist(job);
            if (!RequestWindowClose(process.MainWindowHandle, process.Id))
                throw new VmProjectToolException("player_quit_window_unavailable", "The verified Player has no enabled main window to close.");
            job.TransactionState["closeRequestAccepted"] = true;
            job.StatusMessage = "Normal close requested; waiting for the native process exit signal.";
            VmAutomationWorkspaceJobRunner.Persist(job);
            ObserveExit(job);
        }

        internal static bool RequestWindowClose(IntPtr window, int processId)
        {
            if (window == IntPtr.Zero) return false;
            GetWindowThreadProcessId(window, out uint actualProcessId);
            if (actualProcessId != (uint)processId)
                throw new VmProjectToolException("player_quit_window_owner_mismatch",
                    $"Window '{window}' belongs to PID {actualProcessId}, not verified Player PID {processId}.");
            if (!IsWindowEnabled(window)) return false;
            // Unity Mono's Process.CloseMainWindow calls TerminateProcess(-2).
            // Post the native close message so Application.quitting can run.
            if (!PostMessageW(window, 0x0010, IntPtr.Zero, IntPtr.Zero))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            return true;
        }

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr window, out uint processId);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindowEnabled(IntPtr window);

        [DllImport("user32.dll", ExactSpelling = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool PostMessageW(IntPtr window, uint message, IntPtr wParam, IntPtr lParam);

        private static void ObserveExit(VmAutomationWorkspaceJob job)
        {
            if (ownedProcess == null || ownedJobId != job.JobId)
                throw new InvalidOperationException($"Player quit job '{job.JobId}' does not own its native process handle.");
            long now = Stopwatch.GetTimestamp();
            if (now < nextObservationTimestamp && now < deadlineTimestamp) return;
            nextObservationTimestamp = now + Stopwatch.Frequency / 10;
            if (ownedProcess.WaitForExit(0))
            {
                job.Result = new Dictionary<string, object>
                {
                    { "executablePath", job.Request["executablePath"] },
                    { "processId", job.Request["processId"] },
                    { "startedAt", job.Request["startedAt"] },
                    { "quitRequestedAt", job.TransactionState["quitRequestedAt"] },
                    { "closeRequestAccepted", true }, { "exitObserved", true },
                    { "exitCode", ownedProcess.ExitCode },
                    { "exitedAt", ownedProcess.ExitTime.ToUniversalTime().ToString("O") },
                    { "observedAt", DateTime.UtcNow.ToString("O") },
                };
                job.Status = "succeeded";
                job.Phase = "succeeded";
                job.StatusMessage = "The verified Player's native exit signal was observed.";
                job.CompletedAt = DateTime.UtcNow;
                Retire(job.JobId);
                VmAutomationWorkspaceJobRunner.Persist(job);
            }
            else if (now >= deadlineTimestamp)
            {
                VmAutomationWorkspaceJobRunner.Fail(job, VmAutomationResponse.Error(
                    "The Player did not exit within timeoutMs after the normal close request. No kill was issued.",
                    "player_quit_timeout", false, job.TransactionState));
            }
        }

        private static void RetireDomain()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= RetireDomain;
            EditorApplication.quitting -= RetireDomain;
            Retire(ownedJobId);
        }

        private static DateTime ReadStartedAt(IReadOnlyDictionary<string, object> request)
        {
            string value = ReadString(request, "startedAt");
            if (value.Length != 28 || !DateTime.TryParseExact(value, "O", CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind, out DateTime result) || result.Kind != DateTimeKind.Utc)
                throw Invalid("startedAt must be the exact round-trip UTC OS creation time returned by Player launch.");
            return result;
        }

        private static int ReadInteger(IReadOnlyDictionary<string, object> request, string field, int defaultValue)
        {
            if (!request.TryGetValue(field, out object value)) return defaultValue;
            if (!int.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out int result))
                throw Invalid($"{field} requires an integer.");
            return result;
        }

        private static string ReadString(IReadOnlyDictionary<string, object> request, string field)
            => request.TryGetValue(field, out object value) && value is string result ? result : "";

        private static VmProjectToolException Invalid(string message)
            => new("invalid_player_quit_arguments", message);
    }
}
