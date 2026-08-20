using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Profiling;

namespace VMUnityAutomation.Editor
{
    /// <summary>
    /// Advanced memory profiling commands.
    /// Provides per-asset-type memory breakdowns, top memory consumers,
    /// and optional Memory Profiler package integration (com.unity.memoryprofiler).
    /// Works without the package using built-in Unity Profiler APIs;
    /// enhanced features available when the package is installed.
    /// </summary>
    public static class VmAutomationMemoryProfilerCommands
    {
        internal const string JobType = "memory-snapshot";
        private static bool _packageChecked;
        private static bool _packageInstalled;

        private sealed class MemorySnapshotJob
        {
            public readonly object CallbackGate = new object();
            public string JobId;
            public string OwnerAgentId;
            public string ApiType;
            public string CaptureFlags;
            public string TempPath;
            public string SnapshotPath;
            public string Status;
            public string Error;
            public string StartedUtc;
            public string CompletedUtc;
            public double StartedAt;
            public bool TimedOut;
            public bool CallbackReceived;
            public bool CallbackSuccess;
            public string CallbackPath;
            public bool CancelRequested;
        }

        private static MemorySnapshotJob _snapshotJob;

        // ─── Package Detection ───

        /// <summary>
        /// Check if com.unity.memoryprofiler package is installed by reading manifest.json.
        /// </summary>
        public static bool IsMemoryProfilerPackageInstalled()
        {
            if (_packageChecked) return _packageInstalled;
            _packageChecked = true;

            try
            {
                string manifestPath = Path.Combine(Application.dataPath, "..", "Packages", "manifest.json");
                if (File.Exists(manifestPath))
                {
                    string content = File.ReadAllText(manifestPath);
                    _packageInstalled = content.Contains("\"com.unity.memoryprofiler\"");
                }
            }
            catch { }

            return _packageInstalled;
        }

        /// <summary>
        /// Check package status and available features.
        /// </summary>
        public static object GetStatus(Dictionary<string, object> args)
        {
            bool hasPkg = IsMemoryProfilerPackageInstalled();

            long totalAllocated = Profiler.GetTotalAllocatedMemoryLong();
            long totalReserved = Profiler.GetTotalReservedMemoryLong();
            long gfxDriver = Profiler.GetAllocatedMemoryForGraphicsDriver();

            return new Dictionary<string, object>
            {
                { "memoryProfilerPackageInstalled", hasPkg },
                { "availableCommands", new string[] {
                    "profiler/memory-status",
                    "profiler/memory-breakdown",
                    "profiler/memory-top-assets",
                    hasPkg ? "profiler/memory-snapshot" : null,
                    hasPkg ? "profiler/memory-snapshot-status" : null
                }.Where(s => s != null).ToArray() },
                { "quickSummary", new Dictionary<string, object>
                    {
                        { "totalAllocatedMB", Math.Round(totalAllocated / (1024.0 * 1024.0), 2) },
                        { "totalReservedMB", Math.Round(totalReserved / (1024.0 * 1024.0), 2) },
                        { "gfxDriverMB", Math.Round(gfxDriver / (1024.0 * 1024.0), 2) },
                    }
                },
            };
        }

        // ─── Memory Breakdown by Asset Type ───

        /// <summary>
        /// Get memory breakdown organized by asset type (textures, meshes, audio, etc.).
        /// Uses built-in Profiler.GetRuntimeMemorySizeLong for per-object sizing.
        /// </summary>
        public static object GetMemoryBreakdown(Dictionary<string, object> args)
        {
            bool includeDetails = args.ContainsKey("includeDetails") && GetBool(args, "includeDetails", false);
            int maxPerCategory = args.ContainsKey("maxPerCategory")
                ? Convert.ToInt32(args["maxPerCategory"]) : 5;

            var categories = new Dictionary<string, object>();
            long grandTotal = 0;

            // Textures (Texture2D + RenderTexture)
            var texResult = ProfileAssetType<Texture2D>("Textures", includeDetails, maxPerCategory,
                t => $"{t.width}x{t.height} {t.format}");
            categories["textures"] = texResult;
            grandTotal += (long)((Dictionary<string, object>)texResult)["totalBytes"];

            var rtResult = ProfileAssetType<RenderTexture>("RenderTextures", includeDetails, maxPerCategory,
                rt => $"{rt.width}x{rt.height} {rt.format} depth={rt.depth}");
            categories["renderTextures"] = rtResult;
            grandTotal += (long)((Dictionary<string, object>)rtResult)["totalBytes"];

            // Meshes
            var meshResult = ProfileAssetType<Mesh>("Meshes", includeDetails, maxPerCategory,
                m => $"{m.vertexCount} verts, {m.triangles.Length / 3} tris");
            categories["meshes"] = meshResult;
            grandTotal += (long)((Dictionary<string, object>)meshResult)["totalBytes"];

            // Materials
            var matResult = ProfileAssetType<Material>("Materials", includeDetails, maxPerCategory,
                m => m.shader != null ? m.shader.name : "no shader");
            categories["materials"] = matResult;
            grandTotal += (long)((Dictionary<string, object>)matResult)["totalBytes"];

            // Shaders
            var shaderResult = ProfileAssetType<Shader>("Shaders", includeDetails, maxPerCategory, null);
            categories["shaders"] = shaderResult;
            grandTotal += (long)((Dictionary<string, object>)shaderResult)["totalBytes"];

            // Audio Clips
            var audioResult = ProfileAssetType<AudioClip>("AudioClips", includeDetails, maxPerCategory,
                a => $"{a.length:F1}s {a.frequency}Hz {a.channels}ch");
            categories["audioClips"] = audioResult;
            grandTotal += (long)((Dictionary<string, object>)audioResult)["totalBytes"];

            // Animation Clips
            var animResult = ProfileAssetType<AnimationClip>("AnimationClips", includeDetails, maxPerCategory,
                a => $"{a.length:F1}s {(a.isLooping ? "loop" : "once")}");
            categories["animationClips"] = animResult;
            grandTotal += (long)((Dictionary<string, object>)animResult)["totalBytes"];

            // Fonts
            var fontResult = ProfileAssetType<Font>("Fonts", includeDetails, maxPerCategory, null);
            categories["fonts"] = fontResult;
            grandTotal += (long)((Dictionary<string, object>)fontResult)["totalBytes"];

            // Scriptable Objects
            var soResult = ProfileAssetType<ScriptableObject>("ScriptableObjects", includeDetails, maxPerCategory,
                so => so.GetType().Name);
            categories["scriptableObjects"] = soResult;
            grandTotal += (long)((Dictionary<string, object>)soResult)["totalBytes"];

            // System summary
            long totalAllocated = Profiler.GetTotalAllocatedMemoryLong();
            long gfxDriver = Profiler.GetAllocatedMemoryForGraphicsDriver();
            long monoUsed = Profiler.GetMonoUsedSizeLong();

            return new Dictionary<string, object>
            {
                { "categories", categories },
                { "scannedAssetTotalMB", Math.Round(grandTotal / (1024.0 * 1024.0), 2) },
                { "scannedAssetTotalBytes", grandTotal },
                { "systemMemory", new Dictionary<string, object>
                    {
                        { "totalAllocatedMB", Math.Round(totalAllocated / (1024.0 * 1024.0), 2) },
                        { "gfxDriverMB", Math.Round(gfxDriver / (1024.0 * 1024.0), 2) },
                        { "monoUsedMB", Math.Round(monoUsed / (1024.0 * 1024.0), 2) },
                    }
                },
                { "memoryProfilerPackageInstalled", IsMemoryProfilerPackageInstalled() },
            };
        }

        private static object ProfileAssetType<T>(string categoryName, bool includeDetails, int maxPerCategory,
            Func<T, string> detailFunc) where T : UnityEngine.Object
        {
            var objects = Resources.FindObjectsOfTypeAll<T>();
            long totalBytes = 0;
            var items = new List<AssetMemInfo>();

            foreach (var obj in objects)
            {
                long size = Profiler.GetRuntimeMemorySizeLong(obj);
                totalBytes += size;

                if (includeDetails)
                {
                    items.Add(new AssetMemInfo
                    {
                        name = obj.name,
                        sizeBytes = size,
                        detail = detailFunc != null ? detailFunc(obj) : null,
                        assetPath = AssetDatabase.GetAssetPath(obj),
                    });
                }
            }

            var result = new Dictionary<string, object>
            {
                { "count", objects.Length },
                { "totalMB", Math.Round(totalBytes / (1024.0 * 1024.0), 2) },
                { "totalBytes", totalBytes },
            };

            if (includeDetails && items.Count > 0)
            {
                items.Sort((a, b) => b.sizeBytes.CompareTo(a.sizeBytes));
                var topItems = items.Take(maxPerCategory).Select(item =>
                {
                    var d = new Dictionary<string, object>
                    {
                        { "name", string.IsNullOrEmpty(item.name) ? "(unnamed)" : item.name },
                        { "sizeMB", Math.Round(item.sizeBytes / (1024.0 * 1024.0), 3) },
                        { "sizeBytes", item.sizeBytes },
                    };
                    if (!string.IsNullOrEmpty(item.detail)) d["detail"] = item.detail;
                    if (!string.IsNullOrEmpty(item.assetPath)) d["assetPath"] = item.assetPath;
                    return d;
                }).ToArray();

                result["topAssets"] = topItems;
            }

            return result;
        }

        private struct AssetMemInfo
        {
            public string name;
            public long sizeBytes;
            public string detail;
            public string assetPath;
        }

        // ─── Top Memory Consumers ───

        /// <summary>
        /// Get the top N memory-consuming assets across all types.
        /// </summary>
        public static object GetTopMemoryConsumers(Dictionary<string, object> args)
        {
            int count = args.ContainsKey("count") ? Convert.ToInt32(args["count"]) : 20;
            string filterType = args.ContainsKey("type") ? args["type"].ToString().ToLower() : "";

            var allAssets = new List<Dictionary<string, object>>();

            // Scan all relevant types
            if (filterType == "" || filterType == "texture")
                ScanType<Texture2D>(allAssets, "Texture2D");
            if (filterType == "" || filterType == "rendertexture")
                ScanType<RenderTexture>(allAssets, "RenderTexture");
            if (filterType == "" || filterType == "mesh")
                ScanType<Mesh>(allAssets, "Mesh");
            if (filterType == "" || filterType == "audioclip" || filterType == "audio")
                ScanType<AudioClip>(allAssets, "AudioClip");
            if (filterType == "" || filterType == "material")
                ScanType<Material>(allAssets, "Material");
            if (filterType == "" || filterType == "shader")
                ScanType<Shader>(allAssets, "Shader");
            if (filterType == "" || filterType == "animationclip" || filterType == "animation")
                ScanType<AnimationClip>(allAssets, "AnimationClip");
            if (filterType == "" || filterType == "font")
                ScanType<Font>(allAssets, "Font");

            // Sort by size descending
            allAssets.Sort((a, b) => ((long)b["sizeBytes"]).CompareTo((long)a["sizeBytes"]));

            var topAssets = allAssets.Take(count).ToArray();

            long grandTotal = allAssets.Sum(a => (long)a["sizeBytes"]);

            return new Dictionary<string, object>
            {
                { "totalScannedAssets", allAssets.Count },
                { "totalScannedMB", Math.Round(grandTotal / (1024.0 * 1024.0), 2) },
                { "returnedCount", topAssets.Length },
                { "filterType", string.IsNullOrEmpty(filterType) ? "all" : filterType },
                { "assets", topAssets },
            };
        }

        private static void ScanType<T>(List<Dictionary<string, object>> output, string typeName) where T : UnityEngine.Object
        {
            var objects = Resources.FindObjectsOfTypeAll<T>();
            foreach (var obj in objects)
            {
                long size = Profiler.GetRuntimeMemorySizeLong(obj);
                if (size <= 0) continue;

                string path = AssetDatabase.GetAssetPath(obj);

                output.Add(new Dictionary<string, object>
                {
                    { "name", string.IsNullOrEmpty(obj.name) ? "(unnamed)" : obj.name },
                    { "type", typeName },
                    { "sizeMB", Math.Round(size / (1024.0 * 1024.0), 3) },
                    { "sizeBytes", size },
                    { "assetPath", path ?? "" },
                });
            }
        }

        // ─── Memory Snapshot (requires com.unity.memoryprofiler) ───

        /// <summary>
        /// Take a memory snapshot using the Memory Profiler package API.
        /// Returns error if the package is not installed.
        /// Uses reflection to avoid compile-time dependency.
        /// </summary>
        public static void TakeMemorySnapshot(Dictionary<string, object> args, Action<object> resolve)
        {
            if (!IsMemoryProfilerPackageInstalled())
            {
                resolve(VmAutomationResponse.Error(
                    "com.unity.memoryprofiler package is not installed. Install it via Package Manager to use memory snapshots.",
                    "memory_profiler_package_missing", false, new Dictionary<string, object>
                    {
                        { "alternatives", new string[] {
                        "profiler/memory-breakdown - Get per-asset-type memory breakdown (built-in, always available)",
                        "profiler/memory-top-assets - Get top N memory consumers (built-in, always available)",
                        "profiler/memory - Get basic memory stats (built-in, always available)",
                        } },
                    }));
                return;
            }

            try
            {
                RefreshSnapshotJobFromFiles(_snapshotJob);
                if (_snapshotJob != null && _snapshotJob.Status == "Capturing")
                {
                    resolve(VmAutomationResponse.Error(
                        "A memory snapshot capture is already in progress. Poll profiler/memory-snapshot-status.",
                        "memory_snapshot_in_progress", true, SnapshotJobData(_snapshotJob)));
                    return;
                }

                // Unity 2022.2+ exposes the public API in UnityEngine.CoreModule. Keep the
                // deprecated Experimental namespaces only as fallbacks for older Editors.
                var memProfilerType = ResolveMemoryProfilerType();

                if (memProfilerType == null)
                {
                    resolve(VmAutomationResponse.Error(
                        "MemoryProfiler experimental API not found in this Unity version.",
                        "memory_profiler_api_missing"));
                    return;
                }

                string snapshotDir = args != null && args.TryGetValue("path", out object pathValue) &&
                                     pathValue != null && !string.IsNullOrWhiteSpace(pathValue.ToString())
                    ? pathValue.ToString()
                    : Path.Combine(Application.temporaryCachePath, "MemorySnapshots");
                snapshotDir = Path.GetFullPath(snapshotDir);

                if (!Directory.Exists(snapshotDir))
                    Directory.CreateDirectory(snapshotDir);

                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
                string snapshotStem = $"snapshot_{timestamp}_{Guid.NewGuid():N}".Substring(0, 39);
                string tempPath = Path.Combine(snapshotDir, snapshotStem + ".tmpsnap");
                string snapshotPath = Path.Combine(snapshotDir, snapshotStem + ".snap");

                var takeSnapshot = memProfilerType.GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .Where(method => method.Name == "TakeSnapshot")
                    .Select(method => new { Method = method, Parameters = method.GetParameters() })
                    .Where(candidate => candidate.Parameters.Length >= 2 &&
                                        candidate.Parameters.Length <= 3 &&
                                        candidate.Parameters[0].ParameterType == typeof(string) &&
                                        IsSnapshotCompletionDelegate(candidate.Parameters[1].ParameterType))
                    .OrderByDescending(candidate => candidate.Parameters.Length == 3 &&
                                                    candidate.Parameters[2].ParameterType.IsEnum)
                    .ThenBy(candidate => candidate.Parameters.Length)
                    .FirstOrDefault();

                if (takeSnapshot == null)
                {
                    resolve(VmAutomationResponse.Error(
                        "A supported TakeSnapshot overload was not found on MemoryProfiler.",
                        "memory_snapshot_api_mismatch"));
                    return;
                }

                int timeoutMs = Math.Max(1000, GetInt(args, "timeoutMs", 120000));
                double startedAt = EditorApplication.timeSinceStartup;
                bool routeResolved = false;

                var job = new MemorySnapshotJob
                {
                    JobId = Guid.NewGuid().ToString("N"),
                    OwnerAgentId = args != null && args.TryGetValue("_agentId", out object agentValue)
                        ? agentValue?.ToString()
                        : "anonymous",
                    ApiType = memProfilerType.FullName,
                    TempPath = tempPath,
                    SnapshotPath = snapshotPath,
                    Status = "Capturing",
                    StartedUtc = DateTime.UtcNow.ToString("O"),
                    StartedAt = startedAt,
                };
                _snapshotJob = job;
                RecordSnapshotJob(job);

                void ResolveOnce(object result)
                {
                    if (routeResolved)
                        return;
                    routeResolved = true;
                    EditorApplication.update -= CheckProgress;
                    resolve(result);
                }

                void CheckProgress()
                {
                    RefreshSnapshotJobFromFiles(job);
                    RecordSnapshotJob(job);
                    if (job.Status != "Capturing")
                    {
                        var terminal = SnapshotJobData(job);
                        terminal["success"] = job.Status == "Completed";
                        terminal["completed"] = true;
                        terminal["canceled"] = job.Status == "Canceled";
                        if (job.Status == "Failed" || job.Status == "Canceled")
                            terminal["error"] = job.Error;
                        ResolveOnce(terminal);
                        return;
                    }

                    if ((EditorApplication.timeSinceStartup - startedAt) * 1000d < timeoutMs)
                        return;

                    job.TimedOut = true;
                    RefreshSnapshotJobFromFiles(job);
                    if (job.Status == "Completed")
                    {
                        var completed = SnapshotJobData(job);
                        completed["success"] = true;
                        completed["completed"] = true;
                        ResolveOnce(completed);
                        return;
                    }

                    var pending = SnapshotJobData(job);
                    pending["success"] = true;
                    pending["completed"] = false;
                    pending["message"] = $"Memory snapshot is still capturing after {timeoutMs} ms. Poll profiler/memory-snapshot-status.";
                    ResolveOnce(pending);
                }

                Action<string, bool> callback = (completedPath, success) =>
                {
                    // The native callback is not guaranteed to be safe for registering a new
                    // Editor callback. Store only atomic state here; the pre-registered update
                    // handler or the status route performs file finalization on the main thread.
                    lock (job.CallbackGate)
                    {
                        job.CallbackPath = completedPath;
                        job.CallbackSuccess = success;
                        job.CallbackReceived = true;
                    }
                };

                Delegate callbackDelegate = callback;
                Type callbackType = takeSnapshot.Parameters[1].ParameterType;
                if (!callbackType.IsInstanceOfType(callback))
                    callbackDelegate = Delegate.CreateDelegate(callbackType, callback.Target, callback.Method);

                var invokeArgs = new List<object> { tempPath, callbackDelegate };
                if (takeSnapshot.Parameters.Length == 3)
                {
                    Type flagsType = takeSnapshot.Parameters[2].ParameterType;
                    object flags = BuildDefaultCaptureFlags(flagsType, out string captureFlags);
                    job.CaptureFlags = captureFlags;
                    invokeArgs.Add(flags);
                }

                takeSnapshot.Method.Invoke(null, invokeArgs.ToArray());
                if (!routeResolved)
                {
                    EditorApplication.update += CheckProgress;
                    CheckProgress();
                }
            }
            catch (Exception ex)
            {
                resolve(VmAutomationResponse.Error(
                    "Failed to start memory snapshot: " + ex.GetBaseException().Message,
                    "memory_snapshot_start_failed"));
            }
        }

        /// <summary>
        /// Poll the current or requested memory snapshot after the initiating route times out.
        /// </summary>
        public static object GetMemorySnapshotStatus(Dictionary<string, object> args)
        {
            string jobId = args != null && args.TryGetValue("jobId", out object value) && value != null
                ? value.ToString()
                : "";

            if (_snapshotJob == null || (!string.IsNullOrEmpty(jobId) && _snapshotJob.JobId != jobId))
            {
                return VmAutomationResponse.Error(
                    string.IsNullOrEmpty(jobId)
                        ? "No memory snapshot job is available in this Editor session."
                        : $"Memory snapshot job '{jobId}' was not found in this Editor session.",
                    "memory_snapshot_job_not_found");
            }
            if (!VmAutomationJobHistory.CanAccess(JobType, _snapshotJob.JobId,
                    _snapshotJob.OwnerAgentId ?? "anonymous", args))
            {
                return VmAutomationResponse.Error(
                    "Memory snapshot job belongs to another agent and the jobAccessToken was not supplied.",
                    "job_owner_mismatch");
            }

            RefreshSnapshotJobFromFiles(_snapshotJob);
            var result = SnapshotJobData(_snapshotJob);
            result["success"] = true;
            result["completed"] = _snapshotJob.Status == "Completed" ||
                                  _snapshotJob.Status == "Canceled";
            RecordSnapshotJob(_snapshotJob);
            return result;
        }

        internal static object CancelMemorySnapshot(Dictionary<string, object> args)
        {
            string jobId = args != null && args.TryGetValue("jobId", out object jobValue)
                ? jobValue?.ToString()
                : "";
            if (_snapshotJob == null || (!string.IsNullOrEmpty(jobId) && _snapshotJob.JobId != jobId))
                return VmAutomationResponse.Error($"Memory snapshot job '{jobId}' was not found.",
                    "job_not_found");

            if (!VmAutomationJobHistory.CanAccess(JobType, _snapshotJob.JobId,
                    _snapshotJob.OwnerAgentId ?? "anonymous", args))
                return VmAutomationResponse.Error(
                    "Memory snapshot job belongs to another agent and the jobAccessToken was not supplied.",
                    "job_owner_mismatch");
            if (_snapshotJob.Status != "Capturing")
                return VmAutomationResponse.Error("Memory snapshot job is already terminal.",
                    "job_already_terminal", false, SnapshotJobData(_snapshotJob));

            _snapshotJob.CancelRequested = true;
            RecordSnapshotJob(_snapshotJob);
            return new Dictionary<string, object>
            {
                { "success", true },
                { "jobId", _snapshotJob.JobId },
                { "jobType", JobType },
                { "status", "canceling" },
                { "cancelRequested", true },
                { "cancelMode", "cooperative" },
                { "captureMayStillComplete", true },
                { "message", "Unity does not expose a portable snapshot abort API. The completed capture will be discarded." },
            };
        }

        private static Type ResolveMemoryProfilerType()
        {
            return Type.GetType("Unity.Profiling.Memory.MemoryProfiler, UnityEngine.CoreModule")
                ?? Type.GetType("Unity.Profiling.Memory.MemoryProfiler, UnityEngine")
                ?? Type.GetType(
                    "UnityEngine.Profiling.Memory.Experimental.MemoryProfiler, UnityEngine.CoreModule")
                ?? Type.GetType(
                    "UnityEngine.Profiling.Memory.Experimental.MemoryProfiler, UnityEngine")
                ?? Type.GetType(
                    "UnityEditor.Profiling.Memory.Experimental.MemoryProfiler, UnityEditor.CoreModule")
                ?? Type.GetType(
                    "UnityEditor.Profiling.Memory.Experimental.MemoryProfiler, UnityEditor");
        }

        private static bool IsSnapshotCompletionDelegate(Type delegateType)
        {
            if (!typeof(Delegate).IsAssignableFrom(delegateType))
                return false;
            MethodInfo invoke = delegateType.GetMethod("Invoke");
            ParameterInfo[] parameters = invoke?.GetParameters();
            return invoke != null && invoke.ReturnType == typeof(void) && parameters != null &&
                   parameters.Length == 2 && parameters[0].ParameterType == typeof(string) &&
                   parameters[1].ParameterType == typeof(bool);
        }

        private static object BuildDefaultCaptureFlags(Type flagsType, out string captureFlags)
        {
            if (!flagsType.IsEnum)
            {
                captureFlags = "default";
                return Activator.CreateInstance(flagsType);
            }

            string[] desiredFlags = { "ManagedObjects", "NativeObjects", "NativeAllocations" };
            ulong combined = 0;
            var enabled = new List<string>();
            foreach (string flagName in desiredFlags)
            {
                if (!Enum.GetNames(flagsType).Contains(flagName))
                    continue;
                object parsed = Enum.Parse(flagsType, flagName);
                combined |= Convert.ToUInt64(parsed);
                enabled.Add(flagName);
            }

            captureFlags = string.Join(",", enabled);
            return Enum.ToObject(flagsType, combined);
        }

        private static string FinalizeSnapshotFile(MemorySnapshotJob job, string completedPath)
        {
            string sourcePath = !string.IsNullOrWhiteSpace(completedPath) && File.Exists(completedPath)
                ? completedPath
                : File.Exists(job.TempPath) ? job.TempPath : null;

            if (sourcePath == null)
                return File.Exists(job.SnapshotPath) ? job.SnapshotPath : null;

            if (string.Equals(Path.GetFullPath(sourcePath), Path.GetFullPath(job.SnapshotPath),
                    StringComparison.OrdinalIgnoreCase))
                return job.SnapshotPath;

            if (File.Exists(job.SnapshotPath))
                File.Delete(job.SnapshotPath);
            File.Move(sourcePath, job.SnapshotPath);
            return job.SnapshotPath;
        }

        private static void RefreshSnapshotJobFromFiles(MemorySnapshotJob job)
        {
            if (job == null || job.Status != "Capturing")
                return;

            bool callbackReceived;
            bool callbackSuccess;
            string callbackPath;
            lock (job.CallbackGate)
            {
                callbackReceived = job.CallbackReceived;
                callbackSuccess = job.CallbackSuccess;
                callbackPath = job.CallbackPath;
            }

            if (callbackReceived)
            {
                if (job.CancelRequested)
                {
                    CompleteCanceledSnapshot(job, callbackPath);
                    return;
                }
                if (!callbackSuccess)
                {
                    job.Status = "Failed";
                    job.Error = "Memory Profiler reported that snapshot capture failed.";
                    job.CompletedUtc = DateTime.UtcNow.ToString("O");
                    return;
                }

                try
                {
                    string callbackFinalPath = FinalizeSnapshotFile(job, callbackPath);
                    if (!string.IsNullOrEmpty(callbackFinalPath))
                    {
                        job.SnapshotPath = callbackFinalPath;
                        job.Status = "Completed";
                        job.CompletedUtc = DateTime.UtcNow.ToString("O");
                        return;
                    }
                }
                catch (IOException)
                {
                    // The callback can race the final writer close. Retry on the next update.
                }
                catch (UnauthorizedAccessException)
                {
                    // Treat a transient sharing violation like a still-active writer.
                }
            }

            if (File.Exists(job.SnapshotPath))
            {
                if (job.CancelRequested)
                {
                    CompleteCanceledSnapshot(job, job.SnapshotPath);
                    return;
                }
                job.Status = "Completed";
                job.CompletedUtc = DateTime.UtcNow.ToString("O");
                return;
            }

            // Some Unity versions finish and close a valid tmpsnap without invoking the
            // completion delegate. Only recover after the route timeout, once the file has
            // been stable and both snapshot format signatures are present.
            if (!job.TimedOut || !File.Exists(job.TempPath))
                return;

            var tempInfo = new FileInfo(job.TempPath);
            if ((DateTime.UtcNow - tempInfo.LastWriteTimeUtc).TotalSeconds < 2d ||
                !HasCompleteSnapshotSignatures(job.TempPath))
                return;

            try
            {
                string recoveredPath = FinalizeSnapshotFile(job, job.TempPath);
                if (!string.IsNullOrEmpty(recoveredPath))
                {
                    if (job.CancelRequested)
                    {
                        CompleteCanceledSnapshot(job, recoveredPath);
                        return;
                    }
                    job.SnapshotPath = recoveredPath;
                    job.Status = "Completed";
                    job.CompletedUtc = DateTime.UtcNow.ToString("O");
                }
            }
            catch (IOException)
            {
                // The native writer may still own the file between validation and rename.
                // Leave the job capturing so the next status poll can retry safely.
            }
        }

        private static bool HasCompleteSnapshotSignatures(string path)
        {
            const uint headerSignature = 0xAEABCDCDu;
            const uint footerSignature = 0xABCDCDAEu;
            try
            {
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    if (stream.Length < sizeof(uint) * 2)
                        return false;
                    var bytes = new byte[sizeof(uint)];
                    if (stream.Read(bytes, 0, bytes.Length) != bytes.Length ||
                        BitConverter.ToUInt32(bytes, 0) != headerSignature)
                        return false;
                    stream.Seek(-sizeof(uint), SeekOrigin.End);
                    if (stream.Read(bytes, 0, bytes.Length) != bytes.Length)
                        return false;
                    return BitConverter.ToUInt32(bytes, 0) == footerSignature;
                }
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
        }

        private static Dictionary<string, object> SnapshotJobData(MemorySnapshotJob job)
        {
            var snapshotFile = new FileInfo(job.SnapshotPath);
            var tempFile = new FileInfo(job.TempPath);
            var response = new Dictionary<string, object>
            {
                { "jobId", job.JobId },
                { "jobType", JobType },
                { "status", job.Status },
                { "apiType", job.ApiType ?? "" },
                { "captureFlags", job.CaptureFlags ?? "" },
                { "snapshotPath", job.SnapshotPath },
                { "tempPath", job.TempPath },
                { "fileExists", snapshotFile.Exists },
                { "fileSizeBytes", snapshotFile.Exists ? snapshotFile.Length : 0L },
                { "tempFileExists", tempFile.Exists },
                { "tempFileSizeBytes", tempFile.Exists ? tempFile.Length : 0L },
                { "startedUtc", job.StartedUtc },
                { "completedUtc", job.CompletedUtc ?? "" },
                { "timedOut", job.TimedOut },
                { "cancelRequested", job.CancelRequested },
                { "captureMayStillComplete", job.Status == "Capturing" },
                { "elapsedMs", Math.Round((EditorApplication.timeSinceStartup - job.StartedAt) * 1000d, 1) },
                { "error", job.Error ?? "" },
            };
            VmAutomationJobHistory.PublishAccessToken(response, JobType, job.JobId,
                job.OwnerAgentId);
            return response;
        }

        private static void CompleteCanceledSnapshot(MemorySnapshotJob job, string callbackPath)
        {
            foreach (string path in new[] { callbackPath, job.TempPath, job.SnapshotPath }
                         .Where(path => !string.IsNullOrEmpty(path))
                         .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                try
                {
                    if (File.Exists(path))
                        File.Delete(path);
                }
                catch (IOException)
                {
                    // The native writer can still own the file. A later status poll retries.
                    return;
                }
                catch (UnauthorizedAccessException)
                {
                    return;
                }
            }

            job.Status = "Canceled";
            job.Error = "Canceled by request; completed snapshot output was discarded.";
            job.CompletedUtc = DateTime.UtcNow.ToString("O");
            RecordSnapshotJob(job);
        }

        private static void RecordSnapshotJob(MemorySnapshotJob job)
        {
            if (job == null)
                return;
            string status = job.CancelRequested && job.Status == "Capturing"
                ? "canceling"
                : job.Status.ToLowerInvariant();
            VmAutomationJobHistory.Record(JobType, job.JobId, job.OwnerAgentId, status,
                SnapshotJobData(job));
        }

        // ─── Helpers ───

        private static bool GetBool(Dictionary<string, object> args, string key, bool defaultValue)
        {
            if (!args.ContainsKey(key)) return defaultValue;
            var val = args[key];
            if (val is bool b) return b;
            if (val is string s) return s.ToLowerInvariant() == "true";
            return defaultValue;
        }

        private static int GetInt(Dictionary<string, object> args, string key, int defaultValue)
        {
            if (args == null || !args.TryGetValue(key, out object value) || value == null)
                return defaultValue;
            return int.TryParse(value.ToString(), out int parsed) ? parsed : defaultValue;
        }
    }
}
