using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using UnityEditor;
using UnityEngine;
using static VMUnityAutomation.Editor.VmAutomationPrefabCommandUtility;

namespace VMUnityAutomation.Editor
{
    /// <summary>
    /// Direct prefab asset editing — browse hierarchy, get/set properties, wire references,
    /// add/remove components and children on prefab assets without needing a scene instance.
    /// Every mutation is atomic: load → modify → save → unload → normalize/import → verify/commit.
    /// </summary>
    public static class VmAutomationPrefabAssetCommands
    {

        // ─── Hierarchy ───

        /// <summary>
        /// Get the full hierarchy tree of a prefab asset.
        /// </summary>
        public static object GetHierarchy(Dictionary<string, object> args)
        {
            string assetPath = GetString(args, "assetPath");
            if (string.IsNullOrEmpty(assetPath))
                return new { error = "assetPath is required" };

            int maxDepth = Math.Max(0, Math.Min(GetInt(args, "maxDepth", 10), 50));
            int maxNodes = Math.Max(1, Math.Min(GetInt(args, "maxNodes", 250), 2000));
            string prefabPath = GetString(args, "prefabPath");

            var root = PrefabUtility.LoadPrefabContents(assetPath);
            if (root == null)
                return new { error = $"Failed to load prefab at '{assetPath}'" };

            try
            {
                var hierarchyRoot = FindInPrefab(root, prefabPath);
                if (hierarchyRoot == null)
                    return new { error = $"GameObject '{prefabPath}' not found in prefab" };

                int totalNodes = CountHierarchyNodes(hierarchyRoot);
                int returnedNodes = 0;
                var hierarchy = BuildHierarchyNode(hierarchyRoot, 0, maxDepth, ref returnedNodes, maxNodes);
                var result = new Dictionary<string, object>
                {
                    { "prefab", root.name },
                    { "assetPath", assetPath },
                    { "hierarchy", hierarchy },
                    { "returnedNodes", returnedNodes },
                    { "totalNodes", totalNodes },
                    { "maxNodes", maxNodes },
                    { "truncated", returnedNodes < totalNodes },
                };
                if (!string.IsNullOrEmpty(prefabPath))
                    result["prefabPath"] = prefabPath;
                return result;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // ─── Component Properties ───

        /// <summary>
        /// Read all properties from a component on a GameObject inside a prefab asset.
        /// </summary>
        public static object AddGameObject(Dictionary<string, object> args)
        {
            string assetPath = GetString(args, "assetPath");
            if (string.IsNullOrEmpty(assetPath))
                return new { error = "assetPath is required" };
            var beforeSnapshot = CaptureAssetText(assetPath);

            string parentPrefabPath = GetString(args, "parentPrefabPath");
            string name = GetString(args, "name");
            if (string.IsNullOrEmpty(name))
                return new { error = "name is required" };

            string primitiveType = GetString(args, "primitiveType");

            var root = PrefabUtility.LoadPrefabContents(assetPath);
            if (root == null)
                return new { error = $"Failed to load prefab at '{assetPath}'" };

            using (var session = new PrefabMutationSession(assetPath, beforeSnapshot, root))
            {
                try
                {
                    var parent = FindInPrefab(root, parentPrefabPath);
                    if (parent == null)
                        return new { error = $"Parent '{parentPrefabPath}' not found in prefab" };

                    if (!TryResolveCreatedGameObjectLayer(args, parent, out int layer, out string layerError))
                        return new { error = layerError };

                    GameObject newGo;
                    if (!string.IsNullOrEmpty(primitiveType) &&
                        Enum.TryParse<PrimitiveType>(primitiveType, true, out var pt))
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

                    if (args.ContainsKey("position"))
                        newGo.transform.localPosition = ParseVector3(args["position"]);
                    if (args.ContainsKey("rotation"))
                        newGo.transform.localEulerAngles = ParseVector3(args["rotation"]);
                    if (args.ContainsKey("scale"))
                        newGo.transform.localScale = ParseVector3(args["scale"]);

                    string prefabName = root.name;
                    string layerName = LayerMask.LayerToName(newGo.layer);
                    int layerIndex = newGo.layer;
                    if (session.SaveAndClose() == null)
                        throw new InvalidOperationException(
                            $"SaveAsPrefabAsset returned null for '{assetPath}'.");

                    var result = new Dictionary<string, object>
                    {
                        { "success", true },
                        { "prefab", prefabName },
                        { "createdGameObject", name },
                        { "parent", string.IsNullOrEmpty(parentPrefabPath) ? "root" : parentPrefabPath },
                        { "layer", layerName },
                        { "layerIndex", layerIndex },
                    };
                    AddPrefabFileDiff(result, beforeSnapshot, assetPath, args);
                    session.Commit();
                    return result;
                }
                catch (Exception ex)
                {
                    return new { error = $"Failed to add GameObject: {ex.Message}" };
                }
            }
        }

        /// <summary>
        /// Instantiate a prefab asset as a child inside another prefab asset.
        /// </summary>
        public static object InstantiatePrefab(Dictionary<string, object> args)
        {
            string assetPath = GetString(args, "assetPath");
            if (string.IsNullOrEmpty(assetPath))
                return new { error = "assetPath is required" };
            var beforeSnapshot = CaptureAssetText(assetPath);

            string sourcePrefabPath = GetString(args, "sourcePrefabPath");
            if (string.IsNullOrEmpty(sourcePrefabPath))
                return new { error = "sourcePrefabPath is required" };

            string parentPrefabPath = GetString(args, "parentPrefabPath");
            string name = GetString(args, "name");
            int siblingIndex = GetInt(args, "siblingIndex", -1);

            var sourcePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePrefabPath);
            if (sourcePrefab == null)
                return new { error = $"Source prefab not found at '{sourcePrefabPath}'" };

            var root = PrefabUtility.LoadPrefabContents(assetPath);
            if (root == null)
                return new { error = $"Failed to load prefab at '{assetPath}'" };

            using (var session = new PrefabMutationSession(assetPath, beforeSnapshot, root))
            {
                try
                {
                    var parent = FindInPrefab(root, parentPrefabPath);
                    if (parent == null)
                        return new { error = $"Parent '{parentPrefabPath}' not found in prefab" };

                    var instance = PrefabUtility.InstantiatePrefab(sourcePrefab, root.scene) as GameObject;
                    if (instance == null)
                        return new { error = $"Failed to instantiate prefab '{sourcePrefabPath}'" };

                    instance.transform.SetParent(parent.transform, false);

                    if (string.IsNullOrEmpty(name) == false)
                        instance.name = name;

                    if (args.ContainsKey("position"))
                        instance.transform.localPosition = ParseVector3(args["position"]);
                    if (args.ContainsKey("rotation"))
                        instance.transform.localEulerAngles = ParseVector3(args["rotation"]);
                    if (args.ContainsKey("scale"))
                        instance.transform.localScale = ParseVector3(args["scale"]);

                    if (siblingIndex >= 0)
                    {
                        instance.transform.SetSiblingIndex(
                            Mathf.Clamp(siblingIndex, 0, parent.transform.childCount - 1));
                    }

                    string prefabName = root.name;
                    string instanceName = instance.name;
                    string instancePath = GetPrefabPath(root, instance);
                    int resolvedSiblingIndex = instance.transform.GetSiblingIndex();
                    if (session.SaveAndClose() == null)
                        throw new InvalidOperationException(
                            $"SaveAsPrefabAsset returned null for '{assetPath}'.");

                    var result = new Dictionary<string, object>
                    {
                        { "success", true },
                        { "prefab", prefabName },
                        { "assetPath", assetPath },
                        { "sourcePrefabPath", sourcePrefabPath },
                        { "createdGameObject", instanceName },
                        { "prefabPath", instancePath },
                        { "parent", string.IsNullOrEmpty(parentPrefabPath) ? "root" : parentPrefabPath },
                        { "siblingIndex", resolvedSiblingIndex },
                    };
                    AddPrefabFileDiff(result, beforeSnapshot, assetPath, args);
                    session.Commit();
                    return result;
                }
                catch (Exception ex)
                {
                    return new { error = $"Failed to instantiate prefab: {ex.Message}" };
                }
            }
        }

        /// <summary>
        /// Delete a child GameObject from a prefab asset.
        /// Cannot delete the root GameObject.
        /// </summary>
        public static object RemoveGameObject(Dictionary<string, object> args)
        {
            string assetPath = GetString(args, "assetPath");
            if (string.IsNullOrEmpty(assetPath))
                return new { error = "assetPath is required" };
            var beforeSnapshot = CaptureAssetText(assetPath);

            string prefabPath = GetString(args, "prefabPath");
            if (string.IsNullOrEmpty(prefabPath))
                return new { error = "prefabPath is required (cannot delete root)" };

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

                    if (go == root)
                        return new { error = "Cannot delete the root GameObject of a prefab" };

                    string prefabName = root.name;
                    string deletedName = go.name;
                    UnityEngine.Object.DestroyImmediate(go);
                    if (session.SaveAndClose() == null)
                        throw new InvalidOperationException(
                            $"SaveAsPrefabAsset returned null for '{assetPath}'.");

                    var result = new Dictionary<string, object>
                    {
                        { "success", true },
                        { "prefab", prefabName },
                        { "deletedGameObject", deletedName },
                        { "prefabPath", prefabPath },
                    };
                    AddPrefabFileDiff(result, beforeSnapshot, assetPath, args);
                    session.Commit();
                    return result;
                }
                catch (Exception ex)
                {
                    return new { error = $"Failed to remove GameObject: {ex.Message}" };
                }
            }
        }

        /// <summary>
        /// Move or reorder a child GameObject inside a prefab asset.
        /// </summary>
        public static object MoveGameObject(Dictionary<string, object> args)
        {
            string assetPath = GetString(args, "assetPath");
            if (string.IsNullOrEmpty(assetPath))
                return new { error = "assetPath is required" };
            var beforeSnapshot = CaptureAssetText(assetPath);

            string prefabPath = GetString(args, "prefabPath");
            if (string.IsNullOrEmpty(prefabPath))
                return new { error = "prefabPath is required (cannot move root)" };

            string newParentPrefabPath = GetString(args, "newParentPrefabPath");
            int siblingIndex = GetInt(args, "siblingIndex", -1);
            bool worldPositionStays = GetBool(args, "worldPositionStays", false);

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

                    if (go == root)
                        return new { error = "Cannot move the root GameObject of a prefab" };

                    var newParent = FindInPrefab(root, newParentPrefabPath);
                    if (newParent == null)
                        return new { error = $"New parent '{newParentPrefabPath}' not found in prefab" };

                    string prefabName = root.name;
                    string oldPath = GetPrefabPath(root, go);
                    string oldParentPath = go.transform.parent != null
                        ? GetPrefabPath(root, go.transform.parent.gameObject)
                        : "";
                    int oldSiblingIndex = go.transform.GetSiblingIndex();

                    go.transform.SetParent(newParent.transform, worldPositionStays);
                    if (siblingIndex >= 0)
                    {
                        go.transform.SetSiblingIndex(
                            Mathf.Clamp(siblingIndex, 0, newParent.transform.childCount - 1));
                    }

                    string newPath = GetPrefabPath(root, go);
                    int newSiblingIndex = go.transform.GetSiblingIndex();
                    if (session.SaveAndClose() == null)
                        throw new InvalidOperationException(
                            $"SaveAsPrefabAsset returned null for '{assetPath}'.");

                    var result = new Dictionary<string, object>
                    {
                        { "success", true },
                        { "prefab", prefabName },
                        { "assetPath", assetPath },
                        { "oldPath", oldPath },
                        { "newPath", newPath },
                        { "oldParent", oldParentPath },
                        { "newParent", string.IsNullOrEmpty(newParentPrefabPath) ? "root" : newParentPrefabPath },
                        { "oldSiblingIndex", oldSiblingIndex },
                        { "newSiblingIndex", newSiblingIndex },
                    };
                    AddPrefabFileDiff(result, beforeSnapshot, assetPath, args);
                    session.Commit();
                    return result;
                }
                catch (Exception ex)
                {
                    return new { error = $"Failed to move GameObject: {ex.Message}" };
                }
            }
        }

        /// <summary>
        /// Find GameObjects in a prefab asset by name/path, component type, and optional serialized property value.
        /// </summary>
        public static object Find(Dictionary<string, object> args)
        {
            string assetPath = GetString(args, "assetPath");
            if (string.IsNullOrEmpty(assetPath))
                return new { error = "assetPath is required" };

            string name = GetString(args, "name");
            string nameContains = GetString(args, "nameContains");
            string pathContains = GetString(args, "pathContains");
            string componentType = GetString(args, "componentType");
            string propertyName = GetString(args, "propertyName");
            bool hasPropertyValue = args != null && args.ContainsKey("propertyValue");
            object propertyValue = hasPropertyValue ? args["propertyValue"] : null;
            int maxResults = GetInt(args, "maxResults", 50);

            Type type = null;
            if (string.IsNullOrEmpty(componentType) == false)
            {
                type = VmAutomationComponentCommands.FindType(componentType);
                if (type == null)
                    return new { error = $"Type '{componentType}' not found" };
            }

            var root = PrefabUtility.LoadPrefabContents(assetPath);
            if (root == null)
                return new { error = $"Failed to load prefab at '{assetPath}'" };

            try
            {
                var results = new List<Dictionary<string, object>>();
                bool truncated = false;

                foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                {
                    var go = transform.gameObject;
                    string prefabPath = GetPrefabPath(root, go);

                    if (string.IsNullOrEmpty(name) == false && go.name != name)
                        continue;
                    if (string.IsNullOrEmpty(nameContains) == false &&
                        go.name.IndexOf(nameContains, StringComparison.OrdinalIgnoreCase) < 0)
                        continue;
                    if (string.IsNullOrEmpty(pathContains) == false &&
                        prefabPath.IndexOf(pathContains, StringComparison.OrdinalIgnoreCase) < 0)
                        continue;

                    var components = type != null ? go.GetComponents(type) : go.GetComponents<Component>();
                    bool requiresComponentMatch = type != null || string.IsNullOrEmpty(propertyName) == false;

                    if (requiresComponentMatch == false)
                    {
                        if (TryAddFindResult(results, maxResults, ref truncated, root, go, null, null, null))
                            continue;
                        break;
                    }

                    foreach (var component in components)
                    {
                        if (component == null)
                            continue;

                        object matchedValue = null;

                        if (string.IsNullOrEmpty(propertyName) == false)
                        {
                            using (var serialized = new SerializedObject(component))
                            {
                                var matchedProperty = serialized.FindProperty(propertyName);
                                if (matchedProperty == null)
                                    continue;

                                matchedValue = VmAutomationComponentCommands.GetSerializedValue(matchedProperty);
                                if (hasPropertyValue &&
                                    SerializedValueMatches(matchedValue, propertyValue) == false)
                                    continue;
                            }
                        }

                        if (TryAddFindResult(results, maxResults, ref truncated, root, go, component,
                                propertyName, matchedValue))
                            continue;

                        return BuildFindResponse(root, assetPath, results, truncated);
                    }
                }

                return BuildFindResponse(root, assetPath, results, truncated);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        // ─── Variant Management ───

        /// <summary>
        /// Get variant info for a prefab: is it a variant? what's the base? Also list all known variants of a base prefab.
        /// </summary>
    }
}
