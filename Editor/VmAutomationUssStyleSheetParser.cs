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
            foreach (Match match in importRegex.Matches(MaskComments(text ?? "")))
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
            var sanitized = MaskComments(text);
            var rules = new List<UssRule>();
            var cursor = 0;
            var lineCursor = 0;
            var lineNumber = 1;

            while (cursor < sanitized.Length)
            {
                var openBrace = FindStructuralCharacter(sanitized, '{', cursor);
                if (openBrace < 0)
                {
                    break;
                }

                var closeBrace = FindStructuralCharacter(sanitized, '}', openBrace + 1);
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
                    while (lineCursor < selectorIndex)
                        if (text[lineCursor++] == '\n') lineNumber++;
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
                        SourceStart = selectorIndex,
                        SourceLength = closeBrace + 1 - selectorIndex,
                        BodyStart = openBrace + 1,
                        BodyLength = closeBrace - openBrace - 1,
                        Line = lineNumber,
                        SelectorGroup = Regex.Replace(selectorGroup, @"\s+", " ").Trim(),
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
            foreach (var declaration in ParseDeclarationRanges(body))
                declarations[declaration.Property] = declaration.Value;
            return declarations;
        }

        internal readonly struct DeclarationRange
        {
            public readonly string Property, Value;
            public readonly int Start, Length, ValueStart, ValueLength;

            public DeclarationRange(string property, string value, int start, int length,
                int valueStart, int valueLength)
            {
                Property = property; Value = value; Start = start; Length = length;
                ValueStart = valueStart; ValueLength = valueLength;
            }
        }

        internal static List<DeclarationRange> ParseDeclarationRanges(string body)
        {
            body = MaskComments(body ?? "");
            var declarations = new List<DeclarationRange>();
            var start = 0;
            var parentheses = 0;
            var quote = '\0';

            for (var index = 0; index <= body.Length; index++)
            {
                var character = index < body.Length ? body[index] : ';';
                if (quote != '\0')
                {
                    if (character == '\\' && index + 1 < body.Length)
                        index++;
                    else if (character == quote)
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

                var declarationStart = start;
                while (declarationStart < index && char.IsWhiteSpace(body[declarationStart]))
                    declarationStart++;
                var declaration = body.Substring(declarationStart, index - declarationStart);
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
                    int valueStart = declarationStart + colon + 1;
                    while (valueStart < index && char.IsWhiteSpace(body[valueStart])) valueStart++;
                    int valueEnd = index;
                    while (valueEnd > valueStart && char.IsWhiteSpace(body[valueEnd - 1])) valueEnd--;
                    declarations.Add(new DeclarationRange(property, value, declarationStart,
                        Math.Min(index + 1, body.Length) - declarationStart, valueStart, valueEnd - valueStart));
                }
            }

            return declarations;
        }

        private static string MaskComments(string text)
        {
            var masked = text.ToCharArray();
            char quote = '\0';
            for (int index = 0; index < text.Length; index++)
            {
                char character = text[index];
                if (quote != '\0')
                {
                    if (character == '\\' && index + 1 < text.Length) index++;
                    else if (character == quote) quote = '\0';
                }
                else if (character == '"' || character == '\'') quote = character;
                else if (character == '/' && index + 1 < text.Length && text[index + 1] == '*')
                {
                    int end = text.IndexOf("*/", index + 2, StringComparison.Ordinal);
                    if (end < 0) throw new FormatException("USS comment has no closing delimiter.");
                    for (int offset = index; offset < end + 2; offset++)
                        if (text[offset] != '\n' && text[offset] != '\r') masked[offset] = ' ';
                    index = end + 1;
                }
            }
            return new string(masked);
        }

        private static int FindStructuralCharacter(string source, char target, int start)
        {
            char quote = '\0';
            for (int index = start; index < source.Length; index++)
            {
                char character = source[index];
                if (quote != '\0')
                {
                    if (character == '\\' && index + 1 < source.Length) index++;
                    else if (character == quote) quote = '\0';
                }
                else if (character == '"' || character == '\'') quote = character;
                else if (character == target) return index;
            }
            return -1;
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
