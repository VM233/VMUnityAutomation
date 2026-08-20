using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace VMUnityAutomation.Editor
{
    /// <summary>
    /// Commands for inspecting and modifying texture import settings.
    /// </summary>
    public static class MCPTextureCommands
    {
        // ─── Get Texture Info ───

        public static object GetTextureInfo(Dictionary<string, object> args)
        {
            string path = args.ContainsKey("path") ? args["path"].ToString() : "";
            if (string.IsNullOrEmpty(path))
                return new { error = "path is required" };

            var texture = AssetDatabase.LoadAssetAtPath<Texture>(path);
            if (texture == null)
                return new { error = $"Texture not found at '{path}'" };

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                return new { error = $"No texture importer for '{path}'" };

            var result = new Dictionary<string, object>
            {
                { "path", path },
                { "name", texture.name },
                { "width", texture.width },
                { "height", texture.height },
                { "texelSize", new Dictionary<string, object>
                    {
                        { "x", texture.texelSize.x },
                        { "y", texture.texelSize.y },
                    }
                },
                { "textureType", importer.textureType.ToString() },
                { "spriteMode", importer.spriteImportMode.ToString() },
                { "sRGB", importer.sRGBTexture },
                { "alphaSource", importer.alphaSource.ToString() },
                { "alphaIsTransparency", importer.alphaIsTransparency },
                { "readable", importer.isReadable },
                { "mipmapEnabled", importer.mipmapEnabled },
                { "filterMode", importer.filterMode.ToString() },
                { "wrapMode", importer.wrapMode.ToString() },
                { "anisoLevel", importer.anisoLevel },
                { "maxTextureSize", importer.maxTextureSize },
                { "textureCompression", importer.textureCompression.ToString() },
                { "compressionQuality", importer.compressionQuality },
                { "npotScale", importer.npotScale.ToString() },
            };

            long runtimeMemoryBytes = UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(texture);
            result["runtimeMemoryBytes"] = runtimeMemoryBytes;
            result["runtimeMemoryKB"] = Math.Round(runtimeMemoryBytes / 1024.0, 1);

            if (texture is Texture2D texture2D)
            {
                result["format"] = texture2D.format.ToString();
                result["mipmapCount"] = texture2D.mipmapCount;
                result["isReadable"] = texture2D.isReadable;
            }

            if (importer.textureType == TextureImporterType.Sprite)
            {
                result["spritePixelsPerUnit"] = importer.spritePixelsPerUnit;
                result["spritePivot"] = new Dictionary<string, object>
                {
                    { "x", importer.spritePivot.x },
                    { "y", importer.spritePivot.y },
                };
                result["spriteBorder"] = new Dictionary<string, object>
                {
                    { "left", importer.spriteBorder.x },
                    { "bottom", importer.spriteBorder.y },
                    { "right", importer.spriteBorder.z },
                    { "top", importer.spriteBorder.w },
                };
            }

            if (importer.textureType == TextureImporterType.NormalMap)
            {
                result["convertToNormalmap"] = importer.convertToNormalmap;
            }

            return result;
        }

        // ─── Set Texture Import Settings ───

        public static object SetTextureImportSettings(Dictionary<string, object> args)
        {
            string path = args.ContainsKey("path") ? args["path"].ToString() : "";
            if (string.IsNullOrEmpty(path))
                return new { error = "path is required" };

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                return new { error = $"No texture importer for '{path}'" };

            var updated = new List<string>();

            if (args.ContainsKey("textureType"))
            {
                if (Enum.TryParse<TextureImporterType>(args["textureType"].ToString(), true, out var texType))
                {
                    importer.textureType = texType;
                    updated.Add("textureType");
                }
            }

            if (args.ContainsKey("sRGB"))
            {
                importer.sRGBTexture = Convert.ToBoolean(args["sRGB"]);
                updated.Add("sRGB");
            }

            if (args.ContainsKey("readable"))
            {
                importer.isReadable = Convert.ToBoolean(args["readable"]);
                updated.Add("readable");
            }

            if (args.ContainsKey("mipmapEnabled"))
            {
                importer.mipmapEnabled = Convert.ToBoolean(args["mipmapEnabled"]);
                updated.Add("mipmapEnabled");
            }

            if (args.ContainsKey("filterMode"))
            {
                if (Enum.TryParse<FilterMode>(args["filterMode"].ToString(), true, out var fm))
                {
                    importer.filterMode = fm;
                    updated.Add("filterMode");
                }
            }

            if (args.ContainsKey("wrapMode"))
            {
                if (Enum.TryParse<TextureWrapMode>(args["wrapMode"].ToString(), true, out var wm))
                {
                    importer.wrapMode = wm;
                    updated.Add("wrapMode");
                }
            }

            if (args.ContainsKey("maxTextureSize"))
            {
                importer.maxTextureSize = Convert.ToInt32(args["maxTextureSize"]);
                updated.Add("maxTextureSize");
            }

            if (args.ContainsKey("textureCompression"))
            {
                if (Enum.TryParse<TextureImporterCompression>(args["textureCompression"].ToString(), true, out var comp))
                {
                    importer.textureCompression = comp;
                    updated.Add("textureCompression");
                }
            }

            if (args.ContainsKey("anisoLevel"))
            {
                importer.anisoLevel = Convert.ToInt32(args["anisoLevel"]);
                updated.Add("anisoLevel");
            }

            if (args.ContainsKey("alphaIsTransparency"))
            {
                importer.alphaIsTransparency = Convert.ToBoolean(args["alphaIsTransparency"]);
                updated.Add("alphaIsTransparency");
            }

            if (args.ContainsKey("spritePixelsPerUnit"))
            {
                importer.spritePixelsPerUnit = Convert.ToSingle(args["spritePixelsPerUnit"]);
                updated.Add("spritePixelsPerUnit");
            }

            if (args.ContainsKey("spriteMode"))
            {
                if (Enum.TryParse<SpriteImportMode>(args["spriteMode"].ToString(), true, out var sm))
                {
                    importer.spriteImportMode = sm;
                    updated.Add("spriteMode");
                }
            }

            if (args.ContainsKey("npotScale"))
            {
                if (Enum.TryParse<TextureImporterNPOTScale>(args["npotScale"].ToString(), true, out var npot))
                {
                    importer.npotScale = npot;
                    updated.Add("npotScale");
                }
            }

            if (updated.Count == 0)
                return new { error = "No valid import settings provided" };

            importer.SaveAndReimport();

            return new Dictionary<string, object>
            {
                { "success", true },
                { "path", path },
                { "updated", updated },
            };
        }

        // ─── Reimport Texture ───

        public static object ReimportTexture(Dictionary<string, object> args)
        {
            string path = args.ContainsKey("path") ? args["path"].ToString() : "";
            if (string.IsNullOrEmpty(path))
                return new { error = "path is required" };

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            return new Dictionary<string, object>
            {
                { "success", true },
                { "path", path },
            };
        }

        public static object ApplySpriteImportPreset(Dictionary<string, object> args)
        {
            string path = GetString(args, "path");
            if (string.IsNullOrEmpty(path))
                return new { error = "path is required" };

            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null)
                return new { error = $"No texture importer for '{path}'" };

            var updated = new List<string>();
            string referencePath = GetString(args, "referencePath");
            if (string.IsNullOrEmpty(referencePath) == false)
            {
                var reference = AssetImporter.GetAtPath(referencePath) as TextureImporter;
                if (reference == null)
                    return new { error = $"No texture importer for referencePath '{referencePath}'" };

                CopyTextureImporterSettings(reference, importer);
                updated.Add("referencePath");
            }

            string preset = GetString(args, "preset");
            if (string.Equals(preset, "pixel-sprite", StringComparison.OrdinalIgnoreCase))
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.mipmapEnabled = false;
                importer.filterMode = FilterMode.Point;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.alphaIsTransparency = true;
                importer.npotScale = TextureImporterNPOTScale.None;
                if (!args.ContainsKey("readable"))
                    importer.isReadable = true;
                if (!args.ContainsKey("pixelsPerUnit"))
                    importer.spritePixelsPerUnit = 32;
                SetDefaultPlatformFormat(importer, TextureImporterFormat.RGBA32,
                    TextureImporterCompression.Uncompressed);
                updated.Add("preset");
            }

            ApplyImporterOverrides(importer, args, updated);
            importer.SaveAndReimport();

            var info = GetTextureInfo(new Dictionary<string, object> { { "path", path } });
            return new Dictionary<string, object>
            {
                { "success", true },
                { "path", path },
                { "updated", updated.Distinct().ToList() },
                { "info", info },
            };
        }

        public static object ImportImage(Dictionary<string, object> args)
        {
            string sourcePath = GetString(args, "sourcePath");
            string sourceUrl = GetString(args, "sourceUrl");

            if (string.IsNullOrEmpty(sourcePath) && string.IsNullOrEmpty(sourceUrl))
                return new { error = "sourcePath or sourceUrl is required" };

            string targetPath = GetString(args, "targetPath");
            if (string.IsNullOrEmpty(targetPath))
            {
                string targetFolder = GetString(args, "targetFolder");
                string assetName = GetString(args, "assetName");
                if (string.IsNullOrEmpty(targetFolder) || string.IsNullOrEmpty(assetName))
                    return new { error = "Pass targetPath, or targetFolder plus assetName" };

                if (Path.HasExtension(assetName) == false)
                    assetName += ".png";
                targetPath = NormalizeAssetPath(targetFolder.TrimEnd('/', '\\') + "/" + assetName);
            }
            else
            {
                targetPath = NormalizeAssetPath(targetPath);
            }

            if (targetPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) == false)
                return new { error = "targetPath must be inside Assets/" };

            byte[] bytes;
            try
            {
                bytes = string.IsNullOrEmpty(sourceUrl) == false
                    ? DownloadBytes(sourceUrl)
                    : File.ReadAllBytes(ResolveFilePath(sourcePath));
            }
            catch (Exception ex)
            {
                return new { error = $"Failed to read image source: {ex.Message}" };
            }

            string projectRoot = GetProjectRoot();
            string absoluteTargetPath = Path.GetFullPath(Path.Combine(projectRoot, targetPath));
            string targetDirectory = Path.GetDirectoryName(absoluteTargetPath);
            if (string.IsNullOrEmpty(targetDirectory) == false)
                Directory.CreateDirectory(targetDirectory);

            bool overwrite = GetBool(args, "overwrite", false);
            bool dedupeByHash = GetBool(args, "dedupeByHash", true);
            string hash = ComputeHash(bytes);

            if (dedupeByHash)
            {
                string duplicate = FindDuplicateAssetByHash(Path.GetDirectoryName(targetPath)?.Replace('\\', '/'), hash,
                    Path.GetExtension(targetPath));
                if (string.IsNullOrEmpty(duplicate) == false &&
                    !string.Equals(duplicate, targetPath, StringComparison.OrdinalIgnoreCase))
                {
                    return new Dictionary<string, object>
                    {
                        { "success", true },
                        { "skipped", true },
                        { "reason", "duplicate-hash" },
                        { "path", duplicate },
                        { "guid", AssetDatabase.AssetPathToGUID(duplicate) },
                        { "sha256", hash },
                    };
                }
            }

            bool existed = File.Exists(absoluteTargetPath);
            if (existed)
            {
                string existingHash = ComputeHash(File.ReadAllBytes(absoluteTargetPath));
                if (existingHash == hash)
                {
                    AssetDatabase.ImportAsset(targetPath, ImportAssetOptions.ForceUpdate);
                    if (GetBool(args, "applySpritePreset", true))
                        ApplySpriteImportPreset(BuildPresetArgs(args, targetPath));

                    return BuildImportResult(targetPath, hash, true, "same-content");
                }

                if (!overwrite)
                    return new { error = $"Target asset already exists with different content: {targetPath}" };
            }

            File.WriteAllBytes(absoluteTargetPath, bytes);
            AssetDatabase.ImportAsset(targetPath, ImportAssetOptions.ForceUpdate);

            if (GetBool(args, "applySpritePreset", true))
                ApplySpriteImportPreset(BuildPresetArgs(args, targetPath));

            return BuildImportResult(targetPath, hash, false, existed ? "overwritten" : "imported");
        }

        public static object CheckImportSettings(Dictionary<string, object> args)
        {
            var assetPaths = GetTextureAssetPaths(args);
            if (assetPaths.Count == 0)
                return new { error = "Provide assetPath, assetPaths, path, or folderPath" };

            string referencePath = NormalizeAssetPath(GetString(args, "referencePath"));
            var reference = string.IsNullOrEmpty(referencePath)
                ? null
                : AssetImporter.GetAtPath(referencePath) as TextureImporter;
            if (!string.IsNullOrEmpty(referencePath) && reference == null)
                return new { error = $"No texture importer for referencePath '{referencePath}'" };

            string preset = GetString(args, "preset");
            bool requirePixelSprite = GetBool(args, "requirePixelSprite", string.IsNullOrEmpty(referencePath));
            if (requirePixelSprite && string.IsNullOrEmpty(preset))
                preset = "pixel-sprite";

            bool includeMatching = GetBool(args, "includeMatching", false);
            float tolerance = GetFloat(args, "tolerance", 0.001f);
            var results = new List<Dictionary<string, object>>();

            foreach (string rawPath in assetPaths)
            {
                string path = NormalizeAssetPath(rawPath);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                {
                    results.Add(new Dictionary<string, object>
                    {
                        { "assetPath", path },
                        { "valid", false },
                        { "error", "Asset is not imported by TextureImporter" },
                    });
                    continue;
                }

                results.Add(BuildImportCheckResult(path, importer, reference, preset, includeMatching, tolerance));
            }

            return new Dictionary<string, object>
            {
                { "success", true },
                { "valid", results.All(result => GetBool(result, "valid", false)) },
                { "referencePath", referencePath },
                { "preset", preset },
                { "count", results.Count },
                { "results", results },
            };
        }

        public static object CheckUIImportSettings(Dictionary<string, object> args)
        {
            var checkArgs = new Dictionary<string, object>(args)
            {
                ["preset"] = string.IsNullOrEmpty(GetString(args, "preset"))
                    ? "pixel-sprite"
                    : GetString(args, "preset"),
                ["requirePixelSprite"] = true,
            };

            var checkResult = CheckImportSettings(checkArgs) as Dictionary<string, object>;
            if (checkResult == null || checkResult.ContainsKey("error"))
                return checkResult ?? new Dictionary<string, object> { { "error", "UI import settings check failed" } };

            bool includeMatching = GetBool(args, "includeMatching", false);
            if (checkResult.TryGetValue("results", out object rawResults) &&
                rawResults is List<Dictionary<string, object>> results)
            {
                foreach (var result in results)
                    AddUIImportComparisons(result, args, includeMatching);

                checkResult["valid"] = results.All(result => GetBool(result, "valid", false));
            }

            checkResult["uiPreset"] = "pixel-sprite";
            checkResult["checks"] = new List<string>
            {
                "TextureImporter pixel sprite settings",
                "Optional expectedWidth/expectedHeight",
                "Optional expectedBorder/border/spriteBorder",
                "Optional maxTextureSize",
            };
            return checkResult;
        }

        private static void ApplyImporterOverrides(TextureImporter importer, Dictionary<string, object> args,
            List<string> updated)
        {
            if (TryParseEnum(args, "textureType", out TextureImporterType textureType))
            {
                importer.textureType = textureType;
                updated.Add("textureType");
            }

            if (TryParseEnum(args, "spriteMode", out SpriteImportMode spriteMode))
            {
                importer.spriteImportMode = spriteMode;
                updated.Add("spriteMode");
            }

            if (args.ContainsKey("pixelsPerUnit"))
            {
                importer.spritePixelsPerUnit = GetFloat(args, "pixelsPerUnit", importer.spritePixelsPerUnit);
                updated.Add("spritePixelsPerUnit");
            }

            if (TryGetVector2(args, "pivot", out Vector2 pivot))
            {
                importer.spritePivot = pivot;
                updated.Add("spritePivot");
            }

            if (TryGetVector4(args, "border", out Vector4 border) || TryGetVector4(args, "spriteBorder", out border))
            {
                importer.spriteBorder = border;
                updated.Add("spriteBorder");
            }

            if (TryParseEnum(args, "filterMode", out FilterMode filterMode))
            {
                importer.filterMode = filterMode;
                updated.Add("filterMode");
            }

            if (TryParseEnum(args, "wrapMode", out TextureWrapMode wrapMode))
            {
                importer.wrapMode = wrapMode;
                updated.Add("wrapMode");
            }

            if (TryParseEnum(args, "textureCompression", out TextureImporterCompression compression))
            {
                importer.textureCompression = compression;
                updated.Add("textureCompression");
            }

            if (TryParseEnum(args, "defaultPlatformFormat", out TextureImporterFormat defaultFormat))
            {
                var platform = importer.GetDefaultPlatformTextureSettings();
                platform.format = defaultFormat;
                importer.SetPlatformTextureSettings(platform);
                updated.Add("defaultPlatformFormat");
            }

            if (TryParseEnum(args, "defaultPlatformCompression", out TextureImporterCompression defaultCompression))
            {
                var platform = importer.GetDefaultPlatformTextureSettings();
                platform.textureCompression = defaultCompression;
                importer.SetPlatformTextureSettings(platform);
                updated.Add("defaultPlatformCompression");
            }

            if (args.ContainsKey("readable"))
            {
                importer.isReadable = GetBool(args, "readable", importer.isReadable);
                updated.Add("readable");
            }

            if (args.ContainsKey("mipmapEnabled"))
            {
                importer.mipmapEnabled = GetBool(args, "mipmapEnabled", importer.mipmapEnabled);
                updated.Add("mipmapEnabled");
            }

            if (args.ContainsKey("alphaIsTransparency"))
            {
                importer.alphaIsTransparency = GetBool(args, "alphaIsTransparency", importer.alphaIsTransparency);
                updated.Add("alphaIsTransparency");
            }

            if (args.ContainsKey("maxTextureSize"))
            {
                importer.maxTextureSize = GetInt(args, "maxTextureSize", importer.maxTextureSize);
                updated.Add("maxTextureSize");
            }
        }

        private static void CopyTextureImporterSettings(TextureImporter source, TextureImporter target)
        {
            target.textureType = source.textureType;
            target.spriteImportMode = source.spriteImportMode;
            target.sRGBTexture = source.sRGBTexture;
            target.alphaSource = source.alphaSource;
            target.alphaIsTransparency = source.alphaIsTransparency;
            target.isReadable = source.isReadable;
            target.mipmapEnabled = source.mipmapEnabled;
            target.filterMode = source.filterMode;
            target.wrapMode = source.wrapMode;
            target.anisoLevel = source.anisoLevel;
            target.maxTextureSize = source.maxTextureSize;
            target.textureCompression = source.textureCompression;
            target.compressionQuality = source.compressionQuality;
            target.npotScale = source.npotScale;
            target.spritePixelsPerUnit = source.spritePixelsPerUnit;
            target.spritePivot = source.spritePivot;
            target.spriteBorder = source.spriteBorder;
            target.SetPlatformTextureSettings(source.GetDefaultPlatformTextureSettings());
            target.SetPlatformTextureSettings(source.GetPlatformTextureSettings("Standalone"));
        }

        private static void SetDefaultPlatformFormat(TextureImporter importer, TextureImporterFormat format,
            TextureImporterCompression compression)
        {
            var platform = importer.GetDefaultPlatformTextureSettings();
            platform.format = format;
            platform.textureCompression = compression;
            importer.SetPlatformTextureSettings(platform);
        }

        private static Dictionary<string, object> BuildPresetArgs(Dictionary<string, object> args, string targetPath)
        {
            var copy = new Dictionary<string, object>(args);
            copy["path"] = targetPath;
            if (!copy.ContainsKey("preset") && !copy.ContainsKey("referencePath"))
                copy["preset"] = "pixel-sprite";
            return copy;
        }

        private static Dictionary<string, object> BuildImportResult(string path, string hash, bool skipped, string reason)
        {
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            return new Dictionary<string, object>
            {
                { "success", true },
                { "skipped", skipped },
                { "reason", reason },
                { "path", path },
                { "guid", AssetDatabase.AssetPathToGUID(path) },
                { "sha256", hash },
                { "width", texture == null ? 0 : texture.width },
                { "height", texture == null ? 0 : texture.height },
            };
        }

        private static void AddUIImportComparisons(Dictionary<string, object> result,
            Dictionary<string, object> args, bool includeMatching)
        {
            var comparisons = result.TryGetValue("comparisons", out object rawComparisons) &&
                              rawComparisons is List<Dictionary<string, object>> list
                ? list
                : new List<Dictionary<string, object>>();
            result["comparisons"] = comparisons;

            string path = result.TryGetValue("assetPath", out object assetPathObject)
                ? assetPathObject?.ToString() ?? ""
                : "";
            var importer = string.IsNullOrEmpty(path) ? null : AssetImporter.GetAtPath(path) as TextureImporter;

            if (args.ContainsKey("expectedWidth") && result.ContainsKey("textureWidth"))
                AddConditionalIntComparison(comparisons, "textureWidth", GetInt(args, "expectedWidth", 0),
                    Convert.ToInt32(result["textureWidth"]), includeMatching);
            if (args.ContainsKey("expectedHeight") && result.ContainsKey("textureHeight"))
                AddConditionalIntComparison(comparisons, "textureHeight", GetInt(args, "expectedHeight", 0),
                    Convert.ToInt32(result["textureHeight"]), includeMatching);
            if (args.ContainsKey("maxTextureSize") && importer != null)
                AddConditionalIntComparison(comparisons, "maxTextureSize", GetInt(args, "maxTextureSize", 0),
                    importer.maxTextureSize, includeMatching);

            if (importer != null &&
                (TryGetVector4(args, "expectedBorder", out Vector4 expectedBorder) ||
                 TryGetVector4(args, "spriteBorder", out expectedBorder) ||
                 TryGetVector4(args, "border", out expectedBorder)))
            {
                AddConditionalVector4Comparison(comparisons, "spriteBorder", expectedBorder,
                    importer.spriteBorder, GetFloat(args, "tolerance", 0.001f), includeMatching);
            }

            var mismatches = comparisons.Where(comparison => GetBool(comparison, "matches", false) == false).ToList();
            result["comparisonCount"] = comparisons.Count;
            result["mismatchCount"] = mismatches.Count;
            result["valid"] = mismatches.Count == 0;
        }

        private static void AddConditionalIntComparison(List<Dictionary<string, object>> comparisons,
            string name, int expected, int actual, bool includeMatching)
        {
            bool matches = expected == actual;
            if (!includeMatching && matches)
                return;

            comparisons.Add(new Dictionary<string, object>
            {
                { "name", name },
                { "matches", matches },
                { "expected", expected },
                { "actual", actual },
            });
        }

        private static void AddConditionalVector4Comparison(List<Dictionary<string, object>> comparisons,
            string name, Vector4 expected, Vector4 actual, float tolerance, bool includeMatching)
        {
            bool matches = Math.Abs(expected.x - actual.x) <= tolerance &&
                           Math.Abs(expected.y - actual.y) <= tolerance &&
                           Math.Abs(expected.z - actual.z) <= tolerance &&
                           Math.Abs(expected.w - actual.w) <= tolerance;
            if (!includeMatching && matches)
                return;

            comparisons.Add(new Dictionary<string, object>
            {
                { "name", name },
                { "matches", matches },
                { "expected", Vector4ToDictionary(expected) },
                { "actual", Vector4ToDictionary(actual) },
                { "tolerance", tolerance },
            });
        }

        private static Dictionary<string, object> BuildImportCheckResult(string path, TextureImporter importer,
            TextureImporter reference, string preset, bool includeMatching, float tolerance)
        {
            var comparisons = new List<Dictionary<string, object>>();
            if (reference != null)
                AddReferenceComparisons(comparisons, importer, reference, tolerance);

            if (string.Equals(preset, "pixel-sprite", StringComparison.OrdinalIgnoreCase))
            {
                AddPixelSpriteComparisons(comparisons, importer, tolerance);
            }

            if (includeMatching == false)
                comparisons.RemoveAll(comparison => GetBool(comparison, "matches", false));

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            var mismatches = comparisons.Where(comparison => GetBool(comparison, "matches", false) == false).ToList();
            var defaultPlatform = importer.GetDefaultPlatformTextureSettings();

            return new Dictionary<string, object>
            {
                { "assetPath", path },
                { "valid", mismatches.Count == 0 },
                { "mismatchCount", mismatches.Count },
                { "comparisonCount", comparisons.Count },
                { "textureWidth", texture == null ? 0 : texture.width },
                { "textureHeight", texture == null ? 0 : texture.height },
                { "textureType", importer.textureType.ToString() },
                { "spriteImportMode", importer.spriteImportMode.ToString() },
                { "filterMode", importer.filterMode.ToString() },
                { "mipmapEnabled", importer.mipmapEnabled },
                { "textureCompression", importer.textureCompression.ToString() },
                { "alphaIsTransparency", importer.alphaIsTransparency },
                { "spritePixelsPerUnit", importer.spritePixelsPerUnit },
                { "defaultPlatformFormat", defaultPlatform.format.ToString() },
                { "defaultPlatformCompression", defaultPlatform.textureCompression.ToString() },
                { "comparisons", comparisons },
            };
        }

        private static void AddReferenceComparisons(List<Dictionary<string, object>> comparisons,
            TextureImporter importer, TextureImporter reference, float tolerance)
        {
            AddComparison(comparisons, "textureType", reference.textureType, importer.textureType);
            AddComparison(comparisons, "spriteImportMode", reference.spriteImportMode, importer.spriteImportMode);
            AddComparison(comparisons, "filterMode", reference.filterMode, importer.filterMode);
            AddComparison(comparisons, "wrapMode", reference.wrapMode, importer.wrapMode);
            AddComparison(comparisons, "mipmapEnabled", reference.mipmapEnabled, importer.mipmapEnabled);
            AddComparison(comparisons, "textureCompression", reference.textureCompression, importer.textureCompression);
            AddComparison(comparisons, "alphaIsTransparency", reference.alphaIsTransparency, importer.alphaIsTransparency);
            AddComparison(comparisons, "isReadable", reference.isReadable, importer.isReadable);
            AddComparison(comparisons, "npotScale", reference.npotScale, importer.npotScale);
            AddFloatComparison(comparisons, "spritePixelsPerUnit", reference.spritePixelsPerUnit,
                importer.spritePixelsPerUnit, tolerance);
            AddVector2Comparison(comparisons, "spritePivot", reference.spritePivot, importer.spritePivot, tolerance);
            AddVector4Comparison(comparisons, "spriteBorder", reference.spriteBorder, importer.spriteBorder, tolerance);

            var expectedPlatform = reference.GetDefaultPlatformTextureSettings();
            var actualPlatform = importer.GetDefaultPlatformTextureSettings();
            AddComparison(comparisons, "defaultPlatform.format", expectedPlatform.format, actualPlatform.format);
            AddComparison(comparisons, "defaultPlatform.textureCompression", expectedPlatform.textureCompression,
                actualPlatform.textureCompression);
            AddComparison(comparisons, "defaultPlatform.maxTextureSize", expectedPlatform.maxTextureSize,
                actualPlatform.maxTextureSize);
        }

        private static void AddPixelSpriteComparisons(List<Dictionary<string, object>> comparisons,
            TextureImporter importer, float tolerance)
        {
            AddComparison(comparisons, "textureType", TextureImporterType.Sprite, importer.textureType);
            AddComparison(comparisons, "filterMode", FilterMode.Point, importer.filterMode);
            AddComparison(comparisons, "mipmapEnabled", false, importer.mipmapEnabled);
            AddComparison(comparisons, "textureCompression", TextureImporterCompression.Uncompressed,
                importer.textureCompression);
            AddComparison(comparisons, "alphaIsTransparency", true, importer.alphaIsTransparency);
            AddComparison(comparisons, "npotScale", TextureImporterNPOTScale.None, importer.npotScale);
            AddFloatComparison(comparisons, "spritePixelsPerUnit", 32, importer.spritePixelsPerUnit, tolerance);

            var platform = importer.GetDefaultPlatformTextureSettings();
            AddComparison(comparisons, "defaultPlatform.format", TextureImporterFormat.RGBA32, platform.format);
            AddComparison(comparisons, "defaultPlatform.textureCompression",
                TextureImporterCompression.Uncompressed, platform.textureCompression);
        }

        private static void AddComparison(List<Dictionary<string, object>> comparisons, string name,
            object expected, object actual)
        {
            bool matches = string.Equals(expected?.ToString() ?? "", actual?.ToString() ?? "",
                StringComparison.OrdinalIgnoreCase);
            comparisons.Add(new Dictionary<string, object>
            {
                { "name", name },
                { "matches", matches },
                { "expected", expected?.ToString() ?? "" },
                { "actual", actual?.ToString() ?? "" },
            });
        }

        private static void AddFloatComparison(List<Dictionary<string, object>> comparisons, string name,
            float expected, float actual, float tolerance)
        {
            comparisons.Add(new Dictionary<string, object>
            {
                { "name", name },
                { "matches", Math.Abs(expected - actual) <= tolerance },
                { "expected", expected },
                { "actual", actual },
                { "delta", actual - expected },
                { "tolerance", tolerance },
            });
        }

        private static void AddVector2Comparison(List<Dictionary<string, object>> comparisons, string name,
            Vector2 expected, Vector2 actual, float tolerance)
        {
            bool matches = Math.Abs(expected.x - actual.x) <= tolerance &&
                           Math.Abs(expected.y - actual.y) <= tolerance;
            comparisons.Add(new Dictionary<string, object>
            {
                { "name", name },
                { "matches", matches },
                { "expected", Vector2ToDictionary(expected) },
                { "actual", Vector2ToDictionary(actual) },
                { "tolerance", tolerance },
            });
        }

        private static void AddVector4Comparison(List<Dictionary<string, object>> comparisons, string name,
            Vector4 expected, Vector4 actual, float tolerance)
        {
            bool matches = Math.Abs(expected.x - actual.x) <= tolerance &&
                           Math.Abs(expected.y - actual.y) <= tolerance &&
                           Math.Abs(expected.z - actual.z) <= tolerance &&
                           Math.Abs(expected.w - actual.w) <= tolerance;
            comparisons.Add(new Dictionary<string, object>
            {
                { "name", name },
                { "matches", matches },
                { "expected", Vector4ToDictionary(expected) },
                { "actual", Vector4ToDictionary(actual) },
                { "tolerance", tolerance },
            });
        }

        private static Dictionary<string, object> Vector2ToDictionary(Vector2 value)
        {
            return new Dictionary<string, object>
            {
                { "x", value.x },
                { "y", value.y },
            };
        }

        private static Dictionary<string, object> Vector4ToDictionary(Vector4 value)
        {
            return new Dictionary<string, object>
            {
                { "x", value.x },
                { "y", value.y },
                { "z", value.z },
                { "w", value.w },
            };
        }

        private static List<string> GetTextureAssetPaths(Dictionary<string, object> args)
        {
            var paths = GetStringList(args, "assetPaths");
            string assetPath = GetString(args, "assetPath");
            if (string.IsNullOrEmpty(assetPath) == false)
                paths.Add(assetPath);

            string folderPath = NormalizeAssetPath(GetString(args, "folderPath"));
            if (string.IsNullOrEmpty(folderPath) == false)
            {
                string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folderPath });
                foreach (string guid in guids)
                    paths.Add(AssetDatabase.GUIDToAssetPath(guid));
            }

            return paths
                .Select(NormalizeAssetPath)
                .Where(path => string.IsNullOrEmpty(path) == false)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static byte[] DownloadBytes(string url)
        {
            using (var client = new WebClient())
                return client.DownloadData(url);
        }

        private static string FindDuplicateAssetByHash(string folderPath, string hash, string extension)
        {
            if (string.IsNullOrEmpty(folderPath) || AssetDatabase.IsValidFolder(folderPath) == false)
                return "";

            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folderPath });
            foreach (string guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                if (string.IsNullOrEmpty(extension) == false &&
                    !assetPath.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
                    continue;

                string absolute = ResolveFilePath(assetPath);
                if (File.Exists(absolute) && ComputeHash(File.ReadAllBytes(absolute)) == hash)
                    return assetPath;
            }

            return "";
        }

        private static string ComputeHash(byte[] bytes)
        {
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }

        private static string GetString(Dictionary<string, object> args, string key)
        {
            return args != null && args.ContainsKey(key) && args[key] != null ? args[key].ToString() : "";
        }

        private static List<string> GetStringList(Dictionary<string, object> args, string key)
        {
            var result = new List<string>();
            if (args == null || args.ContainsKey(key) == false || args[key] == null)
                return result;

            if (args[key] is List<object> list)
            {
                foreach (object item in list)
                {
                    if (item != null && string.IsNullOrEmpty(item.ToString()) == false)
                        result.Add(item.ToString());
                }

                return result;
            }

            string value = args[key].ToString();
            if (string.IsNullOrEmpty(value) == false)
                result.Add(value);
            return result;
        }

        private static int GetInt(Dictionary<string, object> args, string key, int defaultValue)
        {
            if (args == null || !args.ContainsKey(key) || args[key] == null)
                return defaultValue;

            return int.TryParse(args[key].ToString(), out int parsed) ? parsed : defaultValue;
        }

        private static float GetFloat(Dictionary<string, object> args, string key, float defaultValue)
        {
            if (args == null || !args.ContainsKey(key) || args[key] == null)
                return defaultValue;

            return float.TryParse(args[key].ToString(), out float parsed) ? parsed : defaultValue;
        }

        private static bool GetBool(Dictionary<string, object> args, string key, bool defaultValue)
        {
            if (args == null || !args.ContainsKey(key) || args[key] == null)
                return defaultValue;

            if (args[key] is bool value)
                return value;

            return bool.TryParse(args[key].ToString(), out bool parsed) ? parsed : defaultValue;
        }

        private static bool TryParseEnum<T>(Dictionary<string, object> args, string key, out T value) where T : struct
        {
            value = default;
            return args != null && args.ContainsKey(key) && args[key] != null &&
                   Enum.TryParse(args[key].ToString(), true, out value);
        }

        private static bool TryGetVector2(Dictionary<string, object> args, string key, out Vector2 value)
        {
            value = default;
            if (args == null || !args.ContainsKey(key) || args[key] == null)
                return false;

            if (args[key] is Dictionary<string, object> dict)
            {
                value = new Vector2(GetFloat(dict, "x", GetFloat(dict, "left", 0.5f)),
                    GetFloat(dict, "y", GetFloat(dict, "bottom", 0.5f)));
                return true;
            }

            if (args[key] is List<object> list && list.Count >= 2)
            {
                value = new Vector2(Convert.ToSingle(list[0]), Convert.ToSingle(list[1]));
                return true;
            }

            return false;
        }

        private static bool TryGetVector4(Dictionary<string, object> args, string key, out Vector4 value)
        {
            value = default;
            if (args == null || !args.ContainsKey(key) || args[key] == null)
                return false;

            if (args[key] is Dictionary<string, object> dict)
            {
                value = new Vector4(
                    GetFloat(dict, "x", GetFloat(dict, "left", 0)),
                    GetFloat(dict, "y", GetFloat(dict, "bottom", 0)),
                    GetFloat(dict, "z", GetFloat(dict, "right", 0)),
                    GetFloat(dict, "w", GetFloat(dict, "top", 0)));
                return true;
            }

            if (args[key] is List<object> list && list.Count >= 4)
            {
                value = new Vector4(Convert.ToSingle(list[0]), Convert.ToSingle(list[1]),
                    Convert.ToSingle(list[2]), Convert.ToSingle(list[3]));
                return true;
            }

            if (float.TryParse(args[key].ToString(), out float uniform))
            {
                value = new Vector4(uniform, uniform, uniform, uniform);
                return true;
            }

            return false;
        }

        private static string NormalizeAssetPath(string path)
        {
            return path.Replace('\\', '/');
        }

        private static string ResolveFilePath(string path)
        {
            if (Path.IsPathRooted(path))
                return Path.GetFullPath(path);

            return Path.GetFullPath(Path.Combine(GetProjectRoot(), NormalizeAssetPath(path)));
        }

        private static string GetProjectRoot()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        }
    }
}
