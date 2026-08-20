#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using static VMUnityAutomation.Editor.MCPUssAuditContext;

namespace VMUnityAutomation.Editor
{
    internal static class MCPUssRuntimeClassReferenceClassifier
    {
        private static readonly Regex stringConstantRegex = new Regex(
            @"\b(?:const|static\s+readonly)\s+string\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\s*=\s*""(?<token>[A-Za-z_][A-Za-z0-9_-]*)""",
            RegexOptions.Compiled);

        private static readonly Regex classApiCallRegex = new Regex(
            @"(?<api>AddToClassList|RemoveFromClassList|EnableInClassList|ClassListContains|classList\s*\.\s*(?:Add|Remove|Contains))\s*\(\s*(?:""(?<literal>[A-Za-z_][A-Za-z0-9_-]*)""|(?<identifier>[A-Za-z_][A-Za-z0-9_]*))",
            RegexOptions.Compiled);

        internal static void Index(string path, string text, UssUsageIndex index)
        {
            var constants = stringConstantRegex.Matches(text).Cast<Match>()
                .GroupBy(match => match.Groups["name"].Value, StringComparer.Ordinal)
                .ToDictionary(group => group.Key,
                    group => group.Last().Groups["token"].Value,
                    StringComparer.Ordinal);
            var classifiedTokens = new HashSet<string>(StringComparer.Ordinal);

            foreach (Match match in classApiCallRegex.Matches(text))
            {
                var token = match.Groups["literal"].Success
                    ? match.Groups["literal"].Value
                    : ResolveConstant(constants, match.Groups["identifier"].Value);
                if (string.IsNullOrWhiteSpace(token) ||
                    index.AllClassTokens.Contains(token) == false)
                {
                    continue;
                }

                var line = MCPUssStyleSheetParser.GetLineNumber(text, match.Index);
                var column = MCPUssStyleSheetParser.GetColumnNumber(text, match.Index);
                if (IsStaticAssignment(match.Groups["api"].Value))
                {
                    index.AddRuntimeClassAssignment(token, path, line, column);
                }
                else
                {
                    index.AddRuntimeClassSemanticReference(token, path, line, column);
                }

                classifiedTokens.Add(token);
            }

            foreach (var token in index.AllClassTokens)
            {
                if (classifiedTokens.Contains(token))
                {
                    continue;
                }

                var unresolvedReference = index.GetRuntimeClassReferences(token)
                    .FirstOrDefault(location => string.Equals(location.Path, path,
                        StringComparison.OrdinalIgnoreCase));
                if (string.IsNullOrWhiteSpace(unresolvedReference.Path) == false)
                {
                    index.AddRuntimeClassSemanticReference(token, path,
                        unresolvedReference.Line, unresolvedReference.Column);
                }
            }
        }

        private static string ResolveConstant(
            IReadOnlyDictionary<string, string> constants, string identifier)
        {
            return string.IsNullOrWhiteSpace(identifier) == false &&
                   constants.TryGetValue(identifier, out var token)
                ? token
                : "";
        }

        private static bool IsStaticAssignment(string api)
        {
            return string.Equals(api, "AddToClassList", StringComparison.Ordinal) ||
                   Regex.IsMatch(api, @"\bclassList\s*\.\s*Add\b");
        }
    }
}
#endif
