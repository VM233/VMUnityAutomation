using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace VMUnityAutomation.Editor
{
    public static class VmAutomationImageDuplicateCommands
    {
        internal const string NoneMode = "none";
        internal const string FileBytesMode = "fileBytes";
        internal const string DecodedPixelsMode = "decodedPixels";

        private static readonly HashSet<string> DecodableExtensions = new HashSet<string>(
            new[] { ".png", ".jpg", ".jpeg" }, StringComparer.OrdinalIgnoreCase);
        private static readonly Dictionary<string, CachedFingerprint> FingerprintCache =
            new Dictionary<string, CachedFingerprint>(StringComparer.OrdinalIgnoreCase);

        public static object FindDuplicates(Dictionary<string, object> args)
        {
            args ??= new Dictionary<string, object>();
            string requestedMode = GetString(args, "mode");
            if (string.IsNullOrWhiteSpace(requestedMode))
                requestedMode = DecodedPixelsMode;
            if (!TryNormalizeMode(requestedMode, "", false, out string mode, out string modeError))
                return Error(modeError);
            if (mode == NoneMode)
                return Error("mode must be fileBytes or decodedPixels");

            var folders = GetStringList(args, "folders");
            string folder = NormalizeAssetPath(GetString(args, "folder"));
            if (!string.IsNullOrEmpty(folder))
                folders.Add(folder);
            if (folders.Count == 0)
                folders.Add("Assets");
            folders = folders.Select(NormalizeAssetPath).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            foreach (string searchFolder in folders)
            {
                if (!searchFolder.Equals("Assets", StringComparison.OrdinalIgnoreCase) &&
                    !searchFolder.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase))
                    return Error($"folder must be under Assets/: '{searchFolder}'");
                if (!AssetDatabase.IsValidFolder(searchFolder))
                    return Error($"folder does not exist: '{searchFolder}'");
            }

            int maxAssets = Mathf.Clamp(GetInt(args, "maxAssets", 10000), 1, 50000);
            int maxGroups = Mathf.Clamp(GetInt(args, "maxGroups", 100), 1, 2000);
            var extensions = NormalizeExtensions(GetStringList(args, "extensions"));
            if (extensions.Count == 0)
                extensions.UnionWith(DecodableExtensions);
            if (mode == DecodedPixelsMode)
            {
                string unsupported = extensions.FirstOrDefault(extension => !DecodableExtensions.Contains(extension));
                if (!string.IsNullOrEmpty(unsupported))
                    return Error($"decodedPixels supports only PNG and JPEG assets; unsupported extension '{unsupported}'");
            }

            var errors = new List<string>();
            var records = CollectAssetFingerprints(folders, mode, extensions, maxAssets, out bool truncatedAssets,
                errors);
            var duplicateGroups = records.GroupBy(record => record.Fingerprint.Hash, StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .OrderByDescending(group => group.Count())
                .ThenBy(group => group.Min(record => record.AssetPath), StringComparer.OrdinalIgnoreCase)
                .ToList();
            int duplicateAssetCount = duplicateGroups.Sum(group => group.Count());
            bool truncatedGroups = duplicateGroups.Count > maxGroups;
            var groups = duplicateGroups.Take(maxGroups).Select(group =>
            {
                var first = group.First();
                return new Dictionary<string, object>
                {
                    { "hash", group.Key },
                    { "width", first.Fingerprint.Width },
                    { "height", first.Fingerprint.Height },
                    { "assetCount", group.Count() },
                    { "assets", group.Select(record => new Dictionary<string, object>
                        {
                            { "path", record.AssetPath },
                            { "guid", record.Guid },
                            { "fileSize", record.FileSize },
                        }).ToList()
                    },
                };
            }).ToList();

            return new Dictionary<string, object>
            {
                { "success", true },
                { "mode", mode },
                { "folders", folders },
                { "extensions", extensions.OrderBy(extension => extension).ToList() },
                { "scannedCount", records.Count + errors.Count },
                { "fingerprintedCount", records.Count },
                { "duplicateGroupCount", duplicateGroups.Count },
                { "returnedGroupCount", groups.Count },
                { "duplicateAssetCount", duplicateAssetCount },
                { "truncatedAssets", truncatedAssets },
                { "truncatedGroups", truncatedGroups },
                { "errorCount", errors.Count },
                { "errors", errors.Take(50).ToList() },
                { "groups", groups },
            };
        }

        internal static bool TryNormalizeMode(string rawMode, string sourcePath, bool defaultToDecodedPixels,
            out string mode, out string error)
        {
            error = "";
            string compact = (rawMode ?? "").Trim().Replace("-", "").Replace("_", "").ToLowerInvariant();
            if (string.IsNullOrEmpty(compact))
            {
                mode = defaultToDecodedPixels && IsDecodableImagePath(sourcePath)
                    ? DecodedPixelsMode
                    : NoneMode;
                return true;
            }

            mode = compact switch
            {
                "none" => NoneMode,
                "filebytes" => FileBytesMode,
                "decodedpixels" => DecodedPixelsMode,
                _ => "",
            };
            if (string.IsNullOrEmpty(mode))
            {
                error = $"Unknown dedupeMode '{rawMode}'. Supported: none, fileBytes, decodedPixels";
                return false;
            }
            if (mode == DecodedPixelsMode && !string.IsNullOrEmpty(sourcePath) &&
                !IsDecodableImagePath(sourcePath))
            {
                error = "decodedPixels supports only PNG and JPEG sources";
                return false;
            }
            return true;
        }

        internal static bool IsDecodableImagePath(string path)
        {
            return DecodableExtensions.Contains(Path.GetExtension(path) ?? "");
        }

        internal static ImageFingerprint CreateFingerprint(string path, string mode)
        {
            var fileInfo = new FileInfo(path);
            string cacheKey = mode + "|" + fileInfo.FullName;
            if (FingerprintCache.TryGetValue(cacheKey, out var cached) &&
                cached.FileSize == fileInfo.Length && cached.LastWriteTicks == fileInfo.LastWriteTimeUtc.Ticks)
                return cached.Fingerprint;

            byte[] bytes = File.ReadAllBytes(path);
            if (mode == FileBytesMode)
            {
                var fileFingerprint = new ImageFingerprint(HashBytes(bytes), 0, 0);
                FingerprintCache[cacheKey] = new CachedFingerprint(fileInfo.Length,
                    fileInfo.LastWriteTimeUtc.Ticks, fileFingerprint);
                return fileFingerprint;
            }
            if (mode != DecodedPixelsMode)
                throw new ArgumentException($"Cannot fingerprint image with mode '{mode}'.");
            if (!IsDecodableImagePath(path))
                throw new ArgumentException("decodedPixels supports only PNG and JPEG images.");

            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            try
            {
                if (!ImageConversion.LoadImage(texture, bytes, false))
                    throw new InvalidDataException($"Unity could not decode image '{path}'.");
                Color32[] pixels = texture.GetPixels32();
                using var sha = SHA256.Create();
                byte[] header = Encoding.ASCII.GetBytes($"rgba32:{texture.width}x{texture.height}:");
                sha.TransformBlock(header, 0, header.Length, header, 0);
                var buffer = new byte[65536];
                int used = 0;
                foreach (Color32 pixel in pixels)
                {
                    if (used + 4 > buffer.Length)
                    {
                        sha.TransformBlock(buffer, 0, used, buffer, 0);
                        used = 0;
                    }
                    buffer[used++] = pixel.r;
                    buffer[used++] = pixel.g;
                    buffer[used++] = pixel.b;
                    buffer[used++] = pixel.a;
                }
                if (used > 0)
                    sha.TransformBlock(buffer, 0, used, buffer, 0);
                sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                var pixelFingerprint = new ImageFingerprint(ToHex(sha.Hash), texture.width, texture.height);
                FingerprintCache[cacheKey] = new CachedFingerprint(fileInfo.Length,
                    fileInfo.LastWriteTimeUtc.Ticks, pixelFingerprint);
                return pixelFingerprint;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }

        internal static Dictionary<string, List<ImageAssetRecord>> BuildAssetIndex(string folder, string mode,
            out List<string> errors)
        {
            errors = new List<string>();
            var extensions = mode == DecodedPixelsMode
                ? new HashSet<string>(DecodableExtensions, StringComparer.OrdinalIgnoreCase)
                : new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var records = CollectAssetFingerprints(new[] { folder }, mode, extensions, int.MaxValue,
                out _, errors);
            return records.GroupBy(record => record.Fingerprint.Hash, StringComparer.Ordinal)
                .ToDictionary(group => group.Key, group => group.ToList(), StringComparer.Ordinal);
        }

        private static List<ImageAssetRecord> CollectAssetFingerprints(IEnumerable<string> folders, string mode,
            HashSet<string> extensions, int maxAssets, out bool truncated, List<string> errors)
        {
            var paths = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string folder in folders)
            {
                if (!AssetDatabase.IsValidFolder(folder))
                    continue;
                foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (!string.IsNullOrEmpty(path))
                        paths.Add(path);
                }
            }

            truncated = paths.Count > maxAssets;
            var records = new List<ImageAssetRecord>();
            foreach (string assetPath in paths.Take(maxAssets))
            {
                string extension = Path.GetExtension(assetPath) ?? "";
                if (extensions.Count > 0 && !extensions.Contains(extension))
                    continue;
                if (mode == DecodedPixelsMode && !IsDecodableImagePath(assetPath))
                    continue;
                string absolutePath = GetAbsolutePath(assetPath);
                if (!File.Exists(absolutePath))
                    continue;
                try
                {
                    records.Add(new ImageAssetRecord(assetPath, AssetDatabase.AssetPathToGUID(assetPath),
                        new FileInfo(absolutePath).Length, CreateFingerprint(absolutePath, mode)));
                }
                catch (Exception exception)
                {
                    errors.Add($"{assetPath}: {exception.Message}");
                }
            }
            return records;
        }

        private static string HashBytes(byte[] bytes)
        {
            using var sha = SHA256.Create();
            return ToHex(sha.ComputeHash(bytes));
        }

        private static string ToHex(byte[] bytes)
        {
            return bytes == null ? "" : BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
        }

        private static HashSet<string> NormalizeExtensions(IEnumerable<string> values)
        {
            var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string value in values)
            {
                string extension = (value ?? "").Trim().ToLowerInvariant();
                if (string.IsNullOrEmpty(extension))
                    continue;
                result.Add(extension.StartsWith(".") ? extension : "." + extension);
            }
            return result;
        }

        private static List<string> GetStringList(Dictionary<string, object> args, string key)
        {
            var result = new List<string>();
            if (!args.TryGetValue(key, out object value) || value == null || value is string)
                return result;
            if (!(value is IEnumerable enumerable))
                return result;
            foreach (object item in enumerable)
            {
                if (item != null && !string.IsNullOrWhiteSpace(item.ToString()))
                    result.Add(item.ToString());
            }
            return result;
        }

        private static int GetInt(Dictionary<string, object> args, string key, int defaultValue)
        {
            return args.TryGetValue(key, out object value) && value != null
                ? Convert.ToInt32(value)
                : defaultValue;
        }

        private static string GetString(Dictionary<string, object> args, string key)
        {
            return args.TryGetValue(key, out object value) && value != null ? value.ToString() : "";
        }

        private static string NormalizeAssetPath(string path)
        {
            return string.IsNullOrWhiteSpace(path) ? "" : path.Replace('\\', '/').Trim().Trim('/');
        }

        private static string GetAbsolutePath(string assetPath)
        {
            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.GetFullPath(Path.Combine(projectRoot, NormalizeAssetPath(assetPath)));
        }

        private static Dictionary<string, object> Error(string message)
        {
            return new Dictionary<string, object> { { "success", false }, { "error", message } };
        }

        internal sealed class ImageFingerprint
        {
            public readonly string Hash;
            public readonly int Width;
            public readonly int Height;

            public ImageFingerprint(string hash, int width, int height)
            {
                Hash = hash;
                Width = width;
                Height = height;
            }
        }

        internal sealed class ImageAssetRecord
        {
            public readonly string AssetPath;
            public readonly string Guid;
            public readonly long FileSize;
            public readonly ImageFingerprint Fingerprint;

            public ImageAssetRecord(string assetPath, string guid, long fileSize, ImageFingerprint fingerprint)
            {
                AssetPath = assetPath;
                Guid = guid;
                FileSize = fileSize;
                Fingerprint = fingerprint;
            }
        }

        private sealed class CachedFingerprint
        {
            public readonly long FileSize;
            public readonly long LastWriteTicks;
            public readonly ImageFingerprint Fingerprint;

            public CachedFingerprint(long fileSize, long lastWriteTicks, ImageFingerprint fingerprint)
            {
                FileSize = fileSize;
                LastWriteTicks = lastWriteTicks;
                Fingerprint = fingerprint;
            }
        }
    }
}
