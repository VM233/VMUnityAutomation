using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEngine;
using static VMUnityAutomation.Editor.VmAutomationPrefabCommandUtility;

namespace VMUnityAutomation.Editor
{
    internal static class VmAutomationPrefabBatchEditor
    {
    internal static GameObject FindInPrefab(GameObject root, string prefabPath)
    {
        if (string.IsNullOrEmpty(prefabPath))
            return root;

        string[] parts = prefabPath.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
        Transform current = root.transform;
        int startIndex = parts.Length > 0 && parts[0] == root.name ? 1 : 0;
        for (int partIndex = startIndex; partIndex < parts.Length; partIndex++)
        {
            Transform next = null;
            for (int childIndex = 0; childIndex < current.childCount; childIndex++)
            {
                var child = current.GetChild(childIndex);
                if (child.name == parts[partIndex])
                {
                    next = child;
                    break;
                }
            }

            if (next == null)
                return null;
            current = next;
        }
        return current.gameObject;
    }

    internal static int CountHierarchyNodes(GameObject go)
    {
        int count = 1;
        for (int i = 0; i < go.transform.childCount; i++)
            count += CountHierarchyNodes(go.transform.GetChild(i).gameObject);
        return count;
    }

    internal static Dictionary<string, object> BuildHierarchyNode(GameObject go, int depth, int maxDepth,
        ref int nodeCount, int maxNodes)
    {
        if (nodeCount >= maxNodes)
            return null;
        nodeCount++;

        var components = new List<string>();
        foreach (var comp in go.GetComponents<Component>())
        {
            if (VmAutomationComponentCommands.ShouldIncludeInComponentSummary(comp))
                components.Add(comp.GetType().Name);
        }

        var node = new Dictionary<string, object>
        {
            { "name", go.name },
            { "active", go.activeSelf },
            { "tag", go.tag },
            { "layer", LayerMask.LayerToName(go.layer) },
            { "components", components },
        };
        VmAutomationTransformSerialization.AddLocal(node, go.transform);

        if (depth < maxDepth && go.transform.childCount > 0)
        {
            var children = new List<object>();
            for (int i = 0; i < go.transform.childCount; i++)
            {
                if (nodeCount >= maxNodes)
                    break;
                var child = BuildHierarchyNode(go.transform.GetChild(i).gameObject, depth + 1, maxDepth,
                    ref nodeCount, maxNodes);
                if (child != null)
                    children.Add(child);
            }
            if (children.Count > 0)
                node["children"] = children;
            node["childCount"] = go.transform.childCount;
            if (children.Count < go.transform.childCount)
            {
                node["childrenIncluded"] = children.Count;
                node["childrenTruncated"] = true;
            }
        }
        else if (go.transform.childCount > 0)
        {
            node["childCount"] = go.transform.childCount;
            node["childrenTruncated"] = true;
        }

        return node;
    }

    internal static bool TryApplyBatchOperation(GameObject root, Dictionary<string, object> operation,
        int operationIndex, out Dictionary<string, object> summary, out string error)
    {
        summary = null;
        error = "";

        string operationType = GetOperationType(operation);
        if (string.IsNullOrEmpty(operationType))
        {
            error = $"Operation {operationIndex} is missing type/op/action";
            return false;
        }

        switch (operationType)
        {
            case "addcomponent":
                return TryBatchAddComponent(root, operation, operationIndex, out summary, out error);
            case "configurecomponent":
                return TryBatchConfigureComponent(root, operation, operationIndex, out summary, out error);
            case "setproperty":
                return TryBatchSetProperty(root, operation, operationIndex, out summary, out error);
            case "setreference":
                return TryBatchSetReference(root, operation, operationIndex, out summary, out error);
            case "arrayinsert":
            case "arrayremove":
            case "arrayset":
            case "arrayclear":
                return TryBatchArrayOperation(root, operation, operationIndex, operationType,
                    out summary, out error);
            case "addgameobject":
                return TryBatchAddGameObject(root, operation, operationIndex, out summary, out error);
            case "instantiateprefab":
                return TryBatchInstantiatePrefab(root, operation, operationIndex, out summary, out error);
            case "removecomponent":
                return TryBatchRemoveComponent(root, operation, operationIndex, out summary, out error);
            case "removegameobject":
                return TryBatchRemoveGameObject(root, operation, operationIndex, out summary, out error);
            case "movegameobject":
                return TryBatchMoveGameObject(root, operation, operationIndex, out summary, out error);
            default:
                error = $"Unsupported prefab batch operation '{operationType}' at index {operationIndex}";
                return false;
        }
    }

    internal static bool TryBatchAddComponent(GameObject root, Dictionary<string, object> operation,
        int operationIndex, out Dictionary<string, object> summary, out string error)
    {
        summary = null;
        if (!TryAddPrefabComponent(root, operation, out var go, out var component,
                out int componentIndex, out var changedProperties, out _, out error))
        {
            error = $"Operation {operationIndex}: {error}";
            return false;
        }

        summary = BuildBatchSummary(operationIndex, "addComponent", go, component);
        summary["prefabPath"] = GetPrefabPath(root, go);
        summary["componentIndex"] = componentIndex;
        summary["properties"] = changedProperties;
        return true;
    }

    internal static bool TryAddPrefabComponent(GameObject root,
        Dictionary<string, object> operation, out GameObject gameObject, out Component component,
        out int componentIndex, out List<string> changedProperties,
        out Dictionary<string, object> expectedValues, out string error)
    {
        gameObject = null;
        component = null;
        componentIndex = -1;
        changedProperties = new List<string>();
        expectedValues = new Dictionary<string, object>();
        error = "";

        string prefabPath = GetString(operation, "prefabPath");
        string componentType = GetString(operation, "componentType");
        if (string.IsNullOrEmpty(componentType))
        {
            error = "componentType is required";
            return false;
        }

        gameObject = FindInPrefab(root, prefabPath);
        if (gameObject == null)
        {
            error = $"GameObject '{prefabPath}' not found in prefab";
            return false;
        }

        Type type = VmAutomationComponentCommands.FindType(componentType);
        if (type == null)
        {
            error = $"Type '{componentType}' not found";
            return false;
        }

        componentIndex = gameObject.GetComponents(type).Length;
        component = gameObject.AddComponent(type);
        if (component == null)
        {
            error = $"Failed to add component '{componentType}'";
            return false;
        }

        var properties = GetDictionary(operation, "properties");
        if (properties != null &&
            !TryApplySerializedProperties(component, properties, changedProperties, out error))
        {
            return false;
        }

        return TryCaptureSerializedProperties(component, changedProperties, expectedValues,
            out error);
    }

    internal static bool TryBatchConfigureComponent(GameObject root, Dictionary<string, object> operation,
        int operationIndex, out Dictionary<string, object> summary, out string error)
    {
        summary = null;
        error = "";
        string prefabPath = GetString(operation, "prefabPath");
        string componentType = GetString(operation, "componentType");
        if (string.IsNullOrEmpty(componentType))
        {
            error = $"Operation {operationIndex}: componentType is required";
            return false;
        }

        if (!TryResolveConfigureTarget(root, prefabPath,
                GetBool(operation, "createPathIfMissing", false),
                out var go, out var createdPrefabPaths, out string targetError))
        {
            error = $"Operation {operationIndex}: {targetError}";
            return false;
        }

        Type type = VmAutomationComponentCommands.FindType(componentType);
        if (type == null)
        {
            error = $"Operation {operationIndex}: Type '{componentType}' not found";
            return false;
        }

        int componentIndex = GetInt(operation, "componentIndex", 0);
        if (componentIndex < 0)
        {
            error = $"Operation {operationIndex}: componentIndex must be zero or greater";
            return false;
        }

        var components = go.GetComponents(type);
        bool added = false;
        Component component;
        if (componentIndex < components.Length)
        {
            component = components[componentIndex];
        }
        else if (GetBool(operation, "addIfMissing", true) && componentIndex == components.Length)
        {
            component = go.AddComponent(type);
            added = true;
        }
        else
        {
            error = $"Operation {operationIndex}: Component '{componentType}' at index {componentIndex} " +
                    $"was not found on '{go.name}'";
            return false;
        }

        var changedProperties = new List<string>();
        var properties = GetDictionary(operation, "properties");
        if (properties != null &&
            !TryApplySerializedProperties(component, properties, changedProperties, out error))
        {
            error = $"Operation {operationIndex}: {error}";
            return false;
        }

        var changedReferences = new List<Dictionary<string, object>>();
        foreach (var reference in GetDictionaryList(operation, "references"))
        {
            string propertyName = GetString(reference, "propertyName");
            if (string.IsNullOrEmpty(propertyName))
            {
                error = $"Operation {operationIndex}: references[].propertyName is required";
                return false;
            }

            string referenceDescription;
            using (var serialized = new SerializedObject(component))
            {
                var property = serialized.FindProperty(propertyName);
                if (property == null)
                {
                    error = $"Operation {operationIndex}: Property '{propertyName}' not found on " +
                            $"'{component.GetType().Name}'";
                    return false;
                }
                if (property.propertyType != SerializedPropertyType.ObjectReference)
                {
                    error = $"Operation {operationIndex}: Property '{propertyName}' is not an ObjectReference";
                    return false;
                }

                if (!TryResolveBatchReference(root, property, reference,
                        out UnityEngine.Object targetReference,
                        out referenceDescription, out error))
                {
                    error = $"Operation {operationIndex}: {error}";
                    return false;
                }

                property.objectReferenceValue = targetReference;
                serialized.ApplyModifiedProperties();
            }
            changedReferences.Add(new Dictionary<string, object>
            {
                { "propertyName", propertyName },
                { "reference", referenceDescription },
            });
        }

        summary = BuildBatchSummary(operationIndex, "configureComponent", go, component);
        summary["prefabPath"] = GetPrefabPath(root, go);
        summary["componentIndex"] = componentIndex;
        summary["added"] = added;
        summary["properties"] = changedProperties;
        summary["references"] = changedReferences;
        if (createdPrefabPaths.Count > 0)
            summary["createdPrefabPaths"] = createdPrefabPaths;
        return true;
    }

    internal static bool TryResolveConfigureTarget(GameObject root, string prefabPath,
        bool createPathIfMissing, out GameObject gameObject, out List<string> createdPrefabPaths,
        out string error)
    {
        gameObject = null;
        createdPrefabPaths = new List<string>();
        error = "";

        if (!createPathIfMissing)
        {
            gameObject = FindInPrefab(root, prefabPath);
            if (gameObject != null)
                return true;
            error = $"GameObject '{prefabPath}' not found in prefab";
            return false;
        }

        if (string.IsNullOrWhiteSpace(prefabPath))
        {
            gameObject = root;
            return true;
        }
        if (prefabPath.IndexOf('\\') >= 0)
        {
            error = "prefabPath must use '/' separators";
            return false;
        }

        string[] parts = prefabPath.Split('/');
        if (parts.Any(part => string.IsNullOrWhiteSpace(part) || part == "." || part == ".."))
        {
            error = $"prefabPath '{prefabPath}' contains an empty or traversal segment";
            return false;
        }

        Transform current = root.transform;
        int startIndex = parts.Length > 0 && parts[0] == root.name ? 1 : 0;
        for (int partIndex = startIndex; partIndex < parts.Length; partIndex++)
        {
            Transform next = null;
            for (int childIndex = 0; childIndex < current.childCount; childIndex++)
            {
                Transform child = current.GetChild(childIndex);
                if (child.name == parts[partIndex])
                {
                    next = child;
                    break;
                }
            }

            if (next == null)
            {
                var child = new GameObject(parts[partIndex]);
                child.layer = current.gameObject.layer;
                child.transform.SetParent(current, false);
                next = child.transform;
                createdPrefabPaths.Add(GetPrefabPath(root, child));
            }
            current = next;
        }

        gameObject = current.gameObject;
        return true;
    }

    internal static bool TryBatchSetProperty(GameObject root, Dictionary<string, object> operation,
        int operationIndex, out Dictionary<string, object> summary, out string error)
    {
        summary = null;
        if (!TryGetBatchComponent(root, operation, operationIndex, out var go, out var component, out error))
            return false;

        var changedProperties = new List<string>();
        var properties = GetDictionary(operation, "properties");
        if (properties != null)
        {
            if (!TryApplySerializedProperties(component, properties, changedProperties, out error))
            {
                error = $"Operation {operationIndex}: {error}";
                return false;
            }
        }
        else
        {
            string propertyName = GetString(operation, "propertyName");
            if (string.IsNullOrEmpty(propertyName))
            {
                error = $"Operation {operationIndex}: propertyName or properties is required";
                return false;
            }

            if (!operation.ContainsKey("value"))
            {
                error = $"Operation {operationIndex}: value is required";
                return false;
            }

            if (!TryApplySerializedProperty(component, propertyName, operation["value"], out error))
            {
                error = $"Operation {operationIndex}: {error}";
                return false;
            }

            changedProperties.Add(propertyName);
        }

        summary = BuildBatchSummary(operationIndex, "setProperty", go, component);
        summary["prefabPath"] = GetPrefabPath(root, go);
        summary["properties"] = changedProperties;
        return true;
    }

    internal static bool TryBatchSetReference(GameObject root, Dictionary<string, object> operation,
        int operationIndex, out Dictionary<string, object> summary, out string error)
    {
        summary = null;
        error = "";
        string prefabPath = GetString(operation, "prefabPath");
        string propertyName = GetString(operation, "propertyName");
        if (string.IsNullOrEmpty(propertyName))
        {
            error = $"Operation {operationIndex}: propertyName is required";
            return false;
        }

        var go = FindInPrefab(root, prefabPath);
        if (go == null)
        {
            error = $"Operation {operationIndex}: GameObject '{prefabPath}' not found in prefab";
            return false;
        }

        Component component = null;
        string componentType = GetString(operation, "componentType");
        if (string.IsNullOrEmpty(componentType) == false)
        {
            if (!TryGetBatchComponent(root, operation, operationIndex, out _, out component, out error))
                return false;
        }
        else
        {
            foreach (var candidate in go.GetComponents<Component>())
            {
                if (candidate == null)
                    continue;

                using (var serializedCandidate = new SerializedObject(candidate))
                {
                    if (serializedCandidate.FindProperty(propertyName) != null)
                    {
                        component = candidate;
                        break;
                    }
                }
            }
        }

        if (component == null)
        {
            error = $"Operation {operationIndex}: Component '{componentType}' not found on '{go.name}', or no component has property '{propertyName}'";
            return false;
        }

        string refDescription;
        using (var serialized = new SerializedObject(component))
        {
            var prop = serialized.FindProperty(propertyName);
            if (prop == null)
            {
                error = $"Operation {operationIndex}: Property '{propertyName}' not found";
                return false;
            }

            if (prop.propertyType != SerializedPropertyType.ObjectReference)
            {
                error = $"Operation {operationIndex}: Property '{propertyName}' is not an ObjectReference";
                return false;
            }

            if (!TryResolveBatchReference(root, prop, operation, out UnityEngine.Object targetRef,
                    out refDescription, out error))
            {
                error = $"Operation {operationIndex}: {error}";
                return false;
            }

            prop.objectReferenceValue = targetRef;
            serialized.ApplyModifiedProperties();
        }

        summary = BuildBatchSummary(operationIndex, "setReference", go, component);
        summary["prefabPath"] = GetPrefabPath(root, go);
        summary["property"] = propertyName;
        summary["reference"] = refDescription;
        return true;
    }

    internal static bool TryBatchArrayOperation(GameObject root, Dictionary<string, object> operation,
        int operationIndex, string operationType, out Dictionary<string, object> summary, out string error)
    {
        summary = null;
        if (!TryGetBatchComponent(root, operation, operationIndex, out var go, out var component, out error))
            return false;

        string propertyName = GetString(operation, "propertyName");
        if (string.IsNullOrEmpty(propertyName))
        {
            error = $"Operation {operationIndex}: propertyName is required";
            return false;
        }

        int index = GetInt(operation, "index", -1);
        int beforeSize;
        int afterSize;
        using (var serialized = new SerializedObject(component))
        {
            var property = serialized.FindProperty(propertyName);
            if (property == null)
            {
                error = $"Operation {operationIndex}: Property '{propertyName}' not found";
                return false;
            }
            if (!property.isArray || property.propertyType == SerializedPropertyType.String)
            {
                error = $"Operation {operationIndex}: Property '{propertyName}' is not an array or list";
                return false;
            }

            beforeSize = property.arraySize;
            try
            {
                switch (operationType)
                {
                    case "arrayinsert":
                        if (index < 0 || index > property.arraySize)
                            throw new IndexOutOfRangeException($"Index {index} is invalid for size {property.arraySize}");
                        property.InsertArrayElementAtIndex(index);
                        if (operation.TryGetValue("value", out var insertValue))
                            VmAutomationComponentCommands.SetSerializedValue(property.GetArrayElementAtIndex(index), insertValue);
                        break;
                    case "arrayremove":
                        if (index < 0 || index >= property.arraySize)
                            throw new IndexOutOfRangeException($"Index {index} is invalid for size {property.arraySize}");
                        int sizeBeforeDelete = property.arraySize;
                        property.DeleteArrayElementAtIndex(index);
                        if (property.arraySize == sizeBeforeDelete)
                            property.DeleteArrayElementAtIndex(index);
                        break;
                    case "arrayset":
                        if (index < 0 || index >= property.arraySize)
                            throw new IndexOutOfRangeException($"Index {index} is invalid for size {property.arraySize}");
                        if (!operation.TryGetValue("value", out var setValue))
                            throw new ArgumentException("value is required");
                        VmAutomationComponentCommands.SetSerializedValue(property.GetArrayElementAtIndex(index), setValue);
                        break;
                    case "arrayclear":
                        property.ClearArray();
                        break;
                }

                serialized.ApplyModifiedProperties();
                afterSize = property.arraySize;
            }
            catch (Exception ex)
            {
                error = $"Operation {operationIndex}: {ex.Message}";
                return false;
            }
        }

        summary = BuildBatchSummary(operationIndex, operationType, go, component);
        summary["prefabPath"] = GetPrefabPath(root, go);
        summary["property"] = propertyName;
        summary["index"] = index;
        summary["beforeSize"] = beforeSize;
        summary["afterSize"] = afterSize;
        error = "";
        return true;
    }

    internal static bool TryBatchAddGameObject(GameObject root, Dictionary<string, object> operation,
        int operationIndex, out Dictionary<string, object> summary, out string error)
    {
        summary = null;
        error = "";
        string parentPrefabPath = GetString(operation, "parentPrefabPath");
        string name = GetString(operation, "name");
        if (string.IsNullOrEmpty(name))
        {
            error = $"Operation {operationIndex}: name is required";
            return false;
        }

        var parent = FindInPrefab(root, parentPrefabPath);
        if (parent == null)
        {
            error = $"Operation {operationIndex}: Parent '{parentPrefabPath}' not found in prefab";
            return false;
        }

        if (!TryResolveCreatedGameObjectLayer(operation, parent, out int layer, out string layerError))
        {
            error = $"Operation {operationIndex}: {layerError}";
            return false;
        }

        string primitiveType = GetString(operation, "primitiveType");
        GameObject newGo;
        if (!string.IsNullOrEmpty(primitiveType) && Enum.TryParse<PrimitiveType>(primitiveType, true, out var pt))
        {
            newGo = GameObject.CreatePrimitive(pt);
            newGo.name = name;
        }
        else
        {
            newGo = new GameObject(name);
        }

        newGo.transform.SetParent(parent.transform, false);
        newGo.layer = layer;
        ApplyTransformArguments(newGo.transform, operation);

        summary = BuildBatchSummary(operationIndex, "addGameObject", newGo, null);
        summary["parent"] = string.IsNullOrEmpty(parentPrefabPath) ? "root" : parentPrefabPath;
        summary["prefabPath"] = GetPrefabPath(root, newGo);
        summary["layer"] = LayerMask.LayerToName(newGo.layer);
        summary["layerIndex"] = newGo.layer;
        return true;
    }

    internal static bool TryBatchInstantiatePrefab(GameObject root, Dictionary<string, object> operation,
        int operationIndex, out Dictionary<string, object> summary, out string error)
    {
        summary = null;
        string sourcePrefabPath = GetString(operation, "sourcePrefabPath");
        if (string.IsNullOrEmpty(sourcePrefabPath))
        {
            error = $"Operation {operationIndex}: sourcePrefabPath is required";
            return false;
        }

        string parentPrefabPath = GetString(operation, "parentPrefabPath");
        var parent = FindInPrefab(root, parentPrefabPath);
        if (parent == null)
        {
            error = $"Operation {operationIndex}: Parent '{parentPrefabPath}' not found in prefab";
            return false;
        }

        var sourcePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePrefabPath);
        if (sourcePrefab == null)
        {
            error = $"Operation {operationIndex}: Source prefab not found at '{sourcePrefabPath}'";
            return false;
        }

        var instance = PrefabUtility.InstantiatePrefab(sourcePrefab, root.scene) as GameObject;
        if (instance == null)
        {
            error = $"Operation {operationIndex}: Failed to instantiate prefab '{sourcePrefabPath}'";
            return false;
        }

        instance.transform.SetParent(parent.transform, false);

        string name = GetString(operation, "name");
        if (string.IsNullOrEmpty(name) == false)
            instance.name = name;

        ApplyTransformArguments(instance.transform, operation);

        int siblingIndex = GetInt(operation, "siblingIndex", -1);
        if (siblingIndex >= 0)
            instance.transform.SetSiblingIndex(Mathf.Clamp(siblingIndex, 0, parent.transform.childCount - 1));

        summary = BuildBatchSummary(operationIndex, "instantiatePrefab", instance, null);
        summary["sourcePrefabPath"] = sourcePrefabPath;
        summary["parent"] = string.IsNullOrEmpty(parentPrefabPath) ? "root" : parentPrefabPath;
        summary["prefabPath"] = GetPrefabPath(root, instance);
        summary["siblingIndex"] = instance.transform.GetSiblingIndex();
        error = "";
        return true;
    }

    internal static bool TryBatchRemoveComponent(GameObject root, Dictionary<string, object> operation,
        int operationIndex, out Dictionary<string, object> summary, out string error)
    {
        summary = null;
        if (!TryGetBatchComponent(root, operation, operationIndex, out var go, out var component, out error))
            return false;

        string componentName = component.GetType().Name;
        int index = GetInt(operation, "componentIndex", 0);
        UnityEngine.Object.DestroyImmediate(component);

        summary = new Dictionary<string, object>
        {
            { "index", operationIndex },
            { "type", "removeComponent" },
            { "gameObject", go.name },
            { "prefabPath", GetPrefabPath(root, go) },
            { "component", componentName },
            { "componentIndex", index },
        };
        return true;
    }

    internal static bool TryBatchRemoveGameObject(GameObject root, Dictionary<string, object> operation,
        int operationIndex, out Dictionary<string, object> summary, out string error)
    {
        summary = null;
        error = "";
        string prefabPath = GetString(operation, "prefabPath");
        if (string.IsNullOrEmpty(prefabPath))
        {
            error = $"Operation {operationIndex}: prefabPath is required";
            return false;
        }

        var go = FindInPrefab(root, prefabPath);
        if (go == null)
        {
            error = $"Operation {operationIndex}: GameObject '{prefabPath}' not found in prefab";
            return false;
        }

        if (go == root)
        {
            error = $"Operation {operationIndex}: Cannot delete the root GameObject of a prefab";
            return false;
        }

        string deletedName = go.name;
        UnityEngine.Object.DestroyImmediate(go);

        summary = new Dictionary<string, object>
        {
            { "index", operationIndex },
            { "type", "removeGameObject" },
            { "deletedGameObject", deletedName },
            { "prefabPath", prefabPath },
        };
        return true;
    }

    internal static bool TryBatchMoveGameObject(GameObject root, Dictionary<string, object> operation,
        int operationIndex, out Dictionary<string, object> summary, out string error)
    {
        summary = null;
        string prefabPath = GetString(operation, "prefabPath");
        if (string.IsNullOrEmpty(prefabPath))
        {
            error = $"Operation {operationIndex}: prefabPath is required";
            return false;
        }

        var go = FindInPrefab(root, prefabPath);
        if (go == null)
        {
            error = $"Operation {operationIndex}: GameObject '{prefabPath}' not found in prefab";
            return false;
        }

        if (go == root)
        {
            error = $"Operation {operationIndex}: Cannot move the root GameObject of a prefab";
            return false;
        }

        string newParentPrefabPath = GetString(operation, "newParentPrefabPath");
        var newParent = FindInPrefab(root, newParentPrefabPath);
        if (newParent == null)
        {
            error = $"Operation {operationIndex}: New parent '{newParentPrefabPath}' not found in prefab";
            return false;
        }

        string oldPath = GetPrefabPath(root, go);
        string oldParentPath = go.transform.parent != null ? GetPrefabPath(root, go.transform.parent.gameObject) : "";
        int oldSiblingIndex = go.transform.GetSiblingIndex();
        bool worldPositionStays = GetBool(operation, "worldPositionStays", false);
        int siblingIndex = GetInt(operation, "siblingIndex", -1);

        go.transform.SetParent(newParent.transform, worldPositionStays);
        if (siblingIndex >= 0)
            go.transform.SetSiblingIndex(Mathf.Clamp(siblingIndex, 0, newParent.transform.childCount - 1));

        summary = new Dictionary<string, object>
        {
            { "index", operationIndex },
            { "type", "moveGameObject" },
            { "gameObject", go.name },
            { "oldPath", oldPath },
            { "newPath", GetPrefabPath(root, go) },
            { "oldParent", oldParentPath },
            { "newParent", string.IsNullOrEmpty(newParentPrefabPath) ? "root" : newParentPrefabPath },
            { "oldSiblingIndex", oldSiblingIndex },
            { "newSiblingIndex", go.transform.GetSiblingIndex() },
        };
        error = "";
        return true;
    }

    internal static bool TryGetBatchComponent(GameObject root, Dictionary<string, object> operation,
        int operationIndex, out GameObject go, out Component component, out string error)
    {
        go = null;
        component = null;
        error = "";

        string prefabPath = GetString(operation, "prefabPath");
        string componentType = GetString(operation, "componentType");
        if (string.IsNullOrEmpty(componentType))
        {
            error = $"Operation {operationIndex}: componentType is required";
            return false;
        }

        go = FindInPrefab(root, prefabPath);
        if (go == null)
        {
            error = $"Operation {operationIndex}: GameObject '{prefabPath}' not found in prefab";
            return false;
        }

        Type type = VmAutomationComponentCommands.FindType(componentType);
        if (type == null)
        {
            error = $"Operation {operationIndex}: Type '{componentType}' not found";
            return false;
        }

        int index = GetInt(operation, "componentIndex", 0);
        var components = go.GetComponents(type);
        if (components == null || index < 0 || index >= components.Length)
        {
            error = $"Operation {operationIndex}: Component '{componentType}' at index {index} not found on '{go.name}'";
            return false;
        }

        component = components[index];
        return true;
    }

    internal static bool TryApplySerializedProperties(Component component, Dictionary<string, object> properties,
        List<string> changedProperties, out string error)
    {
        error = "";
        foreach (var pair in properties)
        {
            if (!TryApplySerializedProperty(component, pair.Key, pair.Value, out error))
                return false;

            changedProperties.Add(pair.Key);
        }

        return true;
    }

    internal static bool TryApplySerializedProperty(Component component, string propertyName, object value,
        out string error)
    {
        error = "";
        using (var serialized = new SerializedObject(component))
        {
            var prop = serialized.FindProperty(propertyName);
            if (prop == null)
            {
                error = $"Property '{propertyName}' not found on '{component.GetType().Name}'";
                return false;
            }

            VmAutomationComponentCommands.SetSerializedValue(prop, value);
            serialized.ApplyModifiedProperties();
        }
        return true;
    }

    internal static bool TryCaptureSerializedProperties(Component component,
        IEnumerable<string> propertyNames, Dictionary<string, object> values, out string error)
    {
        error = "";
        if (component == null)
        {
            error = "Component is missing";
            return false;
        }

        using (var serialized = new SerializedObject(component))
        {
            serialized.UpdateIfRequiredOrScript();
            foreach (string propertyName in propertyNames ?? Enumerable.Empty<string>())
            {
                var property = serialized.FindProperty(propertyName);
                if (property == null)
                {
                    error =
                        $"Property '{propertyName}' not found on '{component.GetType().Name}' after assignment";
                    return false;
                }

                values[propertyName] = VmAutomationComponentCommands.GetSerializedValue(
                    property, 16, 10000);
            }
        }

        return true;
    }

    internal static bool TryVerifyPrefabComponentConfiguration(string assetPath,
        string prefabPath, Type componentType, int componentIndex,
        Dictionary<string, object> expectedValues, out string error)
    {
        error = "";
        try
        {
            var prefabRoot = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (prefabRoot == null)
            {
                error = $"Prefab '{assetPath}' could not be loaded";
                return false;
            }

            var gameObject = FindInPrefab(prefabRoot, prefabPath);
            if (gameObject == null)
            {
                error = $"GameObject '{prefabPath}' was not found";
                return false;
            }

            var components = gameObject.GetComponents(componentType);
            if (componentIndex < 0 || componentIndex >= components.Length)
            {
                error = $"Component '{componentType.FullName}' at index {componentIndex} " +
                        $"was not found; persisted count is {components.Length}";
                return false;
            }

            using (var serialized = new SerializedObject(components[componentIndex]))
            {
                serialized.UpdateIfRequiredOrScript();
                foreach (var pair in expectedValues)
                {
                    var property = serialized.FindProperty(pair.Key);
                    if (property == null)
                    {
                        error =
                            $"Property '{pair.Key}' was not found on persisted component";
                        return false;
                    }

                    object actualValue = VmAutomationComponentCommands.GetSerializedValue(
                        property, 16, 10000);
                    if (SerializedValuesEquivalent(pair.Value, actualValue))
                        continue;

                    error = $"Property '{pair.Key}' did not match. Expected " +
                            $"{MiniJson.Serialize(pair.Value)}, read {MiniJson.Serialize(actualValue)}";
                    return false;
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            error = ex.GetBaseException().Message;
            return false;
        }
    }

    internal static bool SerializedValuesEquivalent(object expected, object actual)
    {
        return MiniJson.Serialize(NormalizeSerializedValueForComparison(expected)) ==
               MiniJson.Serialize(NormalizeSerializedValueForComparison(actual));
    }

    internal static object NormalizeSerializedValueForComparison(object value)
    {
        if (value is Dictionary<string, object> dictionary)
        {
            var normalized = new Dictionary<string, object>();
            foreach (var pair in dictionary.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                if (pair.Key == "instanceId")
                    continue;
                normalized[pair.Key] = NormalizeSerializedValueForComparison(pair.Value);
            }
            return normalized;
        }

        if (value is System.Collections.IList list && !(value is string))
        {
            var normalized = new List<object>();
            foreach (object item in list)
                normalized.Add(NormalizeSerializedValueForComparison(item));
            return normalized;
        }

        return value;
    }

    internal static bool TryResolveBatchReference(GameObject root, SerializedProperty property,
        Dictionary<string, object> operation, out UnityEngine.Object targetRef,
        out string refDescription, out string error)
    {
        targetRef = null;
        refDescription = "null (cleared)";
        error = "";

        bool clearRef = GetBool(operation, "clear", false);
        if (clearRef)
            return true;

        string referenceAssetPath = GetString(operation, "referenceAssetPath");
        if (string.IsNullOrEmpty(referenceAssetPath) == false)
        {
            string referenceSubAssetName = GetString(operation, "referenceSubAssetName");
            string referenceSubAssetLocalId = GetString(operation, "referenceSubAssetLocalId");
            if (!TryResolveAssetReference(property, referenceAssetPath, referenceSubAssetName,
                    referenceSubAssetLocalId, out targetRef, out error))
                return false;

            refDescription = $"{targetRef.name} ({targetRef.GetType().Name})";
            return true;
        }

        string referencePrefabPath = GetString(operation, "referencePrefabPath");
        if (operation.ContainsKey("referencePrefabPath"))
        {
            var refGo = FindInPrefab(root, referencePrefabPath);
            if (refGo == null)
            {
                error = $"GameObject '{referencePrefabPath}' not found in prefab";
                return false;
            }

            string referenceComponentType = GetString(operation, "referenceComponentType");
            if (string.IsNullOrEmpty(referenceComponentType) == false)
            {
                Type refType = VmAutomationComponentCommands.FindType(referenceComponentType);
                if (refType == null)
                {
                    error = $"Type '{referenceComponentType}' not found";
                    return false;
                }

                int referenceComponentIndex = GetInt(operation, "referenceComponentIndex", 0);
                var referenceComponents = refGo.GetComponents(refType);
                if (referenceComponentIndex < 0 || referenceComponentIndex >= referenceComponents.Length)
                {
                    error = $"Component '{referenceComponentType}' at index {referenceComponentIndex} " +
                            $"not found on '{refGo.name}'";
                    return false;
                }
                targetRef = referenceComponents[referenceComponentIndex];
            }
            else
            {
                targetRef = refGo;
            }

            refDescription = $"{targetRef.name} ({targetRef.GetType().Name})";
            return true;
        }

        error = "Provide referenceAssetPath, referencePrefabPath, or clear=true";
        return false;
    }

    internal static bool TryResolveAssetReference(SerializedProperty property, string assetPath,
        string subAssetName, string subAssetLocalIdText, out UnityEngine.Object targetRef,
        out string error)
    {
        targetRef = null;
        error = "";
        long? subAssetLocalId = null;
        if (string.IsNullOrWhiteSpace(subAssetLocalIdText) == false)
        {
            if (!long.TryParse(subAssetLocalIdText, NumberStyles.Integer, CultureInfo.InvariantCulture,
                    out long parsedLocalId))
            {
                error = "referenceSubAssetLocalId must be a signed 64-bit decimal string";
                return false;
            }

            subAssetLocalId = parsedLocalId;
        }

        var candidates = AssetDatabase.LoadAllAssetsAtPath(assetPath);
        if (candidates == null || candidates.Length == 0)
        {
            error = $"Asset not found at '{assetPath}'";
            return false;
        }

        UnityEngine.Object originalReference = property.objectReferenceValue;
        var compatibleCandidates = new List<UnityEngine.Object>();
        try
        {
            foreach (var candidate in candidates)
            {
                if (candidate == null)
                    continue;

                property.objectReferenceValue = candidate;
                if (property.objectReferenceValue == candidate)
                    compatibleCandidates.Add(candidate);
            }
        }
        finally
        {
            property.objectReferenceValue = originalReference;
        }

        if (compatibleCandidates.Count == 0)
        {
            error = $"No asset at '{assetPath}' is compatible with property " +
                    $"'{property.propertyPath}' ({property.type})";
            return false;
        }

        IEnumerable<UnityEngine.Object> selectedCandidates = compatibleCandidates;
        if (string.IsNullOrEmpty(subAssetName) == false)
        {
            selectedCandidates = selectedCandidates.Where(candidate =>
                string.Equals(candidate.name, subAssetName, StringComparison.Ordinal));
        }

        if (subAssetLocalId.HasValue)
        {
            selectedCandidates = selectedCandidates.Where(candidate =>
                TryGetAssetLocalId(candidate, out long localId) && localId == subAssetLocalId.Value);
        }

        var selected = selectedCandidates.ToList();
        if (selected.Count == 1)
        {
            targetRef = selected[0];
            property.objectReferenceValue = targetRef;
            return true;
        }

        string available = DescribeAssetReferenceCandidates(compatibleCandidates);
        if (selected.Count == 0 &&
            (string.IsNullOrEmpty(subAssetName) == false || subAssetLocalId.HasValue))
        {
            var selectors = new List<string>();
            if (string.IsNullOrEmpty(subAssetName) == false)
                selectors.Add($"referenceSubAssetName='{subAssetName}'");
            if (subAssetLocalId.HasValue)
                selectors.Add($"referenceSubAssetLocalId='{subAssetLocalId.Value.ToString(CultureInfo.InvariantCulture)}'");
            error = $"No compatible asset at '{assetPath}' matches {string.Join(" and ", selectors)}. " +
                    $"Available compatible assets: {available}";
            return false;
        }

        error = $"Asset path '{assetPath}' has {selected.Count} compatible objects for property " +
                $"'{property.propertyPath}' ({property.type}). Specify referenceSubAssetName or " +
                $"referenceSubAssetLocalId. Compatible assets: {available}";
        return false;
    }

    internal static bool TryGetAssetLocalId(UnityEngine.Object asset, out long localId)
    {
        return AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string _, out localId);
    }

    internal static string DescribeAssetReferenceCandidates(IReadOnlyList<UnityEngine.Object> candidates)
    {
        const int MaxDescriptions = 8;
        var descriptions = candidates.Take(MaxDescriptions).Select(candidate =>
        {
            string localId = TryGetAssetLocalId(candidate, out long value)
                ? value.ToString(CultureInfo.InvariantCulture)
                : "unknown";
            return $"'{candidate.name}' ({candidate.GetType().Name}, localId {localId})";
        });
        string suffix = candidates.Count > MaxDescriptions
            ? $", ... ({candidates.Count - MaxDescriptions} more)"
            : "";
        return string.Join(", ", descriptions) + suffix;
    }

    internal static void ApplyTransformArguments(Transform transform, Dictionary<string, object> operation)
    {
        if (operation.ContainsKey("position"))
            transform.localPosition = ParseVector3(operation["position"]);
        if (operation.ContainsKey("rotation"))
            transform.localEulerAngles = ParseVector3(operation["rotation"]);
        if (operation.ContainsKey("scale"))
            transform.localScale = ParseVector3(operation["scale"]);
    }

    internal static bool TryResolveCreatedGameObjectLayer(Dictionary<string, object> args, GameObject parent,
        out int layer, out string error)
    {
        layer = parent != null ? parent.layer : 0;
        error = "";

        if (args == null || !args.TryGetValue("layer", out object rawLayer))
            return true;

        string layerValue = rawLayer?.ToString()?.Trim();
        if (string.IsNullOrEmpty(layerValue))
        {
            error = "layer must be a defined Unity layer name or an index from 0 to 31";
            return false;
        }

        if (!int.TryParse(layerValue, out layer))
            layer = LayerMask.NameToLayer(layerValue);

        if (layer < 0 || layer > 31)
        {
            error = $"Layer '{layerValue}' was not found. Provide a defined Unity layer name or an index from 0 to 31";
            return false;
        }

        return true;
    }

    internal static Dictionary<string, object> BuildBatchSummary(int operationIndex, string operationType,
        GameObject go, Component component)
    {
        var summary = new Dictionary<string, object>
        {
            { "index", operationIndex },
            { "type", operationType },
            { "gameObject", go != null ? go.name : "" },
        };

        if (component != null)
        {
            summary["component"] = component.GetType().Name;
            summary["fullType"] = component.GetType().FullName;
        }

        return summary;
    }

    internal static List<string> CollectBatchEditComponentTypes(Dictionary<string, object> args)
    {
        var types = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var operation in GetDictionaryList(args, "operations"))
        {
            AddComponentType(types, GetString(operation, "componentType"));
            AddComponentType(types, GetString(operation, "referenceComponentType"));
            foreach (var reference in GetDictionaryList(operation, "references"))
                AddComponentType(types, GetString(reference, "referenceComponentType"));
        }

        return types.OrderBy(type => type, StringComparer.OrdinalIgnoreCase).ToList();
    }

    internal static void AddComponentType(HashSet<string> types, string componentType)
    {
        if (string.IsNullOrEmpty(componentType) == false)
            types.Add(componentType);
    }

    internal static string GetOperationType(Dictionary<string, object> operation)
    {
        string operationType = GetString(operation, "type");
        if (string.IsNullOrEmpty(operationType))
            operationType = GetString(operation, "op");
        if (string.IsNullOrEmpty(operationType))
            operationType = GetString(operation, "action");

        return operationType
            .Replace("-", "")
            .Replace("_", "")
            .Replace(" ", "")
            .ToLowerInvariant();
    }

    }
}
