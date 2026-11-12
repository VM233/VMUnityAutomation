using System;
using System.IO;
using UnityEngine;

namespace VMUnityAutomation.Editor
{
    internal static class VmAutomationAssetPathUtility
    {
    internal static string NormalizeAssetPath(string rawPath, string relativeToAssetPath)
    {
        if (string.IsNullOrWhiteSpace(rawPath))
            return "";

        string path = rawPath.Trim().Replace('\\', '/');
        int queryIndex = path.IndexOf('?');
        if (queryIndex >= 0)
            path = path.Substring(0, queryIndex);

        int fragmentIndex = path.IndexOf('#');
        if (fragmentIndex >= 0)
            path = path.Substring(0, fragmentIndex);

        const string projectPrefix = "project://database/";
        if (path.StartsWith(projectPrefix, StringComparison.OrdinalIgnoreCase))
            path = path.Substring(projectPrefix.Length);

        path = Uri.UnescapeDataString(path);

        if (path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("Packages/", StringComparison.OrdinalIgnoreCase) ||
            Path.IsPathRooted(path))
        {
            return path;
        }

        if (string.IsNullOrEmpty(relativeToAssetPath) == false)
        {
            string directory = Path.GetDirectoryName(relativeToAssetPath)?.Replace('\\', '/');
            if (string.IsNullOrEmpty(directory) == false)
                return $"{directory}/{path}";
        }

        return path;
    }

    internal static string GetAbsoluteAssetPath(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath))
            return "";

        if (Path.IsPathRooted(assetPath))
            return Path.GetFullPath(assetPath);

        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        return Path.GetFullPath(Path.Combine(projectRoot, assetPath));
    }

    }
}
