#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Linq;
using UssRule = VMUnityAutomation.Editor.MCPUssAuditContext.UssRule;
using UssAuthoredElement = VMUnityAutomation.Editor.MCPUssAuditContext.UssAuthoredElement;
using UssUsageIndex = VMUnityAutomation.Editor.MCPUssAuditContext.UssUsageIndex;
using static VMUnityAutomation.Editor.MCPUssCascadeAuditor;
using static VMUnityAutomation.Editor.MCPUssStyleSheetParser;

namespace VMUnityAutomation.Editor
{
    internal static class MCPUssVariantFamilyContract
    {
        internal static HashSet<string> FindContractTokens(
            IReadOnlyList<UssRule> rules, UssUsageIndex usageIndex)
        {
            var definitions = rules
                .Select(rule => TryCreateDefinition(rule, usageIndex,
                    out var definition) ? definition : null)
                .Where(definition => definition != null)
                .GroupBy(definition => definition.Token, StringComparer.Ordinal)
                .Where(group => group.Count() == 1)
                .Select(group => group.Single())
                .ToList();
            var result = new HashSet<string>(StringComparer.Ordinal);

            for (var leftIndex = 0; leftIndex < definitions.Count; leftIndex++)
            {
                var left = definitions[leftIndex];
                for (var rightIndex = leftIndex + 1;
                     rightIndex < definitions.Count; rightIndex++)
                {
                    var right = definitions[rightIndex];
                    if (string.Equals(left.FamilyStem, right.FamilyStem,
                            StringComparison.Ordinal) == false ||
                        string.Equals(left.Rule.AssetPath, right.Rule.AssetPath,
                            StringComparison.OrdinalIgnoreCase) == false ||
                        HasSymmetricDeclarations(left.Rule, right.Rule) == false ||
                        HasAuthoredBasePair(left, right) == false)
                    {
                        continue;
                    }

                    result.Add(left.Token);
                    result.Add(right.Token);
                }
            }

            return result;
        }

        private static bool TryCreateDefinition(UssRule rule,
            UssUsageIndex usageIndex, out VariantDefinition definition)
        {
            definition = null;
            if (rule.Declarations.Count == 0 ||
                TryGetSingleClassToken(rule, out var token) == false ||
                TryGetFamilyStem(token, out var familyStem) == false)
            {
                return false;
            }

            var elements = usageIndex.Documents
                .SelectMany(document => document.Elements)
                .Where(element => element.AuthoredClasses.Contains(token))
                .Distinct()
                .ToList();
            if (elements.Count == 0)
            {
                return false;
            }

            definition = new VariantDefinition(rule, token, familyStem, elements);
            return true;
        }

        private static bool TryGetSingleClassToken(UssRule rule, out string token)
        {
            token = "";
            if (rule.Selectors.Count != 1 ||
                TryParseSimpleSelector(rule.Selectors[0], out var selector) == false ||
                string.IsNullOrWhiteSpace(selector.TypeName) == false ||
                string.IsNullOrWhiteSpace(selector.Id) == false ||
                selector.ClassNames.Count != 1)
            {
                return false;
            }

            token = selector.ClassNames[0];
            return true;
        }

        private static bool TryGetFamilyStem(string token, out string familyStem)
        {
            familyStem = "";
            var separatorIndex = token.LastIndexOf('-');
            if (separatorIndex <= 0 || separatorIndex >= token.Length - 1)
            {
                return false;
            }

            familyStem = token.Substring(0, separatorIndex);
            return true;
        }

        private static bool HasSymmetricDeclarations(UssRule left, UssRule right)
        {
            if (left.Declarations.Count == 0 ||
                left.Declarations.Count != right.Declarations.Count)
            {
                return false;
            }

            var hasDistinctValue = false;
            foreach (var declaration in left.Declarations)
            {
                if (right.Declarations.TryGetValue(declaration.Key,
                        out var rightValue) == false)
                {
                    return false;
                }

                if (StyleValuesEqual(declaration.Value, rightValue) == false)
                {
                    hasDistinctValue = true;
                }
            }

            return hasDistinctValue;
        }

        private static bool HasAuthoredBasePair(VariantDefinition left,
            VariantDefinition right)
        {
            foreach (var leftElement in left.Elements)
            {
                foreach (var rightElement in right.Elements)
                {
                    if (ReferenceEquals(leftElement, rightElement) ||
                        string.Equals(leftElement.ComponentTypeName,
                            rightElement.ComponentTypeName,
                            StringComparison.Ordinal) == false)
                    {
                        continue;
                    }

                    if (leftElement.AuthoredClasses
                        .Intersect(rightElement.AuthoredClasses,
                            StringComparer.Ordinal)
                        .Any(baseToken => IsSharedBaseToken(baseToken,
                            left.Token, right.Token)))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool IsSharedBaseToken(string baseToken, string leftToken,
            string rightToken)
        {
            if (string.Equals(baseToken, leftToken, StringComparison.Ordinal) ||
                string.Equals(baseToken, rightToken, StringComparison.Ordinal))
            {
                return false;
            }

            var prefix = baseToken + "-";
            return leftToken.StartsWith(prefix, StringComparison.Ordinal) &&
                   rightToken.StartsWith(prefix, StringComparison.Ordinal);
        }

        internal static IReadOnlyList<Dictionary<string, object>> RunSelfTests()
        {
            var cases = new List<Dictionary<string, object>>();
            AddCase(cases, "symmetric authored variant family passes",
                HasContract(
                    ".connector-vertical-normal { width: 9px; height: 45px; }\n" +
                    ".connector-vertical-highlighted { width: 6px; height: 45px; }\n",
                    "<ui:VisualElement class=\"connector connector-vertical-normal\"/>" +
                    "<ui:VisualElement class=\"connector connector-vertical-highlighted\"/>",
                    "connector-vertical-normal"));
            AddCase(cases, "lone modifier still warns",
                HasContract(
                    ".connector-vertical-normal { width: 9px; height: 45px; }\n",
                    "<ui:VisualElement class=\"connector connector-vertical-normal\"/>",
                    "connector-vertical-normal") == false);
            AddCase(cases, "mismatched declaration shape still warns",
                HasContract(
                    ".connector-vertical-normal { width: 9px; height: 45px; }\n" +
                    ".connector-vertical-highlighted { width: 6px; }\n",
                    "<ui:VisualElement class=\"connector connector-vertical-normal\"/>" +
                    "<ui:VisualElement class=\"connector connector-vertical-highlighted\"/>",
                    "connector-vertical-normal") == false);
            AddCase(cases, "missing authored base still warns",
                HasContract(
                    ".connector-vertical-normal { width: 9px; height: 45px; }\n" +
                    ".connector-vertical-highlighted { width: 6px; height: 45px; }\n",
                    "<ui:VisualElement class=\"connector-vertical-normal\"/>" +
                    "<ui:VisualElement class=\"connector-vertical-highlighted\"/>",
                    "connector-vertical-normal") == false);
            AddCase(cases, "duplicate-value modifiers still warn",
                HasContract(
                    ".connector-vertical-normal { width: 9px; height: 45px; }\n" +
                    ".connector-vertical-highlighted { width: 9px; height: 45px; }\n",
                    "<ui:VisualElement class=\"connector connector-vertical-normal\"/>" +
                    "<ui:VisualElement class=\"connector connector-vertical-highlighted\"/>",
                    "connector-vertical-normal") == false);
            return cases;
        }

        private static bool HasContract(string ussBody, string uxmlBody,
            string token)
        {
            const string ussPath = "Assets/__VariantFamilySelfTest.uss";
            var rules = ParseStyleSheet(ussPath, ussBody);
            var usageIndex = new UssUsageIndex();
            usageIndex.Documents.Add(new MCPUssAuditContext.UssAuthoredDocument(
                "Assets/__VariantFamilySelfTest.uxml",
                XDocument.Parse(
                    "<ui:UXML xmlns:ui=\"UnityEngine.UIElements\">" +
                    uxmlBody + "</ui:UXML>", LoadOptions.SetLineInfo)));
            return FindContractTokens(rules, usageIndex).Contains(token);
        }

        private static void AddCase(ICollection<Dictionary<string, object>> cases,
            string name, bool passed)
        {
            cases.Add(new Dictionary<string, object>
            {
                { "name", name },
                { "passed", passed }
            });
        }

        private sealed class VariantDefinition
        {
            public readonly UssRule Rule;
            public readonly string Token;
            public readonly string FamilyStem;
            public readonly IReadOnlyList<UssAuthoredElement> Elements;

            public VariantDefinition(UssRule rule, string token, string familyStem,
                IReadOnlyList<UssAuthoredElement> elements)
            {
                Rule = rule;
                Token = token;
                FamilyStem = familyStem;
                Elements = elements;
            }
        }
    }
}
#endif
