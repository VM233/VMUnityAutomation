#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace VMUnityAutomation.Editor
{
    internal static class VmAutomationUxmlNaturalFlowLayoutAuditor
    {
        internal const string SUPPRESSION_MARKER =
            "uxml-layout-audit: allow-manual-sibling-layout";
        internal const string FIXED_NATURAL_CROSS_SIZE_SUPPRESSION_MARKER =
            "uxml-layout-audit: allow-fixed-natural-cross-size";
        internal const string SCROLL_AXIS_FLEX_SHRINK_SUPPRESSION_MARKER =
            "uxml-layout-audit: allow-scroll-axis-flex-shrink";
        internal const string FIXED_LOCALIZED_BUTTON_WIDTH_SUPPRESSION_MARKER =
            "uxml-layout-audit: allow-fixed-localized-button-width";
        internal const string FIXED_CONTENT_LABEL_HEIGHT_SUPPRESSION_MARKER =
            "uxml-layout-audit: allow-fixed-content-label-height";

        internal const string ABSOLUTE_FLOW_STACK_KIND =
            "manual-absolute-flow-stack";

        private const float LayoutEpsilon = 0.01f;
        private const int MinimumAbsoluteFlowStackSize = 3;

        private static readonly Regex PixelValueRegex =
            new Regex(@"^(?<value>-?(?:\d+(?:\.\d+)?|\.\d+))px$",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex StyleDeclarationRegex =
            new Regex(@"(?:^|;)\s*(?<name>[-A-Za-z0-9]+)\s*:\s*(?<value>[^;]+)",
                RegexOptions.Compiled);

        private static readonly Regex SuppressionRegex =
            new Regex(
                @"^\s*uxml-layout-audit:\s*allow-manual-sibling-layout\s+(?<reason>.+?)\s*$",
                RegexOptions.Compiled | RegexOptions.IgnoreCase |
                RegexOptions.Singleline);

        private static readonly Regex FixedNaturalCrossSizeSuppressionRegex =
            new Regex(
                @"^\s*uxml-layout-audit:\s*allow-fixed-natural-cross-size\s+(?<reason>.+?)\s*$",
                RegexOptions.Compiled | RegexOptions.IgnoreCase |
                RegexOptions.Singleline);

        private static readonly Regex ScrollAxisFlexShrinkSuppressionRegex =
            new Regex(
                @"^\s*uxml-layout-audit:\s*allow-scroll-axis-flex-shrink\s+(?<reason>.+?)\s*$",
                RegexOptions.Compiled | RegexOptions.IgnoreCase |
                RegexOptions.Singleline);

        private static readonly Regex FixedLocalizedButtonWidthSuppressionRegex =
            new Regex(
                @"^\s*uxml-layout-audit:\s*allow-fixed-localized-button-width\s+(?<reason>.+?)\s*$",
                RegexOptions.Compiled | RegexOptions.IgnoreCase |
                RegexOptions.Singleline);

        private static readonly Regex FixedContentLabelHeightSuppressionRegex =
            new Regex(
                @"^\s*uxml-layout-audit:\s*allow-fixed-content-label-height\s+(?<reason>.+?)\s*$",
                RegexOptions.Compiled | RegexOptions.IgnoreCase |
                RegexOptions.Singleline);

        internal static void Audit(string assetPath, XDocument document,
            Func<XElement, IReadOnlyDictionary<string, string>> resolveAuthoredStyle,
            Func<XElement, IReadOnlyDictionary<string, string>, bool> hasVisualBoxContract,
            VmAutomationUxmlLayoutAuditReport report, bool includeSuppressed)
        {
            if (document?.Root == null)
            {
                return;
            }

            AuditFixedLocalizedButtonWidths(assetPath, document, resolveAuthoredStyle,
                report, includeSuppressed);
            AuditFixedContentLabelHeights(assetPath, document, resolveAuthoredStyle,
                report, includeSuppressed);
            AuditFixedNaturalCrossSizes(assetPath, document, resolveAuthoredStyle,
                report, includeSuppressed);
            AuditScrollAxisFlexShrink(assetPath, document, resolveAuthoredStyle,
                report, includeSuppressed);

            foreach (var parent in document.Root.DescendantsAndSelf())
            {
                var absoluteFlowStackKeys = AuditAbsoluteFlowStacks(assetPath, parent,
                    resolveAuthoredStyle, report, includeSuppressed);
                var candidates = parent.Elements()
                    .Select(element => CreateCandidate(element, resolveAuthoredStyle,
                        hasVisualBoxContract))
                    .Where(candidate => candidate != null)
                    .ToList();
                if (candidates.Count < 2)
                {
                    continue;
                }

                var emittedGroups = new HashSet<string>(StringComparer.Ordinal);
                var classGroups = candidates
                    .SelectMany(candidate => candidate.ClassNames.Select(className =>
                        new { ClassName = className, Candidate = candidate }))
                    .GroupBy(item => item.ClassName, StringComparer.Ordinal)
                    .OrderBy(group => group.Key, StringComparer.Ordinal);
                foreach (var classGroup in classGroups)
                {
                    var signatureGroups = classGroup
                        .Select(item => item.Candidate)
                        .GroupBy(candidate => candidate.LayoutSignature,
                            StringComparer.Ordinal);
                    foreach (var signatureGroup in signatureGroups)
                    {
                        var siblings = signatureGroup
                            .Distinct()
                            .OrderBy(candidate => candidate.Line)
                            .ToList();
                        if (siblings.Count < 2 ||
                            TryGetSequenceAxis(siblings, out var axis) == false)
                        {
                            continue;
                        }

                        var groupKey = axis + "|" + string.Join(",",
                            siblings.Select(candidate => candidate.Line));
                        if (absoluteFlowStackKeys.Contains(groupKey) ||
                            emittedGroups.Add(groupKey) == false)
                        {
                            continue;
                        }

                        RecordIssue(assetPath, parent, classGroup.Key, siblings, axis,
                            report, includeSuppressed);
                    }
                }
            }
        }

        internal static IReadOnlyList<Dictionary<string, object>> RunSelfTests()
        {
            var cases = new List<Dictionary<string, object>>();
            const string manualColumns =
                "<ui:VisualElement name=\"Tree\">" +
                "<ui:VisualElement class=\"manual-column\" style=\"left: 21px; top: 57px;\">" +
                "<ui:VisualElement class=\"node\"/></ui:VisualElement>" +
                "<ui:VisualElement class=\"manual-column\" style=\"left: 102px; top: 57px;\">" +
                "<ui:VisualElement class=\"node\"/></ui:VisualElement>" +
                "<ui:VisualElement class=\"manual-column\" style=\"left: 183px; top: 57px;\">" +
                "<ui:VisualElement class=\"node\"/></ui:VisualElement>" +
                "</ui:VisualElement>";

            var manualReport = AuditSelfTestFixture(manualColumns, false);
            AddSelfTestCase(cases, "manual absolute columns are an error",
                manualReport.ErrorCount == 1 &&
                manualReport.Issues.Single().Kind ==
                ABSOLUTE_FLOW_STACK_KIND &&
                manualReport.Issues.Single().Axis == "horizontal" &&
                manualReport.Issues.Single().AuthoredUsageCount == 3);

            const string mixedVerticalStack =
                "<ui:VisualElement name=\"Panel\">" +
                "<ui:ScrollView name=\"Offers\" style=\"position: absolute; top: 10px; height: 100px;\"/>" +
                "<ui:Button name=\"Refresh\" text=\"Refresh\" style=\"position: absolute; top: 120px; height: 30px;\"/>" +
                "<ui:Label name=\"Hint\" text=\"Hint\" style=\"position: absolute; top: 160px; height: 20px;\"/>" +
                "</ui:VisualElement>";
            var mixedVerticalStackReport = AuditSelfTestFixture(mixedVerticalStack, false);
            AddSelfTestCase(cases, "mixed absolute vertical flow stack is an error",
                mixedVerticalStackReport.ErrorCount == 1 &&
                mixedVerticalStackReport.Issues.Single().Kind ==
                ABSOLUTE_FLOW_STACK_KIND &&
                mixedVerticalStackReport.Issues.Single().Axis == "vertical" &&
                mixedVerticalStackReport.Issues.Single().AuthoredUsageCount == 3);

            const string legacyPair =
                "<ui:VisualElement name=\"Tree\">" +
                "<ui:VisualElement class=\"manual-column\" style=\"left: 21px; top: 57px;\">" +
                "<ui:VisualElement class=\"node\"/></ui:VisualElement>" +
                "<ui:VisualElement class=\"manual-column\" style=\"left: 102px; top: 57px;\">" +
                "<ui:VisualElement class=\"node\"/></ui:VisualElement>" +
                "</ui:VisualElement>";
            var legacyPairReport = AuditSelfTestFixture(legacyPair, false);
            AddSelfTestCase(cases, "two matching absolute siblings retain the focused warning",
                legacyPairReport.WarningCount == 1 &&
                legacyPairReport.Issues.Single().Kind ==
                "manual-absolute-sibling-layout");

            var naturalFlowReport = AuditSelfTestFixture(
                manualColumns.Replace("manual-column", "flow-column")
                    .Replace(" style=\"left: 21px; top: 57px;\"", "")
                    .Replace(" style=\"left: 102px; top: 57px;\"", "")
                    .Replace(" style=\"left: 183px; top: 57px;\"", ""),
                false);
            AddSelfTestCase(cases, "natural flex columns pass",
                naturalFlowReport.WarningCount == 0);

            const string fixedNaturalRow =
                "<ui:VisualElement name=\"Tree\" style=\"flex-direction: row; " +
                "align-items: flex-start; height: 458px;\">" +
                "<ui:VisualElement class=\"flow-column\">" +
                "<ui:VisualElement class=\"node\"/></ui:VisualElement>" +
                "<ui:VisualElement class=\"flow-column\">" +
                "<ui:VisualElement class=\"node\"/></ui:VisualElement>" +
                "</ui:VisualElement>";
            var fixedNaturalRowReport = AuditSelfTestFixture(fixedNaturalRow, false);
            AddSelfTestCase(cases, "fixed cross size on natural flex row warns",
                fixedNaturalRowReport.WarningCount == 1 &&
                fixedNaturalRowReport.Issues.Single().Kind ==
                "fixed-natural-flow-cross-size" &&
                fixedNaturalRowReport.Issues.Single().Axis == "vertical" &&
                fixedNaturalRowReport.Issues.Single().Size == 458);

            var fixedDefaultColumnReport = AuditSelfTestFixture(
                "<ui:VisualElement style=\"align-items: flex-start; width: 180px;\">" +
                "<ui:VisualElement class=\"node\"/>" +
                "<ui:VisualElement class=\"node\"/></ui:VisualElement>", false);
            AddSelfTestCase(cases, "VisualElement default column fixed width warns",
                fixedDefaultColumnReport.WarningCount == 1 &&
                fixedDefaultColumnReport.Issues.Single().Axis == "horizontal");

            var intrinsicRowReport = AuditSelfTestFixture(
                fixedNaturalRow.Replace(" height: 458px;", ""), false);
            AddSelfTestCase(cases, "intrinsically sized flex row passes",
                intrinsicRowReport.WarningCount == 0);

            var stretchedRowReport = AuditSelfTestFixture(
                fixedNaturalRow.Replace("align-items: flex-start;", "align-items: stretch;"),
                false);
            AddSelfTestCase(cases, "stretched fixed flex region passes",
                stretchedRowReport.WarningCount == 0);

            var fixedVisualRegionReport = AuditSelfTestFixture(
                fixedNaturalRow.Replace("height: 458px;",
                    "height: 458px; background-color: white;"), false);
            AddSelfTestCase(cases, "visually owned fixed flex region passes",
                fixedVisualRegionReport.WarningCount == 0);

            var fixedAbsoluteCanvasReport = AuditSelfTestFixture(
                "<ui:VisualElement name=\"Tree\" style=\"flex-direction: row; " +
                "align-items: flex-start; height: 458px;\">" +
                "<ui:VisualElement style=\"position: absolute; left: 0; top: 0; " +
                "width: 60px; height: 60px;\"/>" +
                "<ui:VisualElement style=\"position: absolute; left: 90px; top: 0; " +
                "width: 60px; height: 60px;\"/></ui:VisualElement>", false);
            AddSelfTestCase(cases, "fixed absolute canvas passes",
                fixedAbsoluteCanvasReport.WarningCount == 0);

            var anchoredOverlayReport = AuditSelfTestFixture(
                "<ui:VisualElement name=\"Tree\" style=\"flex-direction: row; " +
                "align-items: flex-start; height: 458px;\">" +
                "<ui:VisualElement class=\"flow-column\">" +
                "<ui:VisualElement class=\"node\"/></ui:VisualElement>" +
                "<ui:VisualElement class=\"flow-column\">" +
                "<ui:VisualElement class=\"node\"/></ui:VisualElement>" +
                "<ui:VisualElement style=\"position: absolute; top: 0; bottom: 0;\"/>" +
                "</ui:VisualElement>", false);
            AddSelfTestCase(cases, "cross-axis anchored overlay fixed region passes",
                anchoredOverlayReport.WarningCount == 0);

            var suppressedNaturalSizeReport = AuditSelfTestFixture(
                $"<!-- {FIXED_NATURAL_CROSS_SIZE_SUPPRESSION_MARKER} " +
                "fixture owns a measured viewport -->" + fixedNaturalRow, true);
            AddSelfTestCase(cases, "reasoned fixed natural size suppression is retained",
                suppressedNaturalSizeReport.WarningCount == 0 &&
                suppressedNaturalSizeReport.SuppressedCount == 1 &&
                suppressedNaturalSizeReport.Issues.Single().Suppressed);

            const string fixedLocalizedButton =
                "<ui:Button name=\"Refresh\" style=\"width: 344px; " +
                "padding-left: 50px; padding-right: 50px;\">" +
                "<ui:Label><Bindings><UnityEngine.Localization.LocalizedString " +
                "property=\"text\" table=\"GeneralUI\" entry=\"Refresh\"/>" +
                "</Bindings></ui:Label></ui:Button>";
            var fixedLocalizedButtonReport = AuditSelfTestFixture(
                fixedLocalizedButton, false);
            AddSelfTestCase(cases,
                "fixed width localized button warns",
                fixedLocalizedButtonReport.WarningCount == 1 &&
                fixedLocalizedButtonReport.Issues.Single().Kind ==
                "fixed-localized-button-width" &&
                fixedLocalizedButtonReport.Issues.Single().Size == 344);

            var minimumLocalizedButtonReport = AuditSelfTestFixture(
                fixedLocalizedButton.Replace("width: 344px;", "min-width: 344px;"),
                false);
            AddSelfTestCase(cases,
                "minimum width localized button passes",
                minimumLocalizedButtonReport.WarningCount == 0);

            var invariantFixedButtonReport = AuditSelfTestFixture(
                "<ui:Button text=\"OK\" style=\"width: 96px;\"/>", false);
            AddSelfTestCase(cases,
                "fixed width language-invariant button passes",
                invariantFixedButtonReport.WarningCount == 0);

            var suppressedLocalizedButtonReport = AuditSelfTestFixture(
                $"<!-- {FIXED_LOCALIZED_BUTTON_WIDTH_SUPPRESSION_MARKER} " +
                "fixture owns a measured fixed artwork region -->" +
                fixedLocalizedButton, true);
            AddSelfTestCase(cases,
                "reasoned fixed localized button width suppression is retained",
                suppressedLocalizedButtonReport.WarningCount == 0 &&
                suppressedLocalizedButtonReport.SuppressedCount == 1 &&
                suppressedLocalizedButtonReport.Issues.Single().Suppressed);

            const string fixedContentLabel =
                "<ui:Label name=\"SelectionHint\" style=\"height: 38px; margin-top: 10px;\">" +
                "<Bindings><UnityEngine.Localization.LocalizedString property=\"text\" " +
                "table=\"GeneralUI\" entry=\"ForgeSelectionHint\"/></Bindings></ui:Label>";
            var fixedContentLabelReport = AuditSelfTestFixture(fixedContentLabel, false);
            AddSelfTestCase(cases, "localized content Label fixed height warns",
                fixedContentLabelReport.WarningCount == 1 &&
                fixedContentLabelReport.Issues.Single().Kind ==
                "fixed-content-label-height" &&
                fixedContentLabelReport.Issues.Single().Size == 38);

            var invariantContentLabelReport = AuditSelfTestFixture(
                "<ui:Label text=\"Hint\" style=\"height: 38px;\"/>", false);
            AddSelfTestCase(cases, "invariant content Label fixed height warns",
                invariantContentLabelReport.WarningCount == 1 &&
                invariantContentLabelReport.Issues.Single().Kind ==
                "fixed-content-label-height");

            var minimumContentLabelReport = AuditSelfTestFixture(
                fixedContentLabel.Replace("height: 38px;", "min-height: 38px;"), false);
            AddSelfTestCase(cases, "minimum content Label height passes",
                minimumContentLabelReport.WarningCount == 0);

            var spacedContentLabelReport = AuditSelfTestFixture(
                fixedContentLabel.Replace("height: 38px;",
                    "padding-top: 5px; padding-bottom: 5px;"), false);
            AddSelfTestCase(cases, "padded content Label passes",
                spacedContentLabelReport.WarningCount == 0);

            var visualLabelReport = AuditSelfTestFixture(
                fixedContentLabel.Replace("height: 38px;",
                    "height: 38px; background-color: white;"), false);
            AddSelfTestCase(cases, "visually owned Label height passes",
                visualLabelReport.WarningCount == 0);

            var absoluteLabelReport = AuditSelfTestFixture(
                fixedContentLabel.Replace("height: 38px;",
                    "position: absolute; height: 38px;"), false);
            AddSelfTestCase(cases, "absolute overlay Label height passes",
                absoluteLabelReport.WarningCount == 0);

            var suppressedContentLabelReport = AuditSelfTestFixture(
                $"<!-- {FIXED_CONTENT_LABEL_HEIGHT_SUPPRESSION_MARKER} " +
                "fixture owns a measured interaction region -->" +
                fixedContentLabel, true);
            AddSelfTestCase(cases, "reasoned fixed content Label height suppression is retained",
                suppressedContentLabelReport.WarningCount == 0 &&
                suppressedContentLabelReport.SuppressedCount == 1 &&
                suppressedContentLabelReport.Issues.Single().Suppressed);

            const string scrollAxisShrink =
                "<ui:ScrollView name=\"Trees\">" +
                "<ui:VisualElement name=\"Tree\" style=\"flex-shrink: 0;\"/>" +
                "</ui:ScrollView>";
            var scrollAxisShrinkReport = AuditSelfTestFixture(scrollAxisShrink, false);
            AddSelfTestCase(cases,
                "vertical ScrollView direct content flex shrink warns",
                scrollAxisShrinkReport.WarningCount == 1 &&
                scrollAxisShrinkReport.Issues.Single().Kind ==
                "ineffective-scroll-axis-flex-shrink" &&
                scrollAxisShrinkReport.Issues.Single().Axis == "vertical");

            var horizontalScrollShrinkReport = AuditSelfTestFixture(
                scrollAxisShrink.Replace("name=\"Trees\"",
                    "name=\"Trees\" mode=\"Horizontal\""), false);
            AddSelfTestCase(cases,
                "horizontal ScrollView is outside the vertical shrink proof",
                horizontalScrollShrinkReport.WarningCount == 0);

            var ordinaryParentShrinkReport = AuditSelfTestFixture(
                "<ui:VisualElement><ui:VisualElement " +
                "style=\"flex-shrink: 0;\"/></ui:VisualElement>", false);
            AddSelfTestCase(cases,
                "ordinary flex parent shrink is retained",
                ordinaryParentShrinkReport.WarningCount == 0);

            var suppressedScrollShrinkReport = AuditSelfTestFixture(
                "<ui:ScrollView>" +
                $"<!-- {SCROLL_AXIS_FLEX_SHRINK_SUPPRESSION_MARKER} " +
                "fixture owns a custom constrained content container -->" +
                "<ui:VisualElement style=\"flex-shrink: 0;\"/>" +
                "</ui:ScrollView>", true);
            AddSelfTestCase(cases,
                "reasoned scroll-axis shrink suppression is retained",
                suppressedScrollShrinkReport.WarningCount == 0 &&
                suppressedScrollShrinkReport.SuppressedCount == 1 &&
                suppressedScrollShrinkReport.Issues.Single().Suppressed);

            const string overlappingVisualLayers =
                "<ui:VisualElement name=\"Canvas\">" +
                "<ui:VisualElement style=\"position: absolute; left: 0; top: 0; width: 60px; height: 60px; background-color: white;\"/>" +
                "<ui:Button text=\"Layer\" style=\"position: absolute; left: 0; top: 0; width: 60px; height: 60px;\"/>" +
                "<ui:Label text=\"Layer\" style=\"position: absolute; left: 0; top: 0; width: 60px; height: 60px;\"/>" +
                "</ui:VisualElement>";
            var visualLayersReport = AuditSelfTestFixture(overlappingVisualLayers, false);
            AddSelfTestCase(cases, "overlapping absolute visual layers pass",
                visualLayersReport.ErrorCount == 0 &&
                visualLayersReport.WarningCount == 0);

            var suppressedReport = AuditSelfTestFixture(
                $"<!-- {SUPPRESSION_MARKER} fixture owns authored overlay coordinates -->" +
                manualColumns,
                true);
            AddSelfTestCase(cases, "reasoned manual sibling suppression is retained",
                suppressedReport.WarningCount == 0 &&
                suppressedReport.SuppressedCount == 1 &&
                suppressedReport.Issues.Single().Suppressed);

            return cases;
        }

        private static HashSet<string> AuditAbsoluteFlowStacks(string assetPath,
            XElement parent,
            Func<XElement, IReadOnlyDictionary<string, string>> resolveAuthoredStyle,
            VmAutomationUxmlLayoutAuditReport report, bool includeSuppressed)
        {
            var emittedGroups = new HashSet<string>(StringComparer.Ordinal);
            AuditAbsoluteFlowStackAxis(assetPath, parent, "horizontal", "left", "right",
                "width", resolveAuthoredStyle, report, includeSuppressed, emittedGroups);
            AuditAbsoluteFlowStackAxis(assetPath, parent, "vertical", "top", "bottom",
                "height", resolveAuthoredStyle, report, includeSuppressed, emittedGroups);
            return emittedGroups;
        }

        private static void AuditAbsoluteFlowStackAxis(string assetPath, XElement parent,
            string axis, string leadingProperty, string trailingProperty, string sizeProperty,
            Func<XElement, IReadOnlyDictionary<string, string>> resolveAuthoredStyle,
            VmAutomationUxmlLayoutAuditReport report, bool includeSuppressed,
            ISet<string> emittedGroups)
        {
            var run = new List<AbsoluteFlowStackCandidate>();
            foreach (var element in parent.Elements().Where(IsVisualContentElement))
            {
                var candidate = CreateAbsoluteFlowStackCandidate(element, leadingProperty,
                    trailingProperty, sizeProperty, resolveAuthoredStyle);
                if (candidate == null)
                {
                    continue;
                }

                if (run.Count > 0 &&
                    candidate.Offset < run[run.Count - 1].End - LayoutEpsilon)
                {
                    RecordAbsoluteFlowStackIssue(assetPath, parent, axis, leadingProperty,
                        sizeProperty, run, report, includeSuppressed, emittedGroups);
                    run.Clear();
                }

                run.Add(candidate);
            }

            RecordAbsoluteFlowStackIssue(assetPath, parent, axis, leadingProperty,
                sizeProperty, run, report, includeSuppressed, emittedGroups);
        }

        private static AbsoluteFlowStackCandidate CreateAbsoluteFlowStackCandidate(
            XElement element, string leadingProperty, string trailingProperty,
            string sizeProperty,
            Func<XElement, IReadOnlyDictionary<string, string>> resolveAuthoredStyle)
        {
            var style = resolveAuthoredStyle(element);
            if (StyleValue(style, "position") != "absolute" ||
                StyleValue(style, "display") == "none" ||
                style.ContainsKey(trailingProperty) ||
                TryGetPixels(style, leadingProperty, out var offset) == false ||
                TryGetPixels(style, sizeProperty, out var size) == false ||
                offset < 0 || size <= 0)
            {
                return null;
            }

            return new AbsoluteFlowStackCandidate
            {
                Element = element,
                Line = GetLineNumber(element),
                Offset = offset,
                Size = size
            };
        }

        private static void RecordAbsoluteFlowStackIssue(string assetPath, XElement parent,
            string axis, string leadingProperty, string sizeProperty,
            IReadOnlyList<AbsoluteFlowStackCandidate> candidates,
            VmAutomationUxmlLayoutAuditReport report, bool includeSuppressed,
            ISet<string> emittedGroups)
        {
            if (candidates.Count < MinimumAbsoluteFlowStackSize)
            {
                return;
            }

            var groupKey = axis + "|" + string.Join(",",
                candidates.Select(candidate => candidate.Line));
            if (emittedGroups.Add(groupKey) == false)
            {
                return;
            }

            var parentName = AttributeValue(parent, "name");
            var parentLabel = string.IsNullOrWhiteSpace(parentName)
                ? $"<{parent.Name.LocalName}>"
                : $"#{parentName}";
            var suppressionReason = GetSuppressionReason(parent);
            if (string.IsNullOrWhiteSpace(suppressionReason))
            {
                suppressionReason = GetSuppressionReason(candidates[0].Element);
            }

            var issue = new VmAutomationUxmlLayoutAuditIssue
            {
                AssetPath = assetPath,
                Line = GetLineNumber(parent),
                Element = parentLabel,
                ElementName = parentName,
                Kind = ABSOLUTE_FLOW_STACK_KIND,
                Severity = "error",
                Axis = axis,
                AuthoredUsageCount = candidates.Count,
                FixedProperties = new List<string>
                {
                    "position",
                    leadingProperty,
                    sizeProperty
                },
                Suppressed = string.IsNullOrWhiteSpace(suppressionReason) == false,
                SuppressionReason = suppressionReason,
                Message =
                    $"{parentLabel} manually arranges {candidates.Count} direct children as a " +
                    $"non-overlapping {axis} sequence using position: absolute plus authored " +
                    $"{leadingProperty}/{sizeProperty} values. Make the parent the Flex flow " +
                    "owner and keep sequential content in normal flow. Retain absolute positioning " +
                    "only for real overlays, edge chrome, popups, or canvas geometry and document " +
                    $"that contract with a reasoned '{SUPPRESSION_MARKER}' marker."
            };
            foreach (var candidate in candidates)
            {
                var name = AttributeValue(candidate.Element, "name");
                issue.UsageLocations.Add(new Dictionary<string, object>
                {
                    { "element", string.IsNullOrWhiteSpace(name)
                        ? $"<{candidate.Element.Name.LocalName}>"
                        : $"#{name}" },
                    { "elementName", name },
                    { "line", candidate.Line },
                    { "offset", candidate.Offset },
                    { "size", candidate.Size }
                });
            }

            report.Record(issue, includeSuppressed);
        }

        private static void AuditScrollAxisFlexShrink(string assetPath,
            XDocument document,
            Func<XElement, IReadOnlyDictionary<string, string>> resolveAuthoredStyle,
            VmAutomationUxmlLayoutAuditReport report, bool includeSuppressed)
        {
            foreach (var scrollView in document.Root.DescendantsAndSelf()
                         .Where(IsDefaultVerticalScrollView))
            {
                foreach (var child in scrollView.Elements().Where(IsVisualContentElement))
                {
                    var style = resolveAuthoredStyle(child);
                    if (style.TryGetValue("flex-shrink", out var flexShrink) == false ||
                        string.IsNullOrWhiteSpace(flexShrink))
                    {
                        continue;
                    }

                    var name = AttributeValue(child, "name");
                    var elementLabel = string.IsNullOrWhiteSpace(name)
                        ? $"<{child.Name.LocalName}>"
                        : $"#{name}";
                    var suppressionReason = GetSuppressionReason(child,
                        ScrollAxisFlexShrinkSuppressionRegex);
                    var issue = new VmAutomationUxmlLayoutAuditIssue
                    {
                        AssetPath = assetPath,
                        Line = GetLineNumber(child),
                        Element = elementLabel,
                        ElementName = name,
                        Kind = "ineffective-scroll-axis-flex-shrink",
                        Axis = "vertical",
                        FixedProperties = new List<string> { "flex-shrink" },
                        InlineDeclarations = new Dictionary<string, string>(
                            StringComparer.OrdinalIgnoreCase)
                        {
                            { "flex-shrink", flexShrink }
                        },
                        Suppressed = string.IsNullOrWhiteSpace(suppressionReason) == false,
                        SuppressionReason = suppressionReason,
                        Message =
                            $"{elementLabel} authors flex-shrink: {flexShrink} as direct " +
                            "content of a default vertical ScrollView. Its generated content " +
                            "container expands on the vertical scroll axis, so no finite Flex " +
                            "line exists for shrink to resolve. Remove flex-shrink from the " +
                            "element or its USS class; retain it only when a custom content " +
                            "container intentionally constrains that axis and document the " +
                            "contract with a reasoned suppression."
                    };
                    report.Record(issue, includeSuppressed);
                }
            }
        }

        private static bool IsDefaultVerticalScrollView(XElement element)
        {
            if (element.Name.LocalName != "ScrollView")
            {
                return false;
            }

            var mode = AttributeValue(element, "mode");
            return string.IsNullOrWhiteSpace(mode) ||
                   string.Equals(mode, "Vertical",
                       StringComparison.OrdinalIgnoreCase);
        }

        private static void AuditFixedNaturalCrossSizes(string assetPath,
            XDocument document,
            Func<XElement, IReadOnlyDictionary<string, string>> resolveAuthoredStyle,
            VmAutomationUxmlLayoutAuditReport report, bool includeSuppressed)
        {
            foreach (var element in document.Root.DescendantsAndSelf()
                         .Where(candidate => candidate.Name.LocalName == "VisualElement"))
            {
                var style = resolveAuthoredStyle(element);
                if (StyleValue(style, "position") == "absolute" ||
                    StyleValue(style, "display") == "none" ||
                    HasIndependentNaturalSizeContract(element, style))
                {
                    continue;
                }

                var flexDirection = StyleValue(style, "flex-direction");
                if (string.IsNullOrWhiteSpace(flexDirection))
                {
                    flexDirection = "column";
                }

                var horizontalFlow = flexDirection == "row" ||
                                     flexDirection == "row-reverse";
                var verticalFlow = flexDirection == "column" ||
                                   flexDirection == "column-reverse";
                if (horizontalFlow == false && verticalFlow == false)
                {
                    continue;
                }

                var flexWrap = StyleValue(style, "flex-wrap");
                if (string.IsNullOrWhiteSpace(flexWrap) == false &&
                    flexWrap != "nowrap")
                {
                    continue;
                }

                var alignment = StyleValue(style, "align-items");
                if (alignment != "flex-start" && alignment != "center" &&
                    alignment != "flex-end")
                {
                    continue;
                }

                var crossSizeProperty = horizontalFlow ? "height" : "width";
                if (TryGetPixels(style, crossSizeProperty, out var crossSize) == false ||
                    crossSize <= 0 ||
                    HasCrossAxisAnchoredAbsoluteChild(element, crossSizeProperty,
                        resolveAuthoredStyle))
                {
                    continue;
                }

                var inFlowChildren = element.Elements()
                    .Where(IsVisualContentElement)
                    .Where(child =>
                    {
                        var childStyle = resolveAuthoredStyle(child);
                        return StyleValue(childStyle, "position") != "absolute" &&
                               StyleValue(childStyle, "display") != "none";
                    })
                    .ToList();
                if (inFlowChildren.Count < 2 ||
                    inFlowChildren.All(child => EstablishesNaturalAxisSize(child,
                        crossSizeProperty, resolveAuthoredStyle)) == false)
                {
                    continue;
                }

                RecordFixedNaturalCrossSizeIssue(assetPath, element, crossSizeProperty,
                    crossSize, inFlowChildren.Count, report, includeSuppressed);
            }
        }

        private static bool EstablishesNaturalAxisSize(XElement element,
            string sizeProperty,
            Func<XElement, IReadOnlyDictionary<string, string>> resolveAuthoredStyle)
        {
            var style = resolveAuthoredStyle(element);
            if (StyleValue(style, "position") == "absolute" ||
                StyleValue(style, "display") == "none" ||
                StyleValue(style, "align-self") == "stretch")
            {
                return false;
            }

            if (TryGetPixels(style, sizeProperty, out var size) && size > 0)
            {
                return true;
            }

            if (style.TryGetValue(sizeProperty, out var authoredSize) &&
                string.IsNullOrWhiteSpace(authoredSize) == false &&
                string.Equals(authoredSize.Trim(), "auto",
                    StringComparison.OrdinalIgnoreCase) == false)
            {
                return false;
            }

            return element.Elements()
                .Where(IsVisualContentElement)
                .Any(child => EstablishesNaturalAxisSize(child, sizeProperty,
                    resolveAuthoredStyle));
        }

        private static bool HasCrossAxisAnchoredAbsoluteChild(XElement element,
            string crossSizeProperty,
            Func<XElement, IReadOnlyDictionary<string, string>> resolveAuthoredStyle)
        {
            var leading = crossSizeProperty == "width" ? "left" : "top";
            var trailing = crossSizeProperty == "width" ? "right" : "bottom";
            return element.Elements().Where(IsVisualContentElement).Any(child =>
            {
                var style = resolveAuthoredStyle(child);
                return StyleValue(style, "position") == "absolute" &&
                       style.ContainsKey(leading) && style.ContainsKey(trailing);
            });
        }

        private static bool HasIndependentNaturalSizeContract(XElement element,
            IReadOnlyDictionary<string, string> style)
        {
            if (style.Any(property => IsVisualOrClippingProperty(property.Key,
                    property.Value)) ||
                element.Elements().Any(child => child.Name.LocalName == "Bindings") ||
                string.Equals(AttributeValue(element, "focusable"), "true",
                    StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(AttributeValue(element, "tabindex")) == false ||
                string.IsNullOrWhiteSpace(AttributeValue(element, "tooltip")) == false ||
                string.Equals(AttributeValue(element, "picking-mode"), "Position",
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }

        private static bool IsVisualOrClippingProperty(string property, string value)
        {
            property = (property ?? "").Trim().ToLowerInvariant();
            value = (value ?? "").Trim().ToLowerInvariant();
            if (property == "background-image" ||
                property.StartsWith("-unity-background", StringComparison.Ordinal))
            {
                return value != "none" && value != "initial";
            }

            if (property == "background-color")
            {
                return value != "transparent" && value != "initial" &&
                       Regex.IsMatch(value,
                           @"^rgba\([^,]+,[^,]+,[^,]+,\s*0(?:\.0+)?\)$") == false;
            }

            if (property.StartsWith("border-", StringComparison.Ordinal))
            {
                return property.EndsWith("-width", StringComparison.Ordinal) == false ||
                       Regex.IsMatch(value, @"^[+-]?0(?:\.0+)?(?:px)?$") == false;
            }

            return property == "overflow" && value != "visible";
        }

        private static void RecordFixedNaturalCrossSizeIssue(string assetPath,
            XElement element, string crossSizeProperty, float crossSize,
            int inFlowChildCount, VmAutomationUxmlLayoutAuditReport report,
            bool includeSuppressed)
        {
            var name = AttributeValue(element, "name");
            var elementLabel = string.IsNullOrWhiteSpace(name)
                ? "<VisualElement>"
                : $"#{name}";
            var axis = crossSizeProperty == "width" ? "horizontal" : "vertical";
            var suppressionReason = GetSuppressionReason(element,
                FixedNaturalCrossSizeSuppressionRegex);
            var issue = new VmAutomationUxmlLayoutAuditIssue
            {
                AssetPath = assetPath,
                Line = GetLineNumber(element),
                Element = elementLabel,
                ElementName = name,
                Kind = "fixed-natural-flow-cross-size",
                Axis = axis,
                FixedProperties = new List<string> { crossSizeProperty },
                AuthoredUsageCount = inFlowChildCount,
                Size = crossSize,
                Suppressed = string.IsNullOrWhiteSpace(suppressionReason) == false,
                SuppressionReason = suppressionReason,
                Message =
                    $"{elementLabel} fixes its {axis} cross size to {crossSizeProperty}: " +
                    $"{crossSize.ToString("0.###", CultureInfo.InvariantCulture)}px even though " +
                    $"{inFlowChildCount} in-flow children can establish that extent naturally. " +
                    $"Remove the fixed {crossSizeProperty} and let the Flex content size the " +
                    "layout-only container. Keep a fixed cross size only for an independently " +
                    "owned visual, clipping, interaction, or anchored-overlay region and document " +
                    "that contract with a reasoned suppression."
            };
            report.Record(issue, includeSuppressed);
        }

        private static AbsoluteLayoutCandidate CreateCandidate(XElement element,
            Func<XElement, IReadOnlyDictionary<string, string>> resolveAuthoredStyle,
            Func<XElement, IReadOnlyDictionary<string, string>, bool> hasVisualBoxContract)
        {
            if (element.Name.LocalName != "VisualElement")
            {
                return null;
            }

            var style = resolveAuthoredStyle(element);
            if (StyleValue(style, "position") != "absolute" ||
                HasIndependentContainerContract(element, style, hasVisualBoxContract))
            {
                return null;
            }

            var flexDirection = StyleValue(style, "flex-direction");
            if (flexDirection != "column" && flexDirection != "row")
            {
                return null;
            }

            var crossSizeProperty = flexDirection == "column" ? "width" : "height";
            if (TryGetPixels(style, crossSizeProperty, out var crossSize) == false ||
                crossSize <= 0 ||
                TryGetPixels(style, "left", out var left) == false ||
                TryGetPixels(style, "top", out var top) == false ||
                IsCrossSizeEstablishedByChildren(element, crossSizeProperty, crossSize,
                    resolveAuthoredStyle) == false)
            {
                return null;
            }

            var classNames = SplitWhitespace(AttributeValue(element, "class"))
                .Distinct(StringComparer.Ordinal)
                .ToList();
            if (classNames.Count == 0)
            {
                return null;
            }

            return new AbsoluteLayoutCandidate
            {
                Element = element,
                Line = GetLineNumber(element),
                Left = left,
                Top = top,
                CrossSize = crossSize,
                CrossSizeProperty = crossSizeProperty,
                FlexDirection = flexDirection,
                ClassNames = classNames
            };
        }

        private static bool IsCrossSizeEstablishedByChildren(XElement element,
            string crossSizeProperty, float containerCrossSize,
            Func<XElement, IReadOnlyDictionary<string, string>> resolveAuthoredStyle)
        {
            var matchingChildFound = false;
            foreach (var child in element.Elements().Where(IsVisualContentElement))
            {
                var childStyle = resolveAuthoredStyle(child);
                if (StyleValue(childStyle, "position") == "absolute")
                {
                    continue;
                }

                if (TryGetPixels(childStyle, crossSizeProperty, out var childCrossSize) == false)
                {
                    return false;
                }

                var horizontal = crossSizeProperty == "width";
                var leadingMargin = horizontal ? "margin-left" : "margin-top";
                var trailingMargin = horizontal ? "margin-right" : "margin-bottom";
                childCrossSize += PixelValueOrZero(childStyle, leadingMargin) +
                                  PixelValueOrZero(childStyle, trailingMargin);
                if (childCrossSize > containerCrossSize + LayoutEpsilon)
                {
                    return false;
                }

                if (Math.Abs(childCrossSize - containerCrossSize) <= LayoutEpsilon)
                {
                    matchingChildFound = true;
                }
            }

            return matchingChildFound;
        }

        private static bool HasIndependentContainerContract(XElement element,
            IReadOnlyDictionary<string, string> style,
            Func<XElement, IReadOnlyDictionary<string, string>, bool> hasVisualBoxContract)
        {
            if (hasVisualBoxContract(element, style) ||
                element.Elements().Any(child => child.Name.LocalName == "Bindings") ||
                string.Equals(AttributeValue(element, "focusable"), "true",
                    StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrWhiteSpace(AttributeValue(element, "tabindex")) == false ||
                string.IsNullOrWhiteSpace(AttributeValue(element, "tooltip")) == false ||
                string.Equals(AttributeValue(element, "picking-mode"), "Position",
                    StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            return false;
        }

        private static bool TryGetSequenceAxis(
            IReadOnlyCollection<AbsoluteLayoutCandidate> candidates, out string axis)
        {
            axis = "";
            var sameTop = candidates.Max(candidate => candidate.Top) -
                          candidates.Min(candidate => candidate.Top) <= LayoutEpsilon;
            var distinctLeft = candidates.Max(candidate => candidate.Left) -
                               candidates.Min(candidate => candidate.Left) > LayoutEpsilon;
            if (sameTop && distinctLeft)
            {
                axis = "horizontal";
                return true;
            }

            var sameLeft = candidates.Max(candidate => candidate.Left) -
                           candidates.Min(candidate => candidate.Left) <= LayoutEpsilon;
            var distinctTop = candidates.Max(candidate => candidate.Top) -
                              candidates.Min(candidate => candidate.Top) > LayoutEpsilon;
            if (sameLeft && distinctTop)
            {
                axis = "vertical";
                return true;
            }

            return false;
        }

        private static void RecordIssue(string assetPath, XElement parent,
            string sharedClass, IReadOnlyList<AbsoluteLayoutCandidate> candidates,
            string axis, VmAutomationUxmlLayoutAuditReport report, bool includeSuppressed)
        {
            var first = candidates[0];
            var parentName = AttributeValue(parent, "name");
            var parentLabel = string.IsNullOrWhiteSpace(parentName)
                ? $"<{parent.Name.LocalName}>"
                : $"#{parentName}";
            var suppressionReason = GetSuppressionReason(parent);
            if (string.IsNullOrWhiteSpace(suppressionReason))
            {
                suppressionReason = GetSuppressionReason(first.Element);
            }

            var sequenceProperty = axis == "horizontal" ? "left" : "top";
            var fixedProperties = new List<string>
            {
                "position",
                sequenceProperty,
                axis == "horizontal" ? "top" : "left",
                first.CrossSizeProperty
            };
            var issue = new VmAutomationUxmlLayoutAuditIssue
            {
                AssetPath = assetPath,
                Line = GetLineNumber(parent),
                Element = parentLabel,
                ElementName = parentName,
                Kind = "manual-absolute-sibling-layout",
                Axis = axis,
                BaseClass = sharedClass,
                AuthoredUsageCount = candidates.Count,
                FixedProperties = fixedProperties,
                Size = first.CrossSize,
                Suppressed = string.IsNullOrWhiteSpace(suppressionReason) == false,
                SuppressionReason = suppressionReason,
                Message =
                    $"{parentLabel} manually arranges {candidates.Count} layout-only " +
                    $".{sharedClass} siblings as a {axis} sequence with absolute offsets " +
                    $"and a fixed {first.CrossSizeProperty} already established by in-flow " +
                    "child content. Make the parent the flex layout owner, keep these siblings " +
                    $"in flow, and remove the redundant {first.CrossSizeProperty}; retain manual " +
                    "coordinates only for an independently owned visual, clipping, or interaction " +
                    "region and document that contract with a reasoned suppression."
            };
            foreach (var candidate in candidates.OrderBy(candidate =>
                         axis == "horizontal" ? candidate.Left : candidate.Top))
            {
                var name = AttributeValue(candidate.Element, "name");
                issue.UsageLocations.Add(new Dictionary<string, object>
                {
                    { "element", string.IsNullOrWhiteSpace(name) ? "<VisualElement>" : $"#{name}" },
                    { "elementName", name },
                    { "line", candidate.Line },
                    { "left", candidate.Left },
                    { "top", candidate.Top },
                    { "crossSizeProperty", candidate.CrossSizeProperty },
                    { "crossSize", candidate.CrossSize }
                });
            }

            report.Record(issue, includeSuppressed);
        }

        private static VmAutomationUxmlLayoutAuditReport AuditSelfTestFixture(string element,
            bool includeSuppressed)
        {
            var document = XDocument.Parse(
                "<ui:UXML xmlns:ui=\"UnityEngine.UIElements\">" + element +
                "</ui:UXML>", LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo);
            var report = new VmAutomationUxmlLayoutAuditReport(100);
            Audit("Assets/__NaturalFlowLayoutAuditSelfTest.uxml", document,
                ResolveSelfTestStyle,
                (_, style) => style.ContainsKey("background-color"),
                report, includeSuppressed);
            report.SortIssues();
            return report;
        }

        private static IReadOnlyDictionary<string, string> ResolveSelfTestStyle(
            XElement element)
        {
            var style = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var classes = SplitWhitespace(AttributeValue(element, "class")).ToList();
            if (classes.Contains("manual-column"))
            {
                style["position"] = "absolute";
                style["width"] = "60px";
                style["flex-direction"] = "column";
            }

            if (classes.Contains("flow-column"))
            {
                style["flex-direction"] = "column";
            }

            if (classes.Contains("node"))
            {
                style["width"] = "60px";
                style["height"] = "60px";
            }

            foreach (Match declaration in StyleDeclarationRegex.Matches(
                         AttributeValue(element, "style")))
            {
                style[declaration.Groups["name"].Value.Trim()] =
                    declaration.Groups["value"].Value.Trim();
            }

            return style;
        }

        private static void AddSelfTestCase(
            ICollection<Dictionary<string, object>> cases, string name, bool passed)
        {
            cases.Add(new Dictionary<string, object>
            {
                { "name", name },
                { "passed", passed }
            });
        }

        private static string GetSuppressionReason(XElement element)
        {
            return GetSuppressionReason(element, SuppressionRegex);
        }

        private static string GetSuppressionReason(XElement element, Regex suppressionRegex)
        {
            var previous = element.NodesBeforeSelf().Reverse().FirstOrDefault(node =>
                !(node is XText text) || string.IsNullOrWhiteSpace(text.Value) == false);
            if (!(previous is XComment comment))
            {
                return "";
            }

            var match = suppressionRegex.Match(comment.Value);
            return match.Success ? match.Groups["reason"].Value.Trim() : "";
        }

        private static bool IsVisualContentElement(XElement element)
        {
            switch (element.Name.LocalName)
            {
                case "Bindings":
                case "Style":
                case "Template":
                case "AttributeOverrides":
                    return false;
                default:
                    return true;
            }
        }

        private static string StyleValue(IReadOnlyDictionary<string, string> style,
            string property)
        {
            return style.TryGetValue(property, out var value)
                ? value.Trim().ToLowerInvariant()
                : "";
        }

        private static float PixelValueOrZero(IReadOnlyDictionary<string, string> style,
            string property)
        {
            return TryGetPixels(style, property, out var value) ? value : 0;
        }

        private static bool TryGetPixels(IReadOnlyDictionary<string, string> style,
            string property, out float value)
        {
            value = 0;
            if (style.TryGetValue(property, out var rawValue) == false)
            {
                return false;
            }

            var match = PixelValueRegex.Match(rawValue.Trim());
            return match.Success &&
                   float.TryParse(match.Groups["value"].Value, NumberStyles.Float,
                       CultureInfo.InvariantCulture, out value);
        }

        private static IEnumerable<string> SplitWhitespace(string value)
        {
            return (value ?? "").Split((char[])null,
                StringSplitOptions.RemoveEmptyEntries);
        }

        private static string AttributeValue(XElement element, string attributeName)
        {
            return element.Attributes().FirstOrDefault(attribute =>
                    string.Equals(attribute.Name.LocalName, attributeName,
                        StringComparison.OrdinalIgnoreCase))
                ?.Value ?? "";
        }

        private static int GetLineNumber(XObject value)
        {
            return value is IXmlLineInfo lineInfo && lineInfo.HasLineInfo()
                ? lineInfo.LineNumber
                : 1;
        }

        private sealed class AbsoluteLayoutCandidate
        {
            public XElement Element;
            public int Line;
            public float Left;
            public float Top;
            public float CrossSize;
            public string CrossSizeProperty;
            public string FlexDirection;
            public List<string> ClassNames;

            public string LayoutSignature =>
                FlexDirection + "|" + CrossSizeProperty + "|" +
                CrossSize.ToString("R", CultureInfo.InvariantCulture);
        }

        private static void AuditFixedContentLabelHeights(string assetPath,
            XDocument document,
            Func<XElement, IReadOnlyDictionary<string, string>> resolveAuthoredStyle,
            VmAutomationUxmlLayoutAuditReport report, bool includeSuppressed)
        {
            foreach (var element in document.Root.DescendantsAndSelf()
                         .Where(candidate => candidate.Name.LocalName == "Label"))
            {
                var style = resolveAuthoredStyle(element);
                if (StyleValue(style, "position") == "absolute" ||
                    StyleValue(style, "display") == "none" ||
                    TryGetPixels(style, "height", out var height) == false ||
                    height <= 0 ||
                    style.Any(property => IsVisualOrClippingProperty(property.Key,
                        property.Value)) ||
                    string.Equals(AttributeValue(element, "focusable"), "true",
                        StringComparison.OrdinalIgnoreCase) ||
                    string.IsNullOrWhiteSpace(AttributeValue(element, "tabindex")) == false ||
                    string.Equals(AttributeValue(element, "picking-mode"), "Position",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var name = AttributeValue(element, "name");
                var elementLabel = string.IsNullOrWhiteSpace(name)
                    ? "<Label>"
                    : $"#{name}";
                var suppressionReason = GetSuppressionReason(element,
                    FixedContentLabelHeightSuppressionRegex);
                report.Record(new VmAutomationUxmlLayoutAuditIssue
                {
                    AssetPath = assetPath,
                    Line = GetLineNumber(element),
                    Element = elementLabel,
                    ElementName = name,
                    Kind = "fixed-content-label-height",
                    Axis = "vertical",
                    FixedProperties = new List<string> { "height" },
                    Size = height,
                    Suppressed = string.IsNullOrWhiteSpace(suppressionReason) == false,
                    SuppressionReason = suppressionReason,
                    Message =
                        $"{elementLabel} fixes height to " +
                        $"{height.ToString("0.###", CultureInfo.InvariantCulture)}px " +
                        "although its text can determine the height naturally. Remove height; " +
                        "use margin or padding for spacing, or min-height when the text must " +
                        "be able to grow. Keep a fixed height only for a measured visual, " +
                        "clipping, or interaction contract and document it with a reasoned " +
                        $"'{FIXED_CONTENT_LABEL_HEIGHT_SUPPRESSION_MARKER}' marker."
                }, includeSuppressed);
            }
        }

        private static void AuditFixedLocalizedButtonWidths(string assetPath,
            XDocument document,
            Func<XElement, IReadOnlyDictionary<string, string>> resolveAuthoredStyle,
            VmAutomationUxmlLayoutAuditReport report, bool includeSuppressed)
        {
            foreach (var element in FindButtonsWithLocalizedText(document.Root))
            {
                var style = resolveAuthoredStyle(element);
                if (StyleValue(style, "display") == "none" ||
                    TryGetPixels(style, "width", out var width) == false ||
                    width <= 0)
                {
                    continue;
                }

                var name = AttributeValue(element, "name");
                var elementLabel = string.IsNullOrWhiteSpace(name)
                    ? $"<{element.Name.LocalName}>"
                    : $"#{name}";
                var suppressionReason = GetSuppressionReason(element,
                    FixedLocalizedButtonWidthSuppressionRegex);
                report.Record(new VmAutomationUxmlLayoutAuditIssue
                {
                    AssetPath = assetPath,
                    Line = GetLineNumber(element),
                    Element = elementLabel,
                    ElementName = name,
                    Kind = "fixed-localized-button-width",
                    Axis = "horizontal",
                    FixedProperties = new List<string> { "width" },
                    Size = width,
                    Suppressed = string.IsNullOrWhiteSpace(suppressionReason) == false,
                    SuppressionReason = suppressionReason,
                    Message =
                        $"{elementLabel} fixes width to " +
                        $"{width.ToString("0.###", CultureInfo.InvariantCulture)}px even " +
                        "though its authored text is localized and can change length by locale. " +
                        "Replace width with min-width and use horizontal padding so the text " +
                        "determines the final button width. Keep a fixed width only for a measured " +
                        "artwork, clipping, or interaction contract and document it with a " +
                        "reasoned suppression."
                }, includeSuppressed);
            }
        }

        private static IReadOnlyCollection<XElement> FindButtonsWithLocalizedText(
            XElement root)
        {
            var buttons = new HashSet<XElement>();
            var stack = new Stack<LocalizedButtonTraversalState>();
            stack.Push(new LocalizedButtonTraversalState(root, null, false));
            while (stack.Count > 0)
            {
                var state = stack.Pop();
                var element = state.Element;
                var localName = element.Name.LocalName;
                var button = localName.EndsWith("Button", StringComparison.Ordinal)
                    ? element
                    : state.Button;
                var insideBindings = state.InsideBindings || localName == "Bindings";
                var localizedString = localName == "LocalizedString" ||
                                      localName.EndsWith(".LocalizedString",
                                          StringComparison.Ordinal);
                if (insideBindings && button != null && localizedString &&
                    (string.Equals(AttributeValue(element, "property"), "text",
                         StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(AttributeValue(element, "property"), "label",
                         StringComparison.OrdinalIgnoreCase)))
                {
                    buttons.Add(button);
                }

                foreach (var child in element.Elements().Reverse())
                {
                    stack.Push(new LocalizedButtonTraversalState(child, button,
                        insideBindings));
                }
            }

            return buttons;
        }

        private sealed class LocalizedButtonTraversalState
        {
            public readonly XElement Element;
            public readonly XElement Button;
            public readonly bool InsideBindings;

            public LocalizedButtonTraversalState(XElement element, XElement button,
                bool insideBindings)
            {
                Element = element;
                Button = button;
                InsideBindings = insideBindings;
            }
        }

        private sealed class AbsoluteFlowStackCandidate
        {
            public XElement Element;
            public int Line;
            public float Offset;
            public float Size;

            public float End => Offset + Size;
        }
    }
}
#endif
