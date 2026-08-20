using System;
using System.Collections.Generic;
using System.Linq;

namespace VMUnityAutomation.Editor
{
    /// <summary>
    /// Single registration source for persistent Job identity and lifecycle ownership.
    /// History resolves an omitted jobType and remains a read-only terminal fallback when
    /// the owning runner no longer retains an old Job; it never executes Job work.
    /// </summary>
    internal static class MCPJobOwnerRegistry
    {
        private delegate object JobCommand(Dictionary<string, object> arguments);

        private sealed class Owner
        {
            internal Owner(string jobType, JobCommand get, JobCommand cancel,
                JobCommand cleanup = null, Func<string, bool> containsJob = null)
            {
                JobType = jobType;
                Get = get;
                Cancel = cancel;
                Cleanup = cleanup;
                ContainsJob = containsJob;
            }

            internal string JobType { get; }
            internal JobCommand Get { get; }
            internal JobCommand Cancel { get; }
            internal JobCommand Cleanup { get; }
            internal Func<string, bool> ContainsJob { get; }
        }

        private static readonly IReadOnlyDictionary<string, Owner> Owners = BuildOwners();
        private static readonly Owner WorkspaceRequestOwner =
            new Owner("workspace-request", MCPWorkspaceJobRunner.Get,
                MCPWorkspaceJobRunner.Cancel, MCPWorkspaceJobRunner.Cleanup);
        private static readonly Owner HistoryOwner =
            new Owner("history-only", GetHistorySnapshot,
                CancelHistorySnapshot, CleanupHistorySnapshot);

        internal static object Get(Dictionary<string, object> arguments)
        {
            if (!TryResolve(arguments, allowRequestId: true,
                    out Owner owner, out object error))
                return error;
            object result = owner.Get(arguments);
            return IsMissingOwnedJob(result) && HasJobId(arguments)
                ? GetHistorySnapshot(arguments)
                : result;
        }

        internal static object Cancel(Dictionary<string, object> arguments)
        {
            if (!TryResolve(arguments, allowRequestId: false,
                    out Owner owner, out object error))
                return error;
            object result = owner.Cancel(arguments);
            return IsMissingOwnedJob(result)
                ? CancelHistorySnapshot(arguments)
                : result;
        }

        internal static object Cleanup(Dictionary<string, object> arguments)
        {
            if (!TryResolve(arguments, allowRequestId: false,
                    out Owner owner, out object error))
                return error;
            if (owner.Cleanup == null)
            {
                return MCPResponse.Error(
                    $"Job type '{owner.JobType}' does not expose a cleanup contract.",
                    "job_not_cleanable");
            }
            object result = owner.Cleanup(arguments);
            return IsMissingOwnedJob(result)
                ? CleanupHistorySnapshot(arguments)
                : result;
        }

        private static bool TryResolve(Dictionary<string, object> arguments,
            bool allowRequestId, out Owner owner, out object error)
        {
            arguments ??= new Dictionary<string, object>();
            string requestedJobType = GetString(arguments, "jobType");
            string requestId = GetString(arguments, "requestId");
            string jobId = GetString(arguments, "jobId");

            if (!string.IsNullOrWhiteSpace(requestedJobType))
            {
                if (Owners.TryGetValue(requestedJobType, out owner))
                {
                    if (!string.IsNullOrWhiteSpace(requestId) &&
                        !MCPWorkspaceJobRunner.OwnsJobType(requestedJobType))
                    {
                        error = MCPResponse.Error(
                            $"Job type '{requestedJobType}' cannot be resolved by requestId.",
                            "invalid_arguments");
                        return false;
                    }
                    if (!string.IsNullOrWhiteSpace(jobId))
                    {
                        Owner liveOwner = FindLiveOwner(jobId);
                        if (liveOwner != null &&
                            !string.Equals(liveOwner.JobType, requestedJobType,
                                StringComparison.Ordinal))
                        {
                            error = MCPResponse.Error(
                                $"Job '{jobId}' is owned by job type '{liveOwner.JobType}', not '{requestedJobType}'.",
                                "job_type_mismatch");
                            return false;
                        }
                        if (liveOwner == null &&
                            TryGetHistoricalJobIgnoringRequestedType(
                                arguments, out Dictionary<string, object> historicalJob) &&
                            !string.Equals(GetString(historicalJob, "jobType"),
                                requestedJobType, StringComparison.Ordinal))
                        {
                            string historicalJobType = GetString(historicalJob, "jobType");
                            error = MCPResponse.Error(
                                $"Job '{jobId}' is owned by job type '{historicalJobType}', not '{requestedJobType}'.",
                                "job_type_mismatch");
                            return false;
                        }
                    }
                    error = null;
                    return true;
                }
                if (!string.IsNullOrWhiteSpace(jobId) &&
                    HasHistorySnapshot(arguments))
                {
                    owner = HistoryOwner;
                    error = null;
                    return true;
                }
                owner = null;
                error = MCPResponse.Error(
                    $"Job type '{requestedJobType}' has no registered owner.",
                    "job_owner_not_registered");
                return false;
            }

            if (allowRequestId && !string.IsNullOrWhiteSpace(requestId))
            {
                owner = WorkspaceRequestOwner;
                error = null;
                return true;
            }
            if (string.IsNullOrWhiteSpace(jobId))
            {
                owner = null;
                error = MCPResponse.Error(
                    allowRequestId
                        ? "jobs/get requires jobId or requestId."
                        : "jobId is required.",
                    "invalid_arguments");
                return false;
            }

            owner = FindLiveOwner(jobId);
            if (owner != null)
            {
                error = null;
                return true;
            }

            object historyResult = MCPJobHistory.Get(arguments);
            Dictionary<string, object> history = MCPResponse.ToDictionary(historyResult);
            if (history == null ||
                history.TryGetValue("job", out object jobValue) == false ||
                MCPResponse.ToDictionary(jobValue) is not { } job)
            {
                owner = null;
                error = historyResult;
                return false;
            }

            string persistedJobType = GetString(job, "jobType");
            if (!Owners.TryGetValue(persistedJobType, out owner))
            {
                owner = HistoryOwner;
            }
            error = null;
            return true;
        }

        private static IReadOnlyDictionary<string, Owner> BuildOwners()
        {
            var owners = new Dictionary<string, Owner>(StringComparer.Ordinal);
            Register(owners, new Owner(MCPWorkspaceJobRunner.AssetRefreshJobType,
                MCPWorkspaceJobRunner.Get, MCPWorkspaceJobRunner.Cancel,
                MCPWorkspaceJobRunner.Cleanup, jobId =>
                    MCPWorkspaceJobRunner.ContainsJob(
                        MCPWorkspaceJobRunner.AssetRefreshJobType, jobId)));
            Register(owners, new Owner(MCPWorkspaceJobRunner.PackageUpdateJobType,
                MCPWorkspaceJobRunner.Get, MCPWorkspaceJobRunner.Cancel,
                MCPWorkspaceJobRunner.Cleanup, jobId =>
                    MCPWorkspaceJobRunner.ContainsJob(
                        MCPWorkspaceJobRunner.PackageUpdateJobType, jobId)));
            Register(owners, new Owner(MCPWorkspaceJobRunner.PackageResolveJobType,
                MCPWorkspaceJobRunner.Get, MCPWorkspaceJobRunner.Cancel,
                MCPWorkspaceJobRunner.Cleanup, jobId =>
                    MCPWorkspaceJobRunner.ContainsJob(
                        MCPWorkspaceJobRunner.PackageResolveJobType, jobId)));
            Register(owners, new Owner(MCPAssetTransactionJobRunner.JobType,
                MCPWorkspaceJobRunner.Get, MCPWorkspaceJobRunner.Cancel,
                MCPWorkspaceJobRunner.Cleanup, jobId =>
                    MCPWorkspaceJobRunner.ContainsJob(
                        MCPAssetTransactionJobRunner.JobType, jobId)));
            Register(owners, new Owner(MCPPersistentJobRunner.ExecuteCodeJobType,
                MCPPersistentJobRunner.Get, MCPPersistentJobRunner.Cancel,
                MCPPersistentJobRunner.RequestCleanup, jobId =>
                    MCPPersistentJobRunner.ContainsJob(
                        MCPPersistentJobRunner.ExecuteCodeJobType, jobId)));
            Register(owners, new Owner(MCPPersistentJobRunner.ProjectToolJobType,
                MCPPersistentJobRunner.Get, MCPPersistentJobRunner.Cancel,
                MCPPersistentJobRunner.RequestCleanup, jobId =>
                    MCPPersistentJobRunner.ContainsJob(
                        MCPPersistentJobRunner.ProjectToolJobType, jobId)));
            Register(owners, new Owner(MCPBuildCommands.JobType,
                MCPBuildCommands.GetBuildJob, MCPBuildCommands.CancelBuild));
            Register(owners, new Owner(MCPTestRunnerCommands.JobType,
                MCPTestRunnerCommands.GetTestJob, MCPTestRunnerCommands.CancelTestJob));
            Register(owners, new Owner(MCPPackageTestCommands.JobType,
                MCPPackageTestCommands.GetPackageTestJob,
                MCPPackageTestCommands.CancelPackageTest));
            Register(owners, new Owner(MCPMemoryProfilerCommands.JobType,
                MCPMemoryProfilerCommands.GetMemorySnapshotStatus,
                MCPMemoryProfilerCommands.CancelMemorySnapshot));
            Register(owners, new Owner(MCPAddressablesCommands.JobType,
                MCPAddressablesCommands.GetBuildJob,
                MCPAddressablesCommands.CancelBuild));
            return owners;
        }

        private static Owner FindLiveOwner(string jobId)
        {
            return Owners.Values.FirstOrDefault(candidate =>
                candidate.ContainsJob != null && candidate.ContainsJob(jobId));
        }

        private static bool HasHistorySnapshot(Dictionary<string, object> arguments)
        {
            Dictionary<string, object> history = MCPResponse.ToDictionary(
                MCPJobHistory.Get(arguments));
            return history != null && history.TryGetValue("job", out object jobValue) &&
                   MCPResponse.ToDictionary(jobValue) != null;
        }

        private static bool TryGetHistoricalJobIgnoringRequestedType(
            Dictionary<string, object> arguments,
            out Dictionary<string, object> job)
        {
            var lookup = arguments != null
                ? new Dictionary<string, object>(arguments)
                : new Dictionary<string, object>();
            lookup.Remove("jobType");
            Dictionary<string, object> history = MCPResponse.ToDictionary(
                MCPJobHistory.Get(lookup));
            if (history != null && history.TryGetValue("job", out object jobValue))
            {
                job = MCPResponse.ToDictionary(jobValue);
                return job != null;
            }
            job = null;
            return false;
        }

        private static object GetHistorySnapshot(Dictionary<string, object> arguments)
        {
            object historyResult = MCPJobHistory.Get(arguments);
            Dictionary<string, object> history = MCPResponse.ToDictionary(historyResult);
            if (history == null ||
                !history.TryGetValue("job", out object jobValue) ||
                MCPResponse.ToDictionary(jobValue) is not { } job)
                return historyResult;
            return job.TryGetValue("snapshot", out object snapshot)
                ? snapshot
                : job;
        }

        private static object CancelHistorySnapshot(Dictionary<string, object> arguments)
        {
            return BuildHistoryLifecycleError(arguments, "job_not_cancellable",
                "does not expose a live cancellation contract");
        }

        private static object CleanupHistorySnapshot(Dictionary<string, object> arguments)
        {
            return BuildHistoryLifecycleError(arguments, "job_not_cleanable",
                "does not expose a live cleanup contract");
        }

        private static object BuildHistoryLifecycleError(
            Dictionary<string, object> arguments, string errorCode, string reason)
        {
            object historyResult = MCPJobHistory.Get(arguments);
            Dictionary<string, object> history = MCPResponse.ToDictionary(historyResult);
            if (history == null ||
                !history.TryGetValue("job", out object jobValue) ||
                MCPResponse.ToDictionary(jobValue) is not { } job)
                return historyResult;

            string jobId = GetString(job, "jobId");
            string jobType = GetString(job, "jobType");
            return MCPResponse.Error(
                $"Persisted Job '{jobType}/{jobId}' {reason}.",
                errorCode, false, new Dictionary<string, object>
                {
                    { "jobId", jobId },
                    { "jobType", jobType },
                    { "status", GetString(job, "status") },
                });
        }

        private static bool IsMissingOwnedJob(object result)
        {
            return MCPResponse.TryGetError(result, out _, out string errorCode, out _) &&
                   string.Equals(errorCode, "job_not_found", StringComparison.Ordinal);
        }

        private static bool HasJobId(Dictionary<string, object> arguments)
        {
            return !string.IsNullOrWhiteSpace(GetString(arguments, "jobId"));
        }

        private static void Register(IDictionary<string, Owner> owners, Owner owner)
        {
            if (string.IsNullOrWhiteSpace(owner.JobType) ||
                owners.ContainsKey(owner.JobType))
            {
                throw new InvalidOperationException(
                    $"Duplicate or empty MCP Job owner registration '{owner.JobType}'.");
            }
            owners.Add(owner.JobType, owner);
        }

        private static string GetString(Dictionary<string, object> values, string key)
        {
            return values != null && values.TryGetValue(key, out object value) && value != null
                ? value.ToString()
                : "";
        }
    }
}
