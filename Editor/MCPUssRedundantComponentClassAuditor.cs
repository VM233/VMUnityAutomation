#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using static VMUnityAutomation.Editor.MCPUssAuditContext;

namespace VMUnityAutomation.Editor
{
    internal static class MCPUssRedundantComponentClassAuditor
    {
        internal const string KIND = "redundant-component-scope-class";

        private static readonly Regex classTokenRegex = new Regex(
            @"(?<![A-Za-z0-9_-])\.(?<token>[A-Za-z_][A-Za-z0-9_-]*)",
            RegexOptions.Compiled);

        internal static void Audit(IReadOnlyList<UssRule> rules,
            UssUsageIndex usageIndex, MCPUssStyleAuditReport report,
            bool includeSuppressed)
        {
            foreach (var token in EnumerateClassTokens(rules))
            {
                var authoredUsages = FindAuthoredUsages(usageIndex, token);
                if (authoredUsages.Count < 2 ||
                    authoredUsages.Select(usage => usage.Document)
                        .Distinct().Count() != 1 ||
                    authoredUsages.Select(usage => usage.Element.ComponentTypeName)
                        .Distinct(StringComparer.Ordinal).Count() != 1)
                {
                    continue;
                }

                var runtimeReferences = usageIndex.GetRuntimeClassReferences(token);
                if (runtimeReferences.Count > 0 &&
                    (usageIndex.GetRuntimeClassAssignments(token).Count == 0 ||
                     usageIndex.GetRuntimeClassSemanticReferences(token).Count > 0))
                {
                    continue;
                }

                if (HasPlacementContract(rules, token) ||
                    MCPUssGeneratedChildStyleOwnershipAuditor
                        .ClassAnchorsGeneratedChildStyle(rules, usageIndex, token))
                {
                    continue;
                }

                var ancestor = FindNearestCommonNamedAncestor(authoredUsages);
                if (ancestor == null)
                {
                    continue;
                }

                var componentClass = FindReplacementComponentClass(usageIndex,
                    authoredUsages, ancestor);
                if (string.IsNullOrWhiteSpace(componentClass))
                {
                    continue;
                }

                var selectorOwner = FindPrimarySelector(rules, token);
                if (selectorOwner.Rule == null)
                {
                    continue;
                }

                var authoredLocations = authoredUsages.Select(usage =>
                        new UssUsageLocation(usage.Document.AssetPath,
                            usage.Element.Line, usage.Element.Column))
                    .ToList();
                var componentName = authoredUsages[0].Element.ComponentTypeName;
                var suggestedSelector = $"#{ancestor.Name} .{componentClass}";
                MCPUssStyleAuditor.AddIssue(report, selectorOwner.Rule,
                    selectorOwner.Selector, token, KIND, authoredLocations,
                    runtimeReferences,
                    $"Authored class '.{token}' is attached to every {componentName} below " +
                    $"'#{ancestor.Name}', while that component already supplies '.{componentClass}'. " +
                    $"Remove the authored and assignment-only runtime class, then scope the " +
                    $"affected component, pseudo-state, and child selectors under " +
                    $"'{suggestedSelector}' instead.",
                    includeSuppressed);
            }
        }

        internal static IReadOnlyList<Dictionary<string, object>> RunSelfTests()
        {
            const string path = "Assets/__RedundantComponentClassAudit.uss";
            var rules = MCPUssStyleSheetParser.ParseStyleSheet(path,
                ".component-alias { width: 12px; }\n" +
                ".component-alias > .unity-button__text { color: white; }\n" +
                ".component-alias:active { opacity: 0.8; }\n");

            var exactIndex = CreateIndex(
                "<ui:UXML xmlns:ui=\"UnityEngine.UIElements\">" +
                "<ui:VisualElement name=\"Scope\">" +
                "<ui:Button class=\"component-alias\"/>" +
                "<ui:Button class=\"component-alias\"/>" +
                "</ui:VisualElement></ui:UXML>");
            exactIndex.AddRuntimeClassReference("component-alias",
                "Assets/Runtime.cs", 3);
            exactIndex.AddRuntimeClassAssignment("component-alias",
                "Assets/Runtime.cs", 4);
            var exactReport = AuditForSelfTest(rules, exactIndex);

            var semanticIndex = CreateIndex(
                "<ui:UXML xmlns:ui=\"UnityEngine.UIElements\">" +
                "<ui:VisualElement name=\"Scope\">" +
                "<ui:Button class=\"component-alias\"/>" +
                "<ui:Button class=\"component-alias\"/>" +
                "</ui:VisualElement></ui:UXML>");
            semanticIndex.AddRuntimeClassReference("component-alias",
                "Assets/Runtime.cs", 3);
            semanticIndex.AddRuntimeClassAssignment("component-alias",
                "Assets/Runtime.cs", 4);
            semanticIndex.AddRuntimeClassSemanticReference("component-alias",
                "Assets/Runtime.cs", 5);
            var semanticReport = AuditForSelfTest(rules, semanticIndex);

            var partialIndex = CreateIndex(
                "<ui:UXML xmlns:ui=\"UnityEngine.UIElements\">" +
                "<ui:VisualElement name=\"Scope\">" +
                "<ui:Button class=\"component-alias\"/>" +
                "<ui:Button class=\"component-alias\"/>" +
                "<ui:Button/>" +
                "</ui:VisualElement></ui:UXML>");
            var partialReport = AuditForSelfTest(rules, partialIndex);

            var placementRules = MCPUssStyleSheetParser.ParseStyleSheet(path,
                ".rank { position: absolute; right: 3px; bottom: 3px; }\n");
            var placementIndex = CreateIndex(
                "<ui:UXML xmlns:ui=\"UnityEngine.UIElements\">" +
                "<ui:VisualElement name=\"Scope\">" +
                "<ui:Button class=\"rank\"/>" +
                "<ui:Button class=\"rank\"/>" +
                "</ui:VisualElement></ui:UXML>", "rank");
            var placementReport = AuditForSelfTest(placementRules, placementIndex);

            var skinRules = MCPUssStyleSheetParser.ParseStyleSheet(path,
                ".number-skin > .glyph { width: 8px; }\n");
            var skinIndex = CreateIndex(
                "<ui:UXML xmlns:ui=\"UnityEngine.UIElements\">" +
                "<ui:VisualElement name=\"Scope\">" +
                "<ui:Button class=\"number-skin\"/>" +
                "<ui:Button class=\"number-skin\"/>" +
                "</ui:VisualElement></ui:UXML>", "number-skin");
            skinIndex.AddRuntimeClassAssignment("glyph", "Assets/Runtime.cs", 7);
            var skinReport = AuditForSelfTest(skinRules, skinIndex);

            return new[]
            {
                TestCase("component alias under exact named scope warns",
                    exactReport.Issues.Count(issue => issue.Kind == KIND) == 1),
                TestCase("component alias recommends inherent component class",
                    exactReport.Issues.Single().Message.Contains(
                        "#Scope .unity-button", StringComparison.Ordinal)),
                TestCase("assignment-only runtime class remains removable",
                    exactReport.Issues.Single().RuntimeReferenceCount == 1),
                TestCase("dynamic semantic class contract passes",
                    semanticReport.Issues.All(issue => issue.Kind != KIND)),
                TestCase("partial component subset keeps its semantic class",
                    partialReport.Issues.All(issue => issue.Kind != KIND)),
                TestCase("semantic placement class passes",
                    placementReport.Issues.All(issue => issue.Kind != KIND)),
                TestCase("generated child skin class passes",
                    skinReport.Issues.All(issue => issue.Kind != KIND))
            };
        }

        private static bool HasPlacementContract(IEnumerable<UssRule> rules,
            string token)
        {
            return rules.Any(rule => rule.Selectors.Any(selector =>
                       TargetCompoundContainsClass(selector, token)) &&
                   new[] { "position", "left", "right", "top", "bottom" }
                       .Any(rule.Declarations.ContainsKey));
        }

        private static bool TargetCompoundContainsClass(string selector, string token)
        {
            var value = (selector ?? "").Trim();
            var splitIndex = value.LastIndexOfAny(new[] { ' ', '>', '+', '~' });
            var target = splitIndex < 0 ? value : value.Substring(splitIndex + 1);
            return classTokenRegex.Matches(target).Cast<Match>().Any(match =>
                string.Equals(match.Groups["token"].Value, token,
                    StringComparison.Ordinal));
        }

        private static IEnumerable<string> EnumerateClassTokens(
            IEnumerable<UssRule> rules)
        {
            return rules.SelectMany(rule => rule.Selectors)
                .SelectMany(selector => classTokenRegex.Matches(selector).Cast<Match>())
                .Select(match => match.Groups["token"].Value)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(token => token, StringComparer.Ordinal);
        }

        private static List<ElementUsage> FindAuthoredUsages(UssUsageIndex index,
            string token)
        {
            return index.Documents.SelectMany(document => document.Elements
                    .Where(element => element.AuthoredClasses.Contains(token))
                    .Select(element => new ElementUsage(document, element)))
                .ToList();
        }

        private static UssAuthoredElement FindNearestCommonNamedAncestor(
            IReadOnlyList<ElementUsage> usages)
        {
            for (var candidate = usages[0].Element.Parent;
                 candidate != null; candidate = candidate.Parent)
            {
                if (string.IsNullOrWhiteSpace(candidate.Name) == false &&
                    usages.All(usage => IsAncestorOf(candidate, usage.Element)))
                {
                    return candidate;
                }
            }

            return null;
        }

        private static string FindReplacementComponentClass(UssUsageIndex index,
            IReadOnlyList<ElementUsage> usages, UssAuthoredElement ancestor)
        {
            var commonClasses = new HashSet<string>(
                usages[0].Element.ImplicitClasses, StringComparer.Ordinal);
            foreach (var usage in usages.Skip(1))
            {
                commonClasses.IntersectWith(usage.Element.ImplicitClasses);
            }

            var authoredElements = new HashSet<UssAuthoredElement>(
                usages.Select(usage => usage.Element));
            var document = usages[0].Document;
            return commonClasses.Where(componentClass =>
                    HasExactScopedCoverage(document, ancestor, componentClass,
                        authoredElements) &&
                    IsComponentSpecific(index, componentClass,
                        usages[0].Element.ComponentTypeName))
                .OrderBy(componentClass =>
                    componentClass.StartsWith("unity-", StringComparison.Ordinal) ? 1 : 0)
                .ThenByDescending(componentClass => ComponentNameMatchesClass(
                    usages[0].Element.ComponentTypeName, componentClass))
                .ThenBy(componentClass => componentClass.Length)
                .ThenBy(componentClass => componentClass, StringComparer.Ordinal)
                .FirstOrDefault();
        }

        private static bool HasExactScopedCoverage(UssAuthoredDocument document,
            UssAuthoredElement ancestor, string componentClass,
            ISet<UssAuthoredElement> authoredElements)
        {
            var scopedElements = document.Elements.Where(element =>
                    IsAncestorOf(ancestor, element) &&
                    element.ImplicitClasses.Contains(componentClass))
                .ToList();
            return scopedElements.Count == authoredElements.Count &&
                   scopedElements.All(authoredElements.Contains);
        }

        private static bool IsComponentSpecific(UssUsageIndex index,
            string componentClass, string componentTypeName)
        {
            var componentTypes = index.Documents.SelectMany(document => document.Elements)
                .Where(element => element.ImplicitClasses.Contains(componentClass))
                .Select(element => element.ComponentTypeName)
                .Distinct(StringComparer.Ordinal)
                .ToList();
            return componentTypes.Count == 1 &&
                   string.Equals(componentTypes[0], componentTypeName,
                       StringComparison.Ordinal);
        }

        private static bool ComponentNameMatchesClass(string componentTypeName,
            string componentClass)
        {
            var shortName = componentTypeName?.Split('.').LastOrDefault() ?? "";
            foreach (var suffix in new[] { "VisualElement", "Element", "Field" })
            {
                if (shortName.EndsWith(suffix, StringComparison.Ordinal))
                {
                    shortName = shortName.Substring(0,
                        shortName.Length - suffix.Length);
                    break;
                }
            }

            var normalizedName = NormalizeIdentifier(shortName);
            return normalizedName.Length > 0 &&
                   NormalizeIdentifier(componentClass).Contains(normalizedName);
        }

        private static string NormalizeIdentifier(string value)
        {
            return new string((value ?? "").Where(char.IsLetterOrDigit)
                .Select(char.ToLowerInvariant).ToArray());
        }

        private static bool IsAncestorOf(UssAuthoredElement ancestor,
            UssAuthoredElement element)
        {
            for (var current = element.Parent; current != null; current = current.Parent)
            {
                if (ReferenceEquals(current, ancestor))
                {
                    return true;
                }
            }

            return false;
        }

        private static SelectorOwner FindPrimarySelector(IEnumerable<UssRule> rules,
            string token)
        {
            SelectorOwner fallback = default;
            foreach (var rule in rules)
            {
                foreach (var selector in rule.Selectors)
                {
                    if (classTokenRegex.Matches(selector).Cast<Match>().Any(match =>
                            string.Equals(match.Groups["token"].Value, token,
                                StringComparison.Ordinal)) == false)
                    {
                        continue;
                    }

                    var owner = new SelectorOwner(rule, selector);
                    if (string.Equals(selector, $".{token}", StringComparison.Ordinal))
                    {
                        return owner;
                    }

                    if (fallback.Rule == null)
                    {
                        fallback = owner;
                    }
                }
            }

            return fallback;
        }

        private static UssUsageIndex CreateIndex(string xml,
            string authoredClass = "component-alias")
        {
            var index = new UssUsageIndex();
            var document = new UssAuthoredDocument(
                "Assets/__RedundantComponentClassAudit.uxml",
                XDocument.Parse(xml, LoadOptions.SetLineInfo));
            index.Documents.Add(document);
            foreach (var element in document.Elements.Where(element =>
                         element.AuthoredClasses.Contains(authoredClass)))
            {
                index.AddClassUsage(authoredClass, document.AssetPath,
                    element.Line, element.Column, element.Name);
            }

            return index;
        }

        private static MCPUssStyleAuditReport AuditForSelfTest(
            IReadOnlyList<UssRule> rules, UssUsageIndex index)
        {
            var report = new MCPUssStyleAuditReport(100);
            Audit(rules, index, report, true);
            report.SortIssues();
            return report;
        }

        private static Dictionary<string, object> TestCase(string name, bool passed)
        {
            return new Dictionary<string, object>
            {
                { "name", name },
                { "passed", passed }
            };
        }

        private sealed class ElementUsage
        {
            internal readonly UssAuthoredDocument Document;
            internal readonly UssAuthoredElement Element;

            internal ElementUsage(UssAuthoredDocument document,
                UssAuthoredElement element)
            {
                Document = document;
                Element = element;
            }
        }

        private readonly struct SelectorOwner
        {
            internal readonly UssRule Rule;
            internal readonly string Selector;

            internal SelectorOwner(UssRule rule, string selector)
            {
                Rule = rule;
                Selector = selector;
            }
        }
    }
}
#endif
