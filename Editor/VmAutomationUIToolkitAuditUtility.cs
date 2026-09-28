#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace VMUnityAutomation.Editor
{
    internal static class VmAutomationUIToolkitAuditUtility
    {
        internal static string GetProjectRoot()
        {
            return Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        }

        internal static List<string> GetStringList(IDictionary<string, object> args, string key)
        {
            object value;
            if (args == null || !args.TryGetValue(key, out value) || value == null)
                return new List<string>();

            var text = value as string;
            if (text != null)
                return new List<string> { text };

            var result = new List<string>();
            var values = value as IEnumerable;
            if (values == null)
                return result;
            foreach (object item in values)
            {
                if (item != null)
                    result.Add(item.ToString());
            }
            return result;
        }

        internal static bool GetBool(IDictionary<string, object> args, string key,
            bool defaultValue = false)
        {
            object value;
            if (args == null || !args.TryGetValue(key, out value) || value == null)
                return defaultValue;
            if (value is bool)
                return (bool)value;
            bool parsed;
            return bool.TryParse(value.ToString(), out parsed) ? parsed : defaultValue;
        }

        internal static int GetInt(IDictionary<string, object> args, string key, int defaultValue)
        {
            object value;
            if (args == null || !args.TryGetValue(key, out value) || value == null)
                return defaultValue;
            try
            {
                return Convert.ToInt32(value);
            }
            catch
            {
                return defaultValue;
            }
        }

        internal static string NormalizeAssetPath(string path)
        {
            path = (path ?? "").Trim().Replace('\\', '/');
            if (string.IsNullOrEmpty(path))
                return "";

            if (Path.IsPathRooted(path))
            {
                string fullPath = Path.GetFullPath(path).Replace('\\', '/');
                string projectRoot = GetProjectRoot().Replace('\\', '/').TrimEnd('/');
                if (fullPath.StartsWith(projectRoot + "/", StringComparison.OrdinalIgnoreCase))
                    path = fullPath.Substring(projectRoot.Length + 1);
                else
                    return fullPath;
            }

            while (path.StartsWith("./", StringComparison.Ordinal))
                path = path.Substring(2);
            return path.Trim('/');
        }

        internal static string ToFullPath(string assetPath)
        {
            string normalized = NormalizeAssetPath(assetPath);
            if (Path.IsPathRooted(normalized))
                return Path.GetFullPath(normalized);
            return Path.GetFullPath(Path.Combine(GetProjectRoot(),
                normalized.Replace('/', Path.DirectorySeparatorChar)));
        }

        internal static string ToAssetPath(string fullPath)
        {
            string normalizedFullPath = Path.GetFullPath(fullPath).Replace('\\', '/');
            string projectRoot = GetProjectRoot().Replace('\\', '/').TrimEnd('/');
            return normalizedFullPath.StartsWith(projectRoot + "/", StringComparison.OrdinalIgnoreCase)
                ? normalizedFullPath.Substring(projectRoot.Length + 1)
                : normalizedFullPath;
        }

        internal static IEnumerable<string> FindAssetFiles(string extension,
            VmAutomationUIToolkitAuditOptions options)
        {
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string root in options.AssetRoots)
            {
                string fullRoot = ToFullPath(root);
                if (!Directory.Exists(fullRoot))
                    continue;

                foreach (string fullPath in Directory.EnumerateFiles(fullRoot, "*" + extension,
                             SearchOption.AllDirectories))
                {
                    string assetPath = ToAssetPath(fullPath);
                    if (options.Includes(assetPath))
                        paths.Add(assetPath);
                }
            }
            return paths.OrderBy(path => path, StringComparer.Ordinal);
        }

        internal static IEnumerable<string> FindRuntimeSourceFiles(
            VmAutomationUIToolkitAuditOptions options)
        {
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string root in options.RuntimeSourceRoots)
            {
                string fullRoot = ToFullPath(root);
                if (!Directory.Exists(fullRoot))
                    continue;

                foreach (string fullPath in Directory.EnumerateFiles(fullRoot, "*.cs",
                             SearchOption.AllDirectories))
                {
                    string assetPath = ToAssetPath(fullPath);
                    if (options.IncludesRuntimeSource(assetPath))
                        paths.Add(assetPath);
                }
            }
            return paths.OrderBy(path => path, StringComparer.Ordinal);
        }
    }
}
#endif
