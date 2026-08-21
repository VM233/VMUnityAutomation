#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
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
    internal static class VmAutomationUssAuthoredContentNaturalSizeAuditor
    {
        internal const string KIND = "redundant-authored-content-cross-size";

        private const float NumberEpsilon = 0.0001f;

        private static readonly Regex PixelValueRegex = new Regex(
            @"^(?<value>-?(?:\d+(?:\.\d+)?|\.\d+))px$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        internal static void Audit(IReadOnlyList<UssRule> rules,
            UssUsageIndex usageIndex, UssCascadeIndex cascadeIndex,
            VmAutomationUssStyleAuditReport report, bool includeSuppressed)
        {
            var styles = new ResolvedStyleCache();
            foreach (var rule in rules)
            {
                AuditProperty(rule, "width", usageIndex, cascadeIndex, styles,
                    report, includeSuppressed);
                AuditProperty(rule, "height", usageIndex, cascadeIndex, styles,
                    report, includeSuppressed);
            }
        }

        internal static IReadOnlyList<Dictionary<string, object>> RunSelfTests()
        {
            var cases = new List<Dictionary<string, object>>();

            var categoryTitle = AuditFixture(
                ".section { align-items: center; }\n" +
                ".title { height: 30px; flex-direction: row; align-items: center; }\n" +
                ".icon { width: 27px; height: 27px; }\n",
                "<ui:VisualElement class=\"section\">" +
                "<ui:VisualElement class=\"title\">" +
                "<ui:VisualElement class=\"icon\"/><ui:Label/>" +
                "</ui:VisualElement></ui:VisualElement>");
            AddCase(cases, "natural active-skill category title height warns",
                HasActiveFinding(categoryTitle, "height"));

            var naturalColumn = AuditFixture(
                ".outer { align-items: flex-start; }\n" +
                ".column { width: 90px; align-items: flex-start; }\n" +
                ".row { width: 84px; }\n",
                "<ui:VisualElement class=\"outer\">" +
                "<ui:VisualElement class=\"column\">" +
                "<ui:VisualElement class=\"row\"/><ui:Label/>" +
                "</ui:VisualElement></ui:VisualElement>");
            AddCase(cases, "natural column width warns",
                HasActiveFinding(naturalColumn, "width"));

            var rowMainSize = AuditFixture(
                ".title { width: 180px; flex-direction: row; align-items: center; }\n" +
                ".icon { width: 27px; height: 27px; }\n",
                "<ui:VisualElement><ui:VisualElement class=\"title\">" +
                "<ui:VisualElement class=\"icon\"/><ui:Label/>" +
                "</ui:VisualElement></ui:VisualElement>");
            AddCase(cases, "row main-axis width passes",
                HasAnyFinding(rowMainSize) == false);

            var stretched = AuditFixture(
                ".title { height: 30px; flex-direction: row; align-items: stretch; }\n" +
                ".icon { height: 27px; }\n",
                "<ui:VisualElement><ui:VisualElement class=\"title\">" +
                "<ui:VisualElement class=\"icon\"/><ui:Label/>" +
                "</ui:VisualElement></ui:VisualElement>");
            AddCase(cases, "stretched cross-axis region passes",
                HasAnyFinding(stretched) == false);

            var visualOwner = AuditFixture(
                ".title { height: 30px; flex-direction: row; align-items: center; " +
                "background-image: url(\"title.png\"); }\n" +
                ".icon { height: 27px; }\n",
                "<ui:VisualElement><ui:VisualElement class=\"title\">" +
                "<ui:VisualElement class=\"icon\"/><ui:Label/>" +
                "</ui:VisualElement></ui:VisualElement>");
            AddCase(cases, "visual box owner keeps fixed cross size",
                HasAnyFinding(visualOwner) == false);

            var paddedOwner = AuditFixture(
                ".title { height: 30px; flex-direction: row; align-items: center; " +
                "padding-top: 3px; }\n" +
                ".icon { height: 27px; }\n",
                "<ui:VisualElement><ui:VisualElement class=\"title\">" +
                "<ui:VisualElement class=\"icon\"/><ui:Label/>" +
                "</ui:VisualElement></ui:VisualElement>");
            AddCase(cases, "padded box remains conservative",
                HasAnyFinding(paddedOwner) == false);

            var boundedOwner = AuditFixture(
                ".section { height: 90px; }\n" +
                ".title { height: 30px; flex-direction: row; align-items: center; }\n" +
                ".icon { height: 27px; }\n",
                "<ui:VisualElement class=\"section\">" +
                "<ui:VisualElement class=\"title\">" +
                "<ui:VisualElement class=\"icon\"/><ui:Label/>" +
                "</ui:VisualElement></ui:VisualElement>");
            AddCase(cases, "externally bounded parent remains conservative",
                HasAnyFinding(boundedOwner) == false);

            var absoluteParent = AuditFixture(
                ".title { position: absolute; height: 30px; flex-direction: row; " +
                "align-items: center; }\n.icon { height: 27px; }\n",
                "<ui:VisualElement><ui:VisualElement class=\"title\">" +
                "<ui:VisualElement class=\"icon\"/><ui:Label/>" +
                "</ui:VisualElement></ui:VisualElement>");
            AddCase(cases, "absolute container passes",
                HasAnyFinding(absoluteParent) == false);

            var absoluteChildren = AuditFixture(
                ".title { height: 30px; flex-direction: row; align-items: center; }\n" +
                ".icon { position: absolute; height: 27px; }\n",
                "<ui:VisualElement><ui:VisualElement class=\"title\">" +
                "<ui:VisualElement class=\"icon\"/>" +
                "<ui:VisualElement class=\"icon\"/></ui:VisualElement>" +
                "</ui:VisualElement>");
            AddCase(cases, "absolute-only content passes",
                HasAnyFinding(absoluteChildren) == false);

            var interactiveOwner = AuditFixture(
                ".title { height: 30px; flex-direction: row; align-items: center; }\n" +
                ".icon { height: 27px; }\n",
                "<ui:VisualElement><ui:Button class=\"title\">" +
                "<ui:VisualElement class=\"icon\"/><ui:Label/>" +
                "</ui:Button></ui:VisualElement>");
            AddCase(cases, "interactive component keeps its hit-region size",
                HasAnyFinding(interactiveOwner) == false);

            var mixedUsage = AuditFixture(
                ".bounded { height: 90px; }\n" +
                ".title { height: 30px; flex-direction: row; align-items: center; }\n" +
                ".icon { height: 27px; }\n",
                "<ui:VisualElement><ui:VisualElement class=\"title\">" +
                "<ui:VisualElement class=\"icon\"/><ui:Label/>" +
                "</ui:VisualElement></ui:VisualElement>" +
                "<ui:VisualElement class=\"bounded\">" +
                "<ui:VisualElement class=\"title\">" +
                "<ui:VisualElement class=\"icon\"/><ui:Label/>" +
                "</ui:VisualElement></ui:VisualElement>");
            AddCase(cases, "mixed natural and bounded usages remain conservative",
                HasAnyFinding(mixedUsage) == false);

            var runtimeContract = AuditFixture(
                ".title { height: 30px; flex-direction: row; align-items: center; }\n" +
                ".icon { height: 27px; }\n",
                "<ui:VisualElement><ui:VisualElement class=\"title\">" +
                "<ui:VisualElement class=\"icon\"/><ui:Label/>" +
                "</ui:VisualElement></ui:VisualElement>", "title");
            AddCase(cases, "runtime class contract remains conservative",
                HasAnyFinding(runtimeContract) == false);

            var suppressed = AuditFixture(
                "/* uss-audit: allow-redundant-declaration measured title region */\n" +
                ".title { height: 30px; flex-direction: row; align-items: center; }\n" +
                ".icon { height: 27px; }\n",
                "<ui:VisualElement><ui:VisualElement class=\"title\">" +
                "<ui:VisualElement class=\"icon\"/><ui:Label/>" +
                "</ui:VisualElement></ui:VisualElement>", "", true);
            AddCase(cases, "reasoned natural-size suppression is retained",
                suppressed.WarningCount == 0 && suppressed.SuppressedCount == 1 &&
                suppressed.Issues.Single(issue => issue.Kind == KIND).Suppressed);

            return cases;
        }

        private static void AuditProperty(UssRule rule, string property,
            UssUsageIndex usageIndex, UssCascadeIndex cascadeIndex,
            ResolvedStyleCache styles, VmAutomationUssStyleAuditReport report,
            bool includeSuppressed)
        {
            if (TryGetPositivePixels(rule.Declarations, property,
                    out var declaredSize) == false)
            {
                return;
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

                AuditSelector(rule, selectorText, selector, property, declaredSize,
                    usageIndex, cascadeIndex, styles, report, includeSuppressed);
            }
        }

        private static void AuditSelector(UssRule rule, string selectorText,
            UssSimpleSelector selector, string property, float declaredSize,
            UssUsageIndex usageIndex, UssCascadeIndex cascadeIndex,
            ResolvedStyleCache styles, VmAutomationUssStyleAuditReport report,
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
                    if (IsWinningDeclaration(document, element, property, rule,
                            selectorText, declaredSize) == false)
                    {
                        continue;
                    }

                    if (IsRedundantNaturalCrossSize(document, element, property,
                            usageIndex, styles, out var inFlowChildCount) == false)
                    {
                        return;
                    }

                    usages.Add(new Dictionary<string, object>
                    {
                        { "path", document.AuthoredDocument.AssetPath },
                        { "line", element.Line },
                        { "column", element.Column },
                        { "inFlowChildCount", inFlowChildCount },
                        { "parentLine", element.Parent?.Line ?? 0 }
                    });
                }
            }

            if (usages.Count == 0)
            {
                return;
            }

            var axis = property == "width" ? "horizontal" : "vertical";
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
                UsageLocations = usages.Take(20).ToList(),
                Suppressed = string.IsNullOrWhiteSpace(
                    rule.RedundantDeclarationSuppressionReason) == false,
                SuppressionReason = rule.RedundantDeclarationSuppressionReason,
                Message =
                    $"Declaration '{property}: {rule.Declarations[property]}' in selector " +
                    $"'{selectorText}' fixes the {axis} flex cross size of " +
                    $"{usages.Count} authored layout-only VisualElement container(s), even " +
                    "though their visible in-flow authored children establish that extent " +
                    $"naturally. Remove {property} and let content size the container. Keep a " +
                    "fixed cross size only for an independently owned visual, clipping, " +
                    "interaction, externally bounded, or anchored region and document that " +
                    "contract with a reasoned allow-redundant-declaration suppression."
            };
            report.Record(issue, includeSuppressed);
        }

        private static bool IsRedundantNaturalCrossSize(
            UssCascadeDocument document, UssAuthoredElement element, string property,
            UssUsageIndex usageIndex, ResolvedStyleCache styles,
            out int inFlowChildCount)
        {
            inFlowChildCount = 0;
            if (element.TypeName != "VisualElement" || element.Parent == null ||
                StyleValue(styles, document, element, "position") == "absolute" ||
                StyleValue(styles, document, element, "display") == "none" ||
                HasRuntimeLayoutUncertainty(element, usageIndex, styles) ||
                IsCrossAxisProperty(styles, document, element, property) == false ||
                HasIndependentNaturalSizeContract(styles, document, element, property) ||
                HasExternalExtentContract(styles, document, element, property) ||
                HasCrossAxisAnchoredAbsoluteChild(styles, document, element, property))
            {
                return false;
            }

            var alignment = StyleValue(styles, document, element, "align-items");
            if (alignment != "flex-start" && alignment != "center" &&
                alignment != "flex-end")
            {
                return false;
            }

            var inFlowChildren = element.Children.Where(child =>
                    StyleValue(styles, document, child, "position") != "absolute" &&
                    StyleValue(styles, document, child, "display") != "none")
                .ToList();
            inFlowChildCount = inFlowChildren.Count;
            return inFlowChildren.Count >= 2 && inFlowChildren.All(child =>
                EstablishesNaturalAxisSize(styles, document, child, property));
        }

        private static bool EstablishesNaturalAxisSize(ResolvedStyleCache styles,
            UssCascadeDocument document, UssAuthoredElement element, string property)
        {
            if (StyleValue(styles, document, element, "position") == "absolute" ||
                StyleValue(styles, document, element, "display") == "none" ||
                StyleValue(styles, document, element, "align-self") == "stretch")
            {
                return false;
            }

            var size = StyleValue(styles, document, element, property);
            if (TryGetPixels(size, out var pixels) && pixels > 0)
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(size) == false && size != "auto" &&
                size != "initial" && size != "unset" && size != "inherit")
            {
                return false;
            }

            if (IsIntrinsicTextElement(element))
            {
                return true;
            }

            return element.Children.Any(child => EstablishesNaturalAxisSize(styles,
                document, child, property));
        }

        private static bool IsIntrinsicTextElement(UssAuthoredElement element)
        {
            return element.TypeName == "Label" || element.TypeName == "TextElement" ||
                   element.TypeName.EndsWith(".Label", StringComparison.Ordinal) ||
                   element.TypeName.EndsWith(".TextElement", StringComparison.Ordinal);
        }

        private static bool IsCrossAxisProperty(ResolvedStyleCache styles,
            UssCascadeDocument document, UssAuthoredElement element, string property)
        {
            var direction = StyleValue(styles, document, element, "flex-direction");
            if (string.IsNullOrWhiteSpace(direction))
            {
                direction = "column";
            }

            var wrap = StyleValue(styles, document, element, "flex-wrap");
            if (string.IsNullOrWhiteSpace(wrap) == false && wrap != "nowrap")
            {
                return false;
            }

            return (direction == "row" || direction == "row-reverse")
                ? property == "height"
                : (direction == "column" || direction == "column-reverse") &&
                  property == "width";
        }

        private static bool HasIndependentNaturalSizeContract(
            ResolvedStyleCache styles, UssCascadeDocument document,
            UssAuthoredElement element, string property)
        {
            if (element.HasBindings || string.Equals(element.Focusable, "true",
                    StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(element.TabIndex) == false ||
                string.Equals(element.PickingMode, "Position",
                    StringComparison.OrdinalIgnoreCase) ||
                HasMeaningfulVisualValue("background-image",
                    StyleValue(styles, document, element, "background-image")) ||
                HasMeaningfulVisualValue("background-color",
                    StyleValue(styles, document, element, "background-color")) ||
                HasMeaningfulVisualValue("overflow",
                    StyleValue(styles, document, element, "overflow")))
            {
                return true;
            }

            var leading = property == "width" ? "left" : "top";
            var trailing = property == "width" ? "right" : "bottom";
            if (HasConcreteStyle(styles, document, element, leading) &&
                HasConcreteStyle(styles, document, element, trailing) ||
                HasConcreteStyle(styles, document, element, "min-" + property) ||
                HasConcreteStyle(styles, document, element, "max-" + property))
            {
                return true;
            }

            var boxProperties = property == "width"
                ? new[] { "padding-left", "padding-right", "border-left-width",
                    "border-right-width" }
                : new[] { "padding-top", "padding-bottom", "border-top-width",
                    "border-bottom-width" };
            return boxProperties.Any(boxProperty => HasNonZeroLength(
                StyleValue(styles, document, element, boxProperty)));
        }

        private static bool HasExternalExtentContract(ResolvedStyleCache styles,
            UssCascadeDocument document, UssAuthoredElement element, string property)
        {
            var owner = element.Parent;
            if (owner == null ||
                HasConcreteStyle(styles, document, owner, property) ||
                HasConcreteStyle(styles, document, owner, "min-" + property) ||
                HasConcreteStyle(styles, document, owner, "max-" + property) ||
                HasPositiveNumber(StyleValue(styles, document, element, "flex-grow")) ||
                HasConcreteStyle(styles, document, element, "flex-basis"))
            {
                return true;
            }

            var leading = property == "width" ? "left" : "top";
            var trailing = property == "width" ? "right" : "bottom";
            if (StyleValue(styles, document, owner, "position") == "absolute" &&
                HasConcreteStyle(styles, document, owner, leading) &&
                HasConcreteStyle(styles, document, owner, trailing))
            {
                return true;
            }

            var ownerDirection = StyleValue(styles, document, owner, "flex-direction");
            var ownerHorizontal = ownerDirection == "row" ||
                                  ownerDirection == "row-reverse";
            var propertyIsOwnerCrossAxis = ownerHorizontal
                ? property == "height"
                : property == "width";
            if (propertyIsOwnerCrossAxis == false)
            {
                return false;
            }

            var alignSelf = StyleValue(styles, document, element, "align-self");
            if (alignSelf == "stretch")
            {
                return true;
            }

            if (string.IsNullOrWhiteSpace(alignSelf) == false && alignSelf != "auto")
            {
                return false;
            }

            var alignItems = StyleValue(styles, document, owner, "align-items");
            return string.IsNullOrWhiteSpace(alignItems) || alignItems == "stretch";
        }

        private static bool HasCrossAxisAnchoredAbsoluteChild(
            ResolvedStyleCache styles, UssCascadeDocument document,
            UssAuthoredElement element, string property)
        {
            var leading = property == "width" ? "left" : "top";
            var trailing = property == "width" ? "right" : "bottom";
            return element.Children.Any(child =>
                StyleValue(styles, document, child, "position") == "absolute" &&
                HasConcreteStyle(styles, document, child, leading) &&
                HasConcreteStyle(styles, document, child, trailing));
        }

        private static bool HasRuntimeLayoutUncertainty(UssAuthoredElement element,
            UssUsageIndex usageIndex, ResolvedStyleCache styles)
        {
            return styles.HasRuntimeClassReferenceInSubtree(element, usageIndex) ||
                   styles.HasOwnRuntimeClassReference(element.Parent, usageIndex);
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
                locations = selector.ClassNames.Select(usageIndex.GetClassUsages)
                    .OrderBy(values => values.Count).First();
            }
            else
            {
                return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            }

            return new HashSet<string>(locations.Select(location => location.Path),
                StringComparer.OrdinalIgnoreCase);
        }

        private static bool IsWinningDeclaration(UssCascadeDocument document,
            UssAuthoredElement element, string property, UssRule expectedRule,
            string expectedSelector, float expectedSize)
        {
            if (element.InlineDeclarations.ContainsKey(property))
            {
                return false;
            }

            var winner = document.Resolve(element, property, null);
            return winner != null && ReferenceEquals(winner.Rule, expectedRule) &&
                   winner.SelectorText == expectedSelector &&
                   TryGetPixels(winner.Value, out var winnerSize) &&
                   Math.Abs(winnerSize - expectedSize) <= NumberEpsilon;
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

        private static bool HasConcreteStyle(ResolvedStyleCache styles,
            UssCascadeDocument document, UssAuthoredElement element, string property)
        {
            var value = StyleValue(styles, document, element, property);
            return string.IsNullOrWhiteSpace(value) == false && value != "auto" &&
                   value != "none" && value != "initial" && value != "inherit" &&
                   value != "unset";
        }

        private static string StyleValue(ResolvedStyleCache styles,
            UssCascadeDocument document, UssAuthoredElement element, string property)
        {
            return styles.Get(document, element, property).Trim().ToLowerInvariant();
        }

        private static bool HasPositiveNumber(string value)
        {
            return float.TryParse((value ?? "").Trim(), NumberStyles.Float,
                       CultureInfo.InvariantCulture, out var parsed) &&
                   parsed > NumberEpsilon;
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
                Math.Abs(parsed) > NumberEpsilon);
        }

        private static VmAutomationUssStyleAuditReport AuditFixture(string uss,
            string uxmlBody, string runtimeClass = "", bool includeSuppressed = false)
        {
            const string ussPath = "Assets/__AuthoredContentNaturalSizeSelfTest.uss";
            var rules = ParseStyleSheet(ussPath, uss);
            var usageIndex = new UssUsageIndex();
            var authoredDocument = new UssAuthoredDocument(
                "Assets/__AuthoredContentNaturalSizeSelfTest.uxml",
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
                    "Assets/__AuthoredContentNaturalSizeSelfTest.cs", 1, 1);
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

        private static bool HasActiveFinding(VmAutomationUssStyleAuditReport report,
            string property)
        {
            return report.Issues.Any(issue => issue.Kind == KIND &&
                issue.Property == property && issue.Suppressed == false);
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

        private sealed class ResolvedStyleCache
        {
            private readonly Dictionary<UssCascadeDocument,
                Dictionary<UssAuthoredElement, Dictionary<string, string>>> values =
                new Dictionary<UssCascadeDocument,
                    Dictionary<UssAuthoredElement, Dictionary<string, string>>>();
            private readonly Dictionary<UssAuthoredElement, bool>
                runtimeClassReferenceInSubtree =
                    new Dictionary<UssAuthoredElement, bool>();

            public string Get(UssCascadeDocument document, UssAuthoredElement element,
                string property)
            {
                if (element == null)
                {
                    return "";
                }

                if (element.InlineDeclarations.TryGetValue(property,
                        out var inlineValue))
                {
                    return inlineValue ?? "";
                }

                if (values.TryGetValue(document, out var documentValues) == false)
                {
                    documentValues = new Dictionary<UssAuthoredElement,
                        Dictionary<string, string>>();
                    values[document] = documentValues;
                }

                if (documentValues.TryGetValue(element, out var elementValues) == false)
                {
                    elementValues = new Dictionary<string, string>(
                        StringComparer.OrdinalIgnoreCase);
                    documentValues[element] = elementValues;
                }

                if (elementValues.TryGetValue(property, out var value) == false)
                {
                    value = document.Resolve(element, property, null)?.Value ?? "";
                    elementValues[property] = value;
                }

                return value;
            }

            public bool HasRuntimeClassReferenceInSubtree(
                UssAuthoredElement element, UssUsageIndex usageIndex)
            {
                if (runtimeClassReferenceInSubtree.TryGetValue(element,
                        out var cached))
                {
                    return cached;
                }

                var result = HasOwnRuntimeClassReference(element, usageIndex) ||
                             element.Children.Any(child =>
                                 HasRuntimeClassReferenceInSubtree(child, usageIndex));
                runtimeClassReferenceInSubtree[element] = result;
                return result;
            }

            public bool HasOwnRuntimeClassReference(UssAuthoredElement element,
                UssUsageIndex usageIndex)
            {
                return element != null && element.AuthoredClasses.Any(className =>
                    usageIndex.GetRuntimeClassReferences(className).Count > 0);
            }
        }
    }
}
#endif
