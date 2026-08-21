using System;
using System.Collections.Generic;
using System.IO;

namespace VMUnityAutomation.Editor
{
    /// <summary>
    /// Publishes a durable acknowledgement that an authorized client has received a
    /// workspace Job token and started polling it. The background-safe status reader
    /// writes one immutable marker; the main-thread Job runner adopts and removes it
    /// before crossing any mutation or Domain Reload boundary.
    /// </summary>
    internal static class VmAutomationWorkspaceJobAdoptionStore
    {
        internal static void PublishFromSnapshot(object snapshot)
        {
            Dictionary<string, object> values =
                VmAutomationResponse.ToDictionary(snapshot);
            if (values == null || !GetBool(values, "success") ||
                !string.Equals(GetString(values, "status"), "queued",
                    StringComparison.Ordinal))
            {
                return;
            }

            string jobType = GetString(values, "jobType");
            if (!VmAutomationWorkspaceJobRunner.OwnsJobType(jobType))
                return;

            Publish(GetString(values, "jobId"));
        }

        internal static void Publish(string jobId)
        {
            string path = GetMarkerPath(jobId);
            VmAutomationPersistenceFile.WriteAllText(path, jobId);
        }

        internal static bool IsPublished(string jobId)
        {
            string path = GetMarkerPath(jobId);
            return VmAutomationPersistenceFile.TryReadAllText(path, out string contents) &&
                   string.Equals(contents, jobId, StringComparison.Ordinal);
        }

        internal static void Delete(string jobId)
        {
            VmAutomationPersistenceFile.DeleteIfExists(GetMarkerPath(jobId));
        }

        private static string GetMarkerPath(string jobId)
        {
            if (!Guid.TryParseExact(jobId, "N", out _))
                throw new ArgumentException(
                    "A workspace Job adoption marker requires a canonical Job ID.",
                    nameof(jobId));

            return Path.Combine(Directory.GetCurrentDirectory(), "Library",
                "VMUnityAutomation", "workspace-job-adoptions", jobId + ".txt");
        }

        private static string GetString(
            IReadOnlyDictionary<string, object> values, string key)
        {
            return values.TryGetValue(key, out object value) && value != null
                ? value.ToString()
                : "";
        }

        private static bool GetBool(
            IReadOnlyDictionary<string, object> values, string key)
        {
            if (!values.TryGetValue(key, out object value) || value == null)
                return false;
            return value is bool result
                ? result
                : bool.TryParse(value.ToString(), out result) && result;
        }
    }
}
