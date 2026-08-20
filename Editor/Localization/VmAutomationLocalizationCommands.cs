using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Pseudo;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.SmartFormat.Extensions;
using UnityEngine.Localization.SmartFormat.PersistentVariables;
using UnityEngine.Localization.Tables;
using Object = UnityEngine.Object;

namespace VMUnityAutomation.Editor.Localization
{
    public static class VmAutomationLocalizationCommands
    {
        internal static object GetStatus()
        {
            var package = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(Locale).Assembly);
            var settings = LocalizationEditorSettings.ActiveLocalizationSettings;
            var locales = LocalizationEditorSettings.GetLocales();
            var stringCollections = LocalizationEditorSettings.GetStringTableCollections();
            var assetCollections = LocalizationEditorSettings.GetAssetTableCollections();
            var result = BuildSettingsInfo(settings);
            result["success"] = true;
            result["packageName"] = package?.name ?? "com.unity.localization";
            result["packageVersion"] = package?.version ?? "";
            result["localeCount"] = locales.Count;
            result["stringCollectionCount"] = stringCollections.Count;
            result["assetCollectionCount"] = assetCollections.Count;
            return result;
        }

        internal static object ListLocales(Dictionary<string, object> args)
        {
            bool includePseudo = GetBool(args, "includePseudo", true);
            var locales = LocalizationEditorSettings.GetLocales()
                .Where(locale => includePseudo || !(locale is PseudoLocale))
                .OrderBy(locale => locale.Identifier.Code, StringComparer.OrdinalIgnoreCase)
                .Select(BuildLocaleInfo)
                .ToList();
            return new Dictionary<string, object>
            {
                { "success", true },
                { "count", locales.Count },
                { "locales", locales },
            };
        }

        internal static object CreateLocale(Dictionary<string, object> args)
        {
            string code = GetString(args, "code");
            string assetPath = NormalizeAssetPath(GetString(args, "assetPath"));
            if (string.IsNullOrWhiteSpace(code))
                return Error("code is required");
            if (!IsAssetPath(assetPath, ".asset"))
                return Error("assetPath must be under Assets and end in .asset");
            if (LocalizationEditorSettings.GetLocale(code) != null)
                return Error($"Locale '{code}' is already registered");
            if (AssetDatabase.LoadMainAssetAtPath(assetPath) != null)
                return Error($"An asset already exists at '{assetPath}'");

            EnsureAssetDirectory(Path.GetDirectoryName(assetPath)?.Replace('\\', '/'));
            var locale = Locale.CreateLocale(new LocaleIdentifier(code));
            string displayName = GetString(args, "name");
            if (!string.IsNullOrWhiteSpace(displayName))
                locale.name = displayName;

            AssetDatabase.CreateAsset(locale, assetPath);
            if (GetBool(args, "addToProject", true))
                LocalizationEditorSettings.AddLocale(locale);
            AssetDatabase.SaveAssets();

            var result = BuildLocaleInfo(locale);
            result["success"] = true;
            result["registered"] = LocalizationEditorSettings.GetLocale(code) != null;
            return result;
        }

        internal static object SetSelectedLocale(Dictionary<string, object> args)
        {
            string code = GetString(args, "locale");
            var locale = FindLocale(code, out string error);
            if (locale == null)
                return Error(error);

            var previous = LocalizationEditorSettings.ActiveLocalizationSettings?.GetSelectedLocale();
            LocalizationSettings.SelectedLocale = locale;
            return new Dictionary<string, object>
            {
                { "success", true },
                { "previousLocale", previous?.Identifier.Code ?? "" },
                { "selectedLocale", locale.Identifier.Code },
            };
        }

        internal static object ListCollections(Dictionary<string, object> args)
        {
            string type = NormalizeCollectionType(GetString(args, "type"), allowEmpty: true);
            if (type == null)
                return Error("type must be string or asset");
            string nameContains = GetString(args, "nameContains");

            var collections = new List<Dictionary<string, object>>();
            if (string.IsNullOrEmpty(type) || type == "string")
                collections.AddRange(LocalizationEditorSettings.GetStringTableCollections()
                    .Select(collection => BuildCollectionInfo(collection, "string")));
            if (string.IsNullOrEmpty(type) || type == "asset")
                collections.AddRange(LocalizationEditorSettings.GetAssetTableCollections()
                    .Select(collection => BuildCollectionInfo(collection, "asset")));

            if (!string.IsNullOrEmpty(nameContains))
            {
                collections = collections.Where(collection => collection["name"].ToString()
                        .IndexOf(nameContains, StringComparison.OrdinalIgnoreCase) >= 0)
                    .ToList();
            }

            collections = collections.OrderBy(collection => collection["name"].ToString(),
                    StringComparer.OrdinalIgnoreCase)
                .ToList();
            return new Dictionary<string, object>
            {
                { "success", true },
                { "count", collections.Count },
                { "collections", collections },
            };
        }

        internal static object CreateCollection(Dictionary<string, object> args)
        {
            string name = GetString(args, "name");
            string type = NormalizeCollectionType(GetString(args, "type"));
            string assetDirectory = NormalizeAssetPath(GetString(args, "assetDirectory")).TrimEnd('/');
            if (string.IsNullOrWhiteSpace(name))
                return Error("name is required");
            if (type == null)
                return Error("type must be string or asset");
            if (!IsAssetPath(assetDirectory))
                return Error("assetDirectory must be under Assets");

            var existing = GetCollection(name, type);
            if (existing != null)
                return Error($"{type} Table Collection '{name}' already exists");

            var localeCodes = GetStringList(args, "locales");
            var locales = new List<Locale>();
            if (localeCodes.Count == 0)
            {
                locales.AddRange(LocalizationEditorSettings.GetLocales());
            }
            else
            {
                foreach (string localeCode in localeCodes)
                {
                    var locale = FindLocale(localeCode, out string error);
                    if (locale == null)
                        return Error(error);
                    if (!locales.Contains(locale))
                        locales.Add(locale);
                }
            }

            EnsureAssetDirectory(assetDirectory);
            LocalizationTableCollection collection = type == "asset"
                ? LocalizationEditorSettings.CreateAssetTableCollection(name, assetDirectory, locales)
                : LocalizationEditorSettings.CreateStringTableCollection(name, assetDirectory, locales);
            if (collection == null)
                return Error($"Failed to create {type} Table Collection '{name}'");

            string group = GetString(args, "group");
            if (!string.IsNullOrWhiteSpace(group))
                collection.Group = group;
            if (args.ContainsKey("preload"))
                collection.SetPreloadTableFlag(GetBool(args, "preload", false));
            EditorUtility.SetDirty(collection);
            AssetDatabase.SaveAssets();

            var result = BuildCollectionInfo(collection, type);
            result["success"] = true;
            return result;
        }

        internal static object ListEntries(Dictionary<string, object> args)
        {
            string type = NormalizeCollectionType(GetString(args, "type"));
            if (type == null)
                return Error("type must be string or asset");
            var collection = GetCollection(GetString(args, "collection"), type);
            if (collection == null)
                return Error($"{type} Table Collection '{GetString(args, "collection")}' was not found");

            string localeFilter = GetString(args, "locale");
            string keyContains = GetString(args, "keyContains");
            int offset = Math.Max(0, GetInt(args, "offset", 0));
            int limit = Math.Max(1, Math.Min(GetInt(args, "limit", 100), 500));
            var sharedEntries = collection.SharedData.Entries
                .Where(entry => string.IsNullOrEmpty(keyContains) ||
                                entry.Key.IndexOf(keyContains, StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var entries = sharedEntries.Skip(offset).Take(limit)
                .Select(entry => BuildEntryInfo(collection, type, entry, localeFilter))
                .ToList();
            return new Dictionary<string, object>
            {
                { "success", true },
                { "collection", collection.TableCollectionName },
                { "type", type },
                { "total", sharedEntries.Count },
                { "offset", offset },
                { "limit", limit },
                { "returned", entries.Count },
                { "hasMore", offset + entries.Count < sharedEntries.Count },
                { "entries", entries },
            };
        }

        internal static void UpsertEntriesDeferred(Dictionary<string, object> args, Action<object> resolve,
            Action<object> progress)
        {
            if (!VmAutomationExecutionOptions.TryParse(args, out var execution, out string executionError))
            {
                resolve(Error(executionError));
                return;
            }
            if (!TryPrepareUpsertEntries(args, execution, out var state, out object preparationError))
            {
                resolve(preparationError);
                return;
            }
            if (execution.ResolveMode(state.Plans.Count) == VmAutomationExecutionMode.Immediate)
            {
                resolve(ExecutePreparedUpserts(state));
                return;
            }

            double startedAt = EditorApplication.timeSinceStartup;
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
                    state.Errors.Add($"Localization upsert timed out after {execution.TimeoutMs} ms");
                    complete(FinishAndBuildUpsertResult(state));
                    return;
                }

                double frameStartedAt = EditorApplication.timeSinceStartup;
                int processedThisFrame = 0;
                while (state.NextPlanIndex < state.Plans.Count)
                {
                    var plan = state.Plans[state.NextPlanIndex++];
                    try
                    {
                        ApplyUpsertPlan(state, plan);
                    }
                    catch (Exception exception)
                    {
                        state.Errors.Add($"entries[{plan.Index}] failed: {exception.Message}");
                        if (!execution.ContinueOnError)
                        {
                            complete(FinishAndBuildUpsertResult(state));
                            return;
                        }
                    }

                    processedThisFrame++;
                    progress?.Invoke(new Dictionary<string, object>
                    {
                        { "phase", "upserting" },
                        { "processedCount", state.NextPlanIndex },
                        { "entryCount", state.Plans.Count },
                        { "elapsedMs", elapsedMs },
                        { "execution", execution.ToResult(state.Plans.Count) },
                    });
                    double frameElapsedMs = (EditorApplication.timeSinceStartup - frameStartedAt) * 1000d;
                    if (processedThisFrame >= execution.OperationsPerFrame ||
                        frameElapsedMs >= execution.FrameBudgetMs)
                        break;
                }

                if (state.NextPlanIndex < state.Plans.Count)
                    return;
                complete(FinishAndBuildUpsertResult(state));
            };
            EditorApplication.update += tick;
            tick();
        }

        private static bool TryPrepareUpsertEntries(Dictionary<string, object> args, VmAutomationExecutionOptions execution,
            out LocalizationUpsertState state, out object errorResult)
        {
            state = null;
            errorResult = null;
            string type = NormalizeCollectionType(GetString(args, "type"));
            if (type == null)
            {
                errorResult = Error("type must be string or asset");
                return false;
            }

            string collectionName = GetString(args, "collection");
            var collection = GetCollection(collectionName, type);
            if (collection == null)
            {
                errorResult = Error($"{type} Table Collection '{collectionName}' was not found");
                return false;
            }
            if (args == null || !args.TryGetValue("entries", out object rawEntries) ||
                rawEntries == null || rawEntries is string || !(rawEntries is IEnumerable entries))
            {
                errorResult = Error("entries must be a non-empty array");
                return false;
            }

            state = new LocalizationUpsertState
            {
                Type = type,
                CollectionGuid = collection.SharedData.TableCollectionNameGuid.ToString(),
                CollectionName = collection.TableCollectionName,
                Execution = execution,
            };
            var uniqueEntries = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int index = 0;
            foreach (object rawEntry in entries)
            {
                if (state.Plans.Count >= 500)
                {
                    errorResult = Error("entries is capped at 500 items");
                    return false;
                }
                var entry = AsDictionary(rawEntry);
                if (entry == null)
                {
                    errorResult = Error($"entries[{index}] must be an object");
                    return false;
                }
                string key = GetString(entry, "key");
                string localeCode = GetString(entry, "locale");
                if (string.IsNullOrWhiteSpace(key))
                {
                    errorResult = Error($"entries[{index}].key is required");
                    return false;
                }
                var locale = FindLocale(localeCode, out string localeError);
                if (locale == null)
                {
                    errorResult = Error($"entries[{index}]: {localeError}");
                    return false;
                }
                string uniqueKey = locale.Identifier.Code + "\n" + key;
                if (!uniqueEntries.Add(uniqueKey))
                {
                    errorResult = Error($"Duplicate key '{key}' for Locale '{locale.Identifier.Code}'");
                    return false;
                }

                var plan = new LocalizationUpsertPlan
                {
                    Index = index,
                    Key = key,
                    LocaleCode = locale.Identifier.Code,
                    HasSmart = entry.ContainsKey("smart"),
                    Smart = GetBool(entry, "smart", false),
                };
                if (type == "string")
                {
                    if (!entry.TryGetValue("value", out object rawValue) || !(rawValue is string value))
                    {
                        errorResult = Error($"entries[{index}].value must be a string");
                        return false;
                    }
                    plan.StringValue = value;
                }
                else
                {
                    string assetPath = NormalizeAssetPath(GetString(entry, "assetPath"));
                    if (!IsAssetPath(assetPath))
                    {
                        errorResult = Error($"entries[{index}].assetPath under Assets is required");
                        return false;
                    }
                    string subAssetName = GetString(entry, "subAssetName");
                    if (LoadAsset(assetPath, subAssetName) == null)
                    {
                        errorResult = Error($"entries[{index}] asset was not found at '{assetPath}'");
                        return false;
                    }
                    plan.AssetPath = assetPath;
                    plan.SubAssetName = subAssetName;
                }
                state.Plans.Add(plan);
                index++;
            }
            if (state.Plans.Count == 0)
            {
                errorResult = Error("entries must be a non-empty array");
                return false;
            }

            bool createTables = GetBool(args, "createTables", true);
            foreach (string localeCode in state.Plans.Select(plan => plan.LocaleCode)
                         .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                collection = ResolveUpsertCollection(state);
                if (collection == null)
                {
                    errorResult = Error(
                        $"{type} Table Collection '{state.CollectionName}' was not found after asset creation");
                    return false;
                }

                var localeIdentifier = new LocaleIdentifier(localeCode);
                var table = collection.GetTable(localeIdentifier);
                if (table == null && !createTables)
                {
                    errorResult = Error($"Collection '{collectionName}' has no table for Locale '{localeCode}'");
                    return false;
                }
                if (table == null)
                {
                    collection.AddNewTable(localeIdentifier);
                    state.CreatedTables.Add(localeCode);
                }
            }
            return true;
        }

        private static object ExecutePreparedUpserts(LocalizationUpsertState state)
        {
            foreach (var plan in state.Plans)
            {
                try
                {
                    ApplyUpsertPlan(state, plan);
                }
                catch (Exception exception)
                {
                    state.Errors.Add($"entries[{plan.Index}] failed: {exception.Message}");
                    if (!state.Execution.ContinueOnError)
                        break;
                }
            }
            return FinishAndBuildUpsertResult(state);
        }

        private static void ApplyUpsertPlan(LocalizationUpsertState state, LocalizationUpsertPlan plan)
        {
            var collection = ResolveUpsertCollection(state);
            if (collection == null)
                throw new InvalidOperationException(
                    $"{state.Type} Table Collection '{state.CollectionName}' is no longer available");

            var localeIdentifier = new LocaleIdentifier(plan.LocaleCode);
            var table = collection.GetTable(localeIdentifier);
            if (table == null)
                throw new InvalidOperationException(
                    $"Collection '{state.CollectionName}' has no table for Locale '{plan.LocaleCode}'");

            bool createdKey = collection.SharedData.GetEntry(plan.Key) == null;
            bool createdEntry;
            if (state.Type == "string")
            {
                var stringTable = (StringTable)table;
                var entry = stringTable.GetEntry(plan.Key);
                createdEntry = entry == null;
                if (entry == null)
                    entry = stringTable.AddEntry(plan.Key, plan.StringValue);
                entry.Value = plan.StringValue;
                if (plan.HasSmart)
                    entry.IsSmart = plan.Smart;
                EditorUtility.SetDirty(stringTable);
            }
            else
            {
                var assetTable = (AssetTable)table;
                var asset = LoadAsset(plan.AssetPath, plan.SubAssetName);
                if (asset == null)
                    throw new InvalidOperationException(
                        $"Asset for entries[{plan.Index}] is no longer available at '{plan.AssetPath}'");
                createdEntry = assetTable.GetEntry(plan.Key) == null;
                ((AssetTableCollection)collection).AddAssetToTable(localeIdentifier, plan.Key, asset);
                EditorUtility.SetDirty(assetTable);
            }

            if (createdKey)
                state.CreatedKeys.Add(plan.Key);
            if (createdEntry)
                state.CreatedEntryCount++;
            else
                state.UpdatedEntryCount++;
            state.Results.Add(new Dictionary<string, object>
            {
                { "index", plan.Index },
                { "key", plan.Key },
                { "locale", plan.LocaleCode },
                { "createdKey", createdKey },
                { "createdEntry", createdEntry },
            });
            state.NextPlanIndex = Math.Max(state.NextPlanIndex, plan.Index + 1);
        }

        private static object FinishAndBuildUpsertResult(LocalizationUpsertState state)
        {
            try
            {
                FinishPreparedUpserts(state);
            }
            catch (Exception exception)
            {
                state.Errors.Add($"Failed to save Localization upserts: {exception.Message}");
            }
            return BuildUpsertResult(state);
        }

        private static void FinishPreparedUpserts(LocalizationUpsertState state)
        {
            var collection = ResolveUpsertCollection(state);
            if (collection == null)
                throw new InvalidOperationException(
                    $"{state.Type} Table Collection '{state.CollectionName}' is no longer available");
            EditorUtility.SetDirty(collection);
            EditorUtility.SetDirty(collection.SharedData);
            AssetDatabase.SaveAssets();
            state.Saved = true;
        }

        private static LocalizationTableCollection ResolveUpsertCollection(LocalizationUpsertState state)
        {
            return GetCollection(state.CollectionGuid, state.Type) ??
                   GetCollection(state.CollectionName, state.Type);
        }

        private static Dictionary<string, object> BuildUpsertResult(LocalizationUpsertState state)
        {
            return new Dictionary<string, object>
            {
                { "success", state.Errors.Count == 0 && state.Results.Count == state.Plans.Count },
                { "collection", state.CollectionName },
                { "type", state.Type },
                { "entryCount", state.Plans.Count },
                { "processedCount", state.Results.Count },
                { "createdKeyCount", state.CreatedKeys.Count },
                { "createdEntryCount", state.CreatedEntryCount },
                { "updatedEntryCount", state.UpdatedEntryCount },
                { "createdTableCount", state.CreatedTables.Count },
                { "createdTables", state.CreatedTables },
                { "saved", state.Saved },
                { "errors", state.Errors },
                { "entries", state.Results },
                { "execution", state.Execution.ToResult(state.Plans.Count) },
            };
        }

        internal static object RemoveEntry(Dictionary<string, object> args)
        {
            string type = NormalizeCollectionType(GetString(args, "type"));
            if (type == null)
                return Error("type must be string or asset");
            string collectionName = GetString(args, "collection");
            string key = GetString(args, "key");
            if (string.IsNullOrWhiteSpace(key))
                return Error("key is required");

            var collection = GetCollection(collectionName, type);
            if (collection == null)
                return Error($"{type} Table Collection '{collectionName}' was not found");
            bool removed;
            string localeCode = GetString(args, "locale");
            if (string.IsNullOrEmpty(localeCode))
            {
                removed = collection.SharedData.GetEntry(key) != null;
                if (removed)
                {
                    collection.RemoveEntry(key);
                    MarkCollectionEntryRemovalDirty(collection, type);
                }
            }
            else
            {
                var locale = FindLocale(localeCode, out string localeError);
                if (locale == null)
                    return Error(localeError);
                var table = collection.GetTable(locale.Identifier);
                if (table == null)
                    return Error($"Collection '{collectionName}' has no table for Locale '{localeCode}'");

                if (type == "asset")
                {
                    removed = ((AssetTable)table).GetEntry(key) != null;
                    if (removed)
                        ((AssetTableCollection)collection).RemoveAssetFromTable((AssetTable)table, key);
                }
                else
                {
                    removed = ((StringTable)table).GetEntry(key) != null;
                    if (removed)
                        ((StringTable)table).RemoveEntry(key);
                }

                if (removed)
                {
                    EditorUtility.SetDirty(table);
                    EditorUtility.SetDirty(table.SharedData);
                }
            }

            if (removed)
                AssetDatabase.SaveAssets();
            return new Dictionary<string, object>
            {
                { "success", true },
                { "removed", removed },
                { "collection", collection.TableCollectionName },
                { "type", type },
                { "locale", localeCode },
                { "key", key },
            };
        }

        private static void MarkCollectionEntryRemovalDirty(
            LocalizationTableCollection collection,
            string type)
        {
            foreach (var table in GetTables(collection, type))
                EditorUtility.SetDirty(table);
            EditorUtility.SetDirty(collection.SharedData);
            EditorUtility.SetDirty(collection);
        }

        internal static object Validate(Dictionary<string, object> args)
        {
            string typeFilter = NormalizeCollectionType(GetString(args, "type"), allowEmpty: true);
            if (typeFilter == null)
                return Error("type must be string or asset");
            string collectionFilter = GetString(args, "collection");
            bool includeEmpty = GetBool(args, "includeEmpty", true);
            int maxIssues = Math.Max(1, Math.Min(GetInt(args, "maxIssues", 200), 2000));

            var collections = new List<(LocalizationTableCollection collection, string type)>();
            if (string.IsNullOrEmpty(typeFilter) || typeFilter == "string")
                collections.AddRange(LocalizationEditorSettings.GetStringTableCollections()
                    .Select(collection => ((LocalizationTableCollection)collection, "string")));
            if (string.IsNullOrEmpty(typeFilter) || typeFilter == "asset")
                collections.AddRange(LocalizationEditorSettings.GetAssetTableCollections()
                    .Select(collection => ((LocalizationTableCollection)collection, "asset")));
            if (!string.IsNullOrEmpty(collectionFilter))
                collections = collections.Where(item => MatchesCollection(item.collection, collectionFilter)).ToList();

            var issues = new List<Dictionary<string, object>>();
            int totalIssues = 0;
            foreach (var item in collections)
            {
                foreach (var duplicate in item.collection.SharedData.Entries
                             .GroupBy(entry => entry.Key, StringComparer.Ordinal)
                             .Where(group => group.Count() > 1))
                {
                    AddIssue(issues, maxIssues, ref totalIssues, item.collection, item.type,
                        duplicate.First(), "", "duplicate-key");
                }

                foreach (var sharedEntry in item.collection.SharedData.Entries)
                {
                    foreach (var table in GetTables(item.collection, item.type))
                    {
                        string issue = GetEntryIssue(table, item.type, sharedEntry.Id, includeEmpty);
                        if (!string.IsNullOrEmpty(issue))
                        {
                            AddIssue(issues, maxIssues, ref totalIssues, item.collection, item.type,
                                sharedEntry, table.LocaleIdentifier.Code, issue);
                        }
                    }
                }
            }

            return new Dictionary<string, object>
            {
                { "success", true },
                { "valid", totalIssues == 0 },
                { "collectionCount", collections.Count },
                { "issueCount", totalIssues },
                { "returnedIssueCount", issues.Count },
                { "truncated", issues.Count < totalIssues },
                { "issues", issues },
            };
        }

        internal static object UpdateSettings(Dictionary<string, object> args)
        {
            var settings = LocalizationEditorSettings.ActiveLocalizationSettings;
            if (settings == null)
                return Error("No active Localization Settings asset was found");

            var changed = new List<string>();
            if (args.ContainsKey("initializeSynchronously"))
            {
                LocalizationSettings.InitializeSynchronously = GetBool(args, "initializeSynchronously", false);
                changed.Add("initializeSynchronously");
            }

            if (args.ContainsKey("projectLocale"))
            {
                var locale = FindLocale(GetString(args, "projectLocale"), out string error);
                if (locale == null)
                    return Error(error);
                LocalizationSettings.ProjectLocale = locale;
                changed.Add("projectLocale");
            }

            if (args.ContainsKey("selectedLocale"))
            {
                var locale = FindLocale(GetString(args, "selectedLocale"), out string error);
                if (locale == null)
                    return Error(error);
                LocalizationSettings.SelectedLocale = locale;
                changed.Add("selectedLocale");
            }

            if (changed.Count > 0)
            {
                EditorUtility.SetDirty(settings);
                AssetDatabase.SaveAssets();
            }

            var result = BuildSettingsInfo(settings);
            result["success"] = true;
            result["changed"] = changed;
            return result;
        }

        internal static object ListVariables(Dictionary<string, object> args)
        {
            var source = GetPersistentVariablesSource(out string error);
            if (source == null)
                return Error(error);
            string groupFilter = GetString(args, "group");
            string nameContains = GetString(args, "nameContains");
            var groups = source
                .Where(pair => string.IsNullOrEmpty(groupFilter) ||
                               string.Equals(pair.Key, groupFilter, StringComparison.OrdinalIgnoreCase))
                .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                .Select(pair => new Dictionary<string, object>
                {
                    { "name", pair.Key },
                    { "assetPath", AssetDatabase.GetAssetPath(pair.Value) },
                    { "variables", pair.Value
                        .Where(variable => string.IsNullOrEmpty(nameContains) ||
                                           variable.Key.IndexOf(nameContains,
                                               StringComparison.OrdinalIgnoreCase) >= 0)
                        .OrderBy(variable => variable.Key, StringComparer.OrdinalIgnoreCase)
                        .Select(variable => BuildVariableInfo(variable.Key, variable.Value))
                        .ToList() },
                })
                .ToList();
            return new Dictionary<string, object>
            {
                { "success", true },
                { "groupCount", groups.Count },
                { "groups", groups },
            };
        }

        internal static object UpsertVariable(Dictionary<string, object> args)
        {
            string groupName = GetString(args, "group");
            string variableName = GetString(args, "name");
            string variableType = GetString(args, "type").ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(groupName))
                return Error("group is required");
            if (string.IsNullOrWhiteSpace(variableName))
                return Error("name is required");
            if (groupName.Any(char.IsWhiteSpace) || variableName.Any(char.IsWhiteSpace))
                return Error("group and name must not contain whitespace");
            if (!args.TryGetValue("value", out object rawValue))
                return Error("value is required");

            var source = GetPersistentVariablesSource(out string error);
            if (source == null)
                return Error(error);
            bool createdGroup = false;
            if (!source.TryGetValue(groupName, out VariablesGroupAsset group))
            {
                string groupAssetPath = NormalizeAssetPath(GetString(args, "groupAssetPath"));
                if (!IsAssetPath(groupAssetPath, ".asset"))
                    return Error("groupAssetPath under Assets ending in .asset is required for a new group");
                if (AssetDatabase.LoadMainAssetAtPath(groupAssetPath) != null)
                    return Error($"An asset already exists at '{groupAssetPath}'");

                EnsureAssetDirectory(Path.GetDirectoryName(groupAssetPath)?.Replace('\\', '/'));
                group = ScriptableObject.CreateInstance<VariablesGroupAsset>();
                group.name = Path.GetFileNameWithoutExtension(groupAssetPath);
                AssetDatabase.CreateAsset(group, groupAssetPath);
                source.Add(groupName, group);
                createdGroup = true;
            }

            IVariable variable = CreateVariable(variableType, rawValue, out error);
            if (variable == null)
                return Error(error);
            bool createdVariable = !group.ContainsKey(variableName);
            if (!createdVariable)
                group.Remove(variableName);
            group.Add(variableName, variable);

            EditorUtility.SetDirty(group);
            var settings = LocalizationEditorSettings.ActiveLocalizationSettings;
            if (createdGroup && settings != null)
                EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();

            var result = BuildVariableInfo(variableName, variable);
            result["success"] = true;
            result["group"] = groupName;
            result["groupAssetPath"] = AssetDatabase.GetAssetPath(group);
            result["createdGroup"] = createdGroup;
            result["createdVariable"] = createdVariable;
            return result;
        }

        internal static object RemoveVariable(Dictionary<string, object> args)
        {
            string groupName = GetString(args, "group");
            string variableName = GetString(args, "name");
            var source = GetPersistentVariablesSource(out string error);
            if (source == null)
                return Error(error);
            if (!source.TryGetValue(groupName, out VariablesGroupAsset group))
                return Error($"Persistent variable group '{groupName}' was not found");

            bool removed = group.Remove(variableName);
            if (removed)
            {
                EditorUtility.SetDirty(group);
                AssetDatabase.SaveAssets();
            }
            return new Dictionary<string, object>
            {
                { "success", true },
                { "removed", removed },
                { "group", groupName },
                { "name", variableName },
            };
        }

        private static PersistentVariablesSource GetPersistentVariablesSource(out string error)
        {
            error = "";
            var database = LocalizationSettings.StringDatabase;
            if (database == null)
            {
                error = "Localization String Database is not configured";
                return null;
            }

            var source = database.SmartFormatter.GetSourceExtension<PersistentVariablesSource>();
            if (source == null)
                error = "PersistentVariablesSource is not configured on the Localization SmartFormatter";
            return source;
        }

        private static IVariable CreateVariable(string type, object value, out string error)
        {
            error = "";
            switch (type)
            {
                case "bool":
                    if (bool.TryParse(value?.ToString(), out bool boolValue))
                        return new BoolVariable { Value = boolValue };
                    break;
                case "int":
                    if (int.TryParse(value?.ToString(), out int intValue))
                        return new IntVariable { Value = intValue };
                    break;
                case "long":
                    if (long.TryParse(value?.ToString(), out long longValue))
                        return new LongVariable { Value = longValue };
                    break;
                case "float":
                    if (float.TryParse(value?.ToString(), out float floatValue))
                        return new FloatVariable { Value = floatValue };
                    break;
                case "double":
                    if (double.TryParse(value?.ToString(), out double doubleValue))
                        return new DoubleVariable { Value = doubleValue };
                    break;
                case "string":
                    return new StringVariable { Value = value?.ToString() ?? "" };
                case "object":
                    string assetPath = NormalizeAssetPath(value?.ToString());
                    Object asset = AssetDatabase.LoadMainAssetAtPath(assetPath);
                    if (asset != null)
                        return new ObjectVariable { Value = asset };
                    error = $"Object variable asset was not found at '{assetPath}'";
                    return null;
                default:
                    error = "type must be bool, int, long, float, double, string, or object";
                    return null;
            }

            error = $"Value '{value}' is invalid for variable type '{type}'";
            return null;
        }

        private static Dictionary<string, object> BuildVariableInfo(string name, IVariable variable)
        {
            object value;
            string type;
            string assetPath = "";
            switch (variable)
            {
                case BoolVariable typed:
                    type = "bool";
                    value = typed.Value;
                    break;
                case IntVariable typed:
                    type = "int";
                    value = typed.Value;
                    break;
                case LongVariable typed:
                    type = "long";
                    value = typed.Value;
                    break;
                case FloatVariable typed:
                    type = "float";
                    value = typed.Value;
                    break;
                case DoubleVariable typed:
                    type = "double";
                    value = typed.Value;
                    break;
                case StringVariable typed:
                    type = "string";
                    value = typed.Value;
                    break;
                case ObjectVariable typed:
                    type = "object";
                    value = typed.Value != null ? typed.Value.name : "";
                    assetPath = typed.Value != null ? AssetDatabase.GetAssetPath(typed.Value) : "";
                    break;
                default:
                    type = variable.GetType().FullName;
                    value = variable.GetSourceValue(null)?.ToString() ?? "";
                    break;
            }

            return new Dictionary<string, object>
            {
                { "name", name },
                { "type", type },
                { "value", value },
                { "assetPath", assetPath },
            };
        }

        private static Dictionary<string, object> BuildLocaleInfo(Locale locale)
        {
            return new Dictionary<string, object>
            {
                { "code", locale.Identifier.Code },
                { "name", locale.LocaleName },
                { "assetPath", AssetDatabase.GetAssetPath(locale) },
                { "isPseudo", locale is PseudoLocale },
            };
        }

        private static Dictionary<string, object> BuildCollectionInfo(LocalizationTableCollection collection,
            string type)
        {
            var tables = GetTables(collection, type)
                .Select(table => new Dictionary<string, object>
                {
                    { "locale", table.LocaleIdentifier.Code },
                    { "assetPath", AssetDatabase.GetAssetPath(table) },
                    { "entryCount", type == "asset" ? ((AssetTable)table).Count : ((StringTable)table).Count },
                    { "preload", LocalizationEditorSettings.GetPreloadTableFlag(table) },
                })
                .ToList();
            return new Dictionary<string, object>
            {
                { "name", collection.TableCollectionName },
                { "guid", collection.SharedData.TableCollectionNameGuid.ToString() },
                { "type", type },
                { "group", collection.Group },
                { "assetPath", AssetDatabase.GetAssetPath(collection) },
                { "sharedDataPath", AssetDatabase.GetAssetPath(collection.SharedData) },
                { "keyCount", collection.SharedData.Entries.Count },
                { "tableCount", tables.Count },
                { "tables", tables },
            };
        }

        private static Dictionary<string, object> BuildEntryInfo(LocalizationTableCollection collection,
            string type, SharedTableData.SharedTableEntry sharedEntry, string localeFilter)
        {
            var values = GetTables(collection, type)
                .Where(table => string.IsNullOrEmpty(localeFilter) ||
                                string.Equals(table.LocaleIdentifier.Code, localeFilter,
                                    StringComparison.OrdinalIgnoreCase))
                .Select(table => BuildLocalizedValue(table, type, sharedEntry.Id))
                .ToList();
            return new Dictionary<string, object>
            {
                { "key", sharedEntry.Key },
                { "keyId", sharedEntry.Id.ToString(CultureInfo.InvariantCulture) },
                { "values", values },
            };
        }

        private static Dictionary<string, object> BuildLocalizedValue(LocalizationTable table, string type,
            long keyId)
        {
            if (type == "asset")
            {
                var entry = ((AssetTable)table).GetEntry(keyId);
                return new Dictionary<string, object>
                {
                    { "locale", table.LocaleIdentifier.Code },
                    { "missing", entry == null },
                    { "empty", entry == null || entry.IsEmpty },
                    { "assetGuid", entry?.Guid ?? "" },
                    { "assetPath", entry != null ? AssetDatabase.GUIDToAssetPath(entry.Guid) : "" },
                    { "subAssetName", entry?.SubAssetName ?? "" },
                    { "address", entry?.Address ?? "" },
                };
            }

            var stringEntry = ((StringTable)table).GetEntry(keyId);
            return new Dictionary<string, object>
            {
                { "locale", table.LocaleIdentifier.Code },
                { "missing", stringEntry == null },
                { "empty", stringEntry == null || string.IsNullOrEmpty(stringEntry.Value) },
                { "value", stringEntry?.Value ?? "" },
                { "smart", stringEntry != null && stringEntry.IsSmart },
            };
        }

        private static Dictionary<string, object> BuildSettingsInfo(LocalizationSettings settings)
        {
            var selectedLocale = settings?.GetSelectedLocale();
            Locale projectLocale = settings != null ? LocalizationSettings.ProjectLocale : null;
            return new Dictionary<string, object>
            {
                { "hasActiveSettings", settings != null },
                { "settingsAssetPath", settings != null ? AssetDatabase.GetAssetPath(settings) : "" },
                { "selectedLocale", selectedLocale?.Identifier.Code ?? "" },
                { "projectLocale", projectLocale?.Identifier.Code ?? "" },
                { "initializeSynchronously", settings != null && LocalizationSettings.InitializeSynchronously },
                { "startupLocaleSelectors", settings != null
                    ? settings.GetStartupLocaleSelectors().Select(selector => selector.GetType().FullName).ToList()
                    : new List<string>() },
            };
        }

        private static List<LocalizationTable> GetTables(LocalizationTableCollection collection, string type)
        {
            return type == "asset"
                ? ((AssetTableCollection)collection).AssetTables.Cast<LocalizationTable>().ToList()
                : ((StringTableCollection)collection).StringTables.Cast<LocalizationTable>().ToList();
        }

        private static LocalizationTableCollection GetCollection(string nameOrGuid, string type)
        {
            if (string.IsNullOrWhiteSpace(nameOrGuid))
                return null;
            return type == "asset"
                ? LocalizationEditorSettings.GetAssetTableCollection(nameOrGuid)
                : LocalizationEditorSettings.GetStringTableCollection(nameOrGuid);
        }

        private static bool MatchesCollection(LocalizationTableCollection collection, string nameOrGuid)
        {
            return string.Equals(collection.TableCollectionName, nameOrGuid, StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(collection.SharedData.TableCollectionNameGuid.ToString(), nameOrGuid,
                       StringComparison.OrdinalIgnoreCase);
        }

        private static Locale FindLocale(string code, out string error)
        {
            error = "";
            if (string.IsNullOrWhiteSpace(code))
            {
                error = "locale is required";
                return null;
            }

            var locale = LocalizationEditorSettings.GetLocale(code);
            if (locale == null)
                error = $"Registered Locale '{code}' was not found";
            return locale;
        }

        private static Object LoadAsset(string assetPath, string subAssetName)
        {
            if (string.IsNullOrEmpty(subAssetName))
                return AssetDatabase.LoadMainAssetAtPath(assetPath);
            return AssetDatabase.LoadAllAssetsAtPath(assetPath)
                .FirstOrDefault(asset => asset != null && asset.name == subAssetName);
        }

        private static string GetEntryIssue(LocalizationTable table, string type, long keyId,
            bool includeEmpty)
        {
            if (type == "asset")
            {
                var entry = ((AssetTable)table).GetEntry(keyId);
                if (entry == null)
                    return "missing";
                return includeEmpty && entry.IsEmpty ? "empty" : "";
            }

            var stringEntry = ((StringTable)table).GetEntry(keyId);
            if (stringEntry == null)
                return "missing";
            return includeEmpty && string.IsNullOrEmpty(stringEntry.Value) ? "empty" : "";
        }

        private static void AddIssue(List<Dictionary<string, object>> issues, int maxIssues,
            ref int totalIssues, LocalizationTableCollection collection, string type,
            SharedTableData.SharedTableEntry entry, string locale, string issue)
        {
            totalIssues++;
            if (issues.Count >= maxIssues)
                return;
            issues.Add(new Dictionary<string, object>
            {
                { "collection", collection.TableCollectionName },
                { "type", type },
                { "key", entry.Key },
                { "keyId", entry.Id.ToString(CultureInfo.InvariantCulture) },
                { "locale", locale },
                { "issue", issue },
            });
        }

        private static void EnsureAssetDirectory(string assetDirectory)
        {
            assetDirectory = NormalizeAssetPath(assetDirectory).TrimEnd('/');
            if (string.IsNullOrEmpty(assetDirectory) || assetDirectory == "Assets")
                return;
            if (!IsAssetPath(assetDirectory))
                throw new ArgumentException("Directory must be under Assets", nameof(assetDirectory));

            string current = "Assets";
            foreach (string part in assetDirectory.Substring("Assets".Length)
                         .Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string next = current + "/" + part;
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, part);
                current = next;
            }
        }

        private static string NormalizeCollectionType(string type, bool allowEmpty = false)
        {
            if (string.IsNullOrWhiteSpace(type))
                return allowEmpty ? "" : "string";
            if (type.Equals("string", StringComparison.OrdinalIgnoreCase))
                return "string";
            if (type.Equals("asset", StringComparison.OrdinalIgnoreCase))
                return "asset";
            return null;
        }

        private static string NormalizeAssetPath(string path)
        {
            return (path ?? "").Replace('\\', '/').Trim();
        }

        private static bool IsAssetPath(string path, string requiredExtension = null)
        {
            if (string.IsNullOrWhiteSpace(path) ||
                !(path == "Assets" || path.StartsWith("Assets/", StringComparison.Ordinal)))
                return false;
            return string.IsNullOrEmpty(requiredExtension) ||
                   path.EndsWith(requiredExtension, StringComparison.OrdinalIgnoreCase);
        }

        private static string GetString(Dictionary<string, object> args, string key)
        {
            return args != null && args.TryGetValue(key, out object value) && value != null
                ? value.ToString()
                : "";
        }

        private static bool GetBool(Dictionary<string, object> args, string key, bool defaultValue)
        {
            if (args == null || !args.TryGetValue(key, out object value) || value == null)
                return defaultValue;
            if (value is bool boolean)
                return boolean;
            return bool.TryParse(value.ToString(), out bool parsed) ? parsed : defaultValue;
        }

        private static int GetInt(Dictionary<string, object> args, string key, int defaultValue)
        {
            return args != null && args.TryGetValue(key, out object value) && value != null &&
                   int.TryParse(value.ToString(), out int parsed)
                ? parsed
                : defaultValue;
        }

        private static List<string> GetStringList(Dictionary<string, object> args, string key)
        {
            var result = new List<string>();
            if (args == null || !args.TryGetValue(key, out object value) || value == null || value is string)
                return result;
            if (!(value is IEnumerable values))
                return result;
            foreach (object item in values)
            {
                if (item != null && !string.IsNullOrWhiteSpace(item.ToString()))
                    result.Add(item.ToString());
            }
            return result;
        }

        private static Dictionary<string, object> AsDictionary(object value)
        {
            if (value is Dictionary<string, object> dictionary)
                return dictionary;
            if (!(value is IDictionary rawDictionary))
                return null;

            var result = new Dictionary<string, object>();
            foreach (DictionaryEntry entry in rawDictionary)
            {
                if (entry.Key != null)
                    result[entry.Key.ToString()] = entry.Value;
            }
            return result;
        }

        private sealed class LocalizationBatchEntry
        {
            public LocalizationBatchEntry(string key, bool hasSmart, bool smart,
                List<LocalizationBatchTranslation> translations)
            {
                Key = key;
                HasSmart = hasSmart;
                Smart = smart;
                Translations = translations;
            }

            public string Key { get; }
            public bool HasSmart { get; }
            public bool Smart { get; }
            public List<LocalizationBatchTranslation> Translations { get; }
        }

        private sealed class LocalizationBatchTranslation
        {
            public LocalizationBatchTranslation(Locale locale, string value)
            {
                Locale = locale;
                Value = value;
            }

            public Locale Locale { get; }
            public string Value { get; }
        }

        private sealed class LocalizationUpsertState
        {
            public string Type;
            public string CollectionGuid;
            public string CollectionName;
            public VmAutomationExecutionOptions Execution;
            public readonly List<LocalizationUpsertPlan> Plans = new List<LocalizationUpsertPlan>();
            public readonly HashSet<string> CreatedKeys = new HashSet<string>(StringComparer.Ordinal);
            public readonly List<string> CreatedTables = new List<string>();
            public readonly List<string> Errors = new List<string>();
            public readonly List<Dictionary<string, object>> Results =
                new List<Dictionary<string, object>>();
            public int NextPlanIndex;
            public int CreatedEntryCount;
            public int UpdatedEntryCount;
            public bool Saved;
        }

        private sealed class LocalizationUpsertPlan
        {
            public int Index;
            public string Key;
            public string LocaleCode;
            public string StringValue;
            public string AssetPath;
            public string SubAssetName;
            public bool HasSmart;
            public bool Smart;
        }

        private static Dictionary<string, object> Error(string message)
        {
            return new Dictionary<string, object>
            {
                { "success", false },
                { "error", message },
            };
        }
    }
}
