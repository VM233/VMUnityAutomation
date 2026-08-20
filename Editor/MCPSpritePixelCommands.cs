using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace VMUnityAutomation.Editor
{
    public static class MCPSpritePixelCommands
    {
        public static object Check(Dictionary<string, object> args)
        {
            var assetPaths = GetAssetPaths(args);
            if (assetPaths.Count == 0)
                return new { error = "Provide assetPath, assetPaths, or folderPath" };

            int dimensionsMultipleOf = GetInt(args, "dimensionsMultipleOf", 0);
            float expectedScale = GetFloat(args, "expectedScale", 0);
            float tolerance = GetFloat(args, "tolerance", 0.01f);
            bool requirePointFilter = GetBool(args, "requirePointFilter", true);
            bool requireNoCompression = GetBool(args, "requireNoCompression", true);
            bool requireNoMipMaps = GetBool(args, "requireNoMipMaps", true);

            var results = new List<Dictionary<string, object>>();
            foreach (string assetPath in assetPaths)
            {
                var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (importer == null)
                {
                    results.Add(new Dictionary<string, object>
                    {
                        { "assetPath", assetPath },
                        { "valid", false },
                        { "error", "Asset is not imported by TextureImporter" },
                    });
                    continue;
                }

                var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
                var sprites = AssetDatabase.LoadAllAssetsAtPath(assetPath)
                    .OfType<Sprite>()
                    .ToList();

                results.Add(BuildTextureResult(assetPath, importer, texture, sprites, dimensionsMultipleOf,
                    expectedScale, tolerance, requirePointFilter, requireNoCompression, requireNoMipMaps));
            }

            return new Dictionary<string, object>
            {
                { "success", true },
                { "valid", results.All(result => GetBool(result, "valid", false)) },
                { "count", results.Count },
                { "results", results },
            };
        }

        private static Dictionary<string, object> BuildTextureResult(string assetPath, TextureImporter importer,
            Texture2D texture, List<Sprite> sprites, int dimensionsMultipleOf, float expectedScale, float tolerance,
            bool requirePointFilter, bool requireNoCompression, bool requireNoMipMaps)
        {
            var warnings = new List<string>();
            var platformSettings = importer.GetDefaultPlatformTextureSettings();

            if (requirePointFilter && importer.filterMode != FilterMode.Point)
                warnings.Add($"FilterMode is {importer.filterMode}, expected Point.");
            if (requireNoMipMaps && importer.mipmapEnabled)
                warnings.Add("Mip maps are enabled.");
            if (requireNoCompression &&
                platformSettings.format != TextureImporterFormat.RGBA32 &&
                platformSettings.format != TextureImporterFormat.RGB24 &&
                platformSettings.format != TextureImporterFormat.Alpha8)
            {
                warnings.Add($"Default platform format is {platformSettings.format}, expected an uncompressed format.");
            }

            if (dimensionsMultipleOf > 0 && texture != null)
            {
                if (texture.width % dimensionsMultipleOf != 0)
                    warnings.Add($"Texture width {texture.width} is not divisible by {dimensionsMultipleOf}.");
                if (texture.height % dimensionsMultipleOf != 0)
                    warnings.Add($"Texture height {texture.height} is not divisible by {dimensionsMultipleOf}.");
            }

            var spriteInfos = new List<Dictionary<string, object>>();
            foreach (var sprite in sprites)
            {
                var spriteWarnings = new List<string>();
                if (expectedScale > 0)
                    AddScaleWarnings(spriteWarnings, sprite.rect.width, sprite.rect.height, expectedScale, tolerance);

                spriteInfos.Add(new Dictionary<string, object>
                {
                    { "name", sprite.name },
                    { "rect", RectToDictionary(sprite.rect) },
                    { "pivotPixels", Vector2ToDictionary(sprite.pivot) },
                    { "pivotNormalized", new Dictionary<string, object>
                        {
                            { "x", sprite.rect.width <= 0 ? 0 : sprite.pivot.x / sprite.rect.width },
                            { "y", sprite.rect.height <= 0 ? 0 : sprite.pivot.y / sprite.rect.height },
                        }
                    },
                    { "border", Vector4ToDictionary(sprite.border) },
                    { "pixelsPerUnit", sprite.pixelsPerUnit },
                    { "warnings", spriteWarnings },
                    { "valid", spriteWarnings.Count == 0 },
                });
            }

            return new Dictionary<string, object>
            {
                { "assetPath", assetPath },
                { "valid", warnings.Count == 0 && spriteInfos.All(info => GetBool(info, "valid", false)) },
                { "textureName", texture == null ? "" : texture.name },
                { "textureWidth", texture == null ? 0 : texture.width },
                { "textureHeight", texture == null ? 0 : texture.height },
                { "textureType", importer.textureType.ToString() },
                { "spriteImportMode", importer.spriteImportMode.ToString() },
                { "filterMode", importer.filterMode.ToString() },
                { "mipmapEnabled", importer.mipmapEnabled },
                { "alphaIsTransparency", importer.alphaIsTransparency },
                { "spritePixelsPerUnit", importer.spritePixelsPerUnit },
                { "defaultPlatformFormat", platformSettings.format.ToString() },
                { "defaultPlatformMaxTextureSize", platformSettings.maxTextureSize },
                { "warnings", warnings },
                { "spriteCount", spriteInfos.Count },
                { "sprites", spriteInfos },
            };
        }

        private static void AddScaleWarnings(List<string> warnings, float sourceWidth, float sourceHeight,
            float expectedScale, float tolerance)
        {
            float scaledWidth = sourceWidth * expectedScale;
            float scaledHeight = sourceHeight * expectedScale;
            if (Math.Abs(scaledWidth - Mathf.Round(scaledWidth)) > tolerance)
                warnings.Add($"Width {sourceWidth} * scale {expectedScale} is not pixel-aligned.");
            if (Math.Abs(scaledHeight - Mathf.Round(scaledHeight)) > tolerance)
                warnings.Add($"Height {sourceHeight} * scale {expectedScale} is not pixel-aligned.");
        }

        private static List<string> GetAssetPaths(Dictionary<string, object> args)
        {
            var paths = GetStringList(args, "assetPaths", "assetPath");
            string folderPath = GetString(args, "folderPath");
            if (string.IsNullOrEmpty(folderPath) == false)
            {
                string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { folderPath });
                foreach (string guid in guids)
                    paths.Add(AssetDatabase.GUIDToAssetPath(guid));
            }

            return paths.Where(path => string.IsNullOrEmpty(path) == false)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static Dictionary<string, object> RectToDictionary(Rect rect)
        {
            return new Dictionary<string, object>
            {
                { "x", rect.x },
                { "y", rect.y },
                { "width", rect.width },
                { "height", rect.height },
            };
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

        private static string GetString(Dictionary<string, object> args, string key)
        {
            return args != null && args.TryGetValue(key, out var value) && value != null ? value.ToString() : "";
        }

        private static bool GetBool(Dictionary<string, object> args, string key, bool defaultValue)
        {
            if (args == null || !args.TryGetValue(key, out var value) || value == null)
                return defaultValue;

            if (value is bool boolValue)
                return boolValue;

            return bool.TryParse(value.ToString(), out bool parsed) ? parsed : defaultValue;
        }

        private static int GetInt(Dictionary<string, object> args, string key, int defaultValue)
        {
            if (args == null || !args.TryGetValue(key, out var value) || value == null)
                return defaultValue;

            return int.TryParse(value.ToString(), out int parsed) ? parsed : defaultValue;
        }

        private static float GetFloat(Dictionary<string, object> args, string key, float defaultValue)
        {
            if (args == null || !args.TryGetValue(key, out var value) || value == null)
                return defaultValue;

            return float.TryParse(value.ToString(), out float parsed) ? parsed : defaultValue;
        }

        private static List<string> GetStringList(Dictionary<string, object> args, string arrayKey, string singleKey)
        {
            var results = new List<string>();
            if (args == null)
                return results;

            if (args.TryGetValue(arrayKey, out var arrayValue) && arrayValue is List<object> list)
            {
                foreach (var item in list)
                {
                    if (item != null)
                        results.Add(item.ToString());
                }
            }

            string singleValue = GetString(args, singleKey);
            if (string.IsNullOrEmpty(singleValue) == false)
                results.Add(singleValue);

            return results;
        }
    }
}
