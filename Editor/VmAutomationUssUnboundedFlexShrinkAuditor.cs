#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using UssAuthoredDocument = VMUnityAutomation.Editor.VmAutomationUssAuditContext.UssAuthoredDocument;
using UssAuthoredElement = VMUnityAutomation.Editor.VmAutomationUssAuditContext.UssAuthoredElement;
using UssCascadeDocument = VMUnityAutomation.Editor.VmAutomationUssAuditContext.UssCascadeDocument;
using UssCascadeIndex = VMUnityAutomation.Editor.VmAutomationUssAuditContext.UssCascadeIndex;
using UssCascadeRule = VMUnityAutomation.Editor.VmAutomationUssAuditContext.UssCascadeRule;
using UssRule = VMUnityAutomation.Editor.VmAutomationUssAuditContext.UssRule;
using UssSimpleSelector = VMUnityAutomation.Editor.VmAutomationUssAuditContext.UssSimpleSelector;
using UssUsageIndex = VMUnityAutomation.Editor.VmAutomationUssAuditContext.UssUsageIndex;
using static VMUnityAutomation.Editor.VmAutomationUssCascadeAuditor;
using static VMUnityAutomation.Editor.VmAutomationUssStyleSheetParser;

namespace VMUnityAutomation.Editor
{
    internal static class VmAutomationUssUnboundedFlexShrinkAuditor
    {
        internal const string KIND = "ineffective-unbounded-flex-shrink";

        private const float NumberEpsilon = 0.0001f;

        internal static void Audit(IReadOnlyList<UssRule> rules,
            UssUsageIndex usageIndex, UssCascadeIndex cascadeIndex,
            VmAutomationUssStyleAuditReport report, bool includeSuppressed)
        {
            foreach (var rule in rules)
            {
                if (rule.Declarations.TryGetValue("flex-shrink", out var value) == false ||
                    IsZero(value) == false)
                {
                    continue;
                }

                foreach (var selectorText in rule.Selectors)
                {
                    if (TryParseSimpleSelector(selectorText, out var selector) == false ||
                        selector.Specificity == 0 ||
                        VmAutomationUssStyleAuditor.SelectorHasRuntimeClassContract(
                            selectorText, usageIndex))
                    {
                        continue;
                    }

                    AuditSelector(rule, selectorText, selector, value, usageIndex,
                        cascadeIndex, report, includeSuppressed);
                }
            }
        }

        internal static IReadOnlyList<Dictionary<string, object>> RunSelfTests()
        {
            var cases = new List<Dictionary<string, object>>();

            var naturalDivider = AuditFixture(
                ".section { align-items: center; }\n" +
                ".divider { width: 291px; height: 9px; flex-shrink: 0; " +
                "background-image: url(\"divider.png\"); }\n",
                "<ui:VisualElement><ui:VisualElement class=\"section\">" +
                "<ui:VisualElement class=\"divider\"/>" +
                "</ui:VisualElement></ui:VisualElement>");
            AddCase(cases, "natural column divider flex-shrink warns",
                HasActiveFinding(naturalDivider));

            var naturalRowIcon = AuditFixture(
                ".section { align-items: center; }\n" +
                ".title { flex-direction: row; }\n" +
                ".icon { width: 27px; height: 27px; flex-shrink: 0; }\n",
                "<ui:VisualElement class=\"section\">" +
                "<ui:VisualElement class=\"title\">" +
                "<ui:VisualElement class=\"icon\"/>" +
                "</ui:VisualElement></ui:VisualElement>");
            AddCase(cases, "natural row icon flex-shrink warns",
                HasActiveFinding(naturalRowIcon));

            var boundedColumn = AuditFixture(
                ".panel { height: 45px; }\n" +
                ".header { height: 24px; flex-shrink: 0; }\n",
                "<ui:VisualElement class=\"panel\">" +
                "<ui:VisualElement class=\"header\"/>" +
                "</ui:VisualElement>");
            AddCase(cases, "fixed column main size justifies flex-shrink",
                HasAnyFinding(boundedColumn) == false);

            var boundedRow = AuditFixture(
                ".toolbar { width: 315px; flex-direction: row; }\n" +
                ".button { width: 147px; flex-shrink: 0; }\n",
                "<ui:VisualElement class=\"toolbar\">" +
                "<ui:VisualElement class=\"button\"/>" +
                "</ui:VisualElement>");
            AddCase(cases, "fixed row main size justifies flex-shrink",
                HasAnyFinding(boundedRow) == false);

            var maximumSize = AuditFixture(
                ".panel { max-height: 45px; }\n" +
                ".header { height: 24px; flex-shrink: 0; }\n",
                "<ui:VisualElement class=\"panel\">" +
                "<ui:VisualElement class=\"header\"/>" +
                "</ui:VisualElement>");
            AddCase(cases, "maximum main size justifies flex-shrink",
                HasAnyFinding(maximumSize) == false);

            var anchoredParent = AuditFixture(
                ".panel { position: absolute; top: 0; bottom: 0; }\n" +
                ".header { height: 24px; flex-shrink: 0; }\n",
                "<ui:VisualElement><ui:VisualElement class=\"panel\">" +
                "<ui:VisualElement class=\"header\"/>" +
                "</ui:VisualElement></ui:VisualElement>");
            AddCase(cases, "two-edge anchored parent justifies flex-shrink",
                HasAnyFinding(anchoredParent) == false);

            var growingParent = AuditFixture(
                ".panel { flex-grow: 1; }\n" +
                ".header { height: 24px; flex-shrink: 0; }\n",
                "<ui:VisualElement><ui:VisualElement class=\"panel\">" +
                "<ui:VisualElement class=\"header\"/>" +
                "</ui:VisualElement></ui:VisualElement>");
            AddCase(cases, "externally growing parent remains conservative",
                HasAnyFinding(growingParent) == false);

            var absoluteChild = AuditFixture(
                ".panel { height: 45px; }\n" +
                ".overlay { position: absolute; height: 9px; flex-shrink: 0; }\n",
                "<ui:VisualElement class=\"panel\">" +
                "<ui:VisualElement class=\"overlay\"/>" +
                "</ui:VisualElement>");
            AddCase(cases, "absolute child flex-shrink warns",
                HasActiveFinding(absoluteChild));

            var mixedUsage = AuditFixture(
                ".bounded { height: 45px; }\n" +
                ".item { height: 24px; flex-shrink: 0; }\n",
                "<ui:VisualElement><ui:VisualElement class=\"item\"/>" +
                "</ui:VisualElement><ui:VisualElement class=\"bounded\">" +
                "<ui:VisualElement class=\"item\"/>" +
                "</ui:VisualElement>");
            AddCase(cases, "mixed bounded and natural usages remain conservative",
                HasAnyFinding(mixedUsage) == false);

            var runtimeContract = AuditFixture(
                ".section { align-items: center; }\n" +
                ".runtime-divider { height: 9px; flex-shrink: 0; }\n",
                "<ui:VisualElement><ui:VisualElement class=\"section\">" +
                "<ui:VisualElement class=\"runtime-divider\"/>" +
                "</ui:VisualElement></ui:VisualElement>", "runtime-divider");
            AddCase(cases, "runtime class contract remains conservative",
                HasAnyFinding(runtimeContract) == false);

            var suppressed = AuditFixture(
                ".section { align-items: center; }\n" +
                "/* uss-audit: allow-redundant-declaration fixture documents external sizing */\n" +
                ".divider { height: 9px; flex-shrink: 0; }\n",
                "<ui:VisualElement><ui:VisualElement class=\"section\">" +
                "<ui:VisualElement class=\"divider\"/>" +
                "</ui:VisualElement></ui:VisualElement>", "", true);
            AddCase(cases, "reasoned redundant-declaration suppression is retained",
                suppressed.WarningCount == 0 && suppressed.SuppressedCount == 1 &&
                suppressed.Issues.Single(issue => issue.Kind == KIND).Suppressed);

            return cases;
        }

        private static void AuditSelector(UssRule rule, string selectorText,
            UssSimpleSelector selector, string value, UssUsageIndex usageIndex,
            UssCascadeIndex cascadeIndex, VmAutomationUssStyleAuditReport report,
            bool includeSuppressed)
        {
            var candidatePaths = GetCandidateDocumentPaths(selector, usageIndex);
            if (candidatePaths.Count == 0)
            {
                return;
            }

            var usages = new List<Dictionary<string, object>>();
            foreach (var document in cascadeIndex.Documents.Where(document =>
                         candidatePaths.Contains(document.AuthoredDocument.AssetPath) &&
                         document.LoadedAssetPaths.Contains(rule.AssetPath)))
            {
                foreach (var element in document.AuthoredDocument.Elements.Where(
                             selector.Matches))
                {
                    if (IsWinningDeclaration(document, element, rule) == false)
                    {
                        continue;
                    }

                    if (HasRuntimeLayoutUncertainty(element, usageIndex) ||
                        HasShrinkPressureEvidence(document, element))
                    {
                        return;
                    }

                    var parent = element.Parent;
                    usages.Add(new Dictionary<string, object>
                    {
                        { "path", document.AuthoredDocument.AssetPath },
                        { "line", element.Line },
                        { "column", element.Column },
                        { "usageKind", "natural-main-axis-flex-item" },
                        { "parentType", parent?.TypeName ?? "" },
                        { "parentName", parent?.Name ?? "" },
                        { "parentLine", parent?.Line ?? 0 }
                    });
                }
            }

            if (usages.Count == 0)
            {
                return;
            }

            var issue = new VmAutomationUssStyleAuditIssue
            {
                AssetPath = rule.AssetPath,
                Line = rule.Line,
                Selector = selectorText,
                Token = "flex-shrink",
                Kind = KIND,
                Property = "flex-shrink",
                Value = value,
                AuthoredUsageCount = usages.Count,
                UsageLocations = usages.Take(20).ToList(),
                Suppressed = string.IsNullOrWhiteSpace(
                    rule.RedundantDeclarationSuppressionReason) == false,
                SuppressionReason = rule.RedundantDeclarationSuppressionReason,
                Message =
                    $"Declaration 'flex-shrink: {value}' in selector '{selectorText}' " +
                    $"wins for {usages.Count} authored UXML element(s), but every matched " +
                    "element is either outside Flex flow or under a natural-size parent " +
                    "with no finite main-axis size, maximum, anchored extent, or externally " +
                    "allocated flex extent. No negative main-axis free space exists for " +
                    "shrink to resolve. Remove flex-shrink; if an external layout contract " +
                    "really bounds the parent, model that owner or document it with a " +
                    "reasoned allow-redundant-declaration suppression."
            };
            report.Record(issue, includeSuppressed);
        }

        private static HashSet<string> GetCandidateDocumentPaths(
            UssSimpleSelector selector, UssUsageIndex usageIndex)
        {
            IReadOnlyList<UssUsageLocation> locations;
            if (string.IsNullOrWhiteSpace(selector.Id) == false)
            {
                locations = usageIndex.GetIdUsages(selector.Id);
            }
            else if (selector.ClassNames.Count > 0)
            {
                locations = selector.ClassNames
                    .Select(usageIndex.GetClassUsages)
                    .OrderBy(values => values.Count)
                    .First();
            }
            else
            {
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            return new HashSet<string>(locations.Select(location => location.Path),
                StringComparer.OrdinalIgnoreCase);
        }

        private static bool IsWinningDeclaration(UssCascadeDocument document,
            UssAuthoredElement element, UssRule expectedRule)
        {
            if (element.InlineDeclarations.ContainsKey("flex-shrink"))
            {
                return false;
            }

            var winner = document.Resolve(element, "flex-shrink", null);
            return winner != null && ReferenceEquals(winner.Rule, expectedRule) &&
                   IsZero(winner.Value);
        }

        private static bool HasRuntimeLayoutUncertainty(UssAuthoredElement element,
            UssUsageIndex usageIndex)
        {
            return EnumerateElementAndParentClasses(element).Any(className =>
                usageIndex.GetRuntimeClassReferences(className).Count > 0);
        }

        private static IEnumerable<string> EnumerateElementAndParentClasses(
            UssAuthoredElement element)
        {
            if (element != null)
            {
                foreach (var className in element.AuthoredClasses)
                {
                    yield return className;
                }
            }

            if (element?.Parent == null)
            {
                yield break;
            }

            foreach (var className in element.Parent.AuthoredClasses)
            {
                yield return className;
            }
        }

        private static bool HasShrinkPressureEvidence(UssCascadeDocument document,
            UssAuthoredElement element)
        {
            if (StyleValue(document, element, "position") == "absolute")
            {
                return false;
            }

            var parent = element.Parent;
            if (parent == null)
            {
                return true;
            }

            if (IsScrollView(parent))
            {
                return false;
            }

            var direction = StyleValue(document, parent, "flex-direction");
            var horizontal = direction == "row" || direction == "row-reverse";
            var mainSize = horizontal ? "width" : "height";
            if (HasConcreteStyle(document, parent, mainSize) ||
                HasConcreteStyle(document, parent, "max-" + mainSize) ||
                HasAnchoredMainExtent(document, parent, horizontal) ||
                HasPositiveNumber(StyleValue(document, parent, "flex-grow")) ||
                HasConcreteStyle(document, parent, "flex-basis") ||
                MayReceiveCrossAxisStretch(document, parent, horizontal))
            {
                return true;
            }

            return parent.Parent == null;
        }

        private static bool HasAnchoredMainExtent(UssCascadeDocument document,
            UssAuthoredElement element, bool horizontal)
        {
            if (StyleValue(document, element, "position") != "absolute")
            {
                return false;
            }

            return horizontal
                ? HasConcreteStyle(document, element, "left") &&
                  HasConcreteStyle(document, element, "right")
                : HasConcreteStyle(document, element, "top") &&
                  HasConcreteStyle(document, element, "bottom");
        }

        private static bool MayReceiveCrossAxisStretch(UssCascadeDocument document,
            UssAuthoredElement element, bool horizontalAxis)
        {
            var owner = element.Parent;
            if (owner == null)
            {
                return false;
            }

            var ownerDirection = StyleValue(document, owner, "flex-direction");
            var ownerHorizontal = ownerDirection == "row" ||
                                  ownerDirection == "row-reverse";
            if (horizontalAxis == ownerHorizontal)
            {
                return false;
            }

            var alignSelf = StyleValue(document, element, "align-self");
            if (alignSelf == "stretch")
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(alignSelf) == false && alignSelf != "auto")
            {
                return false;
            }

            var alignItems = StyleValue(document, owner, "align-items");
            return string.IsNullOrWhiteSpace(alignItems) || alignItems == "stretch";
        }

        private static bool IsScrollView(UssAuthoredElement element)
        {
            return element.TypeName.EndsWith("ScrollView", StringComparison.Ordinal);
        }

        private static bool HasConcreteStyle(UssCascadeDocument document,
            UssAuthoredElement element, string property)
        {
            var value = StyleValue(document, element, property);
            return string.IsNullOrWhiteSpace(value) == false && value != "auto" &&
                   value != "none" && value != "initial" && value != "inherit" &&
                   value != "unset";
        }

        private static string StyleValue(UssCascadeDocument document,
            UssAuthoredElement element, string property)
        {
            if (element.InlineDeclarations.TryGetValue(property, out var inlineValue))
            {
                return inlineValue.Trim().ToLowerInvariant();
            }

            return document.Resolve(element, property, null)?.Value
                ?.Trim().ToLowerInvariant() ?? "";
        }

        private static bool HasPositiveNumber(string value)
        {
            return float.TryParse((value ?? "").Trim(), NumberStyles.Float,
                       CultureInfo.InvariantCulture, out var parsed) &&
                   parsed > NumberEpsilon;
        }

        private static bool IsZero(string value)
        {
            return float.TryParse((value ?? "").Trim(), NumberStyles.Float,
                       CultureInfo.InvariantCulture, out var parsed) &&
                   Math.Abs(parsed) <= NumberEpsilon;
        }

        private static VmAutomationUssStyleAuditReport AuditFixture(string uss,
            string uxmlBody, string runtimeClass = "", bool includeSuppressed = false)
        {
            const string ussPath = "Assets/__UnboundedFlexShrinkSelfTest.uss";
            var rules = ParseStyleSheet(ussPath, uss);
            var usageIndex = new UssUsageIndex();
            var authoredDocument = new UssAuthoredDocument(
                "Assets/__UnboundedFlexShrinkSelfTest.uxml",
                XDocument.Parse(
                    "<ui:UXML xmlns:ui=\"UnityEngine.UIElements\">" +
                    uxmlBody + "</ui:UXML>", LoadOptions.SetLineInfo));
            usageIndex.Documents.Add(authoredDocument);
            foreach (var element in authoredDocument.Elements)
            {
                foreach (var className in element.AuthoredClasses)
                {
                    usageIndex.AddClassUsage(className, authoredDocument.AssetPath,
                        element.Line, element.Column, element.Name);
                }

                if (string.IsNullOrWhiteSpace(element.Name) == false)
                {
                    usageIndex.AddIdUsage(element.Name, authoredDocument.AssetPath,
                        element.Line, element.Column);
                }
            }

            if (string.IsNullOrWhiteSpace(runtimeClass) == false)
            {
                usageIndex.AddRuntimeClassReference(runtimeClass,
                    "Assets/__UnboundedFlexShrinkSelfTest.cs", 1, 1);
            }

            var cascadeDocument = new UssCascadeDocument(authoredDocument);
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
            var report = new VmAutomationUssStyleAuditReport(100);
            Audit(rules, usageIndex, cascadeIndex, report, includeSuppressed);
            report.SortIssues();
            return report;
        }

        private static bool HasActiveFinding(VmAutomationUssStyleAuditReport report)
        {
            return report.Issues.Any(issue => issue.Kind == KIND &&
                                              issue.Suppressed == false);
        }

        private static bool HasAnyFinding(VmAutomationUssStyleAuditReport report)
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
    }
}
#endif
