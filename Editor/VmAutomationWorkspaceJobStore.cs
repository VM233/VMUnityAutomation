using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace VMUnityAutomation.Editor
{
    internal static class VmAutomationWorkspaceJobStore
    {
        private const int MaximumPersistedJobs = 200;
        private static readonly object Sync = new();
        private static readonly List<VmAutomationWorkspaceJob> Jobs = new();
        private static VmAutomationJobRecordStore recordStore;
        private static bool loaded;

        internal static IReadOnlyList<VmAutomationWorkspaceJob> GetAll()
        {
            EnsureLoaded();
            lock (Sync)
                return Jobs.ToList();
        }

        internal static VmAutomationWorkspaceJob Find(string jobId)
        {
            EnsureLoaded();
            lock (Sync)
                return Jobs.FirstOrDefault(job => job.JobId == jobId);
        }

        internal static VmAutomationWorkspaceJob FindByRequestId(string requestId, string jobType)
        {
            EnsureLoaded();
            lock (Sync)
            {
                VmAutomationWorkspaceJob latest = null;
                foreach (VmAutomationWorkspaceJob job in Jobs)
                    if (job.RequestId == requestId &&
                        (string.IsNullOrEmpty(jobType) || job.JobType == jobType) &&
                        (latest == null || job.CreatedAt > latest.CreatedAt))
                        latest = job;
                return latest;
            }
        }

        internal static VmAutomationWorkspaceJob FindByIdempotencyKey(
            string operation, string idempotencyKey)
        {
            EnsureLoaded();
            lock (Sync)
            {
                VmAutomationWorkspaceJob latest = null;
                foreach (VmAutomationWorkspaceJob job in Jobs)
                    if (job.Operation == operation && job.IdempotencyKey == idempotencyKey &&
                        (latest == null || job.CreatedAt > latest.CreatedAt))
                        latest = job;
                return latest;
            }
        }

        internal static void Add(VmAutomationWorkspaceJob job)
        {
            if (job == null)
                throw new ArgumentNullException(nameof(job));

            EnsureLoaded();
            lock (Sync)
            {
                List<VmAutomationWorkspaceJob> previous = Jobs.ToList();
                try
                {
                    Jobs.Add(job);
                    Prune();
                    SaveWithoutLock(job);
                }
                catch
                {
                    Jobs.Clear();
                    Jobs.AddRange(previous);
                    throw;
                }
            }
        }

        internal static void Save(VmAutomationWorkspaceJob job)
        {
            if (job == null)
                throw new ArgumentNullException(nameof(job));

            EnsureLoaded();
            lock (Sync)
            {
                if (!Jobs.Contains(job))
                    throw new InvalidOperationException(
                        $"Workspace job '{job.JobId}' is not owned by this store.");
                SaveWithoutLock(job);
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
                recordStore = new VmAutomationJobRecordStore(GetPath());
                foreach (Dictionary<string, object> values in recordStore.Load())
                    Jobs.Add(VmAutomationWorkspaceJob.FromDictionary(values));
                VmAutomationPersistenceFile.DeleteIfExists(GetPath() + ".bak");

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

        private static void SaveWithoutLock(VmAutomationWorkspaceJob changed)
        {
            var membership = Jobs.Select(job => new Dictionary<string, object>
            {
                { "jobType", job.JobType }, { "jobId", job.JobId },
            }).ToList();
            recordStore.PublishChanged(membership, changed.ToDictionary());
        }

        private static string GetPath()
        {
            return Path.Combine(Directory.GetCurrentDirectory(), "Library", "VMUnityAutomation",
                "workspace-jobs-v1.json");
        }
    }
}
