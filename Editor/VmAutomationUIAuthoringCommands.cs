using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using UnityEditor;

namespace VMUnityAutomation.Editor
{
    public static class VmAutomationUIAuthoringCommands
    {
        private static readonly Regex UssBlockRegex = new Regex(
            @"(?<selector>[^{}]+)\{(?<body>[^{}]*)\}", RegexOptions.Singleline | RegexOptions.Compiled);

        public static object EditUxml(Dictionary<string, object> args)
        {
            string assetPath = NormalizeAssetPath(GetString(args, "assetPath"));
            var operations = GetDictionaryList(args, "operations");
            bool dryRun = GetBool(args, "dryRun", false);
            if (!TryGetAbsoluteAssetFile(assetPath, ".uxml", out string absolutePath, out string error))
                return VmAutomationResponse.Error(error, "invalid_uxml_path");
            if (operations.Count == 0)
                return VmAutomationResponse.Error("operations is required.", "invalid_arguments");

            string original = File.ReadAllText(absolutePath);
            XDocument document;
            try { document = XDocument.Parse(original, LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo); }
            catch (Exception exception)
            {
                return VmAutomationResponse.Error($"Failed to parse UXML: {exception.Message}", "uxml_parse_failed");
            }

            var results = new List<object>();
            try
            {
                foreach (var operation in operations)
                    results.Add(ApplyUxmlOperation(document, operation));
            }
            catch (Exception exception)
            {
                return VmAutomationResponse.Error(exception.Message, "uxml_edit_failed", false,
                    new Dictionary<string, object> { { "completedOperationCount", results.Count } });
            }

            string updated = document.ToString(SaveOptions.DisableFormatting);
            if (!updated.EndsWith("\n", StringComparison.Ordinal)) updated += "\n";
            if (!dryRun && !string.Equals(updated, original, StringComparison.Ordinal))
            {
                WriteTextAtomically(absolutePath, updated);
                RefreshAsset(assetPath);
            }

            return new Dictionary<string, object>
            {
                { "success", true }, { "assetPath", assetPath }, { "dryRun", dryRun },
                { "changed", !string.Equals(updated, original, StringComparison.Ordinal) },
                { "operationCount", operations.Count }, { "results", results },
            };
        }

        public static object EditUss(Dictionary<string, object> args)
        {
            string assetPath = NormalizeAssetPath(GetString(args, "assetPath"));
            var operations = GetDictionaryList(args, "operations");
            bool dryRun = GetBool(args, "dryRun", false);
            if (!TryGetAbsoluteAssetFile(assetPath, ".uss", out string absolutePath, out string error))
                return VmAutomationResponse.Error(error, "invalid_uss_path");
            if (operations.Count == 0)
                return VmAutomationResponse.Error("operations is required.", "invalid_arguments");

            string original = File.ReadAllText(absolutePath);
            string updated = original;
            var results = new List<object>();
            try
            {
                foreach (var operation in operations)
                {
                    string before = updated;
                    updated = ApplyUssOperation(updated, operation);
                    results.Add(new Dictionary<string, object>
                    {
                        { "type", GetString(operation, "type") ?? "" },
                        { "selector", GetString(operation, "selector") ?? "" },
                        { "changed", !string.Equals(before, updated, StringComparison.Ordinal) },
                    });
                }
            }
            catch (Exception exception)
            {
                return VmAutomationResponse.Error(exception.Message, "uss_edit_failed", false,
                    new Dictionary<string, object> { { "completedOperationCount", results.Count } });
            }

            updated = updated.TrimEnd() + "\n";
            if (!dryRun && !string.Equals(updated, original, StringComparison.Ordinal))
            {
                WriteTextAtomically(absolutePath, updated);
                RefreshAsset(assetPath);
            }

            return new Dictionary<string, object>
            {
                { "success", true }, { "assetPath", assetPath }, { "dryRun", dryRun },
                { "changed", !string.Equals(updated, original, StringComparison.Ordinal) },
                { "operationCount", operations.Count }, { "results", results },
            };
        }

        public static object AuthoringTransaction(Dictionary<string, object> args)
        {
            var edits = GetDictionaryList(args, "edits");
            bool dryRun = GetBool(args, "dryRun", false);
            if (edits.Count == 0)
                return VmAutomationResponse.Error("edits is required.", "invalid_arguments");

            string transactionId = Guid.NewGuid().ToString("N").Substring(0, 12);
            var snapshots = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var results = new List<object>();
            try
            {
                foreach (var edit in edits)
                {
                    string kind = (GetString(edit, "kind") ?? "").ToLowerInvariant();
                    string assetPath = NormalizeAssetPath(GetString(edit, "assetPath"));
                    string extension = kind == "uxml" ? ".uxml" : kind == "uss" ? ".uss" : null;
                    if (extension == null)
                        throw new InvalidOperationException("Each edit kind must be uxml or uss.");
                    if (!TryGetAbsoluteAssetFile(assetPath, extension, out string absolutePath, out string error))
                        throw new InvalidOperationException(error);
                    if (!snapshots.ContainsKey(assetPath))
                        snapshots[assetPath] = File.ReadAllText(absolutePath);

                    var editArgs = new Dictionary<string, object>(edit)
                    {
                        ["assetPath"] = assetPath,
                        ["dryRun"] = dryRun,
                    };
                    object result = kind == "uxml" ? EditUxml(editArgs) : EditUss(editArgs);
                    if (VmAutomationResponse.TryGetError(result, out string message, out _, out _))
                        throw new InvalidOperationException(message);
                    results.Add(result);
                }

                if (!dryRun)
                    RefreshAssets(snapshots.Keys);
                return new Dictionary<string, object>
                {
                    { "success", true }, { "transactionId", transactionId }, { "dryRun", dryRun },
                    { "editCount", edits.Count }, { "assetCount", snapshots.Count }, { "results", results },
                    { "rolledBack", false },
                };
            }
            catch (Exception exception)
            {
                var rollbackErrors = new List<string>();
                if (!dryRun)
                {
                    foreach (var snapshot in snapshots)
                    {
                        try
                        {
                            string absolutePath = ToAbsolutePath(snapshot.Key);
                            WriteTextAtomically(absolutePath, snapshot.Value);
                        }
                        catch (Exception rollbackException) { rollbackErrors.Add(rollbackException.Message); }
                    }
                    RefreshAssets(snapshots.Keys);
                }
                return VmAutomationResponse.Error(exception.Message, "uitoolkit_authoring_transaction_failed", false,
                    new Dictionary<string, object>
                    {
                        { "transactionId", transactionId }, { "rolledBack", rollbackErrors.Count == 0 },
                        { "rollbackErrors", rollbackErrors }, { "completedEditCount", results.Count },
                        { "results", results },
                    });
            }
        }

        private static object ApplyUxmlOperation(XDocument document, Dictionary<string, object> operation)
        {
            string type = (GetString(operation, "type") ?? "").ToLowerInvariant();
            XElement element;
            switch (type)
            {
                case "add-element":
                {
                    XElement parent = ResolveElement(document, GetString(operation, "parentPath"),
                        GetString(operation, "parentName"));
                    string elementType = GetString(operation, "elementType") ?? "VisualElement";
                    XName elementName = ResolveElementName(document.Root, elementType);
                    var added = new XElement(elementName);
                    var attributes = GetDictionary(operation, "attributes");
                    if (attributes != null)
                    {
                        foreach (var attribute in attributes)
                            if (attribute.Value != null) added.SetAttributeValue(attribute.Key, attribute.Value.ToString());
                    }
                    int index = GetInt(operation, "index", -1);
                    var children = parent.Elements().ToList();
                    if (index < 0 || index >= children.Count) parent.Add(added);
                    else children[index].AddBeforeSelf(added);
                    return DescribeElement(added, type);
                }
                case "remove-element":
                    element = ResolveElement(document, GetString(operation, "path"), GetString(operation, "name"));
                    if (element == document.Root) throw new InvalidOperationException("The UXML root cannot be removed.");
                    var removed = DescribeElement(element, type);
                    element.Remove();
                    return removed;
                case "move-element":
                    element = ResolveElement(document, GetString(operation, "path"), GetString(operation, "name"));
                    if (element == document.Root) throw new InvalidOperationException("The UXML root cannot be moved.");
                    XElement newParent = ResolveElement(document, GetString(operation, "parentPath"),
                        GetString(operation, "parentName"));
                    if (element.AncestorsAndSelf().Contains(newParent))
                        throw new InvalidOperationException("An element cannot be moved under itself.");
                    element.Remove();
                    int moveIndex = GetInt(operation, "index", -1);
                    var siblings = newParent.Elements().ToList();
                    if (moveIndex < 0 || moveIndex >= siblings.Count) newParent.Add(element);
                    else siblings[moveIndex].AddBeforeSelf(element);
                    return DescribeElement(element, type);
                case "set-attribute":
                    element = ResolveElement(document, GetString(operation, "path"), GetString(operation, "name"));
                    string attributeName = Required(operation, "attribute");
                    element.SetAttributeValue(attributeName, GetString(operation, "value") ?? "");
                    return DescribeElement(element, type);
                case "remove-attribute":
                    element = ResolveElement(document, GetString(operation, "path"), GetString(operation, "name"));
                    element.Attribute(Required(operation, "attribute"))?.Remove();
                    return DescribeElement(element, type);
                case "add-class":
                case "remove-class":
                    element = ResolveElement(document, GetString(operation, "path"), GetString(operation, "name"));
                    string className = Required(operation, "className");
                    var classes = new HashSet<string>(((string)element.Attribute("class") ?? "")
                        .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries), StringComparer.Ordinal);
                    if (type == "add-class") classes.Add(className); else classes.Remove(className);
                    element.SetAttributeValue("class", string.Join(" ", classes.OrderBy(item => item)));
                    return DescribeElement(element, type);
                case "set-text":
                    element = ResolveElement(document, GetString(operation, "path"), GetString(operation, "name"));
                    element.SetAttributeValue("text", GetString(operation, "text") ?? "");
                    return DescribeElement(element, type);
                default:
                    throw new InvalidOperationException($"Unsupported UXML operation '{type}'.");
            }
        }

        private static string ApplyUssOperation(string source, Dictionary<string, object> operation)
        {
            string type = (GetString(operation, "type") ?? "").ToLowerInvariant();
            string selector = Required(operation, "selector").Trim();
            var match = FindUssBlock(source, selector);
            if (type == "remove-selector")
            {
                if (match == null) throw new InvalidOperationException($"USS selector was not found: '{selector}'.");
                return source.Remove(match.Index, match.Length).TrimEnd();
            }

            var declarations = match == null
                ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                : ParseDeclarations(match.Groups["body"].Value);
            if (type == "upsert-selector")
            {
                var supplied = GetDictionary(operation, "declarations");
                if (supplied == null) throw new InvalidOperationException("declarations is required.");
                declarations.Clear();
                foreach (var declaration in supplied)
                    if (declaration.Value != null) declarations[declaration.Key] = declaration.Value.ToString();
            }
            else if (type == "set-declaration")
            {
                declarations[Required(operation, "property")] = GetString(operation, "value") ?? "";
            }
            else if (type == "remove-declaration")
            {
                declarations.Remove(Required(operation, "property"));
            }
            else
            {
                throw new InvalidOperationException($"Unsupported USS operation '{type}'.");
            }

            string block = FormatUssBlock(selector, declarations);
            if (match == null)
                return source.TrimEnd() + "\n\n" + block;
            return source.Substring(0, match.Index) + block + source.Substring(match.Index + match.Length);
        }

        private static Match FindUssBlock(string source, string selector)
        {
            return UssBlockRegex.Matches(source).Cast<Match>()
                .FirstOrDefault(match => string.Equals(CleanSelector(match.Groups["selector"].Value), selector,
                    StringComparison.Ordinal));
        }

        private static string CleanSelector(string raw)
        {
            string withoutComments = Regex.Replace(raw ?? "", @"/\*.*?\*/", "", RegexOptions.Singleline);
            int newline = Math.Max(withoutComments.LastIndexOf('\n'), withoutComments.LastIndexOf('\r'));
            return (newline >= 0 ? withoutComments.Substring(newline + 1) : withoutComments).Trim();
        }

        private static Dictionary<string, string> ParseDeclarations(string body)
        {
            var declarations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string item in (body ?? "").Split(';'))
            {
                int separator = item.IndexOf(':');
                if (separator <= 0) continue;
                string property = item.Substring(0, separator).Trim();
                string value = item.Substring(separator + 1).Trim();
                if (!string.IsNullOrEmpty(property)) declarations[property] = value;
            }
            return declarations;
        }

        private static string FormatUssBlock(string selector, Dictionary<string, string> declarations)
        {
            var builder = new StringBuilder();
            builder.Append(selector).Append(" {\n");
            foreach (var declaration in declarations.OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase))
                builder.Append("    ").Append(declaration.Key).Append(": ").Append(declaration.Value).Append(";\n");
            return builder.Append('}').ToString();
        }

        private static XElement ResolveElement(XDocument document, string path, string name)
        {
            if (document?.Root == null) throw new InvalidOperationException("UXML root was not found.");
            if (!string.IsNullOrEmpty(name))
            {
                var matches = document.DescendantsAndSelf()
                    .Where(element => string.Equals((string)element.Attribute("name"), name,
                        StringComparison.Ordinal)).ToList();
                if (matches.Count != 1)
                    throw new InvalidOperationException(
                        $"Expected exactly one UXML element named '{name}', found {matches.Count}.");
                return matches[0];
            }

            if (string.IsNullOrEmpty(path) || path == "root") return document.Root;
            string[] segments = path.Split('/');
            int start = segments.Length > 0 && segments[0] == "root" ? 1 : 0;
            XElement current = document.Root;
            for (int index = start; index < segments.Length; index++)
            {
                if (!int.TryParse(segments[index], out int childIndex))
                    throw new InvalidOperationException($"Invalid VisualElementPath segment '{segments[index]}'.");
                var children = current.Elements().ToList();
                if (childIndex < 0 || childIndex >= children.Count)
                    throw new InvalidOperationException($"VisualElementPath '{path}' was not found.");
                current = children[childIndex];
            }
            return current;
        }

        private static IEnumerable<XElement> DescendantsAndSelf(this XDocument document)
        {
            return document.Root == null ? Enumerable.Empty<XElement>() : document.Root.DescendantsAndSelf();
        }

        private static XName ResolveElementName(XElement root, string elementType)
        {
            int separator = elementType.IndexOf(':');
            if (separator < 0)
            {
                XNamespace defaultUi = root.GetNamespaceOfPrefix("ui") ?? root.Name.Namespace;
                return defaultUi + elementType;
            }
            string prefix = elementType.Substring(0, separator);
            string localName = elementType.Substring(separator + 1);
            XNamespace ns = root.GetNamespaceOfPrefix(prefix);
            if (ns == null) throw new InvalidOperationException($"UXML namespace prefix '{prefix}' was not found.");
            return ns + localName;
        }

        private static Dictionary<string, object> DescribeElement(XElement element, string operation)
        {
            return new Dictionary<string, object>
            {
                { "operation", operation }, { "type", element.Name.LocalName },
                { "name", (string)element.Attribute("name") ?? "" },
                { "classes", ((string)element.Attribute("class") ?? "")
                    .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).ToList() },
            };
        }

        private static bool TryGetAbsoluteAssetFile(string assetPath, string extension, out string absolutePath,
            out string error)
        {
            absolutePath = null;
            if (string.IsNullOrEmpty(assetPath) || !assetPath.StartsWith("Assets/", StringComparison.Ordinal) ||
                !assetPath.EndsWith(extension, StringComparison.OrdinalIgnoreCase))
            {
                error = $"Asset path must point to an {extension} file below Assets/.";
                return false;
            }
            absolutePath = ToAbsolutePath(assetPath);
            if (!File.Exists(absolutePath))
            {
                error = $"Asset file was not found: '{assetPath}'.";
                return false;
            }
            error = null;
            return true;
        }

        private static void RefreshAsset(string assetPath)
        {
            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport |
                                                 ImportAssetOptions.ForceUpdate);
        }

        private static void RefreshAssets(IEnumerable<string> assetPaths)
        {
            foreach (string assetPath in assetPaths.Distinct(StringComparer.OrdinalIgnoreCase))
                RefreshAsset(assetPath);
            AssetDatabase.SaveAssets();
        }

        private static void WriteTextAtomically(string path, string contents)
        {
            string tempPath = path + ".unity-mcp.tmp";
            File.WriteAllText(tempPath, contents, new UTF8Encoding(false));
            if (File.Exists(path))
            {
                string backupPath = path + ".unity-mcp.bak";
                try { File.Replace(tempPath, path, backupPath, true); }
                catch (PlatformNotSupportedException)
                {
                    File.Copy(tempPath, path, true);
                    File.Delete(tempPath);
                }
                if (File.Exists(backupPath)) File.Delete(backupPath);
            }
            else File.Move(tempPath, path);
        }

        private static string Required(Dictionary<string, object> args, string key)
        {
            string value = GetString(args, key);
            if (string.IsNullOrWhiteSpace(value)) throw new InvalidOperationException($"{key} is required.");
            return value;
        }

        private static string ToAbsolutePath(string assetPath)
        {
            string projectRoot = Directory.GetParent(UnityEngine.Application.dataPath).FullName;
            return Path.Combine(projectRoot, assetPath.Replace('/', Path.DirectorySeparatorChar));
        }

        private static string NormalizeAssetPath(string path)
        {
            return (path ?? "").Trim().Replace('\\', '/');
        }

        private static string GetString(Dictionary<string, object> args, string key)
        {
            return args != null && args.TryGetValue(key, out object value) && value != null
                ? value.ToString()
                : null;
        }

        private static bool GetBool(Dictionary<string, object> args, string key, bool defaultValue)
        {
            if (args == null || !args.TryGetValue(key, out object value) || value == null) return defaultValue;
            return value is bool boolValue ? boolValue : bool.TryParse(value.ToString(), out bool parsed) && parsed;
        }

        private static int GetInt(Dictionary<string, object> args, string key, int defaultValue)
        {
            if (args == null || !args.TryGetValue(key, out object value) || value == null) return defaultValue;
            return int.TryParse(value.ToString(), out int parsed) ? parsed : defaultValue;
        }

        private static Dictionary<string, object> GetDictionary(Dictionary<string, object> args, string key)
        {
            return args != null && args.TryGetValue(key, out object value)
                ? VmAutomationResponse.ToDictionary(value)
                : null;
        }

        private static List<Dictionary<string, object>> GetDictionaryList(Dictionary<string, object> args,
            string key)
        {
            if (args == null || !args.TryGetValue(key, out object value) || !(value is IList list))
                return new List<Dictionary<string, object>>();
            return list.Cast<object>().Select(VmAutomationResponse.ToDictionary).Where(item => item != null).ToList();
        }
    }
}
