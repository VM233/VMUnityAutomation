using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;

namespace VMUnityAutomation.Editor
{
    public static class VmAutomationAssetWorkspaceCommands
    {
        private const int DefaultPageSize = 100;
        private const int MaxPageSize = 500;

        public static object EnsureFolder(Dictionary<string, object> args)
        {
            string path = NormalizeAssetPath(GetString(args, "path"));
            bool dryRun = GetBool(args, "dryRun", false);
            if (!TryValidateFolderPath(path, out string error))
                return VmAutomationResponse.Error(error, "invalid_folder_path");

            bool existed = AssetDatabase.IsValidFolder(path);
            var created = new List<string>();
            if (!existed && !dryRun)
                EnsureFolderPath(path, created);

            return new Dictionary<string, object>
            {
                { "success", true },
                { "path", path },
                { "existed", existed },
                { "created", created },
                { "dryRun", dryRun },
            };
        }

        public static object Copy(Dictionary<string, object> args)
        {
            var requests = GetDictionaryList(args, "copies");
            if (requests.Count == 0)
                requests.Add(args ?? new Dictionary<string, object>());

            bool dryRun = GetBool(args, "dryRun", false);
            bool overwrite = GetBool(args, "overwrite", false);
            var prepared = new List<CopyRequest>();
            var targetPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var request in requests)
            {
                string source = NormalizeAssetPath(GetString(request, "sourcePath"));
                string target = NormalizeAssetPath(GetString(request, "targetPath"));
                if (string.IsNullOrEmpty(source) || AssetDatabase.LoadMainAssetAtPath(source) == null)
                    return VmAutomationResponse.Error($"Source asset was not found: '{source}'", "asset_not_found");
                if (!TryValidateAssetPath(target, out string targetError))
                    return VmAutomationResponse.Error(targetError, "invalid_asset_path");
                if (string.Equals(source, target, StringComparison.OrdinalIgnoreCase))
                    return VmAutomationResponse.Error("sourcePath and targetPath must be different.", "invalid_asset_path");
                if (!targetPaths.Add(target))
                    return VmAutomationResponse.Error($"Duplicate targetPath in copy batch: '{target}'.",
                        "duplicate_target_path");
                if (AssetDatabase.IsValidFolder(source))
                    return VmAutomationResponse.Error("Generic asset copy currently accepts files, not folders.", "folder_copy_not_supported");
                if (AssetDatabase.LoadMainAssetAtPath(target) != null && !overwrite)
                    return VmAutomationResponse.Error($"Target asset already exists: '{target}'", "asset_exists");
                prepared.Add(new CopyRequest { SourcePath = source, TargetPath = target });
            }

            if (dryRun)
                return BuildCopyResult(prepared, true, false, new List<string>());
            if (VmAutomationSceneCommands.TryRejectLoadedSceneAssetMutation(
                    prepared.Select(request => request.TargetPath), "copy or overwrite assets",
                    out object sceneMutationError))
                return sceneMutationError;

            var snapshots = new List<FileSnapshot>();
            var created = new List<string>();
            var errors = new List<string>();
            try
            {
                foreach (var request in prepared)
                {
                    EnsureParentFolder(request.TargetPath, created);
                    if (AssetDatabase.LoadMainAssetAtPath(request.TargetPath) != null)
                    {
                        snapshots.Add(CaptureFileSnapshot(request.TargetPath));
                        if (!AssetDatabase.DeleteAsset(request.TargetPath))
                            throw new InvalidOperationException($"Failed to replace '{request.TargetPath}'.");
                    }

                    if (!AssetDatabase.CopyAsset(request.SourcePath, request.TargetPath))
                        throw new InvalidOperationException(
                            $"AssetDatabase.CopyAsset failed: '{request.SourcePath}' -> '{request.TargetPath}'.");
                    request.Copied = true;
                }

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                return BuildCopyResult(prepared, false, false, errors);
            }
            catch (Exception exception)
            {
                errors.Add(exception.Message);
                for (int index = prepared.Count - 1; index >= 0; index--)
                {
                    if (prepared[index].Copied)
                        AssetDatabase.DeleteAsset(prepared[index].TargetPath);
                }
                RestoreSnapshots(snapshots, errors);
                DeleteCreatedFolders(created, errors);
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                return BuildCopyResult(prepared, false, true, errors);
            }
        }

        public static object Dependencies(Dictionary<string, object> args)
        {
            string path = NormalizeAssetPath(GetString(args, "path"));
            if (string.IsNullOrEmpty(path) || AssetDatabase.LoadMainAssetAtPath(path) == null)
                return VmAutomationResponse.Error($"Asset was not found: '{path}'", "asset_not_found");

            string direction = (GetString(args, "direction") ?? "both").ToLowerInvariant();
            if (direction != "outgoing" && direction != "incoming" && direction != "both")
                return VmAutomationResponse.Error("direction must be outgoing, incoming, or both.", "invalid_arguments");

            bool recursive = GetBool(args, "recursive", true);
            int offset = Math.Max(0, GetInt(args, "offset", 0));
            int limit = Math.Max(1, Math.Min(MaxPageSize, GetInt(args, "limit", DefaultPageSize)));
            var searchRoots = GetStringList(args, "searchRoots");
            if (searchRoots.Count == 0)
                searchRoots.Add("Assets");
            searchRoots = searchRoots.Select(NormalizeAssetPath)
                .Where(AssetDatabase.IsValidFolder).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            var outgoing = new List<string>();
            if (direction != "incoming")
            {
                outgoing.AddRange(AssetDatabase.GetDependencies(path, recursive)
                    .Select(NormalizeAssetPath)
                    .Where(item => !string.Equals(item, path, StringComparison.OrdinalIgnoreCase)));
            }

            var incoming = new List<string>();
            if (direction != "outgoing")
            {
                foreach (string guid in AssetDatabase.FindAssets("", searchRoots.ToArray()))
                {
                    string candidate = NormalizeAssetPath(AssetDatabase.GUIDToAssetPath(guid));
                    if (string.IsNullOrEmpty(candidate) || candidate == path || AssetDatabase.IsValidFolder(candidate))
                        continue;
                    if (AssetDatabase.GetDependencies(candidate, recursive)
                        .Any(dependency => string.Equals(NormalizeAssetPath(dependency), path,
                            StringComparison.OrdinalIgnoreCase)))
                    {
                        incoming.Add(candidate);
                    }
                }
            }

            outgoing = outgoing.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(item => item).ToList();
            incoming = incoming.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(item => item).ToList();
            var combined = outgoing.Select(item => DescribeReference(item, "outgoing"))
                .Concat(incoming.Select(item => DescribeReference(item, "incoming")))
                .OrderBy(item => item["path"].ToString(), StringComparer.OrdinalIgnoreCase)
                .ThenBy(item => item["direction"].ToString(), StringComparer.OrdinalIgnoreCase)
                .ToList();
            var page = combined.Skip(offset).Take(limit).ToList();

            return new Dictionary<string, object>
            {
                { "success", true },
                { "path", path },
                { "direction", direction },
                { "recursive", recursive },
                { "outgoingCount", outgoing.Count },
                { "incomingCount", incoming.Count },
                { "total", combined.Count },
                { "offset", offset },
                { "limit", limit },
                { "hasMore", offset + page.Count < combined.Count },
                { "nextOffset", offset + page.Count < combined.Count ? (object)(offset + page.Count) : null },
                { "references", page },
            };
        }

        public static object Transaction(Dictionary<string, object> args)
        {
            if (VmAutomationAssetTransactionPlan.GetBool(args, "dryRun"))
                return VmAutomationAssetTransactionJobRunner.DryRun(args);
            if (!VmAutomationAssetTransactionJobRunner.TryValidateStart(args, out object error))
                return error;
            return VmAutomationWorkspaceJobRunner.StartAssetTransaction(args);
        }
        private static Dictionary<string, object> DescribeReference(string path, string direction)
        {
            Type type = AssetDatabase.GetMainAssetTypeAtPath(path);
            return new Dictionary<string, object>
            {
                { "path", path }, { "direction", direction },
                { "guid", AssetDatabase.AssetPathToGUID(path) }, { "type", type?.Name ?? "Unknown" },
            };
        }

        private static object BuildCopyResult(List<CopyRequest> requests, bool dryRun, bool rolledBack,
            List<string> errors)
        {
            var copies = requests.Select(item =>
            {
                var copy = new Dictionary<string, object>
                {
                    { "sourcePath", item.SourcePath },
                    { "targetPath", item.TargetPath },
                };
                if (item.Copied && !rolledBack)
                    copy["targetGuid"] = AssetDatabase.AssetPathToGUID(item.TargetPath);
                return copy;
            }).ToList();

            if (errors.Count > 0)
            {
                var extra = new Dictionary<string, object>
                {
                    { "rolledBack", rolledBack },
                    { "copies", copies },
                };
                if (errors.Count > 1)
                    extra["rollbackErrors"] = errors.Skip(1).ToList();
                return VmAutomationResponse.Error(errors[0], "asset_copy_failed", false, extra);
            }

            var result = new Dictionary<string, object>
            {
                { "success", true },
                { "copies", copies },
            };
            if (dryRun)
                result["dryRun"] = true;
            return result;
        }

        private static FileSnapshot CaptureFileSnapshot(string assetPath)
        {
            string absolutePath = ToAbsolutePath(assetPath);
            if (!File.Exists(absolutePath))
                throw new InvalidOperationException($"Asset file was not found on disk: '{assetPath}'.");
            string metaPath = absolutePath + ".meta";
            return new FileSnapshot
            {
                AssetPath = assetPath,
                AssetBytes = File.ReadAllBytes(absolutePath),
                MetaBytes = File.Exists(metaPath) ? File.ReadAllBytes(metaPath) : null,
            };
        }

        private static void RestoreSnapshots(IEnumerable<FileSnapshot> snapshots, List<string> errors)
        {
            foreach (var snapshot in snapshots.Reverse())
            {
                try { RestoreSnapshot(snapshot); }
                catch (Exception exception) { errors.Add(exception.Message); }
            }
        }

        private static void RestoreSnapshot(FileSnapshot snapshot)
        {
            string absolutePath = ToAbsolutePath(snapshot.AssetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(absolutePath));
            File.WriteAllBytes(absolutePath, snapshot.AssetBytes);
            if (snapshot.MetaBytes != null)
                File.WriteAllBytes(absolutePath + ".meta", snapshot.MetaBytes);
            AssetDatabase.ImportAsset(snapshot.AssetPath, ImportAssetOptions.ForceSynchronousImport |
                                                          ImportAssetOptions.ForceUpdate);
        }

        private static void EnsureParentFolder(string assetPath, List<string> created)
        {
            string parent = NormalizeAssetPath(Path.GetDirectoryName(assetPath));
            if (!string.IsNullOrEmpty(parent)) EnsureFolderPath(parent, created);
        }

        private static void EnsureFolderPath(string path, List<string> created)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int index = 1; index < parts.Length; index++)
            {
                string next = current + "/" + parts[index];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    string guid = AssetDatabase.CreateFolder(current, parts[index]);
                    if (string.IsNullOrEmpty(guid))
                        throw new InvalidOperationException($"Failed to create folder '{next}'.");
                    created.Add(next);
                }
                current = next;
            }
        }

        private static void DeleteCreatedFolders(List<string> created, List<string> errors)
        {
            for (int index = created.Count - 1; index >= 0; index--)
            {
                if (!AssetDatabase.IsValidFolder(created[index])) continue;
                if (!AssetDatabase.DeleteAsset(created[index]))
                    errors.Add($"Failed to remove created folder '{created[index]}'.");
            }
        }

        private static bool TryValidateFolderPath(string path, out string error)
        {
            if (string.IsNullOrEmpty(path) || (path != "Assets" && !path.StartsWith("Assets/", StringComparison.Ordinal)))
            {
                error = "Folder path must be Assets or a child of Assets.";
                return false;
            }
            error = null;
            return true;
        }

        private static bool TryValidateAssetPath(string path, out string error)
        {
            if (string.IsNullOrEmpty(path) || !path.StartsWith("Assets/", StringComparison.Ordinal) ||
                string.IsNullOrEmpty(Path.GetExtension(path)))
            {
                error = "Asset path must point to a file below Assets/.";
                return false;
            }
            error = null;
            return true;
        }

        private static string ToAbsolutePath(string assetPath)
        {
            string projectRoot = Directory.GetParent(UnityEngine.Application.dataPath).FullName;
            return Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
        }

        private static string NormalizeAssetPath(string path)
        {
            return (path ?? "").Trim().Replace('\\', '/').TrimEnd('/');
        }

        private static string GetString(Dictionary<string, object> args, string key)
        {
            return args != null && args.TryGetValue(key, out object value) && value != null
                ? value.ToString()
                : null;
        }

        private static bool GetBool(Dictionary<string, object> args, string key, bool defaultValue)
        {
            if (args == null || !args.TryGetValue(key, out object value) || value == null) return defaultValue;
            return value is bool boolValue ? boolValue : bool.TryParse(value.ToString(), out bool parsed) && parsed;
        }

        private static int GetInt(Dictionary<string, object> args, string key, int defaultValue)
        {
            if (args == null || !args.TryGetValue(key, out object value) || value == null) return defaultValue;
            return int.TryParse(value.ToString(), out int parsed) ? parsed : defaultValue;
        }

        private static List<string> GetStringList(Dictionary<string, object> args, string key)
        {
            if (args == null || !args.TryGetValue(key, out object value) || !(value is IList list))
                return new List<string>();
            return list.Cast<object>().Where(item => item != null).Select(item => item.ToString()).ToList();
        }

        private static List<Dictionary<string, object>> GetDictionaryList(Dictionary<string, object> args,
            string key)
        {
            if (args == null || !args.TryGetValue(key, out object value) || !(value is IList list))
                return new List<Dictionary<string, object>>();
            return list.Cast<object>().Select(VmAutomationResponse.ToDictionary).Where(item => item != null).ToList();
        }

        private sealed class CopyRequest
        {
            public string SourcePath;
            public string TargetPath;
            public bool Copied;
        }

        private sealed class FileSnapshot
        {
            public string AssetPath;
            public byte[] AssetBytes;
            public byte[] MetaBytes;
        }
    }
}
