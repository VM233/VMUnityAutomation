using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;

namespace VMUnityAutomation.Editor
{
    /// <summary>
    /// Builds the complete, immutable asset-transaction plan before the first mutation.
    /// Runtime execution consumes only this normalized plan.
    /// </summary>
    internal sealed class MCPAssetTransactionPlan
    {
        private static readonly HashSet<string> CompilationExtensions =
            new(StringComparer.OrdinalIgnoreCase)
            {
                ".cs", ".asmdef", ".asmref", ".rsp", ".dll", ".pdb", ".mdb",
            };

        private static readonly Dictionary<string, HashSet<string>> OperationFields =
            new(StringComparer.Ordinal)
            {
                ["ensure-folder"] = Set("type", "path"),
                ["copy"] = Set("type", "sourcePath", "targetPath"),
                ["move"] = Set("type", "sourcePath", "targetPath"),
                ["delete"] = Set("type", "path"),
                ["serialized-set"] = Set("type", "assetPath", "assetType",
                    "propertyPath", "value", "maxDepth", "maxArrayElements"),
            };

        internal List<Dictionary<string, object>> Operations { get; } = new();
        internal List<string> TouchedAssetPaths { get; } = new();
        internal List<string> CandidateFolders { get; } = new();
        internal List<string> RequiredAssets { get; } = new();
        internal List<Dictionary<string, object>> ReferenceChecks { get; } = new();
        internal List<string> SceneMutationPaths { get; } = new();
        internal bool CompilationRequired { get; private set; }

        internal static MCPAssetTransactionPlan Build(Dictionary<string, object> args)
        {
            var sourceOperations = GetDictionaryList(args, "operations");
            if (sourceOperations.Count == 0)
                throw new ValidationException("operations is required.", "invalid_arguments");

            var plan = new MCPAssetTransactionPlan();
            var virtualCreated = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var virtualRemoved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var virtualFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var touched = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var candidateFolders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int index = 0; index < sourceOperations.Count; index++)
            {
                Dictionary<string, object> source = sourceOperations[index];
                string type = (GetString(source, "type") ?? "").Trim().ToLowerInvariant();
                if (!OperationFields.TryGetValue(type, out HashSet<string> allowedFields))
                {
                    throw new ValidationException(
                        $"Unsupported transaction operation '{type}' at index {index}.",
                        "invalid_operation");
                }

                string unknownField = source.Keys.FirstOrDefault(key => !allowedFields.Contains(key));
                if (unknownField != null)
                {
                    throw new ValidationException(
                        $"Operation {index} ('{type}') contains unknown field '{unknownField}'.",
                        "invalid_arguments");
                }

                var operation = new Dictionary<string, object>(source) { ["type"] = type };
                NormalizeOperationPaths(operation);
                ValidateRequiredFields(operation, index);
                if (!TryPreflightOperation(operation, virtualCreated, virtualRemoved,
                        virtualFolders, out string error))
                {
                    throw new ValidationException(
                        $"Operation {index} ('{type}') failed preflight: {error}",
                        "transaction_preflight_failed");
                }

                plan.Operations.Add(operation);
                AddMutationPaths(operation, touched, candidateFolders,
                    plan.SceneMutationPaths);
                ApplyVirtualOperation(operation, virtualCreated, virtualRemoved, virtualFolders);
            }

            foreach (string rawPath in GetStringList(args, "requiredAssets"))
            {
                string path = NormalizeAssetPath(rawPath);
                if (!TryValidateProjectPath(path, out string error))
                    throw new ValidationException(error, "invalid_arguments");
                plan.RequiredAssets.Add(path);
            }

            foreach (Dictionary<string, object> rawCheck in GetDictionaryList(args, "referenceChecks"))
            {
                string unknownField = rawCheck.Keys.FirstOrDefault(key =>
                    key != "assetPath" && key != "requiredDependencies");
                if (unknownField != null)
                {
                    throw new ValidationException(
                        $"referenceChecks contains unknown field '{unknownField}'.",
                        "invalid_arguments");
                }

                string assetPath = NormalizeAssetPath(GetString(rawCheck, "assetPath"));
                if (!TryValidateAssetPath(assetPath, out string error))
                    throw new ValidationException(error, "invalid_arguments");
                var requiredDependencies = new List<object>();
                foreach (string rawDependency in GetStringList(rawCheck, "requiredDependencies"))
                {
                    string dependency = NormalizeAssetPath(rawDependency);
                    if (!TryValidateProjectPath(dependency, out error))
                        throw new ValidationException(error, "invalid_arguments");
                    requiredDependencies.Add(dependency);
                }
                plan.ReferenceChecks.Add(new Dictionary<string, object>
                {
                    { "assetPath", assetPath },
                    { "requiredDependencies", requiredDependencies },
                });
            }

            plan.TouchedAssetPaths.AddRange(touched.OrderBy(path => path,
                StringComparer.OrdinalIgnoreCase));
            plan.CandidateFolders.AddRange(candidateFolders
                .Where(path => path != "Assets")
                .OrderBy(path => path.Count(character => character == '/'))
                .ThenBy(path => path, StringComparer.OrdinalIgnoreCase));
            List<string> distinctScenePaths = plan.SceneMutationPaths
                .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            plan.SceneMutationPaths.Clear();
            plan.SceneMutationPaths.AddRange(distinctScenePaths);
            plan.CompilationRequired = plan.TouchedAssetPaths.Any(path =>
                CompilationExtensions.Contains(Path.GetExtension(path)));

            if (MCPSceneCommands.TryRejectLoadedSceneAssetMutation(
                    plan.SceneMutationPaths, "execute an asset transaction",
                    out object sceneMutationError))
            {
                MCPResponse.TryGetError(sceneMutationError, out string message,
                    out string errorCode, out _);
                throw new ValidationException(
                    message ?? "A loaded scene asset cannot be changed by this transaction.",
                    string.IsNullOrEmpty(errorCode) ? "loaded_scene_asset_mutation" : errorCode);
            }

            return plan;
        }

        internal Dictionary<string, object> ToPersistentState()
        {
            return new Dictionary<string, object>
            {
                { "operations", Operations.Cast<object>().ToList() },
                { "touchedAssetPaths", TouchedAssetPaths.Cast<object>().ToList() },
                { "candidateFolders", CandidateFolders.Cast<object>().ToList() },
                { "requiredAssets", RequiredAssets.Cast<object>().ToList() },
                { "referenceChecks", ReferenceChecks.Cast<object>().ToList() },
                { "compilationRequired", CompilationRequired },
                { "nextOperationIndex", 0 },
                { "results", new List<object>() },
            };
        }

        internal static void VerifyPostconditions(Dictionary<string, object> state)
        {
            foreach (string path in GetStringList(state, "requiredAssets"))
            {
                if (AssetDatabase.LoadMainAssetAtPath(path) == null &&
                    !AssetDatabase.IsValidFolder(path))
                {
                    throw new InvalidOperationException(
                        $"Required asset was not found after transaction: '{path}'.");
                }
            }

            foreach (Dictionary<string, object> check in GetDictionaryList(state, "referenceChecks"))
            {
                string assetPath = GetString(check, "assetPath");
                var dependencies = new HashSet<string>(
                    AssetDatabase.GetDependencies(assetPath, true).Select(NormalizeAssetPath),
                    StringComparer.OrdinalIgnoreCase);
                foreach (string required in GetStringList(check, "requiredDependencies"))
                {
                    if (!dependencies.Contains(required))
                    {
                        throw new InvalidOperationException(
                            $"'{assetPath}' does not reference required dependency '{required}'.");
                    }
                }
            }
        }

        private static void ValidateRequiredFields(Dictionary<string, object> operation, int index)
        {
            string type = GetString(operation, "type");
            string[] required = type switch
            {
                "ensure-folder" => new[] { "path" },
                "copy" => new[] { "sourcePath", "targetPath" },
                "move" => new[] { "sourcePath", "targetPath" },
                "delete" => new[] { "path" },
                "serialized-set" => new[] { "assetPath", "propertyPath", "value" },
                _ => Array.Empty<string>(),
            };
            foreach (string field in required)
            {
                if (!operation.TryGetValue(field, out object value) || value == null ||
                    field != "value" && string.IsNullOrWhiteSpace(value.ToString()))
                {
                    throw new ValidationException(
                        $"Operation {index} ('{type}') requires '{field}'.",
                        "invalid_arguments");
                }
            }
        }

        private static void AddMutationPaths(Dictionary<string, object> operation,
            HashSet<string> touched, HashSet<string> folders, List<string> scenePaths)
        {
            string type = GetString(operation, "type");
            switch (type)
            {
                case "ensure-folder":
                    AddFolderAndParents(GetString(operation, "path"), folders);
                    break;
                case "copy":
                    AddTouched(GetString(operation, "targetPath"), touched, scenePaths);
                    AddFolderAndParents(Path.GetDirectoryName(
                        GetString(operation, "targetPath")), folders);
                    break;
                case "move":
                    AddTouched(GetString(operation, "sourcePath"), touched, scenePaths);
                    AddTouched(GetString(operation, "targetPath"), touched, scenePaths);
                    AddFolderAndParents(Path.GetDirectoryName(
                        GetString(operation, "targetPath")), folders);
                    break;
                case "delete":
                    AddTouched(GetString(operation, "path"), touched, scenePaths);
                    break;
                case "serialized-set":
                    AddTouched(GetString(operation, "assetPath"), touched, scenePaths);
                    break;
            }
        }

        private static void AddTouched(string path, HashSet<string> touched,
            List<string> scenePaths)
        {
            if (string.IsNullOrEmpty(path)) return;
            touched.Add(path);
            scenePaths.Add(path);
        }

        private static void AddFolderAndParents(string rawPath, HashSet<string> folders)
        {
            string path = NormalizeAssetPath(rawPath);
            while (!string.IsNullOrEmpty(path) && path.StartsWith("Assets", StringComparison.Ordinal))
            {
                folders.Add(path);
                if (path == "Assets") break;
                path = NormalizeAssetPath(Path.GetDirectoryName(path));
            }
        }

        private static bool TryPreflightOperation(Dictionary<string, object> operation,
            HashSet<string> virtualCreated, HashSet<string> virtualRemoved,
            HashSet<string> virtualFolders, out string error)
        {
            error = null;
            string type = GetString(operation, "type");
            if (type == "ensure-folder")
                return TryValidateFolderPath(GetString(operation, "path"), out error);
            if (type == "copy" || type == "move")
            {
                string source = GetString(operation, "sourcePath");
                string target = GetString(operation, "targetPath");
                if (!VirtuallyExists(source, virtualCreated, virtualRemoved))
                {
                    error = $"Source asset was not found: '{source}'.";
                    return false;
                }
                if (!TryValidateAssetPath(source, out error) ||
                    !TryValidateAssetPath(target, out error))
                    return false;
                if (VirtuallyExists(target, virtualCreated, virtualRemoved))
                {
                    error = $"Target already exists: '{target}'.";
                    return false;
                }
                if (virtualFolders.Contains(source) ||
                    (!virtualRemoved.Contains(source) && AssetDatabase.IsValidFolder(source)))
                {
                    error = $"Transaction {type} accepts files, not folders.";
                    return false;
                }
                return true;
            }
            if (type == "delete")
            {
                string path = GetString(operation, "path");
                if (!TryValidateAssetPath(path, out error)) return false;
                if (virtualFolders.Contains(path) ||
                    (!virtualRemoved.Contains(path) && AssetDatabase.IsValidFolder(path)))
                {
                    error = "Transaction delete accepts files, not folders.";
                    return false;
                }
                if (!VirtuallyExists(path, virtualCreated, virtualRemoved))
                {
                    error = $"Asset was not found: '{path}'.";
                    return false;
                }
                return true;
            }
            if (type == "serialized-set")
            {
                string path = GetString(operation, "assetPath");
                if (!TryValidateAssetPath(path, out error)) return false;
                if (!VirtuallyExists(path, virtualCreated, virtualRemoved))
                {
                    error = $"Serialized asset was not found: '{path}'.";
                    return false;
                }
                return true;
            }
            return false;
        }

        private static bool VirtuallyExists(string path, HashSet<string> created,
            HashSet<string> removed)
        {
            if (created.Contains(path)) return true;
            if (removed.Contains(path)) return false;
            return AssetDatabase.LoadMainAssetAtPath(path) != null ||
                   AssetDatabase.IsValidFolder(path);
        }

        private static void ApplyVirtualOperation(Dictionary<string, object> operation,
            HashSet<string> created, HashSet<string> removed, HashSet<string> folders)
        {
            string type = GetString(operation, "type");
            if (type == "ensure-folder")
            {
                string path = GetString(operation, "path");
                removed.Remove(path);
                created.Add(path);
                folders.Add(path);
                return;
            }
            if (type == "copy")
            {
                string target = GetString(operation, "targetPath");
                removed.Remove(target);
                created.Add(target);
                folders.Remove(target);
                return;
            }
            if (type == "move")
            {
                string source = GetString(operation, "sourcePath");
                string target = GetString(operation, "targetPath");
                created.Remove(source);
                removed.Add(source);
                folders.Remove(source);
                removed.Remove(target);
                created.Add(target);
                folders.Remove(target);
                return;
            }
            if (type == "delete")
            {
                string path = GetString(operation, "path");
                created.Remove(path);
                removed.Add(path);
                folders.Remove(path);
            }
        }

        private static void NormalizeOperationPaths(Dictionary<string, object> operation)
        {
            foreach (string key in new[] { "path", "sourcePath", "targetPath", "assetPath" })
            {
                if (operation.TryGetValue(key, out object value) && value != null)
                    operation[key] = NormalizeAssetPath(value.ToString());
            }
        }

        private static bool TryValidateFolderPath(string path, out string error)
        {
            if (!TryValidateCanonicalProjectPath(path, out error) ||
                path != "Assets" && string.IsNullOrEmpty(Path.GetFileName(path)))
            {
                error ??= "Folder path must be Assets or a child of Assets.";
                return false;
            }
            error = null;
            return true;
        }

        private static bool TryValidateAssetPath(string path, out string error)
        {
            if (!TryValidateCanonicalProjectPath(path, out error) || path == "Assets" ||
                string.IsNullOrEmpty(Path.GetExtension(path)))
            {
                error ??= "Asset path must point to a file below Assets/.";
                return false;
            }
            error = null;
            return true;
        }

        private static bool TryValidateProjectPath(string path, out string error)
        {
            return TryValidateCanonicalProjectPath(path, out error);
        }

        private static bool TryValidateCanonicalProjectPath(string path, out string error)
        {
            error = null;
            if (string.IsNullOrEmpty(path) ||
                path != "Assets" && !path.StartsWith("Assets/", StringComparison.Ordinal))
            {
                error = "Project path must be Assets or a child of Assets.";
                return false;
            }

            string[] segments = path.Split('/');
            if (segments.Any(segment => string.IsNullOrWhiteSpace(segment) ||
                                        segment == "." || segment == ".." ||
                                        segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0))
            {
                error = $"Project path is not canonical: '{path}'.";
                return false;
            }

            string assetsRoot = Path.GetFullPath(UnityEngine.Application.dataPath);
            string relative = path == "Assets" ? "" : path.Substring("Assets/".Length);
            string absolute = Path.GetFullPath(Path.Combine(assetsRoot,
                relative.Replace('/', Path.DirectorySeparatorChar)));
            if (absolute != assetsRoot && !absolute.StartsWith(
                    assetsRoot + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase))
            {
                error = $"Project path escapes Assets: '{path}'.";
                return false;
            }
            return true;
        }

        internal static string NormalizeAssetPath(string path)
        {
            return (path ?? "").Trim().Replace('\\', '/').TrimEnd('/');
        }

        internal static string GetString(Dictionary<string, object> values, string key,
            string fallback = "")
        {
            return values != null && values.TryGetValue(key, out object value) && value != null
                ? value.ToString()
                : fallback;
        }

        internal static int GetInt(Dictionary<string, object> values, string key, int fallback = 0)
        {
            return values != null && values.TryGetValue(key, out object value) && value != null &&
                   int.TryParse(value.ToString(), out int result)
                ? result
                : fallback;
        }

        internal static bool GetBool(Dictionary<string, object> values, string key,
            bool fallback = false)
        {
            if (values == null || !values.TryGetValue(key, out object value) || value == null)
                return fallback;
            return value is bool result
                ? result
                : bool.TryParse(value.ToString(), out result) ? result : fallback;
        }

        internal static List<string> GetStringList(Dictionary<string, object> values, string key)
        {
            if (values == null || !values.TryGetValue(key, out object value) ||
                !(value is IList list))
                return new List<string>();
            return list.Cast<object>().Where(item => item != null)
                .Select(item => item.ToString()).ToList();
        }

        internal static List<Dictionary<string, object>> GetDictionaryList(
            Dictionary<string, object> values, string key)
        {
            if (values == null || !values.TryGetValue(key, out object value) ||
                !(value is IList list))
                return new List<Dictionary<string, object>>();
            return list.Cast<object>().Select(MCPResponse.ToDictionary)
                .Where(item => item != null).ToList();
        }

        private static HashSet<string> Set(params string[] values)
        {
            return new HashSet<string>(values, StringComparer.Ordinal);
        }

        internal sealed class ValidationException : Exception
        {
            internal string ErrorCode { get; }

            internal ValidationException(string message, string errorCode) : base(message)
            {
                ErrorCode = errorCode;
            }
        }
    }
}
