#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using static VMUnityAutomation.Editor.VmAutomationUxmlLayoutAuditor;
using static VMUnityAutomation.Editor.VmAutomationUxmlLayoutContractIndexer;
using static VMUnityAutomation.Editor.VmAutomationUxmlLayoutModels;

namespace VMUnityAutomation.Editor
{
    internal static class VmAutomationUxmlLayoutAuditSelfTests
    {
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
            "height: 36px; flex-direction: row; justify-content: center;\"><ui:Label text=\"Control\"/></ui:Button>");
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

        var localizedButtonStyleIndex = new UxmlInlineStyleContractIndex();
        IndexInlineStyleSheetText("Assets/Buttons.uss",
            ".localized-action { width: 344px; padding-left: 50px; " +
            "padding-right: 50px; }", localizedButtonStyleIndex);
        var stylesheetFixedLocalizedButton = AuditFixture(
            "<ui:Button class=\"localized-action\"><Bindings>" +
            "<UnityEngine.Localization.LocalizedString property=\"text\" " +
            "table=\"GeneralUI\" entry=\"Refresh\"/></Bindings></ui:Button>",
            inlineStyleContracts: localizedButtonStyleIndex);
        AddSelfTestCase(cases,
            "USS fixed width localized button warns",
            stylesheetFixedLocalizedButton.WarningCount == 1 &&
            stylesheetFixedLocalizedButton.Issues.Single().Kind ==
            "fixed-localized-button-width" &&
            stylesheetFixedLocalizedButton.Issues.Single().Size == 344);

        var labelHeightStyleIndex = new UxmlInlineStyleContractIndex();
        IndexInlineStyleSheetText("Assets/Labels.uss",
            ".content-hint { height: 38px; }", labelHeightStyleIndex);
        var stylesheetFixedContentLabel = AuditFixture(
            "<ui:Label class=\"content-hint\"><Bindings>" +
            "<UnityEngine.Localization.LocalizedString property=\"text\" " +
            "table=\"GeneralUI\" entry=\"ForgeSelectionHint\"/>" +
            "</Bindings></ui:Label>",
            inlineStyleContracts: labelHeightStyleIndex);
        AddSelfTestCase(cases, "USS fixed content Label height warns",
            stylesheetFixedContentLabel.WarningCount == 1 &&
            stylesheetFixedContentLabel.Issues.Single().Kind ==
            "fixed-content-label-height" &&
            stylesheetFixedContentLabel.Issues.Single().Size == 38);

        const string inertStretch =
            "<ui:VisualElement style=\"align-items: center;\">" +
            "<ui:Label name=\"Title\" text=\"Title\" style=\"align-self: stretch; margin-left: 18px; " +
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
            "<ui:Label name=\"Title\" text=\"Title\" style=\"flex-grow: 1; margin-bottom: 3px; " +
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
        AddSelfTestCase(cases, "fixed main-size content Label owns one height warning",
            fixedHeightGrow.WarningCount == 1 &&
            fixedHeightGrow.Issues.Single().Kind ==
            "fixed-content-label-height");

        const string horizontalGrow =
            "<ui:VisualElement style=\"flex-direction: row; justify-content: center;\">" +
            "<ui:Label name=\"Title\" text=\"Title\" style=\"flex-grow: 2; " +
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
            fixedVerticalScrollWrapper.WarningCount == 2 &&
            fixedVerticalScrollWrapper.Issues.Single(issue => issue.Kind ==
                "fixed-scroll-cross-axis-content-size").Kind ==
            "fixed-scroll-cross-axis-content-size" &&
            fixedVerticalScrollWrapper.Issues.Single(issue => issue.Kind ==
                "fixed-scroll-cross-axis-content-size").Axis == "horizontal" &&
            Math.Abs(fixedVerticalScrollWrapper.Issues.Single(issue => issue.Kind ==
                "fixed-scroll-cross-axis-content-size").ParentSize - 312f) <=
            CENTER_EPSILON &&
            Math.Abs(fixedVerticalScrollWrapper.Issues.Single(issue => issue.Kind ==
                "fixed-scroll-cross-axis-content-size").Size - 330f) <=
            CENTER_EPSILON);

        var equalVerticalScrollWrapper = AuditFixture(
            fixedVerticalScrollCrossAxisSize.Replace("width: 330px", "width: 312px"));
        AddSelfTestCase(cases, "vertical scroll content wrapper repeated width warns",
            equalVerticalScrollWrapper.WarningCount == 2 &&
            equalVerticalScrollWrapper.Issues.Count(issue => issue.Kind ==
                "fixed-scroll-cross-axis-content-size") == 1);

        var stretchedVerticalScrollWrapper = AuditFixture(
            fixedVerticalScrollCrossAxisSize.Replace("width: 330px; ", ""));
        AddSelfTestCase(cases, "cross-axis stretch scroll wrapper passes",
            stretchedVerticalScrollWrapper.WarningCount == 0);

        var narrowerVerticalScrollWrapper = AuditFixture(
            fixedVerticalScrollCrossAxisSize.Replace("width: 330px", "width: 300px"));
        AddSelfTestCase(cases,
            "narrower scroll wrapper still exposes fixed ScrollView width",
            narrowerVerticalScrollWrapper.WarningCount == 1 &&
            narrowerVerticalScrollWrapper.Issues.Single().Kind ==
            "fixed-scroll-view-cross-axis-size");

        var visualVerticalScrollWrapper = AuditFixture(
            fixedVerticalScrollCrossAxisSize.Replace(
                "width: 330px; align-items: center;",
                "width: 330px; align-items: center; background-color: white;"));
        AddSelfTestCase(cases,
            "visual content ownership does not hide fixed ScrollView width",
            visualVerticalScrollWrapper.WarningCount == 1 &&
            visualVerticalScrollWrapper.Issues.Single().Kind ==
            "fixed-scroll-view-cross-axis-size");

        var clippingVerticalScrollWrapper = AuditFixture(
            fixedVerticalScrollCrossAxisSize.Replace(
                "width: 330px; align-items: center;",
                "width: 330px; align-items: center; overflow: hidden;"));
        AddSelfTestCase(cases,
            "content clipping does not hide fixed ScrollView width",
            clippingVerticalScrollWrapper.WarningCount == 1 &&
            clippingVerticalScrollWrapper.Issues.Single().Kind ==
            "fixed-scroll-view-cross-axis-size");

        var interactiveVerticalScrollWrapper = AuditFixture(
            fixedVerticalScrollCrossAxisSize.Replace(
                "name=\"StagePartyEntries\"",
                "name=\"StagePartyEntries\" focusable=\"true\""));
        AddSelfTestCase(cases,
            "interactive content does not hide fixed ScrollView width",
            interactiveVerticalScrollWrapper.WarningCount == 1 &&
            interactiveVerticalScrollWrapper.Issues.Single().Kind ==
            "fixed-scroll-view-cross-axis-size");

        const string fixedHorizontalScrollCrossAxisSize =
            "<ui:ScrollView mode=\"Horizontal\" style=\"width: 300px; height: 96px;\">" +
            "<ui:VisualElement style=\"height: 102px; flex-direction: row;\">" +
            "<ui:VisualElement/><ui:VisualElement/>" +
            "</ui:VisualElement></ui:ScrollView>";
        var fixedHorizontalScrollWrapper = AuditFixture(fixedHorizontalScrollCrossAxisSize);
        AddSelfTestCase(cases, "horizontal scroll content wrapper fixed height warns",
            fixedHorizontalScrollWrapper.WarningCount == 2 &&
            fixedHorizontalScrollWrapper.Issues.Single(issue => issue.Kind ==
                "fixed-scroll-cross-axis-content-size").Axis == "vertical");

        const string fixedHorizontalScrollViewCrossAxisSize =
            "<ui:ScrollView name=\"Offers\" mode=\"Horizontal\" " +
            "style=\"width: 300px; height: 96px;\">" +
            "<ui:VisualElement style=\"height: 72px;\"/>" +
            "</ui:ScrollView>";
        var fixedHorizontalScrollView = AuditFixture(
            fixedHorizontalScrollViewCrossAxisSize);
        AddSelfTestCase(cases,
            "horizontal ScrollView fixed content-sized height warns",
            fixedHorizontalScrollView.WarningCount == 1 &&
            fixedHorizontalScrollView.Issues.Single().Kind ==
            "fixed-scroll-view-cross-axis-size" &&
            fixedHorizontalScrollView.Issues.Single().Axis == "vertical" &&
            Math.Abs(fixedHorizontalScrollView.Issues.Single().Size - 96f) <=
            CENTER_EPSILON);

        const string fixedVerticalScrollViewCrossAxisSize =
            "<ui:ScrollView name=\"Entries\" mode=\"Vertical\" " +
            "style=\"width: 312px; height: 240px;\">" +
            "<ui:VisualElement style=\"width: 280px;\"/>" +
            "</ui:ScrollView>";
        var fixedVerticalScrollView = AuditFixture(
            fixedVerticalScrollViewCrossAxisSize);
        AddSelfTestCase(cases,
            "vertical ScrollView fixed content-sized width warns",
            fixedVerticalScrollView.WarningCount == 1 &&
            fixedVerticalScrollView.Issues.Single().Kind ==
            "fixed-scroll-view-cross-axis-size" &&
            fixedVerticalScrollView.Issues.Single().Axis == "horizontal" &&
            Math.Abs(fixedVerticalScrollView.Issues.Single().Size - 312f) <=
            CENTER_EPSILON);

        var horizontalScrollAxisBound = AuditFixture(
            fixedHorizontalScrollViewCrossAxisSize.Replace(" height: 96px;", ""));
        AddSelfTestCase(cases,
            "horizontal ScrollView fixed scrolling width passes",
            horizontalScrollAxisBound.WarningCount == 0);

        var verticalScrollAxisBound = AuditFixture(
            fixedVerticalScrollViewCrossAxisSize.Replace("width: 312px; ", ""));
        AddSelfTestCase(cases,
            "vertical ScrollView fixed scrolling height passes",
            verticalScrollAxisBound.WarningCount == 0);

        var unknownHorizontalContentHeight = AuditFixture(
            fixedHorizontalScrollViewCrossAxisSize.Replace(
                " style=\"height: 72px;\"", ""));
        AddSelfTestCase(cases,
            "fixed ScrollView cross axis without provable content size passes",
            unknownHorizontalContentHeight.WarningCount == 0);

        var suppressedHorizontalScrollView = AuditFixture(
            $"<!-- {FIXED_SCROLL_CROSS_AXIS_SIZE_SUPPRESSION_MARKER} " +
            "measured viewport intentionally clips card artwork -->" +
            fixedHorizontalScrollViewCrossAxisSize,
            includeSuppressed: true);
        AddSelfTestCase(cases,
            "reasoned fixed ScrollView cross-axis suppression is retained",
            suppressedHorizontalScrollView.WarningCount == 0 &&
            suppressedHorizontalScrollView.SuppressedCount == 1 &&
            suppressedHorizontalScrollView.Issues.Single().Suppressed);

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
            suppressedScrollWrapper.WarningCount == 1 &&
            suppressedScrollWrapper.SuppressedCount == 1 &&
            suppressedScrollWrapper.Issues.Single(issue => issue.Kind ==
                "fixed-scroll-cross-axis-content-size").Suppressed &&
            suppressedScrollWrapper.Issues.Single(issue => issue.Kind ==
                "fixed-scroll-view-cross-axis-size").Suppressed == false);

        var unconsumedNameIndex = new UxmlElementNameReferenceIndex(true);
        const string unconsumedName =
            "<ui:VisualElement name=\"LayoutOnlyName\"><ui:Label text=\"Child\"/></ui:VisualElement>";
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
            "<ui:Label text=\"Body\"/></ui:VisualElement></ui:VisualElement>";
        var fixedPartition = AuditFixture(fixedFlexPartition);
        var fixedPartitionIssues = fixedPartition.Issues
            .Where(issue => issue.Kind == "fixed-flex-partition")
            .ToArray();
        AddSelfTestCase(cases, "fully fixed flex partition warns",
            fixedPartitionIssues.Length == 1 &&
            fixedPartitionIssues[0].Axis == "vertical" &&
            fixedPartitionIssues[0].FixedProperties
                .SequenceEqual(new[] { "height", "flex-shrink" }));

        var flexibleRemainder = AuditFixture(
            fixedFlexPartition.Replace(
                "height: 426px; flex-shrink: 0;",
                "flex-grow: 1;"));
        AddSelfTestCase(cases, "fixed header with flexible remainder passes",
            flexibleRemainder.WarningCount == 0);

        var incompletePartition = AuditFixture(
            fixedFlexPartition.Replace("height: 426px;", "height: 420px;"));
        AddSelfTestCase(cases, "fixed children that do not partition parent avoid partition warning",
            incompletePartition.Issues.All(issue => issue.Kind != "fixed-flex-partition"));

        var suppressedFixedPartition = AuditFixture(
            $"<!-- {FIXED_FLEX_PARTITION_SUPPRESSION_MARKER} " +
            "external native surface requires exact child extents -->" +
            fixedFlexPartition, includeSuppressed: true);
        var suppressedFixedPartitionIssues = suppressedFixedPartition.Issues
            .Where(issue => issue.Kind == "fixed-flex-partition")
            .ToArray();
        AddSelfTestCase(cases, "reasoned fixed-partition suppression is retained",
            suppressedFixedPartition.SuppressedCount == 1 &&
            suppressedFixedPartitionIssues.Length == 1 &&
            suppressedFixedPartitionIssues[0].Suppressed);

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

        var boundFallback = AuditFixture(
            "<ui:Label text=\"Fallback\"><ui:Bindings>" +
            "<ui:DataBinding property=\"text\"/></ui:Bindings></ui:Label>");
        AddSelfTestCase(cases, "bound literal fallback is an error",
            boundFallback.ErrorCount == 1 &&
            boundFallback.Issues.Single(issue => issue.IsError).Kind ==
            "bound-property-literal-fallback");

        var emptyLabel = AuditFixture("<ui:Label/>");
        AddSelfTestCase(cases, "empty label without text binding is an error",
            emptyLabel.ErrorCount == 1 &&
            emptyLabel.Issues.Single(issue => issue.IsError).Kind ==
            "empty-label-without-text-binding");

        var localizedLabel = AuditFixture(
            "<ui:Label><ui:Bindings>" +
            "<ui:DataBinding property=\"text\"/></ui:Bindings></ui:Label>");
        AddSelfTestCase(cases, "empty label with text binding passes policy",
            localizedLabel.ErrorCount == 0);

        var runtimeLabel = AuditFixture(
            $"<!-- {RUNTIME_TEXT_SUPPRESSION_MARKER} " +
            "InventoryPricePresenter writes the computed price. -->" +
            "<ui:Label name=\"Price\"/>");
        var runtimeTextIssues = runtimeLabel.Issues
            .Where(issue => issue.Kind == "empty-label-without-text-binding")
            .ToArray();
        AddSelfTestCase(cases, "runtime-text owner cannot suppress a blank preview",
            runtimeLabel.ErrorCount == 1 &&
            runtimeLabel.SuppressedCount == 0 &&
            runtimeTextIssues.Length == 1 &&
            runtimeTextIssues[0].Suppressed == false &&
            runtimeTextIssues[0].SuppressionReason.Contains("InventoryPricePresenter"));

        var runtimePreviewLabel = AuditFixture(
            $"<!-- {RUNTIME_TEXT_SUPPRESSION_MARKER} " +
            "InventoryPricePresenter replaces the design-time price. -->" +
            "<ui:Label name=\"Price\" text=\"99\"/>");
        AddSelfTestCase(cases, "runtime text with an authored preview passes policy",
            runtimePreviewLabel.ErrorCount == 0);

        var placeholderLiteral = AuditFixture(
            "<ui:Label text=\"Placeholder\"/>");
        AddSelfTestCase(cases, "placeholder literal is an error",
            placeholderLiteral.ErrorCount == 1 &&
            placeholderLiteral.Issues.Single(issue => issue.IsError).Kind ==
            "placeholder-literal");

        var productLiteral = AuditFixture(
            "<ui:Label text=\"Start Battle\"/>");
        AddSelfTestCase(cases, "non-placeholder product literal passes policy",
            productLiteral.ErrorCount == 0);

        var inlineLabelBackground = AuditFixture(
            "<ui:Label name=\"Price\" text=\"99\" " +
            "style=\"background-image: url(&quot;Gold.png&quot;);\"/>");
        AddSelfTestCase(cases, "inline Label background image is an error",
            inlineLabelBackground.ErrorCount == 1 &&
            inlineLabelBackground.Issues.Single(issue => issue.IsError).Kind ==
            "text-element-background-image");

        var labelBackgroundStyleIndex = new UxmlInlineStyleContractIndex();
        IndexInlineStyleSheetText("Assets/Price.uss",
            ".price-with-icon { background-image: url(\"Gold.png\"); }",
            labelBackgroundStyleIndex);
        var styledLabelBackground = AuditFixture(
            "<ui:Label name=\"Price\" class=\"price-with-icon\" text=\"99\"/>",
            inlineStyleContracts: labelBackgroundStyleIndex);
        AddSelfTestCase(cases, "USS Label background image is an error",
            styledLabelBackground.ErrorCount == 1 &&
            styledLabelBackground.Issues.Single(issue => issue.IsError).Kind ==
            "text-element-background-image");

        var separateIconElement = AuditFixture(
            "<ui:VisualElement style=\"flex-direction: row;\">" +
            "<ui:VisualElement style=\"background-image: url(&quot;Gold.png&quot;);\"/>" +
            "<ui:Label text=\"99\"/>" +
            "</ui:VisualElement>");
        AddSelfTestCase(cases, "dedicated icon VisualElement beside Label passes policy",
            separateIconElement.ErrorCount == 0);

        var missingBuilderPreview = AuditFixture(
            "<!-- ui-builder-preview: runtime-replaced required-images=1 by panel binding -->" +
            "<ui:VisualElement name=\"Icon\"/>");
        AddSelfTestCase(cases, "empty required UI Builder image preview is an error",
            missingBuilderPreview.Issues.Any(issue =>
                issue.Kind == "missing-ui-builder-preview-image" && issue.IsError));

        var authoredBuilderPreview = AuditFixture(
            "<!-- ui-builder-preview: runtime-replaced required-images=1 by panel binding -->" +
            "<ui:VisualElement name=\"Icon\"><ui:VisualElement " +
            "class=\"ui-builder-preview-content\" " +
            "style=\"background-image: url(&quot;Creature.png&quot;);\"/>" +
            "</ui:VisualElement>");
        AddSelfTestCase(cases, "authored UI Builder image preview passes",
            authoredBuilderPreview.Issues.All(issue =>
                issue.Kind != "missing-ui-builder-preview-image"));

        var incompleteBuilderPreview = AuditFixture(
            "<!-- ui-builder-preview: runtime-replaced required-images=2 by slot binding -->" +
            "<ui:VisualElement><ui:VisualElement class=\"ui-builder-preview-content\" " +
            "style=\"background-image: url(&quot;Weapon.png&quot;);\"/>" +
            "</ui:VisualElement>");
        AddSelfTestCase(cases, "required preview image count catches a missing sample",
            incompleteBuilderPreview.Issues.Any(issue =>
                issue.Kind == "missing-ui-builder-preview-image" && issue.IsError));

        var unmarkedDecoration = AuditFixture(
            "<!-- ui-builder-preview: runtime-replaced required-images=1 by panel binding -->" +
            "<ui:VisualElement style=\"background-image: url(&quot;Frame.png&quot;);\"/>");
        AddSelfTestCase(cases, "ordinary decoration cannot satisfy the preview contract",
            unmarkedDecoration.Issues.Any(issue =>
                issue.Kind == "missing-ui-builder-preview-image" && issue.IsError));

        var configuredPreview = new VmAutomationBuilderPreviewRequirement
        {
            Path = "Assets/__UxmlLayoutAuditSelfTest.uxml",
            ElementName = "Icon",
            MinImages = 1
        };
        var missingUnmarkedPreview = AuditFixture(
            "<ui:VisualElement name=\"Icon\"/>",
            requiredBuilderPreviews: new[] { configuredPreview });
        AddSelfTestCase(cases, "configured preview catches removal of marker and image",
            missingUnmarkedPreview.Issues.Any(issue =>
                issue.Kind == "missing-ui-builder-preview-image" && issue.IsError));

        var missingConfiguredTarget = AuditFixture(
            "<ui:VisualElement name=\"Other\"/>",
            requiredBuilderPreviews: new[] { configuredPreview });
        AddSelfTestCase(cases, "configured preview catches removal of entire target",
            missingConfiguredTarget.Issues.Any(issue =>
                issue.Kind == "missing-ui-builder-preview-target" && issue.IsError));

        var completeConfiguredPreview = AuditFixture(
            "<ui:VisualElement name=\"Icon\"><ui:VisualElement " +
            "class=\"ui-builder-preview-content\" " +
            "style=\"background-image: url(&quot;Creature.png&quot;);\"/>" +
            "</ui:VisualElement>",
            requiredBuilderPreviews: new[] { configuredPreview });
        AddSelfTestCase(cases, "configured preview accepts authored image",
            completeConfiguredPreview.Issues.All(issue =>
                issue.Kind != "missing-ui-builder-preview-image" &&
                issue.Kind != "missing-ui-builder-preview-target"));

        var configuredTextPreview = new VmAutomationBuilderPreviewRequirement
        {
            Path = "Assets/__UxmlLayoutAuditSelfTest.uxml",
            ElementName = "Properties",
            MinTextEntries = 2
        };
        var missingTextPreview = AuditFixture(
            "<ui:VisualElement name=\"Properties\"/>",
            requiredBuilderPreviews: new[] { configuredTextPreview });
        AddSelfTestCase(cases, "configured text preview catches an empty generated container",
            missingTextPreview.Issues.Any(issue =>
                issue.Kind == "missing-ui-builder-preview-text-entry" && issue.IsError));

        var blankTextPreview = AuditFixture(
            "<ui:VisualElement name=\"Properties\"><ui:Instance " +
            "class=\"ui-builder-preview-content\" template=\"PropertyRow\"/>" +
            "</ui:VisualElement>",
            requiredBuilderPreviews: new[] { configuredTextPreview });
        AddSelfTestCase(cases, "blank preview instance cannot satisfy text entries",
            blankTextPreview.Issues.Any(issue =>
                issue.Kind == "missing-ui-builder-preview-text-entry" && issue.IsError));

        var completeTextPreview = AuditFixture(
            "<ui:VisualElement name=\"Properties\">" +
            "<ui:Instance class=\"ui-builder-preview-content\" template=\"PropertyRow\">" +
            "<AttributeOverrides element-name=\"Title\" text=\"Health\"/>" +
            "</ui:Instance>" +
            "<ui:Instance class=\"ui-builder-preview-content\" template=\"PropertyRow\">" +
            "<AttributeOverrides element-name=\"Title\" text=\"Shield\"/>" +
            "</ui:Instance></ui:VisualElement>",
            requiredBuilderPreviews: new[] { configuredTextPreview });
        AddSelfTestCase(cases, "configured text preview accepts populated template instances",
            completeTextPreview.Issues.All(issue =>
                issue.Kind != "missing-ui-builder-preview-text-entry"));

        var markedTextPreview = AuditFixture(
            "<!-- ui-builder-preview: runtime-replaced required-text-entries=1 by panel binding -->" +
            "<ui:VisualElement name=\"Properties\"/>");
        AddSelfTestCase(cases, "text preview marker catches a missing sample",
            markedTextPreview.Issues.Any(issue =>
                issue.Kind == "missing-ui-builder-preview-text-entry" && issue.IsError));

        foreach (var testCase in VmAutomationUxmlNaturalFlowLayoutAuditor.RunSelfTests())
        {
            cases.Add(testCase);
        }

        foreach (var testCase in VmAutomationUxmlInlineFlexShrinkAuditor.RunSelfTests())
        {
            cases.Add(testCase);
        }

        foreach (var testCase in VmAutomationUxmlComponentInitialStyleAuditor.RunSelfTests())
        {
            cases.Add(testCase);
        }

        foreach (var testCase in VmAutomationUxmlLayoutPlaceholderAuditor.RunSelfTests())
        {
            cases.Add(testCase);
        }

        foreach (var testCase in VmAutomationUxmlPanelNestingAuditor.RunSelfTests())
        {
            cases.Add(testCase);
        }

        foreach (var testCase in VmAutomationUxmlThemeStyleAuditor.RunSelfTests())
        {
            cases.Add(testCase);
        }

        return new Dictionary<string, object>
        {
            { "passed", cases.All(testCase => (bool)testCase["passed"]) },
            { "cases", cases }
        };
    }

    private static VmAutomationUxmlLayoutAuditReport AuditFixture(string element,
        string parentStyle = "width: 807px; height: 492px;", bool includeSuppressed = false,
        UxmlLayoutContractIndex layoutContracts = null,
        UxmlInlineStyleContractIndex inlineStyleContracts = null,
        UxmlElementNameReferenceIndex elementNameReferences = null,
        bool pixelGridEnabled = false, int pixelGridStep = 3,
        bool uxmlTooltipAttributes = true,
        IEnumerable<VmAutomationBuilderPreviewRequirement> requiredBuilderPreviews = null)
    {
        var text =
            "<ui:UXML xmlns:ui=\"UnityEngine.UIElements\">" +
            $"<ui:VisualElement style=\"{parentStyle}\">{element}</ui:VisualElement>" +
            "</ui:UXML>";
        var report = new VmAutomationUxmlLayoutAuditReport(100)
        {
            ScannedUxmlCount = 1,
            IndexedUxmlCount = 1
        };
        var settings = new VmAutomationUIToolkitAuditProjectSettings
        {
            PixelGridEnabled = pixelGridEnabled,
            PixelGridStep = pixelGridStep,
            UxmlTooltipAttributes = uxmlTooltipAttributes
        };
        if (requiredBuilderPreviews != null)
            settings.RequiredBuilderPreviews.AddRange(requiredBuilderPreviews);
        var options = VmAutomationUIToolkitAuditOptions.FromProjectSettings(settings);
        AuditText("Assets/__UxmlLayoutAuditSelfTest.uxml", text,
            layoutContracts ?? new UxmlLayoutContractIndex(),
            elementNameReferences ?? UxmlElementNameReferenceIndex.Disabled,
            report, includeSuppressed,
            inlineStyleContracts, options);
        report.SortIssues();
        return report;
    }


    }
}
#endif
