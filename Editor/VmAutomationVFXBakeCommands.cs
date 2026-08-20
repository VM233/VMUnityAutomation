using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace VMUnityAutomation.Editor
{
    internal static class VmAutomationVFXBakeCommands
    {
        private const long MaxSdfVoxelCount = 1L << 24;
        private const long MaxTexturePixelCount = 1L << 24;
        private const int MaxPointCount = 1000000;

        internal static object Bake(Dictionary<string, object> args)
        {
            if (!ValidateKeys(args, new[]
                {
                    "kind", "meshPath", "texturePath", "outputPath", "overwrite",
                    "boxSize", "boxCenter", "maxResolution", "signPassCount",
                    "threshold", "offset", "meshBakeMode", "distribution",
                    "pointCount", "seed", "exportNormals", "exportColors",
                    "exportUV", "format", "thresholdMode", "randomize",
                    "_agentId",
                }, out object keyError))
                return keyError;
            if (!VmAutomationVFXReflection.IsAvailable)
                return VmAutomationResponse.Error(
                    "VFX Graph is not available. Install com.unity.visualeffectgraph.",
                    "capability_unavailable");
            string kind = GetString(args, "kind").ToLowerInvariant();
            if (kind != "sdf" && kind != "point-cache-mesh" &&
                kind != "point-cache-texture")
                return VmAutomationResponse.Error(
                    "kind must be sdf, point-cache-mesh, or point-cache-texture.",
                    "invalid_arguments");
            string outputPath = GetString(args, "outputPath");
            string expectedExtension = kind == "sdf" ? ".asset" : ".pcache";
            if (!VmAutomationVFXAssetPath.TryNormalizeFile(outputPath, false,
                    out outputPath, out string outputPathError) ||
                !outputPath.EndsWith(
                    expectedExtension, StringComparison.OrdinalIgnoreCase))
                return VmAutomationResponse.Error(
                    (outputPathError ?? "outputPath has the wrong extension.") +
                    $" Expected '{expectedExtension}'.",
                    "invalid_arguments");
            bool overwrite;
            try
            {
                overwrite = GetBool(args, "overwrite", false);
            }
            catch (Exception exception)
            {
                return VmAutomationResponse.Error(VmAutomationVFXReflection.Unwrap(exception).Message,
                    "invalid_arguments");
            }
            string absolutePath = VmAutomationVFXAssetPath.ToAbsoluteAssetsPath(outputPath);
            string absoluteMetaPath = absolutePath + ".meta";
            bool existed = File.Exists(absolutePath) ||
                           AssetDatabase.LoadMainAssetAtPath(outputPath) != null;
            if (existed && !overwrite)
                return VmAutomationResponse.Error(
                    $"Output asset '{outputPath}' already exists. Set overwrite=true to replace its contents.",
                    "asset_already_exists");
            UnityEngine.Object existing = AssetDatabase.LoadMainAssetAtPath(outputPath);
            if (existing != null && !AssetDatabase.IsOpenForEdit(existing,
                    StatusQueryOptions.UseCachedIfPossible))
                return VmAutomationResponse.Error(
                    $"Output asset '{outputPath}' is not open for edit.",
                    "asset_not_editable");

            byte[] previousBytes = existed && File.Exists(absolutePath)
                ? File.ReadAllBytes(absolutePath) : null;
            byte[] previousMetaBytes = existed && File.Exists(absoluteMetaPath)
                ? File.ReadAllBytes(absoluteMetaPath) : null;
            string previousGuid = existed
                ? AssetDatabase.AssetPathToGUID(outputPath) : "";
            IReadOnlyList<string> createdFolders = Array.Empty<string>();
            try
            {
                createdFolders = VmAutomationVFXAssetPath.EnsureParentFolder(outputPath);
                Dictionary<string, object> result = kind == "sdf"
                    ? BakeSdf(args, outputPath, existing)
                    : kind == "point-cache-mesh"
                        ? BakeMeshPointCache(args, outputPath)
                        : BakeTexturePointCache(args, outputPath);
                string guid = AssetDatabase.AssetPathToGUID(outputPath);
                if (existed && !string.IsNullOrEmpty(previousGuid) &&
                    !string.Equals(previousGuid, guid, StringComparison.Ordinal))
                    throw new InvalidOperationException(
                        "Overwriting the VFX bake output changed its meta GUID.");
                result["success"] = true;
                result["kind"] = kind;
                result["outputPath"] = outputPath;
                result["guid"] = guid;
                result["overwritten"] = existed;
                return result;
            }
            catch (Exception exception)
            {
                try
                {
                    if (existed)
                    {
                        RestoreSnapshot(absolutePath, previousBytes,
                            "VFX bake output");
                        RestoreSnapshot(absoluteMetaPath, previousMetaBytes,
                            "VFX bake output meta");
                        AssetDatabase.ImportAsset(outputPath,
                            ImportAssetOptions.ForceUpdate |
                            ImportAssetOptions.ForceSynchronousImport);
                        VerifySnapshot(absolutePath, previousBytes,
                            "VFX bake output");
                        VerifySnapshot(absoluteMetaPath, previousMetaBytes,
                            "VFX bake output meta");
                    }
                    else if (!existed && (File.Exists(absolutePath) ||
                             AssetDatabase.LoadMainAssetAtPath(outputPath) != null))
                    {
                        AssetDatabase.DeleteAsset(outputPath);
                    }
                    VmAutomationVFXAssetPath.RollBackCreatedFolders(createdFolders);
                }
                catch (Exception rollbackException)
                {
                    return VmAutomationResponse.Error(
                        $"VFX bake failed: {VmAutomationVFXReflection.Unwrap(exception).Message}. Rollback failed: {VmAutomationVFXReflection.Unwrap(rollbackException).Message}",
                        "vfx_bake_rollback_failed");
                }
                return VmAutomationVFXError.Response(exception, "vfx_bake_failed");
            }
        }

        private static Dictionary<string, object> BakeSdf(
            Dictionary<string, object> args, string outputPath,
            UnityEngine.Object existing)
        {
            if (!SystemInfo.supportsComputeShaders ||
                SystemInfo.graphicsDeviceType == GraphicsDeviceType.OpenGLES3)
                throw VmAutomationVFXError.Create("graphics_api_unsupported",
                    "The active graphics device does not support compute shaders required for SDF baking.");
            Mesh mesh = LoadAsset<Mesh>(RequireString(args, "meshPath"),
                "meshPath");
            if (mesh.vertexCount == 0 || mesh.triangles.Length == 0)
                throw new ArgumentException("meshPath identifies an empty mesh.");
            Vector3 size = args.TryGetValue("boxSize", out object rawSize)
                ? (Vector3)VmAutomationVFXValueCodec.ConvertTo(rawSize, typeof(Vector3),
                    "boxSize") : mesh.bounds.size;
            Vector3 center = args.TryGetValue("boxCenter", out object rawCenter)
                ? (Vector3)VmAutomationVFXValueCodec.ConvertTo(rawCenter, typeof(Vector3),
                    "boxCenter") : mesh.bounds.center;
            if (size.x <= 0f || size.y <= 0f || size.z <= 0f)
                throw new ArgumentException(
                    "boxSize components must all be greater than zero.");
            int maxResolution = GetInt(args, "maxResolution", 64);
            int signPassCount = GetInt(args, "signPassCount", 1);
            float threshold = GetFloat(args, "threshold", 0.5f);
            float offset = GetFloat(args, "offset", 0f);
            if (maxResolution < 4 || maxResolution > 512)
                throw new ArgumentException(
                    "maxResolution must be between 4 and 512.");
            if (signPassCount < 1 || signPassCount > 20)
                throw new ArgumentException(
                    "signPassCount must be between 1 and 20.");
            if (threshold < 0f || threshold > 1f)
                throw new ArgumentException("threshold must be in [0, 1].");
            long estimated = EstimateVoxelCount(size, maxResolution);
            if (estimated > MaxSdfVoxelCount)
                throw VmAutomationVFXError.Create("bake_limit_exceeded",
                    $"Requested SDF grid is approximately {estimated} voxels; the route limit is {MaxSdfVoxelCount}. Reduce maxResolution or use a thinner box.");

            Type bakerType = VmAutomationVFXReflection.RequireType(
                VmAutomationVFXReflection.MeshToSdfBakerTypeName);
            object baker = Activator.CreateInstance(bakerType,
                new object[] { size, center, maxResolution, mesh, signPassCount,
                    threshold, offset, null });
            try
            {
                VmAutomationVFXReflection.Invoke(baker, "BakeSDF");
                Vector3Int dimensions = (Vector3Int)VmAutomationVFXReflection.Invoke(baker,
                    "GetGridSize");
                Vector3 actualBoxSize = (Vector3)VmAutomationVFXReflection.Invoke(baker,
                    "GetActualBoxSize");
                RenderTexture source = VmAutomationVFXReflection.Get(baker,
                    "SdfTexture") as RenderTexture ??
                    throw new InvalidOperationException(
                        "SDF baker returned no texture.");
                Texture3D output = CopySdfTexture(baker, source, dimensions);
                output.name = Path.GetFileNameWithoutExtension(outputPath);
                output.filterMode = FilterMode.Bilinear;
                output.wrapMode = TextureWrapMode.Clamp;
                if (existing != null)
                {
                    if (!(existing is Texture3D existingTexture))
                        throw new InvalidOperationException(
                            $"Existing output '{outputPath}' is not a Texture3D.");
                    EditorUtility.CopySerialized(output, existingTexture);
                    EditorUtility.SetDirty(existingTexture);
                    UnityEngine.Object.DestroyImmediate(output);
                }
                else
                {
                    AssetDatabase.CreateAsset(output, outputPath);
                }
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(outputPath,
                    ImportAssetOptions.ForceUpdate |
                    ImportAssetOptions.ForceSynchronousImport);
                Texture3D saved = AssetDatabase.LoadAssetAtPath<Texture3D>(outputPath) ??
                    throw new InvalidOperationException(
                        "Saved SDF Texture3D could not be read back.");
                return new Dictionary<string, object>
                {
                    { "sourceMeshPath", AssetDatabase.GetAssetPath(mesh) },
                    { "dimensions", VmAutomationVFXValueCodec.Sanitize(new Vector3Int(
                        saved.width, saved.height, saved.depth)) },
                    { "actualBoxSize", VmAutomationVFXValueCodec.Sanitize(actualBoxSize) },
                    { "boxCenter", VmAutomationVFXValueCodec.Sanitize(center) },
                    { "textureFormat", saved.format.ToString() },
                    { "voxelCount", (long)saved.width * saved.height * saved.depth },
                };
            }
            finally
            {
                if (baker is IDisposable disposable)
                    disposable.Dispose();
            }
        }

        private static Texture3D CopySdfTexture(object baker, RenderTexture source,
            Vector3Int dimensions)
        {
            ComputeShader shader = VmAutomationVFXReflection.Get(baker,
                "m_computeShader") as ComputeShader ??
                throw new MissingMemberException(baker.GetType().FullName,
                    "m_computeShader");
            int count = checked(dimensions.x * dimensions.y * dimensions.z);
            using (var buffer = new ComputeBuffer(count, 4 * sizeof(float)))
            {
                int kernel = shader.FindKernel("CopyToBuffer");
                shader.SetBuffer(kernel, "voxelsBuffer", buffer);
                shader.SetTexture(kernel, "voxels", source, 0);
                shader.Dispatch(kernel, Mathf.CeilToInt(dimensions.x / 8f),
                    Mathf.CeilToInt(dimensions.y / 8f),
                    Mathf.CeilToInt(dimensions.z / 8f));
                var output = new Texture3D(dimensions.x, dimensions.y,
                    dimensions.z, TextureFormat.RHalf, false);
                Color[] values = output.GetPixels(0);
                buffer.GetData(values);
                output.SetPixels(values, 0);
                output.Apply(false, false);
                return output;
            }
        }

        private static Dictionary<string, object> BakeMeshPointCache(
            Dictionary<string, object> args, string outputPath)
        {
            Mesh mesh = LoadAsset<Mesh>(RequireString(args, "meshPath"),
                "meshPath");
            int pointCount = GetInt(args, "pointCount", 4096);
            if (pointCount < 1 || pointCount > MaxPointCount)
                throw VmAutomationVFXError.Create("bake_limit_exceeded",
                    $"pointCount must be between 1 and {MaxPointCount}.");
            Type toolType = VmAutomationVFXReflection.RequireType(
                VmAutomationVFXReflection.PointCacheBakeToolTypeName);
            ScriptableObject tool = ScriptableObject.CreateInstance(toolType);
            try
            {
                Set(tool, "m_Mesh", mesh);
                SetEnum(tool, "m_Distribution", NormalizeDistribution(
                    GetString(args, "distribution", "random-uniform-area")));
                SetEnum(tool, "m_MeshBakeMode", NormalizeMeshBakeMode(
                    GetString(args, "meshBakeMode", "triangle")));
                Set(tool, "m_OutputPointCount", pointCount);
                Set(tool, "m_SeedMesh", GetInt(args, "seed", 0));
                Set(tool, "m_ExportNormals", GetBool(args, "exportNormals", true));
                Set(tool, "m_ExportColors", GetBool(args, "exportColors", false));
                Set(tool, "m_ExportUV", GetBool(args, "exportUV", false));
                object cache = VmAutomationVFXReflection.Invoke(tool,
                    "ComputePCacheFromMesh") ??
                    throw new InvalidOperationException(
                        "Point cache mesh sampling was cancelled or produced no data.");
                SavePointCache(cache, outputPath,
                    GetString(args, "format", "binary"));
                return PointCacheManifest(outputPath, mesh.vertexCount,
                    mesh.triangles.Length / 3);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(tool);
                EditorUtility.ClearProgressBar();
            }
        }

        private static Dictionary<string, object> BakeTexturePointCache(
            Dictionary<string, object> args, string outputPath)
        {
            string texturePath = RequireString(args, "texturePath");
            Texture2D texture = LoadAsset<Texture2D>(texturePath, "texturePath");
            long pixelCount = (long)texture.width * texture.height;
            if (pixelCount > MaxTexturePixelCount)
                throw VmAutomationVFXError.Create("bake_limit_exceeded",
                    $"Source texture contains {pixelCount} pixels; the route limit is {MaxTexturePixelCount}.");
            string thresholdMode = NormalizeThresholdMode(GetString(args,
                "thresholdMode", "alpha"));
            float threshold = GetFloat(args, "threshold", 0.33333f);
            if (threshold < 0f || threshold > 1f)
                throw new ArgumentException("threshold must be in [0, 1].");
            TextureImporter importer = AssetImporter.GetAtPath(texturePath) as
                TextureImporter;
            bool changedReadability = importer != null && !importer.isReadable;
            bool previousReadable = importer?.isReadable ?? texture.isReadable;
            Type toolType = VmAutomationVFXReflection.RequireType(
                VmAutomationVFXReflection.PointCacheBakeToolTypeName);
            ScriptableObject tool = null;
            try
            {
                if (changedReadability)
                {
                    importer.isReadable = true;
                    importer.SaveAndReimport();
                    texture = LoadAsset<Texture2D>(texturePath, "texturePath");
                }
                tool = ScriptableObject.CreateInstance(toolType);
                Set(tool, "m_Texture", texture);
                SetEnum(tool, "m_DecimationThresholdMode", thresholdMode);
                Set(tool, "m_Threshold", threshold);
                Set(tool, "m_RandomizePixels", GetBool(args, "randomize", false));
                Set(tool, "m_SeedPixels", GetInt(args, "seed", 0));
                bool exportColors = GetBool(args, "exportColors", false);
                Set(tool, "m_ExportColors", exportColors);
                var positions = new List<Vector3>();
                var colors = exportColors ? new List<Vector4>() : null;
                VmAutomationVFXReflection.Invoke(tool, "ComputeTextureData", positions,
                    colors);
                object cache = CreatePointCache(positions, colors);
                SavePointCache(cache, outputPath,
                    GetString(args, "format", "binary"));
                Dictionary<string, object> manifest = PointCacheManifest(outputPath,
                    texture.width * texture.height, 0);
                manifest["sourceTexturePath"] = texturePath;
                manifest["sourceDimensions"] = VmAutomationVFXValueCodec.Sanitize(
                    new Vector2Int(texture.width, texture.height));
                manifest["thresholdMode"] = thresholdMode;
                manifest["threshold"] = threshold;
                return manifest;
            }
            finally
            {
                if (tool != null)
                    UnityEngine.Object.DestroyImmediate(tool);
                if (importer != null && changedReadability)
                {
                    importer.isReadable = previousReadable;
                    importer.SaveAndReimport();
                }
            }
        }

        private static object CreatePointCache(List<Vector3> positions,
            List<Vector4> colors)
        {
            Type cacheType = VmAutomationVFXReflection.RequireType(
                VmAutomationVFXReflection.PointCacheTypeName);
            object cache = Activator.CreateInstance(cacheType);
            VmAutomationVFXReflection.Invoke(cache, "AddVector3Property", "position");
            if (colors != null)
                VmAutomationVFXReflection.Invoke(cache, "AddColorProperty", "color");
            VmAutomationVFXReflection.Invoke(cache, "SetVector3Data", "position", positions);
            if (colors != null)
                VmAutomationVFXReflection.Invoke(cache, "SetColorData", "color", colors);
            return cache;
        }

        private static void SavePointCache(object cache, string outputPath,
            string formatName)
        {
            Type formatType = cache.GetType().GetNestedType("Format") ??
                throw new MissingMemberException(cache.GetType().FullName, "Format");
            object format = VmAutomationVFXValueCodec.ConvertTo(formatName, formatType,
                "format");
            VmAutomationVFXReflection.Invoke(cache, "SaveToFile", outputPath, format);
            AssetDatabase.ImportAsset(outputPath,
                ImportAssetOptions.ForceUpdate |
                ImportAssetOptions.ForceSynchronousImport);
        }

        private static Dictionary<string, object> PointCacheManifest(
            string outputPath, int sourceVertexOrPixelCount, int sourceTriangleCount)
        {
            UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(outputPath) ??
                throw new InvalidOperationException(
                    "Imported point cache asset could not be read back.");
            List<UnityEngine.Object> surfaces = VmAutomationVFXReflection.Enumerate(
                    VmAutomationVFXReflection.Get(asset, "surfaces"))
                .OfType<UnityEngine.Object>().ToList();
            return new Dictionary<string, object>
            {
                { "assetType", asset.GetType().FullName },
                { "pointCount", VmAutomationVFXReflection.Get(asset, "PointCount") ?? 0 },
                { "properties", surfaces.Select(surface =>
                    new Dictionary<string, object>
                    {
                        { "name", surface.name },
                        { "type", surface.GetType().FullName },
                        { "width", (surface as Texture)?.width ?? 0 },
                        { "height", (surface as Texture)?.height ?? 0 },
                    }).ToList() },
                { "sourceVertexOrPixelCount", sourceVertexOrPixelCount },
                { "sourceTriangleCount", sourceTriangleCount },
            };
        }

        private static T LoadAsset<T>(string path, string argumentName)
            where T : UnityEngine.Object
        {
            path = VmAutomationVFXAssetPath.RequireFile(path, false, argumentName);
            UnityEngine.Object mainAsset = AssetDatabase.LoadMainAssetAtPath(path);
            if (mainAsset == null)
                throw VmAutomationVFXError.Create("asset_not_found",
                    $"{argumentName} asset '{path}' was not found.");
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset == null)
                throw VmAutomationVFXError.Create("asset_type_mismatch",
                    $"{argumentName} asset '{path}' is {mainAsset.GetType().FullName}, not {typeof(T).FullName}.");
            return asset;
        }

        private static void RestoreSnapshot(string path, byte[] bytes,
            string label)
        {
            if (bytes == null)
                throw new InvalidOperationException(
                    $"Cannot restore {label}; its original bytes were unavailable.");
            File.WriteAllBytes(path, bytes);
        }

        private static void VerifySnapshot(string path, byte[] bytes,
            string label)
        {
            if (!File.Exists(path) || !File.ReadAllBytes(path).SequenceEqual(bytes))
                throw new InvalidOperationException(
                    $"Rollback did not restore the original {label} bytes.");
        }

        private static long EstimateVoxelCount(Vector3 size, int maxResolution)
        {
            float max = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
            long x = Math.Max(1, Mathf.CeilToInt(maxResolution * size.x / max));
            long y = Math.Max(1, Mathf.CeilToInt(maxResolution * size.y / max));
            long z = Math.Max(1, Mathf.CeilToInt(maxResolution * size.z / max));
            return x * y * z;
        }

        private static string NormalizeDistribution(string value)
        {
            switch (value.ToLowerInvariant())
            {
                case "sequential": return "Sequential";
                case "random": return "Random";
                case "random-uniform-area": return "RandomUniformArea";
                default: throw new ArgumentException(
                    "distribution must be sequential, random, or random-uniform-area.");
            }
        }

        private static string NormalizeMeshBakeMode(string value)
        {
            if (value.Equals("vertex", StringComparison.OrdinalIgnoreCase))
                return "Vertex";
            if (value.Equals("triangle", StringComparison.OrdinalIgnoreCase))
                return "Triangle";
            throw new ArgumentException(
                "meshBakeMode must be vertex or triangle.");
        }

        private static string NormalizeThresholdMode(string value)
        {
            switch (value.ToLowerInvariant())
            {
                case "none": return "None";
                case "alpha": return "Alpha";
                case "luminance": return "Luminance";
                case "r": return "R";
                case "g": return "G";
                case "b": return "B";
                default: throw new ArgumentException(
                    "thresholdMode must be none, alpha, luminance, r, g, or b.");
            }
        }

        private static void SetEnum(object target, string fieldName, string value)
        {
            Type type = VmAutomationVFXReflection.GetMemberType(target, fieldName) ??
                throw new MissingMemberException(target.GetType().FullName,
                    fieldName);
            Set(target, fieldName, VmAutomationVFXValueCodec.ConvertTo(value, type,
                fieldName));
        }

        private static void Set(object target, string fieldName, object value)
        {
            if (!VmAutomationVFXReflection.TrySet(target, fieldName, value))
                throw new MissingMemberException(target.GetType().FullName,
                    fieldName);
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

        private static int GetInt(Dictionary<string, object> args, string key,
            int defaultValue)
        {
            return args != null && args.TryGetValue(key, out object value) &&
                   value != null ? (int)VmAutomationVFXValueCodec.ConvertTo(value,
                       typeof(int), key) : defaultValue;
        }

        private static float GetFloat(Dictionary<string, object> args, string key,
            float defaultValue)
        {
            return args != null && args.TryGetValue(key, out object value) &&
                   value != null ? (float)VmAutomationVFXValueCodec.ConvertTo(value,
                       typeof(float), key) : defaultValue;
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
