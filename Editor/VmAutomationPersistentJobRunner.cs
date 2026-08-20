using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace VMUnityAutomation.Editor
{
    /// <summary>
    /// Project-scoped durable execution owner for operations whose lifetime must outlive an HTTP queue ticket.
    /// The persisted request is replayed only while queued. A domain reload observed after execution began is
    /// reported as interrupted so an unknown partial side effect is never silently replayed.
    /// </summary>
    [InitializeOnLoad]
    internal static class VmAutomationPersistentJobRunner
    {
        internal const string ExecuteCodeJobType = "execute-code";
        internal const string ProjectToolJobType = "project-tool";

        private const string QueuedStatus = "queued";
        private const string RunningStatus = "running";
        private const string SucceededStatus = "succeeded";
        private const string FailedStatus = "failed";
        private const string CanceledStatus = "canceled";
        private const string InterruptedStatus = "interrupted";

        private const string CleanupNoneStatus = "none";
        private const string CleanupAvailableStatus = "available";
        private const string CleanupQueuedStatus = "queued";
        private const string CleanupRunningStatus = "running";
        private const string CleanupSucceededStatus = "succeeded";
        private const string CleanupFailedStatus = "failed";
        private const string CleanupCanceledStatus = "canceled";
        private const string CleanupInterruptedStatus = "interrupted";

        private const int MaxPersistedJobs = 200;
        private static readonly object Sync = new object();
        private static readonly List<Dictionary<string, object>> Jobs = new();

        private static string currentJobId;
        private static bool loaded;
        private static bool ticking;

        internal static string CurrentJobId => currentJobId ?? "";

        internal static bool IsCurrentJobCancellationRequested
        {
            get
            {
                if (string.IsNullOrEmpty(currentJobId))
                    return false;

                lock (Sync)
                {
                    Dictionary<string, object> job = FindById(currentJobId);
                    return job != null && GetBool(job, "cancellationRequested", false);
                }
            }
        }

        static VmAutomationPersistentJobRunner()
        {
            EnsureLoaded();
            RecoverInterruptedJobs();
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }

        internal static object StartExecuteCode(Dictionary<string, object> args)
        {
            args ??= new Dictionary<string, object>();
            return Start(
                ExecuteCodeJobType,
                "editor/execute-code",
                args,
                new Dictionary<string, object>
                {
                    { "sideEffects", new List<object>
                    {
                        "executesArbitraryCode",
                        "writesAssets",
                        "writesScene",
                        "changesRuntimeState",
                        "advancesEditorFrames",
                        "advancesLogicTicks",
                        "createsTemporaryObjects",
                        "capturesArtifacts",
                        "performsExternalIO",
                        "writesProjectFiles",
                        "reloadsDomain",
                    } },
                });
        }

        internal static object StartProjectTool(string toolName, Dictionary<string, object> args,
            Dictionary<string, object> metadata)
        {
            return Start(ProjectToolJobType, toolName, args, metadata);
        }

        private static object Start(string jobType, string operation, Dictionary<string, object> args,
            Dictionary<string, object> metadata)
        {
            EnsureLoaded();
            args ??= new Dictionary<string, object>();
            metadata ??= new Dictionary<string, object>();

            string ownerAgentId = GetString(args, "_agentId", "anonymous");
            string idempotencyKey = GetString(args, "idempotencyKey");
            var persistedArgs = StripTransportArguments(args);
            string requestFingerprint = ComputeRequestFingerprint(jobType, operation, persistedArgs);

            lock (Sync)
            {
                if (!string.IsNullOrEmpty(idempotencyKey))
                {
                    Dictionary<string, object> existing = Jobs
                        .Where(job => GetString(job, "jobType") == jobType &&
                                      GetString(job, "operation") == operation &&
                                      GetString(job, "idempotencyKey") == idempotencyKey)
                        .OrderByDescending(job => ParseDate(GetString(job, "createdAt")))
                        .FirstOrDefault();
                    if (existing != null)
                    {
                        string existingFingerprint = GetString(existing, "requestFingerprint");
                        if (string.IsNullOrEmpty(existingFingerprint))
                        {
                            existingFingerprint = ComputeRequestFingerprint(
                                GetString(existing, "jobType"),
                                GetString(existing, "operation"),
                                GetDictionary(existing, "request"));
                        }
                        if (!string.Equals(existingFingerprint,
                                requestFingerprint, StringComparison.Ordinal))
                        {
                            return VmAutomationResponse.Error(
                                $"Idempotency key '{idempotencyKey}' was already used with different arguments.",
                                "idempotency_conflict",
                                false,
                                new Dictionary<string, object>
                                {
                                    { "jobId", GetString(existing, "jobId") },
                                    { "operation", operation },
                                });
                        }

                        var reused = BuildPublicJob(existing, includeAccessToken: true);
                        VmAutomationContractMetadata.AddTag(reused, VmAutomationContractMetadata.Tag.Reused);
                        return reused;
                    }
                }

                string now = DateTime.UtcNow.ToString("O");
                string cleanupCode = GetString(persistedArgs, "cleanupCode");
                string cleanupToolName = GetString(metadata, "cleanupToolName");
                bool cleanupAvailable = !string.IsNullOrWhiteSpace(cleanupCode);
                bool cleanupDeclared = cleanupAvailable ||
                                       !string.IsNullOrWhiteSpace(cleanupToolName);
                bool incremental = VmAutomationContractMetadata.HasTag(
                    metadata, VmAutomationContractMetadata.Tag.IncrementalJob);
                var job = new Dictionary<string, object>
                {
                    { "jobId", Guid.NewGuid().ToString("N") },
                    { "jobAccessToken", Guid.NewGuid().ToString("N") },
                    { "jobType", jobType },
                    { "operation", operation },
                    { "ownerAgentId", ownerAgentId },
                    { "idempotencyKey", idempotencyKey },
                    { "requestFingerprint", requestFingerprint },
                    { "status", QueuedStatus },
                    { "cleanupStatus", cleanupAvailable ? CleanupAvailableStatus : CleanupNoneStatus },
                    { "cleanupDeclared", cleanupDeclared },
                    { "cleanupToolName", cleanupToolName },
                    { "cleanupToken", "" },
                    { "incremental", incremental },
                    { "jobState", new Dictionary<string, object>() },
                    { "progress", null },
                    { "statusMessage", "Queued." },
                    { "nextRunAt", now },
                    { "stepCount", 0 },
                    { "cancellationRequested", false },
                    { "createdAt", now },
                    { "updatedAt", now },
                    { "startedAt", "" },
                    { "completedAt", "" },
                    { "request", persistedArgs },
                    { "sideEffects", CloneJsonValue(metadata.TryGetValue("sideEffects", out object effects)
                        ? effects
                        : new List<object>()) },
                    { "result", null },
                    { "error", null },
                    { "cleanupResult", null },
                    { "cleanupError", null },
                };
                Jobs.Add(job);
                Prune();
                Save();
                Record(job);

                return BuildPublicJob(job, includeAccessToken: true);
            }
        }

        internal static bool OwnsJobType(string jobType)
        {
            return jobType == ExecuteCodeJobType || jobType == ProjectToolJobType;
        }

        internal static bool ContainsJob(string jobType, string jobId)
        {
            EnsureLoaded();
            lock (Sync)
            {
                Dictionary<string, object> job = FindById(jobId);
                return job != null &&
                       string.Equals(GetString(job, "jobType"), jobType,
                           StringComparison.Ordinal);
            }
        }

        internal static object Get(Dictionary<string, object> args)
        {
            EnsureLoaded();
            args ??= new Dictionary<string, object>();
            string jobId = GetString(args, "jobId");
            if (string.IsNullOrWhiteSpace(jobId))
                return VmAutomationResponse.Error("jobId is required.", "invalid_arguments");

            lock (Sync)
            {
                Dictionary<string, object> job = FindById(jobId);
                if (job == null)
                    return VmAutomationResponse.Error($"Job '{jobId}' was not found.", "job_not_found");
                if (!CanAccess(job, args))
                    return VmAutomationResponse.Error("Job belongs to another agent and the jobAccessToken was not supplied.",
                        "job_owner_mismatch");
                return BuildPublicJob(job, includeAccessToken: false);
            }
        }

        internal static object Cancel(Dictionary<string, object> args)
        {
            EnsureLoaded();
            args ??= new Dictionary<string, object>();
            string jobId = GetString(args, "jobId");
            if (string.IsNullOrWhiteSpace(jobId))
                return VmAutomationResponse.Error("jobId is required.", "invalid_arguments");

            lock (Sync)
            {
                Dictionary<string, object> job = FindById(jobId);
                if (job == null)
                    return VmAutomationResponse.Error($"Job '{jobId}' was not found.", "job_not_found");
                if (!CanAccess(job, args))
                    return VmAutomationResponse.Error("Job belongs to another agent and the jobAccessToken was not supplied.",
                        "job_owner_mismatch");

                string status = GetString(job, "status");
                string cleanupStatus = GetString(job, "cleanupStatus");
                if (cleanupStatus == CleanupQueuedStatus)
                {
                    job["cleanupStatus"] = CleanupCanceledStatus;
                    job["updatedAt"] = DateTime.UtcNow.ToString("O");
                }
                else if (status == QueuedStatus)
                {
                    job["status"] = CanceledStatus;
                    job["cancellationRequested"] = true;
                    // No operation code ran, so there is no owned side effect to
                    // clean. In particular, do not execute caller cleanupCode for
                    // a request that never crossed the execution boundary.
                    job["cleanupStatus"] = CleanupNoneStatus;
                    job["cleanupDeclared"] = false;
                    job["cleanupToken"] = "";
                    job["completedAt"] = DateTime.UtcNow.ToString("O");
                    job["updatedAt"] = job["completedAt"];
                }
                else if (status == RunningStatus)
                {
                    job["cancellationRequested"] = true;
                    job["updatedAt"] = DateTime.UtcNow.ToString("O");
                }
                else
                {
                    return VmAutomationResponse.Error(
                        $"Job '{jobId}' is already terminal with status '{status}'.",
                        "job_not_cancellable",
                        false,
                        BuildPublicJob(job, includeAccessToken: false));
                }

                Save();
                Record(job);
                return BuildPublicJob(job, includeAccessToken: false);
            }
        }

        internal static object RequestCleanup(Dictionary<string, object> args)
        {
            EnsureLoaded();
            args ??= new Dictionary<string, object>();
            string jobId = GetString(args, "jobId");
            if (string.IsNullOrWhiteSpace(jobId))
                return VmAutomationResponse.Error("jobId is required.", "invalid_arguments");

            lock (Sync)
            {
                Dictionary<string, object> job = FindById(jobId);
                if (job == null)
                    return VmAutomationResponse.Error($"Job '{jobId}' was not found.", "job_not_found");
                if (!CanAccess(job, args))
                    return VmAutomationResponse.Error("Job belongs to another agent and the jobAccessToken was not supplied.",
                        "job_owner_mismatch");

                string status = GetString(job, "status");
                if (status != SucceededStatus && status != FailedStatus &&
                    status != InterruptedStatus && status != CanceledStatus)
                {
                    return VmAutomationResponse.Error(
                        $"Job '{jobId}' cannot be cleaned while status is '{status}'.",
                        "job_not_terminal");
                }

                string cleanupStatus = GetString(job, "cleanupStatus", CleanupNoneStatus);
                if (cleanupStatus == CleanupNoneStatus)
                {
                    string errorCode = GetBool(job, "cleanupDeclared", false)
                        ? "job_cleanup_token_missing"
                        : "job_not_cleanable";
                    string message = GetBool(job, "cleanupDeclared", false)
                        ? $"Job '{jobId}' declared cleanup but did not produce a cleanup token."
                        : $"Job '{jobId}' has no cleanup contract.";
                    return VmAutomationResponse.Error(message, errorCode);
                }
                if (cleanupStatus == CleanupQueuedStatus || cleanupStatus == CleanupRunningStatus ||
                    cleanupStatus == CleanupSucceededStatus)
                {
                    var existing = BuildPublicJob(job, includeAccessToken: false);
                    VmAutomationContractMetadata.AddTag(existing, VmAutomationContractMetadata.Tag.Reused);
                    return existing;
                }

                job["cleanupStatus"] = CleanupQueuedStatus;
                job["cleanupError"] = null;
                job["cleanupResult"] = null;
                job["updatedAt"] = DateTime.UtcNow.ToString("O");
                Save();
                Record(job);

                return BuildPublicJob(job, includeAccessToken: false);
            }
        }

        private static void Tick()
        {
            if (ticking || EditorApplication.isCompiling || EditorApplication.isUpdating)
                return;

            Dictionary<string, object> job;
            bool cleanup;
            lock (Sync)
            {
                EnsureLoaded();
                job = Jobs.FirstOrDefault(candidate =>
                    GetString(candidate, "cleanupStatus") == CleanupQueuedStatus);
                cleanup = job != null;
                if (job == null)
                {
                    job = Jobs
                        .Where(candidate =>
                            GetString(candidate, "status") == QueuedStatus ||
                            GetString(candidate, "status") == RunningStatus &&
                            GetBool(candidate, "incremental", false) &&
                            (GetBool(candidate, "cancellationRequested", false) ||
                             IsDue(candidate)))
                        .OrderBy(candidate => ParseDate(
                            GetString(candidate, "nextRunAt")))
                        .ThenBy(candidate => ParseDate(
                            GetString(candidate, "createdAt")))
                        .FirstOrDefault();
                }
                if (job == null)
                    return;

                string now = DateTime.UtcNow.ToString("O");
                if (cleanup)
                {
                    job["cleanupStatus"] = CleanupRunningStatus;
                }
                else
                {
                    if (GetBool(job, "cancellationRequested", false))
                    {
                        job["status"] = CanceledStatus;
                        job["statusMessage"] = "Canceled between persistent job steps.";
                        job["completedAt"] = now;
                        job["updatedAt"] = now;
                        Save();
                        Record(job);
                        return;
                    }

                    if (GetString(job, "status") == QueuedStatus)
                    {
                        job["status"] = RunningStatus;
                        job["startedAt"] = now;
                        job["statusMessage"] = "Running.";
                    }
                }
                job["updatedAt"] = now;
                Save();
                Record(job);
            }

            ticking = true;
            try
            {
                if (cleanup)
                    ExecuteCleanup(job);
                else
                    ExecuteJob(job);
            }
            finally
            {
                currentJobId = null;
                ticking = false;
            }
        }

        private static void ExecuteJob(Dictionary<string, object> job)
        {
            string jobId = GetString(job, "jobId");
            currentJobId = jobId;

            object result;
            string stepCleanupToken = "";
            try
            {
                var request = CloneDictionary(GetDictionary(job, "request"));
                request["_agentId"] = GetString(job, "ownerAgentId", "anonymous");
                request["_jobId"] = jobId;
                if (GetString(job, "jobType") == ExecuteCodeJobType)
                {
                    result = VmAutomationEditorCommands.ExecuteCodeInline(request);
                }
                else
                {
                    if (GetBool(job, "incremental", false))
                    {
                        VmProjectToolJobStep step = VmProjectToolRegistry.ExecuteJobStepInline(
                            GetString(job, "operation"),
                            request,
                            CloneDictionary(GetDictionary(job, "jobState")));
                        stepCleanupToken = step.CleanupToken;
                        if (!step.IsComplete)
                        {
                            PersistPendingStep(job, step);
                            return;
                        }
                        result = step.Result;
                    }
                    else
                    {
                        result = VmProjectToolRegistry.ExecuteJobInline(
                            GetString(job, "operation"), request);
                    }
                }
            }
            catch (VmProjectToolException exception)
            {
                CompleteJob(job, FailedStatus, null,
                    VmAutomationResponse.Error(exception.Message, exception.ErrorCode,
                        exception.Retryable, exception.Details));
                return;
            }
            catch (OperationCanceledException exception)
            {
                CompleteJob(job, CanceledStatus, null,
                    VmAutomationResponse.Error(exception.Message, "job_canceled"));
                return;
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                CompleteJob(job, FailedStatus, null,
                    VmAutomationResponse.Error(exception.Message, "job_execution_failed"));
                return;
            }

            if (GetBool(job, "cancellationRequested", false))
            {
                CompleteJob(job, CanceledStatus, null,
                    VmAutomationResponse.Error("The job was canceled.", "job_canceled"));
                return;
            }

            if (VmAutomationResponse.TryGetError(result, out _, out _, out _))
            {
                CompleteJob(job, FailedStatus, null, result);
                return;
            }

            if (!string.IsNullOrWhiteSpace(stepCleanupToken))
                SetCleanupToken(job, stepCleanupToken);

            Dictionary<string, object> resultDictionary = VmAutomationResponse.ToDictionary(result);
            Dictionary<string, object> cleanupSource = resultDictionary;
            if (resultDictionary != null &&
                resultDictionary.TryGetValue("result", out object nestedResult) &&
                VmAutomationResponse.ToDictionary(nestedResult) is { } nestedDictionary)
            {
                cleanupSource = nestedDictionary;
            }
            if (cleanupSource != null &&
                cleanupSource.TryGetValue("cleanupToken", out object cleanupTokenValue) &&
                cleanupTokenValue != null &&
                string.IsNullOrWhiteSpace(cleanupTokenValue.ToString()) == false)
            {
                SetCleanupToken(job, cleanupTokenValue.ToString());
            }

            CompleteJob(job, SucceededStatus, result, null);
        }

        private static void ExecuteCleanup(Dictionary<string, object> job)
        {
            string jobId = GetString(job, "jobId");
            currentJobId = jobId;
            object result;

            try
            {
                if (GetString(job, "jobType") == ExecuteCodeJobType)
                {
                    var request = GetDictionary(job, "request") ?? new Dictionary<string, object>();
                    string cleanupCode = GetString(request, "cleanupCode");
                    if (string.IsNullOrWhiteSpace(cleanupCode))
                        throw new InvalidOperationException("The execute-code job has no cleanupCode.");
                    var cleanupArgs = CloneDictionary(request);
                    cleanupArgs["code"] = cleanupCode;
                    cleanupArgs.Remove("cleanupCode");
                    cleanupArgs["_agentId"] = GetString(job, "ownerAgentId", "anonymous");
                    result = VmAutomationEditorCommands.ExecuteCodeInline(cleanupArgs);
                }
                else
                {
                    string cleanupToolName = GetString(job, "cleanupToolName");
                    string cleanupToken = GetString(job, "cleanupToken");
                    if (string.IsNullOrWhiteSpace(cleanupToolName) || string.IsNullOrWhiteSpace(cleanupToken))
                        throw new InvalidOperationException("The project-tool job has no complete cleanup contract.");
                    result = VmProjectToolRegistry.ExecuteJobInline(cleanupToolName,
                        new Dictionary<string, object>
                        {
                            { "cleanupToken", cleanupToken },
                            { "action", "cleanup" },
                            { "_agentId", GetString(job, "ownerAgentId", "anonymous") },
                            { "_jobId", jobId },
                        });
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                CompleteCleanup(job, CleanupFailedStatus, null,
                    VmAutomationResponse.Error(exception.Message, "job_cleanup_failed"));
                return;
            }

            if (VmAutomationResponse.TryGetError(result, out _, out _, out _))
                CompleteCleanup(job, CleanupFailedStatus, null, result);
            else
                CompleteCleanup(job, CleanupSucceededStatus, result, null);
        }

        private static void PersistPendingStep(Dictionary<string, object> job,
            VmProjectToolJobStep step)
        {
            lock (Sync)
            {
                string now = DateTime.UtcNow.ToString("O");
                job["jobState"] = CloneJsonValue(step.State);
                job["progress"] = step.Progress.HasValue ? (object)step.Progress.Value : null;
                job["statusMessage"] = string.IsNullOrWhiteSpace(step.StatusMessage)
                    ? "Waiting for the next persistent job step."
                    : step.StatusMessage;
                job["nextRunAt"] = DateTime.UtcNow
                    .AddMilliseconds(step.DelayMilliseconds)
                    .ToString("O");
                job["stepCount"] = GetInt(job, "stepCount", 0) + 1;
                job["updatedAt"] = now;
                if (!string.IsNullOrWhiteSpace(step.CleanupToken))
                    SetCleanupTokenWithoutLock(job, step.CleanupToken);
                Save();
                Record(job);
            }
        }

        private static void SetCleanupToken(Dictionary<string, object> job, string cleanupToken)
        {
            if (string.IsNullOrWhiteSpace(cleanupToken))
                return;
            lock (Sync)
                SetCleanupTokenWithoutLock(job, cleanupToken);
        }

        private static void SetCleanupTokenWithoutLock(Dictionary<string, object> job,
            string cleanupToken)
        {
            job["cleanupToken"] = cleanupToken;
            job["cleanupStatus"] = CleanupAvailableStatus;
        }

        private static void CompleteJob(Dictionary<string, object> job, string status, object result, object error)
        {
            lock (Sync)
            {
                string now = DateTime.UtcNow.ToString("O");
                job["status"] = status;
                job["result"] = CloneJsonValue(result);
                job["error"] = CloneJsonValue(error);
                if (status == SucceededStatus)
                    job["progress"] = 1d;
                job["statusMessage"] = status == SucceededStatus
                    ? "Completed."
                    : status == CanceledStatus
                        ? "Canceled."
                        : status == InterruptedStatus
                            ? "Interrupted by a Unity domain reload."
                            : "Failed.";
                job["completedAt"] = now;
                job["updatedAt"] = now;
                Save();
                Record(job);
            }
        }

        private static void CompleteCleanup(Dictionary<string, object> job, string status, object result,
            object error)
        {
            lock (Sync)
            {
                job["cleanupStatus"] = status;
                job["cleanupResult"] = CloneJsonValue(result);
                job["cleanupError"] = CloneJsonValue(error);
                job["updatedAt"] = DateTime.UtcNow.ToString("O");
                Save();
                Record(job);
            }
        }

        private static Dictionary<string, object> BuildPublicJob(Dictionary<string, object> job,
            bool includeAccessToken)
        {
            var response = new Dictionary<string, object>
            {
                { "success", true },
                { "jobId", GetString(job, "jobId") },
                { "jobType", GetString(job, "jobType") },
                { "operation", GetString(job, "operation") },
                { "status", GetString(job, "status") },
                { "createdAt", GetString(job, "createdAt") },
                { "updatedAt", GetString(job, "updatedAt") },
            };

            string cleanupStatus = GetString(job, "cleanupStatus", CleanupNoneStatus);
            bool cleanupDeclared = GetBool(job, "cleanupDeclared", false);
            bool incremental = GetBool(job, "incremental", false);
            bool cancellationRequested = GetBool(job, "cancellationRequested", false);
            var tags = new List<string>();
            if (cleanupDeclared)
                tags.Add(VmAutomationContractMetadata.Tag.CleanupDeclared);
            if (cleanupStatus != CleanupNoneStatus)
                tags.Add(VmAutomationContractMetadata.Tag.CleanupAvailable);
            if (incremental)
                tags.Add(VmAutomationContractMetadata.Tag.IncrementalJob);
            if (cancellationRequested)
                tags.Add(VmAutomationContractMetadata.Tag.CancellationRequested);
            VmAutomationContractMetadata.SetTags(response, tags);

            if (cleanupStatus != CleanupNoneStatus)
                response["cleanupStatus"] = cleanupStatus;
            VmAutomationContractMetadata.AddOptionalString(response, "cleanupToken",
                GetString(job, "cleanupToken"));
            if (job.TryGetValue("progress", out object progress) && progress != null)
                response["progress"] = CloneJsonValue(progress);
            VmAutomationContractMetadata.AddOptionalString(response, "statusMessage",
                GetString(job, "statusMessage"));
            int stepCount = GetInt(job, "stepCount", 0);
            if (stepCount > 0)
                response["stepCount"] = stepCount;
            VmAutomationContractMetadata.AddOptionalString(response, "nextRunAt",
                GetString(job, "nextRunAt"));
            VmAutomationContractMetadata.AddOptionalString(response, "idempotencyKey",
                GetString(job, "idempotencyKey"));
            VmAutomationContractMetadata.AddOptionalString(response, "startedAt",
                GetString(job, "startedAt"));
            VmAutomationContractMetadata.AddOptionalString(response, "completedAt",
                GetString(job, "completedAt"));
            if (job.TryGetValue("sideEffects", out object sideEffects))
                VmAutomationContractMetadata.AddOptionalList(response, "sideEffects",
                    sideEffects as IEnumerable);
            AddOptionalValue(response, "result", job);
            AddOptionalValue(response, "error", job);
            AddOptionalValue(response, "cleanupResult", job);
            AddOptionalValue(response, "cleanupError", job);
            if (includeAccessToken)
                VmAutomationContractMetadata.AddOptionalString(response, "jobAccessToken",
                    GetString(job, "jobAccessToken"));
            return response;
        }

        private static void AddOptionalValue(Dictionary<string, object> target,
            string key, Dictionary<string, object> source)
        {
            if (source.TryGetValue(key, out object value) && value != null)
                target[key] = CloneJsonValue(value);
        }

        private static bool CanAccess(Dictionary<string, object> job, Dictionary<string, object> args)
        {
            string agentId = GetString(args, "_agentId", "anonymous");
            if (GetString(job, "ownerAgentId", "anonymous") == agentId)
                return true;

            string accessToken = GetString(args, "jobAccessToken");
            return !string.IsNullOrEmpty(accessToken) &&
                   string.Equals(accessToken, GetString(job, "jobAccessToken"), StringComparison.Ordinal);
        }

        private static Dictionary<string, object> StripTransportArguments(Dictionary<string, object> args)
        {
            var result = CloneDictionary(args);
            result.Remove("_agentId");
            result.Remove("_requestId");
            result.Remove("jobAccessToken");
            result.Remove("runAsJob");
            return result;
        }

        private static void EnsureLoaded()
        {
            if (loaded)
                return;

            lock (Sync)
            {
                if (loaded)
                    return;

                Jobs.Clear();
                string path = GetPath();
                try
                {
                    if (VmAutomationPersistenceFile.TryReadAllText(path, out string contents) &&
                        MiniJson.Deserialize(contents) is IList values)
                    {
                        foreach (object value in values)
                        {
                            Dictionary<string, object> job = VmAutomationResponse.ToDictionary(value);
                            if (job != null)
                                Jobs.Add(job);
                        }
                    }
                }
                catch (Exception exception)
                {
                    Debug.LogWarning($"[VM Unity Automation Jobs] Failed to load persistent jobs: {exception.Message}");
                }

                loaded = true;
                Prune();
            }
        }

        private static void RecoverInterruptedJobs()
        {
            lock (Sync)
            {
                bool changed = false;
                foreach (Dictionary<string, object> job in Jobs)
                {
                    string now = DateTime.UtcNow.ToString("O");
                    if (GetString(job, "status") == RunningStatus)
                    {
                        job["status"] = InterruptedStatus;
                        job["error"] = VmAutomationResponse.Error(
                            "The Unity domain reloaded after this job started. The request was not replayed because partial side effects are unknown.",
                            "job_interrupted_by_domain_reload",
                            false);
                        job["completedAt"] = now;
                        job["updatedAt"] = now;
                        changed = true;
                    }
                    if (GetString(job, "cleanupStatus") == CleanupRunningStatus)
                    {
                        job["cleanupStatus"] = CleanupInterruptedStatus;
                        job["cleanupError"] = VmAutomationResponse.Error(
                            "The Unity domain reloaded while cleanup was running. Cleanup was not replayed.",
                            "job_cleanup_interrupted_by_domain_reload",
                            false);
                        job["updatedAt"] = now;
                        changed = true;
                    }
                }

                if (!changed)
                    return;
                Save();
                foreach (Dictionary<string, object> job in Jobs)
                    Record(job);
            }
        }

        private static void Record(Dictionary<string, object> job)
        {
            var snapshot = BuildPublicJob(job, includeAccessToken: true);
            VmAutomationJobHistory.Record(
                GetString(job, "jobType"),
                GetString(job, "jobId"),
                GetString(job, "ownerAgentId", "anonymous"),
                GetString(job, "status"),
                snapshot);
        }

        private static void Prune()
        {
            if (Jobs.Count <= MaxPersistedJobs)
                return;

            var retained = Jobs
                .OrderByDescending(job => ParseDate(GetString(job, "updatedAt")))
                .Take(MaxPersistedJobs)
                .ToList();
            Jobs.Clear();
            Jobs.AddRange(retained);
        }

        private static void Save()
        {
            VmAutomationPersistenceFile.WriteAllText(GetPath(), MiniJson.Serialize(Jobs));
        }

        private static string GetPath()
        {
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.Combine(projectRoot, "Library", "VMUnityAutomation", "persistent-jobs-v2.json");
        }

        private static Dictionary<string, object> FindById(string jobId)
        {
            return Jobs.FirstOrDefault(job => GetString(job, "jobId") == jobId);
        }

        private static Dictionary<string, object> CloneDictionary(Dictionary<string, object> source)
        {
            if (source == null)
                return new Dictionary<string, object>();
            return VmAutomationResponse.ToDictionary(CloneJsonValue(source)) ?? new Dictionary<string, object>();
        }

        private static object CloneJsonValue(object value)
        {
            if (value == null)
                return null;
            try
            {
                return MiniJson.Deserialize(MiniJson.Serialize(value));
            }
            catch
            {
                return value?.ToString();
            }
        }

        private static Dictionary<string, object> GetDictionary(Dictionary<string, object> source, string key)
        {
            return source != null && source.TryGetValue(key, out object value)
                ? VmAutomationResponse.ToDictionary(value)
                : null;
        }

        private static string GetString(Dictionary<string, object> source, string key, string fallback = "")
        {
            return source != null && source.TryGetValue(key, out object value) && value != null
                ? value.ToString()
                : fallback;
        }

        private static bool GetBool(Dictionary<string, object> source, string key, bool fallback)
        {
            if (source == null || !source.TryGetValue(key, out object value) || value == null)
                return fallback;
            if (value is bool boolValue)
                return boolValue;
            return bool.TryParse(value.ToString(), out bool parsed) ? parsed : fallback;
        }

        private static int GetInt(Dictionary<string, object> source, string key, int fallback)
        {
            if (source == null || !source.TryGetValue(key, out object value) || value == null)
                return fallback;
            try
            {
                return Convert.ToInt32(value, CultureInfo.InvariantCulture);
            }
            catch
            {
                return fallback;
            }
        }

        private static bool IsDue(Dictionary<string, object> job)
        {
            DateTime nextRunAt = ParseDate(GetString(job, "nextRunAt"));
            return nextRunAt == DateTime.MinValue || nextRunAt <= DateTime.UtcNow;
        }

        private static string ComputeRequestFingerprint(string jobType, string operation,
            Dictionary<string, object> request)
        {
            object canonical = CanonicalizeJsonValue(new Dictionary<string, object>
            {
                { "jobType", jobType ?? "" },
                { "operation", operation ?? "" },
                { "request", request ?? new Dictionary<string, object>() },
            });
            byte[] bytes = Encoding.UTF8.GetBytes(MiniJson.Serialize(canonical));
            using (SHA256 hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }

        private static object CanonicalizeJsonValue(object value)
        {
            Dictionary<string, object> dictionary = VmAutomationResponse.ToDictionary(value);
            if (dictionary != null)
            {
                var result = new Dictionary<string, object>();
                foreach (KeyValuePair<string, object> pair in dictionary
                             .OrderBy(pair => pair.Key, StringComparer.Ordinal))
                {
                    result[pair.Key] = CanonicalizeJsonValue(pair.Value);
                }
                return result;
            }

            if (value is IList list)
            {
                var result = new List<object>(list.Count);
                foreach (object item in list)
                    result.Add(CanonicalizeJsonValue(item));
                return result;
            }

            return value;
        }

        private static DateTime ParseDate(string value)
        {
            return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind,
                out DateTime parsed)
                ? parsed
                : DateTime.MinValue;
        }
    }
}
