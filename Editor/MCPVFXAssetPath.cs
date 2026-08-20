using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace VMUnityAutomation.Editor
{
    internal static class MCPVFXAssetPath
    {
        internal static bool TryNormalizeFile(string rawPath, bool allowPackages,
            out string path, out string error)
        {
            path = (rawPath ?? "").Trim().Replace('\\', '/');
            error = null;
            string root = path.StartsWith("Assets/", StringComparison.Ordinal)
                ? "Assets"
                : allowPackages && path.StartsWith("Packages/",
                    StringComparison.Ordinal) ? "Packages" : "";
            if (string.IsNullOrEmpty(root))
            {
                error = allowPackages
                    ? "Path must identify a file below Assets/ or Packages/."
                    : "Path must identify a file below Assets/.";
                return false;
            }
            string[] segments = path.Split('/');
            if (segments.Length < 2 || segments.Any(segment =>
                    string.IsNullOrWhiteSpace(segment) || segment == "." ||
                    segment == ".." ||
                    segment.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0) ||
                string.IsNullOrEmpty(Path.GetExtension(path)))
            {
                error = $"Asset path is not canonical: '{path}'.";
                return false;
            }
            if (root == "Assets")
            {
                string assetsRoot = Path.GetFullPath(Application.dataPath);
                string relative = path.Substring("Assets/".Length)
                    .Replace('/', Path.DirectorySeparatorChar);
                string absolute = Path.GetFullPath(Path.Combine(assetsRoot,
                    relative));
                if (!absolute.StartsWith(assetsRoot + Path.DirectorySeparatorChar,
                        StringComparison.OrdinalIgnoreCase))
                {
                    error = $"Asset path escapes Assets: '{path}'.";
                    return false;
                }
            }
            return true;
        }

        internal static string RequireFile(string rawPath, bool allowPackages,
            string argumentName)
        {
            if (!TryNormalizeFile(rawPath, allowPackages, out string path,
                    out string error))
                throw new ArgumentException(argumentName + ": " + error);
            return path;
        }

        internal static string ToAbsoluteAssetsPath(string assetPath)
        {
            string normalized = RequireFile(assetPath, false, "assetPath");
            return Path.GetFullPath(Path.Combine(Application.dataPath,
                normalized.Substring("Assets/".Length)
                    .Replace('/', Path.DirectorySeparatorChar)));
        }

        internal static IReadOnlyList<string> EnsureParentFolder(string assetPath)
        {
            string normalized = RequireFile(assetPath, false, "assetPath");
            string directory = Path.GetDirectoryName(normalized)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(directory) || directory == "Assets")
                return Array.Empty<string>();
            var created = new List<string>();
            string current = "Assets";
            foreach (string segment in directory.Split('/').Skip(1))
            {
                string next = current + "/" + segment;
                if (!AssetDatabase.IsValidFolder(next))
                {
                    string guid = AssetDatabase.CreateFolder(current, segment);
                    if (string.IsNullOrEmpty(guid))
                        throw new InvalidOperationException(
                            $"Failed to create asset folder '{next}'.");
                    created.Add(next);
                }
                current = next;
            }
            return created;
        }

        internal static void RollBackCreatedFolders(
            IReadOnlyList<string> createdFolders)
        {
            for (int index = createdFolders.Count - 1; index >= 0; index--)
            {
                string folder = createdFolders[index];
                if (AssetDatabase.IsValidFolder(folder) &&
                    !AssetDatabase.DeleteAsset(folder))
                    throw new InvalidOperationException(
                        $"Failed to roll back created asset folder '{folder}'.");
            }
        }
    }
}
