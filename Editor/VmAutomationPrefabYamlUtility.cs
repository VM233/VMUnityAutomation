using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using UnityEditor;

namespace VMUnityAutomation.Editor
{
    internal static class VmAutomationPrefabYamlUtility
    {
        private const int TransientFileIoMaxAttempts = 6;

    internal static bool TryStabilizePrefabYaml(string assetPath, byte[] beforeBytes,
        ISet<string> explicitYamlPropertyRoots, out string warning)
    {
        warning = "";
        try
        {
            string absolutePath = GetAbsoluteAssetPath(assetPath);
            if (string.IsNullOrEmpty(absolutePath) || !File.Exists(absolutePath))
                return true;

            byte[] afterBytes = ReadAllBytesWithRetry(absolutePath);
            bool hasUtf8Bom = HasUtf8Bom(beforeBytes) || beforeBytes == null && HasUtf8Bom(afterBytes);
            string afterText = DecodeUtf8(afterBytes);
            string normalized = NormalizeYamlWhitespace(afterText);

            if (beforeBytes != null)
            {
                string beforeText = DecodeUtf8(beforeBytes);
                if (TryPreserveYamlBlockOrder(beforeText, normalized, explicitYamlPropertyRoots,
                        out string reordered))
                    normalized = reordered;
            }

            string currentText = DecodeUtf8(afterBytes);
            if (normalized == currentText && hasUtf8Bom == HasUtf8Bom(afterBytes))
                return true;

            WriteAllTextAtomicallyWithRetry(absolutePath, normalized, hasUtf8Bom);
            ImportPrefabAssetSynchronously(assetPath);
            return true;
        }
        catch (Exception ex)
        {
            warning = $"Prefab '{assetPath}' was saved, but post-save YAML stabilization was skipped: " +
                      ex.GetBaseException().Message;
            return false;
        }
    }

    internal static string NormalizeYamlWhitespace(string text)
    {
        return Regex.Replace(text ?? "", @"[\t ]+(?=\r?$)", "", RegexOptions.Multiline);
    }

    internal static byte[] ReadAllBytesWithRetry(string path)
    {
        return VmAutomationPersistenceFile.ReadAllBytes(path);
    }

    internal static void WriteAllTextAtomicallyWithRetry(string path, string contents, bool includeUtf8Bom)
    {
        byte[] payload = new UTF8Encoding(false).GetBytes(contents ?? "");
        if (includeUtf8Bom)
        {
            byte[] preamble = new UTF8Encoding(true).GetPreamble();
            var withPreamble = new byte[preamble.Length + payload.Length];
            Buffer.BlockCopy(preamble, 0, withPreamble, 0, preamble.Length);
            Buffer.BlockCopy(payload, 0, withPreamble, preamble.Length, payload.Length);
            payload = withPreamble;
        }
        VmAutomationPersistenceFile.WriteAllBytes(path, payload);
    }

    internal static T RetryTransientFileIo<T>(Func<T> operation, int maxAttempts,
        Action<int> delay)
    {
        if (operation == null)
            throw new ArgumentNullException(nameof(operation));
        maxAttempts = Math.Max(1, maxAttempts);
        delay ??= milliseconds => Thread.Sleep(milliseconds);

        for (int attempt = 1; ; attempt++)
        {
            try
            {
                return operation();
            }
            catch (Exception ex) when (attempt < maxAttempts && IsTransientFileIoException(ex))
            {
                delay(Math.Min(250, 10 << Math.Min(attempt - 1, 4)));
            }
        }
    }

    internal static bool IsTransientFileIoException(Exception exception)
    {
        for (Exception current = exception; current != null; current = current.InnerException)
        {
            if (!(current is IOException ioException))
                continue;

            int win32Code = ioException.HResult & 0xFFFF;
            // ERROR_SHARING_VIOLATION, ERROR_LOCK_VIOLATION, and
            // ERROR_USER_MAPPED_FILE (the reported Win32 1224 failure).
            if (win32Code == 32 || win32Code == 33 || win32Code == 1224)
                return true;
        }
        return false;
    }

    internal static bool TryPreserveYamlBlockOrder(string beforeText, string afterText,
        ISet<string> explicitYamlPropertyRoots, out string result)
    {
        result = afterText;
        if (!TryParseYamlFile(beforeText, out var beforeFile) ||
            !TryParseYamlFile(afterText, out var afterFile))
            return false;

        var afterByKey = new Dictionary<string, YamlObjectBlock>(StringComparer.Ordinal);
        foreach (var block in afterFile.Blocks)
        {
            if (afterByKey.ContainsKey(block.Key))
                return false;
            afterByKey.Add(block.Key, block);
        }

        var ordered = new List<YamlObjectBlock>();
        var included = new HashSet<string>(StringComparer.Ordinal);
        bool preservedUnrelatedFields = false;
        foreach (var beforeBlock in beforeFile.Blocks)
        {
            if (!afterByKey.TryGetValue(beforeBlock.Key, out var afterBlock))
                continue;

            string beforeCanonical = CanonicalYamlText(beforeBlock.Text);
            string afterCanonical = CanonicalYamlText(afterBlock.Text);
            if (beforeCanonical == afterCanonical)
            {
                ordered.Add(beforeBlock);
            }
            else if (ShouldPreserveUnrelatedAddedFields(beforeBlock, afterBlock,
                         explicitYamlPropertyRoots))
            {
                ordered.Add(beforeBlock);
                preservedUnrelatedFields = true;
            }
            else
            {
                ordered.Add(afterBlock);
            }
            included.Add(beforeBlock.Key);
        }

        foreach (var afterBlock in afterFile.Blocks)
        {
            if (included.Add(afterBlock.Key))
                ordered.Add(afterBlock);
        }

        if (ordered.Count != afterFile.Blocks.Count)
            return false;

        string preamble = CanonicalYamlText(beforeFile.Preamble) ==
                          CanonicalYamlText(afterFile.Preamble)
            ? beforeFile.Preamble
            : NormalizeYamlWhitespace(afterFile.Preamble);
        string candidate = preamble + string.Concat(ordered.Select(block => block.Text));
        candidate = ApplyLineEnding(candidate, beforeFile.LineEnding);

        if (!TryParseYamlFile(candidate, out var candidateFile))
            return false;
        if (preservedUnrelatedFields)
        {
            if (!YamlFilesHaveSameBlockKeys(candidateFile, afterFile))
                return false;
        }
        else if (!YamlFilesHaveEquivalentBlocks(candidateFile, afterFile))
        {
            return false;
        }

        result = candidate;
        return true;
    }

    internal static bool ShouldPreserveUnrelatedAddedFields(YamlObjectBlock beforeBlock,
        YamlObjectBlock afterBlock, ISet<string> explicitYamlPropertyRoots)
    {
        int separatorIndex = beforeBlock.Key.IndexOf(':');
        if (separatorIndex <= 0 ||
            !int.TryParse(beforeBlock.Key.Substring(0, separatorIndex), out int objectType) ||
            objectType == 1 || objectType == 4 || objectType == 224 || objectType == 1001)
            return false;

        string[] beforeLines = CanonicalYamlText(beforeBlock.Text).Split('\n');
        string[] afterLines = CanonicalYamlText(afterBlock.Text).Split('\n');
        var addedLines = new List<string>();
        int beforeIndex = 0;
        string currentPropertyRoot = "";
        foreach (string afterLine in afterLines)
        {
            string propertyRoot = GetTopLevelYamlPropertyName(afterLine);
            if (string.IsNullOrEmpty(propertyRoot) == false)
                currentPropertyRoot = propertyRoot;

            if (beforeIndex < beforeLines.Length && afterLine == beforeLines[beforeIndex])
            {
                beforeIndex++;
            }
            else
            {
                addedLines.Add(afterLine);
                if (explicitYamlPropertyRoots != null &&
                    explicitYamlPropertyRoots.Contains(currentPropertyRoot))
                    return false;
            }
        }

        if (beforeIndex != beforeLines.Length || addedLines.Count == 0)
            return false;

        if (explicitYamlPropertyRoots == null || explicitYamlPropertyRoots.Count == 0)
            return true;

        foreach (string line in addedLines)
        {
            string trimmed = line.TrimStart();
            if (trimmed.StartsWith("- ", StringComparison.Ordinal))
                trimmed = trimmed.Substring(2).TrimStart();
            int colonIndex = trimmed.IndexOf(':');
            if (colonIndex <= 0)
                continue;
            string propertyName = trimmed.Substring(0, colonIndex);
            if (explicitYamlPropertyRoots.Contains(propertyName))
                return false;
        }

        return true;
    }

    internal static string GetTopLevelYamlPropertyName(string line)
    {
        if (string.IsNullOrEmpty(line) || line.Length < 3 || line[0] != ' ' || line[1] != ' ' ||
            line[2] == ' ' || line[2] == '\t' || line[2] == '-')
            return "";

        int colonIndex = line.IndexOf(':', 2);
        if (colonIndex <= 2)
            return "";

        return line.Substring(2, colonIndex - 2);
    }

    internal static bool YamlFilesHaveSameBlockKeys(YamlFile left, YamlFile right)
    {
        if (left.Blocks.Count != right.Blocks.Count)
            return false;
        return new HashSet<string>(left.Blocks.Select(block => block.Key), StringComparer.Ordinal)
            .SetEquals(right.Blocks.Select(block => block.Key));
    }

    internal static HashSet<string> BuildExplicitYamlPropertyRoots(params string[] propertyPaths)
    {
        var roots = new HashSet<string>(StringComparer.Ordinal);
        if (propertyPaths == null)
            return roots;
        foreach (string propertyPath in propertyPaths)
        {
            if (string.IsNullOrWhiteSpace(propertyPath))
                continue;
            int separatorIndex = propertyPath.IndexOf('.');
            roots.Add(separatorIndex > 0 ? propertyPath.Substring(0, separatorIndex) : propertyPath);
        }
        return roots;
    }

    internal static HashSet<string> CollectExplicitYamlPropertyRoots(
        IEnumerable<Dictionary<string, object>> operations)
    {
        var roots = new HashSet<string>(StringComparer.Ordinal);
        if (operations == null)
            return roots;

        foreach (var operation in operations)
        {
            string operationType = GetOperationType(operation);
            if (operationType != "addcomponent" && operationType != "setproperty" &&
                operationType != "setreference" &&
                operationType != "configurecomponent" && operationType != "arrayinsert" &&
                operationType != "arrayremove" && operationType != "arrayset" &&
                operationType != "arrayclear")
                continue;

            var properties = GetDictionary(operation, "properties");
            if (properties != null)
            {
                foreach (string propertyName in properties.Keys)
                    roots.UnionWith(BuildExplicitYamlPropertyRoots(propertyName));
            }
            else if (operationType != "addcomponent" &&
                     operationType != "configurecomponent")
            {
                roots.UnionWith(BuildExplicitYamlPropertyRoots(GetString(operation, "propertyName")));
            }

            if (operationType == "configurecomponent")
            {
                foreach (var reference in GetDictionaryList(operation, "references"))
                    roots.UnionWith(BuildExplicitYamlPropertyRoots(GetString(reference, "propertyName")));
            }
        }

        return roots;
    }

    internal static bool YamlFilesHaveEquivalentBlocks(YamlFile left, YamlFile right)
    {
        if (left.Blocks.Count != right.Blocks.Count)
            return false;

        var rightByKey = right.Blocks.ToDictionary(block => block.Key, block =>
            NormalizeYamlWhitespace(block.Text).Replace("\r\n", "\n"), StringComparer.Ordinal);
        foreach (var block in left.Blocks)
        {
            if (!rightByKey.TryGetValue(block.Key, out string rightText) ||
                NormalizeYamlWhitespace(block.Text).Replace("\r\n", "\n") != rightText)
                return false;
        }
        return true;
    }

    internal static string CanonicalYamlText(string text)
    {
        return NormalizeYamlWhitespace(text).Replace("\r\n", "\n").Replace('\r', '\n');
    }

    internal static bool TryParseYamlFile(string text, out YamlFile file)
    {
        file = null;
        if (string.IsNullOrEmpty(text))
            return false;

        string lineEnding = text.Contains("\r\n") ? "\r\n" : "\n";
        string normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');
        var matches = Regex.Matches(normalized,
            @"(?m)^--- !u!(?<type>\d+) &(?<id>-?\d+)(?: stripped)?[\t ]*$");
        if (matches.Count == 0)
            return false;

        var blocks = new List<YamlObjectBlock>(matches.Count);
        var keys = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < matches.Count; index++)
        {
            Match match = matches[index];
            int end = index + 1 < matches.Count ? matches[index + 1].Index : normalized.Length;
            string key = match.Groups["type"].Value + ":" + match.Groups["id"].Value;
            if (!keys.Add(key))
                return false;
            blocks.Add(new YamlObjectBlock(key, normalized.Substring(match.Index, end - match.Index)));
        }

        file = new YamlFile(normalized.Substring(0, matches[0].Index), blocks, lineEnding);
        return true;
    }

    internal static string ApplyLineEnding(string text, string lineEnding)
    {
        string normalized = text.Replace("\r\n", "\n").Replace('\r', '\n');
        return lineEnding == "\r\n" ? normalized.Replace("\n", "\r\n") : normalized;
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
        return bytes != null && bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
    }

    private sealed class YamlFile
    {
        public readonly string Preamble;
        public readonly List<YamlObjectBlock> Blocks;
        public readonly string LineEnding;

        public YamlFile(string preamble, List<YamlObjectBlock> blocks, string lineEnding)
        {
            Preamble = preamble;
            Blocks = blocks;
            LineEnding = lineEnding;
        }
    }

    private sealed class YamlObjectBlock
    {
        public readonly string Key;
        public readonly string Text;

        public YamlObjectBlock(string key, string text)
        {
            Key = key;
            Text = text;
        }
    }

    internal static bool RestoreAssetSnapshot(AssetTextSnapshot snapshot)
    {
        if (snapshot == null || !string.IsNullOrEmpty(snapshot.ReadError))
            return false;

        try
        {
            if (!snapshot.Exists)
            {
                if (File.Exists(snapshot.AbsolutePath))
                {
                    AssetDatabase.DeleteAsset(snapshot.AssetPath);
                    return !File.Exists(snapshot.AbsolutePath);
                }
                return true;
            }

            byte[] expectedBytes = snapshot.Bytes ?? Array.Empty<byte>();
            byte[] currentBytes = File.Exists(snapshot.AbsolutePath)
                ? ReadAllBytesWithRetry(snapshot.AbsolutePath)
                : Array.Empty<byte>();
            if (currentBytes.SequenceEqual(expectedBytes))
                return true;

            VmAutomationPersistenceFile.WriteAllBytes(snapshot.AbsolutePath,
                expectedBytes);
            ImportPrefabAssetSynchronously(snapshot.AssetPath);
            byte[] restoredBytes = File.Exists(snapshot.AbsolutePath)
                ? ReadAllBytesWithRetry(snapshot.AbsolutePath)
                : Array.Empty<byte>();
            return restoredBytes.SequenceEqual(expectedBytes);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[VM Unity Automation] Failed to restore prefab asset '{snapshot.AssetPath}': {ex.Message}");
            return false;
        }
    }


    }
}
