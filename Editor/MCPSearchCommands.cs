using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace VMUnityAutomation.Editor
{
    /// <summary>
    /// Commands for searching and finding GameObjects and assets in the project.
    /// </summary>
    public static class MCPSearchCommands
    {
        // ─── Find By Component ───

        // Default result limit for search commands — prevents oversized responses on large projects
        private const int DefaultSearchLimit = 500;

        /// <summary>
        /// Search loaded scene GameObjects with one stable, paginated AND-filter API.
        /// The older by-name/by-component/by-tag/by-layer/by-shader routes remain
        /// available through lazy discovery for compatibility.
        /// </summary>
        public static object SearchScene(Dictionary<string, object> args)
        {
            string name = GetString(args, "name");
            string componentTypeName = GetString(args, "componentType");
            string tag = GetString(args, "tag");
            string layerValue = GetString(args, "layer");
            string shader = GetString(args, "shader");
            bool useRegex = GetBool(args, "regex", false);
            bool includeInactive = GetBool(args, "includeInactive", true);
            int offset = Math.Max(0, GetInt(args, "offset", 0));
            int limit = Math.Max(1, Math.Min(GetInt(args, "limit", 200), DefaultSearchLimit));

            if (string.IsNullOrWhiteSpace(name) &&
                string.IsNullOrWhiteSpace(componentTypeName) &&
                string.IsNullOrWhiteSpace(tag) &&
                string.IsNullOrWhiteSpace(layerValue) &&
                string.IsNullOrWhiteSpace(shader))
            {
                return MCPResponse.Error(
                    "At least one of name, componentType, tag, layer, or shader is required.",
                    "search_filter_required");
            }

            Regex nameRegex = null;
            if (!string.IsNullOrWhiteSpace(name) && useRegex)
            {
                try
                {
                    nameRegex = new Regex(name, RegexOptions.IgnoreCase,
                        TimeSpan.FromMilliseconds(100));
                }
                catch (ArgumentException exception)
                {
                    return MCPResponse.Error(exception.Message, "invalid_name_regex");
                }
            }

            Type componentType = null;
            if (!string.IsNullOrWhiteSpace(componentTypeName))
            {
                var componentTypes = ResolveComponentTypes(componentTypeName);
                if (componentTypes.Count == 0)
                {
                    return MCPResponse.Error(
                        $"Component type '{componentTypeName}' was not found.",
                        "component_type_not_found");
                }
                if (componentTypes.Count > 1)
                {
                    return MCPResponse.Error(
                        $"Component type '{componentTypeName}' is ambiguous. Use a full type name.",
                        "component_type_ambiguous", false, new Dictionary<string, object>
                        {
                            { "matches", componentTypes.Select(type => type.FullName).ToList() },
                        });
                }
                componentType = componentTypes[0];
            }

            if (!string.IsNullOrWhiteSpace(tag) &&
                !InternalEditorUtility.tags.Contains(tag))
            {
                return MCPResponse.Error(
                    $"Tag '{tag}' is not defined in this project.",
                    "tag_not_found");
            }

            int layer = -1;
            if (!string.IsNullOrWhiteSpace(layerValue))
            {
                if (!int.TryParse(layerValue, out layer))
                    layer = LayerMask.NameToLayer(layerValue);
                if (layer < 0 || layer > 31)
                    return MCPResponse.Error("layer must be a valid layer name or an index from 0 to 31.", "invalid_layer");
            }

            var matches = new List<GameObject>();
            var allObjects = MCPObjectSearch.Find<GameObject>(includeInactive);
            foreach (var gameObject in allObjects)
            {
                if (!gameObject.scene.IsValid())
                    continue;

                if (!string.IsNullOrWhiteSpace(name))
                {
                    try
                    {
                        bool nameMatches = useRegex
                            ? nameRegex.IsMatch(gameObject.name)
                            : gameObject.name.IndexOf(name, StringComparison.OrdinalIgnoreCase) >= 0;
                        if (!nameMatches)
                            continue;
                    }
                    catch (RegexMatchTimeoutException)
                    {
                        return MCPResponse.Error(
                            "The name regular expression exceeded the 100 ms match budget.",
                            "name_regex_timeout");
                    }
                }

                if (componentType != null && gameObject.GetComponent(componentType) == null)
                    continue;
                if (!string.IsNullOrWhiteSpace(tag) &&
                    !string.Equals(gameObject.tag, tag, StringComparison.Ordinal))
                    continue;
                if (layer >= 0 && gameObject.layer != layer)
                    continue;
                if (!string.IsNullOrWhiteSpace(shader) && !UsesShader(gameObject, shader))
                    continue;

                matches.Add(gameObject);
            }

            var ordered = matches
                .Select(gameObject => new
                {
                    GameObject = gameObject,
                    Path = GetGameObjectPath(gameObject),
                })
                .OrderBy(item => item.GameObject.scene.name, StringComparer.Ordinal)
                .ThenBy(item => item.Path, StringComparer.Ordinal)
                .ThenBy(item => VmObjectId.Get(item.GameObject), StringComparer.Ordinal)
                .ToList();
            var results = ordered
                .Skip(offset)
                .Take(limit)
                .Select(item => new Dictionary<string, object>
                {
                    { "name", item.GameObject.name },
                    { "path", item.Path },
                    { "instanceId", VmObjectId.Get(item.GameObject) },
                    { "active", item.GameObject.activeInHierarchy },
                    { "tag", item.GameObject.tag },
                    { "layer", LayerMask.LayerToName(item.GameObject.layer) },
                    { "layerIndex", item.GameObject.layer },
                    { "scene", item.GameObject.scene.name },
                })
                .ToList();
            int nextOffset = offset + results.Count;

            return new Dictionary<string, object>
            {
                { "totalFound", ordered.Count },
                { "offset", offset },
                { "returned", results.Count },
                { "hasMore", nextOffset < ordered.Count },
                { "nextOffset", nextOffset < ordered.Count ? (object)nextOffset : null },
                { "results", results },
            };
        }

        public static object FindByComponent(Dictionary<string, object> args)
        {
            string typeName = args.ContainsKey("componentType") ? args["componentType"].ToString() : "";
            if (string.IsNullOrEmpty(typeName))
                return new { error = "componentType is required" };

            bool includeInactive = args.ContainsKey("includeInactive") && Convert.ToBoolean(args["includeInactive"]);
            int limit = args.ContainsKey("limit") ? Convert.ToInt32(args["limit"]) : DefaultSearchLimit;

            // Try to find the type
            Type componentType = ResolveComponentType(typeName);

            if (componentType == null)
                return new { error = $"Component type '{typeName}' not found" };

            var results = new List<Dictionary<string, object>>();
            var objects = MCPObjectSearch.Find(componentType, includeInactive);

            int totalFound = 0;
            foreach (var obj in objects)
            {
                var comp = obj as Component;
                if (comp == null) continue;
                totalFound++;
                if (results.Count < limit)
                {
                    results.Add(new Dictionary<string, object>
                    {
                        { "name", comp.gameObject.name },
                        { "path", GetGameObjectPath(comp.gameObject) },
                        { "instanceId", VmObjectId.Get(comp.gameObject) },
                        { "active", comp.gameObject.activeInHierarchy },
                        { "scene", comp.gameObject.scene.name },
                    });
                }
            }

            var result = new Dictionary<string, object>
            {
                { "componentType", typeName },
                { "totalFound", totalFound },
                { "returned", results.Count },
                { "limit", limit },
                { "results", results },
            };
            if (totalFound > limit)
                result["truncated"] = true;
            return result;
        }

        // ─── Find By Tag ───

        public static object FindByTag(Dictionary<string, object> args)
        {
            string tag = args.ContainsKey("tag") ? args["tag"].ToString() : "";
            if (string.IsNullOrEmpty(tag))
                return new { error = "tag is required" };

            int limit = args.ContainsKey("limit") ? Convert.ToInt32(args["limit"]) : DefaultSearchLimit;

            GameObject[] objects;
            try { objects = GameObject.FindGameObjectsWithTag(tag); }
            catch (Exception e) { return new { error = e.Message }; }

            var results = new List<Dictionary<string, object>>();
            int count = Math.Min(objects.Length, limit);
            for (int i = 0; i < count; i++)
            {
                var go = objects[i];
                results.Add(new Dictionary<string, object>
                {
                    { "name", go.name },
                    { "path", GetGameObjectPath(go) },
                    { "instanceId", VmObjectId.Get(go) },
                    { "active", go.activeInHierarchy },
                    { "layer", LayerMask.LayerToName(go.layer) },
                });
            }

            var result = new Dictionary<string, object>
            {
                { "tag", tag },
                { "totalFound", objects.Length },
                { "returned", results.Count },
                { "limit", limit },
                { "results", results },
            };
            if (objects.Length > limit)
                result["truncated"] = true;
            return result;
        }

        // ─── Find By Layer ───

        public static object FindByLayer(Dictionary<string, object> args)
        {
            int layer = -1;
            if (args.ContainsKey("layer"))
            {
                string val = args["layer"].ToString();
                if (!int.TryParse(val, out layer))
                    layer = LayerMask.NameToLayer(val);
            }
            if (layer < 0)
                return new { error = "Valid layer index or name is required" };

            int limit = args.ContainsKey("limit") ? Convert.ToInt32(args["limit"]) : DefaultSearchLimit;

            var results = new List<Dictionary<string, object>>();
            var allObjects = MCPObjectSearch.Find<GameObject>(includeInactive: true);
            int totalFound = 0;
            foreach (var go in allObjects)
            {
                if (go.layer == layer)
                {
                    totalFound++;
                    if (results.Count < limit)
                    {
                        results.Add(new Dictionary<string, object>
                        {
                            { "name", go.name },
                            { "path", GetGameObjectPath(go) },
                            { "instanceId", VmObjectId.Get(go) },
                            { "active", go.activeInHierarchy },
                            { "tag", go.tag },
                        });
                    }
                }
            }

            var result = new Dictionary<string, object>
            {
                { "layer", LayerMask.LayerToName(layer) },
                { "layerIndex", layer },
                { "totalFound", totalFound },
                { "returned", results.Count },
                { "limit", limit },
                { "results", results },
            };
            if (totalFound > limit)
                result["truncated"] = true;
            return result;
        }

        // ─── Find By Name ───

        public static object FindByName(Dictionary<string, object> args)
        {
            string pattern = args.ContainsKey("name") ? args["name"].ToString() : "";
            if (string.IsNullOrEmpty(pattern))
                return new { error = "name is required" };

            bool useRegex = args.ContainsKey("regex") && Convert.ToBoolean(args["regex"]);
            bool includeInactive = args.ContainsKey("includeInactive") && Convert.ToBoolean(args["includeInactive"]);
            int limit = args.ContainsKey("limit") ? Convert.ToInt32(args["limit"]) : DefaultSearchLimit;

            var results = new List<Dictionary<string, object>>();
            var allObjects = MCPObjectSearch.Find<GameObject>(includeInactive);

            int totalFound = 0;
            foreach (var go in allObjects)
            {
                bool match = false;
                if (useRegex)
                {
                    try { match = Regex.IsMatch(go.name, pattern, RegexOptions.IgnoreCase); }
                    catch { continue; }
                }
                else
                {
                    match = go.name.IndexOf(pattern, StringComparison.OrdinalIgnoreCase) >= 0;
                }

                if (match)
                {
                    totalFound++;
                    if (results.Count < limit)
                    {
                        results.Add(new Dictionary<string, object>
                        {
                            { "name", go.name },
                            { "path", GetGameObjectPath(go) },
                            { "instanceId", VmObjectId.Get(go) },
                            { "active", go.activeInHierarchy },
                            { "tag", go.tag },
                            { "layer", LayerMask.LayerToName(go.layer) },
                        });
                    }
                }
            }

            var result = new Dictionary<string, object>
            {
                { "pattern", pattern },
                { "regex", useRegex },
                { "totalFound", totalFound },
                { "returned", results.Count },
                { "limit", limit },
                { "results", results },
            };
            if (totalFound > limit)
                result["truncated"] = true;
            return result;
        }

        // ─── Find By Shader ───

        public static object FindByShader(Dictionary<string, object> args)
        {
            string shaderName = args.ContainsKey("shader") ? args["shader"].ToString() : "";
            if (string.IsNullOrEmpty(shaderName))
                return new { error = "shader is required" };

            int limit = args.ContainsKey("limit") ? Convert.ToInt32(args["limit"]) : DefaultSearchLimit;

            var results = new List<Dictionary<string, object>>();
            var renderers = MCPObjectSearch.Find<Renderer>(includeInactive: true);

            int totalFound = 0;
            foreach (var renderer in renderers)
            {
                foreach (var mat in renderer.sharedMaterials)
                {
                    if (mat != null && mat.shader != null &&
                        mat.shader.name.IndexOf(shaderName, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        totalFound++;
                        if (results.Count < limit)
                        {
                            results.Add(new Dictionary<string, object>
                            {
                                { "name", renderer.gameObject.name },
                                { "path", GetGameObjectPath(renderer.gameObject) },
                                { "instanceId", VmObjectId.Get(renderer.gameObject) },
                                { "material", mat.name },
                                { "shader", mat.shader.name },
                            });
                        }
                        break; // One entry per object
                    }
                }
            }

            var result = new Dictionary<string, object>
            {
                { "shader", shaderName },
                { "totalFound", totalFound },
                { "returned", results.Count },
                { "limit", limit },
                { "results", results },
            };
            if (totalFound > limit)
                result["truncated"] = true;
            return result;
        }

        // ─── Search Assets ───

        // ─── Find Missing References ───

        public static object FindMissingReferences(Dictionary<string, object> args)
        {
            bool searchScene = !args.ContainsKey("scope") || args["scope"].ToString() != "assets";
            int limit = args.ContainsKey("limit") ? Convert.ToInt32(args["limit"]) : DefaultSearchLimit;

            var results = new List<Dictionary<string, object>>();
            int totalFound = 0;

            if (searchScene)
            {
                var allObjects = MCPObjectSearch.Find<GameObject>(includeInactive: true);
                foreach (var go in allObjects)
                {
                    if (totalFound >= limit) break;

                    var components = go.GetComponents<Component>();
                    for (int i = 0; i < components.Length; i++)
                    {
                        if (components[i] == null)
                        {
                            totalFound++;
                            if (results.Count < limit)
                            {
                                results.Add(new Dictionary<string, object>
                                {
                                    { "gameObject", go.name },
                                    { "path", GetGameObjectPath(go) },
                                    { "issue", "Missing script (component is null)" },
                                    { "componentIndex", i },
                                });
                            }
                            continue;
                        }

                        var so = new SerializedObject(components[i]);
                        var sp = so.GetIterator();
                        while (sp.NextVisible(true))
                        {
                            if (sp.propertyType == SerializedPropertyType.ObjectReference &&
                                sp.objectReferenceValue == null &&
                                VmObjectId.HasObjectRef(sp))
                            {
                                totalFound++;
                                if (results.Count < limit)
                                {
                                    results.Add(new Dictionary<string, object>
                                    {
                                        { "gameObject", go.name },
                                        { "path", GetGameObjectPath(go) },
                                        { "component", components[i].GetType().Name },
                                        { "property", sp.displayName },
                                        { "issue", "Missing object reference" },
                                    });
                                }
                            }
                        }
                    }
                }
            }

            var result = new Dictionary<string, object>
            {
                { "scope", searchScene ? "scene" : "assets" },
                { "totalFound", totalFound },
                { "returned", results.Count },
                { "limit", limit },
                { "results", results },
            };
            if (totalFound > limit)
                result["truncated"] = true;
            return result;
        }

        // ─── Scene Stats ───

        public static object GetSceneStats(Dictionary<string, object> args)
        {
            var scene = SceneManager.GetActiveScene();
            var rootObjects = scene.GetRootGameObjects();

            int totalObjects = 0;
            int totalComponents = 0;
            int totalMeshes = 0;
            int totalLights = 0;
            int totalCameras = 0;
            int totalColliders = 0;
            int totalRigidbodies = 0;
            int totalRenderers = 0;
            long totalVertices = 0;
            long totalTriangles = 0;
            var componentCounts = new Dictionary<string, int>();

            void CountRecursive(GameObject go)
            {
                totalObjects++;
                var components = go.GetComponents<Component>();
                foreach (var comp in components)
                {
                    if (comp == null) continue;
                    totalComponents++;
                    string typeName = comp.GetType().Name;
                    if (!componentCounts.ContainsKey(typeName))
                        componentCounts[typeName] = 0;
                    componentCounts[typeName]++;

                    if (comp is MeshFilter mf && mf.sharedMesh != null)
                    {
                        totalMeshes++;
                        totalVertices += mf.sharedMesh.vertexCount;
                        totalTriangles += mf.sharedMesh.triangles.Length / 3;
                    }
                    if (comp is Light) totalLights++;
                    if (comp is Camera) totalCameras++;
                    if (comp is Collider) totalColliders++;
                    if (comp is Rigidbody) totalRigidbodies++;
                    if (comp is Renderer) totalRenderers++;
                }

                foreach (Transform child in go.transform)
                    CountRecursive(child.gameObject);
            }

            foreach (var root in rootObjects)
                CountRecursive(root);

            // Top 10 most common components
            var topComponents = componentCounts.OrderByDescending(kv => kv.Value).Take(10)
                .Select(kv => new Dictionary<string, object> { { "type", kv.Key }, { "count", kv.Value } })
                .ToList();

            return new Dictionary<string, object>
            {
                { "sceneName", scene.name },
                { "totalGameObjects", totalObjects },
                { "totalComponents", totalComponents },
                { "totalMeshes", totalMeshes },
                { "totalVertices", totalVertices },
                { "totalTriangles", totalTriangles },
                { "totalLights", totalLights },
                { "totalCameras", totalCameras },
                { "totalColliders", totalColliders },
                { "totalRigidbodies", totalRigidbodies },
                { "totalRenderers", totalRenderers },
                { "topComponents", topComponents },
            };
        }

        // ─── Helpers ───

        private static string GetGameObjectPath(GameObject go)
        {
            string path = go.name;
            Transform parent = go.transform.parent;
            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }
            return path;
        }

        private static Type ResolveComponentType(string typeName)
        {
            return ResolveComponentTypes(typeName).FirstOrDefault();
        }

        private static List<Type> ResolveComponentTypes(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName))
                return new List<Type>();

            string normalized = typeName.Trim();
            bool isFullName = normalized.IndexOf('.') >= 0;
            return TypeCache.GetTypesDerivedFrom<Component>()
                .Concat(new[] { typeof(Component) })
                .Where(type => isFullName
                    ? string.Equals(type.FullName, normalized, StringComparison.OrdinalIgnoreCase)
                    : string.Equals(type.Name, normalized, StringComparison.OrdinalIgnoreCase))
                .Distinct()
                .OrderBy(type => type.FullName, StringComparer.Ordinal)
                .ToList();
        }

        private static bool UsesShader(GameObject gameObject, string shaderName)
        {
            var renderer = gameObject.GetComponent<Renderer>();
            if (renderer == null)
                return false;

            return renderer.sharedMaterials.Any(material =>
                material != null &&
                material.shader != null &&
                material.shader.name.IndexOf(shaderName, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static string GetString(Dictionary<string, object> args, string key)
        {
            return args.TryGetValue(key, out var value) && value != null ? value.ToString() : "";
        }

        private static bool GetBool(Dictionary<string, object> args, string key, bool defaultValue)
        {
            if (!args.TryGetValue(key, out var value) || value == null)
                return defaultValue;
            if (value is bool boolValue)
                return boolValue;
            return bool.TryParse(value.ToString(), out bool parsed) ? parsed : defaultValue;
        }

        private static int GetInt(Dictionary<string, object> args, string key, int defaultValue)
        {
            if (!args.TryGetValue(key, out var value) || value == null)
                return defaultValue;
            return int.TryParse(value.ToString(), out int result) ? result : defaultValue;
        }
    }
}
