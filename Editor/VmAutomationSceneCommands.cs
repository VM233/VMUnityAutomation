using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace VMUnityAutomation.Editor
{
    public static class VmAutomationSceneCommands
    {
        public static object GetSceneInfo()
        {
            var scenes = new List<object>();
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                var rootObjects = new List<string>();
                foreach (var go in scene.GetRootGameObjects())
                    rootObjects.Add(go.name);

                scenes.Add(new Dictionary<string, object>
                {
                    { "name", scene.name },
                    { "path", scene.path },
                    { "isDirty", scene.isDirty },
                    { "isLoaded", scene.isLoaded },
                    { "rootObjectCount", scene.rootCount },
                    { "rootObjects", rootObjects },
                    { "buildIndex", scene.buildIndex },
                });
            }

            return new Dictionary<string, object>
            {
                { "activeScene", SceneManager.GetActiveScene().name },
                { "sceneCount", SceneManager.sceneCount },
                { "scenes", scenes },
            };
        }

        public static object OpenScene(Dictionary<string, object> args)
        {
            string path = NormalizeAssetPath(args != null && args.ContainsKey("path")
                ? args["path"]?.ToString()
                : "");
            if (string.IsNullOrEmpty(path))
                return VmAutomationResponse.Error("path is required", "invalid_scene_path");
            if (!path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase) ||
                AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
            {
                return VmAutomationResponse.Error(
                    $"Scene asset was not found at '{path}'.",
                    "scene_not_found");
            }

            Scene activeScene = SceneManager.GetActiveScene();
            if (activeScene.IsValid() && activeScene.isLoaded &&
                string.Equals(NormalizeAssetPath(activeScene.path), path,
                    StringComparison.OrdinalIgnoreCase))
            {
                return new Dictionary<string, object>
                {
                    { "success", true },
                    { "name", activeScene.name },
                    { "path", activeScene.path },
                    { "alreadyOpen", true },
                };
            }

            if (TryBuildDirtySceneTransitionError("open another scene", path,
                    out object dirtySceneError))
                return dirtySceneError;

            try
            {
                var openedScene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                return new { success = true, name = openedScene.name, path = openedScene.path };
            }
            catch (Exception exception)
            {
                return VmAutomationResponse.Error(
                    $"Failed to open scene '{path}': {exception.Message}",
                    "scene_open_failed");
            }
        }

        public static object SaveScene(Dictionary<string, object> args = null)
        {
            var scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
                return VmAutomationResponse.Error("There is no loaded active scene to save.", "scene_not_loaded");

            string requestedPath = NormalizeAssetPath(args != null && args.TryGetValue("path", out object pathValue)
                ? pathValue?.ToString()
                : "");
            string targetPath = string.IsNullOrEmpty(requestedPath)
                ? NormalizeAssetPath(scene.path)
                : requestedPath;
            if (string.IsNullOrEmpty(targetPath))
            {
                return VmAutomationResponse.Error(
                    "The active scene has no asset path. Provide path to save it without opening a modal dialog.",
                    "scene_path_required");
            }
            if (!targetPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) ||
                !targetPath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
            {
                return VmAutomationResponse.Error(
                    "Scene save path must be a .unity asset under Assets/.",
                    "invalid_scene_path");
            }

            string targetDirectory = NormalizeAssetPath(Path.GetDirectoryName(targetPath));
            if (!AssetDatabase.IsValidFolder(targetDirectory))
            {
                return VmAutomationResponse.Error(
                    $"Scene save directory was not found: '{targetDirectory}'.",
                    "scene_directory_not_found");
            }

            string currentPath = NormalizeAssetPath(scene.path);
            bool savingAs = !string.Equals(currentPath, targetPath, StringComparison.OrdinalIgnoreCase);
            bool overwrite = false;
            if (args != null && args.TryGetValue("overwrite", out object overwriteValue) &&
                overwriteValue != null)
            {
                try
                {
                    overwrite = Convert.ToBoolean(overwriteValue);
                }
                catch (Exception)
                {
                    return VmAutomationResponse.Error(
                        "overwrite must be a boolean.",
                        "invalid_arguments");
                }
            }
            if (savingAs && AssetDatabase.LoadAssetAtPath<SceneAsset>(targetPath) != null && !overwrite)
            {
                return VmAutomationResponse.Error(
                    $"Scene asset already exists at '{targetPath}'. Pass overwrite=true to replace it.",
                    "scene_asset_exists");
            }
            if (savingAs && VmAutomationSceneCommands.TryRejectLoadedSceneAssetMutation(
                    new[] { targetPath }, "save over another scene asset",
                    out object sceneMutationError))
                return sceneMutationError;

            try
            {
                bool saved = EditorSceneManager.SaveScene(scene, targetPath);
                if (!saved)
                    return VmAutomationResponse.Error($"Unity did not save scene '{targetPath}'.", "scene_save_failed");
                return new
                {
                    success = true,
                    scene = scene.name,
                    path = scene.path,
                    savedAs = savingAs,
                };
            }
            catch (Exception exception)
            {
                return VmAutomationResponse.Error(
                    $"Failed to save scene '{targetPath}': {exception.Message}",
                    "scene_save_failed");
            }
        }

        public static object NewScene()
        {
            if (TryBuildDirtySceneTransitionError("create a new scene", null, out object dirtySceneError))
                return dirtySceneError;

            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            return new { success = true, name = scene.name };
        }

        internal static bool TryRejectLoadedSceneAssetMutation(IEnumerable<string> assetPaths,
            string operation, out object error)
        {
            error = null;
            if (assetPaths == null)
                return false;

            var candidates = new List<string>();
            foreach (string assetPath in assetPaths)
            {
                string normalized = NormalizeAssetPath(assetPath);
                if (!string.IsNullOrEmpty(normalized))
                    candidates.Add(normalized);
            }

            if (candidates.Count == 0)
                return false;

            var affectedScenes = new List<object>();
            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                Scene scene = SceneManager.GetSceneAt(index);
                string scenePath = NormalizeAssetPath(scene.path);
                if (!scene.IsValid() || !scene.isLoaded || string.IsNullOrEmpty(scenePath))
                    continue;

                bool affected = false;
                foreach (string candidate in candidates)
                {
                    if (string.Equals(candidate, scenePath, StringComparison.OrdinalIgnoreCase) ||
                        scenePath.StartsWith(candidate.TrimEnd('/') + "/",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        affected = true;
                        break;
                    }
                }

                if (affected)
                    affectedScenes.Add(BuildSceneState(scene));
            }

            if (affectedScenes.Count == 0)
                return false;

            error = VmAutomationResponse.Error(
                $"Cannot {operation} because it would modify a loaded scene asset. " +
                "Use scene, GameObject, component, or SerializedObject APIs, save explicitly, and unload or " +
                "switch away from the scene before performing asset-level changes.",
                "loaded_scene_asset_mutation_blocked",
                false,
                new Dictionary<string, object>
                {
                    { "operation", operation },
                    { "loadedScenes", affectedScenes },
                });
            return true;
        }

        private static bool TryBuildDirtySceneTransitionError(string operation, string requestedPath,
            out object error)
        {
            var dirtyScenes = new List<object>();
            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                Scene scene = SceneManager.GetSceneAt(index);
                if (scene.IsValid() && scene.isLoaded && scene.isDirty)
                    dirtyScenes.Add(BuildSceneState(scene));
            }

            if (dirtyScenes.Count == 0)
            {
                error = null;
                return false;
            }

            var extra = new Dictionary<string, object>
            {
                { "dirtyScenes", dirtyScenes },
            };
            if (!string.IsNullOrEmpty(requestedPath))
                extra["requestedScenePath"] = requestedPath;

            error = VmAutomationResponse.Error(
                $"Cannot {operation} while one or more loaded scenes have unsaved changes. " +
                "Call scene/save explicitly, or let the user decide how to handle the changes outside Automation, " +
                "then retry. Automation will not open a modal save or reload dialog.",
                "dirty_scenes_require_explicit_save",
                false,
                extra);
            return true;
        }

        private static Dictionary<string, object> BuildSceneState(Scene scene)
        {
            return new Dictionary<string, object>
            {
                { "name", scene.name },
                { "path", scene.path },
                { "isDirty", scene.isDirty },
                { "isActive", scene == SceneManager.GetActiveScene() },
            };
        }

        private static string NormalizeAssetPath(string path)
        {
            return (path ?? "").Trim().Replace('\\', '/');
        }

        public static object GetHierarchy(Dictionary<string, object> args)
        {
            int maxDepth = 10;
            if (args != null && args.ContainsKey("maxDepth"))
                maxDepth = System.Convert.ToInt32(args["maxDepth"]);
            maxDepth = Math.Max(0, Math.Min(maxDepth, 50));

            // Keep the default response small; callers can explore subtrees with parentPath.
            int maxNodes = 250;
            if (args != null && args.ContainsKey("maxNodes"))
                maxNodes = System.Convert.ToInt32(args["maxNodes"]);
            maxNodes = Math.Max(1, Math.Min(maxNodes, 2000));

            // Optional: only return hierarchy under a specific parent path
            string parentPath = null;
            if (args != null && args.ContainsKey("parentPath"))
                parentPath = args["parentPath"]?.ToString();

            var scene = SceneManager.GetActiveScene();
            var rootObjects = scene.GetRootGameObjects();

            // Count total objects in scene for metadata
            int totalSceneObjects = CountAllObjects(rootObjects);

            // If parentPath specified, find that object and only return its subtree
            GameObject[] startObjects = rootObjects;
            if (!string.IsNullOrEmpty(parentPath))
            {
                var found = GameObject.Find(parentPath);
                if (found == null)
                    return new Dictionary<string, object>
                    {
                        { "error", $"GameObject not found at path: {parentPath}" },
                    };
                startObjects = new[] { found };
            }

            string componentTypeName = args != null && args.TryGetValue("componentType", out var componentTypeValue)
                ? componentTypeValue?.ToString()
                : null;
            if (!string.IsNullOrEmpty(componentTypeName))
            {
                Type componentType = VmAutomationComponentCommands.FindType(componentTypeName);
                if (componentType == null || !typeof(Component).IsAssignableFrom(componentType))
                    return new Dictionary<string, object> { { "error", $"Component type not found: {componentTypeName}" } };

                int maxResults = args.ContainsKey("maxResults")
                    ? Math.Max(1, Math.Min(Convert.ToInt32(args["maxResults"]), 200))
                    : Math.Max(1, Math.Min(maxNodes, 50));
                int offset = args.ContainsKey("offset")
                    ? Math.Max(0, Convert.ToInt32(args["offset"]))
                    : 0;
                string nameContains = args.TryGetValue("nameContains", out var nameValue)
                    ? nameValue?.ToString()
                    : null;
                string pathContains = args.TryGetValue("pathContains", out var pathValue)
                    ? pathValue?.ToString()
                    : null;

                var matches = new List<object>();
                int totalMatches = 0;
                foreach (var root in startObjects)
                {
                    CollectComponentMatches(root, componentType, nameContains, pathContains, offset, maxResults,
                        matches, ref totalMatches);
                }

                int nextOffset = offset + matches.Count;

                var filteredResult = new Dictionary<string, object>
                {
                    { "scene", scene.name },
                    { "filtered", true },
                    { "componentType", componentType.FullName },
                    { "matches", matches },
                    { "matchCount", matches.Count },
                    { "totalMatches", totalMatches },
                    { "offset", offset },
                    { "maxResults", maxResults },
                    { "truncated", nextOffset < totalMatches },
                    { "hasMore", nextOffset < totalMatches },
                    { "nextOffset", nextOffset < totalMatches ? (object)nextOffset : null },
                    { "totalSceneObjects", totalSceneObjects },
                };
                if (!string.IsNullOrEmpty(parentPath))
                    filteredResult["parentPath"] = parentPath;
                if (!string.IsNullOrEmpty(nameContains))
                    filteredResult["nameContains"] = nameContains;
                if (!string.IsNullOrEmpty(pathContains))
                    filteredResult["pathContains"] = pathContains;
                return filteredResult;
            }

            int nodeCount = 0;
            var hierarchy = new List<object>();
            int totalAvailableNodes = CountAllObjects(startObjects);

            foreach (var root in startObjects)
            {
                var node = BuildHierarchyNode(root, 0, maxDepth, ref nodeCount, maxNodes);
                if (node != null)
                    hierarchy.Add(node);
                if (nodeCount >= maxNodes)
                    break;
            }

            var result = new Dictionary<string, object>
            {
                { "scene", scene.name },
                { "hierarchy", hierarchy },
                { "totalSceneObjects", totalSceneObjects },
                { "totalAvailableNodes", totalAvailableNodes },
                { "returnedNodes", nodeCount },
                { "maxNodes", maxNodes },
            };

            if (nodeCount < totalAvailableNodes)
            {
                result["truncated"] = true;
                result["message"] = $"Hierarchy truncated at {nodeCount} nodes ({totalAvailableNodes} available under the selected root). " +
                    "Use parentPath to explore specific subtrees, or increase maxNodes.";
            }
            else
            {
                result["truncated"] = false;
            }

            if (!string.IsNullOrEmpty(parentPath))
                result["parentPath"] = parentPath;

            return result;
        }

        private static void CollectComponentMatches(GameObject go, Type componentType, string nameContains,
            string pathContains, int offset, int maxResults, List<object> matches, ref int totalMatches)
        {
            string path = GetGameObjectPath(go.transform);
            bool nameMatches = string.IsNullOrEmpty(nameContains) ||
                               go.name.IndexOf(nameContains, StringComparison.OrdinalIgnoreCase) >= 0;
            bool pathMatches = string.IsNullOrEmpty(pathContains) ||
                               path.IndexOf(pathContains, StringComparison.OrdinalIgnoreCase) >= 0;
            if (nameMatches && pathMatches && go.GetComponent(componentType) != null)
            {
                int matchIndex = totalMatches++;
                if (matchIndex >= offset && matches.Count < maxResults)
                {
                    var components = new List<string>();
                    foreach (var component in go.GetComponents<Component>())
                    {
                        if (VmAutomationComponentCommands.ShouldIncludeInComponentSummary(component))
                            components.Add(component.GetType().Name);
                    }

                    var match = new Dictionary<string, object>
                    {
                        { "name", go.name },
                        { "path", path },
                        { "instanceId", VmObjectId.Get(go) },
                        { "active", go.activeSelf },
                        { "components", components },
                    };
                    VmAutomationTransformSerialization.AddWorld(match, go.transform);
                    matches.Add(match);
                }
            }

            foreach (Transform child in go.transform)
                CollectComponentMatches(child.gameObject, componentType, nameContains, pathContains,
                    offset, maxResults, matches, ref totalMatches);
        }

        private static string GetGameObjectPath(Transform transform)
        {
            var names = new Stack<string>();
            while (transform != null)
            {
                names.Push(transform.name);
                transform = transform.parent;
            }
            return string.Join("/", names);
        }

        /// <summary>Count all GameObjects recursively without building the full hierarchy.</summary>
        private static int CountAllObjects(GameObject[] roots)
        {
            int count = 0;
            foreach (var root in roots)
                CountRecursive(root, ref count);
            return count;
        }

        private static void CountRecursive(GameObject go, ref int count)
        {
            count++;
            foreach (Transform child in go.transform)
                CountRecursive(child.gameObject, ref count);
        }

        private static Dictionary<string, object> BuildHierarchyNode(
            GameObject go, int depth, int maxDepth, ref int nodeCount, int maxNodes)
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
                { "instanceId", VmObjectId.Get(go) },
                { "active", go.activeSelf },
                { "tag", go.tag },
                { "layer", LayerMask.LayerToName(go.layer) },
                { "components", components },
            };
            VmAutomationTransformSerialization.AddWorld(node, go.transform);

            if (depth < maxDepth && go.transform.childCount > 0)
            {
                var children = new List<object>();
                for (int i = 0; i < go.transform.childCount; i++)
                {
                    if (nodeCount >= maxNodes)
                    {
                        // Record how many children we couldn't include
                        node["childCount"] = go.transform.childCount;
                        node["childrenIncluded"] = children.Count;
                        node["childrenTruncated"] = true;
                        break;
                    }
                    var childNode = BuildHierarchyNode(go.transform.GetChild(i).gameObject, depth + 1, maxDepth, ref nodeCount, maxNodes);
                    if (childNode != null)
                        children.Add(childNode);
                }
                if (children.Count > 0)
                    node["children"] = children;
                if (!node.ContainsKey("childCount"))
                    node["childCount"] = go.transform.childCount;
            }
            else if (go.transform.childCount > 0)
            {
                node["childCount"] = go.transform.childCount;
                node["childrenTruncated"] = true;
            }

            return node;
        }

    }
}
