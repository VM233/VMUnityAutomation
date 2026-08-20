using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace VMUnityAutomation.Editor
{
    internal static class VmAutomationAssetCommandUtility
    {
    internal static List<Dictionary<string, object>> DescribeSubAssets(string assetPath)
    {
        var result = new List<Dictionary<string, object>>();
        foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(assetPath))
        {
            if (asset == null)
                continue;

            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string guid,
                out long fileId);
            result.Add(new Dictionary<string, object>
            {
                { "name", asset.name },
                { "type", asset.GetType().FullName },
                { "guid", guid },
                { "fileID", fileId }
            });
        }

        return result;
    }

    internal static string GetString(Dictionary<string, object> args, string key)
    {
        return args != null && args.ContainsKey(key) ? args[key]?.ToString() : "";
    }

    internal static string GetFirstString(Dictionary<string, object> args, params string[] keys)
    {
        if (args == null || keys == null)
            return "";

        foreach (string key in keys)
        {
            string value = GetString(args, key);
            if (!string.IsNullOrEmpty(value))
                return value;
        }

        return "";
    }

    internal static bool GetBool(Dictionary<string, object> args, string key, bool defaultValue)
    {
        if (args == null || !args.ContainsKey(key) || args[key] == null)
            return defaultValue;

        try
        {
            return Convert.ToBoolean(args[key]);
        }
        catch
        {
            return defaultValue;
        }
    }

    internal static List<string> GetStringList(Dictionary<string, object> args, string key)
    {
        var result = new List<string>();
        if (args == null || !args.ContainsKey(key) || args[key] == null)
            return result;

        object value = args[key];
        if (value is string stringValue)
        {
            if (!string.IsNullOrWhiteSpace(stringValue))
                result.Add(stringValue);
            return result;
        }

        var enumerable = value as IEnumerable;
        if (enumerable == null)
            return result;

        foreach (object item in enumerable)
        {
            if (item == null)
                continue;

            string text = item.ToString();
            if (!string.IsNullOrWhiteSpace(text))
                result.Add(text);
        }

        return result;
    }

    internal static List<Dictionary<string, object>> GetDictionaryList(Dictionary<string, object> args, string key)
    {
        var result = new List<Dictionary<string, object>>();
        if (args == null || !args.TryGetValue(key, out object value) || !(value is IEnumerable enumerable))
            return result;

        foreach (object item in enumerable)
        {
            if (item is Dictionary<string, object> dictionary)
            {
                result.Add(dictionary);
                continue;
            }

            if (!(item is IDictionary dictionaryValue))
                continue;

            var converted = new Dictionary<string, object>();
            foreach (DictionaryEntry pair in dictionaryValue)
            {
                if (pair.Key != null)
                    converted[pair.Key.ToString()] = pair.Value;
            }

            result.Add(converted);
        }

        return result;
    }

    internal static bool TryGetDictionaryList(Dictionary<string, object> args, string key,
        out List<Dictionary<string, object>> result, out string error)
    {
        result = new List<Dictionary<string, object>>();
        error = "";
        if (args == null || !args.TryGetValue(key, out object value) || value == null)
            return true;
        if (value is string || value is IDictionary || !(value is IEnumerable enumerable))
        {
            error = $"{key} must be an array";
            return false;
        }

        int index = 0;
        foreach (object item in enumerable)
        {
            if (item is Dictionary<string, object> dictionary)
            {
                result.Add(dictionary);
            }
            else if (item is IDictionary dictionaryValue)
            {
                var converted = new Dictionary<string, object>();
                foreach (DictionaryEntry pair in dictionaryValue)
                {
                    if (pair.Key != null)
                        converted[pair.Key.ToString()] = pair.Value;
                }
                result.Add(converted);
            }
            else
            {
                error = $"{key}[{index}] must be an object";
                return false;
            }
            index++;
        }
        return true;
    }

    internal static bool TryGetDictionary(Dictionary<string, object> args, string key,
        out Dictionary<string, object> result, out string error)
    {
        result = new Dictionary<string, object>();
        error = "";
        if (args == null || !args.TryGetValue(key, out object value) || value == null)
            return true;
        if (value is Dictionary<string, object> dictionary)
        {
            result = dictionary;
            return true;
        }
        if (!(value is IDictionary dictionaryValue))
        {
            error = $"{key} must be an object";
            return false;
        }

        foreach (DictionaryEntry pair in dictionaryValue)
        {
            if (pair.Key != null)
                result[pair.Key.ToString()] = pair.Value;
        }
        return true;
    }

    internal static bool AssetExists(string path)
    {
        return AssetDatabase.IsValidFolder(path) || AssetDatabase.LoadMainAssetAtPath(path) != null;
    }

    internal static string NormalizeAssetPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return "";

        return path.Replace('\\', '/').Trim().Trim('/');
    }

    internal static string GetMetaPath(string assetPath)
    {
        return string.IsNullOrEmpty(assetPath) ? "" : NormalizeAssetPath(assetPath) + ".meta";
    }

    internal static string GetAbsolutePath(string assetPath)
    {
        if (string.IsNullOrEmpty(assetPath))
            return "";

        if (Path.IsPathRooted(assetPath))
            return Path.GetFullPath(assetPath);

        return Path.GetFullPath(Path.Combine(GetProjectRoot(), NormalizeAssetPath(assetPath)));
    }

    internal static string NormalizeUnityPackageOutputPath(string outputPath)
    {
        string normalized = outputPath.Replace('\\', '/').Trim();
        if (!string.Equals(Path.GetExtension(normalized), ".unitypackage", StringComparison.OrdinalIgnoreCase))
            normalized += ".unitypackage";

        if (!Path.IsPathRooted(normalized))
            normalized = Path.Combine(GetProjectRoot(), normalized);

        return Path.GetFullPath(normalized);
    }

    internal static string NormalizeUnityPackageInputPath(string packagePath)
    {
        string normalized = packagePath.Replace('\\', '/').Trim();
        if (!Path.IsPathRooted(normalized))
            normalized = Path.Combine(GetProjectRoot(), normalized);
        return Path.GetFullPath(normalized);
    }

    internal static string GetProjectRoot()
    {
        return Directory.GetParent(Application.dataPath).FullName;
    }

    internal static string NormalizeMoveTargetPath(string sourcePath, string destinationPath)
    {
        destinationPath = destinationPath.Replace('\\', '/');
        if (AssetDatabase.IsValidFolder(destinationPath))
            return destinationPath.TrimEnd('/') + "/" + Path.GetFileName(sourcePath);

        return destinationPath;
    }


    }
}
