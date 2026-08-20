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
    public static class VmAutomationAssetCommands
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

        public static object Refresh(Dictionary<string, object> args)
        {
            return VmAutomationWorkspaceJobRunner.StartAssetRefresh(args);
        }

        internal static object ExecuteRefreshImmediate(Dictionary<string, object> args)
        {
            bool forceUpdate = GetBool(args, "forceUpdate", false);
            bool saveAssets = GetBool(args, "saveAssets", false);
            var assetPaths = GetStringList(args, "assetPaths");
            if (assetPaths.Count > 0 &&
                VmAutomationSceneCommands.TryRejectLoadedSceneAssetMutation(
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
            return VmAutomationUnityPackageImportWorkflow.Start(args);
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
            if (VmAutomationSceneCommands.TryRejectLoadedSceneAssetMutation(
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
            if (VmAutomationSceneCommands.TryRejectLoadedSceneAssetMutation(
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
                result.Success = VmAutomationSpriteSheetCommands.TryRenameSpritePrefixPreservingIds(
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
            if (!VmAutomationExecutionOptions.TryParse(args, out var execution, out string executionError))
                return new { success = false, error = executionError };
            bool dryRun = GetBool(args, "dryRun", false);
            if (!TryPrepareMoves(args, out var entries, out object preparationError))
                return preparationError;

            if (dryRun)
                return BuildMoveResult(entries, execution, true, new List<string>());
            if (VmAutomationSceneCommands.TryRejectLoadedSceneAssetMutation(
                    entries.Select(entry => entry.OldPath), "move assets",
                    out object sceneMutationError))
                return sceneMutationError;
            return ExecutePreparedMoves(entries, execution);
        }

        public static void MoveDeferred(Dictionary<string, object> args, Action<object> resolve,
            Action<object> progress)
        {
            if (!VmAutomationExecutionOptions.TryParse(args, out var execution, out string executionError))
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
            if (VmAutomationSceneCommands.TryRejectLoadedSceneAssetMutation(
                    entries.Select(entry => entry.OldPath), "move assets",
                    out object sceneMutationError))
            {
                resolve(sceneMutationError);
                return;
            }
            if (execution.ResolveMode(entries.Count) == VmAutomationExecutionMode.Immediate)
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

        private static object ExecutePreparedMoves(List<BatchMoveEntry> entries, VmAutomationExecutionOptions execution)
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
            VmAutomationExecutionOptions execution, bool dryRun, List<string> errors)
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
            VmAutomationExecutionOptions execution, string error, List<string> errors)
        {
            var result = BuildMoveResult(entries, execution, false, errors);
            result["success"] = false;
            result["error"] = error;
            result["errors"] = errors;
            result["rolledBack"] = entries.TrueForAll(entry => !entry.Moved || entry.RolledBack);
            return result;
        }

        private static Dictionary<string, object> BuildMoveProgress(List<BatchMoveEntry> entries,
            VmAutomationExecutionOptions execution, int nextIndex, int elapsedMs)
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

            var go = VmAutomationGameObjectCommands.FindGameObject(args);
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
                instance.transform.position = VmAutomationGameObjectCommands.DictToVector3(args["position"] as Dictionary<string, object>);

            if (args.ContainsKey("rotation"))
                instance.transform.eulerAngles = VmAutomationGameObjectCommands.DictToVector3(args["rotation"] as Dictionary<string, object>);

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
            VmAutomationTransformSerialization.AddWorld(result, instance.transform);
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

    }
}
