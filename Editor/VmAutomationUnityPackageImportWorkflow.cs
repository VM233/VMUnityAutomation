using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace VMUnityAutomation.Editor
{
    [InitializeOnLoad]
    public static class VmAutomationUnityPackageImportWorkflow
    {
        private const string JobType = "unitypackage-import";
        private static readonly TimeSpan ImportTimeout = TimeSpan.FromMinutes(10);
        private static UnityPackageImportJob _job;
        private static bool _callbacksRegistered;
        private static bool _updateRegistered;

        static VmAutomationUnityPackageImportWorkflow()
        {
            _job = LoadJob();
            if (_job == null || _job.IsTerminal)
                return;

            if (_job.Status == "running")
            {
                _job.Status = "waiting-for-editor";
                _job.RecoveredAfterReload = true;
                TouchAndSave();
            }

            EnsureCallbacksRegistered();
            EnsureUpdateRegistered();
        }

        public static object Start(Dictionary<string, object> args)
        {
            string requestedPath = GetString(args, "packagePath");
            if (string.IsNullOrWhiteSpace(requestedPath))
                return BuildFailure("package_path_required", "packagePath is required", "", "");

            string fullPackagePath;
            try
            {
                fullPackagePath = NormalizePackagePath(requestedPath);
            }
            catch (Exception exception)
            {
                return BuildFailure("invalid_package_path", exception.Message, requestedPath, "");
            }

            string packageName = Path.GetFileNameWithoutExtension(fullPackagePath);
            if (!string.Equals(Path.GetExtension(fullPackagePath), ".unitypackage",
                    StringComparison.OrdinalIgnoreCase))
            {
                return BuildFailure("invalid_package_extension",
                    "packagePath must point to a .unitypackage file", fullPackagePath, packageName);
            }
            if (!File.Exists(fullPackagePath))
            {
                return BuildFailure("package_not_found",
                    $"Unity package not found at '{fullPackagePath}'", fullPackagePath, packageName);
            }

            string owner = GetOwner(args);
            if (_job != null && !_job.IsTerminal)
            {
                if (string.Equals(_job.OwnerAgentId, owner, StringComparison.Ordinal) &&
                    string.Equals(_job.PackagePath, fullPackagePath, StringComparison.OrdinalIgnoreCase))
                {
                    var reused = BuildResponse(_job);
                    reused["reused"] = true;
                    return reused;
                }

                return BuildFailure("import_already_running",
                    $"Unity package import job '{_job.JobId}' is already running.",
                    fullPackagePath, packageName);
            }

            _job = new UnityPackageImportJob
            {
                JobId = Guid.NewGuid().ToString("N").Substring(0, 12),
                Status = "queued",
                OwnerAgentId = owner,
                PackagePath = fullPackagePath,
                PackageName = packageName,
                PathsBefore = AssetDatabase.GetAllAssetPaths().ToList(),
                NewAssetPaths = new List<string>(),
                StartedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
            SaveJob();
            EnsureCallbacksRegistered();
            EnsureUpdateRegistered();
            return BuildResponse(_job);
        }

        public static object Get(Dictionary<string, object> args)
        {
            if (_job == null)
                _job = LoadJob();
            if (_job == null)
                return VmAutomationResponse.Error("No Unity package import job was found.", "job_not_found");

            string jobId = GetString(args, "jobId");
            if (!string.IsNullOrEmpty(jobId) && !string.Equals(jobId, _job.JobId, StringComparison.Ordinal))
                return VmAutomationResponse.Error($"Unity package import job '{jobId}' was not found.", "job_not_found");

            return BuildResponse(_job);
        }

        private static void ContinueJob()
        {
            if (_job == null || _job.IsTerminal)
            {
                UnregisterUpdate();
                UnregisterCallbacks();
                return;
            }

            if (DateTime.UtcNow - _job.StartedAt >= ImportTimeout)
            {
                CompleteFailure("import_timeout",
                    $"Unity package import did not reach a completion callback within {ImportTimeout.TotalMinutes:0} minutes.");
                return;
            }

            if (_job.Status != "queued" || EditorApplication.isCompiling || EditorApplication.isUpdating)
                return;

            _job.Status = "running";
            TouchAndSave();
            try
            {
                AssetDatabase.ImportPackage(_job.PackagePath, false);
            }
            catch (Exception exception)
            {
                CompleteFailure("import_failed", exception.GetBaseException().Message);
            }
        }

        private static void OnImportStarted(string callbackPackageName)
        {
            if (!MatchesActivePackage(callbackPackageName))
                return;

            _job.Started = true;
            _job.CallbackPackageName = callbackPackageName ?? "";
            TouchAndSave();
        }

        private static void OnImportCompleted(string callbackPackageName)
        {
            if (!MatchesActivePackage(callbackPackageName))
                return;

            _job.CallbackPackageName = callbackPackageName ?? _job.CallbackPackageName;
            _job.Completed = true;
            _job.Cancelled = false;
            _job.CompletionConfirmedBy = "AssetDatabase.importPackageCompleted";
            var pathsBefore = new HashSet<string>(_job.PathsBefore ?? new List<string>(),
                StringComparer.OrdinalIgnoreCase);
            _job.NewAssetPaths = AssetDatabase.GetAllAssetPaths()
                .Where(path => !pathsBefore.Contains(path))
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToList();
            _job.Status = "succeeded";
            CompleteTerminalJob();
        }

        private static void OnImportCancelled(string callbackPackageName)
        {
            if (!MatchesActivePackage(callbackPackageName))
                return;

            _job.CallbackPackageName = callbackPackageName ?? _job.CallbackPackageName;
            _job.Cancelled = true;
            CompleteFailure("import_cancelled", "Unity package import was cancelled.", "cancelled");
        }

        private static void OnImportFailed(string callbackPackageName, string error)
        {
            if (!MatchesActivePackage(callbackPackageName))
                return;

            _job.CallbackPackageName = callbackPackageName ?? _job.CallbackPackageName;
            CompleteFailure("import_failed", string.IsNullOrEmpty(error)
                ? "Unity package import failed."
                : error);
        }

        private static bool MatchesActivePackage(string callbackPackageName)
        {
            if (_job == null || _job.IsTerminal)
                return false;
            if (string.IsNullOrEmpty(callbackPackageName))
                return true;

            string expectedWithoutExtension = Path.Combine(
                Path.GetDirectoryName(_job.PackagePath) ?? "", _job.PackageName);
            string callbackWithoutExtension = callbackPackageName.Trim();
            if (callbackWithoutExtension.EndsWith(".unitypackage", StringComparison.OrdinalIgnoreCase))
            {
                callbackWithoutExtension = callbackWithoutExtension.Substring(0,
                    callbackWithoutExtension.Length - ".unitypackage".Length);
            }
            try
            {
                if (Path.IsPathRooted(callbackWithoutExtension))
                    callbackWithoutExtension = Path.GetFullPath(callbackWithoutExtension);
            }
            catch
            {
                // Fall through to the package-name comparison below.
            }

            return string.Equals(expectedWithoutExtension, callbackWithoutExtension,
                       StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(_job.PackageName, Path.GetFileName(callbackWithoutExtension),
                       StringComparison.OrdinalIgnoreCase);
        }

        private static void CompleteFailure(string errorCode, string error, string status = "failed")
        {
            if (_job == null || _job.IsTerminal)
                return;

            _job.Status = status;
            _job.ErrorCode = errorCode ?? "import_failed";
            _job.Error = error ?? "Unity package import failed.";
            CompleteTerminalJob();
        }

        private static void CompleteTerminalJob()
        {
            TouchAndSave();
            UnregisterUpdate();
            UnregisterCallbacks();
        }

        private static Dictionary<string, object> BuildResponse(UnityPackageImportJob job)
        {
            int durationMs = Math.Max(0, (int)(job.UpdatedAt - job.StartedAt).TotalMilliseconds);
            var response = new Dictionary<string, object>
            {
                { "success", job.IsTerminal ? job.Status == "succeeded" : true },
                { "jobId", job.JobId },
                { "jobType", JobType },
                { "status", job.Status },
                { "pollRoute", "jobs/get" },
                { "pollArgs", new Dictionary<string, object>
                    {
                        { "jobId", job.JobId },
                        { "jobType", JobType },
                    }
                },
                { "packagePath", job.PackagePath },
                { "packageName", job.PackageName },
                { "callbackPackageName", job.CallbackPackageName ?? "" },
                { "interactive", false },
                { "started", job.Started },
                { "completed", job.Completed },
                { "cancelled", job.Cancelled },
                { "recoveredAfterReload", job.RecoveredAfterReload },
                { "completionConfirmedBy", job.CompletionConfirmedBy ?? "" },
                { "newAssetCount", job.NewAssetPaths?.Count ?? 0 },
                { "newAssetPaths", job.NewAssetPaths ?? new List<string>() },
                { "durationMs", durationMs },
                { "startedAt", job.StartedAt.ToString("O") },
                { "updatedAt", job.UpdatedAt.ToString("O") },
            };
            if (!string.IsNullOrEmpty(job.ErrorCode))
                response["errorCode"] = job.ErrorCode;
            if (!string.IsNullOrEmpty(job.Error))
                response["error"] = job.Error;
            VmAutomationJobHistory.PublishAccessToken(response, JobType, job.JobId, job.OwnerAgentId);
            return response;
        }

        private static Dictionary<string, object> BuildFailure(string errorCode, string error,
            string packagePath, string packageName)
        {
            return new Dictionary<string, object>
            {
                { "success", false },
                { "status", "failed" },
                { "errorCode", errorCode },
                { "error", error ?? "" },
                { "packagePath", packagePath ?? "" },
                { "packageName", packageName ?? "" },
                { "interactive", false },
            };
        }

        private static void EnsureCallbacksRegistered()
        {
            if (_callbacksRegistered)
                return;
            AssetDatabase.importPackageStarted += OnImportStarted;
            AssetDatabase.importPackageCompleted += OnImportCompleted;
            AssetDatabase.importPackageCancelled += OnImportCancelled;
            AssetDatabase.importPackageFailed += OnImportFailed;
            _callbacksRegistered = true;
        }

        private static void UnregisterCallbacks()
        {
            if (!_callbacksRegistered)
                return;
            AssetDatabase.importPackageStarted -= OnImportStarted;
            AssetDatabase.importPackageCompleted -= OnImportCompleted;
            AssetDatabase.importPackageCancelled -= OnImportCancelled;
            AssetDatabase.importPackageFailed -= OnImportFailed;
            _callbacksRegistered = false;
        }

        private static void EnsureUpdateRegistered()
        {
            if (_updateRegistered)
                return;
            EditorApplication.update += ContinueJob;
            _updateRegistered = true;
        }

        private static void UnregisterUpdate()
        {
            if (!_updateRegistered)
                return;
            EditorApplication.update -= ContinueJob;
            _updateRegistered = false;
        }

        private static void TouchAndSave()
        {
            if (_job == null)
                return;
            _job.UpdatedAt = DateTime.UtcNow;
            SaveJob();
        }

        private static void SaveJob()
        {
            if (_job == null)
                return;
            string path = GetJobPath();
            VmAutomationPersistenceFile.WriteAllText(path, MiniJson.Serialize(_job.ToDictionary()));
            VmAutomationJobHistory.Record(JobType, _job.JobId, _job.OwnerAgentId, _job.Status,
                BuildResponse(_job));
        }

        private static UnityPackageImportJob LoadJob()
        {
            try
            {
                string path = GetJobPath();
                if (!VmAutomationPersistenceFile.TryReadAllText(path, out string contents))
                    return null;
                return UnityPackageImportJob.FromDictionary(
                    MiniJson.Deserialize(contents) as Dictionary<string, object>);
            }
            catch (Exception exception)
            {
                Debug.LogWarning($"[VM Unity Automation UnityPackage Import] Failed to restore import job: {exception.Message}");
                return null;
            }
        }

        private static string GetJobPath()
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.Combine(projectRoot, "Library", "VMUnityAutomation", "unitypackage-import-job.json");
        }

        private static string NormalizePackagePath(string packagePath)
        {
            string normalized = packagePath.Replace('\\', '/').Trim();
            if (!Path.IsPathRooted(normalized))
            {
                string projectRoot = Directory.GetParent(Application.dataPath).FullName;
                normalized = Path.Combine(projectRoot, normalized);
            }
            return Path.GetFullPath(normalized);
        }

        private static string GetOwner(Dictionary<string, object> args)
        {
            string owner = GetString(args, "_agentId");
            return string.IsNullOrEmpty(owner) ? "anonymous" : owner;
        }

        private static string GetString(Dictionary<string, object> args, string key)
        {
            return args != null && args.TryGetValue(key, out object value) && value != null
                ? value.ToString()
                : "";
        }

        private sealed class UnityPackageImportJob
        {
            public string JobId;
            public string Status;
            public string OwnerAgentId;
            public string PackagePath;
            public string PackageName;
            public string CallbackPackageName;
            public string CompletionConfirmedBy;
            public string ErrorCode;
            public string Error;
            public bool Started;
            public bool Completed;
            public bool Cancelled;
            public bool RecoveredAfterReload;
            public List<string> PathsBefore;
            public List<string> NewAssetPaths;
            public DateTime StartedAt;
            public DateTime UpdatedAt;

            public bool IsTerminal => Status == "succeeded" || Status == "failed" || Status == "cancelled";

            public Dictionary<string, object> ToDictionary()
            {
                return new Dictionary<string, object>
                {
                    { "jobId", JobId },
                    { "status", Status },
                    { "ownerAgentId", OwnerAgentId },
                    { "packagePath", PackagePath },
                    { "packageName", PackageName },
                    { "callbackPackageName", CallbackPackageName ?? "" },
                    { "completionConfirmedBy", CompletionConfirmedBy ?? "" },
                    { "errorCode", ErrorCode ?? "" },
                    { "error", Error ?? "" },
                    { "started", Started },
                    { "completed", Completed },
                    { "cancelled", Cancelled },
                    { "recoveredAfterReload", RecoveredAfterReload },
                    { "pathsBefore", PathsBefore ?? new List<string>() },
                    { "newAssetPaths", NewAssetPaths ?? new List<string>() },
                    { "startedAt", StartedAt.ToString("O") },
                    { "updatedAt", UpdatedAt.ToString("O") },
                };
            }

            public static UnityPackageImportJob FromDictionary(Dictionary<string, object> values)
            {
                if (values == null)
                    return null;
                return new UnityPackageImportJob
                {
                    JobId = GetString(values, "jobId"),
                    Status = GetString(values, "status"),
                    OwnerAgentId = GetString(values, "ownerAgentId"),
                    PackagePath = GetString(values, "packagePath"),
                    PackageName = GetString(values, "packageName"),
                    CallbackPackageName = GetString(values, "callbackPackageName"),
                    CompletionConfirmedBy = GetString(values, "completionConfirmedBy"),
                    ErrorCode = GetString(values, "errorCode"),
                    Error = GetString(values, "error"),
                    Started = GetBool(values, "started"),
                    Completed = GetBool(values, "completed"),
                    Cancelled = GetBool(values, "cancelled"),
                    RecoveredAfterReload = GetBool(values, "recoveredAfterReload"),
                    PathsBefore = GetStrings(values, "pathsBefore"),
                    NewAssetPaths = GetStrings(values, "newAssetPaths"),
                    StartedAt = ParseDate(values, "startedAt"),
                    UpdatedAt = ParseDate(values, "updatedAt"),
                };
            }

            private static bool GetBool(Dictionary<string, object> values, string key)
            {
                if (values == null || !values.TryGetValue(key, out object value) || value == null)
                    return false;
                if (value is bool result)
                    return result;
                return bool.TryParse(value.ToString(), out result) && result;
            }

            private static List<string> GetStrings(Dictionary<string, object> values, string key)
            {
                if (values == null || !values.TryGetValue(key, out object value) || !(value is IEnumerable list))
                    return new List<string>();
                return list.Cast<object>().Where(item => item != null).Select(item => item.ToString()).ToList();
            }

            private static DateTime ParseDate(Dictionary<string, object> values, string key)
            {
                return DateTime.TryParse(GetString(values, key), CultureInfo.InvariantCulture,
                    DateTimeStyles.RoundtripKind, out DateTime result)
                    ? result
                    : DateTime.UtcNow;
            }
        }
    }
}
