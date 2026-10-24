using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace VMUnityAutomation.Editor
{
    internal sealed class VmPlayerBuildObservation : IDisposable
    {
        private readonly Process process;
        private readonly VmPlayerBuildObservationOptions options;
        private readonly bool terminateAfter;
        private readonly bool captureWindow;
        private readonly Func<IntPtr, Dictionary<string, object>> capture;
        private readonly Func<double> clock;
        private readonly double sampleDeadline;
        private double terminationDeadline;
        private bool killRequested;
        private bool sampled;
        public Dictionary<string, object> Result { get; }
        public bool IsTerminal { get; private set; }

        public VmPlayerBuildObservation(Process process, string executablePath, string logPath,
            VmPlayerBuildObservationOptions options, bool terminateAfter, bool captureWindow,
            Func<IntPtr, Dictionary<string, object>> capture, Func<double> clock)
        {
            this.process = process;
            this.options = options;
            this.terminateAfter = terminateAfter;
            this.captureWindow = captureWindow;
            this.capture = capture;
            this.clock = clock;
            // Acquire the native handle once. All observations and optional kill use this lease.
            _ = process.Handle;
            DateTime startedAt = process.StartTime.ToUniversalTime();
            sampleDeadline = clock() + options.RunSeconds;
            Result = new Dictionary<string, object>
            {
                { "success", true }, { "phase", "observing" },
                { "executablePath", executablePath }, { "processId", process.Id },
                { "startedAt", startedAt.ToString("O") }, { "runSeconds", options.RunSeconds },
                { "playerLogPath", logPath }, { "terminatedAfter", false },
                { "exited", false }, { "exitCode", "" }, { "screenshot", null },
            };
        }

        // Returns true only when the published observation changes.
        public bool Advance()
        {
            if (IsTerminal) return false;
            process.Refresh();
            double now = clock();
            bool exited = process.HasExited;
            if (killRequested)
            {
                if (exited) return Complete();
                if (now >= terminationDeadline) return Fail("Player termination was not observed within 5000 ms.");
                return false;
            }
            if (exited && process.ExitCode != 0)
                return Fail($"Player exited with native exit code {process.ExitCode} before requested termination.");
            if (!exited && now < sampleDeadline) return false;

            bool changed = !sampled;
            if (!sampled)
            {
                sampled = true;
                DateTime sampledAt = DateTime.UtcNow;
                Result["sampledAt"] = sampledAt.ToString("O");
                Result["sampleElapsedSeconds"] = (sampledAt - process.StartTime.ToUniversalTime()).TotalSeconds;
                Result["phase"] = captureWindow ? "waiting-for-window" : "sampled";
            }
            if (captureWindow && Result["screenshot"] == null)
            {
#if UNITY_EDITOR_WIN
                IntPtr window = exited ? IntPtr.Zero : process.MainWindowHandle;
                if (window == IntPtr.Zero)
                {
                    if (exited || now >= sampleDeadline + options.WindowWaitMs / 1000d)
                        Result["screenshot"] = new Dictionary<string, object>
                        {
                            { "success", false },
                            { "error", "Process main window was not found before exit or the window deadline." },
                        };
                    else return changed;
                }
                else Result["screenshot"] = capture(window);
#else
                Result["screenshot"] = new Dictionary<string, object>
                {
                    { "success", false }, { "error", "External process window capture is Windows-only." },
                };
#endif
            }
            if (!exited && terminateAfter)
            {
                process.Kill();
                killRequested = true;
                terminationDeadline = now + 5;
                Result["terminatedAfter"] = true;
                Result["phase"] = "terminating";
                return true;
            }
            return Complete();
        }

        private bool Complete()
        {
            if (Result["screenshot"] is Dictionary<string, object> screenshot &&
                !(bool)screenshot["success"])
                return Fail((string)screenshot["error"]);
            RecordExit();
            Result["phase"] = "completed";
            IsTerminal = true;
            return true;
        }

        public bool Fail(string error)
        {
            RecordExit();
            Result["success"] = false;
            Result["error"] = error;
            Result["phase"] = "failed";
            IsTerminal = true;
            return true;
        }

        private void RecordExit()
        {
            bool exited = process.HasExited;
            Result["exited"] = exited;
            Result["exitCode"] = exited ? process.ExitCode.ToString() : "";
        }

        public void Dispose() => process.Dispose();
    }
}
