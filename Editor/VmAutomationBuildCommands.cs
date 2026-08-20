using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
#if UNITY_EDITOR_WIN
using System.Runtime.InteropServices;
#endif
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace VMUnityAutomation.Editor
{
    [InitializeOnLoad]
    public static class VmAutomationBuildCommands
    {
        internal const string JobType = "player-build";
        private static PlayerBuildJob _job;
        private static bool _updateRegistered;

        static VmAutomationBuildCommands()
        {
            _job = LoadBuildJob();
            if (_job == null || _job.IsTerminal)
                return;

            if (_job.Status == "running")
            {
                _job.Status = "failed";
                _job.Error = "Player build was interrupted by an Editor domain reload or shutdown.";
                _job.UpdatedAt = DateTime.UtcNow;
                SaveBuildJob();
                return;
            }

            EnsureBuildUpdateRegistered();
        }

        private static object ExecuteBuild(Dictionary<string, object> args)
        {
            string targetStr = args.ContainsKey("target") ? args["target"].ToString() : "StandaloneWindows64";
            string outputPath = args.ContainsKey("outputPath") ? args["outputPath"].ToString() : "";
            bool devBuild = args.ContainsKey("developmentBuild") && Convert.ToBoolean(args["developmentBuild"]);

            if (string.IsNullOrEmpty(outputPath))
                return new { error = "outputPath is required" };

            if (!Enum.TryParse<BuildTarget>(targetStr, out var target))
                return new { error = $"Unknown build target: {targetStr}" };

            // Get scenes
            string[] scenes;
            if (args.ContainsKey("scenes"))
            {
                var sceneList = args["scenes"] as List<object>;
                scenes = sceneList?.Select(s => s.ToString()).ToArray() ?? new string[0];
            }
            else
            {
                scenes = EditorBuildSettings.scenes
                    .Where(s => s.enabled)
                    .Select(s => s.path)
                    .ToArray();
            }

            if (scenes.Length == 0)
                return new { error = "No scenes to build. Add scenes to Build Settings or provide them." };

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = target,
                options = devBuild ? BuildOptions.Development : BuildOptions.None,
            };

            try
            {
                var report = BuildPipeline.BuildPlayer(options);
                return BuildReportToDictionary(report);
            }
            catch (Exception ex)
            {
                return new { error = $"Build failed: {ex.Message}" };
            }
        }

        public static object StartBuild(Dictionary<string, object> args)
        {
            bool clearStuck = GetBool(args, "clearStuck", false);
            if (_job != null && !_job.IsTerminal)
            {
                string owner = _job.Arguments != null &&
                               _job.Arguments.TryGetValue("_agentId", out object ownerValue)
                    ? ownerValue?.ToString()
                    : "anonymous";
                string requester = GetString(args, "_agentId");
                if (string.IsNullOrEmpty(requester)) requester = "anonymous";
                if (!string.Equals(owner, requester, StringComparison.Ordinal))
                    return VmAutomationResponse.Error("Player build job belongs to another agent.",
                        "job_owner_mismatch");
            }
            if (_job != null && !_job.IsTerminal && !clearStuck)
            {
                var active = BuildJobResponse(_job);
                active["success"] = false;
                active["error"] = "A Player build job is already running.";
                return active;
            }

            _job = new PlayerBuildJob
            {
                JobId = Guid.NewGuid().ToString("N").Substring(0, 12),
                Status = "queued",
                Arguments = args != null
                    ? MiniJson.Deserialize(MiniJson.Serialize(args)) as Dictionary<string, object>
                    : new Dictionary<string, object>(),
                StartedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
            _job.Arguments ??= new Dictionary<string, object>();
            _job.Arguments.Remove("clearStuck");
            SaveBuildJob();
            EnsureBuildUpdateRegistered();
            return BuildJobResponse(_job);
        }

        public static object GetBuildJob(Dictionary<string, object> args)
        {
            if (_job == null)
                _job = LoadBuildJob();
            if (_job == null)
                return new { error = "No Player build job was found." };
            string owner = _job.Arguments != null && _job.Arguments.TryGetValue("_agentId", out object ownerValue)
                ? ownerValue?.ToString()
                : "anonymous";
            if (!VmAutomationJobHistory.CanAccess(JobType, _job.JobId, owner, args))
                return VmAutomationResponse.Error(
                    "Player build job belongs to another agent and the jobAccessToken was not supplied.",
                    "job_owner_mismatch");

            string jobId = GetString(args, "jobId");
            if (!string.IsNullOrEmpty(jobId) && jobId != _job.JobId)
                return new { error = $"Player build job '{jobId}' was not found." };

            var response = BuildJobResponse(_job);
            if (_job.IsTerminal && GetBool(args, "clear", false))
            {
                DeleteBuildJobFile();
                _job = null;
                response["cleared"] = true;
            }
            return response;
        }

        internal static object CancelBuild(Dictionary<string, object> args)
        {
            if (_job == null)
                _job = LoadBuildJob();
            if (_job == null)
                return VmAutomationResponse.Error("No Player build job was found.", "job_not_found");

            string jobId = GetString(args, "jobId");
            if (!string.IsNullOrEmpty(jobId) && jobId != _job.JobId)
                return VmAutomationResponse.Error($"Player build job '{jobId}' was not found.", "job_not_found");

            string owner = _job.Arguments != null &&
                           _job.Arguments.TryGetValue("_agentId", out object ownerValue)
                ? ownerValue?.ToString()
                : "anonymous";
            if (!VmAutomationJobHistory.CanAccess(JobType, _job.JobId, owner, args))
                return VmAutomationResponse.Error(
                    "Player build job belongs to another agent and the jobAccessToken was not supplied.",
                    "job_owner_mismatch");

            if (_job.IsTerminal)
                return VmAutomationResponse.Error("Player build job is already terminal.", "job_already_terminal",
                    false, BuildJobResponse(_job));

            if (_job.Status == "queued")
            {
                _job.CancelRequested = true;
                _job.Status = "canceled";
                _job.Error = "Canceled before the Player build started.";
                _job.UpdatedAt = DateTime.UtcNow;
                SaveBuildJob();
                UnregisterBuildUpdate();
                var canceled = BuildJobResponse(_job);
                canceled["cancelMode"] = "pre-start";
                canceled["canceled"] = true;
                return canceled;
            }

            MethodInfo cancelMethod = typeof(BuildPipeline)
                .GetMethods(BindingFlags.Static | BindingFlags.Public |
                            BindingFlags.NonPublic)
                .FirstOrDefault(method => method.Name == "CancelBuild" &&
                                          method.GetParameters().Length == 0);
            if (cancelMethod == null)
            {
                return VmAutomationResponse.Error(
                    "This Unity version cannot preempt a Player build after BuildPipeline.BuildPlayer has started.",
                    "job_not_preemptible", false, new Dictionary<string, object>
                    {
                        { "jobId", _job.JobId },
                        { "jobType", JobType },
                        { "status", _job.Status },
                        { "cancelableBeforeStart", true },
                    });
            }

            try
            {
                object result = cancelMethod.Invoke(null, null);
                bool accepted = !(result is bool boolResult) || boolResult;
                if (accepted)
                {
                    _job.CancelRequested = true;
                    _job.UpdatedAt = DateTime.UtcNow;
                    SaveBuildJob();
                }
                return new Dictionary<string, object>
                {
                    { "success", accepted },
                    { "jobId", _job.JobId },
                    { "jobType", JobType },
                    { "status", _job.Status },
                    { "cancelRequested", accepted },
                    { "cancelMode", "unity-build-pipeline" },
                };
            }
            catch (Exception exception)
            {
                return VmAutomationResponse.Error(exception.GetBaseException().Message, "job_cancel_failed");
            }
        }

        private static object ExecuteBuildJob(Dictionary<string, object> args)
        {
            string outputPath = GetString(args, "outputPath");
            if (string.IsNullOrEmpty(outputPath))
                return new { error = "outputPath is required" };

            bool overwrite = GetBool(args, "overwrite", true);
            if (overwrite)
                DeleteExistingBuildOutput(outputPath);

            var buildResult = VmAutomationResponse.ToDictionary(ExecuteBuild(args));
            if (buildResult == null)
                return new { error = "Build did not return a structured result." };

            bool buildSucceeded = buildResult.TryGetValue("success", out object successObj) &&
                                  successObj is bool success && success;
            bool runExecutable = GetBool(args, "run", true);
            if (!buildSucceeded || !runExecutable)
            {
                var result = new Dictionary<string, object>
                {
                    { "success", buildSucceeded },
                    { "build", buildResult },
                    { "run", null },
                };
                if (!buildSucceeded && buildResult.TryGetValue("error", out object error))
                    result["error"] = error;
                return result;
            }

            string executablePath = GetString(buildResult, "outputPath");
            if (string.IsNullOrEmpty(executablePath))
                executablePath = outputPath;

            var runResult = RunBuildExecutable(executablePath, args);
            return new Dictionary<string, object>
            {
                { "success", buildSucceeded && !runResult.ContainsKey("error") },
                { "build", buildResult },
                { "run", runResult },
            };
        }

        private static void ContinueBuildJob()
        {
            if (_job == null || _job.IsTerminal)
            {
                UnregisterBuildUpdate();
                return;
            }

            if (_job.Status != "queued" || EditorApplication.isCompiling || EditorApplication.isUpdating)
                return;

            _job.Status = "running";
            _job.UpdatedAt = DateTime.UtcNow;
            SaveBuildJob();
            try
            {
                _job.Result = VmAutomationResponse.ToDictionary(ExecuteBuildJob(_job.Arguments));
                bool success = _job.Result != null && _job.Result.TryGetValue("success", out object value) &&
                               value is bool succeeded && succeeded;
                _job.Status = _job.CancelRequested && !success
                    ? "canceled"
                    : success ? "succeeded" : "failed";
                if (!success && _job.Result != null && _job.Result.TryGetValue("error", out object error))
                    _job.Error = error?.ToString();
                if (_job.Status == "canceled" && string.IsNullOrEmpty(_job.Error))
                    _job.Error = "Canceled by request.";
            }
            catch (Exception ex)
            {
                _job.Status = "failed";
                _job.Error = ex.GetBaseException().Message;
            }
            finally
            {
                _job.UpdatedAt = DateTime.UtcNow;
                SaveBuildJob();
                UnregisterBuildUpdate();
            }
        }

        private static Dictionary<string, object> BuildJobResponse(PlayerBuildJob job)
        {
            var response = new Dictionary<string, object>
            {
                { "success", job.IsTerminal ? job.Status == "succeeded" : true },
                { "jobId", job.JobId },
                { "jobType", JobType },
                { "status", job.Status },
                { "cancelRequested", job.CancelRequested },
                { "pollRoute", "build/get-job" },
                { "startedAt", job.StartedAt.ToString("O") },
                { "updatedAt", job.UpdatedAt.ToString("O") },
            };
            if (!string.IsNullOrEmpty(job.Error))
                response["error"] = job.Error;
            if (job.Result != null)
                response["result"] = job.Result;
            VmAutomationJobHistory.PublishAccessToken(response, JobType, job.JobId,
                GetString(job.Arguments, "_agentId"));
            return response;
        }

        private static void EnsureBuildUpdateRegistered()
        {
            if (_updateRegistered)
                return;
            EditorApplication.update += ContinueBuildJob;
            _updateRegistered = true;
        }

        private static void UnregisterBuildUpdate()
        {
            if (!_updateRegistered)
                return;
            EditorApplication.update -= ContinueBuildJob;
            _updateRegistered = false;
        }

        private static string GetBuildJobPath()
        {
            return Path.Combine(GetProjectRoot(), "Library", "VMUnityAutomation", "player-build-job.json");
        }

        private static void SaveBuildJob()
        {
            if (_job == null)
                return;
            string path = GetBuildJobPath();
            VmAutomationPersistenceFile.WriteAllText(path, MiniJson.Serialize(_job.ToDictionary()));
            string owner = _job.Arguments != null && _job.Arguments.TryGetValue("_agentId", out object value)
                ? value?.ToString()
                : "anonymous";
            VmAutomationJobHistory.Record(JobType, _job.JobId, owner, _job.Status, BuildJobResponse(_job));
        }

        private static PlayerBuildJob LoadBuildJob()
        {
            try
            {
                string path = GetBuildJobPath();
                if (!VmAutomationPersistenceFile.TryReadAllText(path, out string contents))
                    return null;
                return PlayerBuildJob.FromDictionary(
                    MiniJson.Deserialize(contents) as Dictionary<string, object>);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[VM Unity Automation Build] Failed to restore Player build job: {ex.Message}");
                return null;
            }
        }

        private static void DeleteBuildJobFile()
        {
            VmAutomationPersistenceFile.DeleteIfExists(GetBuildJobPath());
        }

        private static Dictionary<string, object> RunBuildExecutable(string executablePath, Dictionary<string, object> args)
        {
            string absolutePath = Path.IsPathRooted(executablePath)
                ? executablePath
                : Path.GetFullPath(Path.Combine(GetProjectRoot(), executablePath));

            if (!File.Exists(absolutePath))
                return new Dictionary<string, object> { { "error", $"Executable not found at '{absolutePath}'" } };

            int runSeconds = Math.Max(0, GetInt(args, "runSeconds", 5));
            bool terminateAfter = GetBool(args, "terminateAfter", true);
            bool captureWindow = GetBool(args, "captureWindow", false);
            string screenshotPath = GetString(args, "screenshotPath");
            if (string.IsNullOrEmpty(screenshotPath))
                screenshotPath = Path.Combine(GetProjectRoot(), "Builds", "VmAutomation_RunTest.png");

            var processInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = absolutePath,
                WorkingDirectory = Path.GetDirectoryName(absolutePath) ?? GetProjectRoot(),
                UseShellExecute = false,
            };

            var startedAt = DateTime.UtcNow;
            var process = System.Diagnostics.Process.Start(processInfo);
            if (process == null)
                return new Dictionary<string, object> { { "error", $"Failed to start '{absolutePath}'" } };

            Dictionary<string, object> screenshot = null;
            if (captureWindow)
                screenshot = CaptureProcessWindow(process, screenshotPath, GetInt(args, "windowWaitMs", 5000));

            if (runSeconds > 0)
                System.Threading.Thread.Sleep(runSeconds * 1000);

            bool exited = process.HasExited;
            int? exitCode = exited ? process.ExitCode : (int?)null;
            if (!exited && terminateAfter)
            {
                try
                {
                    process.Kill();
                    process.WaitForExit(5000);
                    exited = process.HasExited;
                    exitCode = exited ? process.ExitCode : (int?)null;
                }
                catch (Exception ex)
                {
                    return new Dictionary<string, object>
                    {
                        { "error", $"Failed to terminate process: {ex.Message}" },
                        { "processId", process.Id },
                    };
                }
            }

            string logPath = GetPlayerLogPath();
            return new Dictionary<string, object>
            {
                { "success", true },
                { "executablePath", absolutePath },
                { "processId", process.Id },
                { "startedAt", startedAt.ToString("O") },
                { "runSeconds", runSeconds },
                { "terminatedAfter", terminateAfter },
                { "exited", exited },
                { "exitCode", exitCode.HasValue ? exitCode.Value.ToString() : "" },
                { "playerLogPath", logPath },
                { "playerLogTail", ReadTail(logPath, GetInt(args, "logTailLines", 120)) },
                { "screenshot", screenshot },
            };
        }

        private static Dictionary<string, object> BuildReportToDictionary(BuildReport report)
        {
            return new Dictionary<string, object>
            {
                { "success", report.summary.result == BuildResult.Succeeded },
                { "result", report.summary.result.ToString() },
                { "totalErrors", report.summary.totalErrors },
                { "totalWarnings", report.summary.totalWarnings },
                { "totalTime", report.summary.totalTime.TotalSeconds },
                { "outputPath", report.summary.outputPath },
                { "totalSize", report.summary.totalSize },
                { "platform", report.summary.platform.ToString() },
                { "compilationDiagnostics", VmAutomationConsoleCommands.GetCompilationDiagnosticsSummary() },
            };
        }

        private static void DeleteExistingBuildOutput(string outputPath)
        {
            string absolutePath = Path.IsPathRooted(outputPath)
                ? outputPath
                : Path.GetFullPath(Path.Combine(GetProjectRoot(), outputPath));

            if (File.Exists(absolutePath))
                File.Delete(absolutePath);

            string dataFolder = Path.Combine(Path.GetDirectoryName(absolutePath) ?? "", Path.GetFileNameWithoutExtension(absolutePath) + "_Data");
            if (Directory.Exists(dataFolder))
                Directory.Delete(dataFolder, true);
        }

        private static string GetPlayerLogPath()
        {
#if UNITY_EDITOR_WIN
            string localLow = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
                .Replace("AppData\\Local", "AppData\\LocalLow");
            return Path.Combine(localLow, PlayerSettings.companyName, PlayerSettings.productName, "Player.log");
#else
            return "";
#endif
        }

        private static string ReadTail(string path, int maxLines)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
                return "";

            var lines = File.ReadLines(path).Reverse().Take(Math.Max(1, maxLines)).Reverse();
            return string.Join("\n", lines);
        }

        private static string GetString(Dictionary<string, object> args, string key)
        {
            return args != null && args.ContainsKey(key) && args[key] != null ? args[key].ToString() : "";
        }

        private static int GetInt(Dictionary<string, object> args, string key, int defaultValue)
        {
            if (args == null || !args.ContainsKey(key) || args[key] == null)
                return defaultValue;

            return int.TryParse(args[key].ToString(), out int parsed) ? parsed : defaultValue;
        }

        private static bool GetBool(Dictionary<string, object> args, string key, bool defaultValue)
        {
            if (args == null || !args.ContainsKey(key) || args[key] == null)
                return defaultValue;

            if (args[key] is bool value)
                return value;

            return bool.TryParse(args[key].ToString(), out bool parsed) ? parsed : defaultValue;
        }

        private static string GetProjectRoot()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        }

        private sealed class PlayerBuildJob
        {
            public string JobId;
            public string Status;
            public Dictionary<string, object> Arguments;
            public Dictionary<string, object> Result;
            public string Error;
            public bool CancelRequested;
            public DateTime StartedAt;
            public DateTime UpdatedAt;

            public bool IsTerminal => Status == "succeeded" || Status == "failed" || Status == "canceled";

            public Dictionary<string, object> ToDictionary()
            {
                return new Dictionary<string, object>
                {
                    { "jobId", JobId },
                    { "status", Status },
                    { "arguments", Arguments },
                    { "result", Result },
                    { "error", Error ?? "" },
                    { "cancelRequested", CancelRequested },
                    { "startedAt", StartedAt.ToString("O") },
                    { "updatedAt", UpdatedAt.ToString("O") },
                };
            }

            public static PlayerBuildJob FromDictionary(Dictionary<string, object> values)
            {
                if (values == null)
                    return null;
                return new PlayerBuildJob
                {
                    JobId = GetString(values, "jobId"),
                    Status = GetString(values, "status"),
                    Arguments = values.TryGetValue("arguments", out object arguments)
                        ? arguments as Dictionary<string, object>
                        : new Dictionary<string, object>(),
                    Result = values.TryGetValue("result", out object result)
                        ? result as Dictionary<string, object>
                        : null,
                    Error = GetString(values, "error"),
                    CancelRequested = GetBool(values, "cancelRequested", false),
                    StartedAt = ParseDate(values, "startedAt"),
                    UpdatedAt = ParseDate(values, "updatedAt"),
                };
            }

            private static DateTime ParseDate(Dictionary<string, object> values, string key)
            {
                return DateTime.TryParse(GetString(values, key), null,
                    System.Globalization.DateTimeStyles.RoundtripKind, out DateTime parsed)
                    ? parsed
                    : DateTime.UtcNow;
            }
        }

        private static Dictionary<string, object> CaptureProcessWindow(System.Diagnostics.Process process,
            string screenshotPath, int windowWaitMs)
        {
#if UNITY_EDITOR_WIN
            IntPtr hwnd = IntPtr.Zero;
            DateTime deadline = DateTime.UtcNow.AddMilliseconds(Math.Max(0, windowWaitMs));
            while (DateTime.UtcNow <= deadline)
            {
                if (process.HasExited)
                    break;

                process.Refresh();
                hwnd = process.MainWindowHandle;
                if (hwnd != IntPtr.Zero)
                    break;

                System.Threading.Thread.Sleep(100);
            }

            if (hwnd == IntPtr.Zero)
                return new Dictionary<string, object> { { "success", false }, { "error", "Process main window was not found." } };

            return CaptureWindow(hwnd, screenshotPath);
#else
            return new Dictionary<string, object> { { "success", false }, { "error", "External process window capture is Windows-only." } };
#endif
        }

#if UNITY_EDITOR_WIN
        private static Dictionary<string, object> CaptureWindow(IntPtr hwnd, string path)
        {
            if (!GetWindowRect(hwnd, out RECT rect))
                return new Dictionary<string, object> { { "success", false }, { "error", "GetWindowRect failed." } };

            int width = rect.right - rect.left;
            int height = rect.bottom - rect.top;
            if (width <= 0 || height <= 0)
                return new Dictionary<string, object> { { "success", false }, { "error", $"Invalid window size {width}x{height}." } };

            IntPtr screen = IntPtr.Zero;
            IntPtr mem = IntPtr.Zero;
            IntPtr bitmap = IntPtr.Zero;
            IntPtr old = IntPtr.Zero;
            try
            {
                screen = GetDC(IntPtr.Zero);
                mem = CreateCompatibleDC(screen);
                bitmap = CreateCompatibleBitmap(screen, width, height);
                old = SelectObject(mem, bitmap);
                if (!PrintWindow(hwnd, mem, PW_RENDERFULLCONTENT))
                    return new Dictionary<string, object> { { "success", false }, { "error", "PrintWindow failed." } };

                SelectObject(mem, old);
                old = IntPtr.Zero;

                var bmi = new BITMAPINFOHEADER
                {
                    biSize = (uint)Marshal.SizeOf<BITMAPINFOHEADER>(),
                    biWidth = width,
                    biHeight = height,
                    biPlanes = 1,
                    biBitCount = 32,
                    biCompression = 0,
                };
                byte[] buffer = new byte[width * height * 4];
                int scan = GetDIBits(screen, bitmap, 0, (uint)height, buffer, ref bmi, DIB_RGB_COLORS);
                if (scan == 0)
                    return new Dictionary<string, object> { { "success", false }, { "error", "GetDIBits failed." } };

                byte[] png = EncodeBgraBottomUp(buffer, width, height);
                string directory = Path.GetDirectoryName(path);
                if (string.IsNullOrEmpty(directory) == false)
                    Directory.CreateDirectory(directory);
                File.WriteAllBytes(path, png);

                return new Dictionary<string, object>
                {
                    { "success", true },
                    { "path", path },
                    { "width", width },
                    { "height", height },
                    { "sizeBytes", png.Length },
                };
            }
            finally
            {
                if (old != IntPtr.Zero && mem != IntPtr.Zero) SelectObject(mem, old);
                if (bitmap != IntPtr.Zero) DeleteObject(bitmap);
                if (mem != IntPtr.Zero) DeleteDC(mem);
                if (screen != IntPtr.Zero) ReleaseDC(IntPtr.Zero, screen);
            }
        }

        private static byte[] EncodeBgraBottomUp(byte[] bgra, int width, int height)
        {
            var colors = new Color32[width * height];
            for (int i = 0; i < colors.Length; i++)
            {
                int offset = i * 4;
                colors[i] = new Color32(bgra[offset + 2], bgra[offset + 1], bgra[offset], 255);
            }

            Texture2D texture = null;
            try
            {
                texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
                texture.SetPixels32(colors);
                texture.Apply(false);
                return texture.EncodeToPNG();
            }
            finally
            {
                if (texture != null)
                    UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool PrintWindow(IntPtr hWnd, IntPtr hdcBlt, uint nFlags);
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr GetDC(IntPtr hWnd);
        [DllImport("user32.dll")]
        private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);
        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern IntPtr CreateCompatibleDC(IntPtr hdc);
        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int width, int height);
        [DllImport("gdi32.dll")]
        private static extern IntPtr SelectObject(IntPtr hdc, IntPtr handle);
        [DllImport("gdi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DeleteObject(IntPtr handle);
        [DllImport("gdi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DeleteDC(IntPtr hdc);
        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern int GetDIBits(IntPtr hdc, IntPtr bitmap, uint start, uint lines,
            byte[] bits, ref BITMAPINFOHEADER bmi, uint usage);

        private const uint PW_RENDERFULLCONTENT = 0x00000002;
        private const uint DIB_RGB_COLORS = 0;

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int left;
            public int top;
            public int right;
            public int bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BITMAPINFOHEADER
        {
            public uint biSize;
            public int biWidth;
            public int biHeight;
            public ushort biPlanes;
            public ushort biBitCount;
            public uint biCompression;
            public uint biSizeImage;
            public int biXPPM;
            public int biYPPM;
            public uint biClrUsed;
            public uint biClrImportant;
        }
#endif
    }
}
