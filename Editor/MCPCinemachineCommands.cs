using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace VMUnityAutomation.Editor
{
    internal static class MCPCinemachineCommands
    {
        public static object Info(Dictionary<string, object> args)
        {
            if (!MCPCapabilityRegistry.IsCapabilityAvailable("cinemachine"))
                return MCPResponse.Error(
                    "Cinemachine is unavailable. Install com.unity.cinemachine.",
                    "capability_unavailable");

            string assetPath = GetString(args, "assetPath");
            bool includeProperties = GetBool(args, "includeProperties", false);
            int maxProperties = Math.Max(1, Math.Min(200,
                GetInt(args, "maxProperties", 60)));
            int offset = Math.Max(0, GetInt(args, "offset", 0));
            int limit = Math.Max(1, Math.Min(500,
                GetInt(args, "limit", 100)));
            GameObject prefabRoot = null;
            try
            {
                IEnumerable<Component> components;
                if (!string.IsNullOrEmpty(assetPath))
                {
                    if (AssetDatabase.LoadAssetAtPath<GameObject>(assetPath) == null)
                        return MCPResponse.Error(
                            $"Prefab '{assetPath}' was not found.",
                            "asset_not_found");
                    prefabRoot = PrefabUtility.LoadPrefabContents(assetPath);
                    components = prefabRoot.GetComponentsInChildren<Component>(true);
                }
                else
                {
                    components = Resources.FindObjectsOfTypeAll<Component>()
                        .Where(component => component != null &&
                                            component.gameObject.scene.IsValid() &&
                                            component.gameObject.scene.isLoaded &&
                                            !EditorUtility.IsPersistent(component));
                }

                List<Component> cinemachine = components.Where(IsCinemachineComponent)
                    .OrderBy(component => component.gameObject.scene.path)
                    .ThenBy(component => MCPGameObjectCommands.GetHierarchyPath(
                        component.gameObject))
                    .ThenBy(component => component.GetType().FullName)
                    .ToList();
                List<Component> page = cinemachine.Skip(offset).Take(limit).ToList();
                return new Dictionary<string, object>
                {
                    { "success", true },
                    { "assetPath", assetPath },
                    { "scope", string.IsNullOrEmpty(assetPath) ? "loaded-scenes" : "prefab" },
                    { "componentCount", cinemachine.Count },
                    { "offset", offset },
                    { "limit", limit },
                    { "hasMore", offset + page.Count < cinemachine.Count },
                    { "nextOffset", offset + page.Count < cinemachine.Count
                        ? (object)(offset + page.Count)
                        : null },
                    { "components", page.Select(component =>
                        ComponentInfo(component, includeProperties, maxProperties)).ToList() },
                };
            }
            finally
            {
                if (prefabRoot != null)
                    PrefabUtility.UnloadPrefabContents(prefabRoot);
            }
        }

        public static object Transaction(Dictionary<string, object> args)
        {
            if (!MCPCapabilityRegistry.IsCapabilityAvailable("cinemachine"))
                return MCPResponse.Error(
                    "Cinemachine is unavailable. Install com.unity.cinemachine.",
                    "capability_unavailable");
            List<object> rawOperations = GetList(args, "operations");
            if (rawOperations == null || rawOperations.Count == 0)
                return MCPResponse.Error("operations must contain at least one operation.",
                    "invalid_arguments");

            string assetPath = GetString(args, "assetPath");
            GameObject prefabRoot = null;
            int undoGroup = -1;
            bool savedPrefab = false;
            try
            {
                if (!string.IsNullOrEmpty(assetPath))
                {
                    if (AssetDatabase.LoadAssetAtPath<GameObject>(assetPath) == null)
                        return MCPResponse.Error(
                            $"Prefab '{assetPath}' was not found.",
                            "asset_not_found");
                    prefabRoot = PrefabUtility.LoadPrefabContents(assetPath);
                }

                var prepared = new List<PreparedOperation>();
                for (int index = 0; index < rawOperations.Count; index++)
                {
                    if (!(rawOperations[index] is Dictionary<string, object> operation))
                        return MCPResponse.Error(
                            $"operations[{index}] must be an object.",
                            "invalid_arguments");
                    PreparedOperation item = Prepare(index, operation, prefabRoot);
                    if (item.Error != null)
                        return item.Error;
                    prepared.Add(item);
                }

                bool dryRun = GetBool(args, "dryRun", false);
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

                undoGroup = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("VM Unity Automation Edit Cinemachine");
                var results = new List<Dictionary<string, object>>();
                foreach (PreparedOperation operation in prepared)
                    results.Add(operation.Apply());

                if (prefabRoot != null)
                {
                    PrefabUtility.SaveAsPrefabAsset(prefabRoot, assetPath);
                    savedPrefab = true;
                }
                else
                {
                    foreach (var scene in prepared.Select(item => item.Component.gameObject.scene)
                                 .Where(scene => scene.IsValid()).Distinct())
                        EditorSceneManager.MarkSceneDirty(scene);
                }
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
                if (undoGroup >= 0 && prefabRoot == null)
                    Undo.RevertAllDownToGroup(undoGroup);
                return MCPResponse.Error(exception.GetBaseException().Message,
                    "cinemachine_transaction_failed");
            }
            finally
            {
                if (prefabRoot != null)
                {
                    PrefabUtility.UnloadPrefabContents(prefabRoot);
                    if (savedPrefab)
                        AssetDatabase.SaveAssets();
                }
            }
        }

        private static PreparedOperation Prepare(int index,
            Dictionary<string, object> operation, GameObject prefabRoot)
        {
            string action = GetString(operation, "action").ToLowerInvariant();
            if (action != "set-property" && action != "set-object-reference" &&
                action != "set-enabled")
            {
                return PreparedOperation.WithError(MCPResponse.Error(
                    $"operations[{index}].action must be set-property, set-object-reference, or set-enabled.",
                    "invalid_arguments"));
            }
            try
            {
                ValidateOperationKeys(operation, action);
            }
            catch (Exception exception)
            {
                return PreparedOperation.WithError(MCPResponse.Error(
                    exception.Message, "invalid_arguments"));
            }

            GameObject gameObject = ResolveGameObject(operation, prefabRoot);
            if (gameObject == null)
            {
                return PreparedOperation.WithError(MCPResponse.Error(
                    $"operations[{index}] GameObject was not found.",
                    "gameobject_not_found"));
            }
            Component component = ResolveComponent(gameObject,
                GetString(operation, "componentType"),
                GetInt(operation, "componentIndex", 0));
            if (component == null || !IsCinemachineComponent(component))
            {
                return PreparedOperation.WithError(MCPResponse.Error(
                    $"operations[{index}] Cinemachine component '{GetString(operation, "componentType")}' was not found on '{MCPGameObjectCommands.GetHierarchyPath(gameObject)}'.",
                    "component_not_found"));
            }

            if (action == "set-enabled")
            {
                if (!(component is Behaviour))
                {
                    return PreparedOperation.WithError(MCPResponse.Error(
                        $"operations[{index}] component is not a Behaviour.",
                        "invalid_arguments"));
                }
                if (!operation.TryGetValue("enabled", out object enabled))
                {
                    return PreparedOperation.WithError(MCPResponse.Error(
                        $"operations[{index}].enabled is required.",
                        "invalid_arguments"));
                }
                return new PreparedOperation(index, action, component, "",
                    Convert.ToBoolean(enabled), null, null);
            }

            string propertyPath = GetString(operation, "propertyPath");
            if (string.IsNullOrEmpty(propertyPath))
            {
                return PreparedOperation.WithError(MCPResponse.Error(
                    $"operations[{index}].propertyPath is required.",
                    "invalid_arguments"));
            }
            var serialized = new SerializedObject(component);
            SerializedProperty property = serialized.FindProperty(propertyPath);
            if (property == null)
            {
                return PreparedOperation.WithError(MCPResponse.Error(
                    $"operations[{index}] property '{propertyPath}' was not found.",
                    "property_not_found"));
            }
            object beforeValue =
                MCPComponentCommands.GetSerializedValue(property, 2, 32);

            object value;
            UnityEngine.Object reference = null;
            if (action == "set-object-reference")
            {
                if (property.propertyType != SerializedPropertyType.ObjectReference)
                {
                    return PreparedOperation.WithError(MCPResponse.Error(
                        $"operations[{index}] property '{propertyPath}' is not an object reference.",
                        "invalid_arguments"));
                }
                if (GetBool(operation, "clear", false))
                {
                    value = null;
                }
                else
                {
                    var targetSelector = operation.TryGetValue("target", out object targetValue)
                        ? targetValue as Dictionary<string, object>
                        : null;
                    GameObject target = ResolveGameObject(targetSelector, prefabRoot);
                    if (target == null)
                    {
                        return PreparedOperation.WithError(MCPResponse.Error(
                            $"operations[{index}] target GameObject was not found.",
                            "gameobject_not_found"));
                    }
                    string targetKind = GetString(operation, "targetKind").ToLowerInvariant();
                    reference = ResolveTargetReference(target, targetKind,
                        GetString(operation, "targetComponentType"),
                        GetInt(operation, "targetComponentIndex", 0));
                    value = reference;
                }
                property.objectReferenceValue = reference;
            }
            else if (!operation.TryGetValue("value", out value))
            {
                return PreparedOperation.WithError(MCPResponse.Error(
                    $"operations[{index}].value is required.",
                    "invalid_arguments"));
            }
            else
            {
                MCPComponentCommands.SetSerializedValue(property, value);
            }

            return new PreparedOperation(index, action, component, propertyPath, value,
                beforeValue, reference);
        }

        private static GameObject ResolveGameObject(Dictionary<string, object> selector,
            GameObject prefabRoot)
        {
            if (selector == null)
                return null;
            if (prefabRoot == null)
            {
                if (selector.TryGetValue("instanceId", out object instanceId))
                    return VmObjectId.ToObject(instanceId) as GameObject;
                string scenePath = GetString(selector, "scenePath");
                string sceneObjectPath = GetString(selector, "path");
                if (string.IsNullOrEmpty(sceneObjectPath))
                    sceneObjectPath = GetString(selector, "gameObjectPath");
                if (string.IsNullOrEmpty(sceneObjectPath))
                    return null;
                List<GameObject> sceneMatches = Resources.FindObjectsOfTypeAll<GameObject>()
                    .Where(gameObject => gameObject != null &&
                                         gameObject.scene.IsValid() &&
                                         gameObject.scene.isLoaded &&
                                         !EditorUtility.IsPersistent(gameObject) &&
                                         (string.IsNullOrEmpty(scenePath) ||
                                          string.Equals(gameObject.scene.path, scenePath,
                                              StringComparison.Ordinal)) &&
                                         (string.Equals(gameObject.name, sceneObjectPath,
                                              StringComparison.Ordinal) ||
                                          string.Equals(
                                              MCPGameObjectCommands.GetHierarchyPath(gameObject),
                                              sceneObjectPath, StringComparison.Ordinal)))
                    .Take(2)
                    .ToList();
                return sceneMatches.Count == 1 ? sceneMatches[0] : null;
            }

            string path = GetString(selector, "path");
            if (string.IsNullOrEmpty(path))
                path = GetString(selector, "gameObjectPath");
            if (string.IsNullOrEmpty(path))
                return null;
            List<GameObject> matches = prefabRoot.GetComponentsInChildren<Transform>(true)
                .Select(transform => transform.gameObject)
                .Where(gameObject =>
                    string.Equals(gameObject.name, path, StringComparison.Ordinal) ||
                    string.Equals(MCPGameObjectCommands.GetHierarchyPath(gameObject), path,
                        StringComparison.Ordinal) ||
                    MCPGameObjectCommands.GetHierarchyPath(gameObject)
                        .EndsWith("/" + path, StringComparison.Ordinal))
                .Take(2)
                .ToList();
            return matches.Count == 1 ? matches[0] : null;
        }

        private static Component ResolveComponent(GameObject gameObject, string typeName,
            int componentIndex)
        {
            Component[] candidates = gameObject.GetComponents<Component>()
                .Where(component => component != null && IsCinemachineComponent(component))
                .ToArray();
            if (string.IsNullOrEmpty(typeName))
                return candidates.Length == 1 ? candidates[0] : null;
            Component[] matches = candidates.Where(component =>
                string.Equals(component.GetType().FullName, typeName, StringComparison.Ordinal) ||
                string.Equals(component.GetType().Name, typeName, StringComparison.Ordinal))
                .ToArray();
            return componentIndex >= 0 && componentIndex < matches.Length
                ? matches[componentIndex]
                : null;
        }

        private static UnityEngine.Object ResolveTargetReference(GameObject target,
            string targetKind, string componentType, int componentIndex)
        {
            switch (targetKind)
            {
                case "":
                case "transform":
                    return target.transform;
                case "gameobject":
                    return target;
                case "component":
                    Component component = ResolveAnyComponent(target, componentType,
                        componentIndex);
                    if (component == null)
                        throw new ArgumentException(
                            $"Target component '{componentType}' was not found on '{MCPGameObjectCommands.GetHierarchyPath(target)}'.");
                    return component;
                default:
                    throw new ArgumentException(
                        "targetKind must be transform, gameobject, or component.");
            }
        }

        private static Component ResolveAnyComponent(GameObject gameObject,
            string typeName, int componentIndex)
        {
            if (string.IsNullOrEmpty(typeName))
                return null;
            Component[] matches = gameObject.GetComponents<Component>().Where(component =>
                component != null &&
                (string.Equals(component.GetType().FullName, typeName,
                     StringComparison.Ordinal) ||
                  string.Equals(component.GetType().Name, typeName,
                     StringComparison.Ordinal))).ToArray();
            return componentIndex >= 0 && componentIndex < matches.Length
                ? matches[componentIndex]
                : null;
        }

        private static Dictionary<string, object> ComponentInfo(Component component,
            bool includeProperties, int maxProperties)
        {
            Component[] sameType = component.gameObject.GetComponents(component.GetType());
            var response = new Dictionary<string, object>
            {
                { "gameObjectPath", MCPGameObjectCommands.GetHierarchyPath(component.gameObject) },
                { "scenePath", component.gameObject.scene.path ?? "" },
                { "componentType", component.GetType().FullName },
                { "componentIndex", Array.IndexOf(sameType, component) },
                { "role", ClassifyRole(component.GetType().Name) },
                { "enabled", component is Behaviour behaviour && behaviour.enabled },
            };
            if (!includeProperties)
                return response;

            var serialized = new SerializedObject(component);
            SerializedProperty iterator = serialized.GetIterator();
            var properties = new List<Dictionary<string, object>>();
            int visibleCount = 0;
            if (iterator.NextVisible(true))
            {
                do
                {
                    if (iterator.propertyPath == "m_Script" ||
                        iterator.propertyPath.StartsWith("m_Prefab", StringComparison.Ordinal) ||
                        iterator.propertyPath == "m_ObjectHideFlags" ||
                        iterator.propertyPath == "m_CorrespondingSourceObject")
                        continue;
                    visibleCount++;
                    if (properties.Count >= maxProperties)
                        continue;
                    properties.Add(new Dictionary<string, object>
                    {
                        { "path", iterator.propertyPath },
                        { "type", iterator.propertyType.ToString() },
                        { "value", MCPAssetGraphUtility.SanitizeValue(
                            MCPComponentCommands.GetSerializedValue(iterator, 2, 32)) },
                    });
                } while (iterator.NextVisible(false));
            }
            response["properties"] = properties;
            response["propertiesTruncated"] = visibleCount > properties.Count;
            return response;
        }

        private static void ValidateOperationKeys(Dictionary<string, object> operation,
            string action)
        {
            string[] common =
            {
                "action", "path", "gameObjectPath", "scenePath", "instanceId",
                "componentType", "componentIndex",
            };
            string[] allowed;
            switch (action)
            {
                case "set-enabled":
                    allowed = common.Concat(new[] { "enabled" }).ToArray();
                    break;
                case "set-property":
                    allowed = common.Concat(new[] { "propertyPath", "value" }).ToArray();
                    break;
                default:
                    allowed = common.Concat(new[]
                    {
                        "propertyPath", "clear", "target", "targetKind",
                        "targetComponentType", "targetComponentIndex",
                    }).ToArray();
                    break;
            }

            var allowedSet = new HashSet<string>(allowed, StringComparer.Ordinal);
            string unexpected = operation.Keys.FirstOrDefault(key =>
                !allowedSet.Contains(key));
            if (!string.IsNullOrEmpty(unexpected))
                throw new ArgumentException(
                    $"Unsupported field '{unexpected}' for Cinemachine action '{action}'. " +
                    $"Allowed fields: {string.Join(", ", allowed.OrderBy(item => item))}.");

            if (!operation.TryGetValue("target", out object targetValue) ||
                targetValue == null)
                return;
            if (!(targetValue is Dictionary<string, object> target))
                throw new ArgumentException("target must be an object.");
            var targetKeys = new HashSet<string>(
                new[] { "path", "gameObjectPath", "scenePath", "instanceId" },
                StringComparer.Ordinal);
            string unexpectedTarget = target.Keys.FirstOrDefault(key =>
                !targetKeys.Contains(key));
            if (!string.IsNullOrEmpty(unexpectedTarget))
                throw new ArgumentException(
                    $"Unsupported target field '{unexpectedTarget}'. Allowed fields: " +
                    string.Join(", ", targetKeys.OrderBy(item => item)) + ".");
        }

        private static bool IsCinemachineComponent(Component component)
        {
            if (component == null)
                return false;
            string fullName = component.GetType().FullName ?? "";
            return fullName.StartsWith("Unity.Cinemachine.", StringComparison.Ordinal) ||
                   fullName.StartsWith("Cinemachine.", StringComparison.Ordinal);
        }

        private static string ClassifyRole(string typeName)
        {
            if (typeName.IndexOf("Brain", StringComparison.OrdinalIgnoreCase) >= 0)
                return "brain";
            if (typeName.IndexOf("Extension", StringComparison.OrdinalIgnoreCase) >= 0 ||
                typeName.IndexOf("Collider", StringComparison.OrdinalIgnoreCase) >= 0 ||
                typeName.IndexOf("Confiner", StringComparison.OrdinalIgnoreCase) >= 0 ||
                typeName.IndexOf("Impulse", StringComparison.OrdinalIgnoreCase) >= 0)
                return "extension";
            if (typeName.IndexOf("Camera", StringComparison.OrdinalIgnoreCase) >= 0)
                return "camera";
            return "component";
        }

        private static string GetString(Dictionary<string, object> values, string key)
        {
            return values != null && values.TryGetValue(key, out object value) && value != null
                ? value.ToString()
                : "";
        }

        private static int GetInt(Dictionary<string, object> values, string key, int defaultValue)
        {
            return values != null && values.TryGetValue(key, out object value) && value != null
                ? Convert.ToInt32(value)
                : defaultValue;
        }

        private static bool GetBool(Dictionary<string, object> values, string key,
            bool defaultValue)
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

        private sealed class PreparedOperation
        {
            private PreparedOperation(object error)
            {
                Error = error;
            }

            internal PreparedOperation(int index, string action, Component component,
                string propertyPath, object value, object before,
                UnityEngine.Object reference)
            {
                Index = index;
                Action = action;
                Component = component;
                PropertyPath = propertyPath;
                Value = value;
                Before = before;
                Reference = reference;
            }

            internal int Index { get; }
            internal string Action { get; }
            internal Component Component { get; }
            internal string PropertyPath { get; }
            internal object Value { get; }
            internal object Before { get; }
            internal UnityEngine.Object Reference { get; }
            internal object Error { get; }

            internal static PreparedOperation WithError(object error)
            {
                return new PreparedOperation(error);
            }

            internal Dictionary<string, object> Describe()
            {
                return new Dictionary<string, object>
                {
                    { "index", Index },
                    { "action", Action },
                    { "gameObjectPath", MCPGameObjectCommands.GetHierarchyPath(
                        Component.gameObject) },
                    { "componentType", Component.GetType().FullName },
                    { "propertyPath", PropertyPath },
                    { "before", Action == "set-enabled"
                        ? (object)((Behaviour)Component).enabled
                        : Before },
                    { "requested", Reference != null
                        ? MCPGameObjectCommands.GetHierarchyPath(
                            Reference is Component referenceComponent
                                ? referenceComponent.gameObject
                                : (GameObject)Reference)
                        : Value },
                };
            }

            internal Dictionary<string, object> Apply()
            {
                Undo.RecordObject(Component, "VM Unity Automation Edit Cinemachine");
                if (Action == "set-enabled")
                {
                    ((Behaviour)Component).enabled = Convert.ToBoolean(Value);
                    EditorUtility.SetDirty(Component);
                    var enabledResult = Describe();
                    enabledResult["after"] = ((Behaviour)Component).enabled;
                    return enabledResult;
                }

                var serialized = new SerializedObject(Component);
                serialized.Update();
                SerializedProperty property = serialized.FindProperty(PropertyPath);
                if (property == null)
                    throw new InvalidOperationException(
                        $"Property '{PropertyPath}' disappeared from {Component.GetType().Name}.");
                if (Action == "set-object-reference")
                    property.objectReferenceValue = Reference;
                else
                    MCPComponentCommands.SetSerializedValue(property, Value);
                serialized.ApplyModifiedProperties();
                EditorUtility.SetDirty(Component);
                var result = Describe();
                result["after"] = MCPComponentCommands.GetSerializedValue(
                    new SerializedObject(Component).FindProperty(PropertyPath), 2, 32);
                return result;
            }
        }
    }
}
