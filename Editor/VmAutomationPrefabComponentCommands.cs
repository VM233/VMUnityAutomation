using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using static VMUnityAutomation.Editor.VmAutomationPrefabCommandUtility;

namespace VMUnityAutomation.Editor
{
    public static class VmAutomationPrefabComponentCommands
    {
        private const string AddComponentWaitingForTypePhase = "waiting-for-type";
        private const string AddComponentMutationPreparedPhase = "mutation-prepared";

    public static object GetComponentProperties(Dictionary<string, object> args)
    {
        string assetPath = GetString(args, "assetPath");
        if (string.IsNullOrEmpty(assetPath))
            return new { error = "assetPath is required" };

        string prefabPath = GetString(args, "prefabPath");
        string componentType = GetString(args, "componentType");
        if (string.IsNullOrEmpty(componentType))
            return new { error = "componentType is required" };

        var root = PrefabUtility.LoadPrefabContents(assetPath);
        if (root == null)
            return new { error = $"Failed to load prefab at '{assetPath}'" };

        try
        {
            var go = FindInPrefab(root, prefabPath);
            if (go == null)
                return new { error = $"GameObject '{prefabPath}' not found in prefab" };

            Type type = VmAutomationComponentCommands.FindType(componentType);
            if (type == null)
                return new { error = $"Type '{componentType}' not found" };

            var component = go.GetComponent(type);
            if (component == null)
                return new { error = $"Component '{componentType}' not found on '{go.name}'" };

            var properties = new List<Dictionary<string, object>>();
            using (var serialized = new SerializedObject(component))
            {
                var iterator = serialized.GetIterator();
                if (iterator.NextVisible(true))
                {
                    do
                    {
                        properties.Add(new Dictionary<string, object>
                        {
                            { "name", iterator.name },
                            { "displayName", iterator.displayName },
                            { "type", iterator.propertyType.ToString() },
                            { "value", VmAutomationComponentCommands.GetSerializedValue(iterator) },
                            { "editable", iterator.editable },
                        });
                    } while (iterator.NextVisible(false));
                }
            }

            return new Dictionary<string, object>
            {
                { "prefab", root.name },
                { "gameObject", go.name },
                { "prefabPath", prefabPath ?? "" },
                { "component", componentType },
                { "properties", properties },
            };
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    /// <summary>
    /// Set a component property on a GameObject inside a prefab asset.
    /// </summary>
    public static object SetComponentProperty(Dictionary<string, object> args)
    {
        string assetPath = GetString(args, "assetPath");
        if (string.IsNullOrEmpty(assetPath))
            return new { error = "assetPath is required" };
        var beforeSnapshot = CaptureAssetText(assetPath);

        string prefabPath = GetString(args, "prefabPath");
        string componentType = GetString(args, "componentType");
        string propertyName = GetString(args, "propertyName");

        if (string.IsNullOrEmpty(componentType))
            return new { error = "componentType is required" };
        if (string.IsNullOrEmpty(propertyName))
            return new { error = "propertyName is required" };
        if (!args.ContainsKey("value"))
            return new { error = "value is required" };

        var root = PrefabUtility.LoadPrefabContents(assetPath);
        if (root == null)
            return new { error = $"Failed to load prefab at '{assetPath}'" };

        object expectedValue = null;
        bool hasExpectedValue = false;
        string prefabName = root.name;
        string gameObjectName = "";
        var saveWarnings = new List<string>();
        using (var session = new PrefabMutationSession(assetPath, beforeSnapshot, root))
        {
            try
            {
                var go = FindInPrefab(root, prefabPath);
                if (go == null)
                    return new { error = $"GameObject '{prefabPath}' not found in prefab" };
                gameObjectName = go.name;

                Type type = VmAutomationComponentCommands.FindType(componentType);
                if (type == null)
                    return new { error = $"Type '{componentType}' not found" };

                var component = go.GetComponent(type);
                if (component == null)
                    return new { error = $"Component '{componentType}' not found on '{go.name}'" };

                using (var serialized = new SerializedObject(component))
                {
                    var prop = serialized.FindProperty(propertyName);
                    if (prop == null)
                        return new { error = $"Property '{propertyName}' not found on '{componentType}'" };

                    VmAutomationComponentCommands.SetSerializedValue(prop, args["value"]);
                    serialized.ApplyModifiedProperties();
                    expectedValue = VmAutomationComponentCommands.GetSerializedValue(prop);
                    hasExpectedValue = true;
                }

                session.SaveAndClose(BuildExplicitYamlPropertyRoots(propertyName), saveWarnings);
                if (!TryVerifyPrefabProperty(assetPath, prefabPath, type, propertyName, expectedValue,
                        out object actualValue, out string verificationError))
                {
                    throw new InvalidOperationException(
                        $"Prefab save could not be verified by serialized readback: {verificationError}. " +
                        $"Expected {MiniJson.Serialize(expectedValue)}, read {MiniJson.Serialize(actualValue)}.");
                }

                var result = BuildSetPropertySuccess(prefabName, gameObjectName, componentType,
                    propertyName, saveWarnings, false, null);
                AddPrefabFileDiff(result, beforeSnapshot, assetPath, args);
                session.CommitVerifiedPublication();
                return result;
            }
            catch (Exception ex)
            {
                session.CloseAuthoringRoot();
                if (session.SaveAttempted && hasExpectedValue &&
                    TryVerifyPrefabProperty(assetPath, prefabPath,
                        VmAutomationComponentCommands.FindType(componentType), propertyName, expectedValue,
                        out _, out _))
                {
                    saveWarnings.Add(
                        $"The save path raised '{ex.GetBaseException().Message}', but serialized readback " +
                        "confirmed the requested value was persisted.");
                    var recovered = BuildSetPropertySuccess(prefabName, gameObjectName, componentType,
                        propertyName, saveWarnings, true, ex.GetBaseException().Message);
                    AddPrefabFileDiff(recovered, beforeSnapshot, assetPath, args);
                    session.CommitVerifiedPublication();
                    return recovered;
                }
                return new { error = $"Failed to set property: {ex.Message}" };
            }
        }
    }

    private static Dictionary<string, object> BuildSetPropertySuccess(string prefabName,
        string gameObjectName, string componentType, string propertyName, IList<string> warnings,
        bool recoveredFromSaveException, string saveException)
    {
        var result = new Dictionary<string, object>
        {
            { "success", true },
            { "prefab", prefabName },
            { "gameObject", gameObjectName },
            { "component", componentType },
            { "property", propertyName },
            { "persisted", true },
            { "persistenceVerifiedBy", "serialized-readback" },
            { "recoveredFromSaveException", recoveredFromSaveException },
        };
        if (!string.IsNullOrEmpty(saveException))
            result["saveException"] = saveException;
        if (warnings != null && warnings.Count > 0)
            result["warnings"] = warnings.ToArray();
        return result;
    }

    private static bool TryVerifyPrefabProperty(string assetPath, string prefabPath, Type componentType,
        string propertyName, object expectedValue, out object actualValue, out string error)
    {
        actualValue = null;
        error = "";
        if (componentType == null)
        {
            error = "component type could not be resolved";
            return false;
        }

        try
        {
            var prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (prefabRoot == null)
            {
                error = $"prefab '{assetPath}' could not be loaded";
                return false;
            }

            var gameObject = FindInPrefab(prefabRoot, prefabPath);
            if (gameObject == null)
            {
                error = $"GameObject '{prefabPath}' was not found";
                return false;
            }

            var component = gameObject.GetComponent(componentType);
            if (component == null)
            {
                error = $"component '{componentType.FullName}' was not found";
                return false;
            }

            using (var serialized = new SerializedObject(component))
            {
                serialized.Update();
                var property = serialized.FindProperty(propertyName);
                if (property == null)
                {
                    error = $"property '{propertyName}' was not found";
                    return false;
                }

                actualValue = VmAutomationComponentCommands.GetSerializedValue(property);
                if (MiniJson.Serialize(actualValue) == MiniJson.Serialize(expectedValue))
                    return true;
            }

            error = "serialized value did not match";
            return false;
        }
        catch (Exception ex)
        {
            error = ex.GetBaseException().Message;
            return false;
        }
    }

    // ─── Components ───

    /// <summary>
    /// Add a component to a GameObject inside a prefab asset.
    /// </summary>
    public static object AddComponent(Dictionary<string, object> args)
    {
        string assetPath = GetString(args, "assetPath");
        if (string.IsNullOrEmpty(assetPath))
            return new { error = "assetPath is required" };
        var beforeSnapshot = CaptureAssetText(assetPath);

        string prefabPath = GetString(args, "prefabPath");
        string componentType = GetString(args, "componentType");
        if (string.IsNullOrEmpty(componentType))
            return new { error = "componentType is required" };

        var root = PrefabUtility.LoadPrefabContents(assetPath);
        if (root == null)
            return new { error = $"Failed to load prefab at '{assetPath}'" };

        using (var session = new PrefabMutationSession(assetPath, beforeSnapshot, root))
        {
            try
            {
                if (!TryAddPrefabComponent(root, args, out var go, out var component,
                        out int componentIndex, out var changedProperties, out var expectedValues,
                        out string addError))
                {
                    return VmAutomationResponse.Error(
                        $"Failed to add component: {addError}",
                        "prefab_add_component_failed");
                }

                string prefabName = root.name;
                string gameObjectName = go.name;
                Type addedComponentType = component.GetType();

                if (session.SaveAndClose(
                        BuildExplicitYamlPropertyRoots(changedProperties.ToArray())) == null)
                {
                    throw new InvalidOperationException(
                        $"SaveAsPrefabAsset returned null for '{assetPath}'.");
                }

                if (!TryVerifyPrefabComponentConfiguration(assetPath, prefabPath,
                        addedComponentType, componentIndex, expectedValues,
                        out string verificationError))
                {
                    throw new InvalidOperationException(
                        $"Prefab save could not be verified by serialized readback: {verificationError}.");
                }

                var result = new Dictionary<string, object>
                {
                    { "success", true },
                    { "prefab", prefabName },
                    { "gameObject", gameObjectName },
                    { "component", addedComponentType.Name },
                    { "fullType", addedComponentType.FullName },
                    { "componentIndex", componentIndex },
                    { "configuredProperties", changedProperties },
                    { "configuredPropertyCount", changedProperties.Count },
                    { "persisted", true },
                    { "persistenceVerifiedBy", "serialized-readback" },
                };
                AddPrefabFileDiff(result, beforeSnapshot, assetPath, args);
                session.Commit();
                return result;
            }
            catch (Exception ex)
            {
                return VmAutomationResponse.Error(
                    $"Failed to add component: {ex.GetBaseException().Message}",
                    "prefab_add_component_failed");
            }
        }
    }

    public static void AddComponentDeferred(Dictionary<string, object> args, Action<object> resolve,
        Action<object> progress)
    {
        string componentType = GetString(args, "componentType");
        if (string.IsNullOrEmpty(componentType))
        {
            resolve(new { error = "componentType is required" });
            return;
        }

        if (GetBool(args, "waitForType", true) == false)
        {
            resolve(AddComponent(args));
            return;
        }

        int timeoutMs = Math.Max(1, GetInt(args, "typeResolveTimeoutMs", 30000));
        int stableMs = Math.Max(0, GetInt(args, "typeResolveStableMs", 500));
        bool refreshAssets = GetBool(args, "refreshAssets", true);
        string assetPath = GetString(args, "assetPath");
        string prefabPath = GetString(args, "prefabPath");
        var resumeProgress = GetDictionary(args, "_resumeProgress");
        string resumePhase = GetString(resumeProgress, "phase");
        int baselineComponentCount = GetInt(resumeProgress, "baselineComponentCount", -1);
        DateTime startedAtUtc = GetDateTime(resumeProgress, "startedAtUtc", DateTime.UtcNow);
        DateTime deadlineUtc = GetDateTime(resumeProgress, "deadlineUtc",
            startedAtUtc.AddMilliseconds(timeoutMs));
        double stableStartTime = -1;
        bool refreshRequested = resumePhase == AddComponentWaitingForTypePhase;

        EditorApplication.CallbackFunction tick = null;
        Action<object> complete = result =>
        {
            if (tick != null)
                EditorApplication.update -= tick;
            resolve(result);
        };

        tick = () =>
        {
            try
            {
                bool editorBusy = EditorApplication.isCompiling || EditorApplication.isUpdating;
                Type resolvedType = VmAutomationComponentCommands.FindType(componentType);

                if (resolvedType == null && editorBusy == false && refreshAssets &&
                    refreshRequested == false)
                {
                    refreshRequested = true;
                    progress?.Invoke(BuildAddComponentProgress(AddComponentWaitingForTypePhase,
                        assetPath, prefabPath, componentType, -1, startedAtUtc, deadlineUtc));
                    AssetDatabase.Refresh();
                    editorBusy = EditorApplication.isCompiling || EditorApplication.isUpdating;
                    resolvedType = VmAutomationComponentCommands.FindType(componentType);
                }

                if (resolvedType != null && editorBusy == false)
                {
                    if (stableStartTime < 0)
                        stableStartTime = EditorApplication.timeSinceStartup;

                    double stableElapsedMs = (EditorApplication.timeSinceStartup - stableStartTime) * 1000d;
                    if (stableElapsedMs >= stableMs)
                    {
                        if (resumePhase == AddComponentMutationPreparedPhase)
                        {
                            if (!TryGetPrefabComponentCount(assetPath, prefabPath, resolvedType,
                                    out int persistedCount, out string prefabName,
                                    out string gameObjectName, out string reconciliationError))
                            {
                                complete(VmAutomationResponse.Error(reconciliationError,
                                    "prefab_add_component_reconciliation_failed"));
                                return;
                            }

                            if (persistedCount == baselineComponentCount + 1)
                            {
                                if (!TryEnsurePrefabComponentConfiguration(assetPath, prefabPath,
                                        resolvedType, baselineComponentCount, args,
                                        out var configuredProperties, out string configurationError))
                                {
                                    complete(VmAutomationResponse.Error(
                                        $"Could not reconcile the saved prefab component after " +
                                        $"Domain Reload: {configurationError}",
                                        "prefab_add_component_reconciliation_failed"));
                                    return;
                                }

                                complete(BuildReconciledAddComponentResult(args, prefabName,
                                    gameObjectName, resolvedType, baselineComponentCount,
                                    persistedCount, configuredProperties));
                                return;
                            }

                            if (persistedCount != baselineComponentCount)
                            {
                                complete(VmAutomationResponse.Error(
                                    $"Cannot reconcile prefab component count after Domain Reload. " +
                                    $"Expected {baselineComponentCount} or {baselineComponentCount + 1}, " +
                                    $"found {persistedCount}.",
                                    "prefab_add_component_reconciliation_conflict", false,
                                    new Dictionary<string, object>
                                    {
                                        { "assetPath", assetPath },
                                        { "prefabPath", prefabPath ?? "" },
                                        { "componentType", componentType },
                                        { "baselineComponentCount", baselineComponentCount },
                                        { "persistedComponentCount", persistedCount },
                                    }));
                                return;
                            }
                        }
                        else if (!TryGetPrefabComponentCount(assetPath, prefabPath, resolvedType,
                                     out baselineComponentCount, out _, out _,
                                     out string preflightError))
                        {
                            complete(VmAutomationResponse.Error(preflightError,
                                "prefab_add_component_preflight_failed"));
                            return;
                        }

                        progress?.Invoke(BuildAddComponentProgress(AddComponentMutationPreparedPhase,
                            assetPath, prefabPath, componentType, baselineComponentCount,
                            startedAtUtc, deadlineUtc));
                        object result = AddComponent(args);
                        if (result is Dictionary<string, object> resultDictionary &&
                            resultDictionary.TryGetValue("success", out object successValue) &&
                            successValue is bool succeeded && succeeded)
                        {
                            resultDictionary["componentCountBefore"] = baselineComponentCount;
                            resultDictionary["componentCountAfter"] = baselineComponentCount + 1;
                            resultDictionary["reconciledAfterReload"] = false;
                        }
                        complete(result);
                        return;
                    }
                }
                else
                {
                    stableStartTime = -1;
                }

                double elapsedMs = Math.Max(0d, (DateTime.UtcNow - startedAtUtc).TotalMilliseconds);
                if (DateTime.UtcNow >= deadlineUtc)
                {
                    complete(new Dictionary<string, object>
                    {
                        { "error", $"Type '{componentType}' not found after waiting {timeoutMs} ms" },
                        { "typeResolution", new Dictionary<string, object>
                            {
                                { "componentType", componentType },
                                { "elapsedMs", (int)elapsedMs },
                                { "timeoutMs", timeoutMs },
                                { "refreshedAssets", refreshRequested },
                                { "isCompiling", EditorApplication.isCompiling },
                                { "isUpdating", EditorApplication.isUpdating },
                                { "likelyReason", EditorApplication.isCompiling || EditorApplication.isUpdating ? "unity_busy" : "type_not_found" },
                            }
                        },
                    });
                }
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                complete(VmAutomationResponse.Error(
                    $"Failed while waiting for component type: {ex.Message}",
                    "component_type_wait_failed"));
            }
        };

        EditorApplication.update += tick;
        tick();
    }

    internal static bool CanResumeAddComponentAfterReload(object progress)
    {
        if (progress == null)
            return true;
        if (!(progress is Dictionary<string, object> state))
            return false;

        string phase = GetString(state, "phase");
        if (phase == AddComponentWaitingForTypePhase)
            return true;
        return phase == AddComponentMutationPreparedPhase &&
               GetInt(state, "baselineComponentCount", -1) >= 0;
    }

    private static Dictionary<string, object> BuildAddComponentProgress(string phase,
        string assetPath, string prefabPath, string componentType, int baselineComponentCount,
        DateTime startedAtUtc, DateTime deadlineUtc)
    {
        var state = new Dictionary<string, object>
        {
            { "phase", phase },
            { "assetPath", assetPath ?? "" },
            { "prefabPath", prefabPath ?? "" },
            { "componentType", componentType ?? "" },
            { "startedAtUtc", startedAtUtc.ToString("O") },
            { "deadlineUtc", deadlineUtc.ToString("O") },
        };
        if (baselineComponentCount >= 0)
            state["baselineComponentCount"] = baselineComponentCount;
        return state;
    }

    private static bool TryGetPrefabComponentCount(string assetPath, string prefabPath, Type componentType,
        out int count, out string prefabName, out string gameObjectName, out string error)
    {
        count = -1;
        prefabName = "";
        gameObjectName = "";
        error = "";
        if (string.IsNullOrEmpty(assetPath))
        {
            error = "assetPath is required";
            return false;
        }

        GameObject root = null;
        try
        {
            root = PrefabUtility.LoadPrefabContents(assetPath);
            if (root == null)
            {
                error = $"Failed to load prefab at '{assetPath}'";
                return false;
            }

            var gameObject = FindInPrefab(root, prefabPath);
            if (gameObject == null)
            {
                error = $"GameObject '{prefabPath}' not found in prefab";
                return false;
            }

            prefabName = root.name;
            gameObjectName = gameObject.name;
            count = gameObject.GetComponents(componentType).Length;
            return true;
        }
        catch (Exception ex)
        {
            error = ex.GetBaseException().Message;
            return false;
        }
        finally
        {
            if (root != null)
                PrefabUtility.UnloadPrefabContents(root);
        }
    }

    private static Dictionary<string, object> BuildReconciledAddComponentResult(
        Dictionary<string, object> args, string prefabName, string gameObjectName, Type componentType,
        int componentCountBefore, int componentCountAfter, List<string> configuredProperties)
    {
        configuredProperties ??= new List<string>();
        var result = new Dictionary<string, object>
        {
            { "success", true },
            { "prefab", prefabName },
            { "assetPath", GetString(args, "assetPath") },
            { "gameObject", gameObjectName },
            { "prefabPath", GetString(args, "prefabPath") },
            { "component", componentType.Name },
            { "fullType", componentType.FullName },
            { "componentIndex", componentCountBefore },
            { "configuredProperties", configuredProperties },
            { "configuredPropertyCount", configuredProperties.Count },
            { "persisted", true },
            { "persistenceVerifiedBy", "serialized-readback" },
            { "componentCountBefore", componentCountBefore },
            { "componentCountAfter", componentCountAfter },
            { "reconciledAfterReload", true },
            { "resumeCount", GetInt(args, "_resumeCount", 1) },
        };
        if (GetBool(args, "includePrefabFileDiff",
                VmAutomationSettings.IncludePrefabFileDiffByDefault))
            result["prefabFileDiffUnavailable"] = "reconciled-after-domain-reload";
        return result;
    }

    private static bool TryEnsurePrefabComponentConfiguration(string assetPath,
        string prefabPath, Type componentType, int componentIndex,
        Dictionary<string, object> args, out List<string> configuredProperties,
        out string error)
    {
        configuredProperties = new List<string>();
        error = "";
        var properties = GetDictionary(args, "properties");
        if (properties == null || properties.Count == 0)
        {
            return TryVerifyPrefabComponentConfiguration(assetPath, prefabPath,
                componentType, componentIndex, new Dictionary<string, object>(), out error);
        }

        var beforeSnapshot = CaptureAssetText(assetPath);
        GameObject root;
        try
        {
            root = PrefabUtility.LoadPrefabContents(assetPath);
        }
        catch (Exception ex)
        {
            error = ex.GetBaseException().Message;
            return false;
        }

        if (root == null)
        {
            error = $"Failed to load prefab at '{assetPath}'";
            return false;
        }

        using (var session = new PrefabMutationSession(assetPath, beforeSnapshot, root))
        {
            try
            {
                var gameObject = FindInPrefab(root, prefabPath);
                if (gameObject == null)
                {
                    error = $"GameObject '{prefabPath}' not found in prefab";
                    return false;
                }

                var components = gameObject.GetComponents(componentType);
                if (componentIndex < 0 || componentIndex >= components.Length)
                {
                    error = $"Component '{componentType.FullName}' at index {componentIndex} " +
                            $"was not found; persisted count is {components.Length}";
                    return false;
                }

                var component = components[componentIndex];
                if (!TryApplySerializedProperties(component, properties, configuredProperties,
                        out error))
                {
                    return false;
                }

                var expectedValues = new Dictionary<string, object>();
                if (!TryCaptureSerializedProperties(component, configuredProperties,
                        expectedValues, out error))
                {
                    return false;
                }

                if (session.SaveAndClose(
                        BuildExplicitYamlPropertyRoots(configuredProperties.ToArray())) == null)
                {
                    error = $"SaveAsPrefabAsset returned null for '{assetPath}'";
                    return false;
                }

                if (!TryVerifyPrefabComponentConfiguration(assetPath, prefabPath,
                        componentType, componentIndex, expectedValues, out error))
                {
                    return false;
                }

                session.Commit();
                return true;
            }
            catch (Exception ex)
            {
                error = ex.GetBaseException().Message;
                return false;
            }
        }
    }

    /// <summary>
    /// Remove a component from a GameObject inside a prefab asset.
    /// </summary>
    public static object RemoveComponent(Dictionary<string, object> args)
    {
        string assetPath = GetString(args, "assetPath");
        if (string.IsNullOrEmpty(assetPath))
            return new { error = "assetPath is required" };
        var beforeSnapshot = CaptureAssetText(assetPath);

        string prefabPath = GetString(args, "prefabPath");
        string componentType = GetString(args, "componentType");
        if (string.IsNullOrEmpty(componentType))
            return new { error = "componentType is required" };

        int index = GetInt(args, "componentIndex", 0);

        var root = PrefabUtility.LoadPrefabContents(assetPath);
        if (root == null)
            return new { error = $"Failed to load prefab at '{assetPath}'" };

        using (var session = new PrefabMutationSession(assetPath, beforeSnapshot, root))
        {
            try
            {
                var go = FindInPrefab(root, prefabPath);
                if (go == null)
                    return new { error = $"GameObject '{prefabPath}' not found in prefab" };

                Type type = VmAutomationComponentCommands.FindType(componentType);
                if (type == null)
                    return new { error = $"Type '{componentType}' not found" };

                var components = go.GetComponents(type);
                if (components == null || index >= components.Length)
                    return new { error = $"Component '{componentType}' at index {index} not found on '{go.name}'" };

                string prefabName = root.name;
                string gameObjectName = go.name;
                UnityEngine.Object.DestroyImmediate(components[index]);
                if (session.SaveAndClose() == null)
                    throw new InvalidOperationException(
                        $"SaveAsPrefabAsset returned null for '{assetPath}'.");

                var result = new Dictionary<string, object>
                {
                    { "success", true },
                    { "prefab", prefabName },
                    { "gameObject", gameObjectName },
                    { "removedComponent", componentType },
                    { "index", index },
                };
                AddPrefabFileDiff(result, beforeSnapshot, assetPath, args);
                session.Commit();
                return result;
            }
            catch (Exception ex)
            {
                return new { error = $"Failed to remove component: {ex.Message}" };
            }
        }
    }

    /// <summary>
    /// Move a component between GameObjects inside one prefab asset in a single load/save transaction.
    /// </summary>
    public static object MoveComponent(Dictionary<string, object> args)
    {
        string assetPath = GetString(args, "assetPath");
        if (string.IsNullOrEmpty(assetPath))
            return new { error = "assetPath is required" };

        string sourcePrefabPath = GetString(args, "sourcePrefabPath");
        string targetPrefabPath = GetString(args, "targetPrefabPath");
        string componentType = GetString(args, "componentType");
        if (string.IsNullOrEmpty(componentType))
            return new { error = "componentType is required" };

        int componentIndex = GetInt(args, "componentIndex", 0);
        if (componentIndex < 0)
            return new { error = "componentIndex must be zero or greater" };

        var beforeSnapshot = CaptureAssetText(assetPath);
        var root = PrefabUtility.LoadPrefabContents(assetPath);
        if (root == null)
            return new { error = $"Failed to load prefab at '{assetPath}'" };

        using (var session = new PrefabMutationSession(assetPath, beforeSnapshot, root))
        {
            try
            {
                var source = FindInPrefab(root, sourcePrefabPath);
                if (source == null)
                    return new { error = $"Source GameObject '{sourcePrefabPath}' not found in prefab" };

                var target = FindInPrefab(root, targetPrefabPath);
                if (target == null)
                    return new { error = $"Target GameObject '{targetPrefabPath}' not found in prefab" };
                if (source == target)
                    return new { error = "Source and target GameObjects must be different" };

                Type type = VmAutomationComponentCommands.FindType(componentType);
                if (type == null)
                    return new { error = $"Type '{componentType}' not found" };
                if (typeof(Transform).IsAssignableFrom(type))
                    return new { error = "Transform components cannot be moved" };

                var sourceComponents = source.GetComponents(type);
                if (componentIndex >= sourceComponents.Length)
                {
                    return new
                    {
                        error = $"Component '{componentType}' at index {componentIndex} not found on '{source.name}'"
                    };
                }

                var sourceComponent = sourceComponents[componentIndex];
                Type movedComponentType = sourceComponent.GetType();
                int targetComponentCount = target.GetComponents(type).Length;
                if (!UnityEditorInternal.ComponentUtility.CopyComponent(sourceComponent) ||
                    !UnityEditorInternal.ComponentUtility.PasteComponentAsNew(target))
                {
                    return new { error = $"Failed to copy component '{componentType}' to '{target.name}'" };
                }

                var targetComponents = target.GetComponents(type);
                if (targetComponents.Length != targetComponentCount + 1)
                {
                    return new { error = $"Component '{componentType}' was not added exactly once to '{target.name}'" };
                }

                var movedComponent = targetComponents[targetComponentCount];
                int remappedReferenceCount = RemapComponentReferences(root, sourceComponent, movedComponent);

                UnityEngine.Object.DestroyImmediate(sourceComponent);
                if (source.GetComponents(type).Length != sourceComponents.Length - 1)
                    return new { error = $"Failed to remove component '{componentType}' from '{source.name}'" };

                string prefabName = root.name;
                string resolvedSourcePath = GetPrefabPath(root, source);
                string resolvedTargetPath = GetPrefabPath(root, target);
                if (session.SaveAndClose() == null)
                    throw new InvalidOperationException($"SaveAsPrefabAsset returned null for '{assetPath}'.");

                var result = new Dictionary<string, object>
                {
                    { "success", true },
                    { "prefab", prefabName },
                    { "assetPath", assetPath },
                    { "sourcePrefabPath", resolvedSourcePath },
                    { "targetPrefabPath", resolvedTargetPath },
                    { "component", movedComponentType.Name },
                    { "fullType", movedComponentType.FullName },
                    { "componentIndex", componentIndex },
                    { "remappedReferenceCount", remappedReferenceCount },
                };
                AddPrefabFileDiff(result, beforeSnapshot, assetPath, args);
                session.Commit();
                return result;
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                return VmAutomationResponse.Error($"Failed to move component: {ex.Message}", "move_component_failed");
            }
        }
    }

    private static int RemapComponentReferences(GameObject prefabRoot, Component sourceComponent,
        Component movedComponent)
    {
        int remappedReferenceCount = 0;
        foreach (var owner in prefabRoot.GetComponentsInChildren<Component>(true))
        {
            if (owner == null || owner == sourceComponent)
                continue;

            using (var serializedObject = new SerializedObject(owner))
            {
                serializedObject.UpdateIfRequiredOrScript();
                var property = serializedObject.GetIterator();
                bool changed = false;

                while (property.Next(true))
                {
                    if (property.propertyType == SerializedPropertyType.ObjectReference &&
                        property.objectReferenceValue == sourceComponent)
                    {
                        property.objectReferenceValue = movedComponent;
                        remappedReferenceCount++;
                        changed = true;
                    }
                    else if (property.propertyType == SerializedPropertyType.ExposedReference &&
                             property.exposedReferenceValue == sourceComponent)
                    {
                        property.exposedReferenceValue = movedComponent;
                        remappedReferenceCount++;
                        changed = true;
                    }
                }

                if (changed)
                    serializedObject.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        return remappedReferenceCount;
    }

    // ─── Reference Wiring ───

    /// <summary>
    /// Wire an ObjectReference property on a component inside a prefab asset.
    /// Supports references to assets (by path) and to other GameObjects within the same prefab.
    /// </summary>
    public static object SetReference(Dictionary<string, object> args)
    {
        string assetPath = GetString(args, "assetPath");
        if (string.IsNullOrEmpty(assetPath))
            return new { error = "assetPath is required" };
        var beforeSnapshot = CaptureAssetText(assetPath);

        string prefabPath = GetString(args, "prefabPath");
        string componentType = GetString(args, "componentType");
        string propertyName = GetString(args, "propertyName");
        if (string.IsNullOrEmpty(propertyName))
            return new { error = "propertyName is required" };

        string referenceAssetPath = GetString(args, "referenceAssetPath");
        string referenceSubAssetName = GetString(args, "referenceSubAssetName");
        string referenceSubAssetLocalId = GetString(args, "referenceSubAssetLocalId");
        string referencePrefabPath = GetString(args, "referencePrefabPath");
        bool hasReferencePrefabPath = args.ContainsKey("referencePrefabPath");
        string referenceComponentType = GetString(args, "referenceComponentType");
        bool clearRef = args.ContainsKey("clear") && Convert.ToBoolean(args["clear"]);

        var root = PrefabUtility.LoadPrefabContents(assetPath);
        if (root == null)
            return new { error = $"Failed to load prefab at '{assetPath}'" };

        using (var session = new PrefabMutationSession(assetPath, beforeSnapshot, root))
        {
            try
            {
                var go = FindInPrefab(root, prefabPath);
                if (go == null)
                    return new { error = $"GameObject '{prefabPath}' not found in prefab" };

                // Find component (auto-search if componentType not specified)
                Component component = null;
                if (!string.IsNullOrEmpty(componentType))
                {
                    Type type = VmAutomationComponentCommands.FindType(componentType);
                    if (type != null) component = go.GetComponent(type);
                }
                else
                {
                    foreach (var comp in go.GetComponents<Component>())
                    {
                        if (comp == null) continue;
                        using (var candidate = new SerializedObject(comp))
                        {
                            if (candidate.FindProperty(propertyName) != null)
                            {
                                component = comp;
                                break;
                            }
                        }
                    }
                }

                if (component == null)
                    return new { error = $"Component '{componentType}' not found on '{go.name}', or no component has property '{propertyName}'" };

                string refDescription = "null (cleared)";
                using (var serialized = new SerializedObject(component))
                {
                    var prop = serialized.FindProperty(propertyName);
                    if (prop == null)
                        return new { error = $"Property '{propertyName}' not found" };

                    if (prop.propertyType != SerializedPropertyType.ObjectReference)
                        return new { error = $"Property '{propertyName}' is not an ObjectReference (type: {prop.propertyType})" };

                    UnityEngine.Object targetRef = null;
                    if (clearRef)
                    {
                        prop.objectReferenceValue = null;
                    }
                    else if (!string.IsNullOrEmpty(referenceAssetPath))
                    {
                        if (!TryResolveAssetReference(prop, referenceAssetPath, referenceSubAssetName,
                                referenceSubAssetLocalId, out targetRef, out string error))
                            return new { error };

                        refDescription = $"{targetRef.name} ({targetRef.GetType().Name})";
                    }
                    else if (hasReferencePrefabPath)
                    {
                        var refGo = FindInPrefab(root, referencePrefabPath);
                        if (refGo == null)
                            return new { error = $"GameObject '{referencePrefabPath}' not found in prefab" };

                        if (!string.IsNullOrEmpty(referenceComponentType))
                        {
                            Type refType = VmAutomationComponentCommands.FindType(referenceComponentType);
                            if (refType == null)
                                return new { error = $"Type '{referenceComponentType}' not found" };

                            targetRef = refGo.GetComponent(refType);
                            if (targetRef == null)
                                return new { error = $"Component '{referenceComponentType}' not found on '{refGo.name}'" };
                        }
                        else
                        {
                            targetRef = refGo;
                        }

                        prop.objectReferenceValue = targetRef;
                        refDescription = $"{targetRef.name} ({targetRef.GetType().Name})";
                    }
                    else
                    {
                        return new { error = "Provide referenceAssetPath, referencePrefabPath, or clear=true" };
                    }

                    serialized.ApplyModifiedProperties();
                }

                string prefabName = root.name;
                string gameObjectName = go.name;
                string resolvedComponentType = component.GetType().Name;
                if (session.SaveAndClose(BuildExplicitYamlPropertyRoots(propertyName)) == null)
                    throw new InvalidOperationException(
                        $"SaveAsPrefabAsset returned null for '{assetPath}'.");

                var result = new Dictionary<string, object>
                {
                    { "success", true },
                    { "prefab", prefabName },
                    { "gameObject", gameObjectName },
                    { "component", resolvedComponentType },
                    { "property", propertyName },
                    { "reference", refDescription },
                };
                AddPrefabFileDiff(result, beforeSnapshot, assetPath, args);
                session.Commit();
                return result;
            }
            catch (Exception ex)
            {
                return new { error = $"Failed to set reference: {ex.Message}" };
            }
        }
    }

    // ─── Hierarchy Modification ───

    /// <summary>
    /// Create a new child GameObject inside a prefab asset.
    /// </summary>

    }
}
