#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using UssRule = VMUnityAutomation.Editor.VmAutomationUssAuditContext.UssRule;

namespace VMUnityAutomation.Editor
{
    internal static class VmAutomationUssStyleSheetParser
    {
        private static readonly Regex commentRegex =
            new Regex(@"/\*.*?\*/", RegexOptions.Compiled | RegexOptions.Singleline);

        private static readonly Regex suppressionRegex =
            new Regex(@"/\*\s*uss-audit:\s*allow-single-use\s+(?<reason>.+?)\*/\s*$",
                RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

        private static readonly Regex redundantDeclarationSuppressionRegex =
            new Regex(@"/\*\s*uss-audit:\s*allow-redundant-declaration\s+(?<reason>.+?)\*/\s*$",
                RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

        private static readonly Regex ancestorDefaultResetSuppressionRegex =
            new Regex(@"/\*\s*uss-audit:\s*allow-ancestor-default-reset\s+(?<reason>.+?)\*/\s*$",
                RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

        private static readonly Regex pixelGridSuppressionRegex =
            new Regex(@"/\*\s*uss-audit:\s*allow-off-grid-pixels\s+(?<reason>.+?)\*/\s*$",
                RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

        private static readonly Regex textStyleContractSuppressionRegex =
            new Regex(@"/\*\s*uss-audit:\s*allow-text-style-contract\s+(?<reason>.+?)\*/\s*$",
                RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

        private static readonly Regex importRegex =
            new Regex(@"@import\s+url\(\s*(?:[""'](?<quoted>[^""']+)[""']|(?<plain>[^)\s]+))\s*\)\s*;",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        internal static string ResolveStyleReference(string rawPath, string ownerAssetPath)
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

            if (path.StartsWith("unity-theme://", StringComparison.OrdinalIgnoreCase))
            {
                return "";
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

            var ownerDirectory = Path.GetDirectoryName(ownerAssetPath) ?? "";
            var combined = Path.Combine(ownerDirectory,
                path.Replace('/', Path.DirectorySeparatorChar));
            return VmAutomationUIToolkitAuditUtility.ToAssetPath(
                VmAutomationUIToolkitAuditUtility.ToFullPath(combined));
        }

        internal static IEnumerable<string> GetImportedStylePaths(string ownerPath, string text)
        {
            foreach (Match match in importRegex.Matches(commentRegex.Replace(text ?? "", "")))
            {
                var rawPath = match.Groups["quoted"].Success
                    ? match.Groups["quoted"].Value
                    : match.Groups["plain"].Value;
                var resolved = ResolveStyleReference(rawPath, ownerPath);
                if (string.IsNullOrWhiteSpace(resolved) == false)
                {
                    yield return resolved;
                }
            }
        }

        internal static List<UssRule> ParseStyleSheet(string assetPath, string text)
        {
            var sanitized = commentRegex.Replace(text, match =>
                new string(match.Value.Select(character => character == '\n' || character == '\r'
                    ? character
                    : ' ').ToArray()));
            var rules = new List<UssRule>();
            var cursor = 0;

            while (cursor < sanitized.Length)
            {
                var openBrace = sanitized.IndexOf('{', cursor);
                if (openBrace < 0)
                {
                    break;
                }

                var closeBrace = sanitized.IndexOf('}', openBrace + 1);
                if (closeBrace < 0)
                {
                    break;
                }

                var sanitizedHeader = sanitized.Substring(cursor, openBrace - cursor);
                var originalHeader = text.Substring(cursor, openBrace - cursor);
                var lastSemicolon = sanitizedHeader.LastIndexOf(';');
                var selectorOffset = lastSemicolon >= 0 ? lastSemicolon + 1 : 0;
                var selectorGroup = sanitizedHeader.Substring(selectorOffset).Trim();
                if (string.IsNullOrEmpty(selectorGroup) == false && selectorGroup.StartsWith("@") == false)
                {
                    var leadingLength = sanitizedHeader.Substring(selectorOffset)
                        .TakeWhile(char.IsWhiteSpace).Count();
                    var selectorIndex = cursor + selectorOffset + leadingLength;
                    var suppressionContext = originalHeader.Substring(0, selectorOffset + leadingLength);
                    var suppression = suppressionRegex.Match(suppressionContext);
                    var redundantSuppression =
                        redundantDeclarationSuppressionRegex.Match(suppressionContext);
                    var ancestorDefaultResetSuppression =
                        ancestorDefaultResetSuppressionRegex.Match(suppressionContext);
                    var pixelGridSuppression =
                        pixelGridSuppressionRegex.Match(suppressionContext);
                    var textStyleContractSuppression =
                        textStyleContractSuppressionRegex.Match(suppressionContext);
                    rules.Add(new UssRule
                    {
                        AssetPath = assetPath,
                        Line = GetLineNumber(text, selectorIndex),
                        Selectors = SplitSelectors(selectorGroup),
                        Declarations = ParseDeclarations(
                            text.Substring(openBrace + 1, closeBrace - openBrace - 1)),
                        SuppressionReason = suppression.Success
                            ? suppression.Groups["reason"].Value.Trim()
                            : "",
                        RedundantDeclarationSuppressionReason =
                            redundantSuppression.Success
                                ? redundantSuppression.Groups["reason"].Value.Trim()
                                : "",
                        AncestorDefaultResetSuppressionReason =
                            ancestorDefaultResetSuppression.Success
                                ? ancestorDefaultResetSuppression.Groups["reason"].Value.Trim()
                                : "",
                        PixelGridSuppressionReason =
                            pixelGridSuppression.Success
                                ? pixelGridSuppression.Groups["reason"].Value.Trim()
                                : "",
                        TextStyleContractSuppressionReason =
                            textStyleContractSuppression.Success
                                ? textStyleContractSuppression.Groups["reason"].Value.Trim()
                                : ""
                    });
                }

                cursor = closeBrace + 1;
            }

            return rules;
        }

        internal static Dictionary<string, string> ParseDeclarations(string body)
        {
            var declarations =
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            body = commentRegex.Replace(body ?? "", "");
            var start = 0;
            var parentheses = 0;
            var quote = '\0';

            for (var index = 0; index <= body.Length; index++)
            {
                var character = index < body.Length ? body[index] : ';';
                if (quote != '\0')
                {
                    if (character == quote && (index == 0 || body[index - 1] != '\\'))
                    {
                        quote = '\0';
                    }

                    continue;
                }

                if (character == '"' || character == '\'')
                {
                    quote = character;
                    continue;
                }

                if (character == '(')
                {
                    parentheses++;
                    continue;
                }

                if (character == ')')
                {
                    parentheses = Math.Max(0, parentheses - 1);
                    continue;
                }

                if (character != ';' || parentheses != 0)
                {
                    continue;
                }

                var declaration = body.Substring(start, index - start).Trim();
                start = index + 1;
                var colon = declaration.IndexOf(':');
                if (colon <= 0)
                {
                    continue;
                }

                var property = declaration.Substring(0, colon).Trim();
                var value = declaration.Substring(colon + 1).Trim();
                if (property.Length > 0 && value.Length > 0)
                {
                    declarations[property] = value;
                }
            }

            return declarations;
        }

        private static List<string> SplitSelectors(string selectorGroup)
        {
            var selectors = new List<string>();
            var start = 0;
            var parentheses = 0;
            var brackets = 0;
            for (var index = 0; index < selectorGroup.Length; index++)
            {
                switch (selectorGroup[index])
                {
                    case '(':
                        parentheses++;
                        break;
                    case ')':
                        parentheses = Math.Max(0, parentheses - 1);
                        break;
                    case '[':
                        brackets++;
                        break;
                    case ']':
                        brackets = Math.Max(0, brackets - 1);
                        break;
                    case ',' when parentheses == 0 && brackets == 0:
                        AddSelector(selectors, selectorGroup.Substring(start, index - start));
                        start = index + 1;
                        break;
                }
            }

            AddSelector(selectors, selectorGroup.Substring(start));
            return selectors;
        }

        private static void AddSelector(ICollection<string> selectors, string selector)
        {
            selector = Regex.Replace(selector ?? "", @"\s+", " ").Trim();
            if (string.IsNullOrEmpty(selector) == false)
            {
                selectors.Add(selector);
            }
        }

        internal static IEnumerable<string> SplitWhitespace(string value)
        {
            return (value ?? "").Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
        }

        internal static XAttribute GetAttribute(XElement element, string name)
        {
            return element.Attributes().FirstOrDefault(attribute =>
                string.Equals(attribute.Name.LocalName, name,
                    StringComparison.OrdinalIgnoreCase));
        }

        internal static string GetAttributeValue(XElement element, string name)
        {
            XAttribute attribute = GetAttribute(element, name);
            return attribute != null ? attribute.Value.Trim() : "";
        }

        internal static int GetLineNumber(string text, int characterIndex)
        {
            var line = 1;
            var length = Math.Min(Math.Max(characterIndex, 0), text?.Length ?? 0);
            for (var index = 0; index < length; index++)
            {
                if (text[index] == '\n')
                {
                    line++;
                }
            }

            return line;
        }

        internal static int GetColumnNumber(string text, int characterIndex)
        {
            int index = Math.Min(Math.Max(characterIndex, 0), text?.Length ?? 0);
            int previousLineBreak = index > 0 ? text.LastIndexOf('\n', index - 1) : -1;
            return index - previousLineBreak;
        }

        internal static int GetLineNumber(XObject value)
        {
            var lineInfo = value as IXmlLineInfo;
            return lineInfo != null && lineInfo.HasLineInfo() ? lineInfo.LineNumber : 1;
        }

        internal static int GetColumnNumber(XObject value)
        {
            var lineInfo = value as IXmlLineInfo;
            return lineInfo != null && lineInfo.HasLineInfo() ? lineInfo.LinePosition : 1;
        }
    }
}
#endif
