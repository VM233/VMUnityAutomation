using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace VMUnityAutomation.Editor
{
    [InitializeOnLoad]
    internal static class MCPAddressablesCommands
    {
        internal const string JobType = "addressables-build";
        private const string SettingsDefaultTypeName =
            "UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject";
        private const string SettingsTypeName =
            "UnityEditor.AddressableAssets.Settings.AddressableAssetSettings";
        private static AddressablesBuildJob buildJob;
        private static bool updateRegistered;

        private sealed class ValidationEntry
        {
            internal string Guid;
            internal string AssetPath;
            internal string Address;
            internal string Group;
            internal readonly HashSet<string> Labels =
                new HashSet<string>(StringComparer.Ordinal);

            internal Dictionary<string, object> ToDictionary()
            {
                return new Dictionary<string, object>
                {
                    { "guid", Guid },
                    { "assetPath", AssetPath },
                    { "address", Address ?? "" },
                    { "labels", Labels.OrderBy(item => item).ToArray() },
                    { "group", Group ?? "" },
                };
            }
        }

        private sealed class ValidationState
        {
            internal readonly HashSet<string> Groups =
                new HashSet<string>(StringComparer.Ordinal);
            internal readonly HashSet<string> Labels =
                new HashSet<string>(StringComparer.Ordinal);
            internal readonly Dictionary<string, ValidationEntry> Entries =
                new Dictionary<string, ValidationEntry>(StringComparer.OrdinalIgnoreCase);
            internal string DefaultGroup;
        }

        static MCPAddressablesCommands()
        {
            buildJob = LoadBuildJob();
            if (buildJob == null || buildJob.IsTerminal)
                return;
            if (buildJob.Status == "running")
            {
                buildJob.Status = "failed";
                buildJob.Error = "Addressables build was interrupted by an Editor domain reload.";
                buildJob.UpdatedAt = DateTime.UtcNow;
                SaveBuildJob();
                return;
            }
            EnsureUpdateRegistered();
        }

        public static object Info(Dictionary<string, object> args)
        {
            if (!TryGetSettings(out object settings, out Type settingsType, out object error))
                return error;
            int offset = Math.Max(0, GetInt(args, "offset", 0));
            int limit = Math.Max(1, Math.Min(500, GetInt(args, "limit", 100)));

            var allEntries = new List<Dictionary<string, object>>();
            var groups = new List<Dictionary<string, object>>();
            foreach (object group in Enumerate(GetProperty(settingsType, settings, "groups")))
            {
                if (group == null)
                    continue;
                var groupEntries = Enumerate(GetProperty(group.GetType(), group, "entries"))
                    .Where(item => item != null)
                    .Select(item => EntryInfo(item, GetName(group)))
                    .ToList();
                allEntries.AddRange(groupEntries);
                groups.Add(new Dictionary<string, object>
                {
                    { "name", GetName(group) },
                    { "default", ReferenceEquals(group,
                        GetProperty(settingsType, settings, "DefaultGroup")) },
                    { "assetPath", group is UnityEngine.Object groupObject
                        ? AssetDatabase.GetAssetPath(groupObject)
                        : "" },
                    { "entryCount", groupEntries.Count },
                    { "schemaTypes", ReadSchemaTypes(group) },
                });
            }

            List<Dictionary<string, object>> page = allEntries.Skip(offset).Take(limit).ToList();
            return new Dictionary<string, object>
            {
                { "success", true },
                { "settingsPath", settings is UnityEngine.Object settingsObject
                    ? AssetDatabase.GetAssetPath(settingsObject)
                    : "" },
                { "defaultGroup", GetName(GetProperty(settingsType, settings, "DefaultGroup")) },
                { "labels", Invoke(settingsType, settings, "GetLabels") ?? Array.Empty<string>() },
                { "groupCount", groups.Count },
                { "groups", groups },
                { "totalEntries", allEntries.Count },
                { "offset", offset },
                { "limit", limit },
                { "entries", page },
                { "hasMore", offset + page.Count < allEntries.Count },
                { "nextOffset", offset + page.Count < allEntries.Count
                    ? (object)(offset + page.Count)
                    : null },
            };
        }

        public static object Transaction(Dictionary<string, object> args)
        {
            if (!TryGetSettings(out object settings, out Type settingsType, out object error))
                return error;
            List<object> operations = GetList(args, "operations");
            if (operations == null || operations.Count == 0)
                return MCPResponse.Error("operations must contain at least one operation.",
                    "invalid_arguments");
            bool dryRun = GetBool(args, "dryRun", false);

            var prepared = new List<Dictionary<string, object>>();
            try
            {
                ValidationState validationState = BuildValidationState(settings, settingsType);
                for (int index = 0; index < operations.Count; index++)
                {
                    if (!(operations[index] is Dictionary<string, object> operation))
                        throw new ArgumentException(
                            $"operations[{index}] must be an object.");
                    prepared.Add(ValidateOperation(operation, validationState));
                }
            }
            catch (Exception exception)
            {
                return MCPResponse.Error(exception.GetBaseException().Message,
                    "addressables_transaction_invalid");
            }

            if (dryRun)
            {
                return new Dictionary<string, object>
                {
                    { "success", true },
                    { "dryRun", true },
                    { "operationCount", prepared.Count },
                    { "operations", prepared },
                    { "settingsPath", settings is UnityEngine.Object settingsObject
                        ? AssetDatabase.GetAssetPath(settingsObject)
                        : "" },
                    { "defaultGroup",
                        GetName(GetProperty(settingsType, settings, "DefaultGroup")) },
                };
            }

            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("VM Unity Automation Edit Addressables");
            UnityEngine.Object[] undoObjects = CollectUndoObjects(settings, settingsType);
            if (undoObjects.Length > 0)
                Undo.RegisterCompleteObjectUndo(undoObjects,
                    "VM Unity Automation Edit Addressables");
            var results = new List<Dictionary<string, object>>();
            try
            {
                foreach (Dictionary<string, object> operation in operations
                             .Cast<Dictionary<string, object>>())
                    results.Add(ApplyOperation(settings, settingsType, operation));
                if (settings is UnityEngine.Object dirtySettings)
                    EditorUtility.SetDirty(dirtySettings);
                AssetDatabase.SaveAssets();
                Undo.CollapseUndoOperations(undoGroup);
                return new Dictionary<string, object>
                {
                    { "success", true },
                    { "operationCount", results.Count },
                    { "results", results },
                };
            }
            catch (Exception exception)
            {
                Undo.RevertAllDownToGroup(undoGroup);
                return MCPResponse.Error(exception.GetBaseException().Message,
                    "addressables_transaction_failed");
            }
        }

        private static ValidationState BuildValidationState(object settings, Type settingsType)
        {
            var state = new ValidationState
            {
                DefaultGroup = GetName(GetProperty(settingsType, settings, "DefaultGroup")),
            };
            foreach (object group in Enumerate(GetProperty(settingsType, settings, "groups")))
            {
                if (group == null)
                    continue;
                string groupName = GetName(group);
                state.Groups.Add(groupName);
                foreach (object entry in Enumerate(
                             GetProperty(group.GetType(), group, "entries")))
                {
                    if (entry == null)
                        continue;
                    Dictionary<string, object> info = EntryInfo(entry, groupName);
                    string guid = info["guid"].ToString();
                    var entryState = new ValidationEntry
                    {
                        Guid = guid,
                        AssetPath = info["assetPath"].ToString(),
                        Address = info["address"].ToString(),
                        Group = groupName,
                    };
                    foreach (string label in (string[])info["labels"])
                        entryState.Labels.Add(label);
                    state.Entries[guid] = entryState;
                }
            }
            foreach (string label in ReadLabels(settingsType, settings))
                state.Labels.Add(label);
            return state;
        }

        private static Dictionary<string, object> ValidateOperation(
            Dictionary<string, object> operation, ValidationState state)
        {
            string action = RequireString(operation, "action").ToLowerInvariant();
            var result = new Dictionary<string, object> { { "action", action } };
            switch (action)
            {
                case "create-group":
                {
                    EnsureOnlyKeys(operation, "action", "group", "setAsDefault",
                        "copySchemas", "copySchemasFromGroup");
                    string groupName = RequireString(operation, "group");
                    if (state.Groups.Contains(groupName))
                        throw new ArgumentException(
                            $"Addressables group '{groupName}' already exists.");
                    bool copySchemas = GetBool(operation, "copySchemas", true);
                    string copySource = GetString(operation, "copySchemasFromGroup");
                    if (!copySchemas && !string.IsNullOrEmpty(copySource))
                        throw new ArgumentException(
                            "copySchemasFromGroup cannot be used when copySchemas is false.");
                    if (copySchemas)
                    {
                        if (string.IsNullOrEmpty(copySource))
                            copySource = state.DefaultGroup;
                        if (string.IsNullOrEmpty(copySource) ||
                            !state.Groups.Contains(copySource))
                            throw new ArgumentException(
                                string.IsNullOrEmpty(GetString(operation,
                                    "copySchemasFromGroup"))
                                    ? "The default Addressables group was not found for schema copying."
                                    : $"Schema source group '{copySource}' was not found.");
                    }
                    bool setAsDefault = GetBool(operation, "setAsDefault", false);
                    result["group"] = groupName;
                    result["setAsDefault"] = setAsDefault;
                    result["copySchemas"] = copySchemas;
                    if (copySchemas)
                        result["copySchemasFrom"] = copySource;
                    state.Groups.Add(groupName);
                    if (setAsDefault)
                        state.DefaultGroup = groupName;
                    return result;
                }
                case "remove-group":
                case "set-default-group":
                {
                    EnsureOnlyKeys(operation, "action", "group");
                    string group = RequireString(operation, "group");
                    if (!state.Groups.Contains(group))
                        throw new ArgumentException(
                            $"Addressables group '{group}' was not found.");
                    if (action == "remove-group" &&
                        string.Equals(group, state.DefaultGroup, StringComparison.Ordinal))
                        throw new ArgumentException(
                            "The default Addressables group cannot be removed. Set another default group first.");
                    result["group"] = group;
                    result["entryCount"] = state.Entries.Values.Count(entry =>
                        string.Equals(entry.Group, group, StringComparison.Ordinal));
                    if (action == "set-default-group")
                    {
                        state.DefaultGroup = group;
                    }
                    else
                    {
                        state.Groups.Remove(group);
                        foreach (string guid in state.Entries.Values
                                     .Where(entry => string.Equals(entry.Group, group,
                                         StringComparison.Ordinal))
                                     .Select(entry => entry.Guid).ToArray())
                            state.Entries.Remove(guid);
                    }
                    return result;
                }
                case "add-label":
                case "remove-label":
                {
                    EnsureOnlyKeys(operation, "action", "label");
                    string label = RequireString(operation, "label");
                    bool exists = state.Labels.Contains(label);
                    if (action == "add-label" && exists)
                        throw new ArgumentException(
                            $"Addressables label '{label}' already exists.");
                    if (action == "remove-label" && !exists)
                        throw new ArgumentException(
                            $"Addressables label '{label}' was not found.");
                    result["label"] = label;
                    if (action == "add-label")
                    {
                        state.Labels.Add(label);
                    }
                    else
                    {
                        state.Labels.Remove(label);
                        foreach (ValidationEntry entry in state.Entries.Values)
                            entry.Labels.Remove(label);
                    }
                    return result;
                }
                case "rename-label":
                {
                    EnsureOnlyKeys(operation, "action", "oldLabel", "newLabel");
                    string oldLabel = RequireString(operation, "oldLabel");
                    string newLabel = RequireString(operation, "newLabel");
                    if (!state.Labels.Contains(oldLabel))
                        throw new ArgumentException(
                            $"Addressables label '{oldLabel}' was not found.");
                    if (state.Labels.Contains(newLabel))
                        throw new ArgumentException(
                            $"Addressables label '{newLabel}' already exists.");
                    result["oldLabel"] = oldLabel;
                    result["newLabel"] = newLabel;
                    state.Labels.Remove(oldLabel);
                    state.Labels.Add(newLabel);
                    foreach (ValidationEntry entry in state.Entries.Values)
                    {
                        if (!entry.Labels.Remove(oldLabel))
                            continue;
                        entry.Labels.Add(newLabel);
                    }
                    return result;
                }
                case "create-or-move-entry":
                {
                    EnsureOnlyKeys(operation, "action", "guid", "assetPath", "group",
                        "address");
                    string guid = ResolveGuid(operation);
                    string targetGroup = ResolveValidationGroup(operation, state);
                    bool existing = state.Entries.TryGetValue(guid,
                        out ValidationEntry entry);
                    result["guid"] = guid;
                    result["assetPath"] = AssetDatabase.GUIDToAssetPath(guid);
                    result["group"] = targetGroup;
                    result["existing"] = existing;
                    string address = GetString(operation, "address");
                    if (!string.IsNullOrEmpty(address))
                        result["address"] = address;
                    if (!existing)
                    {
                        entry = new ValidationEntry
                        {
                            Guid = guid,
                            AssetPath = AssetDatabase.GUIDToAssetPath(guid),
                        };
                        state.Entries.Add(guid, entry);
                    }
                    entry.Group = targetGroup;
                    if (!string.IsNullOrEmpty(address))
                        entry.Address = address;
                    return result;
                }
                case "set-address":
                case "set-label":
                case "remove-entry":
                {
                    EnsureOnlyKeys(operation, action == "set-address"
                            ? new[] { "action", "guid", "assetPath", "address" }
                            : action == "set-label"
                                ? new[] { "action", "guid", "assetPath", "label", "enabled" }
                                : new[] { "action", "guid", "assetPath" });
                    string guid = ResolveGuid(operation);
                    if (!state.Entries.TryGetValue(guid, out ValidationEntry entry))
                        throw new ArgumentException(
                            $"Addressables entry '{guid}' was not found.");
                    result["entry"] = entry.ToDictionary();
                    if (action == "set-address")
                    {
                        string address = RequireString(operation, "address");
                        result["address"] = address;
                        entry.Address = address;
                    }
                    if (action == "set-label")
                    {
                        string label = RequireString(operation, "label");
                        if (!state.Labels.Contains(label))
                            throw new ArgumentException(
                                $"Addressables label '{label}' does not exist. Add it first.");
                        result["label"] = label;
                        bool enabled = GetBool(operation, "enabled", true);
                        result["enabled"] = enabled;
                        if (enabled)
                            entry.Labels.Add(label);
                        else
                            entry.Labels.Remove(label);
                    }
                    if (action == "remove-entry")
                        state.Entries.Remove(guid);
                    return result;
                }
                default:
                    throw new ArgumentException(
                        $"Unsupported Addressables action '{action}'. Expected create-group, remove-group, set-default-group, add-label, remove-label, rename-label, create-or-move-entry, set-address, set-label, or remove-entry.");
            }
        }

        private static string ResolveValidationGroup(Dictionary<string, object> operation,
            ValidationState state)
        {
            string group = GetString(operation, "group");
            if (string.IsNullOrEmpty(group))
                group = state.DefaultGroup;
            if (string.IsNullOrEmpty(group) || !state.Groups.Contains(group))
                throw new ArgumentException(
                    string.IsNullOrEmpty(GetString(operation, "group"))
                        ? "The default Addressables group was not found."
                        : $"Addressables group '{group}' was not found.");
            return group;
        }

        private static void EnsureOnlyKeys(Dictionary<string, object> operation,
            params string[] allowed)
        {
            var allowedSet = new HashSet<string>(allowed, StringComparer.Ordinal);
            string unexpected = operation.Keys.FirstOrDefault(key =>
                !allowedSet.Contains(key));
            if (!string.IsNullOrEmpty(unexpected))
                throw new ArgumentException(
                    $"Unsupported field '{unexpected}' for Addressables action " +
                    $"'{GetString(operation, "action")}'. Allowed fields: " +
                    string.Join(", ", allowed.OrderBy(item => item)) + ".");
        }

        public static object StartBuild(Dictionary<string, object> args)
        {
            if (!MCPCapabilityRegistry.IsCapabilityAvailable("addressables"))
                return MCPResponse.Error(
                    "Addressables is unavailable. Install com.unity.addressables.",
                    "capability_unavailable");
            if (buildJob != null && !buildJob.IsTerminal)
            {
                if (!string.Equals(buildJob.OwnerAgentId ?? "anonymous",
                        GetString(args, "_agentId", "anonymous"), StringComparison.Ordinal))
                    return MCPResponse.Error("Addressables build belongs to another agent.",
                        "job_owner_mismatch");
                return MCPResponse.Error("An Addressables build is already active.",
                    "job_already_running", true, BuildJobResponse(buildJob));
            }

            buildJob = new AddressablesBuildJob
            {
                JobId = Guid.NewGuid().ToString("N").Substring(0, 12),
                OwnerAgentId = GetString(args, "_agentId", "anonymous"),
                Status = "queued",
                StartedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            };
            SaveBuildJob();
            EnsureUpdateRegistered();
            return BuildJobResponse(buildJob);
        }

        internal static object CancelBuild(Dictionary<string, object> args)
        {
            if (buildJob == null)
                buildJob = LoadBuildJob();
            string jobId = GetString(args, "jobId");
            if (buildJob == null || (!string.IsNullOrEmpty(jobId) && buildJob.JobId != jobId))
                return MCPResponse.Error($"Addressables build '{jobId}' was not found.",
                    "job_not_found");
            if (!MCPJobHistory.CanAccess(JobType, buildJob.JobId,
                    buildJob.OwnerAgentId ?? "anonymous", args))
                return MCPResponse.Error(
                    "Addressables build belongs to another agent and the jobAccessToken was not supplied.",
                    "job_owner_mismatch");
            if (buildJob.IsTerminal)
                return MCPResponse.Error("Addressables build is already terminal.",
                    "job_already_terminal", false, BuildJobResponse(buildJob));
            if (buildJob.Status != "queued")
                return MCPResponse.Error(
                    "Addressables BuildPlayerContent is synchronous and cannot be preempted after it starts.",
                    "job_not_preemptible", false, BuildJobResponse(buildJob));

            buildJob.Status = "canceled";
            buildJob.Error = "Canceled before Addressables BuildPlayerContent started.";
            buildJob.UpdatedAt = DateTime.UtcNow;
            SaveBuildJob();
            UnregisterUpdate();
            var response = BuildJobResponse(buildJob);
            response["cancelMode"] = "pre-start";
            response["canceled"] = true;
            return response;
        }

        internal static object GetBuildJob(Dictionary<string, object> args)
        {
            if (buildJob == null)
                buildJob = LoadBuildJob();
            string jobId = GetString(args, "jobId");
            if (buildJob == null ||
                (!string.IsNullOrEmpty(jobId) && buildJob.JobId != jobId))
            {
                return MCPResponse.Error(
                    $"Addressables build '{jobId}' was not found.", "job_not_found");
            }
            if (!MCPJobHistory.CanAccess(JobType, buildJob.JobId,
                    buildJob.OwnerAgentId, args))
            {
                return MCPResponse.Error(
                    "Addressables build belongs to another agent and the jobAccessToken was not supplied.",
                    "job_owner_mismatch");
            }
            return BuildJobResponse(buildJob);
        }

        private static Dictionary<string, object> ApplyOperation(object settings, Type settingsType,
            Dictionary<string, object> operation)
        {
            string action = GetString(operation, "action").ToLowerInvariant();
            if (action == "create-group")
            {
                string groupName = GetString(operation, "group");
                if (string.IsNullOrEmpty(groupName))
                    throw new ArgumentException("group is required for create-group.");
                if (FindGroup(settings, settingsType, groupName) != null)
                    throw new ArgumentException($"Addressables group '{groupName}' already exists.");
                MethodInfo create = settingsType.GetMethods()
                    .Where(method => method.Name == "CreateGroup")
                    .OrderByDescending(method => method.GetParameters().Length)
                    .FirstOrDefault();
                if (create == null)
                    throw new MissingMethodException(settingsType.FullName, "CreateGroup");
                ParameterInfo[] parameters = create.GetParameters();
                var values = new object[parameters.Length];
                object schemaSource = ResolveSchemaCopySource(settings, settingsType,
                    operation);
                object schemasToCopy = schemaSource == null
                    ? null
                    : GetProperty(schemaSource.GetType(), schemaSource, "Schemas") ??
                      GetProperty(schemaSource.GetType(), schemaSource, "schemas");
                for (int index = 0; index < parameters.Length; index++)
                {
                    Type parameterType = parameters[index].ParameterType;
                    if (index == 0) values[index] = groupName;
                    else if (parameterType == typeof(bool))
                        values[index] = index == 1
                            ? GetBool(operation, "setAsDefault", false)
                            : index == 3;
                    else if (schemasToCopy != null &&
                             parameterType.IsInstanceOfType(schemasToCopy))
                        values[index] = schemasToCopy;
                    else if (parameterType.IsArray)
                        values[index] = Array.CreateInstance(parameterType.GetElementType(), 0);
                    else
                        values[index] = null;
                }
                object group = create.Invoke(settings, values);
                return new Dictionary<string, object>
                {
                    { "action", action },
                    { "group", GetName(group) },
                    { "schemaTypes", ReadSchemaTypes(group) },
                };
            }

            if (action == "remove-group" || action == "set-default-group")
            {
                object group = FindGroup(settings, settingsType,
                    GetString(operation, "group"));
                if (group == null)
                    throw new ArgumentException(
                        $"Addressables group '{GetString(operation, "group")}' was not found.");
                if (action == "set-default-group")
                {
                    SetProperty(settingsType, settings, "DefaultGroup", group);
                    return new Dictionary<string, object>
                    {
                        { "action", action },
                        { "group", GetName(group) },
                    };
                }
                InvokeCompatible(settingsType, settings, "RemoveGroup", group);
                return new Dictionary<string, object>
                {
                    { "action", action },
                    { "group", GetName(group) },
                    { "removed", true },
                };
            }

            if (action == "add-label" || action == "remove-label" ||
                action == "rename-label")
            {
                if (action == "add-label")
                    InvokeCompatible(settingsType, settings, "AddLabel",
                        GetString(operation, "label"), true);
                else if (action == "remove-label")
                    InvokeCompatible(settingsType, settings, "RemoveLabel",
                        GetString(operation, "label"), true);
                else
                    InvokeCompatible(settingsType, settings, "RenameLabel",
                        GetString(operation, "oldLabel"),
                        GetString(operation, "newLabel"), true);
                return new Dictionary<string, object>
                {
                    { "action", action },
                    { "label", action == "rename-label"
                        ? GetString(operation, "newLabel")
                        : GetString(operation, "label") },
                };
            }

            string guid = ResolveGuid(operation);
            object entry = FindEntry(settings, settingsType, guid);
            if (action == "create-or-move-entry")
            {
                object targetGroup = ResolveTargetGroup(settings, settingsType,
                    operation);
                MethodInfo createOrMove = settingsType.GetMethods()
                    .Where(method => method.Name == "CreateOrMoveEntry")
                    .OrderByDescending(method => method.GetParameters().Length)
                    .FirstOrDefault(method =>
                    {
                        ParameterInfo[] parameters = method.GetParameters();
                        return parameters.Length >= 2 && parameters[0].ParameterType == typeof(string);
                    });
                if (createOrMove == null)
                    throw new MissingMethodException(settingsType.FullName, "CreateOrMoveEntry");
                entry = createOrMove.Invoke(settings,
                    BuildOptionalArguments(createOrMove, guid, targetGroup));
                string address = GetString(operation, "address");
                if (!string.IsNullOrEmpty(address))
                    SetProperty(entry.GetType(), entry, "address", address);
                return EntryInfo(entry, GetName(targetGroup));
            }

            if (entry == null)
                throw new ArgumentException($"Addressables entry '{guid}' was not found.");
            switch (action)
            {
                case "set-address":
                    string address = GetString(operation, "address");
                    if (string.IsNullOrEmpty(address))
                        throw new ArgumentException("address is required for set-address.");
                    SetProperty(entry.GetType(), entry, "address", address);
                    break;
                case "set-label":
                    string label = GetString(operation, "label");
                    if (string.IsNullOrEmpty(label))
                        throw new ArgumentException("label is required for set-label.");
                    MethodInfo setLabel = entry.GetType().GetMethods()
                        .Where(method => method.Name == "SetLabel")
                        .OrderByDescending(method => method.GetParameters().Length)
                        .FirstOrDefault();
                    if (setLabel == null)
                        throw new MissingMethodException(entry.GetType().FullName, "SetLabel");
                    setLabel.Invoke(entry, BuildOptionalArguments(setLabel, label,
                        GetBool(operation, "enabled", true)));
                    break;
                case "remove-entry":
                    MethodInfo remove = settingsType.GetMethods()
                        .Where(method => method.Name == "RemoveAssetEntry")
                        .FirstOrDefault(method =>
                        {
                            ParameterInfo[] parameters = method.GetParameters();
                            return parameters.Length >= 1 &&
                                   parameters[0].ParameterType == typeof(string);
                        });
                    if (remove == null)
                        throw new MissingMethodException(settingsType.FullName,
                            "RemoveAssetEntry");
                    remove.Invoke(settings, BuildOptionalArguments(remove, guid));
                    break;
            }

            return new Dictionary<string, object>
            {
                { "action", action },
                { "guid", guid },
                { "entry", action == "remove-entry" ? null : EntryInfo(entry, "") },
            };
        }

        private static object ResolveTargetGroup(object settings, Type settingsType,
            Dictionary<string, object> operation)
        {
            string name = GetString(operation, "group");
            object target = string.IsNullOrEmpty(name)
                ? GetProperty(settingsType, settings, "DefaultGroup")
                : FindGroup(settings, settingsType, name);
            if (target == null)
                throw new ArgumentException(
                    string.IsNullOrEmpty(name)
                        ? "The default Addressables group was not found."
                        : $"Addressables group '{name}' was not found.");
            return target;
        }

        private static object ResolveSchemaCopySource(object settings, Type settingsType,
            Dictionary<string, object> operation)
        {
            if (!GetBool(operation, "copySchemas", true))
                return null;
            string name = GetString(operation, "copySchemasFromGroup");
            object source = string.IsNullOrEmpty(name)
                ? GetProperty(settingsType, settings, "DefaultGroup")
                : FindGroup(settings, settingsType, name);
            if (source == null)
                throw new ArgumentException(
                    string.IsNullOrEmpty(name)
                        ? "The default Addressables group was not found for schema copying."
                        : $"Schema source group '{name}' was not found.");
            return source;
        }

        private static string[] ReadLabels(Type settingsType, object settings)
        {
            return Enumerate(Invoke(settingsType, settings, "GetLabels"))
                .Select(item => item?.ToString())
                .Where(item => !string.IsNullOrEmpty(item))
                .ToArray();
        }

        private static UnityEngine.Object[] CollectUndoObjects(object settings,
            Type settingsType)
        {
            var objects = new List<UnityEngine.Object>();
            if (settings is UnityEngine.Object settingsObject)
                objects.Add(settingsObject);
            foreach (object group in Enumerate(
                         GetProperty(settingsType, settings, "groups")))
            {
                if (!(group is UnityEngine.Object groupObject))
                    continue;
                objects.Add(groupObject);
                string path = AssetDatabase.GetAssetPath(groupObject);
                if (!string.IsNullOrEmpty(path))
                    objects.AddRange(AssetDatabase.LoadAllAssetsAtPath(path));
            }
            return objects.Where(item => item != null).Distinct().ToArray();
        }

        private static bool TryGetSettings(out object settings, out Type settingsType,
            out object error)
        {
            settings = null;
            settingsType = MCPAssetGraphUtility.FindType(SettingsTypeName);
            Type defaultType = MCPAssetGraphUtility.FindType(SettingsDefaultTypeName);
            if (settingsType == null || defaultType == null)
            {
                error = MCPResponse.Error(
                    "Addressables is unavailable. Install com.unity.addressables.",
                    "capability_unavailable");
                return false;
            }
            PropertyInfo property = defaultType.GetProperty("Settings",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            settings = property?.GetValue(null);
            if (settings == null)
            {
                error = MCPResponse.Error(
                    "Addressables settings have not been created for this project.",
                    "addressables_settings_missing");
                return false;
            }
            error = null;
            return true;
        }

        private static object FindGroup(object settings, Type settingsType, string name)
        {
            return Enumerate(GetProperty(settingsType, settings, "groups")).FirstOrDefault(group =>
                string.Equals(GetName(group), name, StringComparison.Ordinal));
        }

        private static object FindEntry(object settings, Type settingsType, string guid)
        {
            foreach (object group in Enumerate(GetProperty(settingsType, settings, "groups")))
            {
                object entry = Enumerate(GetProperty(group.GetType(), group, "entries"))
                    .FirstOrDefault(candidate =>
                        string.Equals(GetProperty(candidate.GetType(), candidate, "guid")?.ToString(),
                            guid, StringComparison.OrdinalIgnoreCase));
                if (entry != null)
                    return entry;
            }
            return null;
        }

        private static Dictionary<string, object> EntryInfo(object entry, string group)
        {
            Type type = entry.GetType();
            string guid = GetProperty(type, entry, "guid")?.ToString() ?? "";
            return new Dictionary<string, object>
            {
                { "guid", guid },
                { "assetPath", AssetDatabase.GUIDToAssetPath(guid) },
                { "address", GetProperty(type, entry, "address")?.ToString() ?? "" },
                { "labels", Enumerate(GetProperty(type, entry, "labels"))
                    .Select(item => item?.ToString()).Where(item => !string.IsNullOrEmpty(item))
                    .OrderBy(item => item).ToArray() },
                { "group", group ?? "" },
            };
        }

        private static string[] ReadSchemaTypes(object group)
        {
            object schemas = GetProperty(group.GetType(), group, "Schemas") ??
                             GetProperty(group.GetType(), group, "schemas");
            return Enumerate(schemas).Where(item => item != null)
                .Select(item => item.GetType().FullName).ToArray();
        }

        private static string ResolveGuid(Dictionary<string, object> operation)
        {
            string guid = GetString(operation, "guid");
            if (!string.IsNullOrEmpty(guid))
            {
                if (string.IsNullOrEmpty(AssetDatabase.GUIDToAssetPath(guid)))
                    throw new ArgumentException($"Asset GUID '{guid}' was not found.");
                return guid;
            }
            string assetPath = GetString(operation, "assetPath");
            if (string.IsNullOrEmpty(assetPath))
                throw new ArgumentException("guid or assetPath is required.");
            guid = AssetDatabase.AssetPathToGUID(assetPath);
            if (string.IsNullOrEmpty(guid))
                throw new ArgumentException($"Asset '{assetPath}' was not found.");
            return guid;
        }

        private static void ContinueBuild()
        {
            if (buildJob == null || buildJob.IsTerminal)
            {
                UnregisterUpdate();
                return;
            }
            if (buildJob.Status != "queued" || EditorApplication.isCompiling ||
                EditorApplication.isUpdating)
                return;

            buildJob.Status = "running";
            buildJob.UpdatedAt = DateTime.UtcNow;
            SaveBuildJob();
            try
            {
                Type settingsType = MCPAssetGraphUtility.FindType(SettingsTypeName);
                if (settingsType == null)
                    throw new InvalidOperationException("Addressables settings type disappeared.");
                MethodInfo build = settingsType.GetMethods(BindingFlags.Static | BindingFlags.Public)
                    .FirstOrDefault(method =>
                    {
                        if (method.Name != "BuildPlayerContent")
                            return false;
                        ParameterInfo[] parameters = method.GetParameters();
                        return parameters.Length == 1 && parameters[0].IsOut;
                    });
                if (build == null)
                    throw new MissingMethodException(settingsType.FullName,
                        "BuildPlayerContent(out result)");
                var invokeArgs = new object[] { null };
                build.Invoke(null, invokeArgs);
                object result = invokeArgs[0];
                string buildError = GetProperty(result?.GetType(), result, "Error")?.ToString() ?? "";
                buildJob.Result = SerializeBuildResult(result);
                buildJob.Status = string.IsNullOrEmpty(buildError) ? "succeeded" : "failed";
                buildJob.Error = buildError;
            }
            catch (Exception exception)
            {
                buildJob.Status = "failed";
                buildJob.Error = exception.GetBaseException().Message;
            }
            finally
            {
                buildJob.UpdatedAt = DateTime.UtcNow;
                SaveBuildJob();
                UnregisterUpdate();
            }
        }

        private static Dictionary<string, object> SerializeBuildResult(object result)
        {
            if (result == null)
                return new Dictionary<string, object>();
            Type type = result.GetType();
            return new Dictionary<string, object>
            {
                { "type", type.FullName },
                { "error", GetProperty(type, result, "Error")?.ToString() ?? "" },
                { "outputPath", GetProperty(type, result, "OutputPath")?.ToString() ?? "" },
                { "duration", GetProperty(type, result, "Duration") ?? 0d },
                { "locationCount", GetProperty(type, result, "LocationCount") ?? 0 },
            };
        }

        private static Dictionary<string, object> BuildJobResponse(AddressablesBuildJob job)
        {
            var response = new Dictionary<string, object>
            {
                { "success", job.IsTerminal ? job.Status == "succeeded" : true },
                { "jobId", job.JobId },
                { "jobType", JobType },
                { "status", job.Status },
                { "pollRoute", "jobs/get" },
                { "startedAt", job.StartedAt.ToString("O") },
                { "updatedAt", job.UpdatedAt.ToString("O") },
            };
            if (!string.IsNullOrEmpty(job.Error)) response["error"] = job.Error;
            if (job.Result != null) response["result"] = job.Result;
            MCPJobHistory.PublishAccessToken(response, JobType, job.JobId,
                job.OwnerAgentId);
            return response;
        }

        private static void EnsureUpdateRegistered()
        {
            if (updateRegistered) return;
            EditorApplication.update += ContinueBuild;
            updateRegistered = true;
        }

        private static void UnregisterUpdate()
        {
            if (!updateRegistered) return;
            EditorApplication.update -= ContinueBuild;
            updateRegistered = false;
        }

        private static void SaveBuildJob()
        {
            if (buildJob == null) return;
            string path = GetBuildJobPath();
            MCPPersistenceFile.WriteAllText(path, MiniJson.Serialize(buildJob.ToDictionary()));
            MCPJobHistory.Record(JobType, buildJob.JobId, buildJob.OwnerAgentId,
                buildJob.Status, BuildJobResponse(buildJob));
        }

        private static AddressablesBuildJob LoadBuildJob()
        {
            try
            {
                string path = GetBuildJobPath();
                if (!MCPPersistenceFile.TryReadAllText(path, out string contents)) return null;
                return AddressablesBuildJob.FromDictionary(
                    MiniJson.Deserialize(contents) as Dictionary<string, object>);
            }
            catch
            {
                return null;
            }
        }

        private static string GetBuildJobPath()
        {
            return Path.Combine(Path.GetFullPath(Path.Combine(Application.dataPath, "..")),
                "Library", "VMUnityAutomation", "addressables-build-job.json");
        }

        private static object[] BuildOptionalArguments(MethodInfo method, params object[] required)
        {
            ParameterInfo[] parameters = method.GetParameters();
            var values = new object[parameters.Length];
            for (int index = 0; index < parameters.Length; index++)
            {
                if (index < required.Length)
                {
                    values[index] = required[index];
                    continue;
                }
                values[index] = parameters[index].HasDefaultValue
                    ? parameters[index].DefaultValue
                    : parameters[index].ParameterType == typeof(bool)
                        ? (object)true
                        : parameters[index].ParameterType.IsValueType
                            ? Activator.CreateInstance(parameters[index].ParameterType)
                            : null;
            }
            return values;
        }

        private static object GetProperty(Type type, object target, string name)
        {
            return type?.GetProperty(name,
                BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public |
                BindingFlags.NonPublic)?.GetValue(target);
        }

        private static void SetProperty(Type type, object target, string name, object value)
        {
            PropertyInfo property = type.GetProperty(name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property == null || !property.CanWrite)
                throw new MissingMemberException(type.FullName, name);
            property.SetValue(target, value);
        }

        private static object InvokeCompatible(Type type, object target, string name,
            params object[] required)
        {
            MethodInfo method = type.GetMethods(
                    BindingFlags.Instance | BindingFlags.Public |
                    BindingFlags.NonPublic)
                .Where(candidate => candidate.Name == name)
                .OrderBy(candidate => candidate.GetParameters().Length)
                .FirstOrDefault(candidate =>
                {
                    ParameterInfo[] parameters = candidate.GetParameters();
                    if (parameters.Length < required.Length)
                        return false;
                    for (int index = 0; index < required.Length; index++)
                    {
                        object value = required[index];
                        if (value != null &&
                            !parameters[index].ParameterType.IsInstanceOfType(value))
                            return false;
                    }
                    return parameters.Skip(required.Length)
                        .All(parameter => parameter.HasDefaultValue);
                });
            if (method == null)
                throw new MissingMethodException(type.FullName, name);
            object[] values = new object[method.GetParameters().Length];
            for (int index = 0; index < values.Length; index++)
            {
                values[index] = index < required.Length
                    ? required[index]
                    : method.GetParameters()[index].DefaultValue;
            }
            return method.Invoke(target, values);
        }

        private static object Invoke(Type type, object target, string name)
        {
            return type.GetMethod(name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null, Type.EmptyTypes, null)?.Invoke(target, null);
        }

        private static IEnumerable<object> Enumerate(object value)
        {
            return value is IEnumerable enumerable
                ? enumerable.Cast<object>()
                : Enumerable.Empty<object>();
        }

        private static string GetName(object value)
        {
            if (value == null) return "";
            if (value is UnityEngine.Object unityObject) return unityObject.name ?? "";
            return GetProperty(value.GetType(), value, "Name")?.ToString() ??
                   GetProperty(value.GetType(), value, "name")?.ToString() ?? "";
        }

        private static string GetString(Dictionary<string, object> values, string key,
            string defaultValue = "")
        {
            return values != null && values.TryGetValue(key, out object value) && value != null
                ? value.ToString()
                : defaultValue;
        }

        private static string RequireString(Dictionary<string, object> values, string key)
        {
            string value = GetString(values, key);
            if (string.IsNullOrEmpty(value))
                throw new ArgumentException($"{key} is required.");
            return value;
        }

        private static int GetInt(Dictionary<string, object> values, string key, int defaultValue)
        {
            return values != null && values.TryGetValue(key, out object value) && value != null
                ? Convert.ToInt32(value)
                : defaultValue;
        }

        private static bool GetBool(Dictionary<string, object> values, string key, bool defaultValue)
        {
            return values != null && values.TryGetValue(key, out object value) && value != null
                ? Convert.ToBoolean(value)
                : defaultValue;
        }

        private static List<object> GetList(Dictionary<string, object> values, string key)
        {
            return values != null && values.TryGetValue(key, out object value)
                ? value as List<object>
                : null;
        }

        private sealed class AddressablesBuildJob
        {
            public string JobId;
            public string OwnerAgentId;
            public string Status;
            public string Error;
            public Dictionary<string, object> Result;
            public DateTime StartedAt;
            public DateTime UpdatedAt;
            public bool IsTerminal => Status == "succeeded" || Status == "failed" ||
                                      Status == "canceled";

            public Dictionary<string, object> ToDictionary()
            {
                return new Dictionary<string, object>
                {
                    { "jobId", JobId },
                    { "ownerAgentId", OwnerAgentId },
                    { "status", Status },
                    { "error", Error ?? "" },
                    { "result", Result },
                    { "startedAt", StartedAt.ToString("O") },
                    { "updatedAt", UpdatedAt.ToString("O") },
                };
            }

            public static AddressablesBuildJob FromDictionary(Dictionary<string, object> values)
            {
                if (values == null) return null;
                return new AddressablesBuildJob
                {
                    JobId = GetString(values, "jobId"),
                    OwnerAgentId = GetString(values, "ownerAgentId", "anonymous"),
                    Status = GetString(values, "status"),
                    Error = GetString(values, "error"),
                    Result = values.TryGetValue("result", out object result)
                        ? result as Dictionary<string, object>
                        : null,
                    StartedAt = ParseDate(GetString(values, "startedAt")),
                    UpdatedAt = ParseDate(GetString(values, "updatedAt")),
                };
            }

            private static DateTime ParseDate(string value)
            {
                return DateTime.TryParse(value, out DateTime parsed)
                    ? parsed
                    : DateTime.UtcNow;
            }
        }
    }
}
