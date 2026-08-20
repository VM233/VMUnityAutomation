using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace VMUnityAutomation.Editor
{
    /// <summary>
    /// Commands for visual intelligence: asset previews (base64 PNG), scene/game captures,
    /// and deep graphical metadata (mesh, material, texture, renderer, lighting).
    /// </summary>
    public static class VmAutomationGraphicsCommands
    {
        // ─── Helpers ───

        private static string TextureToBase64(Texture2D tex)
        {
            byte[] bytes = tex.EncodeToPNG();
            return System.Convert.ToBase64String(bytes);
        }

        /// <summary>
        /// AssetPreview.GetAssetPreview may return null on first call (async loading).
        /// Retry with short sleeps, then fall back to mini thumbnail.
        /// </summary>
        private static Texture2D GetPreviewWithRetry(UnityEngine.Object asset, int maxAttempts = 30)
        {
            AssetPreview.SetPreviewTextureCacheSize(256);

            for (int i = 0; i < maxAttempts; i++)
            {
                var preview = AssetPreview.GetAssetPreview(asset);
                if (preview != null) return preview;

                if (!VmObjectId.IsLoadingPreview(asset))
                    break;

                System.Threading.Thread.Sleep(100);
            }

            // Fallback to mini thumbnail (always available, smaller)
            return AssetPreview.GetMiniThumbnail(asset);
        }

        private static Dictionary<string, object> Vec3ToDict(Vector3 v)
        {
            return new Dictionary<string, object>
            {
                { "x", Math.Round(v.x, 4) },
                { "y", Math.Round(v.y, 4) },
                { "z", Math.Round(v.z, 4) },
            };
        }

        private static Dictionary<string, object> BoundsToDict(Bounds b)
        {
            return new Dictionary<string, object>
            {
                { "center", Vec3ToDict(b.center) },
                { "size", Vec3ToDict(b.size) },
                { "extents", Vec3ToDict(b.extents) },
                { "min", Vec3ToDict(b.min) },
                { "max", Vec3ToDict(b.max) },
            };
        }

        private static Dictionary<string, object> ColorToDict(Color c)
        {
            return new Dictionary<string, object>
            {
                { "r", Math.Round(c.r, 4) },
                { "g", Math.Round(c.g, 4) },
                { "b", Math.Round(c.b, 4) },
                { "a", Math.Round(c.a, 4) },
            };
        }

        // ─── 1. Asset Preview (Base64 PNG) ───

        public static object CaptureAssetPreview(Dictionary<string, object> args)
        {
            string assetPath = args.ContainsKey("assetPath") ? args["assetPath"].ToString() : "";
            if (string.IsNullOrEmpty(assetPath))
                return new { error = "assetPath is required" };

            var asset = AssetDatabase.LoadMainAssetAtPath(assetPath);
            if (asset == null)
                return new { error = $"Asset not found at '{assetPath}'" };

            var preview = GetPreviewWithRetry(asset);
            if (preview == null)
                return new { error = $"Could not generate preview for '{assetPath}'. Asset type may not support previews." };

            // AssetPreview textures are not always readable, so copy to a readable texture
            RenderTexture rt = null;
            Texture2D readable = null;
            try
            {
                rt = RenderTexture.GetTemporary(preview.width, preview.height, 0);
                Graphics.Blit(preview, rt);
                RenderTexture.active = rt;
                readable = new Texture2D(preview.width, preview.height, TextureFormat.RGBA32, false);
                readable.ReadPixels(new Rect(0, 0, preview.width, preview.height), 0, 0);
                readable.Apply();
                RenderTexture.active = null;

                string base64 = TextureToBase64(readable);

                return new Dictionary<string, object>
                {
                    { "success", true },
                    { "base64", base64 },
                    { "width", readable.width },
                    { "height", readable.height },
                    { "assetPath", assetPath },
                    { "assetType", asset.GetType().Name },
                };
            }
            finally
            {
                RenderTexture.active = null;
                if (rt != null) RenderTexture.ReleaseTemporary(rt);
                if (readable != null) UnityEngine.Object.DestroyImmediate(readable);
            }
        }

        // ─── 2. Mesh Info ───

        public static object GetMeshInfo(Dictionary<string, object> args)
        {
            string assetPath = args.ContainsKey("assetPath") ? args["assetPath"].ToString() : "";
            string gameObjectPath = args.ContainsKey("gameObjectPath") ? args["gameObjectPath"].ToString() : "";

            Mesh mesh = null;
            string source = "";
            bool isSkinned = false;
            int boneCount = 0;

            // Try loading by asset path first
            if (!string.IsNullOrEmpty(assetPath))
            {
                // Could be a mesh asset or a model (FBX) containing meshes
                var loaded = AssetDatabase.LoadAssetAtPath<Mesh>(assetPath);
                if (loaded != null)
                {
                    mesh = loaded;
                    source = assetPath;
                }
                else
                {
                    // Try loading as a model and getting the first mesh
                    var go = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                    if (go != null)
                    {
                        var smr = go.GetComponentInChildren<SkinnedMeshRenderer>();
                        if (smr != null && smr.sharedMesh != null)
                        {
                            mesh = smr.sharedMesh;
                            source = assetPath + " (SkinnedMeshRenderer)";
                            isSkinned = true;
                            boneCount = smr.bones != null ? smr.bones.Length : 0;
                        }
                        else
                        {
                            var mf = go.GetComponentInChildren<MeshFilter>();
                            if (mf != null && mf.sharedMesh != null)
                            {
                                mesh = mf.sharedMesh;
                                source = assetPath + " (MeshFilter)";
                            }
                        }
                    }
                }
            }

            // Try finding in scene by GameObject path
            if (mesh == null && !string.IsNullOrEmpty(gameObjectPath))
            {
                var go = GameObject.Find(gameObjectPath);
                if (go != null)
                {
                    var smr = go.GetComponent<SkinnedMeshRenderer>();
                    if (smr != null && smr.sharedMesh != null)
                    {
                        mesh = smr.sharedMesh;
                        source = gameObjectPath + " (SkinnedMeshRenderer)";
                        isSkinned = true;
                        boneCount = smr.bones != null ? smr.bones.Length : 0;
                    }
                    else
                    {
                        var mf = go.GetComponent<MeshFilter>();
                        if (mf != null && mf.sharedMesh != null)
                        {
                            mesh = mf.sharedMesh;
                            source = gameObjectPath + " (MeshFilter)";
                        }
                    }
                }
            }

            if (mesh == null)
                return new { error = "No mesh found. Provide assetPath to a mesh/model asset or gameObjectPath to a scene object with MeshFilter/SkinnedMeshRenderer." };

            // Count UV channels
            int uvChannels = 0;
            if (mesh.uv != null && mesh.uv.Length > 0) uvChannels++;
            if (mesh.uv2 != null && mesh.uv2.Length > 0) uvChannels++;
            if (mesh.uv3 != null && mesh.uv3.Length > 0) uvChannels++;
            if (mesh.uv4 != null && mesh.uv4.Length > 0) uvChannels++;

            return new Dictionary<string, object>
            {
                { "name", mesh.name },
                { "source", source },
                { "vertexCount", mesh.vertexCount },
                { "triangleCount", mesh.triangles.Length / 3 },
                { "subMeshCount", mesh.subMeshCount },
                { "bounds", BoundsToDict(mesh.bounds) },
                { "uvChannelCount", uvChannels },
                { "hasNormals", mesh.normals != null && mesh.normals.Length > 0 },
                { "hasTangents", mesh.tangents != null && mesh.tangents.Length > 0 },
                { "hasColors", mesh.colors != null && mesh.colors.Length > 0 },
                { "blendShapeCount", mesh.blendShapeCount },
                { "isSkinned", isSkinned },
                { "boneCount", boneCount },
                { "isReadable", mesh.isReadable },
                { "indexFormat", mesh.indexFormat.ToString() },
            };
        }

        // ─── 3. Material Info (with preview) ───

        public static object GetMaterialInfo(Dictionary<string, object> args)
        {
            string assetPath = args.ContainsKey("assetPath") ? args["assetPath"].ToString() : "";
            string gameObjectPath = args.ContainsKey("gameObjectPath") ? args["gameObjectPath"].ToString() : "";
            int materialIndex = args.ContainsKey("materialIndex") ? Convert.ToInt32(args["materialIndex"]) : 0;

            Material mat = null;

            if (!string.IsNullOrEmpty(assetPath))
            {
                mat = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
            }

            if (mat == null && !string.IsNullOrEmpty(gameObjectPath))
            {
                var go = GameObject.Find(gameObjectPath);
                if (go != null)
                {
                    var renderer = go.GetComponent<Renderer>();
                    if (renderer != null && renderer.sharedMaterials.Length > materialIndex)
                        mat = renderer.sharedMaterials[materialIndex];
                }
            }

            if (mat == null)
                return new { error = "Material not found. Provide assetPath to a .mat file or gameObjectPath + materialIndex." };

            var shader = mat.shader;
            var result = new Dictionary<string, object>
            {
                { "name", mat.name },
                { "shaderName", shader.name },
                { "renderQueue", mat.renderQueue },
                { "passCount", mat.passCount },
                { "doubleSidedGI", mat.doubleSidedGI },
                { "enableInstancing", mat.enableInstancing },
                { "globalIlluminationFlags", mat.globalIlluminationFlags.ToString() },
            };

            // Keywords
            var keywords = mat.shaderKeywords;
            result["enabledKeywords"] = keywords != null ? keywords.ToList() : new List<string>();

            // Shader properties
            var properties = new List<Dictionary<string, object>>();
            int propCount = shader.GetPropertyCount();
            for (int i = 0; i < propCount; i++)
            {
                string propName = shader.GetPropertyName(i);
                var propType = shader.GetPropertyType(i);
                var propDict = new Dictionary<string, object>
                {
                    { "name", propName },
                    { "type", propType.ToString() },
                    { "description", shader.GetPropertyDescription(i) },
                };

                try
                {
                    switch (propType)
                    {
                        case ShaderPropertyType.Color:
                            propDict["value"] = ColorToDict(mat.GetColor(propName));
                            break;
                        case ShaderPropertyType.Float:
                        case ShaderPropertyType.Range:
                            propDict["value"] = Math.Round(mat.GetFloat(propName), 4);
                            break;
                        case ShaderPropertyType.Vector:
                            var v = mat.GetVector(propName);
                            propDict["value"] = new Dictionary<string, object>
                            {
                                { "x", Math.Round(v.x, 4) }, { "y", Math.Round(v.y, 4) },
                                { "z", Math.Round(v.z, 4) }, { "w", Math.Round(v.w, 4) },
                            };
                            break;
                        case ShaderPropertyType.Texture:
                            var tex = mat.GetTexture(propName);
                            if (tex != null)
                            {
                                propDict["value"] = new Dictionary<string, object>
                                {
                                    { "name", tex.name },
                                    { "assetPath", AssetDatabase.GetAssetPath(tex) },
                                    { "width", tex.width },
                                    { "height", tex.height },
                                };
                            }
                            else
                            {
                                propDict["value"] = null;
                            }
                            break;
                        case ShaderPropertyType.Int:
                            propDict["value"] = mat.GetInt(propName);
                            break;
                    }
                }
                catch
                {
                    propDict["value"] = "(unreadable)";
                }

                properties.Add(propDict);
            }
            result["properties"] = properties;

            // Material preview thumbnail
            string base64 = null;
            try
            {
                var preview = GetPreviewWithRetry(mat, 20);
                if (preview != null)
                {
                    RenderTexture rt = RenderTexture.GetTemporary(preview.width, preview.height, 0);
                    try
                    {
                        Graphics.Blit(preview, rt);
                        RenderTexture.active = rt;
                        var readable = new Texture2D(preview.width, preview.height, TextureFormat.RGBA32, false);
                        readable.ReadPixels(new Rect(0, 0, preview.width, preview.height), 0, 0);
                        readable.Apply();
                        RenderTexture.active = null;
                        base64 = TextureToBase64(readable);
                        UnityEngine.Object.DestroyImmediate(readable);
                    }
                    finally
                    {
                        RenderTexture.active = null;
                        RenderTexture.ReleaseTemporary(rt);
                    }
                }
            }
            catch { /* preview optional, don't fail */ }

            if (base64 != null) result["base64"] = base64;

            return result;
        }

        public static object InspectImageAlphaBounds(Dictionary<string, object> args)
        {
            string assetPath = GetString(args, "assetPath");
            string filePath = GetString(args, "filePath");

            Texture2D source = null;
            string resolvedPath = assetPath;
            if (string.IsNullOrEmpty(assetPath) == false)
            {
                source = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
                if (source == null)
                    return new { error = $"Texture2D asset not found at '{assetPath}'" };
            }
            else if (string.IsNullOrEmpty(filePath) == false)
            {
                if (File.Exists(filePath) == false)
                    return new { error = $"Image file not found at '{filePath}'" };

                source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                source.LoadImage(File.ReadAllBytes(filePath));
                resolvedPath = filePath;
            }
            else
            {
                return new { error = "assetPath or filePath is required" };
            }

            float alphaThreshold = GetFloat(args, "alphaThreshold", 0.01f);
            byte thresholdByte = alphaThreshold <= 1f
                ? (byte)Mathf.Clamp(Mathf.RoundToInt(alphaThreshold * 255f), 0, 255)
                : (byte)Mathf.Clamp(Mathf.RoundToInt(alphaThreshold), 0, 255);

            Texture2D readable = null;
            bool destroySource = string.IsNullOrEmpty(assetPath);
            try
            {
                readable = CopyToReadableTexture(source);
                var pixels = readable.GetPixels32();
                int width = readable.width;
                int height = readable.height;
                int minX = width;
                int minY = height;
                int maxX = -1;
                int maxY = -1;
                int opaqueCount = 0;

                for (int y = 0; y < height; y++)
                {
                    int row = y * width;
                    for (int x = 0; x < width; x++)
                    {
                        if (pixels[row + x].a < thresholdByte)
                            continue;

                        opaqueCount++;
                        if (x < minX) minX = x;
                        if (y < minY) minY = y;
                        if (x > maxX) maxX = x;
                        if (y > maxY) maxY = y;
                    }
                }

                bool hasAlphaPixels = opaqueCount > 0;
                var result = new Dictionary<string, object>
                {
                    { "success", true },
                    { "path", resolvedPath },
                    { "width", width },
                    { "height", height },
                    { "alphaThreshold", thresholdByte },
                    { "hasAlphaPixels", hasAlphaPixels },
                    { "alphaPixelCount", opaqueCount },
                };

                if (hasAlphaPixels)
                {
                    int boundsWidth = maxX - minX + 1;
                    int boundsHeight = maxY - minY + 1;
                    result["boundsBottomLeft"] = new Dictionary<string, object>
                    {
                        { "x", minX },
                        { "y", minY },
                        { "width", boundsWidth },
                        { "height", boundsHeight },
                        { "xMin", minX },
                        { "yMin", minY },
                        { "xMax", maxX + 1 },
                        { "yMax", maxY + 1 },
                    };
                    result["boundsTopLeft"] = new Dictionary<string, object>
                    {
                        { "x", minX },
                        { "y", height - maxY - 1 },
                        { "width", boundsWidth },
                        { "height", boundsHeight },
                        { "xMin", minX },
                        { "yMin", height - maxY - 1 },
                        { "xMax", maxX + 1 },
                        { "yMax", height - minY },
                    };
                    result["transparentMargins"] = new Dictionary<string, object>
                    {
                        { "left", minX },
                        { "right", width - maxX - 1 },
                        { "bottom", minY },
                        { "top", height - maxY - 1 },
                    };
                }

                return result;
            }
            finally
            {
                if (readable != null)
                    UnityEngine.Object.DestroyImmediate(readable);
                if (destroySource && source != null)
                    UnityEngine.Object.DestroyImmediate(source);
            }
        }

        public static object MeasureRectGap(Dictionary<string, object> args)
        {
            if (!TryGetRect(args, "firstRect", out Rect firstRect))
                return new { error = "firstRect is required with x, y, width, and height" };
            if (!TryGetRect(args, "secondRect", out Rect secondRect))
                return new { error = "secondRect is required with x, y, width, and height" };

            string axis = GetString(args, "axis").ToLowerInvariant();
            if (axis != "y")
                axis = "x";

            string firstEdge = GetString(args, "firstEdge");
            string secondEdge = GetString(args, "secondEdge");
            if (string.IsNullOrEmpty(firstEdge))
                firstEdge = axis == "x" ? "right" : "bottom";
            if (string.IsNullOrEmpty(secondEdge))
                secondEdge = axis == "x" ? "left" : "top";

            float tolerance = GetFloat(args, "tolerance", 0.5f);
            float firstValue = GetRectEdge(firstRect, firstEdge);
            float secondValue = GetRectEdge(secondRect, secondEdge);
            float delta = secondValue - firstValue;

            return new Dictionary<string, object>
            {
                { "success", true },
                { "touching", Math.Abs(delta) <= tolerance },
                { "axis", axis },
                { "firstEdge", firstEdge },
                { "secondEdge", secondEdge },
                { "firstValue", firstValue },
                { "secondValue", secondValue },
                { "delta", delta },
                { "gap", delta > tolerance ? delta : 0 },
                { "overlap", delta < -tolerance ? -delta : 0 },
                { "tolerance", tolerance },
            };
        }

        public static object AnnotateRects(Dictionary<string, object> args)
        {
            string sourcePath = GetString(args, "sourcePath");
            if (string.IsNullOrEmpty(sourcePath))
                return new { error = "sourcePath is required" };

            string absoluteSourcePath = ResolveAbsolutePath(sourcePath);
            if (File.Exists(absoluteSourcePath) == false)
                return new { error = $"Image file not found at '{absoluteSourcePath}'" };

            var rects = GetDictionaryList(args, "rects");
            if (rects.Count == 0)
                return new { error = "rects is required and must contain at least one rectangle" };

            string outputPath = GetString(args, "outputPath");
            if (string.IsNullOrEmpty(outputPath))
            {
                outputPath = Path.Combine(Path.GetDirectoryName(absoluteSourcePath) ?? "",
                    Path.GetFileNameWithoutExtension(absoluteSourcePath) + "_annotated.png");
            }

            outputPath = ResolveAbsolutePath(outputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? "");

            bool originTopLeft = GetBool(args, "originTopLeft", true);
            int defaultThickness = Math.Max(1, Mathf.RoundToInt(GetFloat(args, "thickness", 2)));
            Color defaultColor = ParseColor(GetString(args, "color"), Color.yellow);

            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                texture.LoadImage(File.ReadAllBytes(absoluteSourcePath));

                var annotations = new List<Dictionary<string, object>>();
                foreach (var rectArgs in rects)
                {
                    if (!TryGetRect(rectArgs, "rect", out Rect rect))
                    {
                        if (!TryGetRectFromDictionary(rectArgs, out rect))
                            continue;
                    }

                    int thickness = Math.Max(1, Mathf.RoundToInt(GetFloat(rectArgs, "thickness", defaultThickness)));
                    Color color = ParseColor(GetString(rectArgs, "color"), defaultColor);
                    Rect pixelRect = originTopLeft
                        ? new Rect(rect.x, texture.height - rect.y - rect.height, rect.width, rect.height)
                        : rect;

                    DrawRectBorder(texture, pixelRect, color, thickness);
                    annotations.Add(new Dictionary<string, object>
                    {
                        { "label", GetString(rectArgs, "label") },
                        { "rect", RectToDictionary(rect) },
                        { "pixelRectBottomLeft", RectToDictionary(pixelRect) },
                        { "thickness", thickness },
                        { "color", ColorToDict(color) },
                    });
                }

                texture.Apply();
                File.WriteAllBytes(outputPath, texture.EncodeToPNG());

                return new Dictionary<string, object>
                {
                    { "success", true },
                    { "sourcePath", absoluteSourcePath },
                    { "outputPath", outputPath },
                    { "width", texture.width },
                    { "height", texture.height },
                    { "annotationCount", annotations.Count },
                    { "annotations", annotations },
                };
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        // ─── 4. Renderer Info ───

        public static object GetRendererInfo(Dictionary<string, object> args)
        {
            string gameObjectPath = args.ContainsKey("gameObjectPath") ? args["gameObjectPath"].ToString() : "";
            if (string.IsNullOrEmpty(gameObjectPath))
                return new { error = "gameObjectPath is required" };

            var go = GameObject.Find(gameObjectPath);
            if (go == null)
                return new { error = $"GameObject '{gameObjectPath}' not found in scene" };

            var renderer = go.GetComponent<Renderer>();
            if (renderer == null)
                return new { error = $"No Renderer component found on '{gameObjectPath}'" };

            var result = new Dictionary<string, object>
            {
                { "gameObjectPath", gameObjectPath },
                { "rendererType", renderer.GetType().Name },
                { "enabled", renderer.enabled },
                { "isVisible", renderer.isVisible },
                { "bounds", BoundsToDict(renderer.bounds) },
                { "shadowCastingMode", renderer.shadowCastingMode.ToString() },
                { "receiveShadows", renderer.receiveShadows },
                { "lightmapIndex", renderer.lightmapIndex },
                { "sortingLayerName", renderer.sortingLayerName },
                { "sortingOrder", renderer.sortingOrder },
                { "lightProbeUsage", renderer.lightProbeUsage.ToString() },
                { "reflectionProbeUsage", renderer.reflectionProbeUsage.ToString() },
            };

            // Materials
            var matList = new List<Dictionary<string, object>>();
            foreach (var mat in renderer.sharedMaterials)
            {
                if (mat != null)
                {
                    matList.Add(new Dictionary<string, object>
                    {
                        { "name", mat.name },
                        { "shaderName", mat.shader != null ? mat.shader.name : "(null)" },
                        { "assetPath", AssetDatabase.GetAssetPath(mat) },
                        { "renderQueue", mat.renderQueue },
                    });
                }
                else
                {
                    matList.Add(new Dictionary<string, object> { { "name", "(null/missing)" } });
                }
            }
            result["materials"] = matList;
            result["materialCount"] = matList.Count;

            // Mesh info
            Mesh mesh = null;
            if (renderer is SkinnedMeshRenderer smr && smr.sharedMesh != null)
            {
                mesh = smr.sharedMesh;
                result["isSkinned"] = true;
                result["boneCount"] = smr.bones != null ? smr.bones.Length : 0;
            }
            else
            {
                var mf = go.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null)
                    mesh = mf.sharedMesh;
                result["isSkinned"] = false;
            }

            if (mesh != null)
            {
                result["mesh"] = new Dictionary<string, object>
                {
                    { "name", mesh.name },
                    { "vertexCount", mesh.vertexCount },
                    { "triangleCount", mesh.triangles.Length / 3 },
                    { "assetPath", AssetDatabase.GetAssetPath(mesh) },
                };
            }

            return result;
        }

        // ─── 5. Lighting Summary ───

        public static object GetLightingSummary(Dictionary<string, object> args)
        {
            string lightName = args.ContainsKey("lightName") ? args["lightName"].ToString() : "";

            Light[] allLights;
            if (!string.IsNullOrEmpty(lightName))
            {
                var go = GameObject.Find(lightName);
                if (go == null)
                    return new { error = $"GameObject '{lightName}' not found" };
                var light = go.GetComponent<Light>();
                if (light == null)
                    return new { error = $"No Light component found on '{lightName}'" };
                allLights = new[] { light };
            }
            else
            {
                allLights = VmAutomationObjectSearch.Find<Light>();
            }

            var lights = new List<Dictionary<string, object>>();
            foreach (var light in allLights)
            {
                var entry = new Dictionary<string, object>
                {
                    { "name", light.gameObject.name },
                    { "type", light.type.ToString() },
                    { "color", ColorToDict(light.color) },
                    { "intensity", Math.Round(light.intensity, 4) },
                    { "range", Math.Round(light.range, 4) },
                    { "enabled", light.enabled },
                    { "gameObjectActive", light.gameObject.activeInHierarchy },
                    { "shadows", light.shadows.ToString() },
                    { "shadowStrength", Math.Round(light.shadowStrength, 4) },
                    { "renderMode", light.renderMode.ToString() },
                    { "cullingMask", light.cullingMask },
                    { "bounceIntensity", Math.Round(light.bounceIntensity, 4) },
                };

                if (light.type == LightType.Spot)
                {
                    entry["spotAngle"] = Math.Round(light.spotAngle, 2);
                    entry["innerSpotAngle"] = Math.Round(light.innerSpotAngle, 2);
                }

                lights.Add(entry);
            }

            return new Dictionary<string, object>
            {
                { "lightCount", lights.Count },
                { "lights", lights },
            };
        }

        public static object CompareImages(Dictionary<string, object> args)
        {
            string expectedPath = GetString(args, "expectedPath");
            string actualPath = GetString(args, "actualPath");

            if (string.IsNullOrEmpty(expectedPath) || string.IsNullOrEmpty(actualPath))
                return new { error = "expectedPath and actualPath are required" };

            string expectedAbsolute = ResolveAbsolutePath(expectedPath);
            string actualAbsolute = ResolveAbsolutePath(actualPath);
            if (File.Exists(expectedAbsolute) == false)
                return new { error = $"Expected image not found: '{expectedPath}'" };
            if (File.Exists(actualAbsolute) == false)
                return new { error = $"Actual image not found: '{actualPath}'" };

            Texture2D expected = null;
            Texture2D actual = null;
            Texture2D diff = null;
            try
            {
                expected = LoadImageFile(expectedAbsolute);
                actual = LoadImageFile(actualAbsolute);

                Rect expectedRect = new Rect(0, 0, expected.width, expected.height);
                Rect actualRect = new Rect(0, 0, actual.width, actual.height);
                if (TryGetRect(args, "expectedRect", out var suppliedExpectedRect))
                    expectedRect = suppliedExpectedRect;
                if (TryGetRect(args, "referenceRect", out suppliedExpectedRect))
                    expectedRect = suppliedExpectedRect;
                if (TryGetRect(args, "actualRect", out var suppliedActualRect))
                    actualRect = suppliedActualRect;
                if (TryGetRect(args, "currentRect", out suppliedActualRect))
                    actualRect = suppliedActualRect;

                int width = Mathf.Min(Mathf.RoundToInt(expectedRect.width), Mathf.RoundToInt(actualRect.width));
                int height = Mathf.Min(Mathf.RoundToInt(expectedRect.height), Mathf.RoundToInt(actualRect.height));
                if (width <= 0 || height <= 0)
                    return new { error = "Comparison rects must have positive overlapping size" };

                int tolerance = Mathf.Clamp(Mathf.RoundToInt(GetFloat(args, "tolerance", 0)), 0, 255);
                int maxSamples = Mathf.Max(0, Mathf.RoundToInt(GetFloat(args, "maxSamples", 20)));
                string diffOutputPath = GetString(args, "diffOutputPath");
                bool writeDiff = string.IsNullOrEmpty(diffOutputPath) == false;
                if (writeDiff)
                    diff = new Texture2D(width, height, TextureFormat.RGBA32, false);

                int differentPixels = 0;
                int minX = width;
                int minY = height;
                int maxX = -1;
                int maxY = -1;
                long totalDelta = 0;
                int maxDelta = 0;
                var samples = new List<Dictionary<string, object>>();

                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        var expectedColor = expected.GetPixel(
                            Mathf.Clamp(Mathf.RoundToInt(expectedRect.x) + x, 0, expected.width - 1),
                            Mathf.Clamp(Mathf.RoundToInt(expectedRect.y) + y, 0, expected.height - 1));
                        var actualColor = actual.GetPixel(
                            Mathf.Clamp(Mathf.RoundToInt(actualRect.x) + x, 0, actual.width - 1),
                            Mathf.Clamp(Mathf.RoundToInt(actualRect.y) + y, 0, actual.height - 1));

                        int delta = ColorDelta(expectedColor, actualColor);
                        bool different = delta > tolerance;
                        if (different)
                        {
                            differentPixels++;
                            totalDelta += delta;
                            maxDelta = Math.Max(maxDelta, delta);
                            minX = Math.Min(minX, x);
                            minY = Math.Min(minY, y);
                            maxX = Math.Max(maxX, x);
                            maxY = Math.Max(maxY, y);

                            if (samples.Count < maxSamples)
                            {
                                samples.Add(new Dictionary<string, object>
                                {
                                    { "x", x },
                                    { "y", y },
                                    { "delta", delta },
                                    { "expected", ColorToDict(expectedColor) },
                                    { "actual", ColorToDict(actualColor) },
                                });
                            }
                        }

                        if (writeDiff)
                            diff.SetPixel(x, y, different ? Color.red : new Color(actualColor.r, actualColor.g, actualColor.b, 0.25f));
                    }
                }

                string absoluteDiffOutput = "";
                if (writeDiff)
                {
                    diff.Apply();
                    absoluteDiffOutput = ResolveAbsolutePath(diffOutputPath);
                    string directory = Path.GetDirectoryName(absoluteDiffOutput);
                    if (string.IsNullOrEmpty(directory) == false)
                        Directory.CreateDirectory(directory);
                    File.WriteAllBytes(absoluteDiffOutput, diff.EncodeToPNG());
                    RefreshAssetIfNeeded(absoluteDiffOutput);
                }

                int totalPixels = width * height;
                var result = new Dictionary<string, object>
                {
                    { "success", true },
                    { "expectedPath", expectedPath },
                    { "actualPath", actualPath },
                    { "expectedSize", new Dictionary<string, object> { { "width", expected.width }, { "height", expected.height } } },
                    { "actualSize", new Dictionary<string, object> { { "width", actual.width }, { "height", actual.height } } },
                    { "comparedWidth", width },
                    { "comparedHeight", height },
                    { "tolerance", tolerance },
                    { "differentPixels", differentPixels },
                    { "totalPixels", totalPixels },
                    { "differentRatio", totalPixels > 0 ? Math.Round(differentPixels / (double)totalPixels, 6) : 0 },
                    { "averageDelta", differentPixels > 0 ? Math.Round(totalDelta / (double)differentPixels, 3) : 0 },
                    { "maxDelta", maxDelta },
                    { "samples", samples },
                };

                if (differentPixels > 0)
                    result["differenceBounds"] = RectToDictionary(new Rect(minX, minY, maxX - minX + 1, maxY - minY + 1));

                if (writeDiff)
                {
                    result["diffOutputPath"] = diffOutputPath;
                    result["absoluteDiffOutputPath"] = absoluteDiffOutput;
                }

                return result;
            }
            finally
            {
                if (expected != null)
                    UnityEngine.Object.DestroyImmediate(expected);
                if (actual != null)
                    UnityEngine.Object.DestroyImmediate(actual);
                if (diff != null)
                    UnityEngine.Object.DestroyImmediate(diff);
            }
        }

        private static Texture2D CopyToReadableTexture(Texture texture)
        {
            RenderTexture rt = null;
            Texture2D readable = null;
            RenderTexture previous = RenderTexture.active;
            try
            {
                rt = RenderTexture.GetTemporary(texture.width, texture.height, 0, RenderTextureFormat.ARGB32);
                Graphics.Blit(texture, rt);
                RenderTexture.active = rt;
                readable = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, false);
                readable.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
                readable.Apply();
                return readable;
            }
            finally
            {
                RenderTexture.active = previous;
                if (rt != null)
                    RenderTexture.ReleaseTemporary(rt);
            }
        }

        private static string GetString(Dictionary<string, object> args, string key)
        {
            return args != null && args.ContainsKey(key) && args[key] != null ? args[key].ToString() : "";
        }

        private static bool GetBool(Dictionary<string, object> args, string key, bool defaultValue)
        {
            if (args == null || !args.ContainsKey(key) || args[key] == null)
                return defaultValue;

            if (args[key] is bool value)
                return value;

            return bool.TryParse(args[key].ToString(), out bool parsed) ? parsed : defaultValue;
        }

        private static float GetFloat(Dictionary<string, object> args, string key, float defaultValue)
        {
            if (args == null || !args.ContainsKey(key) || args[key] == null)
                return defaultValue;

            return float.TryParse(args[key].ToString(), out float parsed) ? parsed : defaultValue;
        }

        private static List<Dictionary<string, object>> GetDictionaryList(Dictionary<string, object> args, string key)
        {
            var results = new List<Dictionary<string, object>>();
            if (args == null || !args.TryGetValue(key, out object value) || value == null)
                return results;

            if (value is List<object> list)
            {
                foreach (object item in list)
                {
                    if (item is Dictionary<string, object> dict)
                        results.Add(dict);
                }
            }

            return results;
        }

        private static bool TryGetRectFromDictionary(Dictionary<string, object> args, out Rect rect)
        {
            rect = default(Rect);
            if (args == null)
                return false;

            float x = GetFloat(args, "x", 0);
            float y = GetFloat(args, "y", 0);
            float width = GetFloat(args, "width", float.NaN);
            float height = GetFloat(args, "height", float.NaN);
            if (float.IsNaN(width) || float.IsNaN(height))
                return false;

            rect = new Rect(x, y, width, height);
            return true;
        }

        private static bool TryGetRect(Dictionary<string, object> args, string key, out Rect rect)
        {
            rect = default(Rect);
            if (args == null || args.TryGetValue(key, out object value) == false)
                return false;

            var dictionary = value as Dictionary<string, object>;
            if (dictionary == null)
            {
                return false;
            }

            float x = GetFloat(dictionary, "x", 0);
            float y = GetFloat(dictionary, "y", 0);
            float width = GetFloat(dictionary, "width", float.NaN);
            float height = GetFloat(dictionary, "height", float.NaN);
            if (float.IsNaN(width) || float.IsNaN(height))
                return false;

            rect = new Rect(x, y, width, height);
            return true;
        }

        private static Dictionary<string, object> RectToDictionary(Rect rect)
        {
            return new Dictionary<string, object>
            {
                { "x", rect.x },
                { "y", rect.y },
                { "width", rect.width },
                { "height", rect.height },
                { "xMin", rect.xMin },
                { "yMin", rect.yMin },
                { "xMax", rect.xMax },
                { "yMax", rect.yMax },
            };
        }

        private static string ResolveAbsolutePath(string path)
        {
            if (Path.IsPathRooted(path))
                return Path.GetFullPath(path);

            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            return Path.GetFullPath(Path.Combine(projectRoot, path));
        }

        private static Texture2D LoadImageFile(string absolutePath)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (texture.LoadImage(File.ReadAllBytes(absolutePath)) == false)
            {
                UnityEngine.Object.DestroyImmediate(texture);
                throw new InvalidOperationException($"Could not decode image '{absolutePath}'");
            }

            return texture;
        }

        private static int ColorDelta(Color a, Color b)
        {
            int dr = Mathf.Abs(Mathf.RoundToInt(a.r * 255f) - Mathf.RoundToInt(b.r * 255f));
            int dg = Mathf.Abs(Mathf.RoundToInt(a.g * 255f) - Mathf.RoundToInt(b.g * 255f));
            int db = Mathf.Abs(Mathf.RoundToInt(a.b * 255f) - Mathf.RoundToInt(b.b * 255f));
            int da = Mathf.Abs(Mathf.RoundToInt(a.a * 255f) - Mathf.RoundToInt(b.a * 255f));
            return Math.Max(Math.Max(dr, dg), Math.Max(db, da));
        }

        private static void RefreshAssetIfNeeded(string absolutePath)
        {
            string normalized = absolutePath.Replace('\\', '/');
            string assetsRoot = Application.dataPath.Replace('\\', '/');
            if (normalized.StartsWith(assetsRoot, StringComparison.OrdinalIgnoreCase))
                AssetDatabase.Refresh();
        }

        private static Color ParseColor(string value, Color defaultColor)
        {
            if (string.IsNullOrEmpty(value))
                return defaultColor;

            return ColorUtility.TryParseHtmlString(value, out var color) ? color : defaultColor;
        }

        private static void DrawRectBorder(Texture2D texture, Rect rect, Color color, int thickness)
        {
            int xMin = Mathf.Clamp(Mathf.RoundToInt(rect.xMin), 0, texture.width - 1);
            int xMax = Mathf.Clamp(Mathf.RoundToInt(rect.xMax) - 1, 0, texture.width - 1);
            int yMin = Mathf.Clamp(Mathf.RoundToInt(rect.yMin), 0, texture.height - 1);
            int yMax = Mathf.Clamp(Mathf.RoundToInt(rect.yMax) - 1, 0, texture.height - 1);

            for (int i = 0; i < thickness; i++)
            {
                int top = Mathf.Clamp(yMax - i, 0, texture.height - 1);
                int bottom = Mathf.Clamp(yMin + i, 0, texture.height - 1);
                int left = Mathf.Clamp(xMin + i, 0, texture.width - 1);
                int right = Mathf.Clamp(xMax - i, 0, texture.width - 1);

                for (int x = xMin; x <= xMax; x++)
                {
                    texture.SetPixel(x, top, color);
                    texture.SetPixel(x, bottom, color);
                }

                for (int y = yMin; y <= yMax; y++)
                {
                    texture.SetPixel(left, y, color);
                    texture.SetPixel(right, y, color);
                }
            }
        }

        private static float GetRectEdge(Rect rect, string edge)
        {
            switch ((edge ?? "").ToLowerInvariant())
            {
                case "left":
                case "xmin":
                    return rect.xMin;
                case "right":
                case "xmax":
                    return rect.xMax;
                case "top":
                case "ymin":
                    return rect.yMin;
                case "bottom":
                case "ymax":
                    return rect.yMax;
                case "centerx":
                    return rect.center.x;
                case "centery":
                    return rect.center.y;
                default:
                    throw new ArgumentException($"Unknown rect edge '{edge}'");
            }
        }
    }
}
