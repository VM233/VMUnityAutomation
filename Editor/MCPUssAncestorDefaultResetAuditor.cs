#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using UssRule = VMUnityAutomation.Editor.MCPUssAuditContext.UssRule;
using UssSimpleSelector = VMUnityAutomation.Editor.MCPUssAuditContext.UssSimpleSelector;
using UssAuthoredElement = VMUnityAutomation.Editor.MCPUssAuditContext.UssAuthoredElement;
using UssAuthoredDocument = VMUnityAutomation.Editor.MCPUssAuditContext.UssAuthoredDocument;
using UssResolvedDeclaration = VMUnityAutomation.Editor.MCPUssAuditContext.UssResolvedDeclaration;
using UssCascadeRule = VMUnityAutomation.Editor.MCPUssAuditContext.UssCascadeRule;
using UssCascadeDocument = VMUnityAutomation.Editor.MCPUssAuditContext.UssCascadeDocument;
using UssCascadeIndex = VMUnityAutomation.Editor.MCPUssAuditContext.UssCascadeIndex;
using UssUsageIndex = VMUnityAutomation.Editor.MCPUssAuditContext.UssUsageIndex;
using static VMUnityAutomation.Editor.MCPUssCascadeAuditor;
using static VMUnityAutomation.Editor.MCPUssStyleAuditor;
using static VMUnityAutomation.Editor.MCPUssStyleSheetParser;

namespace VMUnityAutomation.Editor
{
    internal static class MCPUssAncestorDefaultResetAuditor
    {
        private const string KIND = "overbroad-ancestor-default-reset";

        internal static void Audit(IReadOnlyList<UssRule> rules,
            UssUsageIndex usageIndex, UssCascadeIndex cascadeIndex,
            MCPUssStyleAuditReport report, bool includeSuppressed)
        {
            foreach (var broadRule in rules)
            {
                foreach (var broadSelectorText in broadRule.Selectors)
                {
                    if (IsDynamicStateSelector(broadSelectorText) ||
                        SelectorHasRuntimeClassContract(broadSelectorText, usageIndex) ||
                        TryParseSimpleSelector(broadSelectorText,
                            out var broadSelector) == false)
                    {
                        continue;
                    }

                    foreach (var declaration in broadRule.Declarations)
                    {
                        if (MCPUIToolkitInitialStyleComparer.IsInitialValue(
                                declaration.Key, declaration.Value))
                        {
                            continue;
                        }

                        AuditDeclaration(broadRule, broadSelectorText, broadSelector,
                            declaration.Key, declaration.Value, usageIndex,
                            cascadeIndex, report, includeSuppressed);
                    }
                }
            }
        }

        private static void AuditDeclaration(UssRule broadRule,
            string broadSelectorText, UssSimpleSelector broadSelector,
            string property, string value, UssUsageIndex usageIndex,
            UssCascadeIndex cascadeIndex,
            MCPUssStyleAuditReport report, bool includeSuppressed)
        {
            var effectiveUsages = new List<ElementUsage>();
            var resetUsages = new List<ElementUsage>();
            var resetDeclarations = new List<UssResolvedDeclaration>();

            foreach (var document in cascadeIndex.Documents.Where(document =>
                         document.LoadedAssetPaths.Contains(broadRule.AssetPath)))
            {
                foreach (var element in document.AuthoredDocument.Elements.Where(
                             broadSelector.Matches))
                {
                    if (element.InlineDeclarations.ContainsKey(property))
                    {
                        continue;
                    }

                    var ownership = ResolveOwnership(document, element, broadRule,
                        broadSelector, property, usageIndex,
                        out var resetDeclaration);
                    if (ownership == DeclarationOwnership.Uncertain)
                    {
                        return;
                    }

                    if (ownership == DeclarationOwnership.Broad)
                    {
                        effectiveUsages.Add(new ElementUsage(document, element));
                    }
                    else if (ownership == DeclarationOwnership.DefaultReset)
                    {
                        resetUsages.Add(new ElementUsage(document, element));
                        resetDeclarations.Add(resetDeclaration);
                    }
                }
            }

            if (effectiveUsages.Count == 0 || resetUsages.Count == 0)
            {
                return;
            }

            var resetRules = resetDeclarations
                .GroupBy(declaration =>
                        $"{declaration.Rule.AssetPath}\n{declaration.Rule.Line}\n" +
                        declaration.SelectorText,
                    StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .OrderBy(declaration => declaration.Rule.AssetPath,
                    StringComparer.Ordinal)
                .ThenBy(declaration => declaration.Rule.Line)
                .ThenBy(declaration => declaration.SelectorText,
                    StringComparer.Ordinal)
                .ToList();
            var usageLocations = effectiveUsages
                .Select(usage => usage.ToDictionary("non-default-consumer"))
                .Concat(resetUsages.Select(usage =>
                    usage.ToDictionary("ancestor-default-reset")))
                .Take(20)
                .ToList();
            var issue = new MCPUssStyleAuditIssue
            {
                AssetPath = broadRule.AssetPath,
                Line = broadRule.Line,
                Selector = broadSelectorText,
                Token = property,
                Kind = KIND,
                Property = property,
                Value = value,
                AuthoredUsageCount = effectiveUsages.Count + resetUsages.Count,
                UsageLocations = usageLocations,
                StylesheetRules = resetRules.Select(reset =>
                    new Dictionary<string, object>
                    {
                        { "property", property },
                        { "value", reset.Value },
                        { "selector", reset.SelectorText },
                        { "sourcePath", reset.Rule.AssetPath },
                        { "line", reset.Rule.Line },
                        { "sourceKind", "ancestor-default-reset" }
                    }).ToList(),
                Suppressed = string.IsNullOrWhiteSpace(
                    broadRule.AncestorDefaultResetSuppressionReason) == false,
                SuppressionReason = broadRule.AncestorDefaultResetSuppressionReason,
                Message =
                    $"Declaration '{property}: {value}' in broad selector " +
                    $"'{broadSelectorText}' is effective for {effectiveUsages.Count} authored " +
                    $"element(s), while {resetUsages.Count} element(s) under ancestor-scoped " +
                    $"branches only reset the same property to its Unity engine initial " +
                    "value. Scope the non-default declaration to the branch that " +
                    "actually consumes it and remove the ancestor default reset."
            };
            report.Record(issue, includeSuppressed);
        }

        private static DeclarationOwnership ResolveOwnership(
            UssCascadeDocument document, UssAuthoredElement element,
            UssRule broadRule, UssSimpleSelector broadSelector, string property,
            UssUsageIndex usageIndex, out UssResolvedDeclaration resetDeclaration)
        {
            resetDeclaration = null;
            var winner = document.Resolve(element, property, null);
            var winnerIsAncestorScoped = false;

            foreach (var contextualRule in document.Rules)
            {
                if (contextualRule.Selector != null ||
                    contextualRule.Rule.Declarations.TryGetValue(property,
                        out var scopedValue) == false ||
                    IsDynamicStateSelector(contextualRule.SelectorText))
                {
                    continue;
                }

                if (TryParseAncestorScopedSelector(contextualRule.SelectorText,
                        out var scopedSelector) == false)
                {
                    if (PotentiallyTargets(contextualRule.SelectorText,
                            broadSelector.Text))
                    {
                        return DeclarationOwnership.Uncertain;
                    }

                    continue;
                }

                if (SameSimpleSelector(scopedSelector.Target, broadSelector) == false ||
                    scopedSelector.Matches(element) == false)
                {
                    continue;
                }

                if (SelectorHasRuntimeClassContract(contextualRule.SelectorText,
                        usageIndex))
                {
                    return DeclarationOwnership.Uncertain;
                }

                var scopedDeclaration = new UssResolvedDeclaration
                {
                    Rule = contextualRule.Rule,
                    SelectorText = contextualRule.SelectorText,
                    Value = scopedValue,
                    Origin = contextualRule.Origin,
                    Specificity = scopedSelector.Specificity,
                    SourceOrder = contextualRule.SourceOrder
                };
                if (HasHigherOrEqualPriority(scopedDeclaration, winner))
                {
                    winner = scopedDeclaration;
                    winnerIsAncestorScoped = true;
                }
            }

            if (winner == null)
            {
                return DeclarationOwnership.Other;
            }

            if (winnerIsAncestorScoped)
            {
                if (MCPUIToolkitInitialStyleComparer.IsInitialValue(
                        property, winner.Value))
                {
                    resetDeclaration = winner;
                    return DeclarationOwnership.DefaultReset;
                }

                return DeclarationOwnership.Other;
            }

            return ReferenceEquals(winner.Rule, broadRule)
                ? DeclarationOwnership.Broad
                : DeclarationOwnership.Other;
        }

        private static bool HasHigherOrEqualPriority(UssResolvedDeclaration candidate,
            UssResolvedDeclaration current)
        {
            if (current == null)
            {
                return true;
            }

            if (candidate.Origin != current.Origin)
            {
                return candidate.Origin > current.Origin;
            }

            if (candidate.Specificity != current.Specificity)
            {
                return candidate.Specificity > current.Specificity;
            }

            return candidate.SourceOrder >= current.SourceOrder;
        }

        private static bool TryParseAncestorScopedSelector(string selectorText,
            out AncestorScopedSelector selector)
        {
            selector = null;
            var value = Regex.Replace((selectorText ?? "").Trim(), @"\s+", " ");
            if (value.Length == 0 || IsDynamicStateSelector(value))
            {
                return false;
            }

            string ancestorText;
            string targetText;
            var directChild = value.IndexOf('>') >= 0;
            if (directChild)
            {
                var parts = value.Split('>');
                if (parts.Length != 2)
                {
                    return false;
                }

                ancestorText = parts[0].Trim();
                targetText = parts[1].Trim();
            }
            else
            {
                var parts = value.Split(new[] { ' ' },
                    StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length != 2)
                {
                    return false;
                }

                ancestorText = parts[0];
                targetText = parts[1];
            }

            if (TryParseSimpleSelector(ancestorText, out var ancestor) == false ||
                TryParseSimpleSelector(targetText, out var target) == false)
            {
                return false;
            }

            selector = new AncestorScopedSelector(ancestor, target, directChild);
            return true;
        }

        private static bool SameSimpleSelector(UssSimpleSelector left,
            UssSimpleSelector right)
        {
            return string.Equals(left.TypeName, right.TypeName,
                       StringComparison.Ordinal) &&
                   string.Equals(left.Id, right.Id, StringComparison.Ordinal) &&
                   left.ClassNames.OrderBy(value => value, StringComparer.Ordinal)
                       .SequenceEqual(right.ClassNames.OrderBy(value => value,
                           StringComparer.Ordinal));
        }

        private static bool PotentiallyTargets(string selectorText,
            string targetSelectorText)
        {
            var selector = (selectorText ?? "").Trim();
            var target = (targetSelectorText ?? "").Trim();
            return target.Length > 0 && Regex.IsMatch(selector,
                $@"(?:^|[\s>+~]){Regex.Escape(target)}\s*$");
        }

        internal static IReadOnlyList<Dictionary<string, object>> RunSelfTests()
        {
            var cases = new List<Dictionary<string, object>>();
            var direct = AuditFixture(
                ".connector { position: absolute; }\n" +
                ".passive > .connector { position: relative; }\n",
                "<ui:VisualElement name=\"Active\"><ui:VisualElement class=\"connector\"/>" +
                "</ui:VisualElement><ui:VisualElement class=\"passive\">" +
                "<ui:VisualElement class=\"connector\"/></ui:VisualElement>");
            AddCase(cases, "ancestor direct-child default reset warns",
                HasActiveFinding(direct));

            var descendant = AuditFixture(
                ".connector { position: absolute; }\n" +
                ".passive .connector { position: relative; }\n",
                "<ui:VisualElement><ui:VisualElement class=\"connector\"/>" +
                "</ui:VisualElement><ui:VisualElement class=\"passive\">" +
                "<ui:VisualElement><ui:VisualElement class=\"connector\"/>" +
                "</ui:VisualElement></ui:VisualElement>");
            AddCase(cases, "ancestor descendant default reset warns",
                HasActiveFinding(descendant));

            var modifier = AuditFixture(
                ".node { flex-direction: row; }\n" +
                ".node.vertical { flex-direction: column; }\n",
                "<ui:VisualElement class=\"node\"/>" +
                "<ui:VisualElement class=\"node vertical\"/>");
            AddCase(cases, "same-element modifier reset passes",
                HasAnyFinding(modifier) == false);

            var dynamicState = AuditFixture(
                ".node { position: absolute; }\n" +
                ".node:hover { position: relative; }\n",
                "<ui:VisualElement class=\"node\"/>" +
                "<ui:VisualElement class=\"node\"/>");
            AddCase(cases, "dynamic state reset passes",
                HasAnyFinding(dynamicState) == false);

            var nonDefault = AuditFixture(
                ".node { flex-direction: row; }\n" +
                ".passive > .node { flex-direction: row-reverse; }\n",
                "<ui:VisualElement><ui:VisualElement class=\"node\"/>" +
                "</ui:VisualElement><ui:VisualElement class=\"passive\">" +
                "<ui:VisualElement class=\"node\"/></ui:VisualElement>");
            AddCase(cases, "ancestor non-default override passes",
                HasAnyFinding(nonDefault) == false);

            var singleBranch = AuditFixture(
                ".connector { position: absolute; }\n" +
                ".passive > .connector { position: relative; }\n",
                "<ui:VisualElement class=\"passive\">" +
                "<ui:VisualElement class=\"connector\"/></ui:VisualElement>");
            AddCase(cases, "reset-only consumer set passes",
                HasAnyFinding(singleBranch) == false);

            var suppressed = AuditFixture(
                "/* uss-audit: allow-ancestor-default-reset fixture owns a shared reset */\n" +
                ".connector { position: absolute; }\n" +
                ".passive > .connector { position: relative; }\n",
                "<ui:VisualElement><ui:VisualElement class=\"connector\"/>" +
                "</ui:VisualElement><ui:VisualElement class=\"passive\">" +
                "<ui:VisualElement class=\"connector\"/></ui:VisualElement>");
            AddCase(cases, "reasoned ancestor reset suppression is retained",
                suppressed.Issues.Count == 1 && suppressed.Issues[0].Suppressed);
            return cases;
        }

        private static MCPUssStyleAuditReport AuditFixture(string ussBody,
            string uxmlBody)
        {
            const string ussPath = "Assets/__AncestorDefaultResetSelfTest.uss";
            var rules = ParseStyleSheet(ussPath, ussBody);
            var usageIndex = new UssUsageIndex();
            var document = new UssAuthoredDocument(
                "Assets/__AncestorDefaultResetSelfTest.uxml",
                XDocument.Parse(
                    "<ui:UXML xmlns:ui=\"UnityEngine.UIElements\">" +
                    uxmlBody + "</ui:UXML>", LoadOptions.SetLineInfo));
            usageIndex.Documents.Add(document);
            var cascadeDocument = new UssCascadeDocument(document);
            cascadeDocument.LoadedAssetPaths.Add(ussPath);
            foreach (var rule in rules)
            {
                foreach (var selectorText in rule.Selectors)
                {
                    TryParseSimpleSelector(selectorText, out var selector);
                    cascadeDocument.Rules.Add(new UssCascadeRule
                    {
                        Rule = rule,
                        SelectorText = selectorText,
                        Selector = selector,
                        Origin = 1,
                        SourceOrder = cascadeDocument.NextSourceOrder()
                    });
                }
            }

            var cascadeIndex = new UssCascadeIndex();
            cascadeIndex.Documents.Add(cascadeDocument);
            var report = new MCPUssStyleAuditReport(100);
            Audit(rules, usageIndex, cascadeIndex, report, true);
            return report;
        }

        private static bool HasActiveFinding(MCPUssStyleAuditReport report)
        {
            return report.Issues.Any(issue =>
                issue.Kind == KIND && issue.Suppressed == false);
        }

        private static bool HasAnyFinding(MCPUssStyleAuditReport report)
        {
            return report.Issues.Any(issue => issue.Kind == KIND);
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

        private enum DeclarationOwnership
        {
            Other,
            Broad,
            DefaultReset,
            Uncertain
        }

        private sealed class AncestorScopedSelector
        {
            private readonly UssSimpleSelector ancestor;
            private readonly bool directChild;

            public readonly UssSimpleSelector Target;
            public int Specificity => ancestor.Specificity + Target.Specificity;

            public AncestorScopedSelector(UssSimpleSelector ancestor,
                UssSimpleSelector target, bool directChild)
            {
                this.ancestor = ancestor;
                Target = target;
                this.directChild = directChild;
            }

            public bool Matches(UssAuthoredElement element)
            {
                if (Target.Matches(element) == false)
                {
                    return false;
                }

                if (directChild)
                {
                    return element.Parent != null && ancestor.Matches(element.Parent);
                }

                for (var current = element.Parent; current != null;
                     current = current.Parent)
                {
                    if (ancestor.Matches(current))
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        private sealed class ElementUsage
        {
            private readonly UssCascadeDocument document;
            private readonly UssAuthoredElement element;

            public ElementUsage(UssCascadeDocument document,
                UssAuthoredElement element)
            {
                this.document = document;
                this.element = element;
            }

            public Dictionary<string, object> ToDictionary(string usageKind)
            {
                return new Dictionary<string, object>
                {
                    { "path", document.AuthoredDocument.AssetPath },
                    { "line", element.Line },
                    { "column", element.Column },
                    { "usageKind", usageKind }
                };
            }
        }
    }
}
#endif
