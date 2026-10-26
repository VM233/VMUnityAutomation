using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using UnityEditor;

namespace VMUnityAutomation.Editor
{
    internal sealed class VmEditorWindowRecordingSession
    {
        private readonly VmEditorWindowRecordingRequest request;
        private readonly EditorWindow window;
        private readonly Action<double> beforeCapture;
        private Action afterCapture;
        private readonly Stopwatch clock = new Stopwatch();
        private readonly List<VmEditorWindowRecordingFrame> frames = new List<VmEditorWindowRecordingFrame>();
        private Process encoder;
        private double nextFrame;
        private double encodingStarted;
        private int width;
        private int height;
        internal VmEditorWindowRecordingResult Result { get; }

        internal VmEditorWindowRecordingSession(VmEditorWindowRecordingRequest request, EditorWindow window,
            Action<double> beforeCapture, Action afterCapture)
        {
            this.request = request;
            this.window = window;
            this.beforeCapture = beforeCapture;
            this.afterCapture = afterCapture;
            string output = Path.GetFullPath(request.OutputPath);
            Result = new VmEditorWindowRecordingResult
            {
                SessionId = Guid.NewGuid().ToString("N"), State = "Recording", OutputPath = output,
                FrameDirectory = output + ".frames", ManifestPath = output + ".recording.json",
                RequestedFramesPerSecond = request.FramesPerSecond, RelativeCadence = request.RelativeCadence,
                EncoderExitCode = -1, Error = ""
            };
        }

        internal void Start()
        {
            Directory.CreateDirectory(Result.FrameDirectory);
            clock.Start();
            EditorApplication.update += Update;
            Save();
        }

        private void Update()
        {
            try
            {
                if (Result.State == "Encoding")
                {
                    if (encoder.HasExited)
                    {
                        Result.EncoderExitCode = encoder.ExitCode;
                        if (encoder.ExitCode != 0 || !File.Exists(Result.OutputPath) || new FileInfo(Result.OutputPath).Length == 0)
                            throw new InvalidOperationException("FFmpeg did not produce a completed MP4. See encoder.log in the frame directory.");
                        Result.State = "Succeeded";
                        Release();
                    }
                    else if (clock.Elapsed.TotalSeconds - encodingStarted > 60)
                        throw new TimeoutException("FFmpeg exceeded the 60 second encoding budget.");
                    return;
                }
                double elapsed = clock.Elapsed.TotalSeconds;
                if (elapsed >= request.DurationSeconds) { End(); return; }
                if (elapsed < nextFrame) return;
                beforeCapture?.Invoke(elapsed);
                string name = "frame_" + frames.Count.ToString("D6", CultureInfo.InvariantCulture) + ".png";
                var capture = VmAutomationScreenshotCommands.CaptureEditorWindow(window, new Dictionary<string, object>
                {
                    ["path"] = Path.Combine(Result.FrameDirectory, name), ["captureMode"] = "view",
                    ["maxDimension"] = request.MaxDimension
                });
                if (VmAutomationResponse.TryGetError(capture, out string message, out _, out _))
                    throw new InvalidOperationException(message);
                var data = (Dictionary<string, object>)capture;
                int frameWidth = Convert.ToInt32(data["width"]), frameHeight = Convert.ToInt32(data["height"]);
                if (width != 0 && (width != frameWidth || height != frameHeight))
                    throw new InvalidOperationException("The Editor view changed dimensions during recording.");
                width = frameWidth;
                height = frameHeight;
                frames.Add(new VmEditorWindowRecordingFrame { File = name, Seconds = elapsed,
                    CaptureCompletedSeconds = clock.Elapsed.TotalSeconds });
                Result.FrameCount = frames.Count;
                Result.CapturedSeconds = clock.Elapsed.TotalSeconds;
                nextFrame = request.RelativeCadence ? elapsed + 1d / request.FramesPerSecond
                    : (Math.Floor(elapsed * request.FramesPerSecond) + 1) / request.FramesPerSecond;
            }
            catch (Exception exception)
            {
                Result.State = "Failed";
                Result.Error = exception.GetType().Name + ": " + exception.Message;
                Release();
            }
        }

        internal void End()
        {
            try { EndCapture(); }
            catch (Exception exception)
            {
                Result.State = "Failed";
                Result.Error = exception.GetType().Name + ": " + exception.Message;
                Release();
            }
        }

        private void EndCapture()
        {
            if (Result.State != "Recording") return;
            Result.CapturedSeconds = clock.Elapsed.TotalSeconds;
            Restore();
            if (frames.Count == 0) throw new InvalidOperationException("No native frames were captured.");
            var concat = new StringBuilder("ffconcat version 1.0\n");
            for (int index = 0; index < frames.Count; index++)
            {
                double end = index + 1 < frames.Count ? frames[index + 1].Seconds : Result.CapturedSeconds;
                concat.Append("file ").Append(frames[index].File).Append('\n').Append("duration ")
                    .Append((end - frames[index].Seconds).ToString("R", CultureInfo.InvariantCulture)).Append('\n');
            }
            concat.Append("file ").Append(frames[frames.Count - 1].File).Append('\n');
            string manifest = Path.Combine(Result.FrameDirectory, "frames.ffconcat");
            File.WriteAllText(manifest, concat.ToString(), new UTF8Encoding(false));
            var start = new ProcessStartInfo
            {
                FileName = request.EncoderPath, WorkingDirectory = Result.FrameDirectory,
                Arguments = "-nostdin -n -loglevel error -f concat -safe 1 -i \"" + manifest +
                    "\" -vf \"fps=30,scale=trunc(iw/2)*2:trunc(ih/2)*2\" -c:v libx264 -preset fast -crf 16 -pix_fmt yuv420p -movflags +faststart \"" + Result.OutputPath + "\"",
                UseShellExecute = false, CreateNoWindow = true
            };
            start.EnvironmentVariables["FFREPORT"] = "file=encoder.log:level=16";
            encoder = Process.Start(start);
            if (encoder == null) throw new InvalidOperationException("FFmpeg process creation failed.");
            encodingStarted = clock.Elapsed.TotalSeconds;
            Result.State = "Encoding";
            Save();
        }

        internal void Interrupt()
        {
            if (Result.ResourcesReleased) return;
            Result.State = "Interrupted";
            Result.Error = "Recording interrupted by assembly reload or Editor quit.";
            Release();
        }

        private void Restore()
        {
            Action restore = afterCapture;
            afterCapture = null;
            restore?.Invoke();
        }

        private void Release()
        {
            EditorApplication.update -= Update;
            try { Restore(); }
            catch (Exception exception) { Result.State = "Failed"; Result.Error += " Restoration: " + exception.Message; }
            if (encoder != null)
            {
                if (!encoder.HasExited) encoder.Kill();
                encoder.Dispose();
                encoder = null;
            }
            Result.ResourcesReleased = true;
            Save();
        }

        private void Save() => File.WriteAllText(Result.ManifestPath,
            JsonConvert.SerializeObject(new { Recording = Result, Frames = frames }, Formatting.Indented));
    }
}
