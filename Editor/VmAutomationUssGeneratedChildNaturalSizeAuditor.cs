#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using UssAuthoredDocument = VMUnityAutomation.Editor.VmAutomationUssAuditContext.UssAuthoredDocument;
using UssAuthoredElement = VMUnityAutomation.Editor.VmAutomationUssAuditContext.UssAuthoredElement;
using UssCascadeDocument = VMUnityAutomation.Editor.VmAutomationUssAuditContext.UssCascadeDocument;
using UssCascadeIndex = VMUnityAutomation.Editor.VmAutomationUssAuditContext.UssCascadeIndex;
using UssCascadeRule = VMUnityAutomation.Editor.VmAutomationUssAuditContext.UssCascadeRule;
using UssRule = VMUnityAutomation.Editor.VmAutomationUssAuditContext.UssRule;
using UssStaticSelector = VMUnityAutomation.Editor.VmAutomationUssAuditContext.UssStaticSelector;
using UssUsageIndex = VMUnityAutomation.Editor.VmAutomationUssAuditContext.UssUsageIndex;
using static VMUnityAutomation.Editor.VmAutomationUssCascadeAuditor;
using static VMUnityAutomation.Editor.VmAutomationUssStyleSheetParser;

namespace VMUnityAutomation.Editor
{
    internal static class VmAutomationUssGeneratedChildNaturalSizeAuditor
    {
        internal const string KIND = "redundant-generated-child-cross-size";

        private const float SizeEpsilon = 0.01f;

        private static readonly Regex PixelValueRegex = new Regex(
            @"^(?<value>-?(?:\d+(?:\.\d+)?|\.\d+))px$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        internal static void Audit(IReadOnlyList<UssRule> rules,
            UssUsageIndex usageIndex, UssCascadeIndex cascadeIndex,
            VmAutomationUssStyleAuditReport report, bool includeSuppressed)
        {
            foreach (var rule in rules)
            {
                foreach (var selectorText in rule.Selectors)
                {
                    if (TryParseStaticSelector(selectorText,
                            out var parentSelector) == false)
                    {
                        continue;
                    }

                    AuditProperty(rule, selectorText, parentSelector, "width",
                        usageIndex, cascadeIndex, report, includeSuppressed);
                    AuditProperty(rule, selectorText, parentSelector, "height",
                        usageIndex, cascadeIndex, report, includeSuppressed);
                }
            }
        }

        internal static IReadOnlyList<Dictionary<string, object>> RunSelfTests()
        {
            var cases = new List<Dictionary<string, object>>();

            var rowHeight = AuditFixture(
                ".number { flex-direction: row; }\n" +
                ".rank { position: absolute; height: 12px; }\n" +
                ".rank > .glyph { width: 8px; height: 12px; }\n",
                "<ui:VisualElement class=\"number rank\"/>", "glyph");
            AddCase(cases, "generated row child makes matching parent height redundant",
                HasActiveFinding(rowHeight, "height"));

            var rowWidth = AuditFixture(
                ".number { flex-direction: row; }\n" +
                ".rank { position: absolute; width: 8px; }\n" +
                ".rank > .glyph { width: 8px; height: 12px; }\n",
                "<ui:VisualElement class=\"number rank\"/>", "glyph");
            AddCase(cases, "generated row child does not own parent main-axis width",
                HasActiveFinding(rowWidth, "width") == false);

            var columnWidth = AuditFixture(
                ".number { flex-direction: column; }\n" +
                ".rank { position: absolute; width: 8px; }\n" +
                ".rank > .glyph { width: 8px; height: 12px; }\n",
                "<ui:VisualElement class=\"number rank\"/>", "glyph");
            AddCase(cases, "generated column child makes matching parent width redundant",
                HasActiveFinding(columnWidth, "width"));

            var visualOwner = AuditFixture(
                ".number { flex-direction: row; }\n" +
                ".rank { height: 12px; background-image: url(\"panel.png\"); }\n" +
                ".rank > .glyph { height: 12px; }\n",
                "<ui:VisualElement class=\"number rank\"/>", "glyph");
            AddCase(cases, "visual parent retains matching cross size",
                HasActiveFinding(visualOwner, "height") == false);

            var unprovenGeneratedChild = AuditFixture(
                ".number { flex-direction: row; }\n" +
                ".rank { height: 12px; }\n" +
                ".rank > .glyph { height: 12px; }\n",
                "<ui:VisualElement class=\"number rank\"/>", "");
            AddCase(cases, "unproven generated child does not warn",
                HasActiveFinding(unprovenGeneratedChild, "height") == false);

            var suppressed = AuditFixture(
                ".number { flex-direction: row; }\n" +
                "/* uss-audit: allow-redundant-declaration fixture owns a measured hit region */\n" +
                ".rank { height: 12px; }\n" +
                ".rank > .glyph { height: 12px; }\n",
                "<ui:VisualElement class=\"number rank\"/>", "glyph", true);
            AddCase(cases, "reasoned generated-child size suppression is retained",
                suppressed.WarningCount == 0 && suppressed.SuppressedCount == 1 &&
                suppressed.Issues.Single(issue => issue.Kind == KIND).Suppressed);

            return cases;
        }

        private static void AuditProperty(UssRule rule, string selectorText,
            UssStaticSelector parentSelector, string property,
            UssUsageIndex usageIndex,
            UssCascadeIndex cascadeIndex, VmAutomationUssStyleAuditReport report,
            bool includeSuppressed)
        {
            if (TryGetPositivePixels(rule.Declarations, property,
                    out var parentSize) == false)
            {
                return;
            }

            var usages = new List<Dictionary<string, object>>();
            var childRules = new Dictionary<string, GeneratedChildContract>(
                StringComparer.Ordinal);
            var runtimeAssignments = new Dictionary<string, UssUsageLocation>(
                StringComparer.OrdinalIgnoreCase);

            foreach (var document in cascadeIndex.Documents.Where(document =>
                         document.LoadedAssetPaths.Contains(rule.AssetPath)))
            {
                foreach (var parent in document.AuthoredDocument.Elements.Where(
                             parentSelector.Matches))
                {
                    if (parent.Children.Count != 0 ||
                        IsWinningDeclaration(document, parent, property, rule,
                            selectorText, parentSize) == false ||
                        IsCrossAxisProperty(document, parent, property) == false ||
                        HasIndependentCrossSizeContract(document, parent, property))
                    {
                        continue;
                    }

                    var generatedChildren = FindGeneratedChildContracts(document,
                        parent, property, usageIndex).ToList();
                    if (generatedChildren.Count == 0 ||
                        generatedChildren.Any(contract =>
                            Math.Abs(contract.Size - parentSize) > SizeEpsilon))
                    {
                        continue;
                    }

                    usages.Add(new Dictionary<string, object>
                    {
                        { "path", document.AuthoredDocument.AssetPath },
                        { "line", parent.Line },
                        { "column", parent.Column }
                    });
                    foreach (var contract in generatedChildren)
                    {
                        childRules[contract.Key] = contract;
                        foreach (var className in contract.RuntimeAssignedClasses)
                        {
                            foreach (var location in usageIndex
                                         .GetRuntimeClassAssignments(className))
                            {
                                runtimeAssignments[
                                    $"{location.Path}:{location.Line}:{location.Column}"] =
                                    location;
                            }
                        }
                    }
                }
            }

            if (usages.Count == 0)
            {
                return;
            }

            var childSelectorLabels = childRules.Values
                .OrderBy(contract => contract.Rule.AssetPath, StringComparer.Ordinal)
                .ThenBy(contract => contract.Rule.Line)
                .ThenBy(contract => contract.SelectorText, StringComparer.Ordinal)
                .Select(contract => $"'{contract.SelectorText}'")
                .Distinct(StringComparer.Ordinal)
                .ToList();
            var issue = new VmAutomationUssStyleAuditIssue
            {
                AssetPath = rule.AssetPath,
                Line = rule.Line,
                Selector = selectorText,
                Token = property,
                Kind = KIND,
                Property = property,
                Value = rule.Declarations[property],
                AuthoredUsageCount = usages.Count,
                RuntimeReferenceCount = runtimeAssignments.Count,
                UsageLocations = usages
                    .Concat(runtimeAssignments.Values.Select(location =>
                        location.ToDictionary()))
                    .Take(20).ToList(),
                StylesheetRules = childRules.Values
                    .OrderBy(contract => contract.Rule.AssetPath, StringComparer.Ordinal)
                    .ThenBy(contract => contract.Rule.Line)
                    .ThenBy(contract => contract.SelectorText, StringComparer.Ordinal)
                    .Select(contract => new Dictionary<string, object>
                    {
                        { "property", property },
                        { "value", contract.Rule.Declarations[property] },
                        { "selector", contract.SelectorText },
                        { "sourcePath", contract.Rule.AssetPath },
                        { "line", contract.Rule.Line },
                        { "sourceKind", "generated-child-stylesheet" }
                    }).ToList(),
                Suppressed = string.IsNullOrWhiteSpace(
                    rule.RedundantDeclarationSuppressionReason) == false,
                SuppressionReason = rule.RedundantDeclarationSuppressionReason,
                Message =
                    $"Declaration '{property}: {rule.Declarations[property]}' in selector " +
                    $"'{selectorText}' fixes a layout-only parent's flex cross size to the " +
                    $"same size already established by runtime-generated direct children " +
                    $"styled by {string.Join(", ", childSelectorLabels)}. Remove the parent " +
                    $"{property} and let its in-flow generated children establish the natural " +
                    "extent. Retain it only for an independently owned visual, clipping, " +
                    "interaction, or anchored region and document that contract with a " +
                    "reasoned allow-redundant-declaration suppression."
            };
            report.Record(issue, includeSuppressed);
        }

        private static IEnumerable<GeneratedChildContract> FindGeneratedChildContracts(
            UssCascadeDocument document, UssAuthoredElement parent, string property,
            UssUsageIndex usageIndex)
        {
            foreach (var contextualRule in document.Rules)
            {
                if (TryGetPositivePixels(contextualRule.Rule.Declarations, property,
                        out var childSize) == false ||
                    TryParseStaticSelector(contextualRule.SelectorText,
                        out var childSelector) == false ||
                    childSelector.IsDirectChildOf(parent) == false)
                {
                    continue;
                }

                var runtimeClasses = childSelector.Target.ClassNames
                    .Where(className => usageIndex
                        .GetRuntimeClassAssignments(className).Count > 0)
                    .OrderBy(className => className, StringComparer.Ordinal)
                    .ToList();
                if (runtimeClasses.Count == 0)
                {
                    continue;
                }

                var syntheticChild = childSelector.CreateTargetElement(parent);
                if (IsWinningDeclaration(document, syntheticChild, property,
                        contextualRule.Rule, contextualRule.SelectorText, childSize) == false ||
                    StyleValue(document, syntheticChild, "position") == "absolute" ||
                    HasNonZeroCrossMargins(document, syntheticChild, property))
                {
                    continue;
                }

                yield return new GeneratedChildContract(contextualRule.Rule,
                    contextualRule.SelectorText, childSize, runtimeClasses);
            }
        }

        private static bool IsCrossAxisProperty(UssCascadeDocument document,
            UssAuthoredElement element, string property)
        {
            var direction = StyleValue(document, element, "flex-direction");
            if (string.IsNullOrWhiteSpace(direction))
            {
                direction = "column";
            }

            var wrap = StyleValue(document, element, "flex-wrap");
            if (string.IsNullOrWhiteSpace(wrap) == false && wrap != "nowrap")
            {
                return false;
            }

            return (direction == "row" || direction == "row-reverse")
                ? property == "height"
                : (direction == "column" || direction == "column-reverse") &&
                  property == "width";
        }

        private static bool HasIndependentCrossSizeContract(
            UssCascadeDocument document, UssAuthoredElement element, string property)
        {
            var leading = property == "width" ? "left" : "top";
            var trailing = property == "width" ? "right" : "bottom";
            if (HasConcreteStyle(document, element, leading) &&
                HasConcreteStyle(document, element, trailing) ||
                HasConcreteStyle(document, element, "min-" + property) ||
                HasConcreteStyle(document, element, "max-" + property))
            {
                return true;
            }

            if (HasMeaningfulVisualValue("background-image",
                    StyleValue(document, element, "background-image")) ||
                HasMeaningfulVisualValue("background-color",
                    StyleValue(document, element, "background-color")) ||
                HasMeaningfulVisualValue("overflow",
                    StyleValue(document, element, "overflow")))
            {
                return true;
            }

            var crossProperties = property == "width"
                ? new[] { "padding-left", "padding-right", "border-left-width",
                    "border-right-width" }
                : new[] { "padding-top", "padding-bottom", "border-top-width",
                    "border-bottom-width" };
            return crossProperties.Any(crossProperty =>
                HasNonZeroLength(StyleValue(document, element, crossProperty)));
        }

        private static bool HasNonZeroCrossMargins(UssCascadeDocument document,
            UssAuthoredElement element, string property)
        {
            var margins = property == "width"
                ? new[] { "margin-left", "margin-right" }
                : new[] { "margin-top", "margin-bottom" };
            return margins.Any(margin =>
                HasNonZeroLength(StyleValue(document, element, margin)));
        }

        private static bool HasMeaningfulVisualValue(string property, string value)
        {
            value = (value ?? "").Trim().ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(value) || value == "initial" ||
                value == "unset" || value == "inherit")
            {
                return false;
            }

            if (property == "background-image")
            {
                return value != "none";
            }

            if (property == "background-color")
            {
                return value != "transparent" &&
                       Regex.IsMatch(value,
                           @"^rgba\([^,]+,[^,]+,[^,]+,\s*0(?:\.0+)?\)$") == false;
            }

            return property == "overflow" && value != "visible";
        }

        private static bool HasConcreteStyle(UssCascadeDocument document,
            UssAuthoredElement element, string property)
        {
            var value = StyleValue(document, element, property);
            return string.IsNullOrWhiteSpace(value) == false && value != "auto" &&
                   value != "none" && value != "initial" && value != "unset" &&
                   value != "inherit";
        }

        private static string StyleValue(UssCascadeDocument document,
            UssAuthoredElement element, string property)
        {
            if (element.InlineDeclarations.TryGetValue(property, out var inlineValue))
            {
                return inlineValue.Trim().ToLowerInvariant();
            }

            return ResolveDeclaration(document, element, property)?.Value
                ?.Trim().ToLowerInvariant() ?? "";
        }

        private static bool IsWinningDeclaration(UssCascadeDocument document,
            UssAuthoredElement element, string property, UssRule expectedRule,
            string expectedSelector, float expectedSize)
        {
            if (element.InlineDeclarations.ContainsKey(property))
            {
                return false;
            }

            var winner = ResolveDeclaration(document, element, property);
            return winner != null && ReferenceEquals(winner.Rule, expectedRule) &&
                   string.Equals(winner.SelectorText, expectedSelector,
                       StringComparison.Ordinal) &&
                   TryGetPixels(winner.Value, out var winnerSize) &&
                   Math.Abs(winnerSize - expectedSize) <= SizeEpsilon;
        }

        private static StaticDeclaration ResolveDeclaration(
            UssCascadeDocument document, UssAuthoredElement element, string property)
        {
            StaticDeclaration winner = null;
            foreach (var contextualRule in document.Rules)
            {
                if (contextualRule.Rule.Declarations.TryGetValue(property,
                        out var value) == false ||
                    TryParseStaticSelector(contextualRule.SelectorText,
                        out var selector) == false ||
                    selector.Matches(element) == false)
                {
                    continue;
                }

                var candidate = new StaticDeclaration(contextualRule.Rule,
                    contextualRule.SelectorText, value, contextualRule.Origin,
                    selector.Specificity, contextualRule.SourceOrder);
                if (winner == null || candidate.HasHigherOrEqualPriority(winner))
                {
                    winner = candidate;
                }
            }

            return winner;
        }

        private static bool TryGetPositivePixels(
            IReadOnlyDictionary<string, string> declarations, string property,
            out float value)
        {
            value = 0;
            return declarations.TryGetValue(property, out var rawValue) &&
                   TryGetPixels(rawValue, out value) && value > 0;
        }

        private static bool TryGetPixels(string rawValue, out float value)
        {
            value = 0;
            var match = PixelValueRegex.Match((rawValue ?? "").Trim());
            return match.Success && float.TryParse(match.Groups["value"].Value,
                NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }

        private static bool HasNonZeroLength(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var matches = Regex.Matches(value, @"[-+]?(?:\d+(?:\.\d*)?|\.\d+)");
            return matches.Count == 0 || matches.Cast<Match>().Any(match =>
                float.TryParse(match.Value, NumberStyles.Float,
                    CultureInfo.InvariantCulture, out var parsed) == false ||
                Math.Abs(parsed) > SizeEpsilon);
        }

        private static VmAutomationUssStyleAuditReport AuditFixture(string uss,
            string uxmlBody, string runtimeAssignedClass, bool includeSuppressed = false)
        {
            const string ussPath = "Assets/__GeneratedChildNaturalSizeSelfTest.uss";
            var rules = ParseStyleSheet(ussPath, uss);
            var usageIndex = new UssUsageIndex();
            if (string.IsNullOrWhiteSpace(runtimeAssignedClass) == false)
            {
                usageIndex.AddRuntimeClassAssignment(runtimeAssignedClass,
                    "Assets/__GeneratedChildNaturalSizeSelfTest.cs", 1, 1);
            }

            var authoredDocument = new UssAuthoredDocument(
                "Assets/__GeneratedChildNaturalSizeSelfTest.uxml",
                System.Xml.Linq.XDocument.Parse(
                    "<ui:UXML xmlns:ui=\"UnityEngine.UIElements\">" +
                    uxmlBody + "</ui:UXML>",
                    System.Xml.Linq.LoadOptions.SetLineInfo));
            usageIndex.Documents.Add(authoredDocument);
            var cascadeDocument = new UssCascadeDocument(authoredDocument);
            cascadeDocument.LoadedAssetPaths.Add(ussPath);
            foreach (var rule in rules)
            {
                foreach (var selectorText in rule.Selectors)
                {
                    cascadeDocument.Rules.Add(new UssCascadeRule
                    {
                        Rule = rule,
                        SelectorText = selectorText,
                        Origin = 1,
                        SourceOrder = cascadeDocument.NextSourceOrder()
                    });
                }
            }

            var cascadeIndex = new UssCascadeIndex();
            cascadeIndex.Documents.Add(cascadeDocument);
            var report = new VmAutomationUssStyleAuditReport(100);
            Audit(rules, usageIndex, cascadeIndex, report, includeSuppressed);
            report.SortIssues();
            return report;
        }

        private static bool HasActiveFinding(VmAutomationUssStyleAuditReport report,
            string property)
        {
            return report.Issues.Any(issue => issue.Kind == KIND &&
                issue.Property == property && issue.Suppressed == false);
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

        private sealed class StaticDeclaration
        {
            public readonly UssRule Rule;
            public readonly string SelectorText;
            public readonly string Value;
            private readonly int origin;
            private readonly int specificity;
            private readonly int sourceOrder;

            public StaticDeclaration(UssRule rule, string selectorText, string value,
                int origin, int specificity, int sourceOrder)
            {
                Rule = rule;
                SelectorText = selectorText;
                Value = value;
                this.origin = origin;
                this.specificity = specificity;
                this.sourceOrder = sourceOrder;
            }

            public bool HasHigherOrEqualPriority(StaticDeclaration other)
            {
                return origin > other.origin ||
                       origin == other.origin && specificity > other.specificity ||
                       origin == other.origin && specificity == other.specificity &&
                       sourceOrder >= other.sourceOrder;
            }
        }

        private sealed class GeneratedChildContract
        {
            public readonly UssRule Rule;
            public readonly string SelectorText;
            public readonly float Size;
            public readonly IReadOnlyList<string> RuntimeAssignedClasses;

            public string Key => $"{Rule.AssetPath}:{Rule.Line}:{SelectorText}";

            public GeneratedChildContract(UssRule rule, string selectorText,
                float size, IReadOnlyList<string> runtimeAssignedClasses)
            {
                Rule = rule;
                SelectorText = selectorText;
                Size = size;
                RuntimeAssignedClasses = runtimeAssignedClasses;
            }
        }
    }
}
#endif
