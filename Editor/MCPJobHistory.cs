using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;

namespace VMUnityAutomation.Editor
{
    internal static class MCPJobHistory
    {
        private const int MaxSnapshotCharacters = 128 * 1024;
        private const string JobAccessTokenKey = "jobAccessToken";
        private static readonly object Sync = new object();
        private static readonly Dictionary<string, PendingJobAccess> PendingAccessTokens =
            new Dictionary<string, PendingJobAccess>(StringComparer.Ordinal);
        private static List<Dictionary<string, object>> entries;
        private static PublishedState publishedState;

        public static void Record(string jobType, string jobId, string ownerAgentId, string status,
            object snapshot)
        {
            Record(jobType, jobId, ownerAgentId, status, snapshot, "");
        }

        internal static void Record(string jobType, string jobId, string ownerAgentId, string status,
            object snapshot, string requestId)
        {
            if (string.IsNullOrEmpty(jobType) || string.IsNullOrEmpty(jobId)) return;
            lock (Sync)
            {
                EnsureLoaded();
                string owner = NormalizeOwner(ownerAgentId);
                Dictionary<string, object> existing = entries.FirstOrDefault(item =>
                    GetString(item, "jobType") == jobType && GetString(item, "jobId") == jobId);
                Dictionary<string, object> snapshotDictionary =
                    MCPResponse.ToDictionary(snapshot);
                string ownerAccessToken = GetString(snapshotDictionary, JobAccessTokenKey);
                string accessToken = ResolveAccessToken(
                    jobType, jobId, owner, existing, ownerAccessToken);
                entries.RemoveAll(item => GetString(item, "jobType") == jobType &&
                                          GetString(item, "jobId") == jobId);
                object boundedSnapshot = BoundSnapshot(RemoveAccessToken(snapshot), status);
                var entry = new Dictionary<string, object>
                {
                    { "jobType", jobType }, { "jobId", jobId },
                    { "ownerAgentId", owner }, { JobAccessTokenKey, accessToken },
                    { "status", status ?? "unknown" }, { "updatedAt", DateTime.UtcNow.ToString("O") },
                    { "snapshot", boundedSnapshot },
                };
                if (!string.IsNullOrWhiteSpace(requestId))
                    entry["requestId"] = requestId;
                entries.Add(entry);
                PendingAccessTokens.Remove(GetAccessKey(jobType, jobId));
                entries = entries.OrderByDescending(item => ParseDate(GetString(item, "updatedAt")))
                    .Take(VmAutomationSettings.JobHistoryMaxEntries).ToList();
                PublishCurrentEntries();
                Save();
            }
        }

        public static void PublishAccessToken(Dictionary<string, object> response, string jobType,
            string jobId, string ownerAgentId)
        {
            if (response == null || string.IsNullOrEmpty(jobType) || string.IsNullOrEmpty(jobId))
                return;

            lock (Sync)
            {
                EnsureLoaded();
                string owner = NormalizeOwner(ownerAgentId);
                string key = GetAccessKey(jobType, jobId);
                Dictionary<string, object> existing = entries.FirstOrDefault(item =>
                    GetString(item, "jobType") == jobType && GetString(item, "jobId") == jobId);
                string accessToken;
                if (existing != null)
                {
                    RequireOwner(existing, owner, jobType, jobId);
                    accessToken = GetString(existing, JobAccessTokenKey);
                    if (string.IsNullOrEmpty(accessToken))
                    {
                        accessToken = Guid.NewGuid().ToString("N");
                        var replacement = new Dictionary<string, object>(existing)
                        {
                            [JobAccessTokenKey] = accessToken,
                        };
                        int existingIndex = entries.IndexOf(existing);
                        entries[existingIndex] = replacement;
                        PublishCurrentEntries();
                        Save();
                    }
                }
                else if (PendingAccessTokens.TryGetValue(key, out PendingJobAccess pending))
                {
                    if (!string.Equals(pending.OwnerAgentId, owner, StringComparison.Ordinal))
                        throw BuildOwnerChangeException(jobType, jobId, pending.OwnerAgentId, owner);
                    accessToken = pending.AccessToken;
                }
                else
                {
                    accessToken = Guid.NewGuid().ToString("N");
                    PendingAccessTokens[key] = new PendingJobAccess(owner, accessToken);
                }

                response[JobAccessTokenKey] = accessToken;
            }
        }

        public static bool CanAccess(string jobType, string jobId, string ownerAgentId,
            Dictionary<string, object> args)
        {
            string owner = NormalizeOwner(ownerAgentId);
            if (string.Equals(GetString(args, "_agentId", "anonymous"), owner,
                    StringComparison.Ordinal))
                return true;

            lock (Sync)
            {
                EnsureLoaded();
                Dictionary<string, object> existing = entries.FirstOrDefault(item =>
                    GetString(item, "jobType") == jobType && GetString(item, "jobId") == jobId);
                return existing != null &&
                       string.Equals(GetString(existing, "ownerAgentId", "anonymous"), owner,
                           StringComparison.Ordinal) &&
                       HasAccessToken(existing, args);
            }
        }

        public static object List(Dictionary<string, object> args)
        {
            lock (Sync)
            {
                EnsureLoaded();
                string agentId = GetString(args, "_agentId", "anonymous");
                string jobType = GetString(args, "jobType");
                string status = GetString(args, "status");
                int offset = Math.Max(0, GetInt(args, "offset", 0));
                int limit = Math.Max(1, Math.Min(200, GetInt(args, "limit", 50)));
                var filtered = entries.Where(item =>
                        GetString(item, "ownerAgentId", "anonymous") == agentId &&
                        (string.IsNullOrEmpty(jobType) || GetString(item, "jobType") == jobType) &&
                        (string.IsNullOrEmpty(status) || GetString(item, "status") == status))
                    .OrderByDescending(item => ParseDate(GetString(item, "updatedAt"))).ToList();
                var page = filtered.Skip(offset).Take(limit)
                    .Select(CreatePublicEntry).ToList();
                return new Dictionary<string, object>
                {
                    { "success", true }, { "ownerAgentId", agentId }, { "total", filtered.Count },
                    { "offset", offset }, { "limit", limit },
                    { "hasMore", offset + page.Count < filtered.Count },
                    { "nextOffset", offset + page.Count < filtered.Count ? (object)(offset + page.Count) : null },
                    { "jobs", page },
                };
            }
        }

        public static object Get(Dictionary<string, object> args)
        {
            lock (Sync)
            {
                EnsureLoaded();
                string agentId = GetString(args, "_agentId", "anonymous");
                string jobType = GetString(args, "jobType");
                string jobId = GetString(args, "jobId");
                if (string.IsNullOrEmpty(jobId))
                    return MCPResponse.Error("jobId is required.", "invalid_arguments");
                var match = entries.FirstOrDefault(item => GetString(item, "jobId") == jobId &&
                    (string.IsNullOrEmpty(jobType) || GetString(item, "jobType") == jobType));
                if (match == null) return MCPResponse.Error($"Job '{jobId}' was not found.", "job_not_found");
                if (GetString(match, "ownerAgentId", "anonymous") != agentId &&
                    !HasAccessToken(match, args))
                    return MCPResponse.Error(
                        "Job belongs to another agent and the jobAccessToken was not supplied.",
                        "job_owner_mismatch");
                return new Dictionary<string, object>
                {
                    { "success", true }, { "job", CreatePublicEntry(match) },
                };
            }
        }

        internal static object GetPublishedSnapshot(Dictionary<string, object> args)
        {
            PublishedState state = Volatile.Read(ref publishedState);
            if (state == null)
            {
                lock (Sync)
                {
                    EnsureLoaded();
                    state = Volatile.Read(ref publishedState);
                }
            }

            string jobId = GetString(args, "jobId");
            string requestId = GetString(args, "requestId");
            if (string.IsNullOrWhiteSpace(jobId) &&
                string.IsNullOrWhiteSpace(requestId))
            {
                return MCPResponse.Error(
                    "jobs/get requires jobId or requestId.", "invalid_arguments");
            }

            Dictionary<string, object> identified = FindPublishedEntry(
                state, jobId, requestId);
            return BuildPublishedSnapshot(args, identified, jobId, requestId);
        }

        private static Dictionary<string, object> FindPublishedEntry(
            PublishedState state, string jobId, string requestId)
        {
            if (!string.IsNullOrWhiteSpace(jobId))
                return state.ByJobId.TryGetValue(jobId, out Dictionary<string, object> byJob)
                    ? byJob
                    : null;
            return !string.IsNullOrWhiteSpace(requestId) &&
                   state.ByRequestId.TryGetValue(requestId,
                       out Dictionary<string, object> byRequest)
                ? byRequest
                : null;
        }

        private static object BuildPublishedSnapshot(Dictionary<string, object> args,
            Dictionary<string, object> identified, string jobId, string requestId)
        {
            if (identified == null)
            {
                string identity = !string.IsNullOrWhiteSpace(jobId) ? jobId : requestId;
                return MCPResponse.Error(
                    $"Job '{identity}' was not found.", "job_not_found");
            }

            string requestedJobType = GetString(args, "jobType");
            string actualJobType = GetString(identified, "jobType");
            if (!string.IsNullOrWhiteSpace(requestedJobType) &&
                !string.Equals(requestedJobType, actualJobType,
                    StringComparison.Ordinal))
            {
                return MCPResponse.Error(
                    $"Job '{GetString(identified, "jobId")}' is owned by job type " +
                    $"'{actualJobType}', not '{requestedJobType}'.",
                    "job_type_mismatch");
            }
            if (!string.IsNullOrWhiteSpace(requestId) &&
                !MCPWorkspaceJobRunner.OwnsJobType(actualJobType))
            {
                return MCPResponse.Error(
                    $"Job type '{actualJobType}' cannot be resolved by requestId.",
                    "invalid_arguments");
            }
            string agentId = GetString(args, "_agentId", "anonymous");
            if (GetString(identified, "ownerAgentId", "anonymous") != agentId &&
                !HasAccessToken(identified, args))
            {
                return MCPResponse.Error(
                    "Job belongs to another agent and the jobAccessToken was not supplied.",
                    "job_owner_mismatch");
            }

            object published = identified.TryGetValue("snapshot", out object snapshot)
                ? snapshot
                : CreatePublicEntry(identified);
            return CloneJsonValue(published);
        }

        private static string ResolveAccessToken(string jobType, string jobId, string owner,
            Dictionary<string, object> existing, string ownerAccessToken = "")
        {
            if (existing != null)
            {
                RequireOwner(existing, owner, jobType, jobId);
                if (!string.IsNullOrEmpty(ownerAccessToken))
                    return ownerAccessToken;
                string persisted = GetString(existing, JobAccessTokenKey);
                if (!string.IsNullOrEmpty(persisted))
                    return persisted;
            }

            string key = GetAccessKey(jobType, jobId);
            if (PendingAccessTokens.TryGetValue(key, out PendingJobAccess pending))
            {
                if (!string.Equals(pending.OwnerAgentId, owner, StringComparison.Ordinal))
                    throw BuildOwnerChangeException(jobType, jobId, pending.OwnerAgentId, owner);
                if (!string.IsNullOrEmpty(ownerAccessToken))
                    return ownerAccessToken;
                return pending.AccessToken;
            }
            if (!string.IsNullOrEmpty(ownerAccessToken))
                return ownerAccessToken;
            return Guid.NewGuid().ToString("N");
        }

        private static void RequireOwner(Dictionary<string, object> entry, string owner,
            string jobType, string jobId)
        {
            string existingOwner = GetString(entry, "ownerAgentId", "anonymous");
            if (!string.Equals(existingOwner, owner, StringComparison.Ordinal))
                throw BuildOwnerChangeException(jobType, jobId, existingOwner, owner);
        }

        private static InvalidOperationException BuildOwnerChangeException(string jobType,
            string jobId, string existingOwner, string requestedOwner)
        {
            return new InvalidOperationException(
                $"Persistent job '{jobType}/{jobId}' cannot change owner from " +
                $"'{existingOwner}' to '{requestedOwner}'.");
        }

        private static bool HasAccessToken(Dictionary<string, object> entry,
            Dictionary<string, object> args)
        {
            string requested = GetString(args, JobAccessTokenKey);
            return !string.IsNullOrEmpty(requested) &&
                   string.Equals(requested, GetString(entry, JobAccessTokenKey),
                       StringComparison.Ordinal);
        }

        private static object RemoveAccessToken(object snapshot)
        {
            if (!(snapshot is Dictionary<string, object> source))
                return snapshot;
            var sanitized = new Dictionary<string, object>(source);
            sanitized.Remove(JobAccessTokenKey);
            return sanitized;
        }

        private static Dictionary<string, object> CreatePublicEntry(
            Dictionary<string, object> entry)
        {
            var result = new Dictionary<string, object>(entry);
            result.Remove(JobAccessTokenKey);
            result.Remove("requestId");
            return result;
        }

        private static object CloneJsonValue(object value)
        {
            return MiniJson.Deserialize(MiniJson.Serialize(value));
        }

        private static string GetAccessKey(string jobType, string jobId)
        {
            return jobType + "\n" + jobId;
        }

        private static string NormalizeOwner(string ownerAgentId)
        {
            return string.IsNullOrEmpty(ownerAgentId) ? "anonymous" : ownerAgentId;
        }

        private static object BoundSnapshot(object snapshot, string status)
        {
            if (snapshot == null) return new Dictionary<string, object> { { "status", status ?? "unknown" } };
            string json = MiniJson.Serialize(snapshot);
            if (json.Length <= MaxSnapshotCharacters) return MiniJson.Deserialize(json);
            return new Dictionary<string, object>
            {
                { "status", status ?? "unknown" }, { "snapshotTruncated", true },
                { "originalCharacterCount", json.Length },
            };
        }

        private static void EnsureLoaded()
        {
            if (entries != null) return;
            string path = GetPath();
            if (!MCPPersistenceFile.TryReadAllText(path, out string contents))
            {
                entries = new List<Dictionary<string, object>>();
                PublishCurrentEntries();
                return;
            }
            if (!(MiniJson.Deserialize(contents) is IList list))
                throw new InvalidDataException(
                    $"MCP Job history '{path}' does not contain a JSON array.");
            List<Dictionary<string, object>> parsed = list.Cast<object>()
                .Select(MCPResponse.ToDictionary).ToList();
            if (parsed.Any(item => item == null))
                throw new InvalidDataException(
                    $"MCP Job history '{path}' contains a non-object entry.");
            // Record() applies the configured retention bound on the main thread.
            // Reads may arrive on the bridge worker during a compile or Domain Reload
            // and therefore must not touch EditorPrefs or another Unity API.
            entries = parsed;
            PublishCurrentEntries();
        }

        private static void PublishCurrentEntries()
        {
            var byJobId = new Dictionary<string, Dictionary<string, object>>(
                entries.Count, StringComparer.Ordinal);
            var byRequestId = new Dictionary<string, Dictionary<string, object>>(
                entries.Count, StringComparer.Ordinal);
            foreach (Dictionary<string, object> entry in entries)
            {
                string jobId = GetString(entry, "jobId");
                if (!string.IsNullOrWhiteSpace(jobId) && !byJobId.ContainsKey(jobId))
                    byJobId.Add(jobId, entry);
                string requestId = GetString(entry, "requestId");
                if (!string.IsNullOrWhiteSpace(requestId) &&
                    !byRequestId.ContainsKey(requestId))
                {
                    byRequestId.Add(requestId, entry);
                }
            }
            Volatile.Write(ref publishedState, new PublishedState(byJobId, byRequestId));
        }

        private static void Save()
        {
            MCPPersistenceFile.WriteAllText(GetPath(), MiniJson.Serialize(entries));
        }

        private static string GetPath()
        {
            return Path.Combine(Directory.GetCurrentDirectory(), "Library", "VMUnityAutomation", "job-history-v1.json");
        }

        private static DateTime ParseDate(string value)
        {
            return DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind,
                out DateTime parsed) ? parsed : DateTime.MinValue;
        }

        private static string GetString(Dictionary<string, object> args, string key, string defaultValue = "")
        {
            return args != null && args.TryGetValue(key, out object value) && value != null
                ? value.ToString()
                : defaultValue;
        }

        private static int GetInt(Dictionary<string, object> args, string key, int defaultValue)
        {
            return args != null && args.TryGetValue(key, out object value) && value != null &&
                   int.TryParse(value.ToString(), out int parsed) ? parsed : defaultValue;
        }

        private sealed class PendingJobAccess
        {
            public PendingJobAccess(string ownerAgentId, string accessToken)
            {
                OwnerAgentId = ownerAgentId;
                AccessToken = accessToken;
            }

            public string OwnerAgentId { get; }
            public string AccessToken { get; }
        }

        private sealed class PublishedState
        {
            internal PublishedState(
                Dictionary<string, Dictionary<string, object>> byJobId,
                Dictionary<string, Dictionary<string, object>> byRequestId)
            {
                ByJobId = byJobId;
                ByRequestId = byRequestId;
            }

            internal Dictionary<string, Dictionary<string, object>> ByJobId { get; }
            internal Dictionary<string, Dictionary<string, object>> ByRequestId { get; }
        }
    }
}
