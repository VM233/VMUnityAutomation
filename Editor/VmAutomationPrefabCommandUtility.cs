using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace VMUnityAutomation.Editor
{
    internal static class VmAutomationPrefabCommandUtility
    {
    internal static byte[] ReadAllBytesWithRetry(string path)
    {
        return VmAutomationPersistenceFile.ReadAllBytes(path);
    }

    internal static string DecodeUtf8(byte[] bytes)
    {
        if (bytes == null || bytes.Length == 0)
            return "";
        int offset = HasUtf8Bom(bytes) ? 3 : 0;
        return Encoding.UTF8.GetString(bytes, offset, bytes.Length - offset);
    }

    internal static bool HasUtf8Bom(byte[] bytes)
    {
        return bytes != null && bytes.Length >= 3 && bytes[0] == 0xEF &&
               bytes[1] == 0xBB && bytes[2] == 0xBF;
    }

    internal static void ImportPrefabAssetSynchronously(string assetPath)
    {
        AssetDatabase.ImportAsset(assetPath,
            ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
    }

    internal static string GetString(Dictionary<string, object> args, string key)
    {
        return args != null && args.ContainsKey(key) ? args[key]?.ToString() : "";
    }

    internal static List<string> GetStringList(Dictionary<string, object> args, string key)
    {
        var results = new List<string>();
        if (args == null || !args.TryGetValue(key, out object value) || value == null)
            return results;

        if (value is List<object> list)
        {
            foreach (object item in list)
            {
                if (item != null)
                    results.Add(item.ToString());
            }
        }
        else
        {
            results.Add(value.ToString());
        }

        return results;
    }

    internal static Dictionary<string, object> GetDictionary(Dictionary<string, object> args, string key)
    {
        if (args == null || !args.TryGetValue(key, out object value) || value == null)
            return null;

        return value as Dictionary<string, object>;
    }

    internal static List<Dictionary<string, object>> GetDictionaryList(Dictionary<string, object> args, string key)
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

    internal static void AddPrefabFileDiff(Dictionary<string, object> result,
        AssetTextSnapshot beforeSnapshot, string assetPath, Dictionary<string, object> args)
    {
        if (result == null || GetBool(args, "includePrefabFileDiff",
                VmAutomationSettings.IncludePrefabFileDiffByDefault) == false)
            return;

        result["prefabFileDiff"] = BuildAssetTextDiff(beforeSnapshot, assetPath, args);
    }

    internal static Dictionary<string, object> BuildAssetTextDiff(AssetTextSnapshot beforeSnapshot,
        string assetPath, Dictionary<string, object> args)
    {
        var afterSnapshot = CaptureAssetText(assetPath);
        int contextLines = Math.Max(0, GetInt(args, "prefabFileDiffContextLines", 2));
        string diffMode = (GetString(args, "prefabFileDiffMode") ?? "").ToLowerInvariant();
        if (diffMode != "full" && diffMode != "minimal" && diffMode != "summary")
            diffMode = "summary";
        if (diffMode == "minimal")
            contextLines = 0;

        int maxLines = Math.Max(1, Math.Min(GetInt(args, "prefabFileDiffMaxLines", 200), 1000));

        var result = new Dictionary<string, object>
        {
            { "assetPath", assetPath },
            { "absolutePath", afterSnapshot.AbsolutePath },
            { "existsBefore", beforeSnapshot.Exists },
            { "existsAfter", afterSnapshot.Exists },
            { "readErrorBefore", beforeSnapshot.ReadError ?? "" },
            { "readErrorAfter", afterSnapshot.ReadError ?? "" },
        };

        if (!string.IsNullOrEmpty(beforeSnapshot.ReadError) || !string.IsNullOrEmpty(afterSnapshot.ReadError))
        {
            result["changed"] = true;
            result["lines"] = new List<Dictionary<string, object>>();
            result["truncated"] = false;
            return result;
        }

        var beforeLines = SplitLines(beforeSnapshot.Text);
        var afterLines = SplitLines(afterSnapshot.Text);

        result["beforeLineCount"] = beforeLines.Length;
        result["afterLineCount"] = afterLines.Length;
        var allLines = BuildLineDiff(beforeLines, afterLines);
        var summary = BuildDiffSummary(beforeLines.Length, afterLines.Length, allLines);
        int changedLineCount = Convert.ToInt32(summary["addedLineCount"]) +
                               Convert.ToInt32(summary["removedLineCount"]);
        result["changed"] = changedLineCount > 0;

        var lines = BuildDisplayDiffLines(allLines, contextLines);
        int ignoredLineCount = FilterDiffLines(lines, args);
        bool truncated = lines.Count > maxLines;
        if (truncated)
            lines.RemoveRange(maxLines, lines.Count - maxLines);
        summary["contextLineCount"] = lines.Count(line =>
            line.TryGetValue("type", out var value) && value?.ToString() == "context");

        result["changedLineCount"] = changedLineCount;
        result["ignoredLineCount"] = ignoredLineCount;
        result["returnedLineCount"] = diffMode == "summary" ? 0 : lines.Count;
        result["truncated"] = truncated;
        result["summary"] = summary;
        result["lines"] = diffMode == "summary" ? new List<Dictionary<string, object>>() : lines;
        return result;
    }

    internal static Dictionary<string, object> BuildDiffSummary(int beforeLineCount, int afterLineCount,
        List<Dictionary<string, object>> lines)
    {
        int added = 0;
        int removed = 0;
        int context = 0;

        foreach (var line in lines)
        {
            var type = line.TryGetValue("type", out var value) && value != null ? value.ToString() : "";
            switch (type)
            {
                case "added":
                    added++;
                    break;
                case "removed":
                    removed++;
                    break;
                case "context":
                    context++;
                    break;
            }
        }

        return new Dictionary<string, object>
        {
            { "beforeLineCount", beforeLineCount },
            { "afterLineCount", afterLineCount },
            { "netLineDelta", afterLineCount - beforeLineCount },
            { "addedLineCount", added },
            { "removedLineCount", removed },
            { "contextLineCount", context },
        };
    }

    internal static int FilterDiffLines(List<Dictionary<string, object>> lines, Dictionary<string, object> args)
    {
        var ignoreContains = GetStringList(args, "prefabFileDiffIgnoreContains");
        var ignoreProperties = GetStringList(args, "prefabFileDiffIgnoreYamlProperties");
        if (ignoreContains.Count == 0 && ignoreProperties.Count == 0)
            return 0;

        int beforeCount = lines.Count;
        lines.RemoveAll(line =>
        {
            var text = line.TryGetValue("text", out var value) && value != null ? value.ToString() : "";
            return ignoreContains.Any(item => text.Contains(item)) ||
                   ignoreProperties.Any(property => IsYamlPropertyLine(text, property));
        });

        return beforeCount - lines.Count;
    }

    internal static bool IsYamlPropertyLine(string text, string propertyName)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(propertyName))
            return false;

        string trimmed = text.TrimStart();
        return trimmed.StartsWith(propertyName + ":", StringComparison.Ordinal) ||
               trimmed.StartsWith("- " + propertyName + ":", StringComparison.Ordinal);
    }

    internal static List<Dictionary<string, object>> BuildLineDiff(string[] beforeLines, string[] afterLines)
    {
        int beforeCount = beforeLines.Length;
        int afterCount = afterLines.Length;
        int maxDepth = beforeCount + afterCount;
        var trace = new List<Dictionary<int, int>>();
        var vector = new Dictionary<int, int> { { 1, 0 } };

        for (int depth = 0; depth <= maxDepth; depth++)
        {
            trace.Add(new Dictionary<int, int>(vector));
            for (int diagonal = -depth; diagonal <= depth; diagonal += 2)
            {
                int beforeIndex;
                if (diagonal == -depth ||
                    diagonal != depth && GetDiffVectorValue(vector, diagonal - 1) <
                    GetDiffVectorValue(vector, diagonal + 1))
                {
                    beforeIndex = GetDiffVectorValue(vector, diagonal + 1);
                }
                else
                {
                    beforeIndex = GetDiffVectorValue(vector, diagonal - 1) + 1;
                }

                int afterIndex = beforeIndex - diagonal;
                while (beforeIndex < beforeCount && afterIndex < afterCount &&
                       beforeLines[beforeIndex] == afterLines[afterIndex])
                {
                    beforeIndex++;
                    afterIndex++;
                }

                vector[diagonal] = beforeIndex;
                if (beforeIndex >= beforeCount && afterIndex >= afterCount)
                    return BacktrackLineDiff(trace, beforeLines, afterLines);
            }
        }

        return new List<Dictionary<string, object>>();
    }

    internal static List<Dictionary<string, object>> BacktrackLineDiff(
        List<Dictionary<int, int>> trace, string[] beforeLines, string[] afterLines)
    {
        int beforeIndex = beforeLines.Length;
        int afterIndex = afterLines.Length;
        var reversed = new List<Dictionary<string, object>>();

        for (int depth = trace.Count - 1; depth >= 0; depth--)
        {
            var vector = trace[depth];
            int diagonal = beforeIndex - afterIndex;
            int previousDiagonal;
            if (diagonal == -depth ||
                diagonal != depth && GetDiffVectorValue(vector, diagonal - 1) <
                GetDiffVectorValue(vector, diagonal + 1))
            {
                previousDiagonal = diagonal + 1;
            }
            else
            {
                previousDiagonal = diagonal - 1;
            }

            int previousBeforeIndex = GetDiffVectorValue(vector, previousDiagonal);
            int previousAfterIndex = previousBeforeIndex - previousDiagonal;

            while (beforeIndex > previousBeforeIndex && afterIndex > previousAfterIndex)
            {
                reversed.Add(CreateDiffLine("context", beforeIndex, afterIndex,
                    beforeLines[beforeIndex - 1]));
                beforeIndex--;
                afterIndex--;
            }

            if (depth == 0)
                break;

            if (beforeIndex == previousBeforeIndex)
            {
                reversed.Add(CreateDiffLine("added", null, afterIndex, afterLines[afterIndex - 1]));
                afterIndex--;
            }
            else
            {
                reversed.Add(CreateDiffLine("removed", beforeIndex, null, beforeLines[beforeIndex - 1]));
                beforeIndex--;
            }
        }

        reversed.Reverse();
        return reversed;
    }

    internal static int GetDiffVectorValue(Dictionary<int, int> vector, int diagonal)
    {
        return vector.TryGetValue(diagonal, out int value) ? value : 0;
    }

    internal static Dictionary<string, object> CreateDiffLine(string type, int? beforeLine,
        int? afterLine, string text)
    {
        return new Dictionary<string, object>
        {
            { "type", type },
            { "beforeLine", beforeLine.HasValue ? beforeLine.Value : (object)null },
            { "afterLine", afterLine.HasValue ? afterLine.Value : (object)null },
            { "text", text },
        };
    }

    internal static List<Dictionary<string, object>> BuildDisplayDiffLines(
        List<Dictionary<string, object>> allLines, int contextLines)
    {
        if (allLines.Count == 0)
            return new List<Dictionary<string, object>>();

        var included = new bool[allLines.Count];
        for (int i = 0; i < allLines.Count; i++)
        {
            string type = allLines[i].TryGetValue("type", out var value) ? value?.ToString() : "";
            if (type == "context")
                continue;

            int start = Math.Max(0, i - contextLines);
            int end = Math.Min(allLines.Count - 1, i + contextLines);
            for (int lineIndex = start; lineIndex <= end; lineIndex++)
                included[lineIndex] = true;
        }

        var lines = new List<Dictionary<string, object>>();
        for (int i = 0; i < allLines.Count; i++)
        {
            if (included[i])
                lines.Add(allLines[i]);
        }
        return lines;
    }

    internal static AssetTextSnapshot CaptureAssetText(string assetPath)
    {
        var snapshot = new AssetTextSnapshot
        {
            AssetPath = assetPath,
            AbsolutePath = GetAbsoluteAssetPath(assetPath),
        };

        try
        {
            snapshot.Exists = File.Exists(snapshot.AbsolutePath);
            snapshot.Bytes = snapshot.Exists
                ? ReadAllBytesWithRetry(snapshot.AbsolutePath)
                : Array.Empty<byte>();
            snapshot.Text = snapshot.Exists ? DecodeUtf8(snapshot.Bytes) : "";
        }
        catch (Exception ex)
        {
            snapshot.ReadError = ex.Message;
            snapshot.Text = "";
        }

        return snapshot;
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

    internal static string[] SplitLines(string text)
    {
        if (string.IsNullOrEmpty(text))
            return Array.Empty<string>();

        return text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n')
            .Select(line => line.TrimEnd(' ', '\t')).ToArray();
    }


    internal static bool GetBool(Dictionary<string, object> args, string key, bool defaultValue)
    {
        if (args == null || args.ContainsKey(key) == false || args[key] == null)
            return defaultValue;

        if (args[key] is bool value)
            return value;

        return bool.TryParse(args[key].ToString(), out bool parsed) ? parsed : defaultValue;
    }

    internal static int GetInt(Dictionary<string, object> args, string key, int defaultValue)
    {
        if (args == null || args.ContainsKey(key) == false || args[key] == null)
            return defaultValue;

        return int.TryParse(args[key].ToString(), out int value) ? value : defaultValue;
    }

    internal static DateTime GetDateTime(Dictionary<string, object> args, string key,
        DateTime defaultValue)
    {
        if (args == null || !args.TryGetValue(key, out object raw) || raw == null)
            return defaultValue;
        return DateTime.TryParse(raw.ToString(), null,
            System.Globalization.DateTimeStyles.RoundtripKind, out DateTime value)
            ? value
            : defaultValue;
    }

    internal static string GetPrefabPath(GameObject root, GameObject go)
    {
        if (root == go)
            return "";

        var names = new Stack<string>();
        Transform current = go.transform;
        while (current != null && current.gameObject != root)
        {
            names.Push(current.name);
            current = current.parent;
        }

        return current == null ? go.name : string.Join("/", names);
    }

    internal static bool TryAddFindResult(List<Dictionary<string, object>> results, int maxResults,
        ref bool truncated, GameObject root, GameObject go, Component component, string propertyName,
        object propertyValue)
    {
        if (results.Count >= maxResults)
        {
            truncated = true;
            return false;
        }

        var result = new Dictionary<string, object>
        {
            { "name", go.name },
            { "prefabPath", GetPrefabPath(root, go) },
            { "active", go.activeSelf },
            { "layer", LayerMask.LayerToName(go.layer) },
        };
        VmAutomationTransformSerialization.AddLocal(result, go.transform);

        if (component != null)
        {
            result["component"] = component.GetType().Name;
            result["componentFullType"] = component.GetType().FullName;
        }

        if (string.IsNullOrEmpty(propertyName) == false)
        {
            result["propertyName"] = propertyName;
            result["propertyValue"] = propertyValue;
        }

        results.Add(result);
        return true;
    }

    internal static Dictionary<string, object> BuildFindResponse(GameObject root, string assetPath,
        List<Dictionary<string, object>> results, bool truncated)
    {
        return new Dictionary<string, object>
        {
            { "success", true },
            { "prefab", root.name },
            { "assetPath", assetPath },
            { "count", results.Count },
            { "truncated", truncated },
            { "results", results },
        };
    }

    internal static bool SerializedValueMatches(object actual, object expected)
    {
        if (expected == null)
            return actual == null;
        if (actual == null)
            return string.Equals(expected.ToString(), "null", StringComparison.OrdinalIgnoreCase);

        if (actual is Dictionary<string, object> || expected is Dictionary<string, object>)
            return string.Equals(MiniJson.Serialize(actual), MiniJson.Serialize(expected),
                StringComparison.OrdinalIgnoreCase);

        if (actual is System.Collections.IList || expected is System.Collections.IList)
            return string.Equals(MiniJson.Serialize(actual), MiniJson.Serialize(expected),
                StringComparison.OrdinalIgnoreCase);

        return string.Equals(actual.ToString(), expected.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    internal static Vector3 ParseVector3(object value)
    {
        if (value is Dictionary<string, object> d)
        {
            return new Vector3(
                d.ContainsKey("x") ? Convert.ToSingle(d["x"]) : 0f,
                d.ContainsKey("y") ? Convert.ToSingle(d["y"]) : 0f,
                d.ContainsKey("z") ? Convert.ToSingle(d["z"]) : 0f
            );
        }
        return Vector3.zero;
    }
    }
}
