using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace VMUnityAutomation.Editor
{
    internal static class VmAutomationVFXComponentCommands
    {
        private sealed class ExposedPropertyCache
        {
            internal sealed class Entry
            {
                internal string DisplayName;
                internal string Path;
                internal string RealType;
                internal string SheetType;
                internal string Tooltip;
                internal object Space;
                internal bool Spaceable;
                internal float Min;
                internal float Max;
                internal List<string> EnumValues;
                internal Type Type;
                internal object DefaultValue;
            }

            private readonly Dictionary<UnityEngine.Object, List<Entry>> values =
                new Dictionary<UnityEngine.Object, List<Entry>>();
            private int total;

            internal IReadOnlyList<Entry> Get(UnityEngine.Object asset)
            {
                if (asset == null)
                    return Array.Empty<Entry>();
                if (values.TryGetValue(asset, out List<Entry> cached))
                    return cached;
                List<Entry> entries = ExposedPropertyEntries(asset);
                total += entries.Count;
                if (total > VmAutomationVFXLimits.ReturnedOverridesPerRequest)
                    throw VmAutomationVFXError.Create("response_too_large",
                        $"Component inspection discovered more than {VmAutomationVFXLimits.ReturnedOverridesPerRequest} exposed properties across distinct VFX assets.");
                values.Add(asset, entries);
                return entries;
            }
        }

        private sealed class ComponentIndexCache
        {
            private readonly Type componentType;
            private readonly Dictionary<GameObject,
                Dictionary<Component, int>> values = new Dictionary<GameObject,
                    Dictionary<Component, int>>();

            internal ComponentIndexCache(Type componentType)
            {
                this.componentType = componentType;
            }

            internal int Get(Component component)
            {
                if (!values.TryGetValue(component.gameObject, out Dictionary<
                        Component, int> indices))
                {
                    indices = component.gameObject.GetComponents(componentType)
                        .Cast<Component>().Select((value, index) =>
                            new { value, index }).ToDictionary(item => item.value,
                            item => item.index);
                    values.Add(component.gameObject, indices);
                }
                return indices[component];
            }
        }

        private static readonly string[] SelectorKeys =
        {
            "prefabPath", "scenePath", "hierarchyPath", "hierarchyIndexPath",
            "componentIndex", "gameObjectInstanceId", "componentInstanceId",
        };

        internal static object Info(Dictionary<string, object> args)
        {
            if (!ValidateKeys(args, SelectorKeys.Concat(new[]
                {
                    "includeOverrides", "overrideOffset",
                    "maxOverridesPerComponent", "includeRuntimeState",
                    "systemOffset", "maxSystemsPerComponent",
                    "outputEventOffset", "maxOutputEventsPerComponent",
                    "offset", "limit", "_agentId",
                }), out object keyError))
                return keyError;
            if (!ValidateSelectorScope(args, out object selectorError))
                return selectorError;
            if (!VmAutomationVFXReflection.IsAvailable)
                return VmAutomationResponse.Error(
                    "VFX Graph is not available. Install com.unity.visualeffectgraph.",
                    "capability_unavailable");
            try
            {
                int offset = GetInt(args, "offset", 0);
                int limit = GetInt(args, "limit", 100);
                int overrideOffset = GetInt(args, "overrideOffset", 0);
                int maxOverrides = GetInt(args,
                    "maxOverridesPerComponent", 100);
                int systemOffset = GetInt(args, "systemOffset", 0);
                int maxSystems = GetInt(args, "maxSystemsPerComponent", 100);
                int outputEventOffset = GetInt(args, "outputEventOffset", 0);
                int maxOutputEvents = GetInt(args,
                    "maxOutputEventsPerComponent", 100);
                if (offset < 0 || limit < 1 || limit > 500)
                    return VmAutomationResponse.Error(
                        "offset must be at least 0 and limit must be between 1 and 500.",
                        "invalid_arguments");
                if (overrideOffset < 0 || maxOverrides < 1 ||
                    maxOverrides > 256)
                    return VmAutomationResponse.Error(
                        "overrideOffset must be at least 0 and maxOverridesPerComponent must be between 1 and 256.",
                        "invalid_arguments");
                if ((long)limit * maxOverrides >
                    VmAutomationVFXLimits.ReturnedOverridesPerRequest)
                    return VmAutomationResponse.Error(
                        $"The requested component and override page sizes can return more than {VmAutomationVFXLimits.ReturnedOverridesPerRequest} overrides. Reduce limit or maxOverridesPerComponent.",
                        "invalid_arguments");
                if (systemOffset < 0 || maxSystems < 1 || maxSystems > 256 ||
                    outputEventOffset < 0 || maxOutputEvents < 1 ||
                    maxOutputEvents > 256)
                    return VmAutomationResponse.Error(
                        "systemOffset and outputEventOffset must be at least 0; maxSystemsPerComponent and maxOutputEventsPerComponent must be between 1 and 256.",
                        "invalid_arguments");
                if ((long)limit * (maxSystems + maxOutputEvents) >
                    VmAutomationVFXLimits.ReturnedRuntimeRecordsPerRequest)
                    return VmAutomationResponse.Error(
                        $"The requested component and runtime page sizes can return more than {VmAutomationVFXLimits.ReturnedRuntimeRecordsPerRequest} records. Reduce limit or the per-component page sizes.",
                        "invalid_arguments");
                if (args != null && args.ContainsKey("componentIndex") &&
                    GetInt(args, "componentIndex", 0) < 0)
                    return VmAutomationResponse.Error(
                        "componentIndex must be at least 0.",
                        "invalid_arguments");
                bool includeOverrides = GetBool(args, "includeOverrides", true);
                bool includeRuntimeState = GetBool(args,
                    "includeRuntimeState", false);
                if (includeRuntimeState && !string.IsNullOrEmpty(GetString(args,
                        "prefabPath")))
                    return VmAutomationResponse.Error(
                        "includeRuntimeState is only valid for loaded scene components.",
                        "invalid_arguments");
                if (includeRuntimeState && !EditorApplication.isPlaying)
                    return VmAutomationResponse.Error(
                        "includeRuntimeState requires Play Mode.",
                        "play_mode_required");
                bool exact = HasExactSelector(args);
                var propertyCache = new ExposedPropertyCache();
                var runtimeState = new VmAutomationVFXRuntimeState();
                if (exact)
                {
                    if (!VmAutomationVFXComponentTarget.TryResolve(args, true,
                            out VmAutomationVFXComponentTarget target, out object error))
                        return error;
                    using (target)
                    {
                        return new Dictionary<string, object>
                        {
                            { "success", true }, { "total", 1 },
                            { "offset", 0 }, { "returned", 1 },
                            { "runtimeStateIncluded", includeRuntimeState },
                            { "components", new List<Dictionary<string, object>>
                                { Summary(target.Component, target.Identity(),
                                    includeOverrides, overrideOffset,
                                    maxOverrides, propertyCache,
                                    includeRuntimeState, systemOffset, maxSystems,
                                    outputEventOffset, maxOutputEvents,
                                    runtimeState) } },
                        };
                    }
                }
                string prefabPath = GetString(args, "prefabPath");
                int? componentIndexFilter = args != null &&
                    args.ContainsKey("componentIndex")
                        ? GetInt(args, "componentIndex", 0) : (int?)null;
                List<Dictionary<string, object>> page;
                int total;
                if (!string.IsNullOrEmpty(prefabPath))
                {
                    page = EnumeratePrefab(prefabPath, includeOverrides,
                        overrideOffset, maxOverrides, componentIndexFilter,
                        offset, limit, propertyCache, out total);
                }
                else
                {
                    Type visualEffectType = VmAutomationVFXReflection.RequireType(
                        VmAutomationVFXReflection.VisualEffectTypeName);
                    var indexCache = new ComponentIndexCache(visualEffectType);
                    List<Component> all = VmAutomationVFXComponentTarget
                        .EnumerateLoadedComponents(GetString(args, "scenePath"))
                        .Where(component => !componentIndexFilter.HasValue ||
                            indexCache.Get(component) ==
                            componentIndexFilter.Value).ToList();
                    total = all.Count;
                    page = all.Skip(offset).Take(limit)
                        .Select(component => Summary(component,
                            LoadedIdentity(component, indexCache), includeOverrides,
                            overrideOffset, maxOverrides, propertyCache,
                            includeRuntimeState, systemOffset, maxSystems,
                            outputEventOffset, maxOutputEvents,
                            runtimeState)).ToList();
                }
                return new Dictionary<string, object>
                {
                    { "success", true }, { "total", total },
                    { "offset", offset }, { "limit", limit },
                    { "returned", page.Count },
                    { "runtimeStateIncluded", includeRuntimeState },
                    { "hasMore", offset + page.Count < total },
                    { "nextOffset", offset + page.Count < total
                        ? (object)(offset + page.Count) : null },
                    { "components", page },
                };
            }
            catch (Exception exception)
            {
                return VmAutomationVFXError.Response(exception,
                    "vfx_component_info_failed");
            }
        }

        internal static object Transaction(Dictionary<string, object> args)
        {
            if (!ValidateKeys(args, SelectorKeys.Concat(new[]
                {
                    "operations", "dryRun", "_agentId",
                }), out object keyError))
                return keyError;
            if (!ValidateSelectorScope(args, out object selectorError))
                return selectorError;
            List<object> rawOperations = args != null &&
                args.TryGetValue("operations", out object raw)
                    ? VmAutomationVFXGraphMutationContext.AsList(raw) : null;
            if (rawOperations == null || rawOperations.Count == 0 ||
                rawOperations.Count > 128)
                return VmAutomationResponse.Error(
                    "operations must contain between 1 and 128 entries.",
                    "invalid_arguments");
            if (!VmAutomationVFXComponentTarget.TryResolve(args, true,
                    out VmAutomationVFXComponentTarget target, out object resolveError))
                return resolveError;
            using (target)
            {
                Component renderer = RequireRenderer(target.Component);
                string componentBackup = EditorJsonUtility.ToJson(
                    target.Component, true);
                string rendererBackup = EditorJsonUtility.ToJson(renderer, true);
                bool dryRun;
                try
                {
                    dryRun = GetBool(args, "dryRun", false);
                }
                catch (Exception exception)
                {
                    return VmAutomationVFXError.Response(exception,
                        "vfx_component_transaction_failed");
                }
                var results = new List<Dictionary<string, object>>();
                try
                {
                    if (!target.IsPrefab)
                        Undo.RecordObjects(new UnityEngine.Object[]
                            {
                                target.Component, renderer,
                            }, "Edit Visual Effect Component");
                    for (int index = 0; index < rawOperations.Count; index++)
                    {
                        Dictionary<string, object> operation =
                            VmAutomationVFXGraphMutationContext.AsDictionary(
                                rawOperations[index]) ??
                            throw new ArgumentException(
                                $"operations[{index}] must be an object.");
                        Dictionary<string, object> result =
                            ApplyComponentOperation(target, renderer, operation,
                                index);
                        result["index"] = index;
                        results.Add(result);
                    }
                    if (dryRun)
                    {
                        EditorJsonUtility.FromJsonOverwrite(componentBackup,
                            target.Component);
                        EditorJsonUtility.FromJsonOverwrite(rendererBackup,
                            renderer);
                    }
                    else
                        target.Save(renderer);
                    return new Dictionary<string, object>
                    {
                        { "success", true }, { "dryRun", dryRun },
                        { "target", target.Identity() },
                        { "operationCount", results.Count },
                        { "results", results },
                        { "component", Summary(target.Component,
                            target.Identity(), true, 0, 100,
                            new ExposedPropertyCache(), false, 0, 100,
                            0, 100, new VmAutomationVFXRuntimeState()) },
                    };
                }
                catch (Exception exception)
                {
                    try
                    {
                        EditorJsonUtility.FromJsonOverwrite(componentBackup,
                            target.Component);
                        EditorJsonUtility.FromJsonOverwrite(rendererBackup,
                            renderer);
                        if (!dryRun)
                            target.Save(renderer);
                    }
                    catch (Exception rollbackException)
                    {
                        return VmAutomationResponse.Error(
                            $"VFX component transaction failed: {VmAutomationVFXReflection.Unwrap(exception).Message}. Rollback failed: {VmAutomationVFXReflection.Unwrap(rollbackException).Message}",
                            "vfx_transaction_rollback_failed");
                    }
                    Exception failure = VmAutomationVFXReflection.Unwrap(exception);
                    return VmAutomationResponse.Error(failure.Message,
                        VmAutomationVFXError.Code(failure,
                            "vfx_component_transaction_failed"), false,
                        new Dictionary<string, object>
                        {
                            { "failedOperationIndex", results.Count },
                            { "rolledBack", true },
                        });
                }
            }
        }

        internal static void ControlDeferred(Dictionary<string, object> args,
            Action<object> resolve, Action<object> progress)
        {
            if (!ValidateKeys(args, SelectorKeys.Concat(new[]
                {
                    "action", "eventName", "eventAttributes", "deltaTime",
                    "stepCount", "propertyName", "value", "timeoutMs",
                    "_agentId",
                }), out object keyError))
            {
                resolve(keyError);
                return;
            }
            if (!EditorApplication.isPlaying)
            {
                resolve(VmAutomationResponse.Error(
                    "vfxgraph/component-control requires Play Mode.",
                    "play_mode_required"));
                return;
            }
            if (!string.IsNullOrEmpty(GetString(args, "prefabPath")))
            {
                resolve(VmAutomationResponse.Error(
                    "component-control only supports loaded scene components.",
                    "invalid_arguments"));
                return;
            }
            if (!VmAutomationVFXComponentTarget.TryResolve(args, false,
                    out VmAutomationVFXComponentTarget target, out object resolveError))
            {
                resolve(resolveError);
                return;
            }
            using (target)
            {
                string action = GetString(args, "action").ToLowerInvariant();
                try
                {
                    Component component = target.Component;
                    Dictionary<string, object> identity = target.Identity();
                    Dictionary<string, object> stateBefore = StateSummary(component);
                    bool observeVfxUpdate = action == "advance-one-frame" ||
                                            action == "simulate";
                    if (observeVfxUpdate && EditorApplication.isPaused)
                    {
                        resolve(VmAutomationResponse.Error(
                            $"{action} cannot prove completion while Play Mode is globally paused. Keep Play Mode running and pause only the selected VisualEffect component.",
                            "play_mode_paused"));
                        return;
                    }
                    if (observeVfxUpdate &&
                        (!(VmAutomationVFXReflection.Get(component, "pause") is bool
                            componentPaused) || !componentPaused))
                    {
                        resolve(VmAutomationResponse.Error(
                            $"{action} requires the selected VisualEffect component to be paused so the requested simulation can be distinguished from normal playback.",
                            "vfx_component_pause_required"));
                        return;
                    }

                    switch (action)
                    {
                        case "play": VmAutomationVFXReflection.Invoke(component,
                            "Play"); break;
                        case "stop": VmAutomationVFXReflection.Invoke(component,
                            "Stop"); break;
                        case "pause": SetMember(component, "pause", true,
                            "pause"); break;
                        case "resume": SetMember(component, "pause", false,
                            "pause"); break;
                        case "reinit": VmAutomationVFXReflection.Invoke(component,
                            "Reinit"); break;
                        case "advance-one-frame":
                            VmAutomationVFXReflection.Invoke(component,
                                "AdvanceOneFrame");
                            break;
                        case "simulate": Simulate(component, args); break;
                        case "send-event": SendEvent(component, args); break;
                        case "set-override": SetRuntimeOverride(target,
                            RequireString(args, "propertyName"),
                            Required(args, "value")); break;
                        case "reset-override": ResetRuntimeOverride(component,
                            RequireString(args, "propertyName")); break;
                        default:
                            resolve(VmAutomationResponse.Error(
                                "action must be play, stop, pause, resume, reinit, advance-one-frame, simulate, send-event, set-override, or reset-override.",
                                "invalid_arguments"));
                            return;
                    }

                    if (!observeVfxUpdate)
                    {
                        resolve(BuildControlResult(action, identity, stateBefore,
                            StateSummary(component), new Dictionary<string, object>
                            {
                                { "mode", "immediate-readback" },
                                { "effectUpdateObserved", false },
                                { "editorUpdateCount", 0 },
                                { "elapsedMs", 0d },
                                { "expectedTimeDelta", null },
                                { "observedTimeDelta", null },
                            }));
                        return;
                    }

                    ObserveSimulationCompletion(component, action, args,
                        identity, stateBefore, resolve, progress);
                }
                catch (Exception exception)
                {
                    resolve(VmAutomationVFXError.Response(exception,
                        "vfx_component_control_failed"));
                }
            }
        }

        internal static object Control(Dictionary<string, object> args)
        {
            return VmAutomationResponse.Error(
                "vfxgraph/component-control must be executed through the deferred route.",
                "deferred_route_required");
        }

        private static void ObserveSimulationCompletion(Component component,
            string action, Dictionary<string, object> args,
            Dictionary<string, object> identity,
            Dictionary<string, object> stateBefore, Action<object> resolve,
            Action<object> progress)
        {
            double beforeTime = StateTime(stateBefore);
            double expectedTimeDelta = action == "simulate"
                ? SimulationDuration(args)
                : 0d;
            int timeoutMs = GetInt(args, "timeoutMs", 3000);
            if (timeoutMs < 100 || timeoutMs > 10000)
                throw new ArgumentException("timeoutMs must be in [100, 10000].");

            double startedAt = EditorApplication.timeSinceStartup;
            double nextProgressAtMs = 1000d;
            int editorUpdateCount = 0;
            bool completed = false;
            EditorApplication.CallbackFunction tick = null;

            void Finish(object result)
            {
                if (completed)
                    return;
                completed = true;
                if (tick != null)
                    EditorApplication.update -= tick;
                resolve(result);
            }

            tick = () =>
            {
                try
                {
                    editorUpdateCount++;
                    double elapsedMs = (EditorApplication.timeSinceStartup -
                                        startedAt) * 1000d;
                    if (!EditorApplication.isPlaying)
                    {
                        Finish(VmAutomationResponse.Error(
                            $"Play Mode ended before {action} reached a VisualEffect update.",
                            "play_mode_ended"));
                        return;
                    }
                    if (EditorApplication.isPaused)
                    {
                        Finish(VmAutomationResponse.Error(
                            $"Play Mode was globally paused before {action} reached a VisualEffect update.",
                            "play_mode_paused"));
                        return;
                    }
                    if (component == null)
                    {
                        Finish(VmAutomationResponse.Error(
                            $"The selected VisualEffect component was destroyed before {action} completed.",
                            "component_not_found"));
                        return;
                    }

                    Dictionary<string, object> state = StateSummary(component);
                    double observedTimeDelta = StateTime(state) - beforeTime;
                    double tolerance = Math.Max(0.000001d,
                        expectedTimeDelta * 0.0001d);
                    bool updateObserved = action == "simulate"
                        ? observedTimeDelta + tolerance >= expectedTimeDelta
                        : observedTimeDelta > tolerance;
                    if (updateObserved)
                    {
                        Finish(BuildControlResult(action, identity, stateBefore,
                            state, new Dictionary<string, object>
                            {
                                { "mode", "visual-effect-update-observed" },
                                { "effectUpdateObserved", true },
                                { "editorUpdateCount", editorUpdateCount },
                                { "elapsedMs", Math.Round(elapsedMs, 2) },
                                { "expectedTimeDelta", action == "simulate"
                                    ? (object)expectedTimeDelta : null },
                                { "observedTimeDelta", observedTimeDelta },
                            }));
                        return;
                    }

                    if (elapsedMs >= timeoutMs)
                    {
                        Finish(VmAutomationResponse.Error(
                            $"{action} was issued, but no matching VisualEffect update was observed within {timeoutMs} ms.",
                            "vfx_update_not_observed", false,
                            new Dictionary<string, object>
                            {
                                { "action", action }, { "target", identity },
                                { "stateBefore", stateBefore },
                                { "state", state },
                                { "completion", new Dictionary<string, object>
                                    {
                                        { "mode", "timed-out" },
                                        { "effectUpdateObserved", false },
                                        { "editorUpdateCount", editorUpdateCount },
                                        { "elapsedMs", Math.Round(elapsedMs, 2) },
                                        { "expectedTimeDelta", action == "simulate"
                                            ? (object)expectedTimeDelta : null },
                                        { "observedTimeDelta", observedTimeDelta },
                                    }
                                },
                            }));
                        return;
                    }

                    if (elapsedMs >= nextProgressAtMs)
                    {
                        nextProgressAtMs += 1000d;
                        progress?.Invoke(new Dictionary<string, object>
                        {
                            { "phase", "awaiting-visual-effect-update" },
                            { "action", action },
                            { "editorUpdateCount", editorUpdateCount },
                            { "elapsedMs", Math.Round(elapsedMs, 2) },
                            { "expectedTimeDelta", action == "simulate"
                                ? (object)expectedTimeDelta : null },
                            { "observedTimeDelta", observedTimeDelta },
                        });
                    }
                    EditorApplication.QueuePlayerLoopUpdate();
                }
                catch (Exception exception)
                {
                    Finish(VmAutomationVFXError.Response(exception,
                        "vfx_component_control_failed"));
                }
            };

            EditorApplication.update += tick;
            EditorApplication.QueuePlayerLoopUpdate();
        }

        private static Dictionary<string, object> BuildControlResult(string action,
            Dictionary<string, object> identity,
            Dictionary<string, object> stateBefore,
            Dictionary<string, object> state,
            Dictionary<string, object> completion)
        {
            return new Dictionary<string, object>
            {
                { "success", true }, { "action", action },
                { "target", identity }, { "stateBefore", stateBefore },
                { "state", state }, { "completion", completion },
            };
        }

        private static double StateTime(Dictionary<string, object> state)
        {
            if (state == null || !state.TryGetValue("time", out object value) ||
                value == null)
                throw new InvalidOperationException(
                    "The installed VisualEffect API does not expose runtime time for completion verification.");
            return Convert.ToDouble(value);
        }

        private static double SimulationDuration(Dictionary<string, object> args)
        {
            float deltaTime = args != null && args.TryGetValue("deltaTime",
                out object rawDelta) ? (float)VmAutomationVFXValueCodec.ConvertTo(
                    rawDelta, typeof(float), "deltaTime") : 1f / 60f;
            int stepCount = GetInt(args, "stepCount", 1);
            ValidateSimulationBounds(deltaTime, stepCount);
            return (double)deltaTime * stepCount;
        }

        private static Dictionary<string, object> ApplyComponentOperation(
            VmAutomationVFXComponentTarget target, Component renderer,
            Dictionary<string, object> operation, int index)
        {
            Component component = target.Component;
            string op = GetString(operation, "op").ToLowerInvariant();
            switch (op)
            {
                case "set-asset":
                    ValidateOperationKeys(operation, index, "op", "assetPath",
                        "clear");
                    bool clearAsset = GetBool(operation, "clear", false);
                    bool hasAssetPath = !string.IsNullOrWhiteSpace(GetString(
                        operation, "assetPath"));
                    if (clearAsset == hasAssetPath)
                        throw new ArgumentException(
                            $"operations[{index}] must provide exactly one of clear=true or assetPath.");
                    Type assetType = VmAutomationVFXReflection.GetMemberType(component,
                        "visualEffectAsset");
                    object asset = clearAsset ? null :
                        ConvertValue(
                            new Dictionary<string, object>
                            {
                                { "assetPath", RequireString(operation,
                                    "assetPath") },
                            }, assetType, $"operations[{index}].assetPath");
                    SetMember(component, "visualEffectAsset", asset,
                        "visualEffectAsset");
                    return Result(op, "assetPath",
                        AssetDatabase.GetAssetPath(asset as UnityEngine.Object));
                case "set-enabled":
                    ValidateOperationKeys(operation, index, "op", "value");
                    SetTypedMember(component, "enabled",
                        Required(operation, "value"),
                        $"operations[{index}].value");
                    return Result(op, "value", VmAutomationVFXReflection.Get(component,
                        "enabled"));
                case "set-seed":
                    ValidateOperationKeys(operation, index, "op", "startSeed",
                        "resetSeedOnPlay");
                    if (!operation.ContainsKey("startSeed") &&
                        !operation.ContainsKey("resetSeedOnPlay"))
                        throw new ArgumentException(
                            $"operations[{index}] must provide startSeed, resetSeedOnPlay, or both.");
                    if (operation.TryGetValue("startSeed", out object seed))
                        SetTypedMember(component, "startSeed", seed,
                            $"operations[{index}].startSeed");
                    if (operation.TryGetValue("resetSeedOnPlay", out object reset))
                        SetTypedMember(component, "resetSeedOnPlay", reset,
                            $"operations[{index}].resetSeedOnPlay");
                    return new Dictionary<string, object>
                    {
                        { "op", op },
                        { "startSeed", VmAutomationVFXReflection.Get(component,
                            "startSeed") },
                        { "resetSeedOnPlay", VmAutomationVFXReflection.Get(component,
                            "resetSeedOnPlay") },
                    };
                case "set-initial-event":
                    ValidateOperationKeys(operation, index, "op", "name");
                    string initialEventName = RequireString(operation, "name");
                    ValidateEventName(component, initialEventName);
                    SetTypedMember(component, "initialEventName",
                        initialEventName,
                        $"operations[{index}].name");
                    return Result(op, "name", VmAutomationVFXReflection.Get(component,
                        "initialEventName"));
                case "set-rendering":
                    ValidateOperationKeys(operation, index, "op", "propertyName",
                        "value");
                    string propertyName = RequireString(operation, "propertyName");
                    object rawRenderingValue = Required(operation, "value");
                    object appliedValue;
                    if (PersistentComponentMembers().Contains(propertyName))
                    {
                        SetTypedMember(component, propertyName,
                            rawRenderingValue,
                            $"operations[{index}].value");
                        appliedValue = VmAutomationVFXReflection.Get(component,
                            propertyName);
                    }
                    else if (PersistentRendererMembers().Contains(propertyName))
                    {
                        SetRendererMember(target, renderer, propertyName,
                            rawRenderingValue,
                            $"operations[{index}].value");
                        appliedValue = VmAutomationVFXReflection.Get(renderer,
                            propertyName);
                    }
                    else
                    {
                        throw new ArgumentException(
                            $"Property '{propertyName}' is not a documented persistent VisualEffect component setting.");
                    }
                    return new Dictionary<string, object>
                    {
                        { "op", op }, { "propertyName", propertyName },
                        { "value", VmAutomationVFXValueCodec.Sanitize(appliedValue) },
                    };
                case "set-override":
                    ValidateOperationKeys(operation, index, "op", "propertyName",
                        "value");
                    string overrideName = RequireString(operation, "propertyName");
                    SetPersistentOverride(target, overrideName,
                        Required(operation, "value"));
                    return Result(op, "propertyName", overrideName);
                case "reset-override":
                    ValidateOperationKeys(operation, index, "op", "propertyName");
                    string resetName = RequireString(operation, "propertyName");
                    ResetPersistentOverride(component, resetName);
                    return Result(op, "propertyName", resetName);
                default:
                    throw new ArgumentException(
                        $"operations[{index}].op '{op}' is not supported.");
            }
        }

        private static Dictionary<string, object> Summary(Component component,
            Dictionary<string, object> identity, bool includeOverrides,
            int overrideOffset, int maxOverrides,
            ExposedPropertyCache propertyCache, bool includeRuntimeState,
            int systemOffset, int maxSystems, int outputEventOffset,
            int maxOutputEvents, VmAutomationVFXRuntimeState runtimeState)
        {
            object asset = VmAutomationVFXReflection.Get(component, "visualEffectAsset");
            Component renderer = RequireRenderer(component);
            Dictionary<string, object> result = identity ??
                new Dictionary<string, object>();
            result["enabled"] = (component as Behaviour)?.enabled ?? true;
            result["gameObjectActive"] = component.gameObject.activeSelf;
            result["gameObjectActiveInHierarchy"] =
                component.gameObject.activeInHierarchy;
            result["assetPath"] = AssetDatabase.GetAssetPath(
                asset as UnityEngine.Object) ?? "";
            result["assetName"] = (asset as UnityEngine.Object)?.name ?? "";
            foreach (string member in PersistentComponentSummaryMembers())
            {
                object value = VmAutomationVFXReflection.Get(component, member);
                if (value != null)
                    result[member] = VmAutomationVFXValueCodec.Sanitize(value);
            }
            foreach (KeyValuePair<string, object> pair in StateSummary(component))
                result[pair.Key] = pair.Value;
            result["renderer"] = RendererSummary(renderer);
            result["overridesIncluded"] = includeOverrides;
            if (includeOverrides)
            {
                List<Dictionary<string, object>> allOverrides =
                    OverrideSummaries(component, asset as UnityEngine.Object,
                        propertyCache);
                List<Dictionary<string, object>> overrides = allOverrides
                    .Skip(overrideOffset).Take(maxOverrides).ToList();
                result["overrideCount"] = allOverrides.Count;
                result["overrideOffset"] = overrideOffset;
                result["returnedOverrideCount"] = overrides.Count;
                result["overridesTruncated"] = overrideOffset +
                    overrides.Count < allOverrides.Count;
                result["nextOverrideOffset"] = overrideOffset +
                    overrides.Count < allOverrides.Count
                        ? (object)(overrideOffset + overrides.Count) : null;
                result["overrides"] = overrides;
            }
            else
            {
                result["overrideCount"] = null;
                result["overrideOffset"] = overrideOffset;
                result["returnedOverrideCount"] = 0;
                result["overridesTruncated"] = false;
                result["nextOverrideOffset"] = null;
                result["overrides"] = new List<object>();
            }
            result["runtimeStateIncluded"] = includeRuntimeState;
            result["runtimeState"] = includeRuntimeState
                ? (object)runtimeState.Inspect(component, systemOffset,
                    maxSystems, outputEventOffset, maxOutputEvents)
                : null;
            return result;
        }

        private static Dictionary<string, object> StateSummary(Component component)
        {
            var result = new Dictionary<string, object>();
            foreach (string member in new[]
                     {
                         "pause", "playRate", "time", "aliveParticleCount",
                         "culled", "isActiveAndEnabled",
                     })
            {
                object value = VmAutomationVFXReflection.Get(component, member);
                if (value != null)
                    result[member] = VmAutomationVFXValueCodec.Sanitize(value);
            }
            return result;
        }

        private static Dictionary<string, object> RendererSummary(
            Component renderer)
        {
            var result = new Dictionary<string, object>
            {
                { "enabled", (renderer as Behaviour)?.enabled ?? true },
            };
            foreach (string member in PersistentRendererMembers().Concat(new[]
                     {
                         "bounds", "localBounds", "isVisible",
                     }))
            {
                object value = VmAutomationVFXReflection.Get(renderer, member);
                if (value != null)
                    result[member] = VmAutomationVFXValueCodec.Sanitize(value);
            }
            return result;
        }

        private static List<Dictionary<string, object>> OverrideSummaries(
            Component component, UnityEngine.Object asset,
            ExposedPropertyCache propertyCache)
        {
            if (asset == null)
                return new List<Dictionary<string, object>>();
            return propertyCache.Get(asset).Select(entry =>
            {
                string name = entry.Path;
                Type type = entry.Type;
                bool graphicsBuffer = IsGraphicsBuffer(type);
                bool overrideStateAvailable = !string.IsNullOrEmpty(
                    entry.SheetType);
                bool overridden = overrideStateAvailable && IsOverridden(
                    component, name, entry.SheetType);
                object effectiveValue = graphicsBuffer ? null :
                    overrideStateAvailable && !overridden
                        ? entry.DefaultValue
                        : GetStoredOverrideValue(component, name,
                            entry.SheetType, type);
                var result = new Dictionary<string, object>
                {
                    { "name", name }, { "displayName", entry.DisplayName },
                    { "type", type?.FullName ?? "" },
                    { "realType", entry.RealType },
                    { "sheetType", entry.SheetType },
                    { "tooltip", entry.Tooltip },
                    { "spaceable", entry.Spaceable },
                    { "space", entry.Space?.ToString() ?? "" },
                    { "min", float.IsNegativeInfinity(entry.Min)
                        ? null : (object)entry.Min },
                    { "max", float.IsPositiveInfinity(entry.Max)
                        ? null : (object)entry.Max },
                    { "enumValues", entry.EnumValues },
                    { "assetDefaultValue", VmAutomationVFXValueCodec.Sanitize(
                        entry.DefaultValue) },
                    { "valueReadable", !graphicsBuffer },
                    { "value", VmAutomationVFXValueCodec.Sanitize(effectiveValue) },
                    { "overrideStateAvailable", overrideStateAvailable },
                    { "overridden", overrideStateAvailable
                        ? (object)overridden : null },
                    { "settable", !graphicsBuffer },
                    { "setUnavailableReason", graphicsBuffer
                        ? "GraphicsBuffer lifetime is runtime-code-owned and cannot be reconstructed from JSON or persisted in a VisualEffect property sheet."
                        : "" },
                };
                if (type != null && typeof(Texture).IsAssignableFrom(type))
                    result["textureDimension"] = VmAutomationVFXReflection.Invoke(asset,
                        "GetTextureDimension", name)?.ToString() ?? "";
                return result;
            }).ToList();
        }

        private static List<ExposedPropertyCache.Entry> ExposedPropertyEntries(
            UnityEngine.Object asset)
        {
            string assetPath = AssetDatabase.GetAssetPath(asset);
            if (!VmAutomationVFXGraphSession.TryOpen(assetPath,
                    out VmAutomationVFXGraphSession session, out object error))
                throw new InvalidOperationException(
                    $"Could not inspect exposed component properties for '{assetPath}': {MiniJson.Serialize(error)}");
            Type parameterInfoType = VmAutomationVFXReflection.RequireType(
                VmAutomationVFXReflection.ParameterInfoTypeName);
            List<object> descriptors = VmAutomationVFXReflection.Enumerate(
                    VmAutomationVFXReflection.Invoke(parameterInfoType,
                        "BuildParameterInfo", session.Graph))
                .Take(VmAutomationVFXLimits.CollectionItems + 1).ToList();
            if (descriptors.Count > VmAutomationVFXLimits.CollectionItems)
                throw VmAutomationVFXError.Create("response_too_large",
                    $"VisualEffectAsset '{asset.name}' exposes more than {VmAutomationVFXLimits.CollectionItems} component property records.");
            var result = new List<ExposedPropertyCache.Entry>();
            foreach (object descriptor in descriptors)
            {
                string path = VmAutomationVFXReflection.Get(descriptor,
                    "path")?.ToString() ?? "";
                string sheetType = VmAutomationVFXReflection.Get(descriptor,
                    "sheetType")?.ToString() ?? "";
                if (string.IsNullOrEmpty(path) ||
                    string.IsNullOrEmpty(sheetType))
                    continue;
                object serializableDefault = VmAutomationVFXReflection.Get(descriptor,
                    "defaultValue");
                Type type = VmAutomationVFXReflection.Get(serializableDefault,
                    "type") as Type;
                object defaultValue = VmAutomationVFXReflection.Invoke(
                    serializableDefault, "Get");
                type ??= defaultValue?.GetType();
                if (type == null)
                    throw new InvalidOperationException(
                        $"Exposed VFX component property '{path}' has no CLR type.");
                if (result.Any(entry => string.Equals(entry.Path, path,
                        StringComparison.Ordinal)))
                    throw new InvalidOperationException(
                        $"VisualEffectAsset '{asset.name}' contains duplicate exposed component property path '{path}'.");
                result.Add(new ExposedPropertyCache.Entry
                {
                    DisplayName = VmAutomationVFXReflection.Get(descriptor,
                        "name")?.ToString() ?? path,
                    Path = path,
                    RealType = VmAutomationVFXReflection.Get(descriptor,
                        "realType")?.ToString() ?? type.Name,
                    SheetType = sheetType,
                    Tooltip = VmAutomationVFXReflection.Get(descriptor,
                        "tooltip")?.ToString() ?? "",
                    Space = VmAutomationVFXReflection.Get(descriptor, "space"),
                    Spaceable = VmAutomationVFXReflection.Get(descriptor,
                        "spaceable") is bool spaceable && spaceable,
                    Min = VmAutomationVFXReflection.Get(descriptor, "min") is float min
                        ? min : float.NegativeInfinity,
                    Max = VmAutomationVFXReflection.Get(descriptor, "max") is float max
                        ? max : float.PositiveInfinity,
                    EnumValues = VmAutomationVFXReflection.Enumerate(
                            VmAutomationVFXReflection.Get(descriptor, "enumValues"))
                        .Take(VmAutomationVFXLimits.CatalogMetadataPerItem + 1)
                        .Select(value => value?.ToString() ?? "").ToList(),
                    Type = type,
                    DefaultValue = defaultValue,
                });
                if (result[result.Count - 1].EnumValues.Count >
                    VmAutomationVFXLimits.CatalogMetadataPerItem)
                    throw VmAutomationVFXError.Create("response_too_large",
                        $"Exposed VFX component property '{path}' has more than {VmAutomationVFXLimits.CatalogMetadataPerItem} enum labels.");
            }
            return result;
        }

        private static object GetOverrideValue(Component component, string name,
            Type type)
        {
            string suffix = MethodSuffix(type);
            object value = VmAutomationVFXReflection.Invoke(component, "Get" + suffix,
                name);
            if (type == typeof(Color) && value is Vector4 vector)
                return new Color(vector.x, vector.y, vector.z, vector.w);
            return value;
        }

        private static void SetPersistentOverride(VmAutomationVFXComponentTarget target,
            string name, object rawValue)
        {
            Component component = target.Component;
            UnityEngine.Object asset = VmAutomationVFXReflection.Get(component,
                "visualEffectAsset") as UnityEngine.Object;
            if (asset == null)
                throw new InvalidOperationException(
                    "The VisualEffect component has no assigned asset.");
            ExposedPropertyCache.Entry property = RequireExposedProperty(asset,
                name);
            Type type = property.Type;
            if (IsGraphicsBuffer(type))
                throw VmAutomationVFXError.Create("unsupported_vfx_value_type",
                    $"Exposed VFX property '{name}' is a GraphicsBuffer. Its allocation, data upload, and disposal require a runtime code owner and cannot be represented by this JSON transaction.");
            object converted = ConvertOverrideValue(rawValue, type,
                "value", target);
            var serialized = new SerializedObject(component);
            serialized.Update();
            SerializedProperty array = serialized.FindProperty(
                "m_PropertySheet." + property.SheetType + ".m_Array");
            if (array == null || !array.isArray)
                throw new MissingMemberException(component.GetType().FullName,
                    "m_PropertySheet." + property.SheetType + ".m_Array");
            SerializedProperty entry = FindOverrideEntry(array, name);
            if (entry == null)
            {
                array.InsertArrayElementAtIndex(array.arraySize);
                entry = array.GetArrayElementAtIndex(array.arraySize - 1);
                entry.FindPropertyRelative("m_Name").stringValue = name;
            }
            entry.FindPropertyRelative("m_Overridden").boolValue = true;
            SetSerializedValue(entry.FindPropertyRelative("m_Value"), converted,
                "value");
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetRuntimeOverride(VmAutomationVFXComponentTarget target,
            string name, object rawValue)
        {
            Component component = target.Component;
            UnityEngine.Object asset = VmAutomationVFXReflection.Get(component,
                "visualEffectAsset") as UnityEngine.Object;
            if (asset == null)
                throw new InvalidOperationException(
                    "The VisualEffect component has no assigned asset.");
            ExposedPropertyCache.Entry property = RequireExposedProperty(asset,
                name);
            Type type = property.Type;
            if (IsGraphicsBuffer(type))
                throw VmAutomationVFXError.Create("unsupported_vfx_value_type",
                    $"Exposed VFX property '{name}' is a GraphicsBuffer. Its allocation, data upload, and disposal require a runtime code owner and cannot be represented by this JSON transaction.");
            object converted = ConvertOverrideValue(rawValue, type, "value",
                target);
            VmAutomationVFXReflection.Invoke(component, "Set" + MethodSuffix(type), name,
                converted);
        }

        private static object ConvertOverrideValue(object rawValue, Type type,
            string path, VmAutomationVFXComponentTarget target)
        {
            if (type == typeof(Color))
            {
                Color color = (Color)ConvertValue(rawValue,
                    typeof(Color), path);
                return (Vector4)color;
            }
            if (typeof(Component).IsAssignableFrom(type) ||
                type == typeof(GameObject) || type == typeof(Transform))
                return target.ResolveObjectReference(rawValue, type, path);
            return ConvertValue(rawValue, type, path);
        }

        private static ExposedPropertyCache.Entry RequireExposedProperty(
            UnityEngine.Object asset,
            string name)
        {
            ExposedPropertyCache.Entry property = ExposedPropertyEntries(asset)
                .FirstOrDefault(item => string.Equals(item.Path, name,
                    StringComparison.Ordinal));
            if (property == null)
                throw VmAutomationVFXError.Create("property_not_found",
                    $"Exposed VFX property '{name}' was not found on '{asset.name}'.");
            return property;
        }

        private static void ResetPersistentOverride(Component component,
            string name)
        {
            UnityEngine.Object asset = VmAutomationVFXReflection.Get(component,
                "visualEffectAsset") as UnityEngine.Object;
            if (asset == null)
                throw new InvalidOperationException(
                    "The VisualEffect component has no assigned asset.");
            ExposedPropertyCache.Entry property = RequireExposedProperty(asset,
                name);
            var serialized = new SerializedObject(component);
            serialized.Update();
            SerializedProperty array = serialized.FindProperty(
                "m_PropertySheet." + property.SheetType + ".m_Array");
            SerializedProperty entry = FindOverrideEntry(array, name);
            if (entry != null)
            {
                entry.FindPropertyRelative("m_Overridden").boolValue = false;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void ResetRuntimeOverride(Component component,
            string name)
        {
            UnityEngine.Object asset = VmAutomationVFXReflection.Get(component,
                "visualEffectAsset") as UnityEngine.Object;
            if (asset == null)
                throw new InvalidOperationException(
                    "The VisualEffect component has no assigned asset.");
            RequireExposedProperty(asset, name);
            VmAutomationVFXReflection.Invoke(component, "ResetOverride", name);
        }

        private static string MethodSuffix(Type type)
        {
            if (type == typeof(Color)) return "Vector4";
            if (typeof(Texture).IsAssignableFrom(type)) return "Texture";
            if (type == typeof(Mesh)) return "Mesh";
            if (type == typeof(SkinnedMeshRenderer)) return "SkinnedMeshRenderer";
            string name = type?.Name ?? "";
            if (name == "GraphicsBuffer") return "GraphicsBuffer";
            if (name == "GraphicsBufferHandle") return "GraphicsBuffer";
            if (name == "ComputeBuffer") return "GraphicsBuffer";
            switch (name)
            {
                case "Boolean": return "Bool";
                case "Single": return "Float";
                case "Int32": return "Int";
                case "UInt32": return "UInt";
                default: return name;
            }
        }

        private static bool IsGraphicsBuffer(Type type)
        {
            string name = type?.Name ?? "";
            return name == "GraphicsBuffer" || name == "GraphicsBufferHandle" ||
                   name == "ComputeBuffer";
        }

        private static bool IsOverridden(Component component, string name,
            string sheet)
        {
            if (sheet == null)
                return false;
            SerializedProperty array = new SerializedObject(component).FindProperty(
                "m_PropertySheet." + sheet + ".m_Array");
            if (array == null || !array.isArray)
                return false;
            for (int index = 0; index < array.arraySize; index++)
            {
                SerializedProperty entry = array.GetArrayElementAtIndex(index);
                if (entry.FindPropertyRelative("m_Name")?.stringValue == name)
                    return entry.FindPropertyRelative("m_Overridden")?.boolValue ??
                           false;
            }
            return false;
        }

        private static object GetStoredOverrideValue(Component component,
            string name, string sheet, Type type)
        {
            SerializedProperty array = new SerializedObject(component)
                .FindProperty("m_PropertySheet." + sheet + ".m_Array");
            SerializedProperty entry = FindOverrideEntry(array, name);
            SerializedProperty value = entry?.FindPropertyRelative("m_Value");
            object stored = ReadSerializedValue(value, type);
            if (stored == null && type.IsValueType)
                return GetOverrideValue(component, name, type);
            if (type == typeof(Color) && stored is Vector4 vector)
                return new Color(vector.x, vector.y, vector.z, vector.w);
            return stored;
        }

        private static SerializedProperty FindOverrideEntry(
            SerializedProperty array, string name)
        {
            if (array == null || !array.isArray)
                return null;
            for (int index = 0; index < array.arraySize; index++)
            {
                SerializedProperty entry = array.GetArrayElementAtIndex(index);
                if (entry.FindPropertyRelative("m_Name")?.stringValue == name)
                    return entry;
            }
            return null;
        }

        private static object ReadSerializedValue(SerializedProperty property,
            Type valueType)
        {
            if (property == null)
                return null;
            switch (property.propertyType)
            {
                case SerializedPropertyType.Float: return property.floatValue;
                case SerializedPropertyType.Vector2: return property.vector2Value;
                case SerializedPropertyType.Vector3: return property.vector3Value;
                case SerializedPropertyType.Vector4: return property.vector4Value;
                case SerializedPropertyType.Color: return property.colorValue;
                case SerializedPropertyType.ObjectReference:
                    return property.objectReferenceValue;
                case SerializedPropertyType.Integer:
                    return valueType == typeof(uint)
                        ? (object)(uint)property.longValue
                        : (int)property.longValue;
                case SerializedPropertyType.Boolean: return property.boolValue;
                case SerializedPropertyType.Gradient: return property.gradientValue;
                case SerializedPropertyType.AnimationCurve:
                    return property.animationCurveValue;
                case SerializedPropertyType.Generic:
                    if (valueType == typeof(Matrix4x4))
                        return ReadMatrix(property);
                    break;
            }
            throw new InvalidOperationException(
                $"Unsupported VFX property-sheet value type {property.propertyType} for {valueType?.FullName ?? "unknown"}.");
        }

        private static void SetSerializedValue(SerializedProperty property,
            object value, string path)
        {
            if (property == null)
                throw new MissingMemberException(path);
            switch (property.propertyType)
            {
                case SerializedPropertyType.Float:
                    property.floatValue = (float)value;
                    return;
                case SerializedPropertyType.Vector2:
                    property.vector2Value = (Vector2)value;
                    return;
                case SerializedPropertyType.Vector3:
                    property.vector3Value = (Vector3)value;
                    return;
                case SerializedPropertyType.Vector4:
                    property.vector4Value = value is Color color
                        ? (Vector4)color : (Vector4)value;
                    return;
                case SerializedPropertyType.Color:
                    property.colorValue = value is Vector4 vector
                        ? (Color)vector : (Color)value;
                    return;
                case SerializedPropertyType.ObjectReference:
                    property.objectReferenceValue = (UnityEngine.Object)value;
                    return;
                case SerializedPropertyType.Integer:
                    property.longValue = value is uint unsigned
                        ? unsigned : (long)(int)value;
                    return;
                case SerializedPropertyType.Boolean:
                    property.boolValue = (bool)value;
                    return;
                case SerializedPropertyType.Gradient:
                    property.gradientValue = (Gradient)value;
                    return;
                case SerializedPropertyType.AnimationCurve:
                    property.animationCurveValue = (AnimationCurve)value;
                    return;
                case SerializedPropertyType.Generic:
                    if (value is Matrix4x4 matrix)
                    {
                        WriteMatrix(property, matrix);
                        return;
                    }
                    break;
            }
            throw VmAutomationVFXError.Create("unsupported_vfx_value_type",
                $"{path} cannot be stored in VFX property-sheet type {property.propertyType}.");
        }

        private static Matrix4x4 ReadMatrix(SerializedProperty property)
        {
            PropertyInfo accessor = VmAutomationVFXReflection.FindProperty(
                typeof(SerializedProperty), "matrix4x4Value") ??
                throw new MissingMemberException(typeof(SerializedProperty).FullName,
                    "matrix4x4Value");
            return (Matrix4x4)accessor.GetValue(property, null);
        }

        private static void WriteMatrix(SerializedProperty property,
            Matrix4x4 value)
        {
            PropertyInfo accessor = VmAutomationVFXReflection.FindProperty(
                typeof(SerializedProperty), "matrix4x4Value") ??
                throw new MissingMemberException(typeof(SerializedProperty).FullName,
                    "matrix4x4Value");
            accessor.SetValue(property, value, null);
        }

        private static void Simulate(Component component,
            Dictionary<string, object> args)
        {
            float deltaTime = args != null && args.TryGetValue("deltaTime",
                out object rawDelta) ? (float)VmAutomationVFXValueCodec.ConvertTo(
                    rawDelta, typeof(float), "deltaTime") : 1f / 60f;
            int stepCount = GetInt(args, "stepCount", 1);
            ValidateSimulationBounds(deltaTime, stepCount);
            Type countType = component.GetType().GetMethods(BindingFlags.Instance |
                    BindingFlags.Public | BindingFlags.NonPublic)
                .First(method => method.Name == "Simulate" &&
                                 method.GetParameters().Length >= 1)
                .GetParameters().ElementAtOrDefault(1)?.ParameterType;
            object count = countType == typeof(uint) ? (object)(uint)stepCount :
                stepCount;
            VmAutomationVFXReflection.Invoke(component, "Simulate", deltaTime, count);
        }

        internal static void ValidateSimulationBounds(float deltaTime,
            int stepCount)
        {
            if (float.IsNaN(deltaTime) || float.IsInfinity(deltaTime) ||
                deltaTime <= 0f || deltaTime > 10f || stepCount < 1 ||
                stepCount > 1024 || (double)deltaTime * stepCount > 60d)
                throw new ArgumentException(
                    "deltaTime must be finite and in (0, 10], stepCount in [1, 1024], and total simulated time must not exceed 60 seconds.");
        }

        private static void SendEvent(Component component,
            Dictionary<string, object> args)
        {
            string eventName = RequireString(args, "eventName");
            ValidateEventName(component, eventName);
            object attribute = VmAutomationVFXReflection.Invoke(component,
                "CreateVFXEventAttribute");
            try
            {
                List<object> values = args != null && args.TryGetValue(
                    "eventAttributes", out object raw)
                    ? VmAutomationVFXGraphMutationContext.AsList(raw) ??
                      throw new ArgumentException(
                          "eventAttributes must be an array.")
                    : new List<object>();
                if (values.Count > 64)
                    throw new ArgumentException(
                        "eventAttributes cannot contain more than 64 entries.");
                var names = new HashSet<string>(StringComparer.Ordinal);
                for (int index = 0; index < values.Count; index++)
                {
                    Dictionary<string, object> item =
                        VmAutomationVFXGraphMutationContext.AsDictionary(values[index]) ??
                        throw new ArgumentException(
                            $"eventAttributes[{index}] must be an object.");
                    string unknown = item.Keys.FirstOrDefault(key => key != "name" &&
                        key != "type" && key != "value");
                    if (unknown != null)
                        throw new ArgumentException(
                            $"eventAttributes[{index}] contains unsupported field '{unknown}'.");
                    string name = RequireString(item, "name");
                    if (!names.Add(name))
                        throw new ArgumentException(
                            $"eventAttributes[{index}].name '{name}' is duplicated.");
                    string type = RequireString(item, "type");
                    Type valueType = ResolveEventValueType(type);
                    object value = ConvertValue(Required(item,
                        "value"), valueType, $"eventAttributes[{index}].value");
                    string suffix = EventMethodSuffix(valueType);
                    if (!Convert.ToBoolean(VmAutomationVFXReflection.Invoke(attribute,
                            "Has" + suffix, name)))
                        throw VmAutomationVFXError.Create("property_not_found",
                            $"Event attribute '{name}' with type '{type}' is not declared by the assigned VisualEffectAsset.");
                    VmAutomationVFXReflection.Invoke(attribute, "Set" + suffix, name,
                        value);
                }
                VmAutomationVFXReflection.Invoke(component, "SendEvent", eventName,
                    attribute);
            }
            finally
            {
                if (attribute is IDisposable disposable)
                    disposable.Dispose();
            }
        }

        private static Type ResolveEventValueType(string name)
        {
            switch (name.ToLowerInvariant())
            {
                case "bool": return typeof(bool);
                case "int": return typeof(int);
                case "uint": return typeof(uint);
                case "float": return typeof(float);
                case "vector2": return typeof(Vector2);
                case "vector3": return typeof(Vector3);
                case "vector4": return typeof(Vector4);
                case "matrix4x4": return typeof(Matrix4x4);
                default: throw new ArgumentException(
                    $"Unsupported event attribute type '{name}'.");
            }
        }

        private static string EventMethodSuffix(Type type)
        {
            return type == typeof(uint) ? "Uint" : MethodSuffix(type);
        }

        private static void ValidateEventName(Component component,
            string eventName)
        {
            UnityEngine.Object asset = VmAutomationVFXReflection.Get(component,
                "visualEffectAsset") as UnityEngine.Object;
            if (asset == null)
                throw new InvalidOperationException(
                    "The VisualEffect component has no assigned asset.");
            string playEvent = VmAutomationVFXReflection.Get(asset.GetType(),
                "PlayEventName")?.ToString() ??
                throw new MissingMemberException(asset.GetType().FullName,
                    "PlayEventName");
            string stopEvent = VmAutomationVFXReflection.Get(asset.GetType(),
                "StopEventName")?.ToString() ??
                throw new MissingMemberException(asset.GetType().FullName,
                    "StopEventName");
            MethodInfo method = asset.GetType().GetMethods(BindingFlags.Instance |
                    BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(candidate => candidate.Name == "GetEvents" &&
                    candidate.GetParameters().Length == 1) ??
                throw new MissingMethodException(asset.GetType().FullName,
                    "GetEvents");
            object list = Activator.CreateInstance(
                method.GetParameters()[0].ParameterType);
            VmAutomationVFXReflection.InvokeMethod(method, asset, new[] { list });
            List<object> events = VmAutomationVFXReflection.Enumerate(list)
                .Take(VmAutomationVFXLimits.CollectionItems + 1).ToList();
            if (events.Count > VmAutomationVFXLimits.CollectionItems)
                throw VmAutomationVFXError.Create("response_too_large",
                    $"VisualEffectAsset '{asset.name}' exposes more than {VmAutomationVFXLimits.CollectionItems} events.");
            bool declared = string.Equals(eventName, playEvent,
                                StringComparison.Ordinal) ||
                            string.Equals(eventName, stopEvent,
                                StringComparison.Ordinal) ||
                            events.Any(value => string.Equals(value?.ToString(),
                                    eventName, StringComparison.Ordinal));
            if (!declared)
                throw VmAutomationVFXError.Create("event_not_found",
                    $"VFX event '{eventName}' is not declared by '{asset.name}'.");
        }

        private static List<Dictionary<string, object>> EnumeratePrefab(
            string prefabPath, bool includeOverrides, int overrideOffset,
            int maxOverrides, int? componentIndexFilter, int offset, int limit,
            ExposedPropertyCache propertyCache, out int total)
        {
            if (!VmAutomationVFXAssetPath.TryNormalizeFile(prefabPath, false,
                    out prefabPath, out string pathError) ||
                !prefabPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException(
                    pathError ?? "prefabPath must identify a .prefab below Assets/.");
            if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) == null)
                throw VmAutomationVFXError.Create("asset_not_found",
                    $"Prefab '{prefabPath}' was not found.");
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                Type type = VmAutomationVFXReflection.RequireType(
                    VmAutomationVFXReflection.VisualEffectTypeName);
                var indexCache = new ComponentIndexCache(type);
                List<Component> all = VmAutomationVFXComponentTarget
                    .EnumerateComponents(root, type)
                    .Where(component => !componentIndexFilter.HasValue ||
                        indexCache.Get(component) ==
                        componentIndexFilter.Value).ToList();
                total = all.Count;
                return all.Skip(offset).Take(limit)
                    .Select(component =>
                    {
                        return Summary(component, new Dictionary<string, object>
                        {
                            { "scope", "prefab" }, { "prefabPath", prefabPath },
                            { "scenePath", "" },
                            { "hierarchyPath", VmAutomationVFXComponentTarget.HierarchyPath(
                                component.gameObject) },
                            { "hierarchyIndexPath",
                                VmAutomationVFXComponentTarget.HierarchyIndexPath(
                                    component.gameObject) },
                            { "componentIndex", indexCache.Get(component) },
                            { "gameObjectInstanceId", VmObjectId.Get(
                                component.gameObject) },
                            { "componentInstanceId", VmObjectId.Get(component) },
                        }, includeOverrides, overrideOffset, maxOverrides,
                            propertyCache, false, 0, 100, 0, 100,
                            new VmAutomationVFXRuntimeState());
                    }).ToList();
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static Dictionary<string, object> LoadedIdentity(
            Component component, ComponentIndexCache indexCache)
        {
            return new Dictionary<string, object>
            {
                { "scope", "scene" }, { "prefabPath", "" },
                { "scenePath", component.gameObject.scene.path },
                { "hierarchyPath", VmAutomationVFXComponentTarget.HierarchyPath(
                    component.gameObject) },
                { "hierarchyIndexPath", VmAutomationVFXComponentTarget.HierarchyIndexPath(
                    component.gameObject) },
                { "componentIndex", indexCache.Get(component) },
                { "gameObjectInstanceId", VmObjectId.Get(component.gameObject) },
                { "componentInstanceId", VmObjectId.Get(component) },
            };
        }

        private static HashSet<string> PersistentComponentMembers()
        {
            return new HashSet<string>(new[]
            {
                "allowInstancing", "releaseInstanceWhenDisabled",
            }, StringComparer.Ordinal);
        }

        private static HashSet<string> PersistentRendererMembers()
        {
            return new HashSet<string>(new[]
            {
                "renderingLayerMask", "sortingLayerID", "sortingOrder",
                "rendererPriority", "lightProbeUsage",
                "reflectionProbeUsage", "lightProbeProxyVolumeOverride",
                "probeAnchor",
            }, StringComparer.Ordinal);
        }

        private static IEnumerable<string> PersistentComponentSummaryMembers()
        {
            yield return "startSeed";
            yield return "resetSeedOnPlay";
            yield return "initialEventName";
            foreach (string member in PersistentComponentMembers())
                yield return member;
        }

        private static Component RequireRenderer(Component component)
        {
            Type rendererType = VmAutomationVFXReflection.RequireType(
                VmAutomationVFXReflection.VisualEffectRendererTypeName);
            Component renderer = component.gameObject.GetComponent(rendererType);
            if (renderer == null)
                throw new InvalidOperationException(
                    $"VisualEffect component '{component.name}' has no required VFXRenderer companion component.");
            return renderer;
        }

        private static void SetRendererMember(VmAutomationVFXComponentTarget target,
            Component renderer, string member, object rawValue, string path)
        {
            Type type = VmAutomationVFXReflection.GetMemberType(renderer, member) ??
                        throw new MissingMemberException(
                            renderer.GetType().FullName, member);
            object converted = typeof(UnityEngine.Object).IsAssignableFrom(type)
                ? target.ResolveObjectReference(rawValue, type, path)
                : ConvertValue(rawValue, type, path);
            SetMember(renderer, member, converted, member);
        }

        private static void SetTypedMember(object target, string member,
            object rawValue, string path)
        {
            Type type = VmAutomationVFXReflection.GetMemberType(target, member) ??
                throw new MissingMemberException(target.GetType().FullName, member);
            SetMember(target, member,
                ConvertValue(rawValue, type, path), member);
        }

        private static object ConvertValue(object rawValue, Type type,
            string path)
        {
            try
            {
                return VmAutomationVFXValueCodec.ConvertTo(rawValue, type, path);
            }
            catch (VmAutomationVFXError.Failure)
            {
                throw;
            }
            catch (Exception exception) when (exception is ArgumentException ||
                                               exception is FormatException ||
                                               exception is OverflowException ||
                                               exception is InvalidCastException)
            {
                throw VmAutomationVFXError.Create("value_type_mismatch",
                    exception.Message);
            }
        }

        private static void SetMember(object target, string member, object value,
            string displayName)
        {
            if (!VmAutomationVFXReflection.TrySet(target, member, value))
                throw new MissingMemberException(target.GetType().FullName,
                    displayName);
        }

        private static void ValidateOperationKeys(
            Dictionary<string, object> operation, int index,
            params string[] allowed)
        {
            var set = new HashSet<string>(allowed, StringComparer.Ordinal);
            string unknown = operation.Keys.FirstOrDefault(key => !set.Contains(key));
            if (unknown != null)
                throw new ArgumentException(
                    $"operations[{index}] contains unsupported field '{unknown}'.");
        }

        private static bool HasExactSelector(Dictionary<string, object> args)
        {
            return !string.IsNullOrWhiteSpace(GetString(args, "hierarchyPath")) ||
                   !string.IsNullOrWhiteSpace(GetString(args,
                       "hierarchyIndexPath")) ||
                   !string.IsNullOrWhiteSpace(GetString(args,
                       "gameObjectInstanceId")) ||
                   !string.IsNullOrWhiteSpace(GetString(args,
                       "componentInstanceId"));
        }

        private static bool ValidateSelectorScope(Dictionary<string, object> args,
            out object error)
        {
            string prefabPath = GetString(args, "prefabPath");
            if (!string.IsNullOrEmpty(prefabPath) &&
                (!string.IsNullOrEmpty(GetString(args, "scenePath")) ||
                 !string.IsNullOrEmpty(GetString(args,
                     "gameObjectInstanceId")) ||
                 !string.IsNullOrEmpty(GetString(args,
                     "componentInstanceId"))))
            {
                error = VmAutomationResponse.Error(
                    "prefabPath cannot be combined with scenePath or loaded object instance selectors.",
                    "invalid_arguments");
                return false;
            }
            error = null;
            return true;
        }

        private static bool ValidateKeys(Dictionary<string, object> args,
            IEnumerable<string> allowed, out object error)
        {
            var set = new HashSet<string>(allowed, StringComparer.Ordinal);
            string unknown = args?.Keys.FirstOrDefault(key => !set.Contains(key));
            if (unknown == null)
            {
                error = null;
                return true;
            }
            error = VmAutomationResponse.Error($"Unsupported argument '{unknown}'.",
                "invalid_arguments");
            return false;
        }

        private static Dictionary<string, object> Result(string op, string key,
            object value)
        {
            return new Dictionary<string, object>
            {
                { "op", op }, { key, value },
            };
        }

        private static string GetString(Dictionary<string, object> args, string key,
            string defaultValue = "")
        {
            return args != null && args.TryGetValue(key, out object value) &&
                   value != null ? value.ToString() : defaultValue;
        }

        private static string RequireString(Dictionary<string, object> args,
            string key)
        {
            string value = GetString(args, key);
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException(key + " is required.");
            return value;
        }

        private static object Required(Dictionary<string, object> args, string key)
        {
            if (args == null || !args.TryGetValue(key, out object value))
                throw new ArgumentException(key + " is required.");
            return value;
        }

        private static int GetInt(Dictionary<string, object> args, string key,
            int defaultValue)
        {
            return args != null && args.TryGetValue(key, out object value) &&
                   value != null ? (int)VmAutomationVFXValueCodec.ConvertTo(value,
                       typeof(int), key) : defaultValue;
        }

        private static bool GetBool(Dictionary<string, object> args, string key,
            bool defaultValue)
        {
            return args != null && args.TryGetValue(key, out object value) &&
                   value != null ? (bool)VmAutomationVFXValueCodec.ConvertTo(value,
                       typeof(bool), key) : defaultValue;
        }
    }
}
