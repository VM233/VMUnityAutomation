using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace VMUnityAutomation.Editor
{
    internal static class MCPWorkspaceJobStore
    {
        private const int MaximumPersistedJobs = 200;
        private static readonly object Sync = new();
        private static readonly List<MCPWorkspaceJob> Jobs = new();
        private static bool loaded;

        internal static IReadOnlyList<MCPWorkspaceJob> GetAll()
        {
            EnsureLoaded();
            lock (Sync)
                return Jobs.ToList();
        }

        internal static MCPWorkspaceJob Find(string jobId)
        {
            EnsureLoaded();
            lock (Sync)
                return Jobs.FirstOrDefault(job => job.JobId == jobId);
        }

        internal static MCPWorkspaceJob FindByRequestId(string requestId, string jobType)
        {
            EnsureLoaded();
            lock (Sync)
            {
                return Jobs.OrderByDescending(job => job.CreatedAt).FirstOrDefault(job =>
                    job.RequestId == requestId &&
                    (string.IsNullOrEmpty(jobType) || job.JobType == jobType));
            }
        }

        internal static MCPWorkspaceJob FindByIdempotencyKey(
            string operation, string idempotencyKey)
        {
            EnsureLoaded();
            lock (Sync)
            {
                return Jobs.OrderByDescending(job => job.CreatedAt).FirstOrDefault(job =>
                    job.Operation == operation && job.IdempotencyKey == idempotencyKey);
            }
        }

        internal static void Add(MCPWorkspaceJob job)
        {
            if (job == null)
                throw new ArgumentNullException(nameof(job));

            EnsureLoaded();
            lock (Sync)
            {
                List<MCPWorkspaceJob> previous = Jobs.ToList();
                try
                {
                    Jobs.Add(job);
                    Prune();
                    SaveWithoutLock();
                }
                catch
                {
                    Jobs.Clear();
                    Jobs.AddRange(previous);
                    throw;
                }
            }
        }

        internal static void Save(MCPWorkspaceJob job)
        {
            if (job == null)
                throw new ArgumentNullException(nameof(job));

            EnsureLoaded();
            lock (Sync)
            {
                if (!Jobs.Contains(job))
                    throw new InvalidOperationException(
                        $"Workspace job '{job.JobId}' is not owned by this store.");
                SaveWithoutLock();
            }
        }

        internal static void EnsureLoaded()
        {
            if (loaded)
                return;

            lock (Sync)
            {
                if (loaded)
                    return;

                Jobs.Clear();
                string path = GetPath();
                if (MCPPersistenceFile.TryReadAllText(path, out string contents))
                {
                    if (!(MiniJson.Deserialize(contents) is IList persistedJobs))
                        throw new InvalidDataException(
                            $"Workspace job store '{path}' is not a JSON array.");
                    foreach (object value in persistedJobs)
                    {
                        Dictionary<string, object> values = MCPResponse.ToDictionary(value);
                        if (values == null)
                            throw new InvalidDataException(
                                $"Workspace job store '{path}' contains a non-object entry.");
                        Jobs.Add(MCPWorkspaceJob.FromDictionary(values));
                    }
                }

                loaded = true;
                Prune();
            }
        }

        private static void Prune()
        {
            if (Jobs.Count <= MaximumPersistedJobs)
                return;

            var protectedJobs = Jobs.Where(job => !job.IsTerminal ||
                                                  job.HasRetainedRecoveryArtifacts).ToList();
            var retained = protectedJobs
                .Concat(Jobs.Where(job => job.IsTerminal &&
                                          !job.HasRetainedRecoveryArtifacts)
                    .OrderByDescending(job => job.UpdatedAt)
                    .Take(Math.Max(0, MaximumPersistedJobs - protectedJobs.Count)))
                .Distinct().ToList();
            Jobs.Clear();
            Jobs.AddRange(retained);
        }

        private static void SaveWithoutLock()
        {
            MCPPersistenceFile.WriteAllText(
                GetPath(),
                MiniJson.Serialize(Jobs.Select(job => (object)job.ToDictionary()).ToList()),
                backupPath: GetPath() + ".bak");
        }

        private static string GetPath()
        {
            return Path.Combine(Directory.GetCurrentDirectory(), "Library", "VMUnityAutomation",
                "workspace-jobs-v1.json");
        }
    }
}
