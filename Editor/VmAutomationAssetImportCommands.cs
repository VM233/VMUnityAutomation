using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using static VMUnityAutomation.Editor.VmAutomationAssetCommandUtility;

namespace VMUnityAutomation.Editor
{
    public static class VmAutomationAssetImportCommands
    {
    public static object Import(Dictionary<string, object> args)
    {
        if (!VmAutomationExecutionOptions.TryParse(args, out var execution, out string executionError))
            return ImportError(executionError);
        if (!TryPrepareImports(args, out var entries, out object preparationError))
            return preparationError;
        if (GetBool(args, "dryRun", false))
            return BuildImportResult(entries, execution, true, new List<string>());
        if (VmAutomationSceneCommands.TryRejectLoadedSceneAssetMutation(
                entries.Select(entry => entry.DestinationPath), "import or overwrite assets",
                out object sceneMutationError))
            return sceneMutationError;
        return ExecutePreparedImports(entries, execution);
    }

    public static void ImportDeferred(Dictionary<string, object> args, Action<object> resolve,
        Action<object> progress)
    {
        if (!VmAutomationExecutionOptions.TryParse(args, out var execution, out string executionError))
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
        if (VmAutomationSceneCommands.TryRejectLoadedSceneAssetMutation(
                entries.Select(entry => entry.DestinationPath), "import or overwrite assets",
                out object sceneMutationError))
        {
            resolve(sceneMutationError);
            return;
        }
        if (execution.ResolveMode(entries.Count) == VmAutomationExecutionMode.Immediate)
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

            if (!VmAutomationImageDuplicateCommands.TryNormalizeMode(GetString(settings, "dedupeMode"), sourcePath,
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
            Dictionary<string, List<VmAutomationImageDuplicateCommands.ImageAssetRecord>>>(StringComparer.OrdinalIgnoreCase);
        var priorSources = new Dictionary<string, List<BatchImportEntry>>(StringComparer.Ordinal);

        foreach (var entry in entries)
        {
            if (entry.DedupeMode == VmAutomationImageDuplicateCommands.NoneMode)
                continue;
            try
            {
                var fingerprint = VmAutomationImageDuplicateCommands.CreateFingerprint(entry.SourcePath, entry.DedupeMode);
                entry.ContentHash = fingerprint.Hash;
                entry.ImageWidth = fingerprint.Width;
                entry.ImageHeight = fingerprint.Height;

                string searchFolder = ResolveDedupeSearchFolder(entry);
                string indexKey = entry.DedupeMode + "|" + searchFolder;
                if (!assetIndexes.TryGetValue(indexKey, out var assetIndex))
                {
                    assetIndex = VmAutomationImageDuplicateCommands.BuildAssetIndex(searchFolder, entry.DedupeMode,
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

    private static object ExecutePreparedImports(List<BatchImportEntry> entries, VmAutomationExecutionOptions execution)
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
        VmAutomationExecutionOptions execution, bool dryRun, List<string> errors)
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
        VmAutomationExecutionOptions execution, string error, List<string> errors)
    {
        var result = BuildImportResult(entries, execution, false, errors);
        result["success"] = false;
        result["error"] = error;
        result["errors"] = errors;
        result["allTouchedRolledBack"] = entries.TrueForAll(entry => !entry.Touched || entry.RolledBack);
        return result;
    }

    private static Dictionary<string, object> BuildImportProgress(List<BatchImportEntry> entries,
        VmAutomationExecutionOptions execution, int nextIndex, int elapsedMs)
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
        var result = VmAutomationSpriteSheetCommands.SliceSheet(sliceArgs) as Dictionary<string, object>;
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
                var fingerprint = VmAutomationImageDuplicateCommands.CreateFingerprint(sourcePath,
                    VmAutomationImageDuplicateCommands.DecodedPixelsMode);
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
