using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace VMUnityAutomation.Editor
{
    internal sealed class MCPVFXComponentTarget : IDisposable
    {
        private readonly GameObject prefabRoot;
        private bool disposed;

        private MCPVFXComponentTarget(Component component, string scope,
            string prefabPath, GameObject prefabRoot)
        {
            Component = component;
            Scope = scope;
            PrefabPath = prefabPath;
            this.prefabRoot = prefabRoot;
        }

        internal Component Component { get; }
        internal string Scope { get; }
        internal string PrefabPath { get; }
        internal bool IsPrefab => prefabRoot != null;

        internal static bool TryResolve(Dictionary<string, object> selector,
            bool allowPrefab, out MCPVFXComponentTarget target, out object error)
        {
            target = null;
            Type visualEffectType = MCPVFXReflection.FindType(
                MCPVFXReflection.VisualEffectTypeName);
            if (visualEffectType == null)
            {
                error = MCPResponse.Error(
                    "VFX Graph is not available. Install com.unity.visualeffectgraph.",
                    "capability_unavailable");
                return false;
            }
            string prefabPath = GetString(selector, "prefabPath");
            foreach (string key in new[]
                     {
                         "prefabPath", "scenePath", "hierarchyPath",
                         "hierarchyIndexPath",
                     })
            {
                if (GetString(selector, key).Length >
                    MCPVFXLimits.SelectorCharacters)
                {
                    error = MCPResponse.Error(
                        $"{key} cannot exceed {MCPVFXLimits.SelectorCharacters} characters.",
                        "invalid_arguments");
                    return false;
                }
            }
            if (!string.IsNullOrEmpty(prefabPath) &&
                !MCPVFXAssetPath.TryNormalizeFile(prefabPath, false,
                    out prefabPath, out string prefabPathError))
            {
                error = MCPResponse.Error(prefabPathError,
                    "invalid_arguments");
                return false;
            }
            bool hasComponentInstanceId = selector != null &&
                selector.ContainsKey("componentInstanceId");
            bool hasGameObjectInstanceId = selector != null &&
                selector.ContainsKey("gameObjectInstanceId");
            if (!string.IsNullOrEmpty(prefabPath) &&
                (hasComponentInstanceId || hasGameObjectInstanceId))
            {
                error = MCPResponse.Error(
                    "Loaded object instance selectors cannot be combined with prefabPath.",
                    "invalid_arguments");
                return false;
            }
            GameObject root = null;
            try
            {
                if (!string.IsNullOrEmpty(prefabPath))
                {
                    if (!allowPrefab)
                    {
                        error = MCPResponse.Error(
                            "prefabPath is not valid for runtime VFX component control.",
                            "invalid_arguments");
                        return false;
                    }
                    if (!prefabPath.EndsWith(".prefab",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        error = MCPResponse.Error(
                            "prefabPath must identify a .prefab below Assets/.",
                            "invalid_arguments");
                        return false;
                    }
                    if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) ==
                        null)
                    {
                        error = MCPResponse.Error(
                            $"Prefab '{prefabPath}' was not found.",
                            "asset_not_found");
                        return false;
                    }
                    root = PrefabUtility.LoadPrefabContents(prefabPath);
                    if (root == null)
                    {
                        error = MCPResponse.Error(
                            $"Prefab '{prefabPath}' could not be loaded.",
                            "asset_not_found");
                        return false;
                    }
                }

                Component exactComponent = null;
                GameObject gameObject;
                if (root != null)
                {
                    List<GameObject> matches = FindPrefabGameObjects(root,
                        selector);
                    if (matches.Count > 1)
                    {
                        PrefabUtility.UnloadPrefabContents(root);
                        error = MCPResponse.Error(
                            "The prefab GameObject selector matched multiple objects. Use hierarchyIndexPath.",
                            "game_object_selector_ambiguous");
                        return false;
                    }
                    gameObject = matches.SingleOrDefault();
                }
                else if (selector != null && selector.TryGetValue(
                             "componentInstanceId", out object componentId))
                {
                    exactComponent = VmObjectId.ToObject(componentId) as Component;
                    if (exactComponent == null ||
                        !visualEffectType.IsInstanceOfType(exactComponent))
                    {
                        error = MCPResponse.Error(
                            "componentInstanceId does not identify a loaded VisualEffect component.",
                            "component_not_found");
                        return false;
                    }
                    gameObject = exactComponent.gameObject;
                    if (hasGameObjectInstanceId &&
                        VmObjectId.ToObject(selector["gameObjectInstanceId"]) !=
                        gameObject)
                    {
                        error = MCPResponse.Error(
                            "componentInstanceId and gameObjectInstanceId identify different objects.",
                            "component_selector_mismatch");
                        return false;
                    }
                }
                else if (selector != null && selector.TryGetValue(
                             "gameObjectInstanceId", out object gameObjectId))
                {
                    gameObject = VmObjectId.ToObject(gameObjectId) as GameObject;
                }
                else
                {
                    List<GameObject> matches = FindLoadedGameObjects(selector);
                    if (matches.Count > 1)
                    {
                        error = MCPResponse.Error(
                            "The loaded GameObject selector matched multiple objects. Add scenePath or use hierarchyIndexPath, gameObjectInstanceId, or componentInstanceId.",
                            "game_object_selector_ambiguous");
                        return false;
                    }
                    gameObject = matches.SingleOrDefault();
                }
                if (gameObject == null)
                {
                    if (root != null)
                        PrefabUtility.UnloadPrefabContents(root);
                    error = MCPResponse.Error(
                        "The selected VFX GameObject was not found.",
                        "game_object_not_found");
                    return false;
                }
                string requestedHierarchy = GetString(selector, "hierarchyPath");
                if (!string.IsNullOrEmpty(requestedHierarchy) &&
                    !string.Equals(HierarchyPath(gameObject),
                        requestedHierarchy.Trim('/'), StringComparison.Ordinal))
                {
                    if (root != null)
                        PrefabUtility.UnloadPrefabContents(root);
                    error = MCPResponse.Error(
                        $"GameObject hierarchy path is '{HierarchyPath(gameObject)}', not requested '{requestedHierarchy}'.",
                        "game_object_selector_mismatch");
                    return false;
                }
                string requestedIndexPath = GetString(selector,
                    "hierarchyIndexPath");
                if (!string.IsNullOrEmpty(requestedIndexPath) &&
                    !string.Equals(HierarchyIndexPath(gameObject),
                        requestedIndexPath.Trim('/'), StringComparison.Ordinal))
                {
                    if (root != null)
                        PrefabUtility.UnloadPrefabContents(root);
                    error = MCPResponse.Error(
                        $"GameObject hierarchy index path is '{HierarchyIndexPath(gameObject)}', not requested '{requestedIndexPath}'.",
                        "game_object_selector_mismatch");
                    return false;
                }
                string requestedScene = GetString(selector, "scenePath");
                if (root == null && !string.IsNullOrEmpty(requestedScene) &&
                    !string.Equals(gameObject.scene.path, requestedScene,
                        StringComparison.Ordinal))
                {
                    error = MCPResponse.Error(
                        $"GameObject belongs to scene '{gameObject.scene.path}', not requested '{requestedScene}'.",
                        "scene_mismatch");
                    return false;
                }
                Component[] components = gameObject.GetComponents(visualEffectType);
                int componentIndex = GetInt(selector, "componentIndex", 0);
                if (exactComponent != null)
                {
                    int exactIndex = Array.IndexOf(components, exactComponent);
                    if (selector.ContainsKey("componentIndex") &&
                        componentIndex != exactIndex)
                    {
                        error = MCPResponse.Error(
                            $"componentInstanceId resolves to component index {exactIndex}, not requested {componentIndex}.",
                            "component_selector_mismatch");
                        return false;
                    }
                    componentIndex = exactIndex;
                }
                if (componentIndex < 0 || componentIndex >= components.Length)
                {
                    if (root != null)
                        PrefabUtility.UnloadPrefabContents(root);
                    error = MCPResponse.Error(
                        $"VisualEffect componentIndex {componentIndex} is out of range; '{HierarchyPath(gameObject)}' has {components.Length} VisualEffect components.",
                        "component_not_found");
                    return false;
                }
                target = new MCPVFXComponentTarget(exactComponent ??
                    components[componentIndex],
                    root != null ? "prefab" : "scene", prefabPath, root);
                error = null;
                return true;
            }
            catch (Exception exception)
            {
                if (root != null)
                    PrefabUtility.UnloadPrefabContents(root);
                Exception failure = MCPVFXReflection.Unwrap(exception);
                error = MCPResponse.Error(failure.Message,
                    MCPVFXError.Code(failure,
                        "component_resolution_failed"));
                return false;
            }
        }

        internal void Save(params UnityEngine.Object[] additionalObjects)
        {
            if (IsPrefab)
            {
                PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath);
                AssetDatabase.ImportAsset(PrefabPath,
                    ImportAssetOptions.ForceUpdate |
                    ImportAssetOptions.ForceSynchronousImport);
                return;
            }
            EditorUtility.SetDirty(Component);
            foreach (UnityEngine.Object additional in additionalObjects ??
                         Array.Empty<UnityEngine.Object>())
            {
                if (additional != null && additional != Component)
                    EditorUtility.SetDirty(additional);
            }
            if (Component.gameObject.scene.IsValid())
                EditorSceneManager.MarkSceneDirty(Component.gameObject.scene);
        }

        internal UnityEngine.Object ResolveObjectReference(object rawValue,
            Type targetType, string valuePath)
        {
            if (rawValue == null)
                return null;
            if (!(rawValue is Dictionary<string, object> reference))
                throw new ArgumentException(
                    $"{valuePath} must be null or an object reference selector.");
            var allowed = new HashSet<string>(new[]
            {
                "clear", "gameObjectInstanceId", "componentInstanceId",
                "hierarchyIndexPath", "componentIndex",
            }, StringComparer.Ordinal);
            string unknown = reference.Keys.FirstOrDefault(key =>
                !allowed.Contains(key));
            if (unknown != null)
                throw new ArgumentException(
                    $"{valuePath}.{unknown} is not part of the component reference contract.");

            bool clear = reference.TryGetValue("clear", out object clearValue) &&
                         (bool)MCPVFXValueCodec.ConvertTo(clearValue,
                             typeof(bool), valuePath + ".clear");
            int selectorCount = new[]
                {
                    "gameObjectInstanceId", "componentInstanceId",
                    "hierarchyIndexPath",
                }.Count(reference.ContainsKey);
            if (clear)
            {
                if (reference.Count != 1)
                    throw new ArgumentException(
                        $"{valuePath} clear=true cannot be combined with another field.");
                return null;
            }
            if (selectorCount != 1)
                throw new ArgumentException(
                    $"{valuePath} must contain exactly one of gameObjectInstanceId, componentInstanceId, or hierarchyIndexPath.");
            if (IsPrefab && (reference.ContainsKey("gameObjectInstanceId") ||
                             reference.ContainsKey("componentInstanceId")))
                throw new ArgumentException(
                    $"{valuePath} prefab references require hierarchyIndexPath.");

            if (reference.TryGetValue("componentInstanceId",
                    out object componentInstanceId))
            {
                Component exact = VmObjectId.ToObject(componentInstanceId) as
                    Component;
                if (exact == null || !targetType.IsInstanceOfType(exact))
                    throw MCPVFXError.Create("component_not_found",
                        $"{valuePath}.componentInstanceId does not identify a loaded {targetType.FullName} component.");
                RequireSameScope(exact.gameObject, valuePath);
                return exact;
            }

            GameObject gameObject;
            if (reference.TryGetValue("gameObjectInstanceId",
                    out object gameObjectInstanceId))
            {
                gameObject = VmObjectId.ToObject(gameObjectInstanceId) as
                    GameObject;
            }
            else
            {
                string indexPath = reference["hierarchyIndexPath"]?.ToString();
                if (string.IsNullOrWhiteSpace(indexPath) || indexPath.Length >
                    MCPVFXLimits.SelectorCharacters)
                    throw new ArgumentException(
                        $"{valuePath}.hierarchyIndexPath is required and cannot exceed {MCPVFXLimits.SelectorCharacters} characters.");
                GameObject[] roots = IsPrefab
                    ? new[] { prefabRoot }
                    : Component.gameObject.scene.GetRootGameObjects();
                gameObject = FindByIndexPath(roots, indexPath);
            }
            if (gameObject == null)
                throw MCPVFXError.Create("game_object_not_found",
                    $"{valuePath} object reference was not found.");
            RequireSameScope(gameObject, valuePath);
            if (targetType == typeof(GameObject))
                return gameObject;
            if (targetType == typeof(Transform))
                return gameObject.transform;
            if (!typeof(Component).IsAssignableFrom(targetType))
                throw new ArgumentException(
                    $"{valuePath} target type {targetType.FullName} is not a supported scene object reference.");
            Component[] components = gameObject.GetComponents(targetType);
            int componentIndex;
            if (reference.TryGetValue("componentIndex", out object rawIndex))
            {
                componentIndex = (int)MCPVFXValueCodec.ConvertTo(rawIndex,
                    typeof(int), valuePath + ".componentIndex");
            }
            else
            {
                if (components.Length > 1)
                    throw MCPVFXError.Create("component_selector_ambiguous",
                        $"{valuePath} matches {components.Length} {targetType.FullName} components; componentIndex is required.");
                componentIndex = 0;
            }
            if (componentIndex < 0 || componentIndex >= components.Length)
                throw MCPVFXError.Create("component_not_found",
                    $"{valuePath} componentIndex {componentIndex} is out of range for {targetType.FullName}.");
            return components[componentIndex];
        }

        private void RequireSameScope(GameObject gameObject, string valuePath)
        {
            bool sameScope = IsPrefab
                ? gameObject.transform.root == prefabRoot.transform
                : gameObject.scene == Component.gameObject.scene;
            if (!sameScope)
                throw MCPVFXError.Create("component_selector_mismatch",
                    $"{valuePath} must reference an object in the same scene or prefab contents as the VisualEffect component.");
        }

        internal Dictionary<string, object> Identity()
        {
            Type visualEffectType = MCPVFXReflection.FindType(
                MCPVFXReflection.VisualEffectTypeName);
            Component[] siblings = Component.gameObject.GetComponents(visualEffectType);
            return new Dictionary<string, object>
            {
                { "scope", Scope }, { "prefabPath", PrefabPath ?? "" },
                { "scenePath", IsPrefab ? "" : Component.gameObject.scene.path },
                { "hierarchyPath", HierarchyPath(Component.gameObject) },
                { "hierarchyIndexPath", HierarchyIndexPath(
                    Component.gameObject) },
                { "componentIndex", Array.IndexOf(siblings, Component) },
                { "gameObjectInstanceId", VmObjectId.Get(Component.gameObject) },
                { "componentInstanceId", VmObjectId.Get(Component) },
            };
        }

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            if (prefabRoot != null)
                PrefabUtility.UnloadPrefabContents(prefabRoot);
        }

        internal static string HierarchyPath(GameObject gameObject)
        {
            if (gameObject == null)
                return "";
            var names = new Stack<string>();
            Transform current = gameObject.transform;
            int depth = 0;
            while (current != null)
            {
                if (depth++ >= MCPVFXLimits.HierarchyDepth)
                    throw MCPVFXError.Create("response_too_large",
                        $"GameObject hierarchy exceeds depth {MCPVFXLimits.HierarchyDepth}.");
                names.Push(current.name);
                current = current.parent;
            }
            string result = string.Join("/", names);
            if (result.Length > MCPVFXLimits.SelectorCharacters)
                throw MCPVFXError.Create("response_too_large",
                    $"GameObject hierarchy path exceeds {MCPVFXLimits.SelectorCharacters} characters.");
            return result;
        }

        internal static string HierarchyIndexPath(GameObject gameObject)
        {
            if (gameObject == null)
                return "";
            var indices = new Stack<int>();
            Transform current = gameObject.transform;
            int depth = 0;
            while (current != null)
            {
                if (depth++ >= MCPVFXLimits.HierarchyDepth)
                    throw MCPVFXError.Create("response_too_large",
                        $"GameObject hierarchy exceeds depth {MCPVFXLimits.HierarchyDepth}.");
                indices.Push(current.GetSiblingIndex());
                current = current.parent;
            }
            string result = string.Join("/", indices);
            if (result.Length > MCPVFXLimits.SelectorCharacters)
                throw MCPVFXError.Create("response_too_large",
                    $"GameObject hierarchy index path exceeds {MCPVFXLimits.SelectorCharacters} characters.");
            return result;
        }

        internal static IEnumerable<Component> EnumerateLoadedComponents(
            string scenePath)
        {
            Type type = MCPVFXReflection.FindType(
                MCPVFXReflection.VisualEffectTypeName);
            if (type == null)
                yield break;
            var gameObjects = new List<GameObject>();
            for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount;
                 sceneIndex++)
            {
                Scene scene = SceneManager.GetSceneAt(sceneIndex);
                if (!scene.isLoaded || (!string.IsNullOrEmpty(scenePath) &&
                    !string.Equals(scene.path, scenePath, StringComparison.Ordinal)))
                    continue;
                foreach (GameObject root in scene.GetRootGameObjects())
                    AppendHierarchy(root, gameObjects);
            }
            int componentCount = 0;
            foreach (GameObject gameObject in gameObjects)
            foreach (Component component in gameObject.GetComponents(type))
            {
                componentCount++;
                if (componentCount > MCPVFXLimits.CollectionItems)
                    throw MCPVFXError.Create("response_too_large",
                        $"Loaded scenes expose more than {MCPVFXLimits.CollectionItems} VisualEffect components.");
                    yield return component;
            }
        }

        internal static IEnumerable<Component> EnumerateComponents(
            GameObject root, Type componentType)
        {
            var gameObjects = new List<GameObject>();
            AppendHierarchy(root, gameObjects);
            List<Component> components = gameObjects.SelectMany(gameObject =>
                    gameObject.GetComponents(componentType).Cast<Component>())
                .Take(MCPVFXLimits.CollectionItems + 1).ToList();
            if (components.Count > MCPVFXLimits.CollectionItems)
                throw MCPVFXError.Create("response_too_large",
                    $"Prefab exposes more than {MCPVFXLimits.CollectionItems} VisualEffect components.");
            return components;
        }

        private static List<GameObject> FindPrefabGameObjects(GameObject root,
            Dictionary<string, object> selector)
        {
            string hierarchyPath = GetString(selector, "hierarchyPath");
            string indexPath = GetString(selector, "hierarchyIndexPath");
            if (!string.IsNullOrEmpty(indexPath))
            {
                GameObject indexed = FindByIndexPath(new[] { root }, indexPath);
                return indexed != null ? new List<GameObject> { indexed } :
                    new List<GameObject>();
            }
            if (string.IsNullOrEmpty(hierarchyPath))
                return new List<GameObject> { root };
            int inspected = 0;
            return FindInHierarchy(root, hierarchyPath, ref inspected);
        }

        private static List<GameObject> FindLoadedGameObjects(
            Dictionary<string, object> selector)
        {
            string hierarchyPath = GetString(selector, "hierarchyPath");
            string indexPath = GetString(selector, "hierarchyIndexPath");
            string scenePath = GetString(selector, "scenePath");
            if (string.IsNullOrEmpty(hierarchyPath) &&
                string.IsNullOrEmpty(indexPath))
                return new List<GameObject>();
            var result = new List<GameObject>();
            int inspected = 0;
            for (int sceneIndex = 0; sceneIndex < SceneManager.sceneCount;
                 sceneIndex++)
            {
                Scene scene = SceneManager.GetSceneAt(sceneIndex);
                if (!scene.isLoaded || (!string.IsNullOrEmpty(scenePath) &&
                    !string.Equals(scene.path, scenePath, StringComparison.Ordinal)))
                    continue;
                GameObject[] roots = scene.GetRootGameObjects();
                if (!string.IsNullOrEmpty(indexPath))
                {
                    GameObject indexed = FindByIndexPath(roots, indexPath);
                    if (indexed != null)
                        result.Add(indexed);
                    continue;
                }
                foreach (GameObject root in roots)
                    result.AddRange(FindInHierarchy(root, hierarchyPath,
                        ref inspected));
            }
            return result;
        }

        private static List<GameObject> FindInHierarchy(GameObject root,
            string path, ref int inspected)
        {
            if (root == null)
                return new List<GameObject>();
            inspected++;
            if (inspected > MCPVFXLimits.HierarchyObjects)
                throw MCPVFXError.Create("response_too_large",
                    $"GameObject selector inspected more than {MCPVFXLimits.HierarchyObjects} hierarchy objects.");
            if (string.IsNullOrEmpty(path) || path == root.name)
                return new List<GameObject> { root };
            string normalized = path.Trim('/');
            string rootPrefix = root.name + "/";
            if (normalized.StartsWith(rootPrefix, StringComparison.Ordinal))
                normalized = normalized.Substring(rootPrefix.Length);
            var current = new List<Transform> { root.transform };
            foreach (string segment in normalized.Split('/'))
            {
                if (string.IsNullOrEmpty(segment))
                    continue;
                var next = new List<Transform>();
                foreach (Transform parent in current)
                for (int childIndex = 0; childIndex < parent.childCount;
                     childIndex++)
                {
                    inspected++;
                    if (inspected > MCPVFXLimits.HierarchyObjects)
                        throw MCPVFXError.Create("response_too_large",
                            $"GameObject selector inspected more than {MCPVFXLimits.HierarchyObjects} hierarchy objects.");
                    Transform child = parent.GetChild(childIndex);
                    if (string.Equals(child.name, segment,
                            StringComparison.Ordinal))
                        next.Add(child);
                }
                current = next;
                if (current.Count == 0)
                    break;
            }
            return current.Select(transform => transform.gameObject).ToList();
        }

        private static GameObject FindByIndexPath(
            IReadOnlyList<GameObject> roots, string path)
        {
            if (roots.Count > MCPVFXLimits.HierarchyObjects)
                throw MCPVFXError.Create("response_too_large",
                    $"Scene exposes more than {MCPVFXLimits.HierarchyObjects} root GameObjects.");
            string[] parts = path.Trim('/').Split('/');
            if (parts.Length > MCPVFXLimits.HierarchyDepth)
                throw MCPVFXError.Create("response_too_large",
                    $"hierarchyIndexPath exceeds depth {MCPVFXLimits.HierarchyDepth}.");
            if (parts.Length == 0 || !int.TryParse(parts[0], out int rootIndex) ||
                rootIndex < 0 || rootIndex >= roots.Count)
                return null;
            Transform current = roots[rootIndex].transform;
            for (int index = 1; index < parts.Length; index++)
            {
                if (!int.TryParse(parts[index], out int childIndex) ||
                    childIndex < 0 || childIndex >= current.childCount)
                    return null;
                current = current.GetChild(childIndex);
            }
            return current.gameObject;
        }

        private static void AppendHierarchy(GameObject root,
            ICollection<GameObject> result)
        {
            var pending = new Stack<Transform>();
            pending.Push(root.transform);
            while (pending.Count > 0)
            {
                Transform current = pending.Pop();
                if (result.Count >= MCPVFXLimits.HierarchyObjects)
                    throw MCPVFXError.Create("response_too_large",
                        $"Component discovery inspected more than {MCPVFXLimits.HierarchyObjects} hierarchy objects.");
                result.Add(current.gameObject);
                for (int childIndex = current.childCount - 1;
                     childIndex >= 0; childIndex--)
                    pending.Push(current.GetChild(childIndex));
            }
        }

        private static string GetString(Dictionary<string, object> values,
            string key)
        {
            return values != null && values.TryGetValue(key, out object value) &&
                   value != null ? value.ToString() : "";
        }

        private static int GetInt(Dictionary<string, object> values, string key,
            int defaultValue)
        {
            return values != null && values.TryGetValue(key, out object value) &&
                   value != null ? (int)MCPVFXValueCodec.ConvertTo(value,
                       typeof(int), key) : defaultValue;
        }
    }
}
