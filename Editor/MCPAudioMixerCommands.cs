using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;

namespace VMUnityAutomation.Editor
{
    internal static class MCPAudioMixerCommands
    {
        private static readonly HashSet<string> RuntimeActions =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "set-exposed-parameter",
                "clear-exposed-parameter",
            };

        private static readonly HashSet<string> PersistentActions =
            new HashSet<string>(StringComparer.Ordinal)
            {
                "rename",
                "create-group",
                "remove-group",
                "set-group-state",
                "create-snapshot",
                "remove-snapshot",
                "set-target-snapshot",
                "add-effect",
                "remove-effect",
                "set-effect-bypass",
                "expose-effect-parameter",
                "unexpose-parameter",
                "set-snapshot-parameter",
                "set-property",
            };

        public static object Info(Dictionary<string, object> args)
        {
            if (!TryValidateTopLevelKeys(args,
                    new[]
                    {
                        "assetPath", "includeSerialized", "maxObjects", "maxProperties",
                        "maxGroups", "maxSnapshots", "maxEffects",
                        "maxChildrenPerGroup", "maxEffectsPerGroup",
                        "maxParametersPerEffect",
                        "maxExposedParameters", "_agentId",
                    },
                    out object keyError))
                return keyError;
            if (!TryLoad(args, out string assetPath, out AudioMixer mixer,
                    out UnityEngine.Object controller, out object error))
                return error;

            UnityEngine.Object[] subAssets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            List<UnityEngine.Object> groups = OfKind(subAssets, "AudioMixerGroupController");
            List<UnityEngine.Object> snapshots =
                OfKind(subAssets, "AudioMixerSnapshotController");
            List<UnityEngine.Object> effects = OfKind(subAssets, "AudioMixerEffectController");
            int maxGroups = BoundedInt(args, "maxGroups", 100, 500);
            int maxSnapshots = BoundedInt(args, "maxSnapshots", 100, 500);
            int maxEffects = BoundedInt(args, "maxEffects", 100, 500);
            int maxChildrenPerGroup =
                BoundedInt(args, "maxChildrenPerGroup", 50, 200);
            int maxEffectsPerGroup = BoundedInt(args, "maxEffectsPerGroup", 50, 200);
            int maxParametersPerEffect =
                BoundedInt(args, "maxParametersPerEffect", 50, 200);
            int maxExposedParameters =
                BoundedInt(args, "maxExposedParameters", 100, 500);
            List<Dictionary<string, object>> exposedParameters =
                ReadExposedParameters(controller);
            var response = new Dictionary<string, object>
            {
                { "success", true },
                { "assetPath", assetPath },
                { "name", mixer.name ?? "" },
                { "groupCount", groups.Count },
                { "groups", groups.Take(maxGroups)
                    .Select(group => GroupSummary(group, maxChildrenPerGroup,
                        maxEffectsPerGroup)).ToList() },
                { "groupsTruncated", groups.Count > maxGroups },
                { "snapshotCount", snapshots.Count },
                { "snapshots", snapshots.Take(maxSnapshots).Select(snapshot =>
                    SnapshotSummary(snapshot, controller)).ToList() },
                { "snapshotsTruncated", snapshots.Count > maxSnapshots },
                { "effectCount", effects.Count },
                { "effects", effects.Take(maxEffects).Select(effect =>
                    EffectSummary(effect, groups, maxParametersPerEffect)).ToList() },
                { "effectsTruncated", effects.Count > maxEffects },
                { "exposedParameterCount", exposedParameters.Count },
                { "exposedParameters", exposedParameters.Take(maxExposedParameters).ToList() },
                { "exposedParametersTruncated",
                    exposedParameters.Count > maxExposedParameters },
            };
            if (GetBool(args, "includeSerialized", false))
            {
                response["serializedGraph"] = MCPAssetGraphUtility.InspectAsset(assetPath,
                    _ => true, GetInt(args, "maxObjects", 100),
                    GetInt(args, "maxProperties", 40));
            }
            return response;
        }

        public static object Transaction(Dictionary<string, object> args)
        {
            if (!TryValidateTopLevelKeys(args,
                    new[] { "assetPath", "operations", "dryRun", "_agentId" },
                    out object keyError))
                return keyError;
            if (!TryLoad(args, out string assetPath, out AudioMixer mixer,
                    out UnityEngine.Object controller, out object error))
                return error;
            List<object> rawOperations = GetList(args, "operations");
            if (rawOperations == null || rawOperations.Count == 0)
                return MCPResponse.Error("operations must contain at least one operation.",
                    "invalid_arguments");

            var operations = new List<Dictionary<string, object>>();
            bool hasRuntime = false;
            bool hasPersistent = false;
            for (int index = 0; index < rawOperations.Count; index++)
            {
                if (!(rawOperations[index] is Dictionary<string, object> operation))
                    return MCPResponse.Error($"operations[{index}] must be an object.",
                        "invalid_arguments");
                string action = GetString(operation, "action").ToLowerInvariant();
                try
                {
                    ValidateOperationKeys(operation, action);
                }
                catch (Exception exception)
                {
                    return MCPResponse.Error(exception.Message,
                        "invalid_arguments");
                }
                if (RuntimeActions.Contains(action))
                    hasRuntime = true;
                else if (PersistentActions.Contains(action))
                    hasPersistent = true;
                else
                    return MCPResponse.Error(
                        $"operations[{index}].action '{action}' is not supported.",
                        "invalid_arguments", false, new Dictionary<string, object>
                        {
                            { "persistentActions", PersistentActions.OrderBy(value => value).ToArray() },
                            { "runtimeActions", RuntimeActions.OrderBy(value => value).ToArray() },
                        });
                operations.Add(operation);
            }

            if (hasRuntime && hasPersistent)
            {
                return MCPResponse.Error(
                    "Persistent mixer edits and Editor-session runtime overrides cannot be mixed in one transaction.",
                    "mixed_audio_mixer_persistence");
            }

            return hasRuntime
                ? ApplyRuntimeOperations(assetPath, mixer, operations,
                    GetBool(args, "dryRun", false))
                : ApplyPersistentOperations(assetPath, controller, operations,
                    GetBool(args, "dryRun", false));
        }

        private static object ApplyRuntimeOperations(string assetPath, AudioMixer mixer,
            IReadOnlyList<Dictionary<string, object>> operations, bool dryRun)
        {
            var prepared = new List<Dictionary<string, object>>();
            try
            {
                foreach (Dictionary<string, object> operation in operations)
                {
                    string action = GetString(operation, "action").ToLowerInvariant();
                    string parameter = GetString(operation, "parameter");
                    if (string.IsNullOrEmpty(parameter))
                        throw new ArgumentException(
                            "parameter is required for exposed-parameter operations.");
                    if (!mixer.GetFloat(parameter, out float before))
                        throw new ArgumentException(
                            $"AudioMixer exposed parameter '{parameter}' was not found.");
                    object requested = null;
                    if (action == "set-exposed-parameter")
                    {
                        if (!operation.TryGetValue("value", out object value))
                            throw new ArgumentException(
                                "value is required for set-exposed-parameter.");
                        requested = Convert.ToSingle(value);
                    }
                    prepared.Add(new Dictionary<string, object>
                    {
                        { "action", action },
                        { "parameter", parameter },
                        { "before", before },
                        { "requested", requested },
                        { "persistence", "editor-session-runtime-override" },
                    });
                }
            }
            catch (Exception exception)
            {
                return MCPResponse.Error(exception.GetBaseException().Message,
                    "audio_mixer_transaction_invalid");
            }

            if (dryRun)
            {
                return new Dictionary<string, object>
                {
                    { "success", true },
                    { "dryRun", true },
                    { "assetPath", assetPath },
                    { "operationCount", prepared.Count },
                    { "operations", prepared },
                };
            }

            var applied = new List<Dictionary<string, object>>();
            try
            {
                foreach (Dictionary<string, object> operation in prepared)
                {
                    string action = GetString(operation, "action");
                    string parameter = GetString(operation, "parameter");
                    bool succeeded = action == "set-exposed-parameter"
                        ? mixer.SetFloat(parameter,
                            Convert.ToSingle(operation["requested"]))
                        : mixer.ClearFloat(parameter);
                    if (!succeeded)
                        throw new InvalidOperationException(
                            $"AudioMixer rejected exposed parameter '{parameter}'.");
                    var result = new Dictionary<string, object>(operation)
                    {
                        ["success"] = true,
                    };
                    applied.Add(result);
                }
            }
            catch (Exception exception)
            {
                foreach (Dictionary<string, object> operation in applied)
                    mixer.SetFloat(GetString(operation, "parameter"),
                        Convert.ToSingle(operation["before"]));
                return MCPResponse.Error(exception.GetBaseException().Message,
                    "audio_mixer_runtime_update_failed");
            }

            return new Dictionary<string, object>
            {
                { "success", true },
                { "dryRun", false },
                { "assetPath", assetPath },
                { "operationCount", applied.Count },
                { "results", applied },
            };
        }

        private static object ApplyPersistentOperations(string assetPath,
            UnityEngine.Object controller,
            IReadOnlyList<Dictionary<string, object>> operations, bool dryRun)
        {
            var context = new MixerContext(assetPath, controller);
            var prepared = new List<PreparedOperation>();
            try
            {
                for (int index = 0; index < operations.Count; index++)
                    prepared.Add(PreparedOperation.Prepare(index, operations[index], context));
                ValidatePersistentSequence(prepared, context);
            }
            catch (Exception exception)
            {
                return MCPResponse.Error(exception.GetBaseException().Message,
                    "audio_mixer_transaction_invalid");
            }

            if (dryRun)
            {
                return new Dictionary<string, object>
                {
                    { "success", true },
                    { "dryRun", true },
                    { "assetPath", assetPath },
                    { "operationCount", prepared.Count },
                    { "operations", prepared.Select(item => item.Describe()).ToList() },
                };
            }

            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("VM Unity Automation Edit Audio Mixer");
            Undo.RegisterCompleteObjectUndo(
                AssetDatabase.LoadAllAssetsAtPath(assetPath),
                "VM Unity Automation Edit Audio Mixer");
            var results = new List<Dictionary<string, object>>();
            try
            {
                foreach (PreparedOperation operation in prepared)
                    results.Add(operation.Apply(context));
                EditorUtility.SetDirty(controller);
                AssetDatabase.SaveAssets();
                Undo.CollapseUndoOperations(undoGroup);
                return new Dictionary<string, object>
                {
                    { "success", true },
                    { "dryRun", false },
                    { "assetPath", assetPath },
                    { "operationCount", results.Count },
                    { "results", results },
                };
            }
            catch (Exception exception)
            {
                Undo.RevertAllDownToGroup(undoGroup);
                return MCPResponse.Error(exception.GetBaseException().Message,
                    "audio_mixer_transaction_failed");
            }
        }

        private static void ValidatePersistentSequence(
            IReadOnlyList<PreparedOperation> operations, MixerContext context)
        {
            var removed = new HashSet<UnityEngine.Object>();
            var exposed = new HashSet<string>(
                ReadExposedParameterInfos(context.Controller)
                    .Select(item => item.GuidText),
                StringComparer.OrdinalIgnoreCase);
            int remainingSnapshots = context.Snapshots.Count;
            UnityEngine.Object targetSnapshot = context.TargetSnapshot;

            foreach (PreparedOperation operation in operations)
            {
                if (operation.Target != null && removed.Contains(operation.Target))
                    throw new ArgumentException(
                        $"AudioMixer action '{operation.Action}' targets an object removed by an earlier operation.");
                if (operation.Secondary != null && removed.Contains(operation.Secondary))
                    throw new ArgumentException(
                        $"AudioMixer action '{operation.Action}' references an object removed by an earlier operation.");

                switch (operation.Action)
                {
                    case "create-snapshot":
                        remainingSnapshots++;
                        break;
                    case "remove-snapshot":
                        if (ReferenceEquals(operation.Target, targetSnapshot))
                            throw new ArgumentException(
                                "The target snapshot cannot be removed. Set another target snapshot first.");
                        remainingSnapshots--;
                        if (remainingSnapshots < 1)
                            throw new ArgumentException(
                                "An AudioMixer must retain at least one snapshot.");
                        removed.Add(operation.Target);
                        break;
                    case "set-target-snapshot":
                        targetSnapshot = operation.Target;
                        break;
                    case "remove-group":
                        MarkGroupClosureRemoved(operation.Target, removed);
                        break;
                    case "remove-effect":
                        removed.Add(operation.Target);
                        break;
                    case "expose-effect-parameter":
                    {
                        string guid = operation.RawGuid?.ToString() ?? "";
                        if (!exposed.Add(guid))
                            throw new ArgumentException(
                                "The same effect parameter cannot be exposed twice in one transaction.");
                        break;
                    }
                    case "unexpose-parameter":
                    {
                        string guid = operation.RawGuid?.ToString() ?? "";
                        if (!exposed.Remove(guid))
                            throw new ArgumentException(
                                "The same exposed parameter cannot be removed twice in one transaction.");
                        break;
                    }
                }
            }
        }

        private static void MarkGroupClosureRemoved(UnityEngine.Object group,
            ISet<UnityEngine.Object> removed)
        {
            var pending = new Stack<UnityEngine.Object>();
            pending.Push(group);
            while (pending.Count > 0)
            {
                UnityEngine.Object current = pending.Pop();
                if (current == null || !removed.Add(current))
                    continue;
                foreach (UnityEngine.Object effect in Enumerate(
                             GetProperty(current.GetType(), current, "effects"))
                             .OfType<UnityEngine.Object>())
                    removed.Add(effect);
                foreach (UnityEngine.Object child in Enumerate(
                             GetProperty(current.GetType(), current, "children"))
                             .OfType<UnityEngine.Object>())
                    pending.Push(child);
            }
        }

        private sealed class MixerContext
        {
            internal MixerContext(string assetPath, UnityEngine.Object controller)
            {
                AssetPath = assetPath;
                Controller = controller;
                Refresh();
            }

            internal string AssetPath { get; }
            internal UnityEngine.Object Controller { get; }
            internal List<UnityEngine.Object> Groups { get; private set; }
            internal List<UnityEngine.Object> Snapshots { get; private set; }
            internal List<UnityEngine.Object> Effects { get; private set; }

            internal void Refresh()
            {
                UnityEngine.Object[] assets = AssetDatabase.LoadAllAssetsAtPath(AssetPath);
                Groups = OfKind(assets, "AudioMixerGroupController");
                Snapshots = OfKind(assets, "AudioMixerSnapshotController");
                Effects = OfKind(assets, "AudioMixerEffectController");
            }

            internal UnityEngine.Object Find(string localId,
                IEnumerable<UnityEngine.Object> candidates, string label)
            {
                if (string.IsNullOrEmpty(localId))
                    throw new ArgumentException($"{label}LocalId is required.");
                UnityEngine.Object result = candidates.FirstOrDefault(candidate =>
                    string.Equals(LocalId(candidate), localId, StringComparison.Ordinal));
                if (result == null)
                    throw new ArgumentException($"{label} '{localId}' was not found.");
                return result;
            }

            internal UnityEngine.Object MasterGroup =>
                GetProperty(Controller.GetType(), Controller, "masterGroup")
                as UnityEngine.Object;

            internal UnityEngine.Object TargetSnapshot =>
                GetProperty(Controller.GetType(), Controller, "TargetSnapshot")
                as UnityEngine.Object;

            internal UnityEngine.Object ParentForEffect(UnityEngine.Object effect)
            {
                return Groups.FirstOrDefault(group =>
                    Enumerate(GetProperty(group.GetType(), group, "effects"))
                        .OfType<UnityEngine.Object>().Contains(effect));
            }

            internal ExposedParameterInfo FindExposed(string guid, string name)
            {
                return ReadExposedParameterInfos(Controller).FirstOrDefault(item =>
                    !string.IsNullOrEmpty(guid)
                        ? string.Equals(item.GuidText, guid,
                            StringComparison.OrdinalIgnoreCase)
                        : string.Equals(item.Name, name, StringComparison.Ordinal));
            }
        }

        private sealed class PreparedOperation
        {
            private readonly int index;
            private readonly string action;
            private readonly Dictionary<string, object> values;
            private readonly UnityEngine.Object target;
            private readonly UnityEngine.Object secondary;
            private readonly object rawGuid;
            private readonly object before;

            internal string Action => action;
            internal UnityEngine.Object Target => target;
            internal UnityEngine.Object Secondary => secondary;
            internal object RawGuid => rawGuid;

            private PreparedOperation(int index, string action,
                Dictionary<string, object> values, UnityEngine.Object target,
                UnityEngine.Object secondary, object rawGuid, object before)
            {
                this.index = index;
                this.action = action;
                this.values = values;
                this.target = target;
                this.secondary = secondary;
                this.rawGuid = rawGuid;
                this.before = before;
            }

            internal static PreparedOperation Prepare(int index,
                Dictionary<string, object> values, MixerContext context)
            {
                string action = GetString(values, "action").ToLowerInvariant();
                switch (action)
                {
                    case "rename":
                    {
                        UnityEngine.Object target = context.Find(
                            GetString(values, "targetLocalId"),
                            context.Groups.Concat(context.Snapshots).Concat(context.Effects),
                            "target");
                        RequireString(values, "name");
                        return New(index, action, values, target, before: target.name);
                    }
                    case "create-group":
                    {
                        RequireString(values, "name");
                        UnityEngine.Object parent = string.IsNullOrEmpty(
                            GetString(values, "parentGroupLocalId"))
                            ? context.MasterGroup
                            : context.Find(GetString(values, "parentGroupLocalId"),
                                context.Groups, "parentGroup");
                        if (parent == null)
                            throw new ArgumentException("AudioMixer master group was not found.");
                        return New(index, action, values, parent);
                    }
                    case "remove-group":
                    {
                        UnityEngine.Object group = context.Find(
                            GetString(values, "groupLocalId"), context.Groups, "group");
                        if (group == context.MasterGroup)
                            throw new ArgumentException("The AudioMixer master group cannot be removed.");
                        return New(index, action, values, group);
                    }
                    case "set-group-state":
                    {
                        UnityEngine.Object group = context.Find(
                            GetString(values, "groupLocalId"), context.Groups, "group");
                        if (!values.ContainsKey("mute") && !values.ContainsKey("solo") &&
                            !values.ContainsKey("bypassEffects"))
                            throw new ArgumentException(
                                "set-group-state requires mute, solo, or bypassEffects.");
                        foreach (string key in new[] { "mute", "solo", "bypassEffects" })
                            if (values.TryGetValue(key, out object value))
                                Convert.ToBoolean(value);
                        return New(index, action, values, group,
                            before: GroupState(group));
                    }
                    case "create-snapshot":
                        RequireString(values, "name");
                        return New(index, action, values, context.TargetSnapshot);
                    case "remove-snapshot":
                    {
                        if (context.Snapshots.Count <= 1)
                            throw new ArgumentException(
                                "An AudioMixer must retain at least one snapshot.");
                        UnityEngine.Object snapshot = context.Find(
                            GetString(values, "snapshotLocalId"), context.Snapshots,
                            "snapshot");
                        return New(index, action, values, snapshot);
                    }
                    case "set-target-snapshot":
                    {
                        UnityEngine.Object snapshot = context.Find(
                            GetString(values, "snapshotLocalId"), context.Snapshots,
                            "snapshot");
                        return New(index, action, values, snapshot,
                            before: context.TargetSnapshot == null
                                ? ""
                                : LocalId(context.TargetSnapshot));
                    }
                    case "add-effect":
                    {
                        UnityEngine.Object group = context.Find(
                            GetString(values, "groupLocalId"), context.Groups, "group");
                        RequireString(values, "effectName");
                        int indexValue = GetInt(values, "index",
                            Enumerate(GetProperty(group.GetType(), group, "effects")).Count());
                        int effectCount =
                            Enumerate(GetProperty(group.GetType(), group, "effects")).Count();
                        if (indexValue < 0 || indexValue > effectCount)
                            throw new ArgumentException(
                                $"Effect index {indexValue} is outside 0..{effectCount}.");
                        return New(index, action, values, group);
                    }
                    case "remove-effect":
                    case "set-effect-bypass":
                    case "expose-effect-parameter":
                    case "set-snapshot-parameter":
                    {
                        UnityEngine.Object effect = context.Find(
                            GetString(values, "effectLocalId"), context.Effects, "effect");
                        if (action == "set-effect-bypass")
                        {
                            if (!values.TryGetValue("bypass", out object bypass))
                                throw new ArgumentException("bypass is required.");
                            Convert.ToBoolean(bypass);
                            return New(index, action, values, effect,
                                before: ReadEffectBypass(effect));
                        }
                        if (action == "remove-effect")
                        {
                            UnityEngine.Object parent = context.ParentForEffect(effect);
                            if (parent == null)
                                throw new ArgumentException("Effect parent group was not found.");
                            return New(index, action, values, effect, parent);
                        }

                        string parameter = RequireString(values, "parameter");
                        bool mixLevel = IsMixLevelParameter(effect, parameter);
                        object guid = ResolveEffectParameterGuid(effect, parameter,
                            mixLevel);
                        values["_mixLevel"] = mixLevel;
                        if (action == "expose-effect-parameter")
                        {
                            if (ContainsExposedParameter(context.Controller, guid))
                                throw new ArgumentException(
                                    $"Effect parameter '{parameter}' is already exposed.");
                            string exposedName = GetString(values, "exposedName");
                            if (!string.IsNullOrEmpty(exposedName) &&
                                context.FindExposed("", exposedName) != null)
                                throw new ArgumentException(
                                    $"Exposed parameter name '{exposedName}' already exists.");
                            return New(index, action, values, effect, rawGuid: guid);
                        }

                        UnityEngine.Object snapshot = string.IsNullOrEmpty(
                            GetString(values, "snapshotLocalId"))
                            ? context.TargetSnapshot
                            : context.Find(GetString(values, "snapshotLocalId"),
                                context.Snapshots, "snapshot");
                        if (snapshot == null)
                            throw new ArgumentException("Target snapshot was not found.");
                        if (!values.TryGetValue("value", out object requested))
                            throw new ArgumentException("value is required.");
                        float numeric = Convert.ToSingle(requested);
                        object current = mixLevel
                            ? InvokeEffectParameter(effect, "GetValueForMixLevel",
                                context.Controller, snapshot)
                            : InvokeEffectParameter(effect, "GetValueForParameter",
                                context.Controller, snapshot, parameter);
                        values["value"] = numeric;
                        return New(index, action, values, effect, snapshot, guid, current);
                    }
                    case "unexpose-parameter":
                    {
                        string guidText = GetString(values, "guid");
                        string name = GetString(values, "exposedName");
                        if (string.IsNullOrEmpty(guidText) && string.IsNullOrEmpty(name))
                            throw new ArgumentException(
                                "guid or exposedName is required.");
                        ExposedParameterInfo exposed = context.FindExposed(guidText, name);
                        if (exposed == null)
                            throw new ArgumentException("Exposed parameter was not found.");
                        return New(index, action, values, rawGuid: exposed.RawGuid,
                            before: exposed.ToDictionary());
                    }
                    case "set-property":
                    {
                        UnityEngine.Object target = context.Find(
                            GetString(values, "targetLocalId"),
                            context.Groups.Concat(context.Snapshots).Concat(context.Effects),
                            "target");
                        string propertyPath = RequireString(values, "propertyPath");
                        if (!values.TryGetValue("value", out object requested))
                            throw new ArgumentException("value is required.");
                        var serialized = new SerializedObject(target);
                        SerializedProperty property = serialized.FindProperty(propertyPath);
                        if (property == null)
                            throw new ArgumentException(
                                $"Serialized property '{propertyPath}' was not found.");
                        object original =
                            MCPComponentCommands.GetSerializedValue(property, 2, 32);
                        MCPComponentCommands.SetSerializedValue(property, requested);
                        return New(index, action, values, target, before: original);
                    }
                    default:
                        throw new ArgumentException($"Unsupported AudioMixer action '{action}'.");
                }
            }

            private static PreparedOperation New(int index, string action,
                Dictionary<string, object> values, UnityEngine.Object target = null,
                UnityEngine.Object secondary = null, object rawGuid = null,
                object before = null)
            {
                return new PreparedOperation(index, action,
                    new Dictionary<string, object>(values), target, secondary, rawGuid,
                    before);
            }

            internal Dictionary<string, object> Describe()
            {
                var result = new Dictionary<string, object>
                {
                    { "index", index },
                    { "action", action },
                };
                if (target != null)
                {
                    result["targetLocalId"] = LocalId(target);
                    result["targetName"] = target.name ?? "";
                    result["targetType"] = target.GetType().FullName;
                }
                if (secondary != null)
                    result["secondaryLocalId"] = LocalId(secondary);
                if (before != null)
                    result["before"] = MCPAssetGraphUtility.SanitizeValue(before);
                foreach (string key in new[]
                         {
                             "name", "parameter", "exposedName", "effectName", "value",
                             "mute", "solo", "bypassEffects", "bypass", "propertyPath",
                         })
                {
                    if (values.TryGetValue(key, out object value))
                        result[key] = value;
                }
                return result;
            }

            internal Dictionary<string, object> Apply(MixerContext context)
            {
                Dictionary<string, object> result = Describe();
                switch (action)
                {
                    case "rename":
                        Undo.RecordObject(target, "VM Unity Automation Edit Audio Mixer");
                        target.name = GetString(values, "name");
                        EditorUtility.SetDirty(target);
                        result["after"] = target.name;
                        break;
                    case "create-group":
                        result["created"] = CreateGroup(context, target,
                            GetString(values, "name"));
                        break;
                    case "remove-group":
                        InvokeRequired(context.Controller, "DeleteGroups",
                            TypedArray(target.GetType(), target));
                        result["removed"] = true;
                        break;
                    case "set-group-state":
                        SetOptionalBool(target, values, "mute");
                        SetOptionalBool(target, values, "solo");
                        SetOptionalBool(target, values, "bypassEffects");
                        result["after"] = GroupState(target);
                        break;
                    case "create-snapshot":
                        result["created"] = CreateSnapshot(context,
                            GetString(values, "name"));
                        break;
                    case "remove-snapshot":
                        InvokeRequired(context.Controller, "RemoveSnapshot", target);
                        result["removed"] = true;
                        break;
                    case "set-target-snapshot":
                        SetProperty(context.Controller.GetType(), context.Controller,
                            "TargetSnapshot", target);
                        result["after"] = LocalId(target);
                        break;
                    case "add-effect":
                        result["created"] = AddEffect(target,
                            GetString(values, "effectName"),
                            GetInt(values, "index",
                                Enumerate(GetProperty(target.GetType(), target, "effects"))
                                    .Count()));
                        break;
                    case "remove-effect":
                        InvokeRequired(context.Controller, "RemoveEffect", target, secondary);
                        result["removed"] = true;
                        break;
                    case "set-effect-bypass":
                        SetEffectBypass(target, Convert.ToBoolean(values["bypass"]));
                        result["after"] = ReadEffectBypass(target);
                        break;
                    case "expose-effect-parameter":
                        AddExposedParameter(context.Controller, rawGuid,
                            GetString(values, "exposedName"));
                        result["guid"] = rawGuid?.ToString() ?? "";
                        result["exposed"] = true;
                        break;
                    case "unexpose-parameter":
                        InvokeRequired(context.Controller, "RemoveExposedParameter", rawGuid);
                        result["removed"] = true;
                        break;
                    case "set-snapshot-parameter":
                        if (GetBool(values, "_mixLevel", false))
                        {
                            InvokeEffectParameter(target, "SetValueForMixLevel",
                                context.Controller, secondary,
                                Convert.ToSingle(values["value"]));
                        }
                        else
                        {
                            InvokeEffectParameter(target, "SetValueForParameter",
                                context.Controller, secondary,
                                GetString(values, "parameter"),
                                Convert.ToSingle(values["value"]));
                        }
                        result["after"] = values["value"];
                        break;
                    case "set-property":
                    {
                        Undo.RecordObject(target, "VM Unity Automation Edit Audio Mixer");
                        var serialized = new SerializedObject(target);
                        serialized.Update();
                        SerializedProperty property = serialized.FindProperty(
                            GetString(values, "propertyPath"));
                        MCPComponentCommands.SetSerializedValue(property, values["value"]);
                        serialized.ApplyModifiedProperties();
                        EditorUtility.SetDirty(target);
                        result["after"] = MCPComponentCommands.GetSerializedValue(
                            new SerializedObject(target).FindProperty(
                                GetString(values, "propertyPath")), 2, 32);
                        break;
                    }
                }
                EditorUtility.SetDirty(context.Controller);
                return result;
            }
        }

        private sealed class ExposedParameterInfo
        {
            internal object RawGuid;
            internal string GuidText;
            internal string Name;
            internal string Path;

            internal Dictionary<string, object> ToDictionary()
            {
                return new Dictionary<string, object>
                {
                    { "guid", GuidText ?? "" },
                    { "name", Name ?? "" },
                    { "path", Path ?? "" },
                };
            }
        }

        private static Dictionary<string, object> CreateGroup(MixerContext context,
            UnityEngine.Object parent, string name)
        {
            object created = InvokeRequired(context.Controller, "CreateNewGroup", name, true);
            if (!(created is UnityEngine.Object group))
                throw new InvalidOperationException("AudioMixer did not create a group.");
            Undo.RegisterCreatedObjectUndo(group, "VM Unity Automation Create Audio Mixer Group");

            Type groupType = group.GetType();
            List<UnityEngine.Object> children =
                Enumerate(GetProperty(parent.GetType(), parent, "children"))
                    .OfType<UnityEngine.Object>().ToList();
            children.Add(group);
            SetProperty(parent.GetType(), parent, "children",
                TypedArray(groupType, children.Cast<object>().ToArray()));
            EditorUtility.SetDirty(parent);
            EditorUtility.SetDirty(group);
            return ObjectSummary(group);
        }

        private static Dictionary<string, object> CreateSnapshot(MixerContext context,
            string name)
        {
            var before = new HashSet<UnityEngine.Object>(context.Snapshots);
            object returned = InvokeRequired(context.Controller,
                "CloneNewSnapshotFromTarget", true);
            context.Refresh();
            UnityEngine.Object snapshot = returned as UnityEngine.Object ??
                                          context.Snapshots.FirstOrDefault(item =>
                                              !before.Contains(item));
            if (snapshot == null)
                throw new InvalidOperationException("AudioMixer did not create a snapshot.");
            Undo.RegisterCreatedObjectUndo(snapshot,
                "VM Unity Automation Create Audio Mixer Snapshot");
            snapshot.name = name;
            EditorUtility.SetDirty(snapshot);
            return ObjectSummary(snapshot);
        }

        private static Dictionary<string, object> AddEffect(UnityEngine.Object group,
            string effectName, int index)
        {
            Type effectType = MCPAssetGraphUtility.FindType(
                "UnityEditor.Audio.AudioMixerEffectController");
            ConstructorInfo constructor = effectType?.GetConstructor(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null, new[] { typeof(string) }, null);
            if (constructor == null)
                throw new MissingMethodException(
                    "AudioMixerEffectController(string) is unavailable.");
            object created = constructor.Invoke(new object[] { effectName });
            InvokeRequired(group, "InsertEffect", created, index);
            if (!(created is UnityEngine.Object effect))
                throw new InvalidOperationException("AudioMixer did not create an effect.");
            Undo.RegisterCreatedObjectUndo(effect, "VM Unity Automation Create Audio Mixer Effect");
            EditorUtility.SetDirty(group);
            EditorUtility.SetDirty(effect);
            return EffectSummary(effect, new[] { group }, 50);
        }

        private static object ResolveEffectParameterGuid(UnityEngine.Object effect,
            string parameter, bool mixLevel)
        {
            object guid = mixLevel
                ? InvokeRequired(effect, "GetGUIDForMixLevel")
                : InvokeRequired(effect, "GetGUIDForParameter", parameter);
            object contains = InvokeRequired(effect, "ContainsParameterGUID", guid);
            if (!(contains is bool found) || !found)
                throw new ArgumentException(
                    $"Effect '{effect.name}' has no parameter '{parameter}'.");
            return guid;
        }

        private static bool IsMixLevelParameter(UnityEngine.Object effect,
            string parameter)
        {
            if (string.Equals(parameter, "MixLevel",
                    StringComparison.OrdinalIgnoreCase))
                return true;
            return string.Equals(parameter, "Volume",
                       StringComparison.OrdinalIgnoreCase) &&
                   InvokeOptional(effect, "IsAttenuation") is bool attenuation &&
                   attenuation;
        }

        private static object InvokeEffectParameter(UnityEngine.Object effect,
            string methodName, params object[] args)
        {
            return InvokeRequired(effect, methodName, args);
        }

        private static bool ContainsExposedParameter(UnityEngine.Object controller,
            object guid)
        {
            return InvokeRequired(controller, "ContainsExposedParameter", guid)
                   is bool value && value;
        }

        private static void AddExposedParameter(UnityEngine.Object controller,
            object guid, string exposedName)
        {
            MethodInfo add = FindCompatibleMethod(controller.GetType(),
                "AddExposedParameter", new[] { guid });
            if (add == null)
                throw new MissingMethodException(controller.GetType().FullName,
                    "AddExposedParameter");
            Type pathType = add.GetParameters()[0].ParameterType;
            object path = Activator.CreateInstance(pathType, true);
            FieldInfo parameterField = pathType.GetField("parameter",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (parameterField == null)
                throw new MissingFieldException(pathType.FullName, "parameter");
            parameterField.SetValue(path, guid);
            add.Invoke(controller, new[] { path });
            if (!string.IsNullOrEmpty(exposedName))
                RenameExposedParameter(controller, guid, exposedName);
        }

        private static void RenameExposedParameter(UnityEngine.Object controller,
            object guid, string name)
        {
            PropertyInfo property = controller.GetType().GetProperty("exposedParameters",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property == null || !property.CanRead || !property.CanWrite)
                throw new MissingMemberException(controller.GetType().FullName,
                    "exposedParameters");
            Array values = property.GetValue(controller) as Array;
            if (values == null)
                throw new InvalidOperationException("AudioMixer exposed parameters are unavailable.");
            bool found = false;
            for (int index = 0; index < values.Length; index++)
            {
                object item = values.GetValue(index);
                object itemGuid = GetMember(item, "guid");
                if (!string.Equals(itemGuid?.ToString(), guid?.ToString(),
                        StringComparison.OrdinalIgnoreCase))
                    continue;
                SetMember(item, "name", name);
                values.SetValue(item, index);
                found = true;
                break;
            }
            if (!found)
                throw new InvalidOperationException(
                    "The newly exposed AudioMixer parameter was not found.");
            property.SetValue(controller, values);
            InvokeOptional(controller, "OnChangedExposedParameter");
        }

        private static List<Dictionary<string, object>> ReadExposedParameters(
            UnityEngine.Object controller)
        {
            return ReadExposedParameterInfos(controller)
                .Select(item => item.ToDictionary()).ToList();
        }

        private static List<ExposedParameterInfo> ReadExposedParameterInfos(
            UnityEngine.Object controller)
        {
            object raw = GetProperty(controller.GetType(), controller,
                "exposedParameters");
            var result = new List<ExposedParameterInfo>();
            foreach (object item in Enumerate(raw))
            {
                object guid = GetMember(item, "guid");
                string path = "";
                try
                {
                    path = InvokeRequired(controller, "ResolveExposedParameterPath",
                        guid, false)?.ToString() ?? "";
                }
                catch
                {
                    // The public name and GUID remain useful on versions without this helper.
                }
                result.Add(new ExposedParameterInfo
                {
                    RawGuid = guid,
                    GuidText = guid?.ToString() ?? "",
                    Name = GetMember(item, "name")?.ToString() ?? "",
                    Path = path,
                });
            }
            return result;
        }

        private static Dictionary<string, object> ObjectSummary(UnityEngine.Object value)
        {
            return new Dictionary<string, object>
            {
                { "localId", LocalId(value) },
                { "name", value.name ?? "" },
                { "type", value.GetType().FullName },
            };
        }

        private static Dictionary<string, object> GroupSummary(UnityEngine.Object value,
            int maxChildren, int maxEffects)
        {
            Dictionary<string, object> result = ObjectSummary(value);
            List<UnityEngine.Object> children =
                Enumerate(GetProperty(value.GetType(), value, "children"))
                    .OfType<UnityEngine.Object>().ToList();
            result["childCount"] = children.Count;
            result["children"] = children.Take(maxChildren)
                .Select(child => new Dictionary<string, object>
                {
                    { "localId", LocalId(child) },
                    { "name", child.name ?? "" },
                }).ToList();
            result["childrenTruncated"] = children.Count > maxChildren;
            List<UnityEngine.Object> groupEffects =
                Enumerate(GetProperty(value.GetType(), value, "effects"))
                    .OfType<UnityEngine.Object>().ToList();
            result["effectCount"] = groupEffects.Count;
            result["effects"] = groupEffects.Take(maxEffects)
                .Select(effect => new Dictionary<string, object>
                {
                    { "localId", LocalId(effect) },
                    { "name", effect.name ?? "" },
                    { "effectName", ReadEffectName(effect) },
                }).ToList();
            result["effectsTruncated"] = groupEffects.Count > maxEffects;
            result["state"] = GroupState(value);
            return result;
        }

        private static Dictionary<string, object> SnapshotSummary(UnityEngine.Object value,
            UnityEngine.Object controller)
        {
            Dictionary<string, object> result = ObjectSummary(value);
            result["target"] = ReferenceEquals(
                GetProperty(controller.GetType(), controller, "TargetSnapshot"), value);
            return result;
        }

        private static Dictionary<string, object> EffectSummary(UnityEngine.Object value,
            IEnumerable<UnityEngine.Object> groups, int maxParameters)
        {
            Dictionary<string, object> result = ObjectSummary(value);
            result["effectName"] = ReadEffectName(value);
            result["bypass"] = ReadEffectBypass(value);
            List<Dictionary<string, object>> parameters = ReadEffectParameters(value);
            result["parameterCount"] = parameters.Count;
            result["parameters"] = parameters.Take(maxParameters).ToList();
            result["parametersTruncated"] = parameters.Count > maxParameters;
            UnityEngine.Object parent = groups.FirstOrDefault(group =>
                Enumerate(GetProperty(group.GetType(), group, "effects"))
                    .OfType<UnityEngine.Object>().Contains(value));
            if (parent != null)
            {
                result["groupLocalId"] = LocalId(parent);
                result["group"] = parent.name ?? "";
            }
            return result;
        }

        private static List<Dictionary<string, object>> ReadEffectParameters(
            UnityEngine.Object effect)
        {
            string effectName = ReadEffectName(effect);
            var result = new List<Dictionary<string, object>>();
            Type definitions = MCPAssetGraphUtility.FindType(
                "UnityEditor.Audio.MixerEffectDefinitions");
            MethodInfo getParameters = definitions?.GetMethod("GetEffectParameters",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                null, new[] { typeof(string) }, null);
            foreach (object parameter in Enumerate(
                         getParameters?.Invoke(null, new object[] { effectName })))
            {
                result.Add(new Dictionary<string, object>
                {
                    { "name", GetMember(parameter, "name")?.ToString() ?? "" },
                    { "description",
                        GetMember(parameter, "description")?.ToString() ?? "" },
                    { "units", GetMember(parameter, "units")?.ToString() ?? "" },
                    { "minimum", GetMember(parameter, "minRange") ?? 0f },
                    { "maximum", GetMember(parameter, "maxRange") ?? 0f },
                    { "defaultValue", GetMember(parameter, "defaultValue") ?? 0f },
                });
            }
            if (InvokeOptional(effect, "IsAttenuation") is bool attenuation &&
                attenuation)
            {
                result.Insert(0, new Dictionary<string, object>
                {
                    { "name", "MixLevel" },
                    { "alias", "Volume" },
                    { "description", "Attenuation mix level used by snapshots and exposed parameters." },
                    { "units", "dB" },
                });
            }
            return result;
        }

        private static Dictionary<string, object> GroupState(UnityEngine.Object group)
        {
            return new Dictionary<string, object>
            {
                { "mute", GetProperty(group.GetType(), group, "mute") ?? false },
                { "solo", GetProperty(group.GetType(), group, "solo") ?? false },
                { "bypassEffects",
                    GetProperty(group.GetType(), group, "bypassEffects") ?? false },
            };
        }

        private static string ReadEffectName(UnityEngine.Object effect)
        {
            return GetProperty(effect.GetType(), effect, "effectName")?.ToString() ??
                   new SerializedObject(effect).FindProperty("m_EffectName")?.stringValue ??
                   effect.name ?? "";
        }

        private static bool ReadEffectBypass(UnityEngine.Object effect)
        {
            SerializedProperty property =
                new SerializedObject(effect).FindProperty("m_Bypass");
            return property != null && property.boolValue;
        }

        private static void SetEffectBypass(UnityEngine.Object effect, bool value)
        {
            Undo.RecordObject(effect, "VM Unity Automation Edit Audio Mixer");
            var serialized = new SerializedObject(effect);
            SerializedProperty property = serialized.FindProperty("m_Bypass");
            if (property == null)
                throw new MissingMemberException(effect.GetType().FullName, "m_Bypass");
            property.boolValue = value;
            serialized.ApplyModifiedProperties();
            EditorUtility.SetDirty(effect);
        }

        private static void SetOptionalBool(UnityEngine.Object target,
            Dictionary<string, object> values, string key)
        {
            if (values.TryGetValue(key, out object value))
                SetProperty(target.GetType(), target, key, Convert.ToBoolean(value));
        }

        private static bool TryLoad(Dictionary<string, object> args,
            out string assetPath, out AudioMixer mixer,
            out UnityEngine.Object controller, out object error)
        {
            assetPath = GetString(args, "assetPath");
            mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(assetPath);
            controller = null;
            if (mixer == null)
            {
                error = MCPResponse.Error(
                    $"AudioMixer asset '{assetPath}' was not found.",
                    "audio_mixer_not_found");
                return false;
            }
            controller = AssetDatabase.LoadMainAssetAtPath(assetPath);
            if (controller == null ||
                !controller.GetType().Name.Contains("AudioMixerController"))
            {
                error = MCPResponse.Error(
                    $"AudioMixer controller for '{assetPath}' was not available.",
                    "audio_mixer_controller_unavailable");
                return false;
            }
            error = null;
            return true;
        }

        private static List<UnityEngine.Object> OfKind(
            IEnumerable<UnityEngine.Object> values, string typeName)
        {
            return values.Where(item => item != null &&
                                         item.GetType().Name == typeName).ToList();
        }

        private static string LocalId(UnityEngine.Object value)
        {
            return value != null &&
                   AssetDatabase.TryGetGUIDAndLocalFileIdentifier(value, out string _,
                       out long id)
                ? id.ToString()
                : "";
        }

        private static Array TypedArray(Type elementType, params object[] values)
        {
            Array array = Array.CreateInstance(elementType, values?.Length ?? 0);
            for (int index = 0; index < array.Length; index++)
                array.SetValue(values[index], index);
            return array;
        }

        private static object InvokeRequired(object target, string name,
            params object[] values)
        {
            if (target == null)
                throw new ArgumentNullException(nameof(target));
            MethodInfo method = FindCompatibleMethod(target.GetType(), name, values);
            if (method == null)
                throw new MissingMethodException(target.GetType().FullName, name);
            return method.Invoke(target, values);
        }

        private static object InvokeOptional(object target, string name,
            params object[] values)
        {
            MethodInfo method = target == null
                ? null
                : FindCompatibleMethod(target.GetType(), name, values);
            return method?.Invoke(target, values);
        }

        private static MethodInfo FindCompatibleMethod(Type type, string name,
            IReadOnlyList<object> values)
        {
            return type.GetMethods(BindingFlags.Instance | BindingFlags.Public |
                                   BindingFlags.NonPublic)
                .Where(method => method.Name == name)
                .FirstOrDefault(method =>
                {
                    ParameterInfo[] parameters = method.GetParameters();
                    if (parameters.Length != (values?.Count ?? 0))
                        return false;
                    for (int index = 0; index < parameters.Length; index++)
                    {
                        object value = values[index];
                        if (value == null)
                        {
                            if (parameters[index].ParameterType.IsValueType)
                                return false;
                            continue;
                        }
                        if (!parameters[index].ParameterType.IsInstanceOfType(value))
                            return false;
                    }
                    return true;
                });
        }

        private static object GetProperty(Type type, object target, string name)
        {
            PropertyInfo property = FindProperty(type, name);
            return property?.GetValue(target);
        }

        private static void SetProperty(Type type, object target, string name,
            object value)
        {
            PropertyInfo property = FindProperty(type, name);
            if (property == null || !property.CanWrite)
                throw new MissingMemberException(type.FullName, name);
            property.SetValue(target, value);
        }

        private static PropertyInfo FindProperty(Type type, string name)
        {
            while (type != null)
            {
                PropertyInfo property = type.GetProperties(
                        BindingFlags.Instance | BindingFlags.Public |
                        BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                    .FirstOrDefault(item => item.Name == name &&
                                            item.GetIndexParameters().Length == 0);
                if (property != null)
                    return property;
                type = type.BaseType;
            }
            return null;
        }

        private static object GetMember(object target, string name)
        {
            if (target == null)
                return null;
            PropertyInfo property = FindProperty(target.GetType(), name);
            if (property != null)
                return property.GetValue(target);
            Type type = target.GetType();
            while (type != null)
            {
                FieldInfo field = type.GetField(name,
                    BindingFlags.Instance | BindingFlags.Public |
                    BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field != null)
                    return field.GetValue(target);
                type = type.BaseType;
            }
            return null;
        }

        private static void SetMember(object target, string name, object value)
        {
            PropertyInfo property = FindProperty(target.GetType(), name);
            if (property != null && property.CanWrite)
            {
                property.SetValue(target, value);
                return;
            }
            Type type = target.GetType();
            while (type != null)
            {
                FieldInfo field = type.GetField(name,
                    BindingFlags.Instance | BindingFlags.Public |
                    BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field != null)
                {
                    field.SetValue(target, value);
                    return;
                }
                type = type.BaseType;
            }
            throw new MissingMemberException(target.GetType().FullName, name);
        }

        private static IEnumerable<object> Enumerate(object value)
        {
            return value is IEnumerable enumerable
                ? enumerable.Cast<object>()
                : Enumerable.Empty<object>();
        }

        private static void ValidateOperationKeys(Dictionary<string, object> operation,
            string action)
        {
            string[] allowed;
            switch (action)
            {
                case "set-exposed-parameter":
                    allowed = new[] { "action", "parameter", "value" };
                    break;
                case "clear-exposed-parameter":
                    allowed = new[] { "action", "parameter" };
                    break;
                case "rename":
                    allowed = new[] { "action", "targetLocalId", "name" };
                    break;
                case "create-group":
                    allowed = new[]
                    {
                        "action", "name", "parentGroupLocalId",
                    };
                    break;
                case "remove-group":
                case "set-group-state":
                    allowed = action == "remove-group"
                        ? new[] { "action", "groupLocalId" }
                        : new[]
                        {
                            "action", "groupLocalId", "mute", "solo",
                            "bypassEffects",
                        };
                    break;
                case "create-snapshot":
                    allowed = new[] { "action", "name" };
                    break;
                case "remove-snapshot":
                case "set-target-snapshot":
                    allowed = new[] { "action", "snapshotLocalId" };
                    break;
                case "add-effect":
                    allowed = new[]
                    {
                        "action", "groupLocalId", "effectName", "index",
                    };
                    break;
                case "remove-effect":
                    allowed = new[] { "action", "effectLocalId" };
                    break;
                case "set-effect-bypass":
                    allowed = new[]
                    {
                        "action", "effectLocalId", "bypass",
                    };
                    break;
                case "expose-effect-parameter":
                    allowed = new[]
                    {
                        "action", "effectLocalId", "parameter", "exposedName",
                    };
                    break;
                case "unexpose-parameter":
                    allowed = new[] { "action", "guid", "exposedName" };
                    break;
                case "set-snapshot-parameter":
                    allowed = new[]
                    {
                        "action", "effectLocalId", "parameter",
                        "snapshotLocalId", "value",
                    };
                    break;
                case "set-property":
                    allowed = new[]
                    {
                        "action", "targetLocalId", "propertyPath", "value",
                    };
                    break;
                default:
                    allowed = new[] { "action" };
                    break;
            }

            var allowedSet = new HashSet<string>(allowed, StringComparer.Ordinal);
            string unknown = operation.Keys.FirstOrDefault(key =>
                !allowedSet.Contains(key));
            if (!string.IsNullOrEmpty(unknown))
                throw new ArgumentException(
                    $"Unsupported field '{unknown}' for AudioMixer action '{action}'. " +
                    $"Allowed fields: {string.Join(", ", allowed.OrderBy(item => item))}.");
        }

        private static bool TryValidateTopLevelKeys(Dictionary<string, object> values,
            IEnumerable<string> allowed, out object error)
        {
            var allowedSet = new HashSet<string>(allowed, StringComparer.Ordinal);
            string unknown = values?.Keys.FirstOrDefault(key =>
                !allowedSet.Contains(key));
            if (string.IsNullOrEmpty(unknown))
            {
                error = null;
                return true;
            }

            error = MCPResponse.Error(
                $"Unsupported argument '{unknown}'. Allowed arguments: " +
                string.Join(", ", allowedSet.Where(key => key != "_agentId")
                    .OrderBy(key => key)) + ".",
                "invalid_arguments");
            return false;
        }

        private static int BoundedInt(Dictionary<string, object> values, string key,
            int defaultValue, int maximum)
        {
            return Math.Max(1, Math.Min(maximum, GetInt(values, key, defaultValue)));
        }

        private static string RequireString(Dictionary<string, object> values,
            string key)
        {
            string value = GetString(values, key);
            if (string.IsNullOrEmpty(value))
                throw new ArgumentException($"{key} is required.");
            return value;
        }

        private static string GetString(Dictionary<string, object> values, string key)
        {
            return values != null && values.TryGetValue(key, out object value) &&
                   value != null
                ? value.ToString()
                : "";
        }

        private static int GetInt(Dictionary<string, object> values, string key,
            int defaultValue)
        {
            return values != null && values.TryGetValue(key, out object value) &&
                   value != null
                ? Convert.ToInt32(value)
                : defaultValue;
        }

        private static bool GetBool(Dictionary<string, object> values, string key,
            bool defaultValue)
        {
            return values != null && values.TryGetValue(key, out object value) &&
                   value != null
                ? Convert.ToBoolean(value)
                : defaultValue;
        }

        private static List<object> GetList(Dictionary<string, object> values,
            string key)
        {
            return values != null && values.TryGetValue(key, out object value)
                ? value as List<object>
                : null;
        }
    }
}
