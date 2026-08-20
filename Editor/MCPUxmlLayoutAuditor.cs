#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace VMUnityAutomation.Editor
{
    internal static class MCPUxmlLayoutAuditor
    {
        internal const string SUPPRESSION_MARKER = "uxml-layout-audit: allow-manual-center";
        internal const string REPEATED_INLINE_SUPPRESSION_MARKER =
            "uxml-layout-audit: allow-repeated-inline";
        internal const string REDUNDANT_INLINE_SUPPRESSION_MARKER =
            "uxml-layout-audit: allow-redundant-inline";
        internal const string INERT_TEXT_STRETCH_SUPPRESSION_MARKER =
            "uxml-layout-audit: allow-inert-text-stretch";
        internal const string INERT_TEXT_GROW_SUPPRESSION_MARKER =
            "uxml-layout-audit: allow-inert-text-grow";
        internal const string SINGLE_CHILD_CENTERING_WRAPPER_SUPPRESSION_MARKER =
            "uxml-layout-audit: allow-single-child-centering-wrapper";
        internal const string FIXED_SCROLL_CROSS_AXIS_SIZE_SUPPRESSION_MARKER =
            "uxml-layout-audit: allow-fixed-scroll-cross-axis-size";
        internal const string UNCONSUMED_ELEMENT_NAME_SUPPRESSION_MARKER =
            "uxml-layout-audit: allow-unconsumed-element-name";
        internal const string FIXED_FLEX_PARTITION_SUPPRESSION_MARKER =
            "uxml-layout-audit: allow-fixed-flex-partition";
        internal const string PIXEL_GRID_SUPPRESSION_MARKER =
            "uxml-layout-audit: allow-off-grid-pixels";
        internal const string TOOLTIP_ATTRIBUTE_SUPPRESSION_MARKER =
            "uxml-layout-audit: allow-tooltip";

        private const float CENTER_EPSILON = 0.01f;

        private static readonly Regex styleDeclarationRegex =
            new Regex(@"(?:^|;)\s*(?<name>[-A-Za-z0-9]+)\s*:\s*(?<value>[^;]+)",
                RegexOptions.Compiled);

        private static readonly Regex pixelValueRegex =
            new Regex(@"^(?<value>-?(?:\d+(?:\.\d+)?|\.\d+))px$",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex suppressionRegex =
            new Regex(@"^\s*uxml-layout-audit:\s*allow-manual-center\s+(?<reason>.+?)\s*$",
                RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

        private static readonly Regex repeatedInlineSuppressionRegex =
            new Regex(@"^\s*uxml-layout-audit:\s*allow-repeated-inline\s+(?<reason>.+?)\s*$",
                RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

        private static readonly Regex redundantInlineSuppressionRegex =
            new Regex(@"^\s*uxml-layout-audit:\s*allow-redundant-inline\s+(?<reason>.+?)\s*$",
                RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

        private static readonly Regex inertTextStretchSuppressionRegex =
            new Regex(@"^\s*uxml-layout-audit:\s*allow-inert-text-stretch\s+(?<reason>.+?)\s*$",
                RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

        private static readonly Regex inertTextGrowSuppressionRegex =
            new Regex(@"^\s*uxml-layout-audit:\s*allow-inert-text-grow\s+(?<reason>.+?)\s*$",
                RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

        private static readonly Regex singleChildCenteringWrapperSuppressionRegex =
            new Regex(
                @"^\s*uxml-layout-audit:\s*allow-single-child-centering-wrapper\s+(?<reason>.+?)\s*$",
                RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

        private static readonly Regex fixedScrollCrossAxisSizeSuppressionRegex =
            new Regex(
                @"^\s*uxml-layout-audit:\s*allow-fixed-scroll-cross-axis-size\s+(?<reason>.+?)\s*$",
                RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

        private static readonly Regex unconsumedElementNameSuppressionRegex =
            new Regex(
                @"^\s*uxml-layout-audit:\s*allow-unconsumed-element-name\s+(?<reason>.+?)\s*$",
                RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

        private static readonly Regex fixedFlexPartitionSuppressionRegex =
            new Regex(
                @"^\s*uxml-layout-audit:\s*allow-fixed-flex-partition\s+(?<reason>.+?)\s*$",
                RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

        private static readonly Regex pixelGridSuppressionRegex =
            new Regex(
                @"^\s*uxml-layout-audit:\s*allow-off-grid-pixels\s+(?<reason>.+?)\s*$",
                RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

        private static readonly Regex tooltipAttributeSuppressionRegex =
            new Regex(
                @"^\s*uxml-layout-audit:\s*allow-tooltip\s+(?<reason>.+?)\s*$",
                RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

        private static readonly Regex ussCommentRegex =
            new Regex(@"/\*.*?\*/", RegexOptions.Compiled | RegexOptions.Singleline);

        private static readonly Regex ussRuleRegex =
            new Regex(@"(?<selector>[^{}]+)\{(?<body>[^{}]*)\}",
                RegexOptions.Compiled | RegexOptions.Singleline);

        private static readonly Regex classTokenRegex =
            new Regex(@"(?<![A-Za-z0-9_-])\.(?<token>[A-Za-z_][A-Za-z0-9_-]*)",
                RegexOptions.Compiled);

        private static readonly Regex idTokenRegex =
            new Regex(@"(?<![A-Za-z0-9_-])#(?<token>[A-Za-z_][A-Za-z0-9_-]*)",
                RegexOptions.Compiled);

        private static readonly Regex quotedNameReferenceRegex =
            new Regex(@"[""'](?<token>[^""'\r\n]+)[""']",
                RegexOptions.Compiled);

        private static readonly Regex yamlListNameReferenceRegex =
            new Regex(@"^\s*-\s*(?<token>[^#\r\n]+?)\s*$",
                RegexOptions.Compiled | RegexOptions.Multiline);

        private static readonly Regex yamlScalarNameReferenceRegex =
            new Regex(@"^\s*[A-Za-z_][A-Za-z0-9_]*:\s*(?<token>[^#\r\n]+?)\s*$",
                RegexOptions.Compiled | RegexOptions.Multiline);

        private static readonly Dictionary<string, IReadOnlyList<string>>
            implicitElementClassesByType =
                new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);

        private static readonly HashSet<string> variantLayoutProperties =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "position",
                "left",
                "right",
                "top",
                "bottom",
                "width",
                "height",
                "min-width",
                "max-width",
                "min-height",
                "max-height",
                "flex",
                "flex-basis",
                "flex-grow",
                "flex-shrink",
                "flex-direction",
                "flex-wrap",
                "align-content",
                "align-items",
                "align-self",
                "justify-content",
                "margin",
                "margin-left",
                "margin-right",
                "margin-top",
                "margin-bottom",
                "padding",
                "padding-left",
                "padding-right",
                "padding-top",
                "padding-bottom",
                "row-gap",
                "column-gap"
            };

        internal static MCPUxmlLayoutAuditReport Audit(IEnumerable<string> requestedPaths,
            bool includeSuppressed, int maxIssues, MCPUIToolkitAuditOptions options)
        {
            options = options ?? MCPUIToolkitAuditOptions.FromArguments(
                new Dictionary<string, object>());
            var report = new MCPUxmlLayoutAuditReport(maxIssues);
            var requestedPathList = (requestedPaths ?? Array.Empty<string>())
                .Where(path => string.IsNullOrWhiteSpace(path) == false).ToList();
            var requested = NormalizeRequestedPaths(requestedPathList, report.Errors);
            var allUxmlPaths = MCPUIToolkitAuditUtility.FindAssetFiles(".uxml", options)
                .Concat(requested)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToList();
            var targetPaths = requestedPathList.Count == 0 ? allUxmlPaths : requested;
            var layoutContracts = BuildLayoutContractIndex(report, options, allUxmlPaths);
            var elementNameReferences = BuildElementNameReferenceIndex(
                report, options, allUxmlPaths);

            report.ScannedUxmlCount = targetPaths.Count;
            report.IndexedUxmlCount = allUxmlPaths.Count;

            foreach (var path in targetPaths)
            {
                try
                {
                    AuditText(path,
                        File.ReadAllText(MCPUIToolkitAuditUtility.ToFullPath(path)),
                        layoutContracts, elementNameReferences, report,
                        includeSuppressed, options: options);
                }
                catch (Exception exception)
                {
                    report.Errors.Add($"Failed to audit '{path}': {exception.Message}");
                }
            }

            report.SortIssues();
            return report;
        }

        internal static Dictionary<string, object> RunSelfTests()
        {
            const string suspiciousElement =
                "<ui:VisualElement name=\"Navigation\" style=\"position: absolute; left: 309px; " +
                "bottom: 18px; width: 189px; height: 36px; flex-direction: row; align-items: center; " +
                "justify-content: center;\"><ui:Button style=\"width: 24px; height: 33px;\"/></ui:VisualElement>";

            var cases = new List<Dictionary<string, object>>();
            var suspicious = AuditFixture(suspiciousElement);
            AddSelfTestCase(cases, "manual fixed centering box warns",
                suspicious.WarningCount == 1 &&
                suspicious.Issues.Single().Kind == "manual-centered-layout-box");
            AddSelfTestCase(cases, "warning includes redundant fixed height",
                suspicious.Issues.Single().FixedProperties.SequenceEqual(
                    new[] { "left", "width", "height" }));

            var anchored = AuditFixture(
                "<ui:VisualElement name=\"Navigation\" style=\"position: absolute; left: 0; right: 0; " +
                "bottom: 18px; flex-direction: row; align-items: center; justify-content: center;\">" +
                "<ui:Button style=\"width: 24px; height: 33px;\"/></ui:VisualElement>");
            AddSelfTestCase(cases, "owner-edge anchors pass", anchored.WarningCount == 0);

            var offCenter = AuditFixture(
                suspiciousElement.Replace("left: 309px", "left: 300px"));
            AddSelfTestCase(cases, "non-centering fixed region passes", offCenter.WarningCount == 0);

            var visualInline = AuditFixture(
                suspiciousElement.Replace("position: absolute;",
                    "position: absolute; background-color: rgb(1, 2, 3);"));
            AddSelfTestCase(cases, "inline visual region passes", visualInline.WarningCount == 0);

            var visualClassIndex = new UxmlLayoutContractIndex();
            IndexStyleSheetText(".intentional-region { background-image: url(\"Panel.png\"); }",
                visualClassIndex);
            var visualClass = AuditFixture(
                suspiciousElement.Replace("name=\"Navigation\"",
                    "name=\"Navigation\" class=\"intentional-region\""),
                layoutContracts: visualClassIndex);
            AddSelfTestCase(cases, "USS visual contract passes", visualClass.WarningCount == 0);

            var noParentWidth = AuditFixture(suspiciousElement, "height: 492px;");
            AddSelfTestCase(cases, "unknown owner width passes", noParentWidth.WarningCount == 0);

            var control = AuditFixture(
                "<ui:Button name=\"Navigation\" style=\"position: absolute; left: 309px; width: 189px; " +
                "height: 36px; flex-direction: row; justify-content: center;\"><ui:Label/></ui:Button>");
            AddSelfTestCase(cases, "interactive control passes", control.WarningCount == 0);

            var authoredTooltip = AuditFixture(
                "<ui:Button tooltip=\"View Battle\"/>");
            AddSelfTestCase(cases, "authored tooltip attribute warns",
                authoredTooltip.WarningCount == 1 &&
                authoredTooltip.Issues.Single().Kind ==
                "authored-tooltip-attribute" &&
                authoredTooltip.Issues.Single().AttributeName == "tooltip" &&
                authoredTooltip.Issues.Single().AttributeValue == "View Battle");

            var suppressedTooltip = AuditFixture(
                $"<!-- {TOOLTIP_ATTRIBUTE_SUPPRESSION_MARKER} " +
                "product explicitly requires this authored tooltip -->" +
                "<ui:Button tooltip=\"View Battle\"/>",
                includeSuppressed: true);
            AddSelfTestCase(cases, "reasoned tooltip suppression is retained",
                suppressedTooltip.WarningCount == 0 &&
                suppressedTooltip.SuppressedCount == 1 &&
                suppressedTooltip.Issues.Single().Suppressed);

            var disabledTooltip = AuditFixture(
                "<ui:Button tooltip=\"View Battle\"/>",
                uxmlTooltipAttributes: false);
            AddSelfTestCase(cases, "disabled tooltip audit is silent",
                disabledTooltip.WarningCount == 0 &&
                disabledTooltip.SuppressedCount == 0 &&
                disabledTooltip.Issues.Count == 0);

            var suppressed = AuditFixture(
                $"<!-- {SUPPRESSION_MARKER} fixture owns an intentional interaction region -->" +
                suspiciousElement, includeSuppressed: true);
            AddSelfTestCase(cases, "reasoned suppression is retained",
                suppressed.WarningCount == 0 &&
                suppressed.SuppressedCount == 1 &&
                suppressed.Issues.Single().Suppressed);

            var variantIndex = new UxmlLayoutContractIndex();
            IndexStyleSheetText(
                ".stage-label { position: absolute; } " +
                ".stage-label-above { top: -18px; }",
                variantIndex);
            const string repeatedInlineVariantElements =
                "<ui:VisualElement class=\"stage-label stage-label-above\"/>" +
                "<ui:VisualElement name=\"Stage2Label\" class=\"stage-label\" " +
                "style=\"top: 57px; background-image: url(&quot;Stage2.png&quot;);\"/>" +
                "<ui:VisualElement name=\"Stage3Label\" class=\"stage-label\" " +
                "style=\"background-image: url(&quot;Stage3.png&quot;); top: 57px;\"/>";
            var repeatedInlineVariant = AuditFixture(repeatedInlineVariantElements,
                layoutContracts: variantIndex);
            AddSelfTestCase(cases, "repeated inline authored variant warns",
                repeatedInlineVariant.WarningCount == 1 &&
                repeatedInlineVariant.Issues.Single().Kind ==
                "repeated-inline-layout-variant" &&
                repeatedInlineVariant.Issues.Single().AuthoredUsageCount == 2);

            var distinctInlineVariants = AuditFixture(
                repeatedInlineVariantElements.Replace(
                    "background-image: url(&quot;Stage3.png&quot;); top: 57px;",
                    "background-image: url(&quot;Stage3.png&quot;); top: 60px;"),
                layoutContracts: variantIndex);
            AddSelfTestCase(cases, "distinct inline variants pass",
                distinctInlineVariants.WarningCount == 0);

            var prefixOnlyIndex = new UxmlLayoutContractIndex();
            IndexStyleSheetText(".stage-label-glyph { top: -18px; }", prefixOnlyIndex);
            var unprovenVariant = AuditFixture(
                "<ui:VisualElement class=\"stage-label-glyph\"/>" +
                "<ui:VisualElement class=\"stage-label\" style=\"top: 57px;\"/>" +
                "<ui:VisualElement class=\"stage-label\" style=\"top: 57px;\"/>",
                layoutContracts: prefixOnlyIndex);
            AddSelfTestCase(cases, "class-name prefix without co-usage passes",
                unprovenVariant.WarningCount == 0);

            var suppressedInlineVariant = AuditFixture(
                "<ui:VisualElement class=\"stage-label stage-label-above\"/>" +
                $"<!-- {REPEATED_INLINE_SUPPRESSION_MARKER} fixture mirrors runtime layout -->" +
                "<ui:VisualElement class=\"stage-label\" style=\"top: 57px;\"/>" +
                "<ui:VisualElement class=\"stage-label\" style=\"top: 57px;\"/>",
                includeSuppressed: true, layoutContracts: variantIndex);
            AddSelfTestCase(cases, "reasoned repeated-inline suppression is retained",
                suppressedInlineVariant.WarningCount == 0 &&
                suppressedInlineVariant.SuppressedCount == 1 &&
                suppressedInlineVariant.Issues.Single().Suppressed);

            var inlineStyleIndex = new UxmlInlineStyleContractIndex();
            IndexInlineStyleSheetText("Assets/Basics.uss",
                ".unity-base-field { overflow: visible; margin-left: 0; }",
                inlineStyleIndex);
            var redundantImplicitInline = AuditFixture(
                "<ui:TextField name=\"Field\" style=\"overflow: visible;\"/>",
                inlineStyleContracts: inlineStyleIndex);
            AddSelfTestCase(cases, "inline declaration duplicating implicit USS class warns",
                redundantImplicitInline.WarningCount == 1 &&
                redundantImplicitInline.Issues.Single().Kind ==
                "redundant-inline-declaration" &&
                redundantImplicitInline.Issues.Single().FixedProperties
                    .SequenceEqual(new[] { "overflow" }));

            var intentionalInlineOverride = AuditFixture(
                "<ui:TextField name=\"Field\" style=\"overflow: hidden;\"/>",
                inlineStyleContracts: inlineStyleIndex);
            AddSelfTestCase(cases, "inline declaration overriding USS default passes",
                intentionalInlineOverride.WarningCount == 0);

            var suppressedRedundantInline = AuditFixture(
                $"<!-- {REDUNDANT_INLINE_SUPPRESSION_MARKER} fixture documents generated output -->" +
                "<ui:TextField name=\"Field\" style=\"overflow: visible;\"/>",
                includeSuppressed: true, inlineStyleContracts: inlineStyleIndex);
            AddSelfTestCase(cases, "reasoned redundant-inline suppression is retained",
                suppressedRedundantInline.WarningCount == 0 &&
                suppressedRedundantInline.SuppressedCount == 1 &&
                suppressedRedundantInline.Issues.Single().Suppressed);

            const string inertStretch =
                "<ui:VisualElement style=\"align-items: center;\">" +
                "<ui:Label name=\"Title\" style=\"align-self: stretch; margin-left: 18px; " +
                "margin-right: 18px; -unity-text-align: middle-center;\"/>" +
                "</ui:VisualElement>";
            var inertTextStretch = AuditFixture(inertStretch);
            AddSelfTestCase(cases, "centered intrinsic label stretch warns",
                inertTextStretch.WarningCount == 1 &&
                inertTextStretch.Issues.Single().Kind ==
                "visually-inert-text-stretch" &&
                inertTextStretch.Issues.Single().Axis == "horizontal");

            var asymmetricStretch = AuditFixture(
                inertStretch.Replace("margin-right: 18px", "margin-right: 21px"));
            AddSelfTestCase(cases, "asymmetric label stretch passes",
                asymmetricStretch.WarningCount == 0);

            var nonCenteredTextStretch = AuditFixture(
                inertStretch.Replace("middle-center", "middle-left"));
            AddSelfTestCase(cases, "non-centered text stretch passes",
                nonCenteredTextStretch.WarningCount == 0);

            var visualBoxStretch = AuditFixture(
                inertStretch.Replace("align-self: stretch;",
                    "align-self: stretch; background-color: rgb(1, 2, 3);"));
            AddSelfTestCase(cases, "visually owned label stretch passes",
                visualBoxStretch.WarningCount == 0);

            var fixedWidthStretch = AuditFixture(
                inertStretch.Replace("align-self: stretch;",
                    "align-self: stretch; width: 120px;"));
            AddSelfTestCase(cases, "fixed cross-size label stretch passes",
                fixedWidthStretch.WarningCount == 0);

            var labelDefaultStyleIndex = new UxmlInlineStyleContractIndex();
            IndexInlineStyleSheetText("Assets/Basics.uss",
                ".unity-label { padding-top: 0; padding-right: 0; " +
                "padding-bottom: 0; padding-left: 0; }",
                labelDefaultStyleIndex);
            var zeroDefaultPaddingStretch = AuditFixture(inertStretch,
                inlineStyleContracts: labelDefaultStyleIndex);
            AddSelfTestCase(cases, "neutral label defaults do not hide inert stretch",
                zeroDefaultPaddingStretch.WarningCount == 1 &&
                zeroDefaultPaddingStretch.Issues.Single().Kind ==
                "visually-inert-text-stretch");

            var labelBoxIndex = new UxmlLayoutContractIndex();
            IndexStyleSheetText(
                ".intentional-label-box { background-image: url(\"Title.png\"); }",
                labelBoxIndex);
            var styledBoxStretch = AuditFixture(
                inertStretch.Replace("name=\"Title\"",
                    "name=\"Title\" class=\"intentional-label-box\""),
                layoutContracts: labelBoxIndex);
            AddSelfTestCase(cases, "explicit USS label box passes",
                styledBoxStretch.WarningCount == 0);

            var suppressedInertStretch = AuditFixture(
                inertStretch.Replace("<ui:Label",
                    $"<!-- {INERT_TEXT_STRETCH_SUPPRESSION_MARKER} " +
                    "fixture reserves a hit region --><ui:Label"),
                includeSuppressed: true);
            AddSelfTestCase(cases, "reasoned inert-stretch suppression is retained",
                suppressedInertStretch.WarningCount == 0 &&
                suppressedInertStretch.SuppressedCount == 1 &&
                suppressedInertStretch.Issues.Single().Suppressed);

            const string inertGrow =
                "<ui:VisualElement style=\"justify-content: center;\">" +
                "<ui:Label name=\"Title\" style=\"flex-grow: 1; margin-bottom: 3px; " +
                "-unity-text-align: middle-center;\"/>" +
                "</ui:VisualElement>";
            var inertTextGrow = AuditFixture(inertGrow);
            AddSelfTestCase(cases, "centered sole label grow warns",
                inertTextGrow.WarningCount == 1 &&
                inertTextGrow.Issues.Single().Kind == "visually-inert-text-grow" &&
                inertTextGrow.Issues.Single().Axis == "vertical");

            var siblingGrow = AuditFixture(
                inertGrow.Replace("</ui:VisualElement>",
                    "<ui:Label text=\"Sibling\"/></ui:VisualElement>"));
            AddSelfTestCase(cases, "label grow with sibling passes",
                siblingGrow.WarningCount == 0);

            var parentStartGrow = AuditFixture(
                inertGrow.Replace("justify-content: center",
                    "justify-content: flex-end"));
            AddSelfTestCase(cases, "non-centered parent grow passes",
                parentStartGrow.WarningCount == 0);

            var upperTextGrow = AuditFixture(
                inertGrow.Replace("middle-center", "upper-center"));
            AddSelfTestCase(cases, "non-centered main-axis text grow passes",
                upperTextGrow.WarningCount == 0);

            var visualBoxGrow = AuditFixture(
                inertGrow.Replace("flex-grow: 1;",
                    "flex-grow: 1; background-color: rgb(1, 2, 3);"));
            AddSelfTestCase(cases, "visually owned label grow passes",
                visualBoxGrow.WarningCount == 0);

            var fixedHeightGrow = AuditFixture(
                inertGrow.Replace("flex-grow: 1;",
                    "flex-grow: 1; height: 24px;"));
            AddSelfTestCase(cases, "fixed main-size label grow passes",
                fixedHeightGrow.WarningCount == 0);

            const string horizontalGrow =
                "<ui:VisualElement style=\"flex-direction: row; justify-content: center;\">" +
                "<ui:Label name=\"Title\" style=\"flex-grow: 2; " +
                "-unity-text-align: middle-center;\"/>" +
                "</ui:VisualElement>";
            var horizontalTextGrow = AuditFixture(horizontalGrow);
            AddSelfTestCase(cases, "centered row label grow warns",
                horizontalTextGrow.WarningCount == 1 &&
                horizontalTextGrow.Issues.Single().Axis == "horizontal");

            var zeroDefaultPaddingGrow = AuditFixture(inertGrow,
                inlineStyleContracts: labelDefaultStyleIndex);
            AddSelfTestCase(cases, "neutral label defaults do not hide inert grow",
                zeroDefaultPaddingGrow.WarningCount == 1 &&
                zeroDefaultPaddingGrow.Issues.Single().Kind ==
                "visually-inert-text-grow");

            var growDefaultStyleIndex = new UxmlInlineStyleContractIndex();
            IndexInlineStyleSheetText("Assets/Grow Default.uss",
                ".unity-label { flex-grow: 1; }", growDefaultStyleIndex);
            var redundantDefaultGrow = AuditFixture(inertGrow,
                inlineStyleContracts: growDefaultStyleIndex);
            AddSelfTestCase(cases, "loaded grow default reports only its owning rule",
                redundantDefaultGrow.WarningCount == 1 &&
                redundantDefaultGrow.Issues.Single().Kind ==
                "redundant-inline-declaration");

            var suppressedInertGrow = AuditFixture(
                inertGrow.Replace("<ui:Label",
                    $"<!-- {INERT_TEXT_GROW_SUPPRESSION_MARKER} " +
                    "fixture reserves a hit region --><ui:Label"),
                includeSuppressed: true);
            AddSelfTestCase(cases, "reasoned inert-grow suppression is retained",
                suppressedInertGrow.WarningCount == 0 &&
                suppressedInertGrow.SuppressedCount == 1 &&
                suppressedInertGrow.Issues.Single().Suppressed);

            const string redundantCenteringWrapper =
                "<ui:VisualElement name=\"HeaderAnchor\" picking-mode=\"Ignore\" " +
                "style=\"position: absolute; left: 0; top: -6px; right: 0; height: 54px; " +
                "align-items: center;\">" +
                "<ui:VisualElement name=\"Header\" style=\"width: 228px; height: 54px; " +
                "background-image: url(&quot;Header.png&quot;);\"/>" +
                "</ui:VisualElement>";
            var singleChildCenteringWrapper = AuditFixture(redundantCenteringWrapper);
            AddSelfTestCase(cases, "single-child centering wrapper warns",
                singleChildCenteringWrapper.WarningCount == 1 &&
                singleChildCenteringWrapper.Issues.Single().Kind ==
                "single-child-centering-wrapper" &&
                singleChildCenteringWrapper.Issues.Single().Axis == "horizontal" &&
                singleChildCenteringWrapper.Issues.Single().FixedProperties
                    .SequenceEqual(new[] { "left", "right", "height", "align-items" }));

            var asymmetricCenteringWrapper = AuditFixture(
                redundantCenteringWrapper.Replace("right: 0", "right: 12px"));
            AddSelfTestCase(cases, "asymmetric centering wrapper passes",
                asymmetricCenteringWrapper.WarningCount == 0);

            var multiChildCenteringWrapper = AuditFixture(
                redundantCenteringWrapper.Replace("</ui:VisualElement>",
                    "<ui:VisualElement style=\"width: 12px; height: 12px; " +
                    "background-color: white;\"/></ui:VisualElement>"));
            AddSelfTestCase(cases, "multi-child centering wrapper passes",
                multiChildCenteringWrapper.WarningCount == 0);

            var visualCenteringWrapper = AuditFixture(
                redundantCenteringWrapper.Replace("align-items: center;",
                    "align-items: center; background-color: white;"));
            AddSelfTestCase(cases, "visible centering wrapper passes",
                visualCenteringWrapper.WarningCount == 0);

            var verticalLayoutWrapper = AuditFixture(
                redundantCenteringWrapper.Replace("height: 54px; align-items: center;",
                    "height: 72px; align-items: center; justify-content: center;"));
            AddSelfTestCase(cases, "wrapper with vertical layout responsibility passes",
                verticalLayoutWrapper.WarningCount == 0);

            var flexibleChildWrapper = AuditFixture(
                redundantCenteringWrapper.Replace("width: 228px;", ""));
            AddSelfTestCase(cases, "wrapper with flexible child passes",
                flexibleChildWrapper.WarningCount == 0);

            var suppressedCenteringWrapper = AuditFixture(
                $"<!-- {SINGLE_CHILD_CENTERING_WRAPPER_SUPPRESSION_MARKER} " +
                "fixture preserves an external lookup path -->" +
                redundantCenteringWrapper, includeSuppressed: true);
            AddSelfTestCase(cases, "reasoned centering-wrapper suppression is retained",
                suppressedCenteringWrapper.WarningCount == 0 &&
                suppressedCenteringWrapper.SuppressedCount == 1 &&
                suppressedCenteringWrapper.Issues.Single().Suppressed);

            const string fixedVerticalScrollCrossAxisSize =
                "<ui:ScrollView mode=\"Vertical\" style=\"width: 312px; height: 240px;\">" +
                "<ui:VisualElement name=\"StagePartyEntries\" " +
                "style=\"width: 330px; align-items: center;\">" +
                "<ui:VisualElement/><ui:VisualElement/><ui:VisualElement/>" +
                "</ui:VisualElement></ui:ScrollView>";
            var fixedVerticalScrollWrapper = AuditFixture(fixedVerticalScrollCrossAxisSize);
            AddSelfTestCase(cases, "vertical scroll content wrapper fixed width warns",
                fixedVerticalScrollWrapper.WarningCount == 1 &&
                fixedVerticalScrollWrapper.Issues.Single().Kind ==
                "fixed-scroll-cross-axis-content-size" &&
                fixedVerticalScrollWrapper.Issues.Single().Axis == "horizontal" &&
                Math.Abs(fixedVerticalScrollWrapper.Issues.Single().ParentSize - 312f) <=
                CENTER_EPSILON &&
                Math.Abs(fixedVerticalScrollWrapper.Issues.Single().Size - 330f) <=
                CENTER_EPSILON);

            var equalVerticalScrollWrapper = AuditFixture(
                fixedVerticalScrollCrossAxisSize.Replace("width: 330px", "width: 312px"));
            AddSelfTestCase(cases, "vertical scroll content wrapper repeated width warns",
                equalVerticalScrollWrapper.WarningCount == 1 &&
                equalVerticalScrollWrapper.Issues.Single().Kind ==
                "fixed-scroll-cross-axis-content-size");

            var stretchedVerticalScrollWrapper = AuditFixture(
                fixedVerticalScrollCrossAxisSize.Replace("width: 330px; ", ""));
            AddSelfTestCase(cases, "cross-axis stretch scroll wrapper passes",
                stretchedVerticalScrollWrapper.WarningCount == 0);

            var narrowerVerticalScrollWrapper = AuditFixture(
                fixedVerticalScrollCrossAxisSize.Replace("width: 330px", "width: 300px"));
            AddSelfTestCase(cases, "intentional narrower scroll wrapper passes",
                narrowerVerticalScrollWrapper.WarningCount == 0);

            var visualVerticalScrollWrapper = AuditFixture(
                fixedVerticalScrollCrossAxisSize.Replace(
                    "width: 330px; align-items: center;",
                    "width: 330px; align-items: center; background-color: white;"));
            AddSelfTestCase(cases, "visually owned scroll content region passes",
                visualVerticalScrollWrapper.WarningCount == 0);

            var clippingVerticalScrollWrapper = AuditFixture(
                fixedVerticalScrollCrossAxisSize.Replace(
                    "width: 330px; align-items: center;",
                    "width: 330px; align-items: center; overflow: hidden;"));
            AddSelfTestCase(cases, "clipping scroll content region passes",
                clippingVerticalScrollWrapper.WarningCount == 0);

            var interactiveVerticalScrollWrapper = AuditFixture(
                fixedVerticalScrollCrossAxisSize.Replace(
                    "name=\"StagePartyEntries\"",
                    "name=\"StagePartyEntries\" focusable=\"true\""));
            AddSelfTestCase(cases, "interactive scroll content region passes",
                interactiveVerticalScrollWrapper.WarningCount == 0);

            const string fixedHorizontalScrollCrossAxisSize =
                "<ui:ScrollView mode=\"Horizontal\" style=\"width: 300px; height: 96px;\">" +
                "<ui:VisualElement style=\"height: 102px; flex-direction: row;\">" +
                "<ui:VisualElement/><ui:VisualElement/>" +
                "</ui:VisualElement></ui:ScrollView>";
            var fixedHorizontalScrollWrapper = AuditFixture(fixedHorizontalScrollCrossAxisSize);
            AddSelfTestCase(cases, "horizontal scroll content wrapper fixed height warns",
                fixedHorizontalScrollWrapper.WarningCount == 1 &&
                fixedHorizontalScrollWrapper.Issues.Single().Kind ==
                "fixed-scroll-cross-axis-content-size" &&
                fixedHorizontalScrollWrapper.Issues.Single().Axis == "vertical");

            var bidirectionalScrollWrapper = AuditFixture(
                fixedVerticalScrollCrossAxisSize.Replace(
                    "mode=\"Vertical\"", "mode=\"VerticalAndHorizontal\""));
            AddSelfTestCase(cases, "bidirectional scroll content fixed size passes",
                bidirectionalScrollWrapper.WarningCount == 0);

            var unknownScrollExtent = AuditFixture(
                fixedVerticalScrollCrossAxisSize.Replace("width: 312px; ", ""));
            AddSelfTestCase(cases, "unknown scroll cross-axis extent passes",
                unknownScrollExtent.WarningCount == 0);

            var suppressedScrollWrapper = AuditFixture(
                fixedVerticalScrollCrossAxisSize.Replace(
                    "<ui:VisualElement name=\"StagePartyEntries\"",
                    $"<!-- {FIXED_SCROLL_CROSS_AXIS_SIZE_SUPPRESSION_MARKER} " +
                    "external surface owns a measured cross-axis crop -->" +
                    "<ui:VisualElement name=\"StagePartyEntries\""),
                includeSuppressed: true);
            AddSelfTestCase(cases, "reasoned fixed scroll cross-axis suppression is retained",
                suppressedScrollWrapper.WarningCount == 0 &&
                suppressedScrollWrapper.SuppressedCount == 1 &&
                suppressedScrollWrapper.Issues.Single().Suppressed);

            var unconsumedNameIndex = new UxmlElementNameReferenceIndex(true);
            const string unconsumedName =
                "<ui:VisualElement name=\"LayoutOnlyName\"><ui:Label/></ui:VisualElement>";
            var unconsumedElementName = AuditFixture(unconsumedName,
                elementNameReferences: unconsumedNameIndex);
            AddSelfTestCase(cases, "unconsumed authored element name warns",
                unconsumedElementName.WarningCount == 1 &&
                unconsumedElementName.Issues.Single().Kind ==
                "unconsumed-element-name");

            var consumedNameIndex = new UxmlElementNameReferenceIndex(true);
            consumedNameIndex.AddDefinition("QueriedElement");
            consumedNameIndex.AddReference("QueriedElement");
            var consumedElementName = AuditFixture(
                unconsumedName.Replace("LayoutOnlyName", "QueriedElement"),
                elementNameReferences: consumedNameIndex);
            AddSelfTestCase(cases, "referenced authored element name passes",
                consumedElementName.WarningCount == 0);

            var suppressedElementName = AuditFixture(
                $"<!-- {UNCONSUMED_ELEMENT_NAME_SUPPRESSION_MARKER} " +
                "external native integration looks up this element -->" +
                unconsumedName, includeSuppressed: true,
                elementNameReferences: unconsumedNameIndex);
            AddSelfTestCase(cases, "reasoned element-name suppression is retained",
                suppressedElementName.WarningCount == 0 &&
                suppressedElementName.SuppressedCount == 1 &&
                suppressedElementName.Issues.Single().Suppressed);

            const string fixedFlexPartition =
                "<ui:VisualElement name=\"Panel\" style=\"height: 489px;\">" +
                "<ui:VisualElement name=\"Header\" style=\"height: 63px; flex-shrink: 0;\"/>" +
                "<ui:VisualElement name=\"Body\" style=\"height: 426px; flex-shrink: 0;\">" +
                "<ui:Label/></ui:VisualElement></ui:VisualElement>";
            var fixedPartition = AuditFixture(fixedFlexPartition);
            AddSelfTestCase(cases, "fully fixed flex partition warns",
                fixedPartition.WarningCount == 1 &&
                fixedPartition.Issues.Single().Kind == "fixed-flex-partition" &&
                fixedPartition.Issues.Single().Axis == "vertical" &&
                fixedPartition.Issues.Single().FixedProperties
                    .SequenceEqual(new[] { "height", "flex-shrink" }));

            var flexibleRemainder = AuditFixture(
                fixedFlexPartition.Replace(
                    "height: 426px; flex-shrink: 0;",
                    "flex-grow: 1;"));
            AddSelfTestCase(cases, "fixed header with flexible remainder passes",
                flexibleRemainder.WarningCount == 0);

            var incompletePartition = AuditFixture(
                fixedFlexPartition.Replace("height: 426px;", "height: 420px;"));
            AddSelfTestCase(cases, "fixed children that do not partition parent pass",
                incompletePartition.WarningCount == 0);

            var suppressedFixedPartition = AuditFixture(
                $"<!-- {FIXED_FLEX_PARTITION_SUPPRESSION_MARKER} " +
                "external native surface requires exact child extents -->" +
                fixedFlexPartition, includeSuppressed: true);
            AddSelfTestCase(cases, "reasoned fixed-partition suppression is retained",
                suppressedFixedPartition.WarningCount == 0 &&
                suppressedFixedPartition.SuppressedCount == 1 &&
                suppressedFixedPartition.Issues.Single().Suppressed);

            var pixelGridPass = AuditFixture(
                "<ui:VisualElement style=\"left: 6px; margin-left: -3px; " +
                "padding: 3px 6px 9px;\"/>",
                pixelGridEnabled: true, pixelGridStep: 3);
            AddSelfTestCase(cases, "inline values on the configured grid pass",
                pixelGridPass.WarningCount == 0);

            var pixelGridFail = AuditFixture(
                "<ui:VisualElement style=\"top: 4px; padding: 3px 7px; " +
                "width: 7px; font-size: 7px;\"/>",
                pixelGridEnabled: true, pixelGridStep: 3);
            AddSelfTestCase(cases, "only off-grid inline structural declarations warn",
                pixelGridFail.WarningCount == 1 &&
                pixelGridFail.Issues.Single().Kind ==
                "off-grid-pixel-declarations" &&
                pixelGridFail.Issues.Single().InlineDeclarations.Keys
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .SequenceEqual(new[] { "padding", "top" }));

            var pixelGridSuppressed = AuditFixture(
                $"<!-- {PIXEL_GRID_SUPPRESSION_MARKER} " +
                "fixture documents optical alignment -->" +
                "<ui:VisualElement style=\"margin-right: 1px;\"/>",
                includeSuppressed: true, pixelGridEnabled: true, pixelGridStep: 3);
            AddSelfTestCase(cases, "reasoned inline pixel-grid suppression is retained",
                pixelGridSuppressed.WarningCount == 0 &&
                pixelGridSuppressed.SuppressedCount == 1 &&
                pixelGridSuppressed.Issues.Single().Suppressed);

            foreach (var testCase in MCPUxmlNaturalFlowLayoutAuditor.RunSelfTests())
            {
                cases.Add(testCase);
            }

            foreach (var testCase in MCPUxmlComponentInitialStyleAuditor.RunSelfTests())
            {
                cases.Add(testCase);
            }

            return new Dictionary<string, object>
            {
                { "passed", cases.All(testCase => (bool)testCase["passed"]) },
                { "cases", cases }
            };
        }

        private static MCPUxmlLayoutAuditReport AuditFixture(string element,
            string parentStyle = "width: 807px; height: 492px;", bool includeSuppressed = false,
            UxmlLayoutContractIndex layoutContracts = null,
            UxmlInlineStyleContractIndex inlineStyleContracts = null,
            UxmlElementNameReferenceIndex elementNameReferences = null,
            bool pixelGridEnabled = false, int pixelGridStep = 3,
            bool uxmlTooltipAttributes = true)
        {
            var text =
                "<ui:UXML xmlns:ui=\"UnityEngine.UIElements\">" +
                $"<ui:VisualElement style=\"{parentStyle}\">{element}</ui:VisualElement>" +
                "</ui:UXML>";
            var report = new MCPUxmlLayoutAuditReport(100)
            {
                ScannedUxmlCount = 1,
                IndexedUxmlCount = 1
            };
            var options = MCPUIToolkitAuditOptions.FromProjectSettings(
                new MCPUIToolkitAuditProjectSettings
                {
                    PixelGridEnabled = pixelGridEnabled,
                    PixelGridStep = pixelGridStep,
                    UxmlTooltipAttributes = uxmlTooltipAttributes
                });
            AuditText("Assets/__UxmlLayoutAuditSelfTest.uxml", text,
                layoutContracts ?? new UxmlLayoutContractIndex(),
                elementNameReferences ?? UxmlElementNameReferenceIndex.Disabled,
                report, includeSuppressed,
                inlineStyleContracts, options);
            report.SortIssues();
            return report;
        }

        private static void AuditText(string assetPath, string text,
            UxmlLayoutContractIndex layoutContracts,
            UxmlElementNameReferenceIndex elementNameReferences,
            MCPUxmlLayoutAuditReport report, bool includeSuppressed,
            UxmlInlineStyleContractIndex inlineStyleContracts = null,
            MCPUIToolkitAuditOptions options = null)
        {
            var document = XDocument.Parse(text, LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo);
            IndexUxmlDocument(document, layoutContracts);
            options = options ?? MCPUIToolkitAuditOptions.FromArguments(
                new Dictionary<string, object> { { "useProjectSettings", false } });
            inlineStyleContracts = inlineStyleContracts ??
                                   BuildInlineStyleContractIndex(assetPath, document, report);
            if (options.UxmlTooltipAttributes)
                AuditTooltipAttributes(assetPath, document, report, includeSuppressed);
            AuditPixelGridDeclarations(assetPath, document, options, report,
                includeSuppressed);
            foreach (var element in document.Descendants())
            {
                AuditElement(assetPath, element, layoutContracts, report, includeSuppressed);
            }

            AuditRedundantInlineDeclarations(assetPath, document, inlineStyleContracts, report,
                includeSuppressed);
            MCPUxmlComponentInitialStyleAuditor.Audit(assetPath, document,
                element => inlineStyleContracts.Resolve(element).ToDictionary(
                    declaration => declaration.Key,
                    declaration => declaration.Value.Value,
                    StringComparer.OrdinalIgnoreCase),
                report, includeSuppressed);
            AuditVisuallyInertTextStretch(assetPath, document, inlineStyleContracts,
                layoutContracts, report, includeSuppressed);
            AuditVisuallyInertTextGrow(assetPath, document, inlineStyleContracts,
                layoutContracts, report, includeSuppressed);
            AuditSingleChildCenteringWrapper(assetPath, document, inlineStyleContracts,
                layoutContracts, report, includeSuppressed);
            AuditFixedScrollCrossAxisContentSizes(assetPath, document, inlineStyleContracts,
                layoutContracts, report, includeSuppressed);
            AuditUnconsumedElementNames(assetPath, document, elementNameReferences,
                report, includeSuppressed);
            AuditFixedFlexPartitions(assetPath, document, inlineStyleContracts,
                report, includeSuppressed);
            MCPUxmlNaturalFlowLayoutAuditor.Audit(assetPath, document,
                element => ResolveAuthoredStyle(element, inlineStyleContracts),
                (element, style) => HasVisualBoxContract(element, style, layoutContracts),
                report, includeSuppressed);
            AuditRepeatedInlineLayoutVariants(assetPath, document, layoutContracts, report,
                includeSuppressed);
        }

        private static void AuditTooltipAttributes(string assetPath, XDocument document,
            MCPUxmlLayoutAuditReport report, bool includeSuppressed)
        {
            if (document.Root == null)
            {
                return;
            }

            foreach (var element in document.Root.DescendantsAndSelf())
            {
                var tooltipAttributes = element.Attributes().Where(attribute =>
                        string.Equals(attribute.Name.LocalName, "tooltip",
                            StringComparison.OrdinalIgnoreCase))
                    .ToList();
                foreach (var tooltipAttribute in tooltipAttributes)
                {
                    var name = AttributeValue(element, "name");
                    var elementLabel = string.IsNullOrWhiteSpace(name)
                        ? $"<{element.Name.LocalName}>"
                        : $"#{name}";
                    var suppressionReason = GetSuppressionReason(element,
                        tooltipAttributeSuppressionRegex);
                    var issue = new MCPUxmlLayoutAuditIssue
                    {
                        AssetPath = assetPath,
                        Line = GetLineNumber(tooltipAttribute),
                        Element = elementLabel,
                        ElementName = name,
                        Kind = "authored-tooltip-attribute",
                        AttributeName = tooltipAttribute.Name.LocalName,
                        AttributeValue = tooltipAttribute.Value,
                        FixedProperties = new List<string> { "tooltip" },
                        Suppressed = string.IsNullOrWhiteSpace(suppressionReason) == false,
                        SuppressionReason = suppressionReason,
                        Message =
                            $"{elementLabel} declares a tooltip attribute in authored UXML. " +
                            "Remove it unless the product explicitly requires this exact UXML " +
                            "tooltip; document that exception with a reasoned allow-tooltip " +
                            "suppression."
                    };
                    report.Record(issue, includeSuppressed);
                }
            }
        }

        private static void AuditPixelGridDeclarations(string assetPath, XDocument document,
            MCPUIToolkitAuditOptions options, MCPUxmlLayoutAuditReport report,
            bool includeSuppressed)
        {
            if (options.PixelGridEnabled == false)
                return;

            foreach (var element in document.Descendants())
            {
                var offGridDeclarations =
                    MCPUIToolkitPixelGridAuditUtility.FindOffGridDeclarations(
                        ParseStyle(AttributeValue(element, "style")), options.PixelGridStep);
                if (offGridDeclarations.Count == 0)
                    continue;

                var name = AttributeValue(element, "name");
                var elementLabel = string.IsNullOrWhiteSpace(name)
                    ? $"<{element.Name.LocalName}>"
                    : $"#{name}";
                var properties = offGridDeclarations.Keys
                    .OrderBy(property => property, StringComparer.Ordinal)
                    .ToList();
                var suppressionReason = GetSuppressionReason(element,
                    pixelGridSuppressionRegex);
                var issue = new MCPUxmlLayoutAuditIssue
                {
                    AssetPath = assetPath,
                    Line = GetLineNumber(element),
                    Element = elementLabel,
                    ElementName = name,
                    Kind = "off-grid-pixel-declarations",
                    GridStep = options.PixelGridStep,
                    FixedProperties = properties,
                    InlineDeclarations = offGridDeclarations,
                    Suppressed = string.IsNullOrWhiteSpace(suppressionReason) == false,
                    SuppressionReason = suppressionReason,
                    Message =
                        $"{elementLabel} has inline structural offset, spacing, or padding " +
                        $"declarations outside the configured {options.PixelGridStep}px grid: " +
                        $"{string.Join(", ", properties)}. Align them to the project grid or " +
                        "add a reasoned suppression for a measured optical or seam correction."
                };
                report.Record(issue, includeSuppressed);
            }
        }

        private static void AuditElement(string assetPath, XElement element,
            UxmlLayoutContractIndex layoutContracts, MCPUxmlLayoutAuditReport report,
            bool includeSuppressed)
        {
            var parent = element.Parent as XElement;
            if (element.Name.LocalName != "VisualElement" ||
                parent == null ||
                HasVisualChildren(element) == false)
            {
                return;
            }

            var style = ParseStyle(AttributeValue(element, "style"));
            var parentStyle = ParseStyle(AttributeValue(parent, "style"));
            if (StyleValue(style, "position") != "absolute" ||
                StyleValue(style, "flex-direction") != "row" ||
                StyleValue(style, "justify-content") != "center" ||
                style.ContainsKey("right") ||
                TryGetPixels(style, "left", out var left) == false ||
                TryGetPixels(style, "width", out var width) == false ||
                TryGetPixels(parentStyle, "width", out var parentWidth) == false ||
                left < 0 ||
                width <= 0 ||
                parentWidth <= 0 ||
                Math.Abs(left * 2 + width - parentWidth) > CENTER_EPSILON ||
                HasBoxContract(element, style, layoutContracts))
            {
                return;
            }

            var name = AttributeValue(element, "name");
            var fixedProperties = new List<string> { "left", "width" };
            var heightClause = "";
            if (TryGetPixels(style, "height", out var height))
            {
                fixedProperties.Add("height");
                heightClause =
                    $" The fixed height ({FormatPixels(height)}) also has no authored visual, clipping, " +
                    "constrained-region, or explicit interaction contract; let in-flow children determine it.";
            }

            var suppressionReason = GetSuppressionReason(element);
            var elementLabel = string.IsNullOrWhiteSpace(name)
                ? "<VisualElement>"
                : $"#{name}";
            var issue = new MCPUxmlLayoutAuditIssue
            {
                AssetPath = assetPath,
                Line = GetLineNumber(element),
                Element = elementLabel,
                ElementName = name,
                Kind = "manual-centered-layout-box",
                Axis = "horizontal",
                FixedProperties = fixedProperties,
                ParentSize = parentWidth,
                Offset = left,
                Size = width,
                Suppressed = string.IsNullOrWhiteSpace(suppressionReason) == false,
                SuppressionReason = suppressionReason,
                Message =
                    $"Layout-only {elementLabel} is manually centered with left {FormatPixels(left)} plus " +
                    $"width {FormatPixels(width)} inside a {FormatPixels(parentWidth)} owner while also " +
                    "centering its children. These values only create an empty centering box; anchor both " +
                    "horizontal owner edges and keep justify-content: center." + heightClause
            };
            report.Record(issue, includeSuppressed);
        }

        private static bool HasVisualChildren(XElement element)
        {
            return element.Elements().Any(child =>
                child.Name.LocalName != "Bindings" &&
                child.Name.LocalName != "Style" &&
                child.Name.LocalName != "Template" &&
                child.Name.LocalName != "AttributeOverrides");
        }

        private static bool HasBoxContract(XElement element, IReadOnlyDictionary<string, string> style,
            UxmlLayoutContractIndex layoutContracts)
        {
            if (style.Any(property => IsBoxContractProperty(property.Key, property.Value)))
            {
                return true;
            }

            var name = AttributeValue(element, "name");
            if (string.IsNullOrWhiteSpace(name) == false && layoutContracts.BoxIds.Contains(name))
            {
                return true;
            }

            foreach (var className in SplitWhitespace(AttributeValue(element, "class")))
            {
                if (layoutContracts.BoxClasses.Contains(className))
                {
                    return true;
                }
            }

            if (element.Elements().Any(child => child.Name.LocalName == "Bindings"))
            {
                return true;
            }

            if (string.Equals(AttributeValue(element, "focusable"), "true",
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

        private static UxmlElementNameReferenceIndex BuildElementNameReferenceIndex(
            MCPUxmlLayoutAuditReport report, MCPUIToolkitAuditOptions options,
            IEnumerable<string> uxmlPaths)
        {
            var index = new UxmlElementNameReferenceIndex(true);
            var paths = (uxmlPaths ?? Enumerable.Empty<string>()).ToList();

            foreach (var path in paths)
            {
                try
                {
                    var document = XDocument.Parse(
                        File.ReadAllText(MCPUIToolkitAuditUtility.ToFullPath(path)),
                        LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo);
                    foreach (var element in document.Descendants()
                                 .Where(IsAuditableNamedElement))
                    {
                        index.AddDefinition(AttributeValue(element, "name"));
                    }
                }
                catch (Exception exception)
                {
                    report.Errors.Add(
                        $"Failed to index UXML element-name definitions in '{path}': " +
                        exception.Message);
                }
            }

            if (index.DefinitionCount == 0)
            {
                return index;
            }

            foreach (var path in MCPUIToolkitAuditUtility.FindAssetFiles(".uss", options))
            {
                try
                {
                    IndexStylesheetNameReferences(
                        File.ReadAllText(MCPUIToolkitAuditUtility.ToFullPath(path)), index);
                }
                catch (Exception exception)
                {
                    report.Errors.Add(
                        $"Failed to index USS element-name references in '{path}': " +
                        exception.Message);
                }
            }

            foreach (var path in paths)
            {
                try
                {
                    var document = XDocument.Parse(
                        File.ReadAllText(MCPUIToolkitAuditUtility.ToFullPath(path)),
                        LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo);
                    IndexUxmlNameReferences(document, index);
                }
                catch (Exception exception)
                {
                    report.Errors.Add(
                        $"Failed to index UXML element-name references in '{path}': " +
                        exception.Message);
                }
            }

            foreach (var path in MCPUIToolkitAuditUtility.FindRuntimeSourceFiles(options))
            {
                try
                {
                    IndexTextNameReferences(
                        File.ReadAllText(MCPUIToolkitAuditUtility.ToFullPath(path)),
                        index, includeYamlScalars: false);
                    report.IndexedRuntimeSourceCount++;
                }
                catch (Exception exception)
                {
                    report.Errors.Add(
                        $"Failed to index runtime element-name references in '{path}': " +
                        exception.Message);
                }
            }

            foreach (var extension in new[] { ".prefab", ".asset", ".unity" })
            {
                foreach (var path in MCPUIToolkitAuditUtility.FindAssetFiles(extension, options))
                {
                    try
                    {
                        IndexTextNameReferences(
                            File.ReadAllText(MCPUIToolkitAuditUtility.ToFullPath(path)),
                            index, includeYamlScalars: true);
                        report.IndexedSerializedAssetCount++;
                    }
                    catch (Exception exception)
                    {
                        report.Errors.Add(
                            $"Failed to index serialized element-name references in '{path}': " +
                            exception.Message);
                    }
                }
            }

            return index;
        }

        private static void IndexStylesheetNameReferences(string text,
            UxmlElementNameReferenceIndex index)
        {
            var sanitized = ussCommentRegex.Replace(text ?? "", "");
            foreach (Match rule in ussRuleRegex.Matches(sanitized))
            {
                foreach (Match match in idTokenRegex.Matches(
                             rule.Groups["selector"].Value))
                {
                    index.AddReference(match.Groups["token"].Value);
                }
            }
        }

        private static void IndexUxmlNameReferences(XDocument document,
            UxmlElementNameReferenceIndex index)
        {
            foreach (var element in document.Descendants())
            {
                foreach (var attribute in element.Attributes())
                {
                    if (string.Equals(attribute.Name.LocalName, "name",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    index.AddReference(attribute.Value);
                }
            }
        }

        private static void IndexTextNameReferences(string text,
            UxmlElementNameReferenceIndex index, bool includeYamlScalars)
        {
            foreach (Match match in quotedNameReferenceRegex.Matches(text ?? ""))
            {
                index.AddReference(match.Groups["token"].Value);
            }

            if (includeYamlScalars == false)
            {
                return;
            }

            foreach (Match match in yamlListNameReferenceRegex.Matches(text ?? "")
                         .Cast<Match>()
                         .Concat(yamlScalarNameReferenceRegex.Matches(text ?? "")
                             .Cast<Match>()))
            {
                index.AddReference(
                    match.Groups["token"].Value.Trim().Trim('"', '\''));
            }
        }

        private static bool IsAuditableNamedElement(XElement element)
        {
            if (element == null ||
                string.Equals(element.Name.LocalName, "UXML",
                    StringComparison.OrdinalIgnoreCase) ||
                element.AncestorsAndSelf().Any(IsUxmlMetadataElement))
            {
                return false;
            }

            return string.IsNullOrWhiteSpace(AttributeValue(element, "name")) == false;
        }

        private static UxmlLayoutContractIndex BuildLayoutContractIndex(
            MCPUxmlLayoutAuditReport report, MCPUIToolkitAuditOptions options,
            IEnumerable<string> uxmlPaths)
        {
            var index = new UxmlLayoutContractIndex();
            foreach (var path in MCPUIToolkitAuditUtility.FindAssetFiles(".uss", options))
            {
                try
                {
                    IndexStyleSheetText(
                        File.ReadAllText(MCPUIToolkitAuditUtility.ToFullPath(path)), index);
                    report.IndexedStyleSheetCount++;
                }
                catch (Exception exception)
                {
                    report.Errors.Add($"Failed to index USS box contracts in '{path}': {exception.Message}");
                }
            }

            foreach (var path in uxmlPaths ?? Enumerable.Empty<string>())
            {
                try
                {
                    var document = XDocument.Parse(
                        File.ReadAllText(MCPUIToolkitAuditUtility.ToFullPath(path)),
                        LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo);
                    IndexUxmlDocument(document, index);
                }
                catch (Exception exception)
                {
                    report.Errors.Add($"Failed to index UXML layout variants in '{path}': " +
                                      exception.Message);
                }
            }

            return index;
        }

        private static void IndexStyleSheetText(string text, UxmlLayoutContractIndex index)
        {
            var sanitized = ussCommentRegex.Replace(text ?? "", "");
            foreach (Match rule in ussRuleRegex.Matches(sanitized))
            {
                var declarations = ParseStyle(rule.Groups["body"].Value);
                var selector = rule.Groups["selector"].Value;
                var classNames = classTokenRegex.Matches(selector)
                    .Cast<Match>()
                    .Select(match => match.Groups["token"].Value)
                    .Distinct(StringComparer.Ordinal)
                    .ToList();
                foreach (var className in classNames)
                {
                    foreach (var declaration in declarations.Where(declaration =>
                                 IsVariantLayoutProperty(declaration.Key)))
                    {
                        index.AddClassLayoutProperty(className, declaration.Key);
                    }
                }

                if (declarations.Any(property =>
                        IsBoxContractProperty(property.Key, property.Value)))
                {
                    foreach (var className in classNames)
                    {
                        index.BoxClasses.Add(className);
                    }

                    foreach (Match match in idTokenRegex.Matches(selector))
                    {
                        index.BoxIds.Add(match.Groups["token"].Value);
                    }
                }
            }
        }

        private static void IndexUxmlDocument(XDocument document,
            UxmlLayoutContractIndex index)
        {
            foreach (var element in document.Descendants())
            {
                var classNames = SplitWhitespace(AttributeValue(element, "class"))
                    .Distinct(StringComparer.Ordinal)
                    .ToList();
                foreach (var baseClass in classNames)
                {
                    foreach (var variantClass in classNames.Where(candidate =>
                                 candidate.StartsWith(baseClass + "-",
                                     StringComparison.Ordinal)))
                    {
                        index.AddAuthoredVariant(baseClass, variantClass);
                    }
                }
            }
        }

        private static UxmlInlineStyleContractIndex BuildInlineStyleContractIndex(
            string uxmlAssetPath, XDocument document, MCPUxmlLayoutAuditReport report)
        {
            var index = new UxmlInlineStyleContractIndex();
            var indexedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var styleElement in document.Descendants()
                         .Where(element => element.Name.LocalName == "Style"))
            {
                var stylePath = ResolveStyleReference(
                    AttributeValue(styleElement, "src"), uxmlAssetPath);
                if (string.IsNullOrWhiteSpace(stylePath) ||
                    indexedPaths.Add(stylePath) == false)
                {
                    continue;
                }

                try
                {
                    var fullPath = MCPUIToolkitAuditUtility.ToFullPath(stylePath);
                    if (File.Exists(fullPath) == false)
                    {
                        report.Errors.Add(
                            $"Referenced USS asset does not exist: {stylePath} " +
                            $"(from {uxmlAssetPath}).");
                        continue;
                    }

                    IndexInlineStyleSheetText(stylePath, File.ReadAllText(fullPath), index);
                }
                catch (Exception exception)
                {
                    report.Errors.Add(
                        $"Failed to index referenced USS defaults in '{stylePath}': " +
                        exception.Message);
                }
            }

            return index;
        }

        private static string ResolveStyleReference(string rawPath, string uxmlAssetPath)
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

            path = Uri.UnescapeDataString(path);
            if (Path.IsPathRooted(path))
            {
                return MCPUIToolkitAuditUtility.ToAssetPath(path);
            }

            if (path.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) ||
                path.StartsWith("Packages/", StringComparison.OrdinalIgnoreCase))
            {
                return MCPUIToolkitAuditUtility.NormalizeAssetPath(path);
            }

            var ownerDirectory = Path.GetDirectoryName(uxmlAssetPath) ?? "";
            var combined = Path.Combine(ownerDirectory,
                path.Replace('/', Path.DirectorySeparatorChar));
            return MCPUIToolkitAuditUtility.ToAssetPath(
                MCPUIToolkitAuditUtility.ToFullPath(combined));
        }

        private static void IndexInlineStyleSheetText(string sourcePath, string text,
            UxmlInlineStyleContractIndex index)
        {
            var sanitized = ussCommentRegex.Replace(text ?? "", "");
            foreach (Match rule in ussRuleRegex.Matches(sanitized))
            {
                var declarations = ParseStyle(rule.Groups["body"].Value);
                if (declarations.Count == 0)
                {
                    continue;
                }

                foreach (var rawSelector in rule.Groups["selector"].Value.Split(','))
                {
                    if (TryParseSimpleSelector(rawSelector, out var selector) == false)
                    {
                        continue;
                    }

                    index.AddRule(sourcePath, selector, declarations);
                }
            }
        }

        private static bool TryParseSimpleSelector(string rawSelector,
            out UxmlSimpleSelector selector)
        {
            selector = null;
            var value = (rawSelector ?? "").Trim();
            var match = Regex.Match(value,
                @"^(?<type>[A-Za-z_][A-Za-z0-9_-]*)?" +
                @"(?<tokens>(?:[.#][A-Za-z_][A-Za-z0-9_-]*)*)$");
            if (match.Success == false || value.Length == 0)
            {
                return false;
            }

            var classNames = classTokenRegex.Matches(value)
                .Cast<Match>()
                .Select(item => item.Groups["token"].Value)
                .Distinct(StringComparer.Ordinal)
                .ToList();
            var ids = idTokenRegex.Matches(value)
                .Cast<Match>()
                .Select(item => item.Groups["token"].Value)
                .Distinct(StringComparer.Ordinal)
                .ToList();
            if (ids.Count > 1)
            {
                return false;
            }

            var typeName = match.Groups["type"].Value;
            if (string.IsNullOrWhiteSpace(typeName) &&
                classNames.Count == 0 &&
                ids.Count == 0)
            {
                return false;
            }

            selector = new UxmlSimpleSelector
            {
                Text = value,
                TypeName = typeName,
                Id = ids.SingleOrDefault() ?? "",
                Specificity = ids.Count * 100 + classNames.Count * 10 +
                              (string.IsNullOrWhiteSpace(typeName) ? 0 : 1)
            };
            selector.ClassNames.AddRange(classNames);
            return true;
        }

        private static void AuditRedundantInlineDeclarations(string assetPath,
            XDocument document, UxmlInlineStyleContractIndex inlineStyleContracts,
            MCPUxmlLayoutAuditReport report, bool includeSuppressed)
        {
            foreach (var element in document.Descendants())
            {
                var inlineDeclarations = ParseStyle(AttributeValue(element, "style"));
                if (inlineDeclarations.Count == 0)
                {
                    continue;
                }

                var stylesheetDeclarations = inlineStyleContracts.Resolve(element);
                var redundant = inlineDeclarations
                    .Where(declaration =>
                        stylesheetDeclarations.TryGetValue(declaration.Key,
                            out var stylesheetDeclaration) &&
                        StyleValuesEqual(declaration.Value, stylesheetDeclaration.Value))
                    .OrderBy(declaration => declaration.Key,
                        StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(declaration => declaration.Key,
                        declaration => declaration.Value,
                        StringComparer.OrdinalIgnoreCase);
                if (redundant.Count == 0)
                {
                    continue;
                }

                var name = AttributeValue(element, "name");
                var elementLabel = string.IsNullOrWhiteSpace(name)
                    ? $"<{element.Name.LocalName}>"
                    : $"#{name}";
                var stylesheetRules = redundant.Keys.Select(property =>
                {
                    var source = stylesheetDeclarations[property];
                    return new Dictionary<string, object>
                    {
                        { "property", property },
                        { "selector", source.Selector },
                        { "sourcePath", source.SourcePath }
                    };
                }).ToList();
                var sourceLabels = stylesheetRules
                    .Select(source =>
                        $"{source["selector"]} in {source["sourcePath"]}")
                    .Distinct(StringComparer.Ordinal)
                    .ToList();
                var suppressionReason = GetSuppressionReason(element,
                    redundantInlineSuppressionRegex);
                var issue = new MCPUxmlLayoutAuditIssue
                {
                    AssetPath = assetPath,
                    Line = GetLineNumber(element),
                    Element = elementLabel,
                    ElementName = name,
                    Kind = "redundant-inline-declaration",
                    Axis = GetLayoutAxis(redundant.Keys),
                    FixedProperties = redundant.Keys.ToList(),
                    InlineDeclarations = redundant,
                    StylesheetRules = stylesheetRules,
                    Suppressed = string.IsNullOrWhiteSpace(suppressionReason) == false,
                    SuppressionReason = suppressionReason,
                    Message =
                        $"Inline style {FormatDeclarations(redundant)} on {elementLabel} repeats " +
                        $"the same default value supplied by {string.Join(", ", sourceLabels)}. " +
                        "Remove the redundant inline declaration so the loaded USS remains the " +
                        "single style owner."
                };
                report.Record(issue, includeSuppressed);
            }
        }

        private static void AuditVisuallyInertTextStretch(string assetPath,
            XDocument document, UxmlInlineStyleContractIndex inlineStyleContracts,
            UxmlLayoutContractIndex layoutContracts, MCPUxmlLayoutAuditReport report,
            bool includeSuppressed)
        {
            foreach (var element in document.Descendants())
            {
                var parent = element.Parent as XElement;
                if (parent == null)
                {
                    continue;
                }

                var elementType = ResolveVisualElementType(element);
                if (elementType == null ||
                    typeof(Label).IsAssignableFrom(elementType) == false ||
                    ResolveVisualElementType(parent) != typeof(VisualElement))
                {
                    continue;
                }

                var inlineStyle = ParseStyle(AttributeValue(element, "style"));
                if (StyleValue(inlineStyle, "align-self") != "stretch")
                {
                    continue;
                }

                var stylesheetStyle = inlineStyleContracts.Resolve(element);
                if (stylesheetStyle.TryGetValue("align-self",
                        out var stylesheetAlignment) &&
                    StyleValuesEqual(stylesheetAlignment.Value, "stretch"))
                {
                    continue;
                }

                var parentStyle = ResolveAuthoredStyle(parent, inlineStyleContracts);
                if (StyleValue(parentStyle, "align-items") != "center")
                {
                    continue;
                }

                var flexDirection = StyleValue(parentStyle, "flex-direction");
                if (string.IsNullOrWhiteSpace(flexDirection))
                {
                    flexDirection = "column";
                }

                var horizontal = flexDirection == "column" ||
                                 flexDirection == "column-reverse";
                var vertical = flexDirection == "row" ||
                               flexDirection == "row-reverse";
                if (horizontal == false && vertical == false)
                {
                    continue;
                }

                var elementStyle = ResolveAuthoredStyle(element, inlineStyleContracts);
                var textAlignment = StyleValue(elementStyle, "-unity-text-align");
                if ((horizontal && textAlignment.EndsWith("-center",
                         StringComparison.Ordinal) == false) ||
                    (vertical && textAlignment.StartsWith("middle-",
                         StringComparison.Ordinal) == false) ||
                    HasAxisSizeContract(elementStyle, horizontal) ||
                    HasVisualBoxContract(element, elementStyle, layoutContracts) ||
                    HasSymmetricCrossAxisMargins(elementStyle, horizontal) == false)
                {
                    continue;
                }

                var name = AttributeValue(element, "name");
                var elementLabel = string.IsNullOrWhiteSpace(name)
                    ? $"<{element.Name.LocalName}>"
                    : $"#{name}";
                var axis = horizontal ? "horizontal" : "vertical";
                var suppressionReason = GetSuppressionReason(element,
                    inertTextStretchSuppressionRegex);
                var issue = new MCPUxmlLayoutAuditIssue
                {
                    AssetPath = assetPath,
                    Line = GetLineNumber(element),
                    Element = elementLabel,
                    ElementName = name,
                    Kind = "visually-inert-text-stretch",
                    Axis = axis,
                    FixedProperties = new List<string> { "align-self" },
                    InlineDeclarations = new Dictionary<string, string>(
                        StringComparer.OrdinalIgnoreCase)
                    {
                        { "align-self", inlineStyle["align-self"] }
                    },
                    Suppressed = string.IsNullOrWhiteSpace(suppressionReason) == false,
                    SuppressionReason = suppressionReason,
                    Message =
                        $"Inline align-self: stretch expands the transparent {elementLabel} " +
                        $"{axis} layout box, but its plain VisualElement parent already centers " +
                        $"the cross axis, the text alignment is {textAlignment}, and the opposing " +
                        "margins are equal. The glyph center is unchanged at the element's natural " +
                        "size; remove the inert stretch declaration."
                };
                report.Record(issue, includeSuppressed);
            }
        }

        private static void AuditVisuallyInertTextGrow(string assetPath,
            XDocument document, UxmlInlineStyleContractIndex inlineStyleContracts,
            UxmlLayoutContractIndex layoutContracts, MCPUxmlLayoutAuditReport report,
            bool includeSuppressed)
        {
            foreach (var element in document.Descendants())
            {
                var parent = element.Parent as XElement;
                if (parent == null)
                {
                    continue;
                }

                var elementType = ResolveVisualElementType(element);
                if (elementType == null ||
                    typeof(Label).IsAssignableFrom(elementType) == false ||
                    ResolveVisualElementType(parent) != typeof(VisualElement))
                {
                    continue;
                }

                var inlineStyle = ParseStyle(AttributeValue(element, "style"));
                if (inlineStyle.TryGetValue("flex-grow", out var inlineGrow) == false ||
                    float.TryParse(inlineGrow, NumberStyles.Float,
                        CultureInfo.InvariantCulture, out var grow) == false ||
                    float.IsNaN(grow) || float.IsInfinity(grow) || grow <= 0)
                {
                    continue;
                }

                var stylesheetStyle = inlineStyleContracts.Resolve(element);
                if (stylesheetStyle.TryGetValue("flex-grow",
                        out var stylesheetGrow) &&
                    StyleValuesEqual(stylesheetGrow.Value, inlineGrow))
                {
                    continue;
                }

                var parentStyle = ResolveAuthoredStyle(parent, inlineStyleContracts);
                if (StyleValue(parentStyle, "justify-content") != "center" ||
                    IsOnlyVisualContentChild(parent, element) == false)
                {
                    continue;
                }

                var flexDirection = StyleValue(parentStyle, "flex-direction");
                if (string.IsNullOrWhiteSpace(flexDirection))
                {
                    flexDirection = "column";
                }

                var horizontal = flexDirection == "row" ||
                                 flexDirection == "row-reverse";
                var vertical = flexDirection == "column" ||
                               flexDirection == "column-reverse";
                if (horizontal == false && vertical == false)
                {
                    continue;
                }

                var elementStyle = ResolveAuthoredStyle(element, inlineStyleContracts);
                var textAlignment = StyleValue(elementStyle, "-unity-text-align");
                if ((horizontal && textAlignment.EndsWith("-center",
                         StringComparison.Ordinal) == false) ||
                    (vertical && textAlignment.StartsWith("middle-",
                         StringComparison.Ordinal) == false) ||
                    HasAxisSizeContract(elementStyle, horizontal) ||
                    HasVisualBoxContract(element, elementStyle, layoutContracts))
                {
                    continue;
                }

                var name = AttributeValue(element, "name");
                var elementLabel = string.IsNullOrWhiteSpace(name)
                    ? $"<{element.Name.LocalName}>"
                    : $"#{name}";
                var axis = horizontal ? "horizontal" : "vertical";
                var suppressionReason = GetSuppressionReason(element,
                    inertTextGrowSuppressionRegex);
                var issue = new MCPUxmlLayoutAuditIssue
                {
                    AssetPath = assetPath,
                    Line = GetLineNumber(element),
                    Element = elementLabel,
                    ElementName = name,
                    Kind = "visually-inert-text-grow",
                    Axis = axis,
                    FixedProperties = new List<string> { "flex-grow" },
                    InlineDeclarations = new Dictionary<string, string>(
                        StringComparer.OrdinalIgnoreCase)
                    {
                        { "flex-grow", inlineGrow }
                    },
                    Suppressed = string.IsNullOrWhiteSpace(suppressionReason) == false,
                    SuppressionReason = suppressionReason,
                    Message =
                        $"Inline flex-grow: {inlineGrow} expands the transparent {elementLabel} " +
                        $"along its parent's {axis} main axis, but the plain VisualElement parent " +
                        "has no other visual child and already centers that axis with " +
                        $"justify-content: center. With text alignment {textAlignment}, the glyph " +
                        "center is unchanged at the Label's natural size; remove the inert grow " +
                        "declaration."
                };
                report.Record(issue, includeSuppressed);
            }
        }

        private static void AuditSingleChildCenteringWrapper(string assetPath,
            XDocument document, UxmlInlineStyleContractIndex inlineStyleContracts,
            UxmlLayoutContractIndex layoutContracts, MCPUxmlLayoutAuditReport report,
            bool includeSuppressed)
        {
            foreach (var wrapper in document.Descendants())
            {
                var parent = wrapper.Parent as XElement;
                if (parent == null ||
                    ResolveVisualElementType(wrapper) != typeof(VisualElement) ||
                    ResolveVisualElementType(parent) != typeof(VisualElement) ||
                    string.Equals(AttributeValue(wrapper, "picking-mode"), "Ignore",
                        StringComparison.OrdinalIgnoreCase) == false)
                {
                    continue;
                }

                var inlineStyle = ParseStyle(AttributeValue(wrapper, "style"));
                if (StyleValue(inlineStyle, "position") != "absolute" ||
                    StyleValue(inlineStyle, "align-items") != "center" ||
                    TryGetPixelLength(inlineStyle, "left", out var left) == false ||
                    TryGetPixelLength(inlineStyle, "right", out var right) == false ||
                    Math.Abs(left - right) > CENTER_EPSILON ||
                    inlineStyle.ContainsKey("width") ||
                    inlineStyle.ContainsKey("min-width") ||
                    inlineStyle.ContainsKey("max-width"))
                {
                    continue;
                }

                var hasTop = inlineStyle.ContainsKey("top");
                var hasBottom = inlineStyle.ContainsKey("bottom");
                if (hasTop == hasBottom ||
                    (hasTop && TryGetPixelLength(inlineStyle, "top", out _) == false) ||
                    (hasBottom && TryGetPixelLength(inlineStyle, "bottom", out _) == false))
                {
                    continue;
                }

                var wrapperStyle = ResolveAuthoredStyle(wrapper, inlineStyleContracts);
                var wrapperDirection = StyleValue(wrapperStyle, "flex-direction");
                var wrapperJustification = StyleValue(wrapperStyle, "justify-content");
                if ((string.IsNullOrWhiteSpace(wrapperDirection) == false &&
                     wrapperDirection != "column" &&
                     wrapperDirection != "column-reverse") ||
                    (string.IsNullOrWhiteSpace(wrapperJustification) == false &&
                     wrapperJustification != "flex-start") ||
                    HasUnexpectedCenteringWrapperStyle(wrapperStyle) ||
                    HasVisualBoxContract(wrapper, wrapperStyle, layoutContracts) ||
                    HasInteractionContract(wrapper) ||
                    HasNonZeroMargin(wrapperStyle))
                {
                    continue;
                }

                var parentStyle = ResolveAuthoredStyle(parent, inlineStyleContracts);
                if (HasAxisSizeContract(parentStyle, true) == false)
                {
                    continue;
                }

                var visualChildren = GetVisualContentChildren(wrapper);
                if (visualChildren.Count != 1)
                {
                    continue;
                }

                var child = visualChildren[0];
                if (ResolveVisualElementType(child) != typeof(VisualElement))
                {
                    continue;
                }

                var childInlineStyle = ParseStyle(AttributeValue(child, "style"));
                var childStyle = ResolveAuthoredStyle(child, inlineStyleContracts);
                var childPosition = StyleValue(childStyle, "position");
                var childAlignment = StyleValue(childStyle, "align-self");
                if (TryGetPixelLength(childInlineStyle, "width", out _) == false ||
                    TryGetPixelLength(childInlineStyle, "height", out var childHeight) == false ||
                    childStyle.ContainsKey("left") ||
                    childStyle.ContainsKey("right") ||
                    childStyle.ContainsKey("top") ||
                    childStyle.ContainsKey("bottom") ||
                    (string.IsNullOrWhiteSpace(childPosition) == false &&
                     childPosition != "relative") ||
                    (string.IsNullOrWhiteSpace(childAlignment) == false &&
                     childAlignment != "auto") ||
                    HasNonZeroMargin(childStyle) ||
                    HasPositiveFlexGrow(childStyle) ||
                    HasVisualBoxContract(child, childStyle, layoutContracts) == false)
                {
                    continue;
                }

                var hasWrapperHeight = inlineStyle.ContainsKey("height");
                if (hasWrapperHeight &&
                    (TryGetPixelLength(inlineStyle, "height", out var wrapperHeight) == false ||
                     Math.Abs(wrapperHeight - childHeight) > CENTER_EPSILON))
                {
                    continue;
                }

                var name = AttributeValue(wrapper, "name");
                var wrapperLabel = string.IsNullOrWhiteSpace(name)
                    ? $"<{wrapper.Name.LocalName}>"
                    : $"#{name}";
                var childName = AttributeValue(child, "name");
                var childLabel = string.IsNullOrWhiteSpace(childName)
                    ? $"<{child.Name.LocalName}>"
                    : $"#{childName}";
                var fixedProperties = new List<string> { "left", "right" };
                if (hasWrapperHeight)
                {
                    fixedProperties.Add("height");
                }

                fixedProperties.Add("align-items");
                var inlineDeclarations = fixedProperties.ToDictionary(property => property,
                    property => inlineStyle[property],
                    StringComparer.OrdinalIgnoreCase);
                var suppressionReason = GetSuppressionReason(wrapper,
                    singleChildCenteringWrapperSuppressionRegex);
                var heightClause = hasWrapperHeight
                    ? $" Its height repeats {childLabel}'s {FormatPixels(childHeight)} height."
                    : "";
                var issue = new MCPUxmlLayoutAuditIssue
                {
                    AssetPath = assetPath,
                    Line = GetLineNumber(wrapper),
                    Element = wrapperLabel,
                    ElementName = name,
                    Kind = "single-child-centering-wrapper",
                    Axis = "horizontal",
                    FixedProperties = fixedProperties,
                    InlineDeclarations = inlineDeclarations,
                    Suppressed = string.IsNullOrWhiteSpace(suppressionReason) == false,
                    SuppressionReason = suppressionReason,
                    Message =
                        $"Transparent absolute {wrapperLabel} spans equal parent side insets only " +
                        $"to center its sole fixed-size visual child {childLabel}.{heightClause} " +
                        $"Move the wrapper's {(hasTop ? "top" : "bottom")} position to {childLabel}, " +
                        "set the child to position: absolute and align-self: center, and remove the " +
                        "layout-only wrapper."
                };
                report.Record(issue, includeSuppressed);
            }
        }

        private static void AuditFixedScrollCrossAxisContentSizes(string assetPath,
            XDocument document, UxmlInlineStyleContractIndex inlineStyleContracts,
            UxmlLayoutContractIndex layoutContracts, MCPUxmlLayoutAuditReport report,
            bool includeSuppressed)
        {
            foreach (var scrollView in document.Descendants())
            {
                var scrollViewType = ResolveVisualElementType(scrollView);
                if (scrollViewType == null ||
                    typeof(ScrollView).IsAssignableFrom(scrollViewType) == false ||
                    TryGetSingleAxisScrollCrossAxis(scrollView, out var crossAxis,
                        out var crossSizeProperty) == false)
                {
                    continue;
                }

                var scrollStyle = ResolveAuthoredStyle(scrollView, inlineStyleContracts);
                if (TryGetPixelLength(scrollStyle, crossSizeProperty,
                        out var scrollCrossSize) == false ||
                    scrollCrossSize <= 0)
                {
                    continue;
                }

                foreach (var contentWrapper in GetVisualContentChildren(scrollView))
                {
                    if (ResolveVisualElementType(contentWrapper) != typeof(VisualElement) ||
                        GetVisualContentChildren(contentWrapper).Count == 0)
                    {
                        continue;
                    }

                    var inlineStyle = ParseStyle(AttributeValue(contentWrapper, "style"));
                    if (TryGetPixelLength(inlineStyle, crossSizeProperty,
                            out var wrapperCrossSize) == false ||
                        wrapperCrossSize <= 0 ||
                        wrapperCrossSize + CENTER_EPSILON < scrollCrossSize)
                    {
                        continue;
                    }

                    var wrapperStyle =
                        ResolveAuthoredStyle(contentWrapper, inlineStyleContracts);
                    if (StyleValue(wrapperStyle, "position") == "absolute" ||
                        StyleValue(wrapperStyle, "display") == "none" ||
                        HasVisualBoxContract(contentWrapper, wrapperStyle, layoutContracts) ||
                        HasInteractionContract(contentWrapper) ||
                        HasNonZeroMargin(wrapperStyle))
                    {
                        continue;
                    }

                    var wrapperName = AttributeValue(contentWrapper, "name");
                    var wrapperLabel = string.IsNullOrWhiteSpace(wrapperName)
                        ? $"<{contentWrapper.Name.LocalName}>"
                        : $"#{wrapperName}";
                    var scrollName = AttributeValue(scrollView, "name");
                    var scrollLabel = string.IsNullOrWhiteSpace(scrollName)
                        ? $"<{scrollView.Name.LocalName}>"
                        : $"#{scrollName}";
                    var suppressionReason = GetSuppressionReason(contentWrapper,
                        fixedScrollCrossAxisSizeSuppressionRegex);
                    var extentClause = wrapperCrossSize > scrollCrossSize + CENTER_EPSILON
                        ? $"exceeds {scrollLabel}'s authored {FormatPixels(scrollCrossSize)} " +
                          $"{crossSizeProperty} and creates overflow on an axis this " +
                          "single-axis ScrollView does not scroll"
                        : $"repeats {scrollLabel}'s authored {FormatPixels(scrollCrossSize)} " +
                          $"{crossSizeProperty} instead of using the content container's " +
                          "default cross-axis stretch";
                    var issue = new MCPUxmlLayoutAuditIssue
                    {
                        AssetPath = assetPath,
                        Line = GetLineNumber(contentWrapper),
                        Element = wrapperLabel,
                        ElementName = wrapperName,
                        Kind = "fixed-scroll-cross-axis-content-size",
                        Axis = crossAxis,
                        FixedProperties = new List<string> { crossSizeProperty },
                        ParentSize = scrollCrossSize,
                        Size = wrapperCrossSize,
                        InlineDeclarations = new Dictionary<string, string>(
                            StringComparer.OrdinalIgnoreCase)
                        {
                            { crossSizeProperty, inlineStyle[crossSizeProperty] }
                        },
                        Suppressed = string.IsNullOrWhiteSpace(suppressionReason) == false,
                        SuppressionReason = suppressionReason,
                        Message =
                            $"Plain direct content wrapper {wrapperLabel} fixes its " +
                            $"{crossAxis} cross-axis {crossSizeProperty} to " +
                            $"{FormatPixels(wrapperCrossSize)}, which {extentClause}. " +
                            $"Remove the fixed {crossSizeProperty} and let the ScrollView own " +
                            "its viewport while the wrapper stretches on the cross axis."
                    };
                    report.Record(issue, includeSuppressed);
                }
            }
        }

        private static bool TryGetSingleAxisScrollCrossAxis(XElement scrollView,
            out string crossAxis, out string crossSizeProperty)
        {
            var mode = AttributeValue(scrollView, "mode").Trim();
            if (string.IsNullOrWhiteSpace(mode) ||
                string.Equals(mode, "Vertical", StringComparison.OrdinalIgnoreCase))
            {
                crossAxis = "horizontal";
                crossSizeProperty = "width";
                return true;
            }

            if (string.Equals(mode, "Horizontal", StringComparison.OrdinalIgnoreCase))
            {
                crossAxis = "vertical";
                crossSizeProperty = "height";
                return true;
            }

            crossAxis = "";
            crossSizeProperty = "";
            return false;
        }

        private static void AuditUnconsumedElementNames(string assetPath,
            XDocument document, UxmlElementNameReferenceIndex elementNameReferences,
            MCPUxmlLayoutAuditReport report, bool includeSuppressed)
        {
            if (elementNameReferences == null || elementNameReferences.Enabled == false)
            {
                return;
            }

            foreach (var element in document.Descendants().Where(IsAuditableNamedElement))
            {
                var name = AttributeValue(element, "name");
                if (elementNameReferences.IsReferenced(name))
                {
                    continue;
                }

                var suppressionReason = GetSuppressionReason(element,
                    unconsumedElementNameSuppressionRegex);
                var issue = new MCPUxmlLayoutAuditIssue
                {
                    AssetPath = assetPath,
                    Line = GetLineNumber(element),
                    Element = $"#{name}",
                    ElementName = name,
                    Kind = "unconsumed-element-name",
                    FixedProperties = new List<string> { "name" },
                    InlineDeclarations = new Dictionary<string, string>(
                        StringComparer.OrdinalIgnoreCase)
                    {
                        { "name", name }
                    },
                    Suppressed = string.IsNullOrWhiteSpace(suppressionReason) == false,
                    SuppressionReason = suppressionReason,
                    Message =
                        $"Authored element #{name} declares a name with no USS ID selector, " +
                        "AttributeOverrides target, serialized asset reference, or runtime string " +
                        "lookup in the configured audit scope. Remove the unconsumed name; hierarchy " +
                        "and reusable classes should carry structure that has no lookup consumer."
                };
                report.Record(issue, includeSuppressed);
            }
        }

        private static void AuditFixedFlexPartitions(string assetPath,
            XDocument document, UxmlInlineStyleContractIndex inlineStyleContracts,
            MCPUxmlLayoutAuditReport report, bool includeSuppressed)
        {
            foreach (var parent in document.Descendants())
            {
                if (IsUxmlMetadataElement(parent))
                {
                    continue;
                }

                var parentStyle = ResolveAuthoredStyle(parent, inlineStyleContracts);
                var flexDirection = StyleValue(parentStyle, "flex-direction");
                if (string.IsNullOrWhiteSpace(flexDirection))
                {
                    flexDirection = "column";
                }

                var horizontal = flexDirection == "row" ||
                                 flexDirection == "row-reverse";
                var vertical = flexDirection == "column" ||
                               flexDirection == "column-reverse";
                if ((horizontal == false && vertical == false) ||
                    HasNonZeroPartitionSpacing(parentStyle, horizontal) ||
                    (parentStyle.TryGetValue("flex-wrap", out var flexWrap) &&
                     string.Equals(flexWrap.Trim(), "nowrap",
                         StringComparison.OrdinalIgnoreCase) == false) ||
                    (parentStyle.TryGetValue("justify-content", out var justification) &&
                     string.Equals(justification.Trim(), "flex-start",
                         StringComparison.OrdinalIgnoreCase) == false))
                {
                    continue;
                }

                var mainSizeProperty = horizontal ? "width" : "height";
                if (TryGetPixelLength(parentStyle, mainSizeProperty,
                        out var parentSize) == false ||
                    parentSize <= 0)
                {
                    continue;
                }

                var children = GetVisualContentChildren(parent);
                if (children.Count != 2)
                {
                    continue;
                }

                var childStyles = new List<Dictionary<string, string>>();
                var partitionSize = 0f;
                var validPartition = true;
                foreach (var child in children)
                {
                    var childStyle = ResolveAuthoredStyle(child, inlineStyleContracts);
                    var position = StyleValue(childStyle, "position");
                    if (position == "absolute" ||
                        StyleValue(childStyle, "display") == "none" ||
                        TryGetPixelLength(childStyle, mainSizeProperty,
                            out var childSize) == false ||
                        childSize < 0 ||
                        HasPositiveFlexGrow(childStyle) ||
                        HasExplicitZeroFlexShrink(childStyle) == false ||
                        HasNonZeroMainAxisMargins(childStyle, horizontal))
                    {
                        validPartition = false;
                        break;
                    }

                    childStyles.Add(childStyle);
                    partitionSize += childSize;
                }

                if (validPartition == false ||
                    Math.Abs(partitionSize - parentSize) > CENTER_EPSILON)
                {
                    continue;
                }

                var remainder = children[children.Count - 1];
                var remainderStyle = childStyles[childStyles.Count - 1];
                var remainderName = AttributeValue(remainder, "name");
                var remainderLabel = string.IsNullOrWhiteSpace(remainderName)
                    ? $"<{remainder.Name.LocalName}>"
                    : $"#{remainderName}";
                var parentName = AttributeValue(parent, "name");
                var parentLabel = string.IsNullOrWhiteSpace(parentName)
                    ? $"<{parent.Name.LocalName}>"
                    : $"#{parentName}";
                var suppressionReason = GetSuppressionReason(parent,
                    fixedFlexPartitionSuppressionRegex);
                if (string.IsNullOrWhiteSpace(suppressionReason))
                {
                    suppressionReason = GetSuppressionReason(remainder,
                        fixedFlexPartitionSuppressionRegex);
                }

                var axis = horizontal ? "horizontal" : "vertical";
                var issue = new MCPUxmlLayoutAuditIssue
                {
                    AssetPath = assetPath,
                    Line = GetLineNumber(parent),
                    Element = parentLabel,
                    ElementName = parentName,
                    Kind = "fixed-flex-partition",
                    Axis = axis,
                    FixedProperties = new List<string>
                    {
                        mainSizeProperty,
                        "flex-shrink"
                    },
                    InlineDeclarations = new Dictionary<string, string>(
                        StringComparer.OrdinalIgnoreCase)
                    {
                        { mainSizeProperty, remainderStyle[mainSizeProperty] },
                        { "flex-shrink", remainderStyle["flex-shrink"] }
                    },
                    Size = parentSize,
                    Suppressed = string.IsNullOrWhiteSpace(suppressionReason) == false,
                    SuppressionReason = suppressionReason,
                    Message =
                        $"Fixed {FormatPixels(parentSize)} {parentLabel} is fully partitioned " +
                        $"along its {axis} flex axis by {children.Count} in-flow children whose " +
                        $"{mainSizeProperty} values sum to the parent size, while every child " +
                        "also repeats flex-shrink: 0. Keep the fixed chrome size, remove " +
                        $"{mainSizeProperty} and flex-shrink from one remainder region such as " +
                        $"{remainderLabel}, set that region to flex-grow: 1, and let the parent " +
                        "own the total size."
                };
                report.Record(issue, includeSuppressed);
            }
        }

        private static bool HasExplicitZeroFlexShrink(
            IReadOnlyDictionary<string, string> style)
        {
            return style.TryGetValue("flex-shrink", out var value) &&
                   float.TryParse(value, NumberStyles.Float,
                       CultureInfo.InvariantCulture, out var shrink) &&
                   float.IsNaN(shrink) == false &&
                   float.IsInfinity(shrink) == false &&
                   Math.Abs(shrink) <= CENTER_EPSILON;
        }

        private static bool HasNonZeroPartitionSpacing(
            IReadOnlyDictionary<string, string> style, bool horizontal)
        {
            var properties = horizontal
                ? new[]
                {
                    "padding-left", "padding-right", "border-left-width",
                    "border-right-width", "column-gap"
                }
                : new[]
                {
                    "padding-top", "padding-bottom", "border-top-width",
                    "border-bottom-width", "row-gap"
                };
            return properties.Any(property =>
                       style.TryGetValue(property, out var value) &&
                       IsZeroBoxValue(value) == false) ||
                   style.TryGetValue("padding", out var padding) &&
                   IsZeroBoxValue(padding) == false ||
                   style.TryGetValue("gap", out var gap) &&
                   IsZeroBoxValue(gap) == false;
        }

        private static bool HasNonZeroMainAxisMargins(
            IReadOnlyDictionary<string, string> style, bool horizontal)
        {
            var properties = horizontal
                ? new[] { "margin-left", "margin-right" }
                : new[] { "margin-top", "margin-bottom" };
            return properties.Any(property =>
                       style.TryGetValue(property, out var value) &&
                       IsZeroBoxValue(value) == false) ||
                   style.TryGetValue("margin", out var margin) &&
                   IsZeroBoxValue(margin) == false;
        }

        private static bool HasUnexpectedCenteringWrapperStyle(
            IReadOnlyDictionary<string, string> style)
        {
            var allowedProperties = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "position",
                "left",
                "right",
                "top",
                "bottom",
                "height",
                "align-items",
                "justify-content",
                "flex-direction"
            };
            return style.Keys.Any(property => allowedProperties.Contains(property) == false);
        }

        private static bool HasInteractionContract(XElement element)
        {
            return element.Elements().Any(child => child.Name.LocalName == "Bindings") ||
                   string.Equals(AttributeValue(element, "focusable"), "true",
                       StringComparison.OrdinalIgnoreCase) ||
                   string.IsNullOrWhiteSpace(AttributeValue(element, "tabindex")) == false ||
                   string.IsNullOrWhiteSpace(AttributeValue(element, "tooltip")) == false ||
                   string.Equals(AttributeValue(element, "picking-mode"), "Position",
                       StringComparison.OrdinalIgnoreCase);
        }

        private static bool HasNonZeroMargin(IReadOnlyDictionary<string, string> style)
        {
            return style.Any(property =>
                (string.Equals(property.Key, "margin", StringComparison.OrdinalIgnoreCase) ||
                 property.Key.StartsWith("margin-", StringComparison.OrdinalIgnoreCase)) &&
                IsZeroBoxValue(property.Value) == false);
        }

        private static bool HasPositiveFlexGrow(IReadOnlyDictionary<string, string> style)
        {
            return style.TryGetValue("flex-grow", out var value) &&
                   float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture,
                       out var grow) &&
                   float.IsNaN(grow) == false &&
                   float.IsInfinity(grow) == false &&
                   grow > 0;
        }

        private static bool TryGetPixelLength(IReadOnlyDictionary<string, string> style,
            string property, out float value)
        {
            value = 0;
            if (style.TryGetValue(property, out var rawValue) == false)
            {
                return false;
            }

            var normalized = rawValue.Trim();
            if (Regex.IsMatch(normalized, @"^[+-]?0(?:\.0+)?$"))
            {
                return true;
            }

            return TryGetPixels(style, property, out value);
        }

        private static bool IsOnlyVisualContentChild(XElement parent, XElement element)
        {
            var visualChildren = GetVisualContentChildren(parent);
            return visualChildren.Count == 1 && visualChildren[0] == element;
        }

        private static List<XElement> GetVisualContentChildren(XElement parent)
        {
            return parent.Elements()
                .Where(child => IsUxmlMetadataElement(child) == false)
                .ToList();
        }

        private static bool IsUxmlMetadataElement(XElement element)
        {
            switch (element.Name.LocalName)
            {
                case "Bindings":
                case "Style":
                case "Template":
                case "AttributeOverrides":
                    return true;
                default:
                    return false;
            }
        }

        private static Dictionary<string, string> ResolveAuthoredStyle(XElement element,
            UxmlInlineStyleContractIndex inlineStyleContracts)
        {
            var result = inlineStyleContracts.Resolve(element)
                .ToDictionary(declaration => declaration.Key,
                    declaration => declaration.Value.Value,
                    StringComparer.OrdinalIgnoreCase);
            foreach (var declaration in ParseStyle(AttributeValue(element, "style")))
            {
                result[declaration.Key] = declaration.Value;
            }

            return result;
        }

        private static bool HasAxisSizeContract(
            IReadOnlyDictionary<string, string> style, bool horizontal)
        {
            var properties = horizontal
                ? new[] { "width", "min-width", "max-width" }
                : new[] { "height", "min-height", "max-height" };
            return properties.Any(property =>
                style.TryGetValue(property, out var value) &&
                string.IsNullOrWhiteSpace(value) == false &&
                string.Equals(value.Trim(), "auto", StringComparison.OrdinalIgnoreCase) == false &&
                string.Equals(value.Trim(), "none", StringComparison.OrdinalIgnoreCase) == false);
        }

        private static bool HasVisualBoxContract(XElement element,
            IReadOnlyDictionary<string, string> style,
            UxmlLayoutContractIndex layoutContracts)
        {
            if (style.Any(property =>
                    IsMeaningfulVisualBoxProperty(property.Key, property.Value)))
            {
                return true;
            }

            var name = AttributeValue(element, "name");
            if (string.IsNullOrWhiteSpace(name) == false &&
                layoutContracts.BoxIds.Contains(name))
            {
                return true;
            }

            return SplitWhitespace(AttributeValue(element, "class"))
                .Any(className => layoutContracts.BoxClasses.Contains(className));
        }

        private static bool IsMeaningfulVisualBoxProperty(string property, string value)
        {
            if (IsBoxContractProperty(property, value) == false)
            {
                return false;
            }

            var normalizedProperty = (property ?? "").Trim().ToLowerInvariant();
            var normalizedValue = Regex.Replace((value ?? "").Trim().ToLowerInvariant(),
                @"\s+", " ");
            if ((normalizedProperty == "padding" ||
                 normalizedProperty.StartsWith("padding-", StringComparison.Ordinal) ||
                 normalizedProperty.EndsWith("-width", StringComparison.Ordinal)) &&
                IsZeroBoxValue(normalizedValue))
            {
                return false;
            }

            if (normalizedProperty == "background-image" &&
                (normalizedValue == "none" || normalizedValue == "initial"))
            {
                return false;
            }

            if (normalizedProperty == "background-color" &&
                (normalizedValue == "transparent" ||
                 Regex.IsMatch(normalizedValue,
                     @"^rgba\([^,]+,[^,]+,[^,]+,\s*0(?:\.0+)?\)$")))
            {
                return false;
            }

            if (normalizedProperty == "opacity" && normalizedValue == "1" ||
                normalizedProperty == "visibility" && normalizedValue == "visible" ||
                normalizedProperty == "scale" &&
                (normalizedValue == "1" || normalizedValue == "1 1") ||
                normalizedProperty == "rotate" &&
                (normalizedValue == "0" || normalizedValue == "0deg") ||
                normalizedProperty == "translate" && IsZeroBoxValue(normalizedValue))
            {
                return false;
            }

            return true;
        }

        private static bool IsZeroBoxValue(string value)
        {
            var parts = SplitWhitespace(value).ToList();
            return parts.Count > 0 && parts.All(part =>
                Regex.IsMatch(part, @"^[+-]?0(?:\.0+)?(?:px|%|em|rem)?$",
                    RegexOptions.IgnoreCase));
        }

        private static bool HasSymmetricCrossAxisMargins(
            IReadOnlyDictionary<string, string> style, bool horizontal)
        {
            var firstSide = horizontal ? "left" : "top";
            var secondSide = horizontal ? "right" : "bottom";
            if (style.ContainsKey("margin") &&
                (style.ContainsKey("margin-" + firstSide) ||
                 style.ContainsKey("margin-" + secondSide)))
            {
                return false;
            }

            return TryGetBoxSideValue(style, "margin", firstSide, out var first) &&
                   TryGetBoxSideValue(style, "margin", secondSide, out var second) &&
                   StyleValuesEqual(first, second);
        }

        private static bool TryGetBoxSideValue(
            IReadOnlyDictionary<string, string> style, string shorthandProperty,
            string side, out string value)
        {
            var sideProperty = shorthandProperty + "-" + side;
            if (style.TryGetValue(sideProperty, out value))
            {
                return true;
            }

            if (style.TryGetValue(shorthandProperty, out var shorthand) == false)
            {
                value = "0";
                return true;
            }

            var values = SplitWhitespace(shorthand).ToList();
            if (values.Count < 1 || values.Count > 4)
            {
                value = "";
                return false;
            }

            switch (side)
            {
                case "top":
                    value = values[0];
                    return true;
                case "right":
                    value = values.Count == 1 ? values[0] : values[1];
                    return true;
                case "bottom":
                    value = values.Count < 3 ? values[0] : values[2];
                    return true;
                case "left":
                    value = values.Count == 1
                        ? values[0]
                        : values.Count < 4 ? values[1] : values[3];
                    return true;
                default:
                    value = "";
                    return false;
            }
        }

        private static bool StyleValuesEqual(string left, string right)
        {
            return string.Equals(
                Regex.Replace((left ?? "").Trim(), @"\s+", " "),
                Regex.Replace((right ?? "").Trim(), @"\s+", " "),
                StringComparison.OrdinalIgnoreCase);
        }

        private static IReadOnlyCollection<string> GetElementClasses(XElement element)
        {
            var classNames = new HashSet<string>(
                SplitWhitespace(AttributeValue(element, "class")),
                StringComparer.Ordinal);
            foreach (var implicitClass in GetImplicitElementClasses(element))
            {
                classNames.Add(implicitClass);
            }

            return classNames;
        }

        private static IReadOnlyList<string> GetImplicitElementClasses(XElement element)
        {
            var namespaceName = element.Name.NamespaceName;
            if (string.IsNullOrWhiteSpace(namespaceName))
            {
                return Array.Empty<string>();
            }

            var fullTypeName = namespaceName + "." + element.Name.LocalName;
            if (implicitElementClassesByType.TryGetValue(fullTypeName, out var cached))
            {
                return cached;
            }

            var classes = new HashSet<string>(StringComparer.Ordinal);
            var elementType = ResolveVisualElementType(fullTypeName);
            for (var current = elementType;
                 current != null && typeof(VisualElement).IsAssignableFrom(current);
                 current = current.BaseType)
            {
                try
                {
                    var field = current.GetField("ussClassName",
                        BindingFlags.Public | BindingFlags.NonPublic |
                        BindingFlags.Static | BindingFlags.DeclaredOnly);
                    if (field != null && field.FieldType == typeof(string) &&
                        field.GetValue(null) is string className &&
                        string.IsNullOrWhiteSpace(className) == false)
                    {
                        classes.Add(className);
                    }
                }
                catch
                {
                    // A third-party VisualElement can expose an unsafe static accessor.
                    // Static auditing only consumes safe, readable class-name constants.
                }
            }

            var result = classes.OrderBy(value => value, StringComparer.Ordinal).ToList();
            implicitElementClassesByType[fullTypeName] = result;
            return result;
        }

        private static Type ResolveVisualElementType(XElement element)
        {
            var namespaceName = element?.Name.NamespaceName;
            return string.IsNullOrWhiteSpace(namespaceName)
                ? null
                : ResolveVisualElementType(namespaceName + "." + element.Name.LocalName);
        }

        private static Type ResolveVisualElementType(string fullTypeName)
        {
            var engineType = typeof(VisualElement).Assembly.GetType(fullTypeName, false);
            if (engineType != null)
            {
                return engineType;
            }

            foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    var candidate = assembly.GetType(fullTypeName, false);
                    if (candidate != null &&
                        typeof(VisualElement).IsAssignableFrom(candidate))
                    {
                        return candidate;
                    }
                }
                catch
                {
                    // Ignore assemblies that cannot serve reflected UI Toolkit types.
                }
            }

            return null;
        }

        private static void AuditRepeatedInlineLayoutVariants(string assetPath,
            XDocument document, UxmlLayoutContractIndex layoutContracts,
            MCPUxmlLayoutAuditReport report, bool includeSuppressed)
        {
            var candidates = new List<RepeatedInlineLayoutCandidate>();
            foreach (var element in document.Descendants())
            {
                var inlineLayout = ParseStyle(AttributeValue(element, "style"))
                    .Where(declaration => IsVariantLayoutProperty(declaration.Key))
                    .ToDictionary(declaration => declaration.Key,
                        declaration => declaration.Value,
                        StringComparer.OrdinalIgnoreCase);
                if (inlineLayout.Count == 0)
                {
                    continue;
                }

                foreach (var baseClass in SplitWhitespace(AttributeValue(element, "class"))
                             .Distinct(StringComparer.Ordinal))
                {
                    var relatedVariants = layoutContracts
                        .GetRelatedVariants(baseClass, inlineLayout.Keys)
                        .ToList();
                    if (relatedVariants.Count == 0)
                    {
                        continue;
                    }

                    var variantProperties = new HashSet<string>(
                        relatedVariants.SelectMany(layoutContracts.GetClassLayoutProperties),
                        StringComparer.OrdinalIgnoreCase);
                    var relevantDeclarations = inlineLayout
                        .Where(declaration => variantProperties.Contains(declaration.Key))
                        .OrderBy(declaration => declaration.Key,
                            StringComparer.OrdinalIgnoreCase)
                        .ToDictionary(declaration => declaration.Key,
                            declaration => declaration.Value,
                            StringComparer.OrdinalIgnoreCase);
                    if (relevantDeclarations.Count == 0)
                    {
                        continue;
                    }

                    candidates.Add(new RepeatedInlineLayoutCandidate
                    {
                        Element = element,
                        BaseClass = baseClass,
                        Declarations = relevantDeclarations,
                        Signature = BuildDeclarationSignature(relevantDeclarations),
                        RelatedVariantClasses = relatedVariants
                    });
                }
            }

            foreach (var group in candidates
                         .GroupBy(candidate => candidate.BaseClass + "\n" + candidate.Signature,
                             StringComparer.Ordinal)
                         .Where(group => group.Count() > 1))
            {
                var ordered = group.OrderBy(candidate => GetLineNumber(candidate.Element)).ToList();
                var first = ordered[0];
                var elementName = AttributeValue(first.Element, "name");
                var elementLabel = string.IsNullOrWhiteSpace(elementName)
                    ? $".{first.BaseClass}"
                    : $"#{elementName}";
                var relatedVariants = ordered
                    .SelectMany(candidate => candidate.RelatedVariantClasses)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(className => className, StringComparer.Ordinal)
                    .ToList();
                var suppressionReason = GetSuppressionReason(first.Element,
                    repeatedInlineSuppressionRegex);
                var issue = new MCPUxmlLayoutAuditIssue
                {
                    AssetPath = assetPath,
                    Line = GetLineNumber(first.Element),
                    Element = elementLabel,
                    ElementName = elementName,
                    Kind = "repeated-inline-layout-variant",
                    Axis = GetLayoutAxis(first.Declarations.Keys),
                    FixedProperties = first.Declarations.Keys
                        .OrderBy(property => property, StringComparer.OrdinalIgnoreCase)
                        .ToList(),
                    BaseClass = first.BaseClass,
                    AuthoredUsageCount = ordered.Count,
                    InlineDeclarations =
                        new Dictionary<string, string>(first.Declarations,
                            StringComparer.OrdinalIgnoreCase),
                    RelatedVariantClasses = relatedVariants,
                    UsageLocations = ordered.Select(candidate =>
                        new Dictionary<string, object>
                        {
                            { "path", assetPath },
                            { "line", GetLineNumber(candidate.Element) },
                            {
                                "element",
                                FormatElementLabel(candidate.Element, candidate.BaseClass)
                            }
                        }).ToList(),
                    Suppressed = string.IsNullOrWhiteSpace(suppressionReason) == false,
                    SuppressionReason = suppressionReason,
                    Message =
                        $"Inline layout {FormatDeclarations(first.Declarations)} is repeated on " +
                        $"{ordered.Count} authored elements using .{first.BaseClass}, while " +
                        $"{string.Join(", ", relatedVariants.Select(value => "." + value))} already " +
                        "expresses a shared authored variant for the same layout properties. " +
                        "Move the repeated declarations into a semantic shared class and apply that " +
                        "class to these elements."
                };
                report.Record(issue, includeSuppressed);
            }
        }

        private static string BuildDeclarationSignature(
            IReadOnlyDictionary<string, string> declarations)
        {
            return string.Join(";", declarations
                .OrderBy(declaration => declaration.Key, StringComparer.OrdinalIgnoreCase)
                .Select(declaration =>
                    declaration.Key.Trim().ToLowerInvariant() + ":" +
                    Regex.Replace(declaration.Value.Trim(), @"\s+", " ")
                        .ToLowerInvariant()));
        }

        private static string FormatDeclarations(
            IReadOnlyDictionary<string, string> declarations)
        {
            return "{" + string.Join("; ", declarations
                .OrderBy(declaration => declaration.Key, StringComparer.OrdinalIgnoreCase)
                .Select(declaration => declaration.Key + ": " + declaration.Value)) + "}";
        }

        private static string FormatElementLabel(XElement element, string fallbackClass)
        {
            var name = AttributeValue(element, "name");
            return string.IsNullOrWhiteSpace(name) ? "." + fallbackClass : "#" + name;
        }

        private static string GetLayoutAxis(IEnumerable<string> properties)
        {
            var propertyList = (properties ?? Enumerable.Empty<string>())
                .Select(property => (property ?? "").Trim().ToLowerInvariant())
                .ToList();
            var horizontal = propertyList.Any(property =>
                property == "left" ||
                property == "right" ||
                property.Contains("width") ||
                property.EndsWith("-left", StringComparison.Ordinal) ||
                property.EndsWith("-right", StringComparison.Ordinal) ||
                property == "column-gap");
            var vertical = propertyList.Any(property =>
                property == "top" ||
                property == "bottom" ||
                property.Contains("height") ||
                property.EndsWith("-top", StringComparison.Ordinal) ||
                property.EndsWith("-bottom", StringComparison.Ordinal) ||
                property == "row-gap");
            if (horizontal && vertical)
            {
                return "mixed";
            }

            if (horizontal)
            {
                return "horizontal";
            }

            return vertical ? "vertical" : "layout";
        }

        private static bool IsVariantLayoutProperty(string property)
        {
            return variantLayoutProperties.Contains((property ?? "").Trim());
        }

        private static bool IsBoxContractProperty(string property, string value)
        {
            property = (property ?? "").Trim().ToLowerInvariant();
            value = (value ?? "").Trim();
            if (property.StartsWith("background-", StringComparison.Ordinal) ||
                property.StartsWith("border-", StringComparison.Ordinal) ||
                property.StartsWith("-unity-background", StringComparison.Ordinal) ||
                property.StartsWith("padding-", StringComparison.Ordinal) ||
                property == "padding" ||
                property == "opacity" ||
                property == "visibility" ||
                property == "scale" ||
                property == "rotate" ||
                property == "translate" ||
                property == "transform-origin" ||
                property == "min-width" ||
                property == "max-width" ||
                property == "min-height" ||
                property == "max-height")
            {
                return true;
            }

            return property == "overflow" &&
                   string.Equals(value, "visible", StringComparison.OrdinalIgnoreCase) == false;
        }

        private static Dictionary<string, string> ParseStyle(string style)
        {
            var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (Match declaration in styleDeclarationRegex.Matches(style ?? ""))
            {
                properties[declaration.Groups["name"].Value.Trim()] =
                    declaration.Groups["value"].Value.Trim();
            }

            return properties;
        }

        private static string StyleValue(IReadOnlyDictionary<string, string> style, string property)
        {
            return style.TryGetValue(property, out var value)
                ? value.Trim().ToLowerInvariant()
                : "";
        }

        private static bool TryGetPixels(IReadOnlyDictionary<string, string> style, string property,
            out float value)
        {
            value = 0;
            if (style.TryGetValue(property, out var rawValue) == false)
            {
                return false;
            }

            var match = pixelValueRegex.Match(rawValue.Trim());
            return match.Success &&
                   float.TryParse(match.Groups["value"].Value, NumberStyles.Float,
                       CultureInfo.InvariantCulture, out value);
        }

        private static string FormatPixels(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture) + "px";
        }

        private static string GetSuppressionReason(XElement element)
        {
            return GetSuppressionReason(element, suppressionRegex);
        }

        private static string GetSuppressionReason(XElement element, Regex markerRegex)
        {
            var previous = element.NodesBeforeSelf().Reverse().FirstOrDefault(node =>
                !(node is XText) || string.IsNullOrWhiteSpace(((XText)node).Value) == false);
            var comment = previous as XComment;
            if (comment == null)
            {
                return "";
            }

            var match = markerRegex.Match(comment.Value);
            return match.Success ? match.Groups["reason"].Value.Trim() : "";
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

        private static List<string> NormalizeRequestedPaths(IEnumerable<string> requestedPaths,
            ICollection<string> errors)
        {
            var requested = (requestedPaths ?? Array.Empty<string>())
                .Where(path => string.IsNullOrWhiteSpace(path) == false)
                .Select(MCPUIToolkitAuditUtility.NormalizeAssetPath)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToList();
            foreach (var path in requested)
            {
                if (path.StartsWith("Assets/", StringComparison.Ordinal) == false ||
                    path.EndsWith(".uxml", StringComparison.OrdinalIgnoreCase) == false)
                {
                    errors.Add($"UXML layout audit path must be an Assets-relative .uxml path: {path}");
                }
                else if (File.Exists(MCPUIToolkitAuditUtility.ToFullPath(path)) == false)
                {
                    errors.Add($"UXML asset does not exist: {path}");
                }
            }

            return requested
                .Where(path => File.Exists(MCPUIToolkitAuditUtility.ToFullPath(path)))
                .ToList();
        }

        private static IEnumerable<string> SplitWhitespace(string value)
        {
            return (value ?? "").Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
        }

        private static void AddSelfTestCase(ICollection<Dictionary<string, object>> cases, string name,
            bool passed)
        {
            cases.Add(new Dictionary<string, object>
            {
                { "name", name },
                { "passed", passed }
            });
        }

        private sealed class RepeatedInlineLayoutCandidate
        {
            public XElement Element;
            public string BaseClass;
            public string Signature;
            public Dictionary<string, string> Declarations;
            public List<string> RelatedVariantClasses;
        }

        private sealed class UxmlSimpleSelector
        {
            public string Text;
            public string TypeName;
            public string Id;
            public int Specificity;
            public readonly List<string> ClassNames = new List<string>();

            public bool Matches(XElement element, IReadOnlyCollection<string> elementClasses)
            {
                if (string.IsNullOrWhiteSpace(TypeName) == false &&
                    string.Equals(TypeName, element.Name.LocalName,
                        StringComparison.Ordinal) == false)
                {
                    return false;
                }

                if (string.IsNullOrWhiteSpace(Id) == false &&
                    string.Equals(Id, AttributeValue(element, "name"),
                        StringComparison.Ordinal) == false)
                {
                    return false;
                }

                return ClassNames.All(elementClasses.Contains);
            }
        }

        private sealed class UxmlInlineStyleRule
        {
            public string SourcePath;
            public UxmlSimpleSelector Selector;
            public int SourceOrder;
            public readonly Dictionary<string, string> Declarations =
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        private sealed class UxmlStylesheetDeclaration
        {
            public string Value;
            public string Selector;
            public string SourcePath;
            public int Specificity;
            public int SourceOrder;
        }

        private sealed class UxmlInlineStyleContractIndex
        {
            private readonly List<UxmlInlineStyleRule> rules =
                new List<UxmlInlineStyleRule>();
            private int sourceOrder;

            public void AddRule(string sourcePath, UxmlSimpleSelector selector,
                IReadOnlyDictionary<string, string> declarations)
            {
                var rule = new UxmlInlineStyleRule
                {
                    SourcePath = sourcePath,
                    Selector = selector,
                    SourceOrder = sourceOrder++
                };
                foreach (var declaration in declarations)
                {
                    rule.Declarations[declaration.Key] = declaration.Value;
                }

                rules.Add(rule);
            }

            public Dictionary<string, UxmlStylesheetDeclaration> Resolve(XElement element)
            {
                var result = new Dictionary<string, UxmlStylesheetDeclaration>(
                    StringComparer.OrdinalIgnoreCase);
                var elementClasses = GetElementClasses(element);
                foreach (var rule in rules)
                {
                    if (rule.Selector.Matches(element, elementClasses) == false)
                    {
                        continue;
                    }

                    foreach (var declaration in rule.Declarations)
                    {
                        if (result.TryGetValue(declaration.Key, out var current) &&
                            (current.Specificity > rule.Selector.Specificity ||
                             current.Specificity == rule.Selector.Specificity &&
                             current.SourceOrder > rule.SourceOrder))
                        {
                            continue;
                        }

                        result[declaration.Key] = new UxmlStylesheetDeclaration
                        {
                            Value = declaration.Value,
                            Selector = rule.Selector.Text,
                            SourcePath = rule.SourcePath,
                            Specificity = rule.Selector.Specificity,
                            SourceOrder = rule.SourceOrder
                        };
                    }
                }

                return result;
            }
        }

        private sealed class UxmlElementNameReferenceIndex
        {
            public static readonly UxmlElementNameReferenceIndex Disabled =
                new UxmlElementNameReferenceIndex(false);

            private readonly HashSet<string> definitions =
                new HashSet<string>(StringComparer.Ordinal);
            private readonly HashSet<string> references =
                new HashSet<string>(StringComparer.Ordinal);

            public readonly bool Enabled;

            public int DefinitionCount => definitions.Count;

            public UxmlElementNameReferenceIndex(bool enabled)
            {
                Enabled = enabled;
            }

            public void AddDefinition(string name)
            {
                if (Enabled && string.IsNullOrWhiteSpace(name) == false)
                {
                    definitions.Add(name.Trim());
                }
            }

            public void AddReference(string name)
            {
                if (Enabled == false || string.IsNullOrWhiteSpace(name))
                {
                    return;
                }

                var normalized = name.Trim();
                if (definitions.Contains(normalized))
                {
                    references.Add(normalized);
                }
            }

            public bool IsReferenced(string name)
            {
                return Enabled &&
                       string.IsNullOrWhiteSpace(name) == false &&
                       references.Contains(name.Trim());
            }
        }

        private sealed class UxmlLayoutContractIndex
        {
            public readonly HashSet<string> BoxClasses =
                new HashSet<string>(StringComparer.Ordinal);
            public readonly HashSet<string> BoxIds =
                new HashSet<string>(StringComparer.Ordinal);

            private readonly Dictionary<string, HashSet<string>> classLayoutProperties =
                new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            private readonly Dictionary<string, HashSet<string>> authoredVariants =
                new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

            public void AddClassLayoutProperty(string className, string property)
            {
                if (!classLayoutProperties.TryGetValue(className, out var properties))
                {
                    properties = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                    classLayoutProperties[className] = properties;
                }

                properties.Add(property);
            }

            public void AddAuthoredVariant(string baseClass, string variantClass)
            {
                if (!authoredVariants.TryGetValue(baseClass, out var variants))
                {
                    variants = new HashSet<string>(StringComparer.Ordinal);
                    authoredVariants[baseClass] = variants;
                }

                variants.Add(variantClass);
            }

            public IEnumerable<string> GetRelatedVariants(string baseClass,
                IEnumerable<string> inlineProperties)
            {
                if (!authoredVariants.TryGetValue(baseClass, out var variants))
                {
                    return Enumerable.Empty<string>();
                }

                var propertySet = new HashSet<string>(
                    inlineProperties ?? Enumerable.Empty<string>(),
                    StringComparer.OrdinalIgnoreCase);
                return variants.Where(variant =>
                        classLayoutProperties.TryGetValue(variant, out var properties) &&
                        properties.Overlaps(propertySet))
                    .OrderBy(variant => variant, StringComparer.Ordinal);
            }

            public IEnumerable<string> GetClassLayoutProperties(string className)
            {
                return classLayoutProperties.TryGetValue(className, out var properties)
                    ? properties
                    : Enumerable.Empty<string>();
            }
        }
    }

}
#endif
