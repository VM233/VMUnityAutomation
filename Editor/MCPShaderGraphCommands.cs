using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace VMUnityAutomation.Editor
{
    /// <summary>
    /// Commands for Shader Graph and Visual Effect Graph interaction.
    /// Provides listing, inspection, creation, and management of shader graphs.
    /// Requires com.unity.shadergraph to be installed for shader graph features.
    /// Basic shader operations (list, inspect, compile) work without the package.
    /// </summary>
    public static class MCPShaderGraphCommands
    {
        private static bool _sgPackageChecked;
        private static bool _sgPackageInstalled;
        private static bool _vfxPackageChecked;
        private static bool _vfxPackageInstalled;

        // ─── Package Detection ───

        public static bool IsShaderGraphInstalled()
        {
            if (_sgPackageChecked) return _sgPackageInstalled;
            _sgPackageChecked = true;

            try
            {
                string manifestPath = Path.Combine(Application.dataPath, "..", "Packages", "manifest.json");
                if (File.Exists(manifestPath))
                {
                    string content = File.ReadAllText(manifestPath);
                    _sgPackageInstalled = content.Contains("\"com.unity.shadergraph\"");
                }
            }
            catch { }

            // Also check if it's a transitive dependency (URP/HDRP include it)
            if (!_sgPackageInstalled)
            {
                // Check if ShaderGraph types exist in any loaded assembly
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (asm.GetName().Name == "Unity.ShaderGraph.Editor")
                    {
                        _sgPackageInstalled = true;
                        break;
                    }
                }
            }

            return _sgPackageInstalled;
        }

        public static bool IsVFXGraphInstalled()
        {
            if (_vfxPackageChecked) return _vfxPackageInstalled;
            _vfxPackageChecked = true;

            try
            {
                string manifestPath = Path.Combine(Application.dataPath, "..", "Packages", "manifest.json");
                if (File.Exists(manifestPath))
                {
                    string content = File.ReadAllText(manifestPath);
                    _vfxPackageInstalled = content.Contains("\"com.unity.visualeffectgraph\"");
                }
            }
            catch { }

            return _vfxPackageInstalled;
        }

        // ─── Status ───

        /// <summary>
        /// Get status of graph-related packages and available features.
        /// </summary>
        public static object GetStatus(Dictionary<string, object> args)
        {
            bool hasSG = IsShaderGraphInstalled();
            bool hasVFX = IsVFXGraphInstalled();

            var commands = new List<string>
            {
                "shadergraph/status",
                "shadergraph/list-shaders",
            };

            if (hasSG)
            {
                commands.Add("shadergraph/list");
                commands.Add("shadergraph/info");
                commands.Add("shadergraph/create");
                commands.Add("shadergraph/open");
                commands.Add("shadergraph/get-properties");
                commands.Add("shadergraph/list-subgraphs");
                commands.Add("shadergraph/get-nodes");
                commands.Add("shadergraph/get-edges");
                commands.Add("shadergraph/add-node");
                commands.Add("shadergraph/remove-node");
                commands.Add("shadergraph/connect");
                commands.Add("shadergraph/disconnect");
                commands.Add("shadergraph/set-node-property");
                commands.Add("shadergraph/get-node-types");
            }

            if (hasVFX)
            {
                commands.Add("shadergraph/list-vfx");
                commands.Add("shadergraph/open-vfx");
            }

            return new Dictionary<string, object>
            {
                { "shaderGraphInstalled", hasSG },
                { "vfxGraphInstalled", hasVFX },
                { "availableCommands", commands.ToArray() },
            };
        }

        // ─── List All Shaders ───

        /// <summary>
        /// List all shaders in the project (built-in, always available).
        /// </summary>
        public static object ListShaders(Dictionary<string, object> args)
        {
            string filter = args.ContainsKey("filter") ? args["filter"].ToString() : "";
            bool includeBuiltin = args.ContainsKey("includeBuiltin") && GetBool(args, "includeBuiltin", false);
            int maxResults = args.ContainsKey("maxResults") ? Convert.ToInt32(args["maxResults"]) : 100;

            var guids = AssetDatabase.FindAssets("t:Shader");
            var shaders = new List<Dictionary<string, object>>();

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!includeBuiltin && !path.StartsWith("Assets/")) continue;

                var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
                if (shader == null) continue;

                if (!string.IsNullOrEmpty(filter) &&
                    !shader.name.ToLower().Contains(filter.ToLower()) &&
                    !path.ToLower().Contains(filter.ToLower()))
                    continue;

                bool isShaderGraph = path.EndsWith(".shadergraph");
                int propCount = shader.GetPropertyCount();

                var info = new Dictionary<string, object>
                {
                    { "name", shader.name },
                    { "assetPath", path },
                    { "isShaderGraph", isShaderGraph },
                    { "propertyCount", propCount },
                    { "isSupported", shader.isSupported },
                    { "renderQueue", shader.renderQueue },
                    { "passCount", shader.passCount },
                };

                shaders.Add(info);

                if (shaders.Count >= maxResults) break;
            }

            return new Dictionary<string, object>
            {
                { "totalFound", shaders.Count },
                { "maxResults", maxResults },
                { "filter", string.IsNullOrEmpty(filter) ? "(none)" : filter },
                { "shaders", shaders.ToArray() },
            };
        }

        // ─── List Shader Graphs ───

        /// <summary>
        /// List all .shadergraph assets in the project. Requires Shader Graph package.
        /// </summary>
        public static object ListShaderGraphs(Dictionary<string, object> args)
        {
            if (!IsShaderGraphInstalled())
                return PackageNotInstalledError("Shader Graph (com.unity.shadergraph)");

            string filter = args.ContainsKey("filter") ? args["filter"].ToString() : "";
            int maxResults = args.ContainsKey("maxResults") ? Convert.ToInt32(args["maxResults"]) : 100;

            var guids = AssetDatabase.FindAssets("t:Shader");
            var graphs = new List<Dictionary<string, object>>();

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".shadergraph")) continue;

                var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);
                if (shader == null) continue;

                if (!string.IsNullOrEmpty(filter) &&
                    !shader.name.ToLower().Contains(filter.ToLower()) &&
                    !path.ToLower().Contains(filter.ToLower()))
                    continue;

                var info = new Dictionary<string, object>
                {
                    { "name", shader.name },
                    { "assetPath", path },
                    { "propertyCount", shader.GetPropertyCount() },
                    { "isSupported", shader.isSupported },
                    { "renderQueue", shader.renderQueue },
                    { "passCount", shader.passCount },
                };

                // Try to get file size for complexity estimate
                try
                {
                    var fi = new FileInfo(Path.Combine(Application.dataPath, "..", path));
                    if (fi.Exists)
                        info["fileSizeKB"] = Math.Round(fi.Length / 1024.0, 1);
                }
                catch { }

                graphs.Add(info);
                if (graphs.Count >= maxResults) break;
            }

            return new Dictionary<string, object>
            {
                { "totalFound", graphs.Count },
                { "graphs", graphs.ToArray() },
            };
        }

        // ─── Get Shader Graph Info ───

        /// <summary>
        /// Get detailed info about a specific shader graph, including exposed properties.
        /// </summary>
        public static object GetShaderGraphInfo(Dictionary<string, object> args)
        {
            if (!IsShaderGraphInstalled())
                return PackageNotInstalledError("Shader Graph (com.unity.shadergraph)");

            if (!args.ContainsKey("path"))
                return new Dictionary<string, object> { { "error", "Missing required parameter: path" } };

            string path = args["path"].ToString();
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(path);

            if (shader == null)
                return new Dictionary<string, object> { { "error", $"Shader not found at: {path}" } };

            string graphContent = null;
            ShaderGraphDocument graphDocument = null;
            var textureMetadata = new Dictionary<string, Dictionary<string, object>>(StringComparer.Ordinal);
            try
            {
                string fullPath = Path.Combine(Application.dataPath, "..", path);
                if (File.Exists(fullPath))
                {
                    graphContent = File.ReadAllText(fullPath);
                    graphDocument = ParseShaderGraphDocument(graphContent);
                    textureMetadata = GetTexturePropertyMetadata(graphDocument);
                }
            }
            catch
            {
                graphContent = null;
                graphDocument = null;
            }

            int propCount = shader.GetPropertyCount();
            var properties = new List<Dictionary<string, object>>();

            for (int i = 0; i < propCount; i++)
                properties.Add(BuildShaderPropertyInfo(shader, i, textureMetadata));

            // Parse the .shadergraph JSON for additional metadata
            var graphMeta = new Dictionary<string, object>();
            if (graphContent != null && graphDocument != null)
            {
                string fullPath = Path.Combine(Application.dataPath, "..", path);
                graphMeta["fileSizeKB"] = Math.Round(new FileInfo(fullPath).Length / 1024.0, 1);
                graphMeta["nodeCount"] = GetReferencedObjectIds(graphDocument.GraphData, "m_Nodes").Count;
                graphMeta["edgeCount"] = ReadGraphEdges(graphDocument.GraphData).Count;
                graphMeta["blackboardPropertyCount"] =
                    GetReferencedObjectIds(graphDocument.GraphData, "m_Properties").Count;
                graphMeta["usesCustomFunction"] = graphContent.Contains("CustomFunctionNode");
                graphMeta["usesSubGraph"] = graphContent.Contains("SubGraphNode");
                graphMeta["usesKeywords"] = graphContent.Contains("ShaderKeyword");
            }

            var result = new Dictionary<string, object>
            {
                { "name", shader.name },
                { "assetPath", path },
                { "isSupported", shader.isSupported },
                { "renderQueue", shader.renderQueue },
                { "passCount", shader.passCount },
                { "propertyCount", propCount },
                { "properties", properties.ToArray() },
            };

            if (graphMeta.Count > 0)
                result["graphMetadata"] = graphMeta;

            return result;
        }

        // ─── Get Shader Properties ───

        /// <summary>
        /// Get exposed properties of any shader (works with .shader and .shadergraph).
        /// </summary>
        public static object GetShaderProperties(Dictionary<string, object> args)
        {
            if (!args.ContainsKey("path") && !args.ContainsKey("shaderName"))
                return new Dictionary<string, object> { { "error", "Provide 'path' (asset path) or 'shaderName' (shader name like 'Universal Render Pipeline/Lit')" } };

            Shader shader = null;
            string shaderAssetPath = null;

            if (args.ContainsKey("path"))
            {
                shaderAssetPath = args["path"].ToString();
                shader = AssetDatabase.LoadAssetAtPath<Shader>(shaderAssetPath);
            }
            else if (args.ContainsKey("shaderName"))
                shader = Shader.Find(args["shaderName"].ToString());

            if (shader == null)
                return new Dictionary<string, object> { { "error", "Shader not found." } };

            int propCount = shader.GetPropertyCount();
            var properties = new List<Dictionary<string, object>>();
            var textureMetadata = new Dictionary<string, Dictionary<string, object>>(StringComparer.Ordinal);
            if (string.IsNullOrEmpty(shaderAssetPath) == false &&
                shaderAssetPath.EndsWith(".shadergraph", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    string fullPath = Path.Combine(Application.dataPath, "..", shaderAssetPath);
                    textureMetadata = GetTexturePropertyMetadata(
                        ParseShaderGraphDocument(File.ReadAllText(fullPath)));
                }
                catch { }
            }

            for (int i = 0; i < propCount; i++)
                properties.Add(BuildShaderPropertyInfo(shader, i, textureMetadata));

            var result = new Dictionary<string, object>
            {
                { "shaderName", shader.name },
                { "propertyCount", propCount },
                { "properties", properties.ToArray() },
            };
            if (string.IsNullOrEmpty(shaderAssetPath) == false)
                result["assetPath"] = shaderAssetPath;
            return result;
        }

        // ─── Create Shader Graph ───

        /// <summary>
        /// Create a new shader graph from a template type.
        /// </summary>
        public static object CreateShaderGraph(Dictionary<string, object> args)
        {
            if (!IsShaderGraphInstalled())
                return PackageNotInstalledError("Shader Graph (com.unity.shadergraph)");

            if (!args.ContainsKey("path"))
                return new Dictionary<string, object> { { "error", "Missing required parameter: path (e.g. 'Assets/Shaders/MyShader.shadergraph')" } };

            string path = args["path"].ToString();
            if (!path.EndsWith(".shadergraph"))
                path += ".shadergraph";

            if (File.Exists(Path.Combine(Application.dataPath, "..", path)))
                return new Dictionary<string, object> { { "error", $"File already exists at: {path}" } };

            string template = args.ContainsKey("template") ? args["template"].ToString().ToLower() : "urp_lit";

            try
            {
                // Try using ShaderGraph's internal API to create via menu items
                // This is the most reliable approach as the JSON format is complex and version-dependent

                // First ensure directory exists
                string dir = Path.GetDirectoryName(Path.Combine(Application.dataPath, "..", path));
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                // Use ProjectWindowUtil for reliable creation
                bool created = false;

                // Try menu item approach - create in a temp location then move
                string menuPath = GetMenuPathForTemplate(template);

                if (!string.IsNullOrEmpty(menuPath))
                {
                    // Select the target folder first
                    string folderPath = Path.GetDirectoryName(path);
                    var folder = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(folderPath);
                    if (folder != null)
                        Selection.activeObject = folder;

                    // Create using internal API via reflection
                    try
                    {
                        // Try to find the shader graph creation type
                        Type createActionType = null;
                        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                        {
                            if (asm.GetName().Name == "Unity.ShaderGraph.Editor")
                            {
                                createActionType = asm.GetType("UnityEditor.ShaderGraph.CreateShaderGraph");
                                break;
                            }
                        }

                        if (createActionType != null)
                        {
                            // Invoke the creation method
                            var createMethod = createActionType.GetMethod("CreateGraph",
                                BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic);
                            if (createMethod != null)
                            {
                                createMethod.Invoke(null, new object[] { path });
                                created = true;
                            }
                        }
                    }
                    catch { }
                }

                // Fallback: create a minimal .shadergraph file
                if (!created)
                {
                    string graphContent = GetMinimalShaderGraphJson(template, Path.GetFileNameWithoutExtension(path));
                    string fullPath = Path.Combine(Application.dataPath, "..", path);
                    File.WriteAllText(fullPath, graphContent);
                    AssetDatabase.ImportAsset(path);
                    created = true;
                }

                if (created)
                {
                    AssetDatabase.Refresh();
                    return new Dictionary<string, object>
                    {
                        { "success", true },
                        { "assetPath", path },
                        { "template", template },
                        { "note", "Shader graph created. Open it in the Shader Graph editor to add nodes." },
                    };
                }

                return new Dictionary<string, object>
                {
                    { "error", "Failed to create shader graph. Try creating it manually via Assets > Create > Shader Graph." },
                };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "error", "Failed to create shader graph: " + ex.Message } };
            }
        }

        // ─── Open Shader Graph ───

        /// <summary>
        /// Open a shader graph in the Shader Graph editor window.
        /// </summary>
        public static object OpenShaderGraph(Dictionary<string, object> args)
        {
            if (!IsShaderGraphInstalled())
                return PackageNotInstalledError("Shader Graph (com.unity.shadergraph)");

            if (!args.ContainsKey("path"))
                return new Dictionary<string, object> { { "error", "Missing required parameter: path" } };

            string path = args["path"].ToString();
            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);

            if (asset == null)
                return new Dictionary<string, object> { { "error", $"Asset not found at: {path}" } };

            AssetDatabase.OpenAsset(asset);

            return new Dictionary<string, object>
            {
                { "success", true },
                { "assetPath", path },
                { "note", "Shader graph opened in editor." },
            };
        }

        // ─── List Sub-Graphs ───

        /// <summary>
        /// List all .shadersubgraph assets in the project.
        /// </summary>
        public static object ListSubGraphs(Dictionary<string, object> args)
        {
            if (!IsShaderGraphInstalled())
                return PackageNotInstalledError("Shader Graph (com.unity.shadergraph)");

            var guids = AssetDatabase.FindAssets("glob:\"*.shadersubgraph\"");
            var subgraphs = new List<Dictionary<string, object>>();

            // Fallback: search by file extension
            if (guids.Length == 0)
            {
                string[] files = Directory.GetFiles(Application.dataPath, "*.shadersubgraph", SearchOption.AllDirectories);
                foreach (string file in files)
                {
                    string relativePath = "Assets" + file.Replace(Application.dataPath, "").Replace('\\', '/');
                    subgraphs.Add(new Dictionary<string, object>
                    {
                        { "assetPath", relativePath },
                        { "name", Path.GetFileNameWithoutExtension(file) },
                    });
                }
            }
            else
            {
                foreach (string guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (path.EndsWith(".shadersubgraph"))
                    {
                        subgraphs.Add(new Dictionary<string, object>
                        {
                            { "assetPath", path },
                            { "name", Path.GetFileNameWithoutExtension(path) },
                        });
                    }
                }
            }

            return new Dictionary<string, object>
            {
                { "count", subgraphs.Count },
                { "subGraphs", subgraphs.ToArray() },
            };
        }

        // ─── List VFX Graphs ───

        /// <summary>
        /// List all .vfx assets (Visual Effect Graphs) in the project.
        /// </summary>
        public static object ListVFXGraphs(Dictionary<string, object> args)
        {
            if (!IsVFXGraphInstalled())
                return PackageNotInstalledError("Visual Effect Graph (com.unity.visualeffectgraph)");

            var guids = AssetDatabase.FindAssets("t:VisualEffectAsset");
            var vfxGraphs = new List<Dictionary<string, object>>();

            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);

                vfxGraphs.Add(new Dictionary<string, object>
                {
                    { "assetPath", path },
                    { "name", asset != null ? asset.name : Path.GetFileNameWithoutExtension(path) },
                });
            }

            return new Dictionary<string, object>
            {
                { "count", vfxGraphs.Count },
                { "vfxGraphs", vfxGraphs.ToArray() },
            };
        }

        // ─── Open VFX Graph ───

        public static object OpenVFXGraph(Dictionary<string, object> args)
        {
            if (!IsVFXGraphInstalled())
                return PackageNotInstalledError("Visual Effect Graph (com.unity.visualeffectgraph)");

            if (!args.ContainsKey("path"))
                return new Dictionary<string, object> { { "error", "Missing required parameter: path" } };

            string path = args["path"].ToString();
            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);

            if (asset == null)
                return new Dictionary<string, object> { { "error", $"VFX Graph not found at: {path}" } };

            AssetDatabase.OpenAsset(asset);

            return new Dictionary<string, object>
            {
                { "success", true },
                { "assetPath", path },
            };
        }

        // ─── Helpers ───

        private static string GetMenuPathForTemplate(string template)
        {
            switch (template)
            {
                case "urp_lit": return "Assets/Create/Shader Graph/URP/Lit Shader Graph";
                case "urp_unlit": return "Assets/Create/Shader Graph/URP/Unlit Shader Graph";
                case "urp_sprite_lit": return "Assets/Create/Shader Graph/URP/Sprite Lit Shader Graph";
                case "urp_sprite_unlit": return "Assets/Create/Shader Graph/URP/Sprite Unlit Shader Graph";
                case "urp_decal": return "Assets/Create/Shader Graph/URP/Decal Shader Graph";
                case "hdrp_lit": return "Assets/Create/Shader Graph/HDRP/Lit Shader Graph";
                case "hdrp_unlit": return "Assets/Create/Shader Graph/HDRP/Unlit Shader Graph";
                case "blank": return "Assets/Create/Shader Graph/Blank Shader Graph";
                default: return null;
            }
        }

        private static string GetMinimalShaderGraphJson(string template, string name)
        {
            // Minimal valid .shadergraph file structure
            // This creates a basic graph that Unity can parse and open in the editor
            return $@"{{
    ""m_SGVersion"": 3,
    ""m_Type"": ""UnityEditor.ShaderGraph.GraphData"",
    ""m_ObjectId"": ""{Guid.NewGuid():N}"",
    ""m_Properties"": [],
    ""m_Keywords"": [],
    ""m_Dropdowns"": [],
    ""m_CategoryData"": [],
    ""m_Nodes"": [],
    ""m_GroupDatas"": [],
    ""m_StickyNoteDatas"": [],
    ""m_Edges"": [],
    ""m_VertexContext"": {{
        ""m_Position"": {{ ""x"": 0.0, ""y"": 0.0 }},
        ""m_Blocks"": []
    }},
    ""m_FragmentContext"": {{
        ""m_Position"": {{ ""x"": 200.0, ""y"": 0.0 }},
        ""m_Blocks"": []
    }},
    ""m_PreviewData"": {{
        ""serializedMesh"": {{ ""m_SerializedMesh"": """", ""m_Guid"": """" }}
    }},
    ""m_Path"": ""Shader Graphs"",
    ""m_GraphPrecision"": 1,
    ""m_PreviewMode"": 2,
    ""m_OutputNode"": {{
        ""m_Id"": ""{Guid.NewGuid():N}""
    }}
}}";
        }

        private static object PackageNotInstalledError(string packageName)
        {
            return new Dictionary<string, object>
            {
                { "error", $"{packageName} is not installed. Install it via Package Manager to use this feature." },
                { "hint", "Use 'shadergraph/status' to check which graph packages are available." },
            };
        }

        private static bool GetBool(Dictionary<string, object> args, string key, bool defaultValue)
        {
            if (!args.ContainsKey(key)) return defaultValue;
            var val = args[key];
            if (val is bool b) return b;
            if (val is string s) return s.ToLowerInvariant() == "true";
            return defaultValue;
        }

        // ═══════════════════════════════════════════════════════════
        // ─── Node-Level Graph Editing (JSON-based) ───
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// Get all nodes in a shader graph with their types, positions, and slot info.
        /// Parses the .shadergraph JSON file directly.
        /// </summary>
        public static object GetGraphNodes(Dictionary<string, object> args)
        {
            if (!IsShaderGraphInstalled())
                return PackageNotInstalledError("Shader Graph (com.unity.shadergraph)");

            if (!args.ContainsKey("path"))
                return new Dictionary<string, object> { { "error", "Missing required parameter: path" } };

            string path = args["path"].ToString();
            string fullPath = Path.Combine(Application.dataPath, "..", path);

            if (!File.Exists(fullPath))
                return new Dictionary<string, object> { { "error", $"File not found: {path}" } };

            try
            {
                ShaderGraphDocument document = ParseShaderGraphDocument(File.ReadAllText(fullPath));
                var nodes = new List<Dictionary<string, object>>();

                foreach (string objectId in GetReferencedObjectIds(document.GraphData, "m_Nodes"))
                {
                    if (!document.ObjectsById.TryGetValue(objectId, out var node))
                        throw new InvalidDataException($"GraphData references missing node '{objectId}'.");

                    string typeValue = GetString(node, "m_Type");
                    if (string.IsNullOrEmpty(typeValue))
                        throw new InvalidDataException($"Shader Graph node '{objectId}' has no m_Type.");

                    var nodeInfo = new Dictionary<string, object>
                    {
                        { "objectId", objectId },
                        { "type", typeValue },
                        { "name", GetString(node, "m_Name") ?? typeValue.Split('.').Last() },
                        { "slotCount", GetReferencedObjectIds(node, "m_Slots").Count },
                    };

                    if (TryGetPosition(node, out double positionX, out double positionY))
                    {
                        nodeInfo["position"] = new Dictionary<string, object>
                        {
                            { "x", positionX },
                            { "y", positionY },
                        };
                    }

                    if (node.TryGetValue("m_DefaultValue", out object defaultValue) ||
                        node.TryGetValue("m_Value", out defaultValue))
                    {
                        if (defaultValue == null || defaultValue is string || defaultValue is bool ||
                            IsNumber(defaultValue))
                        {
                            nodeInfo["defaultValue"] = defaultValue;
                        }
                    }

                    nodes.Add(nodeInfo);
                }

                return new Dictionary<string, object>
                {
                    { "assetPath", path },
                    { "nodeCount", nodes.Count },
                    { "nodes", nodes.ToArray() },
                };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "error", $"Failed to parse graph: {ex.Message}" } };
            }
        }

        /// <summary>
        /// Get all edges (connections) in a shader graph.
        /// </summary>
        public static object GetGraphEdges(Dictionary<string, object> args)
        {
            if (!IsShaderGraphInstalled())
                return PackageNotInstalledError("Shader Graph (com.unity.shadergraph)");

            if (!args.ContainsKey("path"))
                return new Dictionary<string, object> { { "error", "Missing required parameter: path" } };

            string path = args["path"].ToString();
            string fullPath = Path.Combine(Application.dataPath, "..", path);

            if (!File.Exists(fullPath))
                return new Dictionary<string, object> { { "error", $"File not found: {path}" } };

            try
            {
                ShaderGraphDocument document = ParseShaderGraphDocument(File.ReadAllText(fullPath));
                var edges = ReadGraphEdges(document.GraphData);

                return new Dictionary<string, object>
                {
                    { "assetPath", path },
                    { "edgeCount", edges.Count },
                    { "edges", edges.ToArray() },
                };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "error", $"Failed to parse edges: {ex.Message}" } };
            }
        }

        /// <summary>
        /// Add a node to a shader graph. Uses reflection to find available node types
        /// from the Shader Graph assembly and generates valid serialized JSON.
        /// </summary>
        public static object AddGraphNode(Dictionary<string, object> args)
        {
            if (!IsShaderGraphInstalled())
                return PackageNotInstalledError("Shader Graph (com.unity.shadergraph)");

            if (!args.ContainsKey("path") || !args.ContainsKey("nodeType"))
                return new Dictionary<string, object> { { "error", "path and nodeType are required" } };

            string path = args["path"].ToString();
            string nodeType = args["nodeType"].ToString();
            float posX = args.ContainsKey("positionX") ? Convert.ToSingle(args["positionX"]) : 0f;
            float posY = args.ContainsKey("positionY") ? Convert.ToSingle(args["positionY"]) : 0f;

            string fullPath = Path.Combine(Application.dataPath, "..", path);
            if (!File.Exists(fullPath))
                return new Dictionary<string, object> { { "error", $"File not found: {path}" } };

            try
            {
                // Find the node type in ShaderGraph assembly
                Type resolvedType = ResolveShaderGraphNodeType(nodeType);

                string nodeId = Guid.NewGuid().ToString("N").Substring(0, 24);
                string nodeJson;

                if (resolvedType != null)
                {
                    // Try to serialize via reflection
                    nodeJson = TrySerializeNodeViaReflection(resolvedType, nodeId, posX, posY);
                }
                else
                {
                    // Use template-based approach for common types
                    nodeJson = GetNodeTemplate(nodeType, nodeId, posX, posY);
                }

                if (string.IsNullOrEmpty(nodeJson))
                    return new Dictionary<string, object>
                    {
                        { "error", $"Unknown node type: {nodeType}. Use 'shadergraph/get-node-types' to list available types." },
                    };

                // Read the file and insert the node
                string content = File.ReadAllText(fullPath);

                // Add node reference to the main GraphData block
                string nodeRef = $"{{\"m_Id\":\"{nodeId}\"}}";

                // Find m_Nodes array in the graph data and add the reference
                int nodesArrayEnd = FindJsonArrayEnd(content, "m_Nodes");
                if (nodesArrayEnd < 0)
                    return new Dictionary<string, object> { { "error", "Could not find m_Nodes array in graph file" } };

                // Insert reference before the closing bracket of m_Nodes
                string nodesArrayContent = content.Substring(0, nodesArrayEnd);
                bool hasExistingNodes = nodesArrayContent.TrimEnd().EndsWith("}");
                string separator = hasExistingNodes ? "," : "";
                content = content.Insert(nodesArrayEnd, separator + nodeRef);

                // Append the full node JSON as a new block at the end of the file
                // In MultiJson format, each object is a separate top-level JSON
                content = content.TrimEnd() + "\n\n" + nodeJson;

                File.WriteAllText(fullPath, content);
                AssetDatabase.ImportAsset(path);

                return new Dictionary<string, object>
                {
                    { "success", true },
                    { "assetPath", path },
                    { "nodeId", nodeId },
                    { "nodeType", nodeType },
                    { "position", new Dictionary<string, object> { { "x", posX }, { "y", posY } } },
                    { "note", "Node added. The graph will update when opened in Shader Graph editor." },
                };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "error", $"Failed to add node: {ex.Message}" } };
            }
        }

        /// <summary>
        /// Remove a node from a shader graph by its object ID.
        /// Also removes all edges connected to it.
        /// </summary>
        public static object RemoveGraphNode(Dictionary<string, object> args)
        {
            if (!IsShaderGraphInstalled())
                return PackageNotInstalledError("Shader Graph (com.unity.shadergraph)");

            if (!args.ContainsKey("path") || !args.ContainsKey("nodeId"))
                return new Dictionary<string, object> { { "error", "path and nodeId are required" } };

            string path = args["path"].ToString();
            string nodeId = args["nodeId"].ToString();
            string fullPath = Path.Combine(Application.dataPath, "..", path);

            if (!File.Exists(fullPath))
                return new Dictionary<string, object> { { "error", $"File not found: {path}" } };

            try
            {
                string content = File.ReadAllText(fullPath);

                // Remove node reference from m_Nodes array
                string refPattern = $"{{\"m_Id\":\"{nodeId}\"}}";
                content = content.Replace("," + refPattern, "");
                content = content.Replace(refPattern + ",", "");
                content = content.Replace(refPattern, "");

                // Remove the node's JSON block (MultiJson format)
                var blocks = ParseMultiJson(content);
                var newBlocks = new List<string>();
                int removedEdges = 0;

                foreach (var block in blocks)
                {
                    string blockId = ExtractJsonString(block, "m_ObjectId") ?? ExtractJsonString(block, "m_Id");

                    // Skip the node itself
                    if (blockId == nodeId) continue;

                    // For the main graph block, also remove edges referencing this node
                    if (block.Contains("\"m_Edges\""))
                    {
                        string cleaned = RemoveEdgesForNode(block, nodeId, out removedEdges);
                        newBlocks.Add(cleaned);
                    }
                    else
                    {
                        newBlocks.Add(block);
                    }
                }

                string newContent = string.Join("\n\n", newBlocks);
                File.WriteAllText(fullPath, newContent);
                AssetDatabase.ImportAsset(path);

                return new Dictionary<string, object>
                {
                    { "success", true },
                    { "removedNodeId", nodeId },
                    { "removedEdges", removedEdges },
                    { "assetPath", path },
                };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "error", $"Failed to remove node: {ex.Message}" } };
            }
        }

        /// <summary>
        /// Connect two nodes in a shader graph by creating an edge.
        /// </summary>
        public static object ConnectGraphNodes(Dictionary<string, object> args)
        {
            if (!IsShaderGraphInstalled())
                return PackageNotInstalledError("Shader Graph (com.unity.shadergraph)");

            if (!args.ContainsKey("path"))
                return new Dictionary<string, object> { { "error", "path is required" } };
            if (!args.ContainsKey("outputNodeId") || !args.ContainsKey("outputSlotId"))
                return new Dictionary<string, object> { { "error", "outputNodeId and outputSlotId are required" } };
            if (!args.ContainsKey("inputNodeId") || !args.ContainsKey("inputSlotId"))
                return new Dictionary<string, object> { { "error", "inputNodeId and inputSlotId are required" } };

            string path = args["path"].ToString();
            string outputNodeId = args["outputNodeId"].ToString();
            int outputSlotId = Convert.ToInt32(args["outputSlotId"]);
            string inputNodeId = args["inputNodeId"].ToString();
            int inputSlotId = Convert.ToInt32(args["inputSlotId"]);

            string fullPath = Path.Combine(Application.dataPath, "..", path);
            if (!File.Exists(fullPath))
                return new Dictionary<string, object> { { "error", $"File not found: {path}" } };

            try
            {
                string content = File.ReadAllText(fullPath);

                // Build edge JSON
                string edgeJson = $"{{\"m_OutputSlot\":{{\"m_Node\":{{\"m_Id\":\"{outputNodeId}\"}},\"m_SlotId\":{outputSlotId}}},\"m_InputSlot\":{{\"m_Node\":{{\"m_Id\":\"{inputNodeId}\"}},\"m_SlotId\":{inputSlotId}}}}}";

                // Find m_Edges array and insert
                int edgesArrayEnd = FindJsonArrayEnd(content, "m_Edges");
                if (edgesArrayEnd < 0)
                    return new Dictionary<string, object> { { "error", "Could not find m_Edges array in graph file" } };

                string beforeEnd = content.Substring(0, edgesArrayEnd).TrimEnd();
                bool hasExistingEdges = beforeEnd.EndsWith("}");
                string separator = hasExistingEdges ? "," : "";
                content = content.Insert(edgesArrayEnd, separator + edgeJson);

                File.WriteAllText(fullPath, content);
                AssetDatabase.ImportAsset(path);

                return new Dictionary<string, object>
                {
                    { "success", true },
                    { "assetPath", path },
                    { "outputNodeId", outputNodeId },
                    { "outputSlotId", outputSlotId },
                    { "inputNodeId", inputNodeId },
                    { "inputSlotId", inputSlotId },
                };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "error", $"Failed to connect nodes: {ex.Message}" } };
            }
        }

        /// <summary>
        /// Disconnect two nodes in a shader graph by removing their edge.
        /// </summary>
        public static object DisconnectGraphNodes(Dictionary<string, object> args)
        {
            if (!IsShaderGraphInstalled())
                return PackageNotInstalledError("Shader Graph (com.unity.shadergraph)");

            if (!args.ContainsKey("path"))
                return new Dictionary<string, object> { { "error", "path is required" } };
            if (!args.ContainsKey("outputNodeId") || !args.ContainsKey("inputNodeId"))
                return new Dictionary<string, object> { { "error", "outputNodeId and inputNodeId are required" } };

            string path = args["path"].ToString();
            string outputNodeId = args["outputNodeId"].ToString();
            string inputNodeId = args["inputNodeId"].ToString();
            int outputSlotId = args.ContainsKey("outputSlotId") ? Convert.ToInt32(args["outputSlotId"]) : -1;
            int inputSlotId = args.ContainsKey("inputSlotId") ? Convert.ToInt32(args["inputSlotId"]) : -1;

            string fullPath = Path.Combine(Application.dataPath, "..", path);
            if (!File.Exists(fullPath))
                return new Dictionary<string, object> { { "error", $"File not found: {path}" } };

            try
            {
                string content = File.ReadAllText(fullPath);
                int removed = 0;

                // Find and remove matching edges
                var edges = ParseEdgesFromJson(content);
                var edgesToKeep = new List<string>();

                // Rebuild edges array, skipping the one to remove
                int edgesStart = content.IndexOf("\"m_Edges\"");
                if (edgesStart < 0)
                    return new Dictionary<string, object> { { "error", "Could not find m_Edges in graph file" } };

                int arrayStart = content.IndexOf('[', edgesStart);
                int arrayEnd = FindMatchingBracket(content, arrayStart);

                string edgesArray = content.Substring(arrayStart, arrayEnd - arrayStart + 1);

                // Remove edges matching criteria
                foreach (var edge in edges)
                {
                    string eOut = edge.ContainsKey("outputNodeId") ? edge["outputNodeId"].ToString() : "";
                    string eIn = edge.ContainsKey("inputNodeId") ? edge["inputNodeId"].ToString() : "";

                    if (eOut == outputNodeId && eIn == inputNodeId)
                    {
                        if (outputSlotId >= 0 && edge.ContainsKey("outputSlotId"))
                        {
                            if (Convert.ToInt32(edge["outputSlotId"]) != outputSlotId) continue;
                        }
                        if (inputSlotId >= 0 && edge.ContainsKey("inputSlotId"))
                        {
                            if (Convert.ToInt32(edge["inputSlotId"]) != inputSlotId) continue;
                        }
                        removed++;
                        continue; // Skip this edge
                    }

                    // Reconstruct edge JSON
                    edgesToKeep.Add($"{{\"m_OutputSlot\":{{\"m_Node\":{{\"m_Id\":\"{eOut}\"}},\"m_SlotId\":{edge["outputSlotId"]}}},\"m_InputSlot\":{{\"m_Node\":{{\"m_Id\":\"{eIn}\"}},\"m_SlotId\":{edge["inputSlotId"]}}}}}");
                }

                string newEdgesArray = "[" + string.Join(",", edgesToKeep) + "]";
                content = content.Substring(0, arrayStart) + newEdgesArray + content.Substring(arrayEnd + 1);

                File.WriteAllText(fullPath, content);
                AssetDatabase.ImportAsset(path);

                return new Dictionary<string, object>
                {
                    { "success", true },
                    { "removedEdges", removed },
                    { "remainingEdges", edgesToKeep.Count },
                };
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "error", $"Failed to disconnect: {ex.Message}" } };
            }
        }

        /// <summary>
        /// Set a scalar property value on a serialized Shader Graph object.
        /// </summary>
        public static object SetGraphNodeProperty(Dictionary<string, object> args)
        {
            if (!IsShaderGraphInstalled())
                return PackageNotInstalledError("Shader Graph (com.unity.shadergraph)");

            if (!args.ContainsKey("path") ||
                (!args.ContainsKey("objectId") && !args.ContainsKey("nodeId")) ||
                !args.ContainsKey("propertyName") || !args.ContainsKey("value"))
            {
                return new Dictionary<string, object>
                {
                    { "error", "path, objectId (or legacy nodeId), propertyName, and value are required" },
                };
            }

            string path = args["path"].ToString();
            string objectId = args.ContainsKey("objectId")
                ? args["objectId"].ToString()
                : args["nodeId"].ToString();
            string propertyName = args["propertyName"].ToString();
            object requestedValue = args.ContainsKey("value") ? args["value"] : null;

            string fullPath = Path.Combine(Application.dataPath, "..", path);
            if (!File.Exists(fullPath))
                return new Dictionary<string, object> { { "error", $"File not found: {path}" } };

            string originalContent = null;
            bool wroteFile = false;
            try
            {
                originalContent = File.ReadAllText(fullPath);
                ShaderGraphDocument document = ParseShaderGraphDocument(originalContent);
                if (!document.ObjectsById.TryGetValue(objectId, out var graphObject))
                {
                    return new Dictionary<string, object>
                    {
                        { "error", $"Shader Graph object with ID '{objectId}' not found" },
                    };
                }

                if (!graphObject.TryGetValue(propertyName, out object previousValue))
                {
                    return new Dictionary<string, object>
                    {
                        { "error", $"Property '{propertyName}' does not exist on Shader Graph object '{objectId}'" },
                        { "objectId", objectId },
                        { "objectType", GetString(graphObject, "m_Type") ?? "unknown" },
                    };
                }

                if (!TryNormalizeScalarJsonValue(previousValue, requestedValue,
                        out object normalizedValue, out string valueError))
                {
                    return new Dictionary<string, object>
                    {
                        { "error", valueError },
                        { "objectId", objectId },
                        { "propertyName", propertyName },
                    };
                }

                string targetBlock = null;
                string modifiedBlock = null;
                foreach (string block in document.Blocks)
                {
                    string blockId = ExtractJsonString(block, "m_ObjectId") ?? ExtractJsonString(block, "m_Id");
                    if (blockId != objectId)
                        continue;
                    if (!TrySetTopLevelJsonProperty(block, propertyName, normalizedValue,
                            out modifiedBlock))
                    {
                        throw new InvalidDataException(
                            $"Could not locate serialized property '{propertyName}' on object '{objectId}'.");
                    }
                    targetBlock = block;
                    break;
                }

                if (targetBlock == null || modifiedBlock == null)
                    throw new InvalidDataException($"Serialized block for object '{objectId}' was not found.");
                int blockOffset = originalContent.IndexOf(targetBlock, StringComparison.Ordinal);
                if (blockOffset < 0)
                    throw new InvalidDataException($"Serialized block for object '{objectId}' changed during editing.");
                string newContent = originalContent.Substring(0, blockOffset) + modifiedBlock +
                                    originalContent.Substring(blockOffset + targetBlock.Length);
                File.WriteAllText(fullPath, newContent);
                wroteFile = true;
                AssetDatabase.ImportAsset(path,
                    ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);

                ShaderGraphDocument readback = ParseShaderGraphDocument(File.ReadAllText(fullPath));
                if (!readback.ObjectsById.TryGetValue(objectId, out var readbackObject) ||
                    !readbackObject.TryGetValue(propertyName, out object readbackValue) ||
                    !JsonScalarEquals(readbackValue, normalizedValue))
                {
                    throw new InvalidDataException(
                        $"Readback verification failed for '{objectId}.{propertyName}'.");
                }

                return new Dictionary<string, object>
                {
                    { "success", true },
                    { "objectId", objectId },
                    { "objectType", GetString(graphObject, "m_Type") ?? "unknown" },
                    { "propertyName", propertyName },
                    { "previousValue", previousValue },
                    { "value", normalizedValue },
                };
            }
            catch (Exception ex)
            {
                bool rolledBack = false;
                if (wroteFile && originalContent != null)
                {
                    try
                    {
                        File.WriteAllText(fullPath, originalContent);
                        AssetDatabase.ImportAsset(path,
                            ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
                        rolledBack = true;
                    }
                    catch
                    {
                        rolledBack = false;
                    }
                }

                return new Dictionary<string, object>
                {
                    { "error", $"Failed to set Shader Graph object property: {ex.Message}" },
                    { "rolledBack", rolledBack },
                };
            }
        }

        /// <summary>
        /// List available Shader Graph node types via reflection on the ShaderGraph assembly.
        /// </summary>
        public static object GetNodeTypes(Dictionary<string, object> args)
        {
            if (!IsShaderGraphInstalled())
                return PackageNotInstalledError("Shader Graph (com.unity.shadergraph)");

            string filter = args.ContainsKey("filter") ? args["filter"].ToString().ToLower() : "";
            int maxResults = args.ContainsKey("maxResults") ? Convert.ToInt32(args["maxResults"]) : 200;

            var nodeTypes = new List<Dictionary<string, object>>();

            try
            {
                Assembly sgAssembly = null;
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (asm.GetName().Name == "Unity.ShaderGraph.Editor")
                    {
                        sgAssembly = asm;
                        break;
                    }
                }

                if (sgAssembly == null)
                    return new Dictionary<string, object> { { "error", "ShaderGraph assembly not found" } };

                // Find the base node type
                Type baseNodeType = sgAssembly.GetType("UnityEditor.ShaderGraph.AbstractMaterialNode");
                if (baseNodeType == null)
                    return new Dictionary<string, object> { { "error", "AbstractMaterialNode type not found" } };

                foreach (var type in sgAssembly.GetTypes())
                {
                    if (type.IsAbstract || type.IsInterface) continue;
                    if (!baseNodeType.IsAssignableFrom(type)) continue;

                    string typeName = type.Name;
                    string fullName = type.FullName;

                    if (!string.IsNullOrEmpty(filter) &&
                        !typeName.ToLower().Contains(filter) &&
                        !fullName.ToLower().Contains(filter))
                        continue;

                    // Try to get a title attribute
                    string title = typeName;
                    var titleAttr = type.GetCustomAttributes(false)
                        .FirstOrDefault(a => a.GetType().Name.Contains("Title"));
                    if (titleAttr != null)
                    {
                        var titleProp = titleAttr.GetType().GetProperty("title") ??
                                        titleAttr.GetType().GetProperty("Title");
                        if (titleProp != null)
                            title = titleProp.GetValue(titleAttr)?.ToString() ?? typeName;
                    }

                    nodeTypes.Add(new Dictionary<string, object>
                    {
                        { "name", typeName },
                        { "fullName", fullName },
                        { "title", title },
                    });

                    if (nodeTypes.Count >= maxResults) break;
                }
            }
            catch (Exception ex)
            {
                return new Dictionary<string, object> { { "error", $"Failed to enumerate types: {ex.Message}" } };
            }

            nodeTypes.Sort((a, b) => string.Compare(a["name"].ToString(), b["name"].ToString(), StringComparison.Ordinal));

            return new Dictionary<string, object>
            {
                { "count", nodeTypes.Count },
                { "nodeTypes", nodeTypes.ToArray() },
            };
        }

        // ─── JSON Parsing Helpers ───

        private sealed class ShaderGraphDocument
        {
            public readonly List<string> Blocks = new List<string>();
            public readonly Dictionary<string, Dictionary<string, object>> ObjectsById =
                new Dictionary<string, Dictionary<string, object>>(StringComparer.Ordinal);
            public Dictionary<string, object> GraphData;
        }

        private static ShaderGraphDocument ParseShaderGraphDocument(string content)
        {
            var document = new ShaderGraphDocument();
            foreach (string block in ParseMultiJson(content))
            {
                if (!(MiniJson.Deserialize(block) is Dictionary<string, object> parsed))
                    throw new InvalidDataException("Shader Graph contains a non-object JSON block.");

                document.Blocks.Add(block);
                string objectId = GetString(parsed, "m_ObjectId");
                if (string.IsNullOrEmpty(objectId) &&
                    parsed.TryGetValue("m_Id", out object idValue) && idValue is string id)
                {
                    objectId = id;
                }

                if (!string.IsNullOrEmpty(objectId))
                {
                    if (document.ObjectsById.ContainsKey(objectId))
                        throw new InvalidDataException($"Duplicate Shader Graph object ID '{objectId}'.");
                    document.ObjectsById.Add(objectId, parsed);
                }

                string type = GetString(parsed, "m_Type");
                if (!string.IsNullOrEmpty(type) &&
                    type.EndsWith(".GraphData", StringComparison.Ordinal))
                {
                    if (document.GraphData != null)
                        throw new InvalidDataException("Shader Graph contains multiple GraphData objects.");
                    document.GraphData = parsed;
                }
            }

            if (document.GraphData == null)
                throw new InvalidDataException("Shader Graph does not contain a GraphData object.");
            return document;
        }

        private static Dictionary<string, object> BuildShaderPropertyInfo(
            Shader shader,
            int propertyIndex,
            Dictionary<string, Dictionary<string, object>> textureMetadata)
        {
            var propertyType = shader.GetPropertyType(propertyIndex);
            string propertyName = shader.GetPropertyName(propertyIndex);
            var flags = shader.GetPropertyFlags(propertyIndex);
            var property = new Dictionary<string, object>
            {
                { "name", propertyName },
                { "description", shader.GetPropertyDescription(propertyIndex) },
                { "type", propertyType.ToString() },
                { "flags", flags.ToString() },
                { "isHidden", flags.HasFlag(UnityEngine.Rendering.ShaderPropertyFlags.HideInInspector) },
            };

            if (propertyType == UnityEngine.Rendering.ShaderPropertyType.Range)
            {
                Vector2 limits = shader.GetPropertyRangeLimits(propertyIndex);
                property["rangeMin"] = limits.x;
                property["rangeMax"] = limits.y;
                property["rangeDefault"] = shader.GetPropertyDefaultFloatValue(propertyIndex);
            }

            if (propertyType == UnityEngine.Rendering.ShaderPropertyType.Texture)
            {
                property["textureDimension"] = shader.GetPropertyTextureDimension(propertyIndex).ToString();
                if (textureMetadata.TryGetValue(propertyName, out var metadata))
                {
                    foreach (var pair in metadata)
                        property[pair.Key] = pair.Value;
                }
            }

            return property;
        }

        private static Dictionary<string, Dictionary<string, object>> GetTexturePropertyMetadata(
            ShaderGraphDocument document)
        {
            var result = new Dictionary<string, Dictionary<string, object>>(StringComparer.Ordinal);
            foreach (string propertyId in GetReferencedObjectIds(document.GraphData, "m_Properties"))
            {
                if (!document.ObjectsById.TryGetValue(propertyId, out var property))
                    throw new InvalidDataException($"GraphData references missing property '{propertyId}'.");

                string type = GetString(property, "m_Type");
                if (string.IsNullOrEmpty(type) ||
                    !type.EndsWith("ShaderProperty", StringComparison.Ordinal) ||
                    type.IndexOf("Texture", StringComparison.Ordinal) < 0)
                {
                    continue;
                }

                string referenceName = GetString(property, "m_OverrideReferenceName");
                if (string.IsNullOrEmpty(referenceName))
                    referenceName = GetString(property, "m_DefaultReferenceName");
                if (string.IsNullOrEmpty(referenceName))
                    continue;

                var metadata = new Dictionary<string, object>
                {
                    { "graphObjectId", propertyId },
                    { "graphPropertyType", type },
                };
                AddOptionalString(metadata, "graphDisplayName", GetString(property, "m_Name"));
                AddOptionalBoolean(metadata, "generatePropertyBlock", property, "m_GeneratePropertyBlock");
                AddOptionalBoolean(metadata, "perRendererData", property, "m_PerRendererData");
                AddOptionalBoolean(metadata, "isMainTexture", property, "isMainTexture");
                AddOptionalBoolean(metadata, "useTilingAndOffset", property, "useTilingAndOffset");
                AddOptionalBoolean(metadata, "useTexelSize", property, "useTexelSize");
                result[referenceName] = metadata;
            }

            return result;
        }

        private static void AddOptionalString(Dictionary<string, object> target, string key, string value)
        {
            if (!string.IsNullOrEmpty(value))
                target[key] = value;
        }

        private static void AddOptionalBoolean(
            Dictionary<string, object> target,
            string outputKey,
            Dictionary<string, object> source,
            string sourceKey)
        {
            if (source.TryGetValue(sourceKey, out object value) && TryConvertBoolean(value, out bool result))
                target[outputKey] = result;
        }

        private static string GetString(Dictionary<string, object> dictionary, string key)
        {
            return dictionary != null && dictionary.TryGetValue(key, out object value)
                ? value as string
                : null;
        }

        private static List<string> GetReferencedObjectIds(
            Dictionary<string, object> owner,
            string collectionName)
        {
            var result = new List<string>();
            if (owner == null || !owner.TryGetValue(collectionName, out object collection) || collection == null)
                return result;
            if (!(collection is IEnumerable<object> references))
                throw new InvalidDataException($"'{collectionName}' is not a JSON array.");

            foreach (object referenceValue in references)
            {
                if (!(referenceValue is Dictionary<string, object> reference))
                    throw new InvalidDataException($"'{collectionName}' contains a non-object reference.");
                string id = GetString(reference, "m_Id");
                if (string.IsNullOrEmpty(id))
                    throw new InvalidDataException($"'{collectionName}' contains a reference without m_Id.");
                result.Add(id);
            }

            return result;
        }

        private static List<Dictionary<string, object>> ReadGraphEdges(Dictionary<string, object> graphData)
        {
            var result = new List<Dictionary<string, object>>();
            if (graphData == null || !graphData.TryGetValue("m_Edges", out object edgesValue) ||
                edgesValue == null)
            {
                return result;
            }
            if (!(edgesValue is IEnumerable<object> edges))
                throw new InvalidDataException("'m_Edges' is not a JSON array.");

            foreach (object edgeValue in edges)
            {
                if (!(edgeValue is Dictionary<string, object> edge))
                    throw new InvalidDataException("'m_Edges' contains a non-object edge.");

                Dictionary<string, object> outputSlot = GetRequiredDictionary(edge, "m_OutputSlot");
                Dictionary<string, object> inputSlot = GetRequiredDictionary(edge, "m_InputSlot");
                string outputNodeId = GetString(GetRequiredDictionary(outputSlot, "m_Node"), "m_Id");
                string inputNodeId = GetString(GetRequiredDictionary(inputSlot, "m_Node"), "m_Id");
                if (string.IsNullOrEmpty(outputNodeId) || string.IsNullOrEmpty(inputNodeId))
                    throw new InvalidDataException("Shader Graph edge contains an empty node ID.");

                result.Add(new Dictionary<string, object>
                {
                    { "outputNodeId", outputNodeId },
                    { "outputSlotId", GetRequiredInteger(outputSlot, "m_SlotId") },
                    { "inputNodeId", inputNodeId },
                    { "inputSlotId", GetRequiredInteger(inputSlot, "m_SlotId") },
                });
            }

            return result;
        }

        private static Dictionary<string, object> GetRequiredDictionary(
            Dictionary<string, object> owner,
            string key)
        {
            if (!owner.TryGetValue(key, out object value) ||
                !(value is Dictionary<string, object> dictionary))
            {
                throw new InvalidDataException($"Shader Graph JSON is missing object '{key}'.");
            }
            return dictionary;
        }

        private static int GetRequiredInteger(Dictionary<string, object> owner, string key)
        {
            if (!owner.TryGetValue(key, out object value) || value == null)
                throw new InvalidDataException($"Shader Graph JSON is missing integer '{key}'.");
            try
            {
                return Convert.ToInt32(value, CultureInfo.InvariantCulture);
            }
            catch (Exception ex)
            {
                throw new InvalidDataException($"Shader Graph JSON value '{key}' is not an integer.", ex);
            }
        }

        private static bool TryGetPosition(
            Dictionary<string, object> node,
            out double positionX,
            out double positionY)
        {
            positionX = 0;
            positionY = 0;
            if (!node.TryGetValue("m_DrawState", out object drawStateValue) ||
                !(drawStateValue is Dictionary<string, object> drawState) ||
                !drawState.TryGetValue("m_Position", out object positionValue) ||
                !(positionValue is Dictionary<string, object> position) ||
                !position.TryGetValue("x", out object xValue) ||
                !position.TryGetValue("y", out object yValue))
            {
                return false;
            }

            try
            {
                positionX = Convert.ToDouble(xValue, CultureInfo.InvariantCulture);
                positionY = Convert.ToDouble(yValue, CultureInfo.InvariantCulture);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static List<string> ParseMultiJson(string content)
        {
            var blocks = new List<string>();
            int index = 0;
            while (index < content.Length)
            {
                while (index < content.Length &&
                       (char.IsWhiteSpace(content[index]) || content[index] == '\uFEFF'))
                    index++;
                if (index >= content.Length)
                    break;
                if (content[index] != '{')
                    throw new InvalidDataException($"Unexpected Shader Graph content at offset {index}.");

                int blockEnd = FindMatchingJsonDelimiter(content, index);
                if (blockEnd < 0)
                    throw new InvalidDataException($"Unterminated Shader Graph JSON object at offset {index}.");
                blocks.Add(content.Substring(index, blockEnd - index + 1));
                index = blockEnd + 1;
            }

            return blocks;
        }

        private static string ExtractJsonString(string json, string key)
        {
            string pattern = $"\"{key}\"\\s*:\\s*\"([^\"]*)\"";
            var match = System.Text.RegularExpressions.Regex.Match(json, pattern);
            return match.Success ? match.Groups[1].Value : null;
        }

        private static List<Dictionary<string, object>> ParseEdgesFromJson(string content)
        {
            return ReadGraphEdges(ParseShaderGraphDocument(content).GraphData);
        }

        private static int FindJsonArrayEnd(string content, string arrayName)
        {
            int idx = content.IndexOf($"\"{arrayName}\"");
            if (idx < 0) return -1;
            int arrayStart = content.IndexOf('[', idx);
            if (arrayStart < 0) return -1;
            return FindMatchingBracket(content, arrayStart);
        }

        private static int FindMatchingBracket(string content, int openPos)
        {
            return FindMatchingJsonDelimiter(content, openPos);
        }

        private static int FindMatchingJsonDelimiter(string content, int openPosition)
        {
            if (string.IsNullOrEmpty(content) || openPosition < 0 || openPosition >= content.Length)
                return -1;
            char open = content[openPosition];
            if (open != '{' && open != '[')
                return -1;
            char close = open == '{' ? '}' : ']';
            int depth = 1;
            bool inString = false;
            bool escaped = false;
            for (int index = openPosition + 1; index < content.Length; index++)
            {
                char character = content[index];
                if (inString)
                {
                    if (escaped)
                    {
                        escaped = false;
                    }
                    else if (character == '\\')
                    {
                        escaped = true;
                    }
                    else if (character == '"')
                    {
                        inString = false;
                    }
                    continue;
                }

                if (character == '"')
                {
                    inString = true;
                    continue;
                }
                if (character == open)
                {
                    depth++;
                }
                else if (character == close)
                {
                    depth--;
                    if (depth == 0)
                        return index;
                }
            }
            return -1;
        }

        private static string RemoveEdgesForNode(string graphBlock, string nodeId, out int removedCount)
        {
            removedCount = 0;
            int edgesIdx = graphBlock.IndexOf("\"m_Edges\"");
            if (edgesIdx < 0) return graphBlock;

            int arrayStart = graphBlock.IndexOf('[', edgesIdx);
            int arrayEnd = FindMatchingBracket(graphBlock, arrayStart);
            if (arrayEnd < 0) return graphBlock;

            var edges = ParseEdgesFromJson(graphBlock);
            var keepEdges = new List<string>();

            foreach (var edge in edges)
            {
                string outNode = edge["outputNodeId"].ToString();
                string inNode = edge["inputNodeId"].ToString();

                if (outNode == nodeId || inNode == nodeId)
                {
                    removedCount++;
                    continue;
                }

                keepEdges.Add($"{{\"m_OutputSlot\":{{\"m_Node\":{{\"m_Id\":\"{outNode}\"}},\"m_SlotId\":{edge["outputSlotId"]}}},\"m_InputSlot\":{{\"m_Node\":{{\"m_Id\":\"{inNode}\"}},\"m_SlotId\":{edge["inputSlotId"]}}}}}");
            }

            string newArray = "[" + string.Join(",", keepEdges) + "]";
            return graphBlock.Substring(0, arrayStart) + newArray + graphBlock.Substring(arrayEnd + 1);
        }

        private static bool TryNormalizeScalarJsonValue(
            object previousValue,
            object requestedValue,
            out object normalizedValue,
            out string error)
        {
            normalizedValue = null;
            error = null;

            if (previousValue is bool)
            {
                if (!TryConvertBoolean(requestedValue, out bool boolValue))
                {
                    error = $"Value '{requestedValue}' is not a boolean.";
                    return false;
                }
                normalizedValue = boolValue;
                return true;
            }

            if (IsNumber(previousValue))
            {
                if (!double.TryParse(Convert.ToString(requestedValue, CultureInfo.InvariantCulture),
                        NumberStyles.Float, CultureInfo.InvariantCulture, out double numericValue) ||
                    double.IsNaN(numericValue) || double.IsInfinity(numericValue))
                {
                    error = $"Value '{requestedValue}' is not a finite number.";
                    return false;
                }

                if (previousValue is byte || previousValue is sbyte || previousValue is short ||
                    previousValue is ushort || previousValue is int || previousValue is uint ||
                    previousValue is long || previousValue is ulong)
                {
                    if (numericValue % 1 != 0 || numericValue < long.MinValue || numericValue > long.MaxValue)
                    {
                        error = $"Value '{requestedValue}' is not an integer in range.";
                        return false;
                    }
                    normalizedValue = Convert.ToInt64(numericValue);
                }
                else
                {
                    normalizedValue = numericValue;
                }
                return true;
            }

            if (previousValue is string)
            {
                normalizedValue = requestedValue?.ToString() ?? string.Empty;
                return true;
            }

            if (previousValue == null &&
                (requestedValue == null || requestedValue is string || requestedValue is bool ||
                 IsNumber(requestedValue)))
            {
                normalizedValue = requestedValue;
                return true;
            }

            error = "Only scalar string, number, boolean, or null Shader Graph fields can be edited safely.";
            return false;
        }

        private static bool TryConvertBoolean(object value, out bool result)
        {
            if (value is bool boolean)
            {
                result = boolean;
                return true;
            }
            return bool.TryParse(value?.ToString(), out result);
        }

        private static bool IsNumber(object value)
        {
            return value is byte || value is sbyte || value is short || value is ushort ||
                   value is int || value is uint || value is long || value is ulong ||
                   value is float || value is double || value is decimal;
        }

        private static bool JsonScalarEquals(object left, object right)
        {
            if (IsNumber(left) && IsNumber(right))
            {
                return Convert.ToDouble(left, CultureInfo.InvariantCulture).Equals(
                    Convert.ToDouble(right, CultureInfo.InvariantCulture));
            }
            return Equals(left, right);
        }

        private static bool TrySetTopLevelJsonProperty(
            string jsonObject,
            string propertyName,
            object value,
            out string modified)
        {
            modified = jsonObject;
            if (!TryFindTopLevelJsonPropertyValue(
                    jsonObject, propertyName, out int valueStart, out int valueEnd))
            {
                return false;
            }

            string serializedValue = MiniJson.Serialize(value);
            modified = jsonObject.Substring(0, valueStart) + serializedValue +
                       jsonObject.Substring(valueEnd);
            return true;
        }

        private static bool TryFindTopLevelJsonPropertyValue(
            string jsonObject,
            string propertyName,
            out int valueStart,
            out int valueEnd)
        {
            valueStart = -1;
            valueEnd = -1;
            int objectDepth = 0;
            int arrayDepth = 0;

            for (int index = 0; index < jsonObject.Length; index++)
            {
                char character = jsonObject[index];
                if (character == '"')
                {
                    int stringEnd = FindJsonStringEnd(jsonObject, index);
                    if (stringEnd < 0)
                        return false;

                    if (objectDepth == 1 && arrayDepth == 0)
                    {
                        string keyToken = jsonObject.Substring(index, stringEnd - index + 1);
                        string key = MiniJson.Deserialize(keyToken) as string;
                        int colon = stringEnd + 1;
                        while (colon < jsonObject.Length && char.IsWhiteSpace(jsonObject[colon]))
                            colon++;
                        if (string.Equals(key, propertyName, StringComparison.Ordinal) &&
                            colon < jsonObject.Length && jsonObject[colon] == ':')
                        {
                            valueStart = colon + 1;
                            while (valueStart < jsonObject.Length && char.IsWhiteSpace(jsonObject[valueStart]))
                                valueStart++;
                            valueEnd = FindJsonValueEnd(jsonObject, valueStart);
                            return valueEnd > valueStart;
                        }
                    }

                    index = stringEnd;
                    continue;
                }

                if (character == '{')
                    objectDepth++;
                else if (character == '}')
                    objectDepth--;
                else if (character == '[')
                    arrayDepth++;
                else if (character == ']')
                    arrayDepth--;
            }

            return false;
        }

        private static int FindJsonStringEnd(string json, int quotePosition)
        {
            bool escaped = false;
            for (int index = quotePosition + 1; index < json.Length; index++)
            {
                char character = json[index];
                if (escaped)
                {
                    escaped = false;
                }
                else if (character == '\\')
                {
                    escaped = true;
                }
                else if (character == '"')
                {
                    return index;
                }
            }
            return -1;
        }

        private static int FindJsonValueEnd(string json, int valueStart)
        {
            if (valueStart < 0 || valueStart >= json.Length)
                return -1;
            char first = json[valueStart];
            if (first == '"')
            {
                int stringEnd = FindJsonStringEnd(json, valueStart);
                return stringEnd < 0 ? -1 : stringEnd + 1;
            }
            if (first == '{' || first == '[')
            {
                int delimiterEnd = FindMatchingJsonDelimiter(json, valueStart);
                return delimiterEnd < 0 ? -1 : delimiterEnd + 1;
            }

            int end = valueStart;
            while (end < json.Length && json[end] != ',' && json[end] != '}' && json[end] != ']')
                end++;
            while (end > valueStart && char.IsWhiteSpace(json[end - 1]))
                end--;
            return end;
        }

        private static Type ResolveShaderGraphNodeType(string typeName)
        {
            try
            {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (asm.GetName().Name != "Unity.ShaderGraph.Editor") continue;

                    // Try exact match
                    Type t = asm.GetType($"UnityEditor.ShaderGraph.{typeName}");
                    if (t != null) return t;

                    // Try with "Node" suffix
                    t = asm.GetType($"UnityEditor.ShaderGraph.{typeName}Node");
                    if (t != null) return t;

                    // Search by name
                    foreach (var type in asm.GetTypes())
                    {
                        if (type.Name.Equals(typeName, StringComparison.OrdinalIgnoreCase) ||
                            type.Name.Equals(typeName + "Node", StringComparison.OrdinalIgnoreCase))
                            return type;
                    }
                }
            }
            catch { }

            return null;
        }

        private static string TrySerializeNodeViaReflection(Type nodeType, string nodeId, float posX, float posY)
        {
            try
            {
                // Create instance
                var node = Activator.CreateInstance(nodeType);
                if (node == null) return null;

                // Use JsonUtility to get a baseline serialization
                string serialized = JsonUtility.ToJson(node, true);

                // Inject our ID and position
                if (!serialized.Contains("m_ObjectId"))
                    serialized = serialized.TrimEnd('}') + $",\"m_ObjectId\":\"{nodeId}\"}}";
                else
                    serialized = System.Text.RegularExpressions.Regex.Replace(
                        serialized, "\"m_ObjectId\"\\s*:\\s*\"[^\"]*\"", $"\"m_ObjectId\":\"{nodeId}\"");

                // Inject type info
                if (!serialized.Contains("m_Type"))
                    serialized = serialized.TrimEnd('}') + $",\"m_Type\":\"{nodeType.FullName}\"}}";

                // Add draw state with position
                if (!serialized.Contains("m_DrawState"))
                {
                    string drawState = $"\"m_DrawState\":{{\"m_Expanded\":true,\"m_Position\":{{\"serializedVersion\":\"2\",\"x\":{posX},\"y\":{posY},\"width\":208,\"height\":311}}}}";
                    serialized = serialized.TrimEnd('}') + "," + drawState + "}";
                }

                return serialized;
            }
            catch
            {
                return null;
            }
        }

        private static string GetNodeTemplate(string nodeType, string nodeId, float posX, float posY)
        {
            string lower = nodeType.ToLowerInvariant();

            // Common node templates
            string position = $"\"x\":{posX},\"y\":{posY},\"width\":208,\"height\":311";
            string drawState = $"\"m_DrawState\":{{\"m_Expanded\":true,\"m_Position\":{{\"serializedVersion\":\"2\",{position}}}}}";

            switch (lower)
            {
                case "add":
                case "addnode":
                    return $"{{\"m_ObjectId\":\"{nodeId}\",\"m_Type\":\"UnityEditor.ShaderGraph.AddNode\",\"m_Name\":\"Add\",{drawState},\"m_Slots\":[],\"m_SerializableSlots\":[]}}";
                case "multiply":
                case "multiplynode":
                    return $"{{\"m_ObjectId\":\"{nodeId}\",\"m_Type\":\"UnityEditor.ShaderGraph.MultiplyNode\",\"m_Name\":\"Multiply\",{drawState},\"m_Slots\":[],\"m_SerializableSlots\":[]}}";
                case "subtract":
                case "subtractnode":
                    return $"{{\"m_ObjectId\":\"{nodeId}\",\"m_Type\":\"UnityEditor.ShaderGraph.SubtractNode\",\"m_Name\":\"Subtract\",{drawState},\"m_Slots\":[],\"m_SerializableSlots\":[]}}";
                case "divide":
                case "dividenode":
                    return $"{{\"m_ObjectId\":\"{nodeId}\",\"m_Type\":\"UnityEditor.ShaderGraph.DivideNode\",\"m_Name\":\"Divide\",{drawState},\"m_Slots\":[],\"m_SerializableSlots\":[]}}";
                case "lerp":
                case "lerpnode":
                    return $"{{\"m_ObjectId\":\"{nodeId}\",\"m_Type\":\"UnityEditor.ShaderGraph.LerpNode\",\"m_Name\":\"Lerp\",{drawState},\"m_Slots\":[],\"m_SerializableSlots\":[]}}";
                case "color":
                case "colornode":
                    return $"{{\"m_ObjectId\":\"{nodeId}\",\"m_Type\":\"UnityEditor.ShaderGraph.ColorNode\",\"m_Name\":\"Color\",{drawState},\"m_Slots\":[],\"m_SerializableSlots\":[],\"m_Color\":{{\"r\":1,\"g\":1,\"b\":1,\"a\":1}}}}";
                case "float":
                case "vector1":
                case "vector1node":
                    return $"{{\"m_ObjectId\":\"{nodeId}\",\"m_Type\":\"UnityEditor.ShaderGraph.Vector1Node\",\"m_Name\":\"Float\",{drawState},\"m_Slots\":[],\"m_SerializableSlots\":[],\"m_Value\":0}}";
                case "vector2":
                case "vector2node":
                    return $"{{\"m_ObjectId\":\"{nodeId}\",\"m_Type\":\"UnityEditor.ShaderGraph.Vector2Node\",\"m_Name\":\"Vector 2\",{drawState},\"m_Slots\":[],\"m_SerializableSlots\":[]}}";
                case "vector3":
                case "vector3node":
                    return $"{{\"m_ObjectId\":\"{nodeId}\",\"m_Type\":\"UnityEditor.ShaderGraph.Vector3Node\",\"m_Name\":\"Vector 3\",{drawState},\"m_Slots\":[],\"m_SerializableSlots\":[]}}";
                case "vector4":
                case "vector4node":
                    return $"{{\"m_ObjectId\":\"{nodeId}\",\"m_Type\":\"UnityEditor.ShaderGraph.Vector4Node\",\"m_Name\":\"Vector 4\",{drawState},\"m_Slots\":[],\"m_SerializableSlots\":[]}}";
                case "time":
                case "timenode":
                    return $"{{\"m_ObjectId\":\"{nodeId}\",\"m_Type\":\"UnityEditor.ShaderGraph.TimeNode\",\"m_Name\":\"Time\",{drawState},\"m_Slots\":[],\"m_SerializableSlots\":[]}}";
                case "uv":
                case "uvnode":
                    return $"{{\"m_ObjectId\":\"{nodeId}\",\"m_Type\":\"UnityEditor.ShaderGraph.UVNode\",\"m_Name\":\"UV\",{drawState},\"m_Slots\":[],\"m_SerializableSlots\":[]}}";
                case "position":
                case "positionnode":
                    return $"{{\"m_ObjectId\":\"{nodeId}\",\"m_Type\":\"UnityEditor.ShaderGraph.PositionNode\",\"m_Name\":\"Position\",{drawState},\"m_Slots\":[],\"m_SerializableSlots\":[]}}";
                case "normal":
                case "normalnode":
                case "normalvector":
                case "normalvectornode":
                    return $"{{\"m_ObjectId\":\"{nodeId}\",\"m_Type\":\"UnityEditor.ShaderGraph.NormalVectorNode\",\"m_Name\":\"Normal Vector\",{drawState},\"m_Slots\":[],\"m_SerializableSlots\":[]}}";
                case "sampletexture2d":
                case "sampletexture2dnode":
                    return $"{{\"m_ObjectId\":\"{nodeId}\",\"m_Type\":\"UnityEditor.ShaderGraph.SampleTexture2DNode\",\"m_Name\":\"Sample Texture 2D\",{drawState},\"m_Slots\":[],\"m_SerializableSlots\":[]}}";
                case "fresnel":
                case "fresneleffect":
                case "fresneleffectnode":
                    return $"{{\"m_ObjectId\":\"{nodeId}\",\"m_Type\":\"UnityEditor.ShaderGraph.FresnelEffectNode\",\"m_Name\":\"Fresnel Effect\",{drawState},\"m_Slots\":[],\"m_SerializableSlots\":[]}}";
                case "saturate":
                case "saturatenode":
                    return $"{{\"m_ObjectId\":\"{nodeId}\",\"m_Type\":\"UnityEditor.ShaderGraph.SaturateNode\",\"m_Name\":\"Saturate\",{drawState},\"m_Slots\":[],\"m_SerializableSlots\":[]}}";
                case "oneminusx":
                case "oneminusnode":
                    return $"{{\"m_ObjectId\":\"{nodeId}\",\"m_Type\":\"UnityEditor.ShaderGraph.OneMinusNode\",\"m_Name\":\"One Minus\",{drawState},\"m_Slots\":[],\"m_SerializableSlots\":[]}}";
                case "power":
                case "powernode":
                    return $"{{\"m_ObjectId\":\"{nodeId}\",\"m_Type\":\"UnityEditor.ShaderGraph.PowerNode\",\"m_Name\":\"Power\",{drawState},\"m_Slots\":[],\"m_SerializableSlots\":[]}}";
                case "split":
                case "splitnode":
                    return $"{{\"m_ObjectId\":\"{nodeId}\",\"m_Type\":\"UnityEditor.ShaderGraph.SplitNode\",\"m_Name\":\"Split\",{drawState},\"m_Slots\":[],\"m_SerializableSlots\":[]}}";
                case "combine":
                case "combinenode":
                    return $"{{\"m_ObjectId\":\"{nodeId}\",\"m_Type\":\"UnityEditor.ShaderGraph.CombineNode\",\"m_Name\":\"Combine\",{drawState},\"m_Slots\":[],\"m_SerializableSlots\":[]}}";
                default:
                    return null;
            }
        }
    }
}
