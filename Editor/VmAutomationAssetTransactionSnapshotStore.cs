using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;

namespace VMUnityAutomation.Editor
{
    /// <summary>
    /// Owns durable asset-transaction snapshots, per-file atomic restoration, and byte-level
    /// commit/rollback evidence. All artifacts live under one job-owned Library directory.
    /// </summary>
    internal static class VmAutomationAssetTransactionSnapshotStore
    {
        private const string PreparingMarker = ".preparing";
        private const string ReadyMarker = ".ready";

        internal static void CaptureBaseline(string jobId, VmAutomationAssetTransactionPlan plan,
            Dictionary<string, object> state)
        {
            string root = GetJobRoot(jobId);
            if (Directory.Exists(root))
            {
                throw new InvalidOperationException(
                    $"Asset transaction snapshot directory already exists for job '{jobId}'.");
            }

            Directory.CreateDirectory(root);
            VmAutomationPersistenceFile.WriteAllText(Path.Combine(root, PreparingMarker), jobId);
            try
            {
                var assetSnapshots = new List<object>();
                for (int index = 0; index < plan.TouchedAssetPaths.Count; index++)
                {
                    string assetPath = plan.TouchedAssetPaths[index];
                    string absolutePath = ToAbsoluteAssetPath(assetPath);
                    bool exists = File.Exists(absolutePath);
                    bool metaExists = File.Exists(absolutePath + ".meta");
                    var snapshot = new Dictionary<string, object>
                    {
                        { "kind", "asset" },
                        { "path", assetPath },
                        { "exists", exists },
                        { "metaExists", metaExists },
                    };
                    if (exists)
                    {
                        byte[] bytes = VmAutomationPersistenceFile.ReadAllBytes(absolutePath);
                        string blobName = $"{index:D4}.asset";
                        VmAutomationPersistenceFile.WriteAllBytes(Path.Combine(root, blobName), bytes);
                        snapshot["assetBlob"] = blobName;
                        snapshot["assetSha256"] = Hash(bytes);
                    }
                    if (metaExists)
                    {
                        byte[] bytes = VmAutomationPersistenceFile.ReadAllBytes(absolutePath + ".meta");
                        string blobName = $"{index:D4}.meta";
                        VmAutomationPersistenceFile.WriteAllBytes(Path.Combine(root, blobName), bytes);
                        snapshot["metaBlob"] = blobName;
                        snapshot["metaSha256"] = Hash(bytes);
                    }
                    assetSnapshots.Add(snapshot);
                }

                var folderSnapshots = plan.CandidateFolders.Select(path => (object)
                    new Dictionary<string, object>
                    {
                        { "kind", "folder" },
                        { "path", path },
                        { "exists", Directory.Exists(ToAbsoluteAssetPath(path)) },
                    }).ToList();

                state["assetSnapshots"] = assetSnapshots;
                state["folderSnapshots"] = folderSnapshots;
                state["baselineEvidence"] = BuildBaselineEvidence(state);
                VmAutomationPersistenceFile.WriteAllText(Path.Combine(root, ReadyMarker), jobId);
                VerifySnapshotArtifacts(jobId, state);
                VmAutomationPersistenceFile.DeleteIfExists(Path.Combine(root, PreparingMarker));
            }
            catch
            {
                Cleanup(jobId);
                throw;
            }
        }

        internal static void VerifySnapshotArtifacts(string jobId,
            Dictionary<string, object> state)
        {
            string root = GetJobRoot(jobId);
            string readyPath = Path.Combine(root, ReadyMarker);
            if (!VmAutomationPersistenceFile.TryReadAllText(readyPath, out string marker) ||
                !string.Equals(marker, jobId, StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"Asset transaction '{jobId}' has no valid prepared snapshot marker.");
            }

            foreach (Dictionary<string, object> snapshot in GetDictionaryList(state,
                         "assetSnapshots"))
            {
                VerifyBlob(root, snapshot, "assetBlob", "assetSha256");
                VerifyBlob(root, snapshot, "metaBlob", "metaSha256");
            }
        }

        internal static List<object> CaptureCurrentEvidence(Dictionary<string, object> state)
        {
            var evidence = new List<object>();
            foreach (string assetPath in GetStringList(state, "touchedAssetPaths"))
                evidence.Add(CaptureAssetEvidence(assetPath));
            foreach (string folderPath in GetStringList(state, "candidateFolders"))
            {
                evidence.Add(new Dictionary<string, object>
                {
                    { "kind", "folder" },
                    { "path", folderPath },
                    { "exists", Directory.Exists(ToAbsoluteAssetPath(folderPath)) },
                });
            }
            return evidence;
        }

        internal static List<object> BuildBaselineEvidence(Dictionary<string, object> state)
        {
            var evidence = new List<object>();
            foreach (Dictionary<string, object> snapshot in GetDictionaryList(state,
                         "assetSnapshots"))
            {
                evidence.Add(new Dictionary<string, object>
                {
                    { "kind", "asset" },
                    { "path", GetString(snapshot, "path") },
                    { "exists", GetBool(snapshot, "exists") },
                    { "assetSha256", GetString(snapshot, "assetSha256") },
                    { "metaExists", GetBool(snapshot, "metaExists") },
                    { "metaSha256", GetString(snapshot, "metaSha256") },
                });
            }
            foreach (Dictionary<string, object> snapshot in GetDictionaryList(state,
                         "folderSnapshots"))
            {
                evidence.Add(new Dictionary<string, object>
                {
                    { "kind", "folder" },
                    { "path", GetString(snapshot, "path") },
                    { "exists", GetBool(snapshot, "exists") },
                });
            }
            return evidence;
        }

        internal static bool VerifyEvidence(object expectedValue,
            out List<string> differences)
        {
            differences = new List<string>();
            if (!(expectedValue is IList expectedList))
            {
                differences.Add("Expected transaction evidence is not an array.");
                return false;
            }

            foreach (object value in expectedList)
            {
                Dictionary<string, object> expected = VmAutomationResponse.ToDictionary(value);
                if (expected == null)
                {
                    differences.Add("Expected transaction evidence contains a non-object entry.");
                    continue;
                }

                string kind = GetString(expected, "kind");
                string path = GetString(expected, "path");
                Dictionary<string, object> actual = kind == "folder"
                    ? new Dictionary<string, object>
                    {
                        { "kind", "folder" },
                        { "path", path },
                        { "exists", Directory.Exists(ToAbsoluteAssetPath(path)) },
                    }
                    : CaptureAssetEvidence(path);
                CompareEvidence(expected, actual, differences);
            }
            return differences.Count == 0;
        }

        internal static List<string> RestoreAndVerify(string jobId,
            Dictionary<string, object> state)
        {
            var errors = new List<string>();
            string root = GetJobRoot(jobId);
            try
            {
                VerifySnapshotArtifacts(jobId, state);
            }
            catch (Exception exception)
            {
                errors.Add(exception.GetBaseException().Message);
                return errors;
            }

            foreach (Dictionary<string, object> snapshot in GetDictionaryList(state,
                         "assetSnapshots"))
            {
                string assetPath = GetString(snapshot, "path");
                string absolutePath = ToAbsoluteAssetPath(assetPath);
                try
                {
                    if (GetBool(snapshot, "exists"))
                    {
                        byte[] bytes = ReadVerifiedBlob(root, snapshot,
                            "assetBlob", "assetSha256");
                        VmAutomationPersistenceFile.WriteAllBytes(absolutePath, bytes);
                    }
                    else
                    {
                        VmAutomationPersistenceFile.DeleteIfExists(absolutePath);
                    }

                    if (GetBool(snapshot, "metaExists"))
                    {
                        byte[] bytes = ReadVerifiedBlob(root, snapshot,
                            "metaBlob", "metaSha256");
                        VmAutomationPersistenceFile.WriteAllBytes(absolutePath + ".meta", bytes);
                    }
                    else
                    {
                        VmAutomationPersistenceFile.DeleteIfExists(absolutePath + ".meta");
                    }
                }
                catch (Exception exception)
                {
                    errors.Add($"Failed to restore '{assetPath}': " +
                               exception.GetBaseException().Message);
                }
            }

            foreach (Dictionary<string, object> snapshot in GetDictionaryList(state,
                         "assetSnapshots").Where(snapshot => GetBool(snapshot, "exists")))
            {
                string assetPath = GetString(snapshot, "path");
                try
                {
                    AssetDatabase.ImportAsset(assetPath,
                        ImportAssetOptions.ForceSynchronousImport |
                        ImportAssetOptions.ForceUpdate);
                }
                catch (Exception exception)
                {
                    errors.Add($"Failed to import restored asset '{assetPath}': " +
                               exception.GetBaseException().Message);
                }
            }

            foreach (Dictionary<string, object> snapshot in GetDictionaryList(state,
                         "folderSnapshots")
                         .Where(snapshot => !GetBool(snapshot, "exists"))
                         .OrderByDescending(snapshot =>
                             GetString(snapshot, "path").Count(character => character == '/')))
            {
                string folderPath = GetString(snapshot, "path");
                string absolutePath = ToAbsoluteAssetPath(folderPath);
                try
                {
                    if (Directory.Exists(absolutePath))
                    {
                        string[] entries = Directory.GetFileSystemEntries(absolutePath);
                        if (entries.Length > 0)
                        {
                            errors.Add(
                                $"Created folder '{folderPath}' is not empty after asset restoration.");
                            continue;
                        }
                        Directory.Delete(absolutePath, false);
                    }
                    VmAutomationPersistenceFile.DeleteIfExists(absolutePath + ".meta");
                }
                catch (Exception exception)
                {
                    errors.Add($"Failed to remove created folder '{folderPath}': " +
                               exception.GetBaseException().Message);
                }
            }

            try
            {
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport |
                                      ImportAssetOptions.ForceUpdate);
            }
            catch (Exception exception)
            {
                errors.Add("Failed to refresh restored assets: " +
                           exception.GetBaseException().Message);
            }

            if (!VerifyEvidence(state.TryGetValue("baselineEvidence", out object baseline)
                    ? baseline
                    : null, out List<string> differences))
                errors.AddRange(differences.Select(difference => "Rollback readback: " + difference));
            return errors;
        }

        internal static void Cleanup(string jobId)
        {
            string root = GetJobRoot(jobId);
            if (!Directory.Exists(root)) return;
            Directory.Delete(root, true);
        }

        internal static void CleanupOrphanPreparingDirectories(
            IEnumerable<string> retainedJobIds)
        {
            string root = GetRoot();
            if (!Directory.Exists(root)) return;
            var retained = new HashSet<string>(retainedJobIds ?? Array.Empty<string>(),
                StringComparer.Ordinal);
            foreach (string directory in Directory.GetDirectories(root))
            {
                string jobId = Path.GetFileName(directory);
                if (!IsJobId(jobId) || retained.Contains(jobId) ||
                    !File.Exists(Path.Combine(directory, PreparingMarker)) ||
                    File.Exists(Path.Combine(directory, ReadyMarker)))
                    continue;
                Directory.Delete(directory, true);
            }
        }

        internal static bool SnapshotDirectoryExists(string jobId)
        {
            return Directory.Exists(GetJobRoot(jobId));
        }

        private static Dictionary<string, object> CaptureAssetEvidence(string assetPath)
        {
            string absolutePath = ToAbsoluteAssetPath(assetPath);
            bool exists = File.Exists(absolutePath);
            bool metaExists = File.Exists(absolutePath + ".meta");
            return new Dictionary<string, object>
            {
                { "kind", "asset" },
                { "path", assetPath },
                { "exists", exists },
                { "assetSha256", exists ? Hash(VmAutomationPersistenceFile.ReadAllBytes(absolutePath)) : "" },
                { "metaExists", metaExists },
                { "metaSha256", metaExists ? Hash(VmAutomationPersistenceFile.ReadAllBytes(
                    absolutePath + ".meta")) : "" },
            };
        }

        private static void CompareEvidence(Dictionary<string, object> expected,
            Dictionary<string, object> actual, List<string> differences)
        {
            string path = GetString(expected, "path");
            foreach (string key in GetString(expected, "kind") == "folder"
                         ? new[] { "exists" }
                         : new[] { "exists", "assetSha256", "metaExists", "metaSha256" })
            {
                string expectedValue = expected.TryGetValue(key, out object left) && left != null
                    ? left.ToString()
                    : "";
                string actualValue = actual.TryGetValue(key, out object right) && right != null
                    ? right.ToString()
                    : "";
                if (!string.Equals(expectedValue, actualValue, StringComparison.OrdinalIgnoreCase))
                {
                    differences.Add(
                        $"'{path}' field '{key}' expected '{expectedValue}' but was '{actualValue}'.");
                }
            }
        }

        private static void VerifyBlob(string root, Dictionary<string, object> snapshot,
            string blobKey, string hashKey)
        {
            string blobName = GetString(snapshot, blobKey);
            if (string.IsNullOrEmpty(blobName)) return;
            byte[] bytes = VmAutomationPersistenceFile.ReadAllBytes(Path.Combine(root, blobName));
            string expectedHash = GetString(snapshot, hashKey);
            string actualHash = Hash(bytes);
            if (!string.Equals(expectedHash, actualHash, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"Snapshot blob '{blobName}' failed SHA-256 verification.");
            }
        }

        private static byte[] ReadVerifiedBlob(string root,
            Dictionary<string, object> snapshot, string blobKey, string hashKey)
        {
            string blobName = GetString(snapshot, blobKey);
            if (string.IsNullOrEmpty(blobName))
                throw new InvalidDataException($"Snapshot is missing '{blobKey}'.");
            byte[] bytes = VmAutomationPersistenceFile.ReadAllBytes(Path.Combine(root, blobName));
            if (!string.Equals(Hash(bytes), GetString(snapshot, hashKey),
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"Snapshot blob '{blobName}' failed SHA-256 verification.");
            }
            return bytes;
        }

        private static string Hash(byte[] bytes)
        {
            using SHA256 hash = SHA256.Create();
            return BitConverter.ToString(hash.ComputeHash(bytes ?? Array.Empty<byte>()))
                .Replace("-", "").ToLowerInvariant();
        }

        private static string ToAbsoluteAssetPath(string assetPath)
        {
            string assetsRoot = Path.GetFullPath(UnityEngine.Application.dataPath);
            string normalized = VmAutomationAssetTransactionPlan.NormalizeAssetPath(assetPath);
            if (normalized != "Assets" &&
                !normalized.StartsWith("Assets/", StringComparison.Ordinal))
                throw new InvalidDataException($"Asset path is outside Assets: '{assetPath}'.");
            string relative = normalized == "Assets"
                ? ""
                : normalized.Substring("Assets/".Length);
            string absolute = Path.GetFullPath(Path.Combine(assetsRoot,
                relative.Replace('/', Path.DirectorySeparatorChar)));
            if (absolute != assetsRoot && !absolute.StartsWith(
                    assetsRoot + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"Asset path escapes Assets: '{assetPath}'.");
            return absolute;
        }

        private static string GetRoot()
        {
            string projectRoot = Directory.GetParent(UnityEngine.Application.dataPath).FullName;
            return Path.GetFullPath(Path.Combine(projectRoot, "Library", "VMUnityAutomation",
                "asset-transactions"));
        }

        private static string GetJobRoot(string jobId)
        {
            if (!IsJobId(jobId))
                throw new ArgumentException("A 32-character hexadecimal job id is required.",
                    nameof(jobId));
            string root = GetRoot();
            string path = Path.GetFullPath(Path.Combine(root, jobId));
            if (!path.StartsWith(root + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Transaction snapshot path escaped its root.");
            return path;
        }

        private static bool IsJobId(string value)
        {
            return value != null && value.Length == 32 &&
                   value.All(character => character >= '0' && character <= '9' ||
                                          character >= 'a' && character <= 'f' ||
                                          character >= 'A' && character <= 'F');
        }

        private static string GetString(Dictionary<string, object> values, string key)
        {
            return VmAutomationAssetTransactionPlan.GetString(values, key);
        }

        private static bool GetBool(Dictionary<string, object> values, string key)
        {
            return VmAutomationAssetTransactionPlan.GetBool(values, key);
        }

        private static List<string> GetStringList(Dictionary<string, object> values, string key)
        {
            return VmAutomationAssetTransactionPlan.GetStringList(values, key);
        }

        private static List<Dictionary<string, object>> GetDictionaryList(
            Dictionary<string, object> values, string key)
        {
            return VmAutomationAssetTransactionPlan.GetDictionaryList(values, key);
        }
    }
}
