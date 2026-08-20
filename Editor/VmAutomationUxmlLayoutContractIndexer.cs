#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using UnityEngine;
using static VMUnityAutomation.Editor.VmAutomationUxmlLayoutAuditor;
using static VMUnityAutomation.Editor.VmAutomationUxmlLayoutModels;

namespace VMUnityAutomation.Editor
{
    internal static class VmAutomationUxmlLayoutContractIndexer
    {
    internal static UxmlElementNameReferenceIndex BuildElementNameReferenceIndex(
        VmAutomationUxmlLayoutAuditReport report, VmAutomationUIToolkitAuditOptions options,
        IEnumerable<string> uxmlPaths)
    {
        var index = new UxmlElementNameReferenceIndex(true);
        var paths = (uxmlPaths ?? Enumerable.Empty<string>()).ToList();

        foreach (var path in paths)
        {
            try
            {
                var document = XDocument.Parse(
                    File.ReadAllText(VmAutomationUIToolkitAuditUtility.ToFullPath(path)),
                    LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo);
                foreach (var element in document.Descendants()
                             .Where(IsAuditableNamedElement))
                {
                    index.AddDefinition(AttributeValue(element, "name"));
                }
            }
            catch (Exception exception)
            {
                report.Errors.Add(
                    $"Failed to index UXML element-name definitions in '{path}': " +
                    exception.Message);
            }
        }

        if (index.DefinitionCount == 0)
        {
            return index;
        }

        foreach (var path in VmAutomationUIToolkitAuditUtility.FindAssetFiles(".uss", options))
        {
            try
            {
                IndexStylesheetNameReferences(
                    File.ReadAllText(VmAutomationUIToolkitAuditUtility.ToFullPath(path)), index);
            }
            catch (Exception exception)
            {
                report.Errors.Add(
                    $"Failed to index USS element-name references in '{path}': " +
                    exception.Message);
            }
        }

        foreach (var path in paths)
        {
            try
            {
                var document = XDocument.Parse(
                    File.ReadAllText(VmAutomationUIToolkitAuditUtility.ToFullPath(path)),
                    LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo);
                IndexUxmlNameReferences(document, index);
            }
            catch (Exception exception)
            {
                report.Errors.Add(
                    $"Failed to index UXML element-name references in '{path}': " +
                    exception.Message);
            }
        }

        foreach (var path in VmAutomationUIToolkitAuditUtility.FindRuntimeSourceFiles(options))
        {
            try
            {
                IndexTextNameReferences(
                    File.ReadAllText(VmAutomationUIToolkitAuditUtility.ToFullPath(path)),
                    index, includeYamlScalars: false);
                report.IndexedRuntimeSourceCount++;
            }
            catch (Exception exception)
            {
                report.Errors.Add(
                    $"Failed to index runtime element-name references in '{path}': " +
                    exception.Message);
            }
        }

        foreach (var extension in new[] { ".prefab", ".asset", ".unity" })
        {
            foreach (var path in VmAutomationUIToolkitAuditUtility.FindAssetFiles(extension, options))
            {
                try
                {
                    IndexTextNameReferences(
                        File.ReadAllText(VmAutomationUIToolkitAuditUtility.ToFullPath(path)),
                        index, includeYamlScalars: true);
                    report.IndexedSerializedAssetCount++;
                }
                catch (Exception exception)
                {
                    report.Errors.Add(
                        $"Failed to index serialized element-name references in '{path}': " +
                        exception.Message);
                }
            }
        }

        return index;
    }

    internal static void IndexStylesheetNameReferences(string text,
        UxmlElementNameReferenceIndex index)
    {
        var sanitized = ussCommentRegex.Replace(text ?? "", "");
        foreach (Match rule in ussRuleRegex.Matches(sanitized))
        {
            foreach (Match match in idTokenRegex.Matches(
                         rule.Groups["selector"].Value))
            {
                index.AddReference(match.Groups["token"].Value);
            }
        }
    }

    internal static void IndexUxmlNameReferences(XDocument document,
        UxmlElementNameReferenceIndex index)
    {
        foreach (var element in document.Descendants())
        {
            foreach (var attribute in element.Attributes())
            {
                if (string.Equals(attribute.Name.LocalName, "name",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                index.AddReference(attribute.Value);
            }
        }
    }

    internal static void IndexTextNameReferences(string text,
        UxmlElementNameReferenceIndex index, bool includeYamlScalars)
    {
        foreach (Match match in quotedNameReferenceRegex.Matches(text ?? ""))
        {
            index.AddReference(match.Groups["token"].Value);
        }

        if (includeYamlScalars == false)
        {
            return;
        }

        foreach (Match match in yamlListNameReferenceRegex.Matches(text ?? "")
                     .Cast<Match>()
                     .Concat(yamlScalarNameReferenceRegex.Matches(text ?? "")
                         .Cast<Match>()))
        {
            index.AddReference(
                match.Groups["token"].Value.Trim().Trim('"', '\''));
        }
    }

    internal static bool IsAuditableNamedElement(XElement element)
    {
        if (element == null ||
            string.Equals(element.Name.LocalName, "UXML",
                StringComparison.OrdinalIgnoreCase) ||
            element.AncestorsAndSelf().Any(IsUxmlMetadataElement))
        {
            return false;
        }

        return string.IsNullOrWhiteSpace(AttributeValue(element, "name")) == false;
    }

    internal static UxmlLayoutContractIndex BuildLayoutContractIndex(
        VmAutomationUxmlLayoutAuditReport report, VmAutomationUIToolkitAuditOptions options,
        IEnumerable<string> uxmlPaths)
    {
        var index = new UxmlLayoutContractIndex();
        foreach (var path in VmAutomationUIToolkitAuditUtility.FindAssetFiles(".uss", options))
        {
            try
            {
                IndexStyleSheetText(
                    File.ReadAllText(VmAutomationUIToolkitAuditUtility.ToFullPath(path)), index);
                report.IndexedStyleSheetCount++;
            }
            catch (Exception exception)
            {
                report.Errors.Add($"Failed to index USS box contracts in '{path}': {exception.Message}");
            }
        }

        foreach (var path in uxmlPaths ?? Enumerable.Empty<string>())
        {
            try
            {
                var document = XDocument.Parse(
                    File.ReadAllText(VmAutomationUIToolkitAuditUtility.ToFullPath(path)),
                    LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo);
                IndexUxmlDocument(document, index);
            }
            catch (Exception exception)
            {
                report.Errors.Add($"Failed to index UXML layout variants in '{path}': " +
                                  exception.Message);
            }
        }

        return index;
    }

    internal static void IndexStyleSheetText(string text, UxmlLayoutContractIndex index)
    {
        var sanitized = ussCommentRegex.Replace(text ?? "", "");
        foreach (Match rule in ussRuleRegex.Matches(sanitized))
        {
            var declarations = ParseStyle(rule.Groups["body"].Value);
            var selector = rule.Groups["selector"].Value;
            var classNames = classTokenRegex.Matches(selector)
                .Cast<Match>()
                .Select(match => match.Groups["token"].Value)
                .Distinct(StringComparer.Ordinal)
                .ToList();
            foreach (var className in classNames)
            {
                foreach (var declaration in declarations.Where(declaration =>
                             IsVariantLayoutProperty(declaration.Key)))
                {
                    index.AddClassLayoutProperty(className, declaration.Key);
                }
            }

            if (declarations.Any(property =>
                    IsBoxContractProperty(property.Key, property.Value)))
            {
                foreach (var className in classNames)
                {
                    index.BoxClasses.Add(className);
                }

                foreach (Match match in idTokenRegex.Matches(selector))
                {
                    index.BoxIds.Add(match.Groups["token"].Value);
                }
            }
        }
    }

    internal static void IndexUxmlDocument(XDocument document,
        UxmlLayoutContractIndex index)
    {
        foreach (var element in document.Descendants())
        {
            var classNames = SplitWhitespace(AttributeValue(element, "class"))
                .Distinct(StringComparer.Ordinal)
                .ToList();
            foreach (var baseClass in classNames)
            {
                foreach (var variantClass in classNames.Where(candidate =>
                             candidate.StartsWith(baseClass + "-",
                                 StringComparison.Ordinal)))
                {
                    index.AddAuthoredVariant(baseClass, variantClass);
                }
            }
        }
    }

    internal static UxmlInlineStyleContractIndex BuildInlineStyleContractIndex(
        string uxmlAssetPath, XDocument document, VmAutomationUxmlLayoutAuditReport report)
    {
        var index = new UxmlInlineStyleContractIndex();
        var indexedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var styleElement in document.Descendants()
                     .Where(element => element.Name.LocalName == "Style"))
        {
            var stylePath = ResolveStyleReference(
                AttributeValue(styleElement, "src"), uxmlAssetPath);
            if (string.IsNullOrWhiteSpace(stylePath) ||
                indexedPaths.Add(stylePath) == false)
            {
                continue;
            }

            try
            {
                var fullPath = VmAutomationUIToolkitAuditUtility.ToFullPath(stylePath);
                if (File.Exists(fullPath) == false)
                {
                    report.Errors.Add(
                        $"Referenced USS asset does not exist: {stylePath} " +
                        $"(from {uxmlAssetPath}).");
                    continue;
                }

                IndexInlineStyleSheetText(stylePath, File.ReadAllText(fullPath), index);
            }
            catch (Exception exception)
            {
                report.Errors.Add(
                    $"Failed to index referenced USS defaults in '{stylePath}': " +
                    exception.Message);
            }
        }

        return index;
    }

    internal static string ResolveStyleReference(string rawPath, string uxmlAssetPath)
    {
        if (string.IsNullOrWhiteSpace(rawPath))
        {
            return "";
        }

        var path = rawPath.Trim().Replace('\\', '/');
        var queryIndex = path.IndexOf('?');
        if (queryIndex >= 0)
        {
            path = path.Substring(0, queryIndex);
        }

        var fragmentIndex = path.IndexOf('#');
        if (fragmentIndex >= 0)
        {
            path = path.Substring(0, fragmentIndex);
        }

        const string projectPrefix = "project://database/";
        if (path.StartsWith(projectPrefix, StringComparison.OrdinalIgnoreCase))
        {
            path = path.Substring(projectPrefix.Length);
        }

        path = Uri.UnescapeDataString(path);
        if (Path.IsPathRooted(path))
        {
            return VmAutomationUIToolkitAuditUtility.ToAssetPath(path);
        }

        if (path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith("Packages/", StringComparison.OrdinalIgnoreCase))
        {
            return VmAutomationUIToolkitAuditUtility.NormalizeAssetPath(path);
        }

        var ownerDirectory = Path.GetDirectoryName(uxmlAssetPath) ?? "";
        var combined = Path.Combine(ownerDirectory,
            path.Replace('/', Path.DirectorySeparatorChar));
        return VmAutomationUIToolkitAuditUtility.ToAssetPath(
            VmAutomationUIToolkitAuditUtility.ToFullPath(combined));
    }

    internal static void IndexInlineStyleSheetText(string sourcePath, string text,
        UxmlInlineStyleContractIndex index)
    {
        var sanitized = ussCommentRegex.Replace(text ?? "", "");
        foreach (Match rule in ussRuleRegex.Matches(sanitized))
        {
            var declarations = ParseStyle(rule.Groups["body"].Value);
            if (declarations.Count == 0)
            {
                continue;
            }

            foreach (var rawSelector in rule.Groups["selector"].Value.Split(','))
            {
                if (TryParseSimpleSelector(rawSelector, out var selector) == false)
                {
                    continue;
                }

                index.AddRule(sourcePath, selector, declarations);
            }
        }
    }

    internal static bool TryParseSimpleSelector(string rawSelector,
        out UxmlSimpleSelector selector)
    {
        selector = null;
        var value = (rawSelector ?? "").Trim();
        var match = Regex.Match(value,
            @"^(?<type>[A-Za-z_][A-Za-z0-9_-]*)?" +
            @"(?<tokens>(?:[.#][A-Za-z_][A-Za-z0-9_-]*)*)$");
        if (match.Success == false || value.Length == 0)
        {
            return false;
        }

        var classNames = classTokenRegex.Matches(value)
            .Cast<Match>()
            .Select(item => item.Groups["token"].Value)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var ids = idTokenRegex.Matches(value)
            .Cast<Match>()
            .Select(item => item.Groups["token"].Value)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (ids.Count > 1)
        {
            return false;
        }

        var typeName = match.Groups["type"].Value;
        if (string.IsNullOrWhiteSpace(typeName) &&
            classNames.Count == 0 &&
            ids.Count == 0)
        {
            return false;
        }

        selector = new UxmlSimpleSelector
        {
            Text = value,
            TypeName = typeName,
            Id = ids.SingleOrDefault() ?? "",
            Specificity = ids.Count * 100 + classNames.Count * 10 +
                          (string.IsNullOrWhiteSpace(typeName) ? 0 : 1)
        };
        selector.ClassNames.AddRange(classNames);
        return true;
    }


    }
}
#endif
