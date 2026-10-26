using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using Newtonsoft.Json;
using UnityEditor;

namespace VMUnityAutomation.Editor
{
    internal sealed class VmPlayerWindowRecordingSession
    {
        private readonly VmPlayerWindowRecordingRequest request;
        private readonly Stopwatch clock = new Stopwatch();
        private Process player;
        private Process encoder;
        private IntPtr window;
        private double deadline;
        internal VmPlayerWindowRecordingResult Result { get; }

        internal VmPlayerWindowRecordingSession(VmPlayerWindowRecordingRequest request)
        {
            Validate(request);
            this.request = request;
            string output = Path.GetFullPath(request.OutputPath);
            Result = new VmPlayerWindowRecordingResult
            {
                SessionId = Guid.NewGuid().ToString("N"), State = "Recording", OutputPath = output,
                ManifestPath = output + ".recording.json", EncoderLogPath = output + ".encoder.log",
                ExecutablePath = request.ExecutablePath, ProcessId = request.ProcessId,
                StartedAt = request.StartedAt, X = request.X, Y = request.Y,
                Width = request.Width, Height = request.Height, FramesPerSecond = request.FramesPerSecond,
                RequestedSeconds = request.DurationSeconds
            };
        }

        internal static void Validate(VmPlayerWindowRecordingRequest request)
        {
            if (double.IsNaN(request.DurationSeconds) || double.IsInfinity(request.DurationSeconds) ||
                request.DurationSeconds <= 0 || request.DurationSeconds > 30 || request.FramesPerSecond < 1 ||
                request.FramesPerSecond > 60 || request.X < 0 || request.Y < 0 || request.Width < 2 ||
                request.Height < 2 || request.Width > 4096 || request.Height > 4096 ||
                (long)request.Width * request.Height > 4194304)
                Invalid("Duration, cadence and client region must satisfy the documented bounded domains.");
            if (!Path.IsPathRooted(request.OutputPath ?? "") || request.OutputPath.Contains('"') ||
                !request.OutputPath.EndsWith(".mp4", StringComparison.OrdinalIgnoreCase) ||
                !Path.IsPathRooted(request.EncoderPath ?? "") || !File.Exists(request.EncoderPath))
                Invalid("Provide an absolute new .mp4 output and an installed absolute FFmpeg executable.");
            string output = Path.GetFullPath(request.OutputPath);
            string assets = Path.GetFullPath("Assets") + Path.DirectorySeparatorChar;
            if (output.StartsWith(assets, StringComparison.OrdinalIgnoreCase) ||
                !Directory.Exists(Path.GetDirectoryName(output)) || File.Exists(output) ||
                File.Exists(output + ".recording.json") || File.Exists(output + ".encoder.log"))
                Invalid("Output directory must exist outside Assets; recording outputs must all be new.");
        }

        internal void Start()
        {
            try
            {
                player = VmPlayerProcessIdentity.Open(request);
                Result.ExecutablePath = Path.GetFullPath(request.ExecutablePath);
                window = VmPlayerProcessIdentity.MainWindow(player);
                ReadClient(out int width, out int height);
                if ((long)request.X + request.Width > width || (long)request.Y + request.Height > height)
                    Invalid("The requested capture region extends beyond the verified Player client.");
                Result.ClientWidth = width;
                Result.ClientHeight = height;
                Result.WindowHandle = window.ToString();
                var start = new ProcessStartInfo
                {
                    FileName = request.EncoderPath, WorkingDirectory = Path.GetDirectoryName(Result.OutputPath),
                    Arguments = "-n -loglevel error -thread_queue_size 4 -f gdigrab -draw_mouse 0 -framerate " +
                        request.FramesPerSecond.ToString(CultureInfo.InvariantCulture) + " -offset_x " + request.X +
                        " -offset_y " + request.Y + " -video_size " + request.Width + "x" + request.Height +
                        " -i hwnd=" + window.ToInt64().ToString(CultureInfo.InvariantCulture) + " -t " +
                        request.DurationSeconds.ToString("R", CultureInfo.InvariantCulture) +
                        " -vf \"scale=trunc(iw/2)*2:trunc(ih/2)*2\" -c:v libx264 -preset ultrafast -crf 18 -pix_fmt yuv420p -movflags +faststart \"" +
                        Result.OutputPath + "\"",
                    UseShellExecute = false, CreateNoWindow = true, RedirectStandardInput = true
                };
                start.EnvironmentVariables["FFREPORT"] = "file=" + Path.GetFileName(Result.EncoderLogPath) + ":level=16";
                encoder = Process.Start(start);
                if (encoder == null) throw new InvalidOperationException("FFmpeg process creation failed.");
                deadline = request.DurationSeconds + 60;
                clock.Start();
                EditorApplication.update += Update;
                Save();
            }
            catch (Exception exception)
            {
                Result.State = "Failed";
                Result.Error = exception.GetType().Name + ": " + exception.Message;
                Release(false);
                throw;
            }
        }

        private void Update()
        {
            try
            {
                Result.ElapsedSeconds = clock.Elapsed.TotalSeconds;
                if (encoder.HasExited)
                {
                    Result.EncoderExitCode = encoder.ExitCode;
                    Result.OutputBytes = File.Exists(Result.OutputPath) ? new FileInfo(Result.OutputPath).Length : 0;
                    if (encoder.ExitCode != 0 || Result.OutputBytes == 0)
                        throw new InvalidOperationException("FFmpeg did not finish a nonempty MP4. Inspect the retained encoder log.");
                    Result.State = "Succeeded";
                    Release(true);
                    return;
                }
                if (clock.Elapsed.TotalSeconds > deadline) throw new TimeoutException("FFmpeg exceeded its capture/finalization budget.");
                if (player.HasExited || VmPlayerProcessIdentity.MainWindow(player) != window)
                    throw new InvalidOperationException("The owned Player process/window retired during recording.");
                ReadClient(out int width, out int height);
                if (width != Result.ClientWidth || height != Result.ClientHeight)
                    throw new InvalidOperationException("The Player client changed size during recording.");
            }
            catch (Exception exception)
            {
                Result.State = "Failed";
                Result.Error = exception.GetType().Name + ": " + exception.Message;
                Release(true);
            }
        }

        internal bool Matches(VmPlayerWindowRecordingRequest candidate) =>
            candidate.SessionId == Result.SessionId && candidate.ProcessId == Result.ProcessId &&
            candidate.StartedAt == Result.StartedAt && Path.IsPathRooted(candidate.ExecutablePath ?? "") &&
            string.Equals(Path.GetFullPath(candidate.ExecutablePath), Result.ExecutablePath, StringComparison.OrdinalIgnoreCase);

        internal void End()
        {
            if (Result.ResourcesReleased || Result.State == "Finalizing") return;
            try
            {
                if (!encoder.HasExited) { encoder.StandardInput.WriteLine("q"); encoder.StandardInput.Flush(); }
                Result.State = "Finalizing";
                deadline = clock.Elapsed.TotalSeconds + 60;
                Save();
            }
            catch (Exception exception)
            {
                Result.State = "Failed";
                Result.Error = exception.GetType().Name + ": " + exception.Message;
                Release(true);
            }
        }

        internal void Interrupt()
        {
            if (Result.ResourcesReleased) return;
            Result.State = "Interrupted";
            Result.Error = "Recording interrupted by assembly reload or Editor quit.";
            Release(true);
        }

        private void ReadClient(out int width, out int height)
        {
            if (!GetClientRect(window, out NativeRect rectangle))
                throw new InvalidOperationException("The owned Player client rectangle is unavailable.");
            width = rectangle.Right - rectangle.Left;
            height = rectangle.Bottom - rectangle.Top;
        }

        private void Release(bool save)
        {
            EditorApplication.update -= Update;
            try
            {
                if (encoder != null)
                {
                    try { if (!encoder.HasExited) encoder.Kill(); }
                    finally { encoder.Dispose(); encoder = null; }
                }
            }
            finally
            {
                player?.Dispose();
                player = null;
                Result.ResourcesReleased = true;
                if (save) Save();
            }
        }

        private void Save() => File.WriteAllText(Result.ManifestPath, JsonConvert.SerializeObject(Result, Formatting.Indented));
        private static void Invalid(string message) => throw new VmProjectToolException("player_recording_invalid_arguments", message);

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeRect { public int Left, Top, Right, Bottom; }
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetClientRect(IntPtr window, out NativeRect rectangle);
    }
}
