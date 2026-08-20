using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace VMUnityAutomation.Editor
{
    public static class MCPAssetCommands
    {
        public static object List(Dictionary<string, object> args)
        {
            string folder = args.ContainsKey("folder") ? args["folder"].ToString() : "Assets";
            string typeFilter = args.ContainsKey("type") ? args["type"].ToString() : null;
            string search = args.ContainsKey("search") ? args["search"].ToString() : null;
            bool recursive = !args.ContainsKey("recursive") || Convert.ToBoolean(args["recursive"]);
            int offset = Math.Max(0, args.TryGetValue("offset", out var offsetValue) && offsetValue != null
                ? Convert.ToInt32(offsetValue)
                : 0);
            int limit = Math.Max(1, Math.Min(500,
                args.TryGetValue("limit", out var limitValue) && limitValue != null
                    ? Convert.ToInt32(limitValue)
                    : 100));

            string searchQuery = "";
            if (!string.IsNullOrEmpty(search))
                searchQuery = search;
            if (!string.IsNullOrEmpty(typeFilter))
                searchQuery += $" t:{typeFilter}";

            string[] guids;
            if (!string.IsNullOrEmpty(searchQuery))
            {
                string[] searchFolders = recursive ? new[] { folder } : new[] { folder };
                guids = AssetDatabase.FindAssets(searchQuery.Trim(), searchFolders);
            }
            else
            {
                guids = AssetDatabase.FindAssets("", new[] { folder });
            }

            var assets = new List<Dictionary<string, object>>();
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);

                // If not recursive, only include direct children
                if (!recursive)
                {
                    string parentDir = Path.GetDirectoryName(path).Replace("\\", "/");
                    if (parentDir != folder) continue;
                }

                var assetType = AssetDatabase.GetMainAssetTypeAtPath(path);
                assets.Add(new Dictionary<string, object>
                {
                    { "path", path },
                    { "name", Path.GetFileName(path) },
                    { "type", assetType?.Name ?? "Unknown" },
                    { "guid", guid },
                    { "isFolder", AssetDatabase.IsValidFolder(path) },
                });
            }

            assets = assets.OrderBy(asset => asset["path"].ToString(), StringComparer.Ordinal).ToList();
            int total = assets.Count;
            var page = assets.Skip(offset).Take(limit).ToList();
            return new Dictionary<string, object>
            {
                { "folder", folder },
                { "count", page.Count },
                { "total", total },
                { "offset", offset },
                { "limit", limit },
                { "hasMore", offset + page.Count < total },
                { "nextOffset", offset + page.Count < total ? (object)(offset + page.Count) : null },
                { "assets", page },
            };
        }

        public static object Import(Dictionary<string, object> args)
        {
            if (!MCPExecutionOptions.TryParse(args, out var execution, out string executionError))
                return ImportError(executionError);
            if (!TryPrepareImports(args, out var entries, out object preparationError))
                return preparationError;
            if (GetBool(args, "dryRun", false))
                return BuildImportResult(entries, execution, true, new List<string>());
            if (MCPSceneCommands.TryRejectLoadedSceneAssetMutation(
                    entries.Select(entry => entry.DestinationPath), "import or overwrite assets",
                    out object sceneMutationError))
                return sceneMutationError;
            return ExecutePreparedImports(entries, execution);
        }

        public static void ImportDeferred(Dictionary<string, object> args, Action<object> resolve,
            Action<object> progress)
        {
            if (!MCPExecutionOptions.TryParse(args, out var execution, out string executionError))
            {
                resolve(ImportError(executionError));
                return;
            }
            if (!TryPrepareImports(args, out var entries, out object preparationError))
            {
                resolve(preparationError);
                return;
            }
            if (GetBool(args, "dryRun", false))
            {
                resolve(BuildImportResult(entries, execution, true, new List<string>()));
                return;
            }
            if (MCPSceneCommands.TryRejectLoadedSceneAssetMutation(
                    entries.Select(entry => entry.DestinationPath), "import or overwrite assets",
                    out object sceneMutationError))
            {
                resolve(sceneMutationError);
                return;
            }
            if (execution.ResolveMode(entries.Count) == MCPExecutionMode.Immediate)
            {
                resolve(ExecutePreparedImports(entries, execution));
                return;
            }

            int nextIndex = 0;
            double startedAt = EditorApplication.timeSinceStartup;
            string backupRoot = CreateImportBackupRoot();
            var errors = new List<string>();
            EditorApplication.CallbackFunction tick = null;
            Action<object> complete = result =>
            {
                if (tick != null)
                    EditorApplication.update -= tick;
                FinishAssetImports(backupRoot);
                resolve(result);
            };
            tick = () =>
            {
                int elapsedMs = (int)((EditorApplication.timeSinceStartup - startedAt) * 1000d);
                if (elapsedMs >= execution.TimeoutMs)
                {
                    string timeoutError = $"Asset imports timed out after {execution.TimeoutMs} ms";
                    errors.Add(timeoutError);
                    errors.AddRange(RollbackImports(entries));
                    complete(BuildImportFailure(entries, execution, timeoutError, errors));
                    return;
                }

                double frameStartedAt = EditorApplication.timeSinceStartup;
                int processedThisFrame = 0;
                while (nextIndex < entries.Count)
                {
                    var entry = entries[nextIndex++];
                    try
                    {
                        ExecuteImport(entry, backupRoot);
                    }
                    catch (Exception exception)
                    {
                        entry.Error = exception.Message;
                        errors.Add($"Import {entry.Index} failed: {exception.Message}");
                        if (execution.ContinueOnError)
                            errors.AddRange(RollbackImports(new[] { entry }));
                    }

                    processedThisFrame++;
                    progress?.Invoke(BuildImportProgress(entries, execution, nextIndex, elapsedMs));
                    if (!string.IsNullOrEmpty(entry.Error) && !execution.ContinueOnError)
                        break;
                    double frameElapsedMs = (EditorApplication.timeSinceStartup - frameStartedAt) * 1000d;
                    if (processedThisFrame >= execution.OperationsPerFrame ||
                        frameElapsedMs >= execution.FrameBudgetMs)
                        break;
                }

                if (errors.Count > 0 && !execution.ContinueOnError)
                {
                    errors.AddRange(RollbackImports(entries));
                    complete(BuildImportFailure(entries, execution, errors[0], errors));
                    return;
                }
                if (nextIndex < entries.Count)
                    return;

                complete(errors.Count == 0
                    ? BuildImportResult(entries, execution, false, errors)
                    : BuildImportFailure(entries, execution, "One or more asset imports failed", errors));
            };
            EditorApplication.update += tick;
            tick();
        }

        private static bool TryPrepareImports(Dictionary<string, object> args, out List<BatchImportEntry> entries,
            out object errorResult)
        {
            entries = new List<BatchImportEntry>();
            errorResult = null;
            if (!TryGetDictionaryList(args, "imports", out var requests, out string requestsError))
            {
                errorResult = ImportError(requestsError);
                return false;
            }
            if (requests.Count == 0)
            {
                errorResult = ImportError("imports must contain at least one import request");
                return false;
            }
            if (requests.Count > 500)
            {
                errorResult = ImportError("imports cannot contain more than 500 requests");
                return false;
            }

            if (!TryGetDictionary(args, "defaults", out var defaults, out string defaultsError))
            {
                errorResult = ImportError(defaultsError);
                return false;
            }

            string assetsRoot = Path.GetFullPath(Application.dataPath)
                .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            for (int index = 0; index < requests.Count; index++)
            {
                var request = requests[index];
                string sourcePath = GetString(request, "sourcePath");
                string destinationPath = NormalizeAssetPath(GetString(request, "destinationPath"));
                if (string.IsNullOrWhiteSpace(sourcePath))
                    return FailImportPreparation(index, "sourcePath is required", out errorResult);
                if (!Path.IsPathRooted(sourcePath))
                    return FailImportPreparation(index, "sourcePath must be an absolute path", out errorResult);
                sourcePath = Path.GetFullPath(sourcePath);
                if (!File.Exists(sourcePath))
                    return FailImportPreparation(index, $"Source file not found at '{sourcePath}'", out errorResult);
                if (string.IsNullOrEmpty(destinationPath))
                    return FailImportPreparation(index, "destinationPath is required", out errorResult);
                if (!destinationPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                    return FailImportPreparation(index, "destinationPath must be under Assets/", out errorResult);
                if (string.IsNullOrEmpty(Path.GetExtension(destinationPath)))
                    return FailImportPreparation(index, "destinationPath must include a file extension", out errorResult);
                if (!string.Equals(Path.GetExtension(sourcePath), Path.GetExtension(destinationPath),
                        StringComparison.OrdinalIgnoreCase))
                    return FailImportPreparation(index, "sourcePath and destinationPath must use the same file extension",
                        out errorResult);

                string absoluteDestinationPath = GetAbsolutePath(destinationPath);
                string destinationRoot = Path.GetDirectoryName(absoluteDestinationPath) ?? "";
                if (!destinationRoot.StartsWith(assetsRoot + Path.DirectorySeparatorChar,
                        StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(destinationRoot, assetsRoot, StringComparison.OrdinalIgnoreCase))
                    return FailImportPreparation(index, "destinationPath resolves outside Assets/", out errorResult);
                if (string.Equals(sourcePath, absoluteDestinationPath, StringComparison.OrdinalIgnoreCase))
                    return FailImportPreparation(index, "sourcePath and destinationPath resolve to the same file",
                        out errorResult);

                var settings = new Dictionary<string, object>(defaults);
                foreach (var pair in request)
                    settings[pair.Key] = pair.Value;
                if (!ValidateImportSettings(settings, out string settingsError))
                    return FailImportPreparation(index, settingsError, out errorResult);
                if (!TryParseSpriteSlice(settings, sourcePath, out var spriteSlice, out string spriteSliceError))
                    return FailImportPreparation(index, spriteSliceError, out errorResult);

                if (!MCPImageDuplicateCommands.TryNormalizeMode(GetString(settings, "dedupeMode"), sourcePath,
                        true, out string dedupeMode, out string dedupeModeError))
                    return FailImportPreparation(index, dedupeModeError, out errorResult);
                string dedupeScope = NormalizeDedupeScope(GetString(settings, "dedupeScope"));
                if (string.IsNullOrEmpty(dedupeScope))
                    return FailImportPreparation(index,
                        $"Unknown dedupeScope '{GetString(settings, "dedupeScope")}'. Supported: destinationFolder, searchPath, assets",
                        out errorResult);
                string dedupeSearchPath = NormalizeAssetPath(GetString(settings, "dedupeSearchPath"));
                if (dedupeScope == "searchPath")
                {
                    if (string.IsNullOrEmpty(dedupeSearchPath))
                        return FailImportPreparation(index,
                            "dedupeSearchPath is required when dedupeScope is searchPath", out errorResult);
                    if (!dedupeSearchPath.Equals("Assets", StringComparison.OrdinalIgnoreCase) &&
                        !dedupeSearchPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                        return FailImportPreparation(index, "dedupeSearchPath must be under Assets/", out errorResult);
                    if (!AssetDatabase.IsValidFolder(dedupeSearchPath))
                        return FailImportPreparation(index,
                            $"dedupeSearchPath does not exist: '{dedupeSearchPath}'", out errorResult);
                }
                string onDuplicate = NormalizeDuplicateAction(GetString(settings, "onDuplicate"));
                if (string.IsNullOrEmpty(onDuplicate))
                    return FailImportPreparation(index,
                        $"Unknown onDuplicate '{GetString(settings, "onDuplicate")}'. Supported: skip, error, report",
                        out errorResult);

                bool overwrite = GetBool(settings, "overwrite", false);
                bool existedBefore = File.Exists(absoluteDestinationPath);

                entries.Add(new BatchImportEntry
                {
                    Index = index,
                    SourcePath = sourcePath,
                    DestinationPath = destinationPath,
                    AbsoluteDestinationPath = absoluteDestinationPath,
                    Settings = settings,
                    Overwrite = overwrite,
                    ExistedBefore = existedBefore,
                    DedupeMode = dedupeMode,
                    DedupeScope = dedupeScope,
                    DedupeSearchPath = dedupeSearchPath,
                    OnDuplicate = onDuplicate,
                    SpriteSlice = spriteSlice,
                });
            }

            for (int index = 0; index < entries.Count; index++)
            {
                for (int otherIndex = index + 1; otherIndex < entries.Count; otherIndex++)
                {
                    if (string.Equals(entries[index].DestinationPath, entries[otherIndex].DestinationPath,
                            StringComparison.OrdinalIgnoreCase))
                        return FailImportPreparation(otherIndex,
                            $"Duplicate destinationPath '{entries[otherIndex].DestinationPath}'", out errorResult);
                }
            }

            if (!ApplyDuplicateDetection(entries, out int duplicateErrorIndex, out string duplicateError))
                return FailImportPreparation(duplicateErrorIndex, duplicateError, out errorResult);

            foreach (var entry in entries)
            {
                if (entry.ExistedBefore && !entry.Overwrite && !entry.Skipped)
                    return FailImportPreparation(entry.Index,
                        $"Target asset already exists at '{entry.DestinationPath}'; pass overwrite=true to replace it",
                        out errorResult);
            }

            return true;
        }

        private static bool FailImportPreparation(int index, string error, out object errorResult)
        {
            errorResult = ImportError($"Import {index} is invalid: {error}");
            return false;
        }

        private static Dictionary<string, object> ImportError(string error)
        {
            return new Dictionary<string, object>
            {
                { "success", false },
                { "error", error },
            };
        }

        private static bool ValidateImportSettings(Dictionary<string, object> settings, out string error)
        {
            error = "";
            try
            {
                ValidateEnum<TextureImporterType>(settings, "textureType");
                ValidateEnum<SpriteImportMode>(settings, "spriteMode");
                ValidateEnum<FilterMode>(settings, "filterMode");
                ValidateEnum<SpriteMeshType>(settings, "meshType");
                foreach (string key in new[] { "overwrite", "isReadable", "alphaIsTransparency", "mipmapEnabled" })
                {
                    if (settings.TryGetValue(key, out object value) && value != null)
                        Convert.ToBoolean(value);
                }
                if (settings.TryGetValue("pixelsPerUnit", out object pixelsPerUnit) && pixelsPerUnit != null)
                {
                    float parsed = Convert.ToSingle(pixelsPerUnit,
                        System.Globalization.CultureInfo.InvariantCulture);
                    if (float.IsNaN(parsed) || float.IsInfinity(parsed))
                        throw new ArgumentException("pixelsPerUnit must be a finite number");
                }

                string compression = GetString(settings, "compression");
                if (!string.IsNullOrWhiteSpace(compression) &&
                    !new[] { "none", "uncompressed", "low", "lq", "normal", "compressed", "high", "hq" }
                        .Contains(compression.ToLowerInvariant()))
                    throw new ArgumentException($"Unknown compression '{compression}'");

                if (!TryParseSpriteSlice(settings, null, out _, out string spriteSliceError))
                    throw new ArgumentException(spriteSliceError);
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        private static bool ApplyDuplicateDetection(List<BatchImportEntry> entries, out int errorIndex,
            out string error)
        {
            errorIndex = -1;
            error = "";
            var assetIndexes = new Dictionary<string,
                Dictionary<string, List<MCPImageDuplicateCommands.ImageAssetRecord>>>(StringComparer.OrdinalIgnoreCase);
            var priorSources = new Dictionary<string, List<BatchImportEntry>>(StringComparer.Ordinal);

            foreach (var entry in entries)
            {
                if (entry.DedupeMode == MCPImageDuplicateCommands.NoneMode)
                    continue;
                try
                {
                    var fingerprint = MCPImageDuplicateCommands.CreateFingerprint(entry.SourcePath, entry.DedupeMode);
                    entry.ContentHash = fingerprint.Hash;
                    entry.ImageWidth = fingerprint.Width;
                    entry.ImageHeight = fingerprint.Height;

                    string searchFolder = ResolveDedupeSearchFolder(entry);
                    string indexKey = entry.DedupeMode + "|" + searchFolder;
                    if (!assetIndexes.TryGetValue(indexKey, out var assetIndex))
                    {
                        assetIndex = MCPImageDuplicateCommands.BuildAssetIndex(searchFolder, entry.DedupeMode,
                            out var indexErrors);
                        if (indexErrors.Count > 0)
                            throw new InvalidDataException(
                                $"Could not fingerprint every candidate asset under '{searchFolder}': {indexErrors[0]}");
                        assetIndexes[indexKey] = assetIndex;
                    }

                    if (assetIndex.TryGetValue(entry.ContentHash, out var duplicateAssets) &&
                        duplicateAssets.Count > 0)
                    {
                        var duplicateAsset = duplicateAssets[0];
                        entry.Duplicate = true;
                        entry.DuplicateAssetPath = duplicateAsset.AssetPath;
                        entry.DuplicateAssetGuid = duplicateAsset.Guid;
                    }
                    else
                    {
                        string sourceKey = entry.DedupeMode + "|" + entry.ContentHash;
                        if (priorSources.TryGetValue(sourceKey, out var duplicateSources) &&
                            duplicateSources.Count > 0)
                        {
                            var duplicateSource = duplicateSources[0];
                            entry.Duplicate = true;
                            entry.DuplicateSourceIndex = duplicateSource.Index;
                            entry.DuplicateSourcePath = duplicateSource.SourcePath;
                        }
                    }

                    string priorKey = entry.DedupeMode + "|" + entry.ContentHash;
                    if (!priorSources.TryGetValue(priorKey, out var priorEntries))
                    {
                        priorEntries = new List<BatchImportEntry>();
                        priorSources[priorKey] = priorEntries;
                    }
                    priorEntries.Add(entry);

                    if (!entry.Duplicate)
                        continue;
                    if (entry.OnDuplicate == "error")
                    {
                        errorIndex = entry.Index;
                        error = BuildDuplicateMessage(entry);
                        return false;
                    }
                    if (entry.OnDuplicate == "skip")
                        entry.Skipped = true;
                }
                catch (Exception exception)
                {
                    errorIndex = entry.Index;
                    error = $"Duplicate detection failed: {exception.Message}";
                    return false;
                }
            }
            return true;
        }

        private static string ResolveDedupeSearchFolder(BatchImportEntry entry)
        {
            return entry.DedupeScope switch
            {
                "assets" => "Assets",
                "searchPath" => entry.DedupeSearchPath,
                _ => NormalizeAssetPath(Path.GetDirectoryName(entry.DestinationPath) ?? "Assets"),
            };
        }

        private static string NormalizeDedupeScope(string value)
        {
            string compact = (value ?? "").Trim().Replace("-", "").Replace("_", "").ToLowerInvariant();
            return compact switch
            {
                "" or "assets" => "assets",
                "destinationfolder" => "destinationFolder",
                "searchpath" => "searchPath",
                _ => "",
            };
        }

        private static string NormalizeDuplicateAction(string value)
        {
            return (value ?? "").Trim().ToLowerInvariant() switch
            {
                "" or "skip" => "skip",
                "error" => "error",
                "report" => "report",
                _ => "",
            };
        }

        private static string BuildDuplicateMessage(BatchImportEntry entry)
        {
            return !string.IsNullOrEmpty(entry.DuplicateAssetPath)
                ? $"Source content duplicates existing asset '{entry.DuplicateAssetPath}'"
                : $"Source content duplicates import {entry.DuplicateSourceIndex} ('{entry.DuplicateSourcePath}')";
        }

        private static void ValidateEnum<TEnum>(Dictionary<string, object> settings, string key)
            where TEnum : struct
        {
            string value = GetString(settings, key);
            if (!string.IsNullOrWhiteSpace(value) && !Enum.TryParse(value, true, out TEnum _))
                throw new ArgumentException($"Unknown {key} '{value}'");
        }

        private static object ExecutePreparedImports(List<BatchImportEntry> entries, MCPExecutionOptions execution)
        {
            string backupRoot = CreateImportBackupRoot();
            var errors = new List<string>();
            try
            {
                foreach (var entry in entries)
                {
                    try
                    {
                        ExecuteImport(entry, backupRoot);
                    }
                    catch (Exception exception)
                    {
                        entry.Error = exception.Message;
                        errors.Add($"Import {entry.Index} failed: {exception.Message}");
                        if (execution.ContinueOnError)
                            errors.AddRange(RollbackImports(new[] { entry }));
                        else
                            break;
                    }
                }

                if (errors.Count > 0 && !execution.ContinueOnError)
                    errors.AddRange(RollbackImports(entries));
                return errors.Count == 0
                    ? BuildImportResult(entries, execution, false, errors)
                    : BuildImportFailure(entries, execution,
                        execution.ContinueOnError ? "One or more asset imports failed" : errors[0], errors);
            }
            finally
            {
                FinishAssetImports(backupRoot);
            }
        }

        private static void ExecuteImport(BatchImportEntry entry, string backupRoot)
        {
            if (entry.Skipped)
                return;
            if (!File.Exists(entry.SourcePath))
                throw new FileNotFoundException("Source file disappeared after preflight", entry.SourcePath);
            bool existsNow = File.Exists(entry.AbsoluteDestinationPath);
            if (existsNow != entry.ExistedBefore)
                throw new IOException("Destination changed after preflight");

            string destinationDirectory = Path.GetDirectoryName(entry.AbsoluteDestinationPath);
            if (!string.IsNullOrEmpty(destinationDirectory))
                Directory.CreateDirectory(destinationDirectory);
            if (entry.ExistedBefore)
            {
                string entryBackupDirectory = Path.Combine(backupRoot, entry.Index.ToString());
                Directory.CreateDirectory(entryBackupDirectory);
                entry.BackupAssetPath = Path.Combine(entryBackupDirectory, "asset");
                File.Copy(entry.AbsoluteDestinationPath, entry.BackupAssetPath, true);
                string metaPath = entry.AbsoluteDestinationPath + ".meta";
                entry.MetaExistedBefore = File.Exists(metaPath);
                if (entry.MetaExistedBefore)
                {
                    entry.BackupMetaPath = Path.Combine(entryBackupDirectory, "asset.meta");
                    File.Copy(metaPath, entry.BackupMetaPath, true);
                }
                entry.OriginalGuid = AssetDatabase.AssetPathToGUID(entry.DestinationPath);
            }

            entry.Touched = true;
            File.Copy(entry.SourcePath, entry.AbsoluteDestinationPath, true);
            AssetDatabase.ImportAsset(entry.DestinationPath, ImportAssetOptions.ForceUpdate);
            entry.ImporterSettings = ConfigureTextureImporter(entry.DestinationPath, entry.Settings);
            entry.SpriteSliceResult = entry.SpriteSlice == null
                ? null
                : ApplySpriteSlice(entry.DestinationPath, entry.SpriteSlice);
            entry.SubAssets = DescribeSubAssets(entry.DestinationPath);
            entry.Imported = true;
        }

        private static List<string> RollbackImports(IEnumerable<BatchImportEntry> entries)
        {
            var rollbackErrors = new List<string>();
            foreach (var entry in entries.Reverse())
            {
                if (!entry.Touched || entry.RolledBack)
                    continue;
                try
                {
                    if (entry.ExistedBefore)
                    {
                        File.Copy(entry.BackupAssetPath, entry.AbsoluteDestinationPath, true);
                        string metaPath = entry.AbsoluteDestinationPath + ".meta";
                        if (entry.MetaExistedBefore)
                            File.Copy(entry.BackupMetaPath, metaPath, true);
                        else if (File.Exists(metaPath))
                            File.Delete(metaPath);
                        AssetDatabase.ImportAsset(entry.DestinationPath,
                            ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                    }
                    else
                    {
                        AssetDatabase.DeleteAsset(entry.DestinationPath);
                        if (File.Exists(entry.AbsoluteDestinationPath))
                            File.Delete(entry.AbsoluteDestinationPath);
                        string metaPath = entry.AbsoluteDestinationPath + ".meta";
                        if (File.Exists(metaPath))
                            File.Delete(metaPath);
                    }
                    entry.RolledBack = true;
                }
                catch (Exception exception)
                {
                    entry.RollbackError = exception.Message;
                    rollbackErrors.Add($"Rollback {entry.Index} failed: {exception.Message}");
                }
            }
            return rollbackErrors;
        }

        private static string CreateImportBackupRoot()
        {
            string path = Path.Combine(Path.GetTempPath(), $"unity-mcp-asset-import-{Guid.NewGuid():N}");
            Directory.CreateDirectory(path);
            return path;
        }

        private static void FinishAssetImports(string backupRoot)
        {
            try
            {
                AssetDatabase.SaveAssets();
            }
            finally
            {
                try
                {
                    if (!string.IsNullOrEmpty(backupRoot) && Directory.Exists(backupRoot))
                        Directory.Delete(backupRoot, true);
                }
                catch
                {
                    // A locked temporary backup is safe to leave for OS cleanup and must not hide the import result.
                }
            }
        }

        private static Dictionary<string, object> BuildImportResult(List<BatchImportEntry> entries,
            MCPExecutionOptions execution, bool dryRun, List<string> errors)
        {
            return new Dictionary<string, object>
            {
                { "success", errors.Count == 0 },
                { "dryRun", dryRun },
                { "importCount", entries.Count },
                { "importedCount", entries.Count(entry => entry.Imported && !entry.RolledBack) },
                { "skippedCount", entries.Count(entry => entry.Skipped) },
                { "duplicateCount", entries.Count(entry => entry.Duplicate) },
                { "failedCount", entries.Count(entry => !string.IsNullOrEmpty(entry.Error)) },
                { "rolledBackCount", entries.Count(entry => entry.RolledBack) },
                { "imports", entries.ConvertAll(CreateBatchImportResult) },
                { "execution", execution.ToResult(entries.Count) },
            };
        }

        private static Dictionary<string, object> BuildImportFailure(List<BatchImportEntry> entries,
            MCPExecutionOptions execution, string error, List<string> errors)
        {
            var result = BuildImportResult(entries, execution, false, errors);
            result["success"] = false;
            result["error"] = error;
            result["errors"] = errors;
            result["allTouchedRolledBack"] = entries.TrueForAll(entry => !entry.Touched || entry.RolledBack);
            return result;
        }

        private static Dictionary<string, object> BuildImportProgress(List<BatchImportEntry> entries,
            MCPExecutionOptions execution, int nextIndex, int elapsedMs)
        {
            return new Dictionary<string, object>
            {
                { "phase", "importing" },
                { "importCount", entries.Count },
                { "processedCount", nextIndex },
                { "elapsedMs", elapsedMs },
                { "execution", execution.ToResult(entries.Count) },
            };
        }

        private static Dictionary<string, object> CreateBatchImportResult(BatchImportEntry entry)
        {
            return new Dictionary<string, object>
            {
                { "index", entry.Index },
                { "sourcePath", entry.SourcePath },
                { "destinationPath", entry.DestinationPath },
                { "overwrite", entry.Overwrite },
                { "existedBefore", entry.ExistedBefore },
                { "existsNow", File.Exists(entry.AbsoluteDestinationPath) },
                { "originalGuid", entry.OriginalGuid ?? "" },
                { "currentGuid", AssetDatabase.AssetPathToGUID(entry.DestinationPath) },
                { "imported", entry.Imported },
                { "skipped", entry.Skipped },
                { "duplicate", entry.Duplicate },
                { "dedupeMode", entry.DedupeMode },
                { "dedupeScope", entry.DedupeScope },
                { "dedupeSearchPath", entry.DedupeSearchPath ?? "" },
                { "onDuplicate", entry.OnDuplicate },
                { "contentHash", entry.ContentHash ?? "" },
                { "imageWidth", entry.ImageWidth },
                { "imageHeight", entry.ImageHeight },
                { "duplicateAssetPath", entry.DuplicateAssetPath ?? "" },
                { "duplicateAssetGuid", entry.DuplicateAssetGuid ?? "" },
                { "duplicateSourceIndex", entry.DuplicateSourceIndex },
                { "duplicateSourcePath", entry.DuplicateSourcePath ?? "" },
                { "rolledBack", entry.RolledBack },
                { "error", entry.Error ?? "" },
                { "rollbackError", entry.RollbackError ?? "" },
                { "importer", entry.ImporterSettings },
                { "spriteSlice", entry.SpriteSliceResult },
                { "subAssets", entry.SubAssets ?? new List<Dictionary<string, object>>() },
            };
        }

        private static object ConfigureTextureImporter(string assetPath, Dictionary<string, object> args)
        {
            if (AssetImporter.GetAtPath(assetPath) is not TextureImporter importer)
                return null;

            bool changed = false;
            string textureType = GetString(args, "textureType");
            if (!string.IsNullOrWhiteSpace(textureType))
            {
                if (!Enum.TryParse(textureType, true, out TextureImporterType parsedTextureType))
                    throw new ArgumentException($"Unknown textureType '{textureType}'.");
                importer.textureType = parsedTextureType;
                changed = true;
            }

            string spriteMode = GetString(args, "spriteMode");
            if (!string.IsNullOrWhiteSpace(spriteMode))
            {
                if (!Enum.TryParse(spriteMode, true, out SpriteImportMode parsedSpriteMode))
                    throw new ArgumentException($"Unknown spriteMode '{spriteMode}'.");
                importer.spriteImportMode = parsedSpriteMode;
                changed = true;
            }

            if (args.ContainsKey("pixelsPerUnit"))
            {
                importer.spritePixelsPerUnit = Mathf.Max(0.0001f,
                    Convert.ToSingle(args["pixelsPerUnit"], System.Globalization.CultureInfo.InvariantCulture));
                changed = true;
            }

            string filterMode = GetString(args, "filterMode");
            if (!string.IsNullOrWhiteSpace(filterMode))
            {
                if (!Enum.TryParse(filterMode, true, out FilterMode parsedFilterMode))
                    throw new ArgumentException($"Unknown filterMode '{filterMode}'.");
                importer.filterMode = parsedFilterMode;
                changed = true;
            }

            if (args.ContainsKey("isReadable"))
            {
                importer.isReadable = Convert.ToBoolean(args["isReadable"]);
                changed = true;
            }

            string compression = GetString(args, "compression");
            if (!string.IsNullOrWhiteSpace(compression))
            {
                importer.textureCompression = compression.ToLowerInvariant() switch
                {
                    "none" or "uncompressed" => TextureImporterCompression.Uncompressed,
                    "low" or "lq" => TextureImporterCompression.CompressedLQ,
                    "normal" or "compressed" => TextureImporterCompression.Compressed,
                    "high" or "hq" => TextureImporterCompression.CompressedHQ,
                    _ => throw new ArgumentException($"Unknown compression '{compression}'.")
                };
                changed = true;
            }

            if (args.ContainsKey("alphaIsTransparency"))
            {
                importer.alphaIsTransparency = Convert.ToBoolean(args["alphaIsTransparency"]);
                changed = true;
            }

            string meshType = GetString(args, "meshType");
            if (!string.IsNullOrWhiteSpace(meshType))
            {
                if (!Enum.TryParse(meshType, true, out SpriteMeshType parsedMeshType))
                    throw new ArgumentException($"Unknown meshType '{meshType}'.");
                var serializedImporter = new SerializedObject(importer);
                var spriteMeshType = serializedImporter.FindProperty("m_SpriteMeshType");
                if (spriteMeshType == null)
                    throw new NotSupportedException("TextureImporter does not expose m_SpriteMeshType on this Unity version.");
                spriteMeshType.intValue = (int)parsedMeshType;
                serializedImporter.ApplyModifiedPropertiesWithoutUndo();
                changed = true;
            }

            if (args.ContainsKey("mipmapEnabled"))
            {
                importer.mipmapEnabled = Convert.ToBoolean(args["mipmapEnabled"]);
                changed = true;
            }

            if (changed)
                importer.SaveAndReimport();

            var importerObject = new SerializedObject(importer);
            var serializedMeshType = importerObject.FindProperty("m_SpriteMeshType");
            return new Dictionary<string, object>
            {
                { "type", importer.textureType.ToString() },
                { "spriteMode", importer.spriteImportMode.ToString() },
                { "pixelsPerUnit", importer.spritePixelsPerUnit },
                { "filterMode", importer.filterMode.ToString() },
                { "isReadable", importer.isReadable },
                { "compression", importer.textureCompression.ToString() },
                { "alphaIsTransparency", importer.alphaIsTransparency },
                { "meshType", serializedMeshType == null ? "" : ((SpriteMeshType)serializedMeshType.intValue).ToString() },
                { "mipmapEnabled", importer.mipmapEnabled }
            };
        }

        private static Dictionary<string, object> ApplySpriteSlice(string assetPath, SpriteSliceSettings settings)
        {
            var sliceArgs = new Dictionary<string, object>(settings.Arguments)
            {
                { "texturePath", assetPath }
            };
            var result = MCPSpriteSheetCommands.SliceSheet(sliceArgs) as Dictionary<string, object>;
            if (result == null)
                throw new InvalidOperationException("Fixed-grid sprite slicing returned an invalid result.");
            if (result.TryGetValue("error", out object error) && error != null &&
                !string.IsNullOrWhiteSpace(error.ToString()))
                throw new InvalidOperationException($"Fixed-grid sprite slicing failed: {error}");
            if (!result.TryGetValue("success", out object success) || !Convert.ToBoolean(success))
                throw new InvalidOperationException("Fixed-grid sprite slicing did not report success.");
            return result;
        }

        private static bool TryParseSpriteSlice(Dictionary<string, object> settings, string sourcePath,
            out SpriteSliceSettings spriteSlice, out string error)
        {
            spriteSlice = null;
            error = "";
            if (settings == null || !settings.TryGetValue("spriteSlice", out object rawValue) || rawValue == null)
                return true;
            if (!TryConvertToDictionary(rawValue, out var arguments))
            {
                error = "spriteSlice must be an object";
                return false;
            }

            if (!TryGetRequiredPositiveInt(arguments, "frameWidth", out int frameWidth, out error) ||
                !TryGetRequiredPositiveInt(arguments, "frameHeight", out int frameHeight, out error) ||
                !TryGetOptionalPositiveInt(arguments, "columns", out int columns, out error) ||
                !TryGetOptionalPositiveInt(arguments, "frameCount", out int frameCount, out error) ||
                !TryGetOptionalNonNegativeInt(arguments, "startX", out int startX, out error) ||
                !TryGetOptionalNonNegativeInt(arguments, "startY", out int startY, out error) ||
                !TryGetNormalizedPivot(arguments, out error))
                return false;

            if (arguments.TryGetValue("preserveSpriteIDs", out object preserveSpriteIDs) && preserveSpriteIDs != null)
            {
                try
                {
                    Convert.ToBoolean(preserveSpriteIDs);
                }
                catch (Exception exception)
                {
                    error = $"spriteSlice.preserveSpriteIDs must be a boolean: {exception.Message}";
                    return false;
                }
            }

            if (!string.IsNullOrEmpty(sourcePath))
            {
                try
                {
                    var fingerprint = MCPImageDuplicateCommands.CreateFingerprint(sourcePath,
                        MCPImageDuplicateCommands.DecodedPixelsMode);
                    int availableWidth = fingerprint.Width - startX;
                    int availableHeight = fingerprint.Height - startY;
                    int maximumColumns = availableWidth / frameWidth;
                    int maximumRows = availableHeight / frameHeight;
                    if (maximumColumns <= 0 || maximumRows <= 0)
                    {
                        error = $"spriteSlice frame grid does not fit within source image {fingerprint.Width}x{fingerprint.Height}";
                        return false;
                    }

                    int resolvedColumns = columns > 0 ? columns : maximumColumns;
                    if (resolvedColumns > maximumColumns)
                    {
                        error = $"spriteSlice.columns ({resolvedColumns}) exceeds the {maximumColumns} full columns available in source image";
                        return false;
                    }

                    int resolvedFrameCount = frameCount > 0 ? frameCount : resolvedColumns * maximumRows;
                    int requiredRows = (resolvedFrameCount + resolvedColumns - 1) / resolvedColumns;
                    if (requiredRows > maximumRows)
                    {
                        error = $"spriteSlice.frameCount ({resolvedFrameCount}) exceeds the {resolvedColumns}x{maximumRows} full-frame grid available in source image";
                        return false;
                    }
                }
                catch (Exception exception)
                {
                    error = $"Unable to validate spriteSlice source image: {exception.Message}";
                    return false;
                }
            }

            spriteSlice = new SpriteSliceSettings(arguments);
            return true;
        }

        private static bool TryConvertToDictionary(object value, out Dictionary<string, object> result)
        {
            if (value is Dictionary<string, object> dictionary)
            {
                result = new Dictionary<string, object>(dictionary);
                return true;
            }
            if (value is IDictionary dictionaryValue)
            {
                result = new Dictionary<string, object>();
                foreach (DictionaryEntry pair in dictionaryValue)
                {
                    if (pair.Key != null)
                        result[pair.Key.ToString()] = pair.Value;
                }
                return true;
            }

            result = null;
            return false;
        }

        private static bool TryGetRequiredPositiveInt(Dictionary<string, object> args, string key,
            out int value, out string error)
        {
            if (!args.TryGetValue(key, out object rawValue) || rawValue == null)
            {
                value = 0;
                error = $"spriteSlice.{key} is required";
                return false;
            }
            return TryGetInteger(rawValue, $"spriteSlice.{key}", 1, out value, out error);
        }

        private static bool TryGetOptionalPositiveInt(Dictionary<string, object> args, string key,
            out int value, out string error)
        {
            if (!args.TryGetValue(key, out object rawValue) || rawValue == null)
            {
                value = 0;
                error = "";
                return true;
            }
            return TryGetInteger(rawValue, $"spriteSlice.{key}", 1, out value, out error);
        }

        private static bool TryGetOptionalNonNegativeInt(Dictionary<string, object> args, string key,
            out int value, out string error)
        {
            if (!args.TryGetValue(key, out object rawValue) || rawValue == null)
            {
                value = 0;
                error = "";
                return true;
            }
            return TryGetInteger(rawValue, $"spriteSlice.{key}", 0, out value, out error);
        }

        private static bool TryGetInteger(object rawValue, string name, int minimum,
            out int value, out string error)
        {
            value = 0;
            error = "";
            try
            {
                double number = Convert.ToDouble(rawValue, System.Globalization.CultureInfo.InvariantCulture);
                if (double.IsNaN(number) || double.IsInfinity(number) || Math.Abs(number - Math.Round(number)) > 0.000001d)
                {
                    error = $"{name} must be an integer";
                    return false;
                }
                if (number < minimum || number > int.MaxValue)
                {
                    error = $"{name} must be at least {minimum}";
                    return false;
                }
                value = (int)number;
                return true;
            }
            catch (Exception exception)
            {
                error = $"{name} must be an integer: {exception.Message}";
                return false;
            }
        }

        private static bool TryGetNormalizedPivot(Dictionary<string, object> args, out string error)
        {
            error = "";
            bool hasX = args.TryGetValue("pivotX", out object rawX) && rawX != null;
            bool hasY = args.TryGetValue("pivotY", out object rawY) && rawY != null;
            if (hasX != hasY)
            {
                error = "spriteSlice.pivotX and spriteSlice.pivotY must be provided together";
                return false;
            }
            if (!hasX)
                return true;

            try
            {
                float x = Convert.ToSingle(rawX, System.Globalization.CultureInfo.InvariantCulture);
                float y = Convert.ToSingle(rawY, System.Globalization.CultureInfo.InvariantCulture);
                if (float.IsNaN(x) || float.IsInfinity(x) || float.IsNaN(y) || float.IsInfinity(y) ||
                    x < 0f || x > 1f || y < 0f || y > 1f)
                {
                    error = "spriteSlice pivot values must be normalized numbers between 0 and 1";
                    return false;
                }
                return true;
            }
            catch (Exception exception)
            {
                error = $"spriteSlice pivot values must be numbers: {exception.Message}";
                return false;
            }
        }

        private static List<Dictionary<string, object>> DescribeSubAssets(string assetPath)
        {
            var result = new List<Dictionary<string, object>>();
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(assetPath))
            {
                if (asset == null) continue;
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out var guid, out long fileID);
                result.Add(new Dictionary<string, object>
                {
                    { "name", asset.name }, { "type", asset.GetType().FullName },
                    { "guid", guid }, { "fileID", fileID }
                });
            }

            return result;
        }

        public static object Refresh(Dictionary<string, object> args)
        {
            return MCPWorkspaceJobRunner.StartAssetRefresh(args);
        }

        internal static object ExecuteRefreshImmediate(Dictionary<string, object> args)
        {
            bool forceUpdate = GetBool(args, "forceUpdate", false);
            bool saveAssets = GetBool(args, "saveAssets", false);
            var assetPaths = GetStringList(args, "assetPaths");
            if (assetPaths.Count > 0 &&
                MCPSceneCommands.TryRejectLoadedSceneAssetMutation(
                    assetPaths, "refresh or reimport assets", out object sceneMutationError))
                return sceneMutationError;

            var importedPaths = new List<string>();
            var forceUpdateSkippedPaths = new List<string>();

            if (assetPaths.Count > 0)
            {
                foreach (string path in OrderTargetedImportPaths(assetPaths))
                {
                    ImportAssetOptions options = GetTargetedImportOptions(path, forceUpdate);
                    if (forceUpdate && (options & ImportAssetOptions.ForceUpdate) == 0)
                        forceUpdateSkippedPaths.Add(path);
                    AssetDatabase.ImportAsset(path, options);
                    importedPaths.Add(path);
                }
            }
            else
            {
                var options = forceUpdate ? ImportAssetOptions.ForceUpdate : ImportAssetOptions.Default;
                AssetDatabase.Refresh(options | ImportAssetOptions.ForceSynchronousImport);
            }

            if (saveAssets)
                AssetDatabase.SaveAssets();

            return new Dictionary<string, object>
            {
                { "success", true },
                { "forceUpdate", forceUpdate },
                { "forceUpdateSkippedPaths", forceUpdateSkippedPaths },
                { "saveAssets", saveAssets },
                { "importedPaths", importedPaths },
                { "refreshMode", assetPaths.Count > 0 ? "targeted" : "full" },
                { "refreshedAllAssets", assetPaths.Count == 0 },
                { "isUpdating", EditorApplication.isUpdating },
                { "isCompiling", EditorApplication.isCompiling },
            };
        }

        internal static ImportAssetOptions GetTargetedImportOptions(string path, bool forceUpdate)
        {
            var options = ImportAssetOptions.ForceSynchronousImport;
            if (forceUpdate && !IsCompilationAssetPath(path))
                options |= ImportAssetOptions.ForceUpdate;
            return options;
        }

        internal static List<string> GetTargetedForceUpdateSkippedPaths(IEnumerable<string> paths,
            bool forceUpdate)
        {
            if (!forceUpdate)
                return new List<string>();
            return OrderTargetedImportPaths(paths).Where(IsCompilationAssetPath).ToList();
        }

        private static bool IsCompilationAssetPath(string path)
        {
            switch (Path.GetExtension(path)?.ToLowerInvariant())
            {
                case ".cs":
                case ".asmdef":
                case ".asmref":
                case ".rsp":
                    return true;
                default:
                    return false;
            }
        }

        internal static List<string> OrderTargetedImportPaths(IEnumerable<string> rawPaths)
        {
            var requestedPaths = new List<string>();
            var requestedSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string rawPath in rawPaths ?? Enumerable.Empty<string>())
            {
                string path = NormalizeAssetPath(rawPath);
                if (!string.IsNullOrEmpty(path) && requestedSet.Add(path))
                    requestedPaths.Add(path);
            }

            var orderedPaths = new List<string>();
            var visitStates = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (string path in requestedPaths)
                AppendTargetedImport(path, requestedSet, visitStates, orderedPaths);
            return orderedPaths;
        }

        private static void AppendTargetedImport(string path, HashSet<string> requestedPaths,
            Dictionary<string, int> visitStates, List<string> orderedPaths)
        {
            if (visitStates.TryGetValue(path, out int state))
            {
                if (state == 2 || state == 1)
                    return;
            }

            visitStates[path] = 1;
            foreach (string dependency in AssetDatabase.GetDependencies(path, false))
            {
                string normalizedDependency = NormalizeAssetPath(dependency);
                if (requestedPaths.Contains(normalizedDependency))
                {
                    AppendTargetedImport(normalizedDependency, requestedPaths, visitStates, orderedPaths);
                }
            }

            visitStates[path] = 2;
            orderedPaths.Add(path);
        }

        public static object ImportUnityPackage(Dictionary<string, object> args)
        {
            return MCPUnityPackageImportWorkflow.Start(args);
        }

        public static object ExportUnityPackage(Dictionary<string, object> args)
        {
            var assetPaths = GetStringList(args, "assetPaths");
            string outputPath = GetString(args, "outputPath");

            if (assetPaths.Count == 0)
                return new { error = "assetPaths is required" };
            if (string.IsNullOrEmpty(outputPath))
                return new { error = "outputPath is required" };

            var normalizedPaths = new List<string>();
            var missingPaths = new List<string>();
            foreach (string assetPath in assetPaths)
            {
                string normalizedPath = NormalizeAssetPath(assetPath);
                if (string.IsNullOrEmpty(normalizedPath))
                    continue;

                if (!AssetExists(normalizedPath))
                    missingPaths.Add(normalizedPath);
                else if (!normalizedPaths.Contains(normalizedPath))
                    normalizedPaths.Add(normalizedPath);
            }

            if (normalizedPaths.Count == 0)
                return new { error = "No valid asset paths were provided" };
            if (missingPaths.Count > 0)
            {
                return new Dictionary<string, object>
                {
                    { "error", "One or more asset paths were not found" },
                    { "missingPaths", missingPaths },
                };
            }

            string fullOutputPath = NormalizeUnityPackageOutputPath(outputPath);
            bool overwrite = GetBool(args, "overwrite", false);
            if (File.Exists(fullOutputPath))
            {
                if (!overwrite)
                    return new { error = $"Output file already exists: '{fullOutputPath}'. Pass overwrite=true to replace it." };

                File.Delete(fullOutputPath);
            }

            string outputDirectory = Path.GetDirectoryName(fullOutputPath);
            if (!string.IsNullOrEmpty(outputDirectory) && !Directory.Exists(outputDirectory))
                Directory.CreateDirectory(outputDirectory);

            bool includeDependencies = GetBool(args, "includeDependencies", true);
            bool recurse = GetBool(args, "recurse", true);
            bool interactive = GetBool(args, "interactive", false);

            var options = ExportPackageOptions.Default;
            if (includeDependencies)
                options |= ExportPackageOptions.IncludeDependencies;
            if (recurse)
                options |= ExportPackageOptions.Recurse;
            if (interactive)
                options |= ExportPackageOptions.Interactive;

            AssetDatabase.ExportPackage(normalizedPaths.ToArray(), fullOutputPath, options);

            bool exported = File.Exists(fullOutputPath);
            long size = exported ? new FileInfo(fullOutputPath).Length : 0;
            return new Dictionary<string, object>
            {
                { "success", exported },
                { "assetPaths", normalizedPaths },
                { "outputPath", fullOutputPath },
                { "size", size },
                { "includeDependencies", includeDependencies },
                { "recurse", recurse },
                { "interactive", interactive },
            };
        }

        public static object Delete(Dictionary<string, object> args)
        {
            string path = NormalizeAssetPath(args != null && args.ContainsKey("path")
                ? args["path"]?.ToString()
                : "");
            if (string.IsNullOrEmpty(path))
                return new { error = "path is required" };
            if (MCPSceneCommands.TryRejectLoadedSceneAssetMutation(
                    new[] { path }, "delete assets", out object sceneMutationError))
                return sceneMutationError;

            bool deleted = AssetDatabase.DeleteAsset(path);
            if (deleted)
                AssetDatabase.SaveAssets();

            return new Dictionary<string, object>
            {
                { "success", deleted },
                { "path", path },
                { "savedAssets", deleted },
            };
        }

        public static object Rename(Dictionary<string, object> args)
        {
            string path = NormalizeAssetPath(GetString(args, "path"));
            string newName = GetString(args, "newName");
            bool dryRun = GetBool(args, "dryRun", false);

            if (string.IsNullOrEmpty(path))
                return new { error = "path is required" };
            if (string.IsNullOrEmpty(newName))
                return new { error = "newName is required" };
            if (newName.Contains("/") || newName.Contains("\\"))
                return new { error = "newName must be a file or folder name, not a path" };
            if (!AssetExists(path))
                return new { error = $"Asset not found at '{path}'" };

            string oldGuid = AssetDatabase.AssetPathToGUID(path);
            string oldMetaPath = GetMetaPath(path);
            bool oldMetaExists = File.Exists(GetAbsolutePath(oldMetaPath));
            bool isFolder = AssetDatabase.IsValidFolder(path);
            string directory = Path.GetDirectoryName(path)?.Replace('\\', '/') ?? "";
            string extension = isFolder ? "" : Path.GetExtension(path);
            string newExtension = isFolder ? "" : Path.GetExtension(newName);
            string oldAssetName = isFolder ? "" : Path.GetFileNameWithoutExtension(path);

            if (!isFolder && !string.IsNullOrEmpty(newExtension) &&
                !string.Equals(newExtension, extension, StringComparison.OrdinalIgnoreCase))
            {
                return new { error = $"Changing file extension is not supported: '{extension}' to '{newExtension}'" };
            }

            string renameName = isFolder ? newName : Path.GetFileNameWithoutExtension(newName);
            string expectedPath = string.IsNullOrEmpty(directory)
                ? renameName + extension
                : directory + "/" + renameName + extension;

            if (!string.Equals(path, expectedPath, StringComparison.OrdinalIgnoreCase) &&
                AssetExists(expectedPath))
            {
                return new { error = $"Target asset already exists at '{expectedPath}'" };
            }

            if (dryRun)
            {
                return new Dictionary<string, object>
                {
                    { "success", true },
                    { "dryRun", true },
                    { "oldPath", path },
                    { "expectedPath", expectedPath },
                    { "oldGuid", oldGuid },
                    { "oldMetaPath", oldMetaPath },
                    { "expectedMetaPath", GetMetaPath(expectedPath) },
                    { "oldMetaExists", oldMetaExists },
                };
            }
            if (MCPSceneCommands.TryRejectLoadedSceneAssetMutation(
                    new[] { path }, "rename assets", out object sceneMutationError))
                return sceneMutationError;

            string error = AssetDatabase.RenameAsset(path, renameName);
            if (!string.IsNullOrEmpty(error))
                return new { error };

            SpriteNameSynchronizationResult spriteNameSynchronization =
                SynchronizeSpriteNames(expectedPath, oldAssetName, renameName);
            if (spriteNameSynchronization.Attempted && spriteNameSynchronization.Success == false)
            {
                string rollbackError = AssetDatabase.RenameAsset(expectedPath, oldAssetName);
                bool pathRolledBack = string.IsNullOrEmpty(rollbackError);
                SpriteNameSynchronizationResult restoration = pathRolledBack
                    ? SynchronizeSpriteNames(path, renameName, oldAssetName)
                    : new SpriteNameSynchronizationResult();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                return new Dictionary<string, object>
                {
                    { "success", false },
                    {
                        "error",
                        spriteNameSynchronization.Error ??
                        $"Failed to synchronize Sprite names at '{expectedPath}'."
                    },
                    { "oldPath", path },
                    { "expectedPath", expectedPath },
                    { "pathRolledBack", pathRolledBack },
                    { "spriteNamesRolledBack", pathRolledBack && restoration.Success },
                    { "rollbackError", rollbackError ?? "" },
                };
            }
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            string newPath = AssetDatabase.GUIDToAssetPath(oldGuid);
            string newGuid = string.IsNullOrEmpty(newPath) ? "" : AssetDatabase.AssetPathToGUID(newPath);
            bool guidChanged = !string.Equals(oldGuid, newGuid, StringComparison.Ordinal);
            string newMetaPath = string.IsNullOrEmpty(newPath) ? "" : GetMetaPath(newPath);
            bool newMetaExists = !string.IsNullOrEmpty(newMetaPath) && File.Exists(GetAbsolutePath(newMetaPath));

            return new Dictionary<string, object>
            {
                { "success", true },
                { "dryRun", false },
                { "oldPath", path },
                { "newPath", newPath },
                { "expectedPath", expectedPath },
                { "actualPathMatchesExpected", string.Equals(newPath, expectedPath, StringComparison.OrdinalIgnoreCase) },
                { "oldGuid", oldGuid },
                { "newGuid", newGuid },
                { "guidChanged", guidChanged },
                { "metaPreserved", !guidChanged },
                { "oldMetaPath", oldMetaPath },
                { "newMetaPath", newMetaPath },
                { "oldMetaExists", oldMetaExists },
                { "newMetaExists", newMetaExists },
                { "spriteNameSynchronizationAttempted", spriteNameSynchronization.Attempted },
                { "synchronizedSpriteNames", spriteNameSynchronization.Success },
                { "synchronizedSpriteCount", spriteNameSynchronization.SynchronizedCount },
                { "spriteImportMode", spriteNameSynchronization.SpriteImportMode },
                { "spriteNameSynchronizationError", spriteNameSynchronization.Error ?? "" },
                {
                    "synchronizedSingleSpriteName",
                    spriteNameSynchronization.SpriteImportMode == nameof(SpriteImportMode.Single) &&
                    spriteNameSynchronization.Success
                },
                {
                    "synchronizedMultipleSpriteNames",
                    spriteNameSynchronization.SpriteImportMode == nameof(SpriteImportMode.Multiple) &&
                    spriteNameSynchronization.Success
                },
                { "subAssets", DescribeSubAssets(newPath) },
            };
        }

        private static SpriteNameSynchronizationResult SynchronizeSpriteNames(string assetPath,
            string oldAssetName, string newAssetName)
        {
            var result = new SpriteNameSynchronizationResult();
            if (AssetImporter.GetAtPath(assetPath) is not TextureImporter importer ||
                importer.textureType != TextureImporterType.Sprite)
                return result;

            result.Attempted = true;
            result.SpriteImportMode = importer.spriteImportMode.ToString();
            if (importer.spriteImportMode == SpriteImportMode.Single)
            {
                result.Success = SynchronizeSingleSpriteName(assetPath, newAssetName);
                result.SynchronizedCount = result.Success
                    ? AssetDatabase.LoadAllAssetsAtPath(assetPath).OfType<Sprite>().Count()
                    : 0;
                if (result.Success == false)
                    result.Error = $"Failed to synchronize Single Sprite internal names at '{assetPath}'.";
                return result;
            }

            if (importer.spriteImportMode == SpriteImportMode.Multiple)
            {
                result.Success = MCPSpriteSheetCommands.TryRenameSpritePrefixPreservingIds(
                    assetPath, oldAssetName, newAssetName, out int synchronizedCount,
                    out string synchronizationError);
                result.SynchronizedCount = synchronizedCount;
                result.Error = synchronizationError;
                return result;
            }

            result.Success = true;
            return result;
        }

        private static bool SynchronizeSingleSpriteName(string assetPath, string spriteName)
        {
            if (AssetImporter.GetAtPath(assetPath) is not TextureImporter importer ||
                importer.textureType != TextureImporterType.Sprite ||
                importer.spriteImportMode != SpriteImportMode.Single)
                return false;

            var serializedImporter = new SerializedObject(importer);
            serializedImporter.Update();
            var nameTable = serializedImporter.FindProperty("m_InternalIDToNameTable") ??
                            serializedImporter.FindProperty("internalIDToNameTable");
            SetSerializedArrayEntryNames(nameTable, spriteName, "second", "name");

            var spriteSheet = serializedImporter.FindProperty("m_SpriteSheet");
            SetSerializedArrayEntryNames(spriteSheet?.FindPropertyRelative("m_Sprites"), spriteName,
                "m_Name", "name");
            SetSerializedArrayEntryNames(spriteSheet?.FindPropertyRelative("m_NameFileIdTable"), spriteName,
                "first", "name");
            serializedImporter.ApplyModifiedPropertiesWithoutUndo();

            foreach (var sprite in AssetDatabase.LoadAllAssetsAtPath(assetPath).OfType<Sprite>())
            {
                sprite.name = spriteName;
                EditorUtility.SetDirty(sprite);
            }

            importer.SaveAndReimport();
            var importedSprites = AssetDatabase.LoadAllAssetsAtPath(assetPath).OfType<Sprite>().ToArray();
            if (importedSprites.Length == 0 || importedSprites.Any(sprite => sprite.name != spriteName))
                return false;

            importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
            if (importer == null)
                return false;
            serializedImporter = new SerializedObject(importer);
            serializedImporter.Update();
            nameTable = serializedImporter.FindProperty("m_InternalIDToNameTable") ??
                        serializedImporter.FindProperty("internalIDToNameTable");
            spriteSheet = serializedImporter.FindProperty("m_SpriteSheet");
            return SerializedArrayEntryNamesMatch(nameTable, spriteName, "second", "name") &&
                   SerializedArrayEntryNamesMatch(spriteSheet?.FindPropertyRelative("m_Sprites"), spriteName,
                       "m_Name", "name") &&
                   SerializedArrayEntryNamesMatch(spriteSheet?.FindPropertyRelative("m_NameFileIdTable"),
                       spriteName, "first", "name");
        }

        private static void SetSerializedArrayEntryNames(SerializedProperty entries, string value,
            params string[] nameProperties)
        {
            if (entries == null || !entries.isArray)
                return;

            for (int index = 0; index < entries.arraySize; index++)
            {
                var name = FindRelativeProperty(entries.GetArrayElementAtIndex(index), nameProperties);
                if (name != null && name.propertyType == SerializedPropertyType.String)
                    name.stringValue = value;
            }
        }

        private static bool SerializedArrayEntryNamesMatch(SerializedProperty entries, string expected,
            params string[] nameProperties)
        {
            if (entries == null || !entries.isArray)
                return true;

            for (int index = 0; index < entries.arraySize; index++)
            {
                var name = FindRelativeProperty(entries.GetArrayElementAtIndex(index), nameProperties);
                if (name != null && name.propertyType == SerializedPropertyType.String &&
                    !string.Equals(name.stringValue, expected, StringComparison.Ordinal))
                    return false;
            }

            return true;
        }

        private static SerializedProperty FindRelativeProperty(SerializedProperty parent,
            params string[] relativePaths)
        {
            foreach (string relativePath in relativePaths)
            {
                var property = parent.FindPropertyRelative(relativePath);
                if (property != null)
                    return property;
            }

            return null;
        }

        public static object Move(Dictionary<string, object> args)
        {
            if (!MCPExecutionOptions.TryParse(args, out var execution, out string executionError))
                return new { success = false, error = executionError };
            bool dryRun = GetBool(args, "dryRun", false);
            if (!TryPrepareMoves(args, out var entries, out object preparationError))
                return preparationError;

            if (dryRun)
                return BuildMoveResult(entries, execution, true, new List<string>());
            if (MCPSceneCommands.TryRejectLoadedSceneAssetMutation(
                    entries.Select(entry => entry.OldPath), "move assets",
                    out object sceneMutationError))
                return sceneMutationError;
            return ExecutePreparedMoves(entries, execution);
        }

        public static void MoveDeferred(Dictionary<string, object> args, Action<object> resolve,
            Action<object> progress)
        {
            if (!MCPExecutionOptions.TryParse(args, out var execution, out string executionError))
            {
                resolve(new { success = false, error = executionError });
                return;
            }
            if (!TryPrepareMoves(args, out var entries, out object preparationError))
            {
                resolve(preparationError);
                return;
            }
            if (GetBool(args, "dryRun", false))
            {
                resolve(BuildMoveResult(entries, execution, true, new List<string>()));
                return;
            }
            if (MCPSceneCommands.TryRejectLoadedSceneAssetMutation(
                    entries.Select(entry => entry.OldPath), "move assets",
                    out object sceneMutationError))
            {
                resolve(sceneMutationError);
                return;
            }
            if (execution.ResolveMode(entries.Count) == MCPExecutionMode.Immediate)
            {
                resolve(ExecutePreparedMoves(entries, execution));
                return;
            }

            int nextIndex = 0;
            double startedAt = EditorApplication.timeSinceStartup;
            var errors = new List<string>();
            EditorApplication.CallbackFunction tick = null;
            Action<object> complete = result =>
            {
                if (tick != null)
                    EditorApplication.update -= tick;
                resolve(result);
            };
            tick = () =>
            {
                int elapsedMs = (int)((EditorApplication.timeSinceStartup - startedAt) * 1000d);
                if (elapsedMs >= execution.TimeoutMs)
                {
                    string timeoutError = $"Asset moves timed out after {execution.TimeoutMs} ms";
                    errors.Add(timeoutError);
                    var rollbackErrors = RollbackMovesAndRestoreSpriteNames(entries);
                    errors.AddRange(rollbackErrors);
                    FinishAssetMoves();
                    complete(BuildMoveFailure(entries, execution, timeoutError, errors));
                    return;
                }

                double frameStartedAt = EditorApplication.timeSinceStartup;
                int processedThisFrame = 0;
                AssetDatabase.StartAssetEditing();
                try
                {
                    while (nextIndex < entries.Count)
                    {
                        var entry = entries[nextIndex++];
                        string error = AssetDatabase.MoveAsset(entry.OldPath, entry.TargetPath);
                        if (string.IsNullOrEmpty(error))
                            entry.Moved = true;
                        else
                        {
                            entry.Error = error;
                            errors.Add($"Move {entry.Index} failed: {error}");
                            if (!execution.ContinueOnError)
                                break;
                        }

                        processedThisFrame++;
                        progress?.Invoke(BuildMoveProgress(entries, execution, nextIndex, elapsedMs));
                        double frameElapsedMs = (EditorApplication.timeSinceStartup - frameStartedAt) * 1000d;
                        if (processedThisFrame >= execution.OperationsPerFrame ||
                            frameElapsedMs >= execution.FrameBudgetMs)
                            break;
                    }
                }
                catch (Exception exception)
                {
                    errors.Add(exception.Message);
                }
                finally
                {
                    AssetDatabase.StopAssetEditing();
                }

                if (errors.Count > 0 && !execution.ContinueOnError)
                {
                    errors.AddRange(RollbackMovesAndRestoreSpriteNames(entries));
                    FinishAssetMoves();
                    complete(BuildMoveFailure(entries, execution, errors[0], errors));
                    return;
                }
                if (nextIndex < entries.Count)
                    return;

                SynchronizeMovedSpriteNames(entries, errors);
                if (errors.Count > 0 && !execution.ContinueOnError)
                {
                    errors.AddRange(RollbackMovesAndRestoreSpriteNames(entries));
                    FinishAssetMoves();
                    complete(BuildMoveFailure(entries, execution, errors[0], errors));
                    return;
                }

                FinishAssetMoves();
                if (errors.Count > 0)
                    complete(BuildMoveFailure(entries, execution, "One or more asset moves failed", errors));
                else
                    complete(BuildMoveResult(entries, execution, false, errors));
            };
            EditorApplication.update += tick;
            tick();
        }

        private static bool TryPrepareMoves(Dictionary<string, object> args, out List<BatchMoveEntry> entries,
            out object errorResult)
        {
            entries = new List<BatchMoveEntry>();
            errorResult = null;
            List<Dictionary<string, object>> requestedMoves = GetDictionaryList(args, "moves");
            if (requestedMoves.Count == 0)
            {
                errorResult = new { error = "moves must contain at least one move request" };
                return false;
            }

            for (int index = 0; index < requestedMoves.Count; index++)
            {
                var request = requestedMoves[index];
                string path = NormalizeAssetPath(GetString(request, "path"));
                string destinationPath = NormalizeAssetPath(GetFirstString(request, "destinationPath",
                    "destinationFolder"));
                if (string.IsNullOrEmpty(path))
                    return FailMovePreparation(index, "path is required", out errorResult);
                if (string.IsNullOrEmpty(destinationPath))
                    return FailMovePreparation(index,
                        "destinationPath or destinationFolder is required", out errorResult);
                if (!AssetExists(path))
                    return FailMovePreparation(index, $"Asset not found at '{path}'", out errorResult);

                string targetPath = NormalizeMoveTargetPath(path, destinationPath);
                string targetDirectory = Path.GetDirectoryName(targetPath)?.Replace('\\', '/') ?? "";
                bool sourceIsFolder = AssetDatabase.IsValidFolder(path);
                if (!sourceIsFolder && !AssetDatabase.IsValidFolder(destinationPath))
                {
                    string sourceExtension = Path.GetExtension(path);
                    string targetExtension = Path.GetExtension(targetPath);
                    if (string.IsNullOrEmpty(targetExtension))
                        return FailMovePreparation(index,
                            "destinationPath must be an existing folder or include the asset file extension",
                            out errorResult);
                    if (!string.Equals(sourceExtension, targetExtension, StringComparison.OrdinalIgnoreCase))
                        return FailMovePreparation(index,
                            $"Changing file extension is not supported: '{sourceExtension}' to '{targetExtension}'",
                            out errorResult);
                }

                if (!string.IsNullOrEmpty(targetDirectory) && !AssetDatabase.IsValidFolder(targetDirectory))
                    return FailMovePreparation(index, $"Target directory does not exist: '{targetDirectory}'",
                        out errorResult);
                if (string.Equals(path, targetPath, StringComparison.OrdinalIgnoreCase))
                    return FailMovePreparation(index, "Source and target paths are the same", out errorResult);
                if (AssetExists(targetPath))
                    return FailMovePreparation(index, $"Target asset already exists at '{targetPath}'", out errorResult);

                entries.Add(new BatchMoveEntry
                {
                    Index = index,
                    OldPath = path,
                    RequestedDestinationPath = destinationPath,
                    TargetPath = targetPath,
                    OldGuid = AssetDatabase.AssetPathToGUID(path),
                    OldMetaPath = GetMetaPath(path),
                    OldMetaExists = File.Exists(GetAbsolutePath(GetMetaPath(path))),
                });
            }

            for (int index = 0; index < entries.Count; index++)
            {
                for (int otherIndex = index + 1; otherIndex < entries.Count; otherIndex++)
                {
                    if (string.Equals(entries[index].OldPath, entries[otherIndex].OldPath,
                            StringComparison.OrdinalIgnoreCase))
                        return FailMovePreparation(otherIndex,
                            $"Duplicate source path '{entries[otherIndex].OldPath}'", out errorResult);
                    if (string.Equals(entries[index].TargetPath, entries[otherIndex].TargetPath,
                            StringComparison.OrdinalIgnoreCase))
                        return FailMovePreparation(otherIndex,
                            $"Duplicate target path '{entries[otherIndex].TargetPath}'", out errorResult);
                }
            }

            return true;
        }

        private static bool FailMovePreparation(int index, string error, out object errorResult)
        {
            errorResult = BatchMoveValidationError(index, error);
            return false;
        }

        private static object ExecutePreparedMoves(List<BatchMoveEntry> entries, MCPExecutionOptions execution)
        {
            var errors = new List<string>();
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var entry in entries)
                {
                    string error = AssetDatabase.MoveAsset(entry.OldPath, entry.TargetPath);
                    if (string.IsNullOrEmpty(error))
                    {
                        entry.Moved = true;
                        continue;
                    }

                    entry.Error = error;
                    errors.Add($"Move {entry.Index} failed: {error}");
                    if (!execution.ContinueOnError)
                        break;
                }
            }
            catch (Exception exception)
            {
                errors.Add(exception.Message);
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            if (errors.Count == 0 || execution.ContinueOnError)
                SynchronizeMovedSpriteNames(entries, errors);
            if (errors.Count > 0 && !execution.ContinueOnError)
                errors.AddRange(RollbackMovesAndRestoreSpriteNames(entries));
            FinishAssetMoves();
            return errors.Count == 0
                ? BuildMoveResult(entries, execution, false, errors)
                : BuildMoveFailure(entries, execution, errors[0], errors);
        }

        private static List<string> RollbackMoves(List<BatchMoveEntry> entries)
        {
            var rollbackErrors = new List<string>();
            AssetDatabase.StartAssetEditing();
            try
            {
                for (int index = entries.Count - 1; index >= 0; index--)
                {
                    var entry = entries[index];
                    if (!entry.Moved || entry.RolledBack)
                        continue;
                    string error = AssetDatabase.MoveAsset(entry.TargetPath, entry.OldPath);
                    if (string.IsNullOrEmpty(error))
                        entry.RolledBack = true;
                    else
                        rollbackErrors.Add($"Rollback {entry.Index} failed: {error}");
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }
            return rollbackErrors;
        }

        private static List<string> RollbackMovesAndRestoreSpriteNames(List<BatchMoveEntry> entries)
        {
            var rollbackErrors = RollbackMoves(entries);
            foreach (var entry in entries)
            {
                if (!entry.RolledBack || !entry.SpriteNameSynchronizationAttempted)
                    continue;

                try
                {
                    string oldAssetName = Path.GetFileNameWithoutExtension(entry.OldPath);
                    string movedAssetName = Path.GetFileNameWithoutExtension(entry.TargetPath);
                    SpriteNameSynchronizationResult restoration =
                        SynchronizeSpriteNames(entry.OldPath, movedAssetName, oldAssetName);
                    ApplySpriteNameSynchronization(entry, restoration);
                    if (!entry.SynchronizedSpriteNames)
                    {
                        rollbackErrors.Add(
                            $"Rollback {entry.Index} restored the asset path but not its Sprite internal names.");
                    }
                }
                catch (Exception exception)
                {
                    rollbackErrors.Add(
                        $"Rollback {entry.Index} could not restore its Sprite internal names: {exception.Message}");
                }
            }

            return rollbackErrors;
        }

        private static void SynchronizeMovedSpriteNames(List<BatchMoveEntry> entries, List<string> errors)
        {
            foreach (var entry in entries)
            {
                if (!entry.Moved || entry.RolledBack ||
                    string.Equals(Path.GetFileNameWithoutExtension(entry.OldPath),
                        Path.GetFileNameWithoutExtension(entry.TargetPath), StringComparison.Ordinal))
                    continue;
                if (AssetImporter.GetAtPath(entry.TargetPath) is not TextureImporter importer ||
                    importer.textureType != TextureImporterType.Sprite)
                    continue;

                entry.SpriteNameSynchronizationAttempted = true;
                entry.SpriteImportMode = importer.spriteImportMode.ToString();
                entry.SingleSpriteNameSynchronizationAttempted =
                    importer.spriteImportMode == SpriteImportMode.Single;
                try
                {
                    string oldAssetName = Path.GetFileNameWithoutExtension(entry.OldPath);
                    string newAssetName = Path.GetFileNameWithoutExtension(entry.TargetPath);
                    SpriteNameSynchronizationResult synchronization =
                        SynchronizeSpriteNames(entry.TargetPath, oldAssetName, newAssetName);
                    ApplySpriteNameSynchronization(entry, synchronization);
                    if (entry.SynchronizedSpriteNames)
                        continue;

                    entry.Error =
                        $"Move {entry.Index} left Sprite internal names unsynchronized at '{entry.TargetPath}': " +
                        (entry.SpriteNameSynchronizationError ?? "unknown synchronization error");
                }
                catch (Exception exception)
                {
                    entry.Error =
                        $"Move {entry.Index} could not synchronize Sprite internal names at " +
                        $"'{entry.TargetPath}': {exception.Message}";
                }

                errors.Add(entry.Error);
            }
        }

        private static void ApplySpriteNameSynchronization(BatchMoveEntry entry,
            SpriteNameSynchronizationResult synchronization)
        {
            entry.SpriteNameSynchronizationAttempted = synchronization.Attempted;
            entry.SynchronizedSpriteNames = synchronization.Success;
            entry.SynchronizedSpriteCount = synchronization.SynchronizedCount;
            entry.SpriteImportMode = synchronization.SpriteImportMode;
            entry.SpriteNameSynchronizationError = synchronization.Error;
            entry.SingleSpriteNameSynchronizationAttempted =
                synchronization.SpriteImportMode == nameof(SpriteImportMode.Single) &&
                synchronization.Attempted;
            entry.SynchronizedSingleSpriteName =
                synchronization.SpriteImportMode == nameof(SpriteImportMode.Single) &&
                synchronization.Success;
            entry.SynchronizedMultipleSpriteNames =
                synchronization.SpriteImportMode == nameof(SpriteImportMode.Multiple) &&
                synchronization.Success;
        }

        private static void FinishAssetMoves()
        {
            AssetDatabase.SaveAssets();
        }

        private static Dictionary<string, object> BuildMoveResult(List<BatchMoveEntry> entries,
            MCPExecutionOptions execution, bool dryRun, List<string> errors)
        {
            return new Dictionary<string, object>
            {
                { "success", errors.Count == 0 },
                { "dryRun", dryRun },
                { "moveCount", entries.Count },
                { "movedCount", entries.FindAll(entry => entry.Moved && !entry.RolledBack).Count },
                { "failedCount", entries.FindAll(entry => !string.IsNullOrEmpty(entry.Error)).Count },
                { "moves", entries.ConvertAll(CreateBatchMoveResult) },
                { "execution", execution.ToResult(entries.Count) },
            };
        }

        private static Dictionary<string, object> BuildMoveFailure(List<BatchMoveEntry> entries,
            MCPExecutionOptions execution, string error, List<string> errors)
        {
            var result = BuildMoveResult(entries, execution, false, errors);
            result["success"] = false;
            result["error"] = error;
            result["errors"] = errors;
            result["rolledBack"] = entries.TrueForAll(entry => !entry.Moved || entry.RolledBack);
            return result;
        }

        private static Dictionary<string, object> BuildMoveProgress(List<BatchMoveEntry> entries,
            MCPExecutionOptions execution, int nextIndex, int elapsedMs)
        {
            return new Dictionary<string, object>
            {
                { "phase", "moving" },
                { "moveCount", entries.Count },
                { "processedCount", nextIndex },
                { "elapsedMs", elapsedMs },
                { "execution", execution.ToResult(entries.Count) },
            };
        }

        public static object CreatePrefab(Dictionary<string, object> args)
        {
            string goPath = args.ContainsKey("gameObjectPath") ? args["gameObjectPath"].ToString() : "";
            string savePath = args.ContainsKey("savePath") ? args["savePath"].ToString() : "";

            var go = MCPGameObjectCommands.FindGameObject(args);
            if (go == null) return new { error = "GameObject not found" };

            if (string.IsNullOrEmpty(savePath))
                return new { error = "savePath is required" };

            // Ensure directory exists
            string dir = Path.GetDirectoryName(savePath)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(dir) && !AssetDatabase.IsValidFolder(dir))
            {
                string[] parts = dir.Split('/');
                string current = parts[0];
                for (int i = 1; i < parts.Length; i++)
                {
                    string next = current + "/" + parts[i];
                    if (!AssetDatabase.IsValidFolder(next))
                        AssetDatabase.CreateFolder(current, parts[i]);
                    current = next;
                }
            }

            var prefab = PrefabUtility.SaveAsPrefabAsset(go, savePath);
            return new Dictionary<string, object>
            {
                { "success", prefab != null },
                { "path", savePath },
                { "name", prefab?.name },
            };
        }

        public static object InstantiatePrefab(Dictionary<string, object> args)
        {
            string prefabPath = args.ContainsKey("prefabPath") ? args["prefabPath"].ToString() : "";
            if (string.IsNullOrEmpty(prefabPath))
                return new { error = "prefabPath is required" };

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null) return new { error = $"Prefab not found at {prefabPath}" };

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            if (instance == null) return new { error = "Failed to instantiate prefab" };

            if (args.ContainsKey("name"))
                instance.name = args["name"].ToString();

            if (args.ContainsKey("position"))
                instance.transform.position = MCPGameObjectCommands.DictToVector3(args["position"] as Dictionary<string, object>);

            if (args.ContainsKey("rotation"))
                instance.transform.eulerAngles = MCPGameObjectCommands.DictToVector3(args["rotation"] as Dictionary<string, object>);

            if (args.ContainsKey("parent"))
            {
                var parent = GameObject.Find(args["parent"].ToString());
                if (parent != null) instance.transform.SetParent(parent.transform);
            }

            Undo.RegisterCreatedObjectUndo(instance, $"Instantiate {prefab.name}");

            var result = new Dictionary<string, object>
            {
                { "success", true },
                { "name", instance.name },
                { "instanceId", VmObjectId.Get(instance) },
            };
            MCPTransformSerialization.AddWorld(result, instance.transform);
            return result;
        }

        public static object CreateMaterial(Dictionary<string, object> args)
        {
            string path = args.ContainsKey("path") ? args["path"].ToString() : "";
            string shaderName = args.ContainsKey("shader") ? args["shader"].ToString() : "Standard";

            if (string.IsNullOrEmpty(path))
                return new { error = "path is required" };

            var shader = Shader.Find(shaderName);
            if (shader == null) return new { error = $"Shader '{shaderName}' not found" };

            var material = new Material(shader);

            if (args.ContainsKey("color"))
            {
                var cd = args["color"] as Dictionary<string, object>;
                if (cd != null)
                {
                    material.color = new Color(
                        Convert.ToSingle(cd.GetValueOrDefault("r", 1f)),
                        Convert.ToSingle(cd.GetValueOrDefault("g", 1f)),
                        Convert.ToSingle(cd.GetValueOrDefault("b", 1f)),
                        Convert.ToSingle(cd.GetValueOrDefault("a", 1f))
                    );
                }
            }

            // Ensure directory exists (normalize backslashes from Path.GetDirectoryName on Windows)
            string dir = Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(dir) && !AssetDatabase.IsValidFolder(dir))
            {
                string[] parts = dir.Split('/');
                string current = parts[0];
                for (int i = 1; i < parts.Length; i++)
                {
                    string next = current + "/" + parts[i];
                    if (!AssetDatabase.IsValidFolder(next))
                        AssetDatabase.CreateFolder(current, parts[i]);
                    current = next;
                }
            }

            AssetDatabase.CreateAsset(material, path);
            AssetDatabase.SaveAssets();

            return new { success = true, path, shader = shaderName };
        }

        private static string GetString(Dictionary<string, object> args, string key)
        {
            return args != null && args.ContainsKey(key) ? args[key]?.ToString() : "";
        }

        private static string GetFirstString(Dictionary<string, object> args, params string[] keys)
        {
            if (args == null || keys == null)
                return "";

            foreach (string key in keys)
            {
                string value = GetString(args, key);
                if (!string.IsNullOrEmpty(value))
                    return value;
            }

            return "";
        }

        private static bool GetBool(Dictionary<string, object> args, string key, bool defaultValue)
        {
            if (args == null || !args.ContainsKey(key) || args[key] == null)
                return defaultValue;

            try
            {
                return Convert.ToBoolean(args[key]);
            }
            catch
            {
                return defaultValue;
            }
        }

        private static List<string> GetStringList(Dictionary<string, object> args, string key)
        {
            var result = new List<string>();
            if (args == null || !args.ContainsKey(key) || args[key] == null)
                return result;

            object value = args[key];
            if (value is string stringValue)
            {
                if (!string.IsNullOrWhiteSpace(stringValue))
                    result.Add(stringValue);
                return result;
            }

            var enumerable = value as IEnumerable;
            if (enumerable == null)
                return result;

            foreach (object item in enumerable)
            {
                if (item == null)
                    continue;

                string text = item.ToString();
                if (!string.IsNullOrWhiteSpace(text))
                    result.Add(text);
            }

            return result;
        }

        private static List<Dictionary<string, object>> GetDictionaryList(Dictionary<string, object> args, string key)
        {
            var result = new List<Dictionary<string, object>>();
            if (args == null || !args.TryGetValue(key, out object value) || !(value is IEnumerable enumerable))
                return result;

            foreach (object item in enumerable)
            {
                if (item is Dictionary<string, object> dictionary)
                {
                    result.Add(dictionary);
                    continue;
                }

                if (!(item is IDictionary dictionaryValue))
                    continue;

                var converted = new Dictionary<string, object>();
                foreach (DictionaryEntry pair in dictionaryValue)
                {
                    if (pair.Key != null)
                        converted[pair.Key.ToString()] = pair.Value;
                }

                result.Add(converted);
            }

            return result;
        }

        private static bool TryGetDictionaryList(Dictionary<string, object> args, string key,
            out List<Dictionary<string, object>> result, out string error)
        {
            result = new List<Dictionary<string, object>>();
            error = "";
            if (args == null || !args.TryGetValue(key, out object value) || value == null)
                return true;
            if (value is string || value is IDictionary || !(value is IEnumerable enumerable))
            {
                error = $"{key} must be an array";
                return false;
            }

            int index = 0;
            foreach (object item in enumerable)
            {
                if (item is Dictionary<string, object> dictionary)
                {
                    result.Add(dictionary);
                }
                else if (item is IDictionary dictionaryValue)
                {
                    var converted = new Dictionary<string, object>();
                    foreach (DictionaryEntry pair in dictionaryValue)
                    {
                        if (pair.Key != null)
                            converted[pair.Key.ToString()] = pair.Value;
                    }
                    result.Add(converted);
                }
                else
                {
                    error = $"{key}[{index}] must be an object";
                    return false;
                }
                index++;
            }
            return true;
        }

        private static bool TryGetDictionary(Dictionary<string, object> args, string key,
            out Dictionary<string, object> result, out string error)
        {
            result = new Dictionary<string, object>();
            error = "";
            if (args == null || !args.TryGetValue(key, out object value) || value == null)
                return true;
            if (value is Dictionary<string, object> dictionary)
            {
                result = dictionary;
                return true;
            }
            if (!(value is IDictionary dictionaryValue))
            {
                error = $"{key} must be an object";
                return false;
            }

            foreach (DictionaryEntry pair in dictionaryValue)
            {
                if (pair.Key != null)
                    result[pair.Key.ToString()] = pair.Value;
            }
            return true;
        }

        private static bool AssetExists(string path)
        {
            return AssetDatabase.IsValidFolder(path) || AssetDatabase.LoadMainAssetAtPath(path) != null;
        }

        private static string NormalizeAssetPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return "";

            return path.Replace('\\', '/').Trim().Trim('/');
        }

        private static string GetMetaPath(string assetPath)
        {
            return string.IsNullOrEmpty(assetPath) ? "" : NormalizeAssetPath(assetPath) + ".meta";
        }

        private static string GetAbsolutePath(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath))
                return "";

            if (Path.IsPathRooted(assetPath))
                return Path.GetFullPath(assetPath);

            return Path.GetFullPath(Path.Combine(GetProjectRoot(), NormalizeAssetPath(assetPath)));
        }

        private static string NormalizeUnityPackageOutputPath(string outputPath)
        {
            string normalized = outputPath.Replace('\\', '/').Trim();
            if (!string.Equals(Path.GetExtension(normalized), ".unitypackage", StringComparison.OrdinalIgnoreCase))
                normalized += ".unitypackage";

            if (!Path.IsPathRooted(normalized))
                normalized = Path.Combine(GetProjectRoot(), normalized);

            return Path.GetFullPath(normalized);
        }

        private static string NormalizeUnityPackageInputPath(string packagePath)
        {
            string normalized = packagePath.Replace('\\', '/').Trim();
            if (!Path.IsPathRooted(normalized))
                normalized = Path.Combine(GetProjectRoot(), normalized);
            return Path.GetFullPath(normalized);
        }

        private static string GetProjectRoot()
        {
            return Directory.GetParent(Application.dataPath).FullName;
        }

        private static string NormalizeMoveTargetPath(string sourcePath, string destinationPath)
        {
            destinationPath = destinationPath.Replace('\\', '/');
            if (AssetDatabase.IsValidFolder(destinationPath))
                return destinationPath.TrimEnd('/') + "/" + Path.GetFileName(sourcePath);

            return destinationPath;
        }

        private static object BatchMoveValidationError(int index, string error)
        {
            return new { error = $"Move {index} is invalid: {error}" };
        }

        private static Dictionary<string, object> CreateBatchMoveResult(BatchMoveEntry entry)
        {
            string currentPath = AssetDatabase.GUIDToAssetPath(entry.OldGuid);
            string currentGuid = string.IsNullOrEmpty(currentPath) ? "" : AssetDatabase.AssetPathToGUID(currentPath);
            string currentMetaPath = string.IsNullOrEmpty(currentPath) ? "" : GetMetaPath(currentPath);
            return new Dictionary<string, object>
            {
                { "index", entry.Index },
                { "oldPath", entry.OldPath },
                { "requestedDestinationPath", entry.RequestedDestinationPath },
                { "targetPath", entry.TargetPath },
                { "currentPath", currentPath },
                { "oldGuid", entry.OldGuid },
                { "currentGuid", currentGuid },
                { "guidChanged", !string.Equals(entry.OldGuid, currentGuid, StringComparison.Ordinal) },
                { "metaPreserved", string.Equals(entry.OldGuid, currentGuid, StringComparison.Ordinal) },
                { "oldMetaPath", entry.OldMetaPath },
                { "currentMetaPath", currentMetaPath },
                { "oldMetaExists", entry.OldMetaExists },
                { "currentMetaExists", !string.IsNullOrEmpty(currentMetaPath) && File.Exists(GetAbsolutePath(currentMetaPath)) },
                { "moved", entry.Moved },
                { "rolledBack", entry.RolledBack },
                { "spriteNameSynchronizationAttempted", entry.SpriteNameSynchronizationAttempted },
                { "synchronizedSpriteNames", entry.SynchronizedSpriteNames },
                { "synchronizedSpriteCount", entry.SynchronizedSpriteCount },
                { "spriteImportMode", entry.SpriteImportMode ?? "" },
                { "spriteNameSynchronizationError", entry.SpriteNameSynchronizationError ?? "" },
                { "singleSpriteNameSynchronizationAttempted", entry.SingleSpriteNameSynchronizationAttempted },
                { "synchronizedSingleSpriteName", entry.SynchronizedSingleSpriteName },
                { "synchronizedMultipleSpriteNames", entry.SynchronizedMultipleSpriteNames },
                { "error", entry.Error ?? "" },
            };
        }

        private sealed class BatchMoveEntry
        {
            public int Index;
            public string OldPath;
            public string RequestedDestinationPath;
            public string TargetPath;
            public string OldGuid;
            public string OldMetaPath;
            public bool OldMetaExists;
            public bool Moved;
            public bool RolledBack;
            public bool SpriteNameSynchronizationAttempted;
            public bool SynchronizedSpriteNames;
            public int SynchronizedSpriteCount;
            public string SpriteImportMode;
            public string SpriteNameSynchronizationError;
            public bool SingleSpriteNameSynchronizationAttempted;
            public bool SynchronizedSingleSpriteName;
            public bool SynchronizedMultipleSpriteNames;
            public string Error;
        }

        private sealed class SpriteNameSynchronizationResult
        {
            public bool Attempted;
            public bool Success;
            public int SynchronizedCount;
            public string SpriteImportMode = "";
            public string Error;
        }

        private sealed class BatchImportEntry
        {
            public int Index;
            public string SourcePath;
            public string DestinationPath;
            public string AbsoluteDestinationPath;
            public Dictionary<string, object> Settings;
            public bool Overwrite;
            public bool ExistedBefore;
            public string DedupeMode;
            public string DedupeScope;
            public string DedupeSearchPath;
            public string OnDuplicate;
            public string ContentHash;
            public int ImageWidth;
            public int ImageHeight;
            public bool Duplicate;
            public bool Skipped;
            public string DuplicateAssetPath;
            public string DuplicateAssetGuid;
            public int DuplicateSourceIndex = -1;
            public string DuplicateSourcePath;
            public bool MetaExistedBefore;
            public bool Touched;
            public bool Imported;
            public bool RolledBack;
            public string OriginalGuid;
            public string BackupAssetPath;
            public string BackupMetaPath;
            public string Error;
            public string RollbackError;
            public object ImporterSettings;
            public SpriteSliceSettings SpriteSlice;
            public Dictionary<string, object> SpriteSliceResult;
            public List<Dictionary<string, object>> SubAssets;
        }

        private sealed class SpriteSliceSettings
        {
            public SpriteSliceSettings(Dictionary<string, object> arguments)
            {
                Arguments = arguments;
            }

            public Dictionary<string, object> Arguments { get; }
        }
    }
}
