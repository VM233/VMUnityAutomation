#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using UssRule = VMUnityAutomation.Editor.VmAutomationUssAuditContext.UssRule;
using UssAuthoredElement = VMUnityAutomation.Editor.VmAutomationUssAuditContext.UssAuthoredElement;
using UssAuthoredDocument = VMUnityAutomation.Editor.VmAutomationUssAuditContext.UssAuthoredDocument;
using UssCascadeRule = VMUnityAutomation.Editor.VmAutomationUssAuditContext.UssCascadeRule;
using UssCascadeDocument = VMUnityAutomation.Editor.VmAutomationUssAuditContext.UssCascadeDocument;
using UssCascadeIndex = VMUnityAutomation.Editor.VmAutomationUssAuditContext.UssCascadeIndex;
using static VMUnityAutomation.Editor.VmAutomationUssCascadeAuditor;
using static VMUnityAutomation.Editor.VmAutomationUssStyleSheetParser;

namespace VMUnityAutomation.Editor
{
    internal static class VmAutomationUssButtonFeedbackAuditor
    {
        internal static void Audit(UssCascadeIndex cascadeIndex,
            IReadOnlyCollection<string> targetPaths, VmAutomationUssStyleAuditReport report)
        {
            var targets = new HashSet<string>(targetPaths,
                StringComparer.OrdinalIgnoreCase);
            foreach (var document in cascadeIndex.Documents.Where(document =>
                         document.LoadedAssetPaths.Overlaps(targets)))
            {
                foreach (var button in document.AuthoredDocument.Elements.Where(element =>
                             string.Equals(element.TypeName, "Button",
                                 StringComparison.Ordinal)))
                {
                    var background = button.InlineDeclarations.TryGetValue(
                        "background-color", out var inlineBackground)
                        ? inlineBackground
                        : document.Resolve(button, "background-color", null)?.Value;
                    var compositeIcon = string.IsNullOrWhiteSpace(button.Text) &&
                        button.Children.Any(child =>
                            string.Equals(child.TypeName, "Label",
                                StringComparison.Ordinal) == false) &&
                        IsTransparentButtonBackground(background);
                    var hover = GetVisibleButtonFeedback(document, button, "hover");
                    var active = GetVisibleButtonFeedback(document, button, "active");
                    if (compositeIcon == false && hover.Count == 0)
                        continue;
                    foreach (var state in new[] { "hover", "active" })
                    {
                        var hasFeedback = state == "hover"
                            ? hover.Count > 0
                            : active.Any(effect =>
                                hover.TryGetValue(effect.Key, out var hoverValue) == false ||
                                StyleValuesEqual(effect.Value, hoverValue) == false);
                        if (hasFeedback)
                            continue;

                        var buttonName = string.IsNullOrWhiteSpace(button.Name)
                            ? "unnamed Button"
                            : "#" + button.Name;
                        report.Record(new VmAutomationUssStyleAuditIssue
                        {
                            AssetPath = document.AuthoredDocument.AssetPath,
                            Line = button.Line,
                            Selector = buttonName,
                            Token = button.Name ?? "",
                            Kind = compositeIcon
                                ? "missing-composite-button-feedback"
                                : "missing-button-press-feedback",
                            Message = $"Button '{buttonName}' has no visible " +
                                      $":{state} feedback. Author a distinct visual state " +
                                      "in a stylesheet loaded by this UXML; declarations " +
                                      "overridden by inline styles do not count."
                        }, false);
                    }
                }
            }
        }

        private static bool IsTransparentButtonBackground(string value)
        {
            if (string.Equals((value ?? "").Trim(), "transparent",
                    StringComparison.OrdinalIgnoreCase))
                return true;
            return Regex.IsMatch(value ?? "",
                @"^rgba\s*\([^)]*,\s*0(?:\.0+)?\s*\)$",
                RegexOptions.IgnoreCase);
        }

        private static Dictionary<string, string> GetVisibleButtonFeedback(
            UssCascadeDocument document, UssAuthoredElement button, string state)
        {
            var effects = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var rule in document.Rules)
            {
                if (TryGetButtonFeedbackTarget(rule.SelectorText, state, button,
                        out var visualTarget) == false)
                    continue;
                foreach (var declaration in rule.Rule.Declarations)
                {
                    if (IsVisualFeedbackProperty(declaration.Key) == false ||
                        visualTarget.InlineDeclarations.ContainsKey(declaration.Key))
                        continue;
                    var normal = document.Resolve(visualTarget, declaration.Key, null);
                    if (normal != null &&
                        StyleValuesEqual(normal.Value, declaration.Value))
                        continue;
                    if (normal == null &&
                        VmAutomationUIToolkitInitialStyleComparer.IsInitialValue(
                            declaration.Key, declaration.Value))
                        continue;
                    effects[visualTarget.Line + ":" + declaration.Key] = declaration.Value;
                }
            }
            return effects;
        }

        private static bool TryGetButtonFeedbackTarget(string selectorText, string state,
            UssAuthoredElement button, out UssAuthoredElement visualTarget)
        {
            visualTarget = null;
            var match = Regex.Match(selectorText ?? "",
                @":" + state + @"(?![A-Za-z0-9_-])",
                RegexOptions.IgnoreCase);
            if (match.Success == false)
                return false;
            var anchorText = selectorText.Substring(0, match.Index).TrimEnd();
            if (TryParseStaticSelector(anchorText, out var anchor) == false ||
                anchor.Matches(button) == false)
                return false;
            var suffix = selectorText.Substring(match.Index + match.Length);
            if (string.IsNullOrWhiteSpace(suffix))
            {
                visualTarget = button;
                return true;
            }
            if (char.IsWhiteSpace(suffix[0]) == false && suffix[0] != '>' ||
                TryParseStaticSelector(anchorText + suffix, out var full) == false)
                return false;
            visualTarget = Descendants(button).FirstOrDefault(full.Matches);
            return visualTarget != null;
        }

        private static IEnumerable<UssAuthoredElement> Descendants(
            UssAuthoredElement parent)
        {
            foreach (var child in parent.Children)
            {
                yield return child;
                foreach (var descendant in Descendants(child))
                    yield return descendant;
            }
        }

        private static bool IsVisualFeedbackProperty(string property)
        {
            return property == "opacity" || property == "scale" ||
                   property == "translate" || property == "rotate" ||
                   property == "color" || property == "background-color" ||
                   property == "background-image" || property == "background-size" ||
                   property == "-unity-background-image-tint-color" ||
                   property.StartsWith("border-", StringComparison.Ordinal) &&
                   (property.EndsWith("-color", StringComparison.Ordinal) ||
                    property.EndsWith("-width", StringComparison.Ordinal));
        }

        internal static IEnumerable<Dictionary<string, object>>
            RunSelfTests()
        {
            const string stylePath = "Assets/__ButtonFeedbackSelfTest.uss";
            const string uxmlPath = "Assets/__ButtonFeedbackSelfTest.uxml";
            var authored = new UssAuthoredDocument(uxmlPath, XDocument.Parse(
                "<ui:UXML xmlns:ui=\"UnityEngine.UIElements\">" +
                "<ui:Button name=\"Missing\"><ui:VisualElement/></ui:Button>" +
                "<ui:Button name=\"HoverOnly\"><ui:VisualElement/></ui:Button>" +
                "<ui:Button name=\"SameStates\"><ui:VisualElement/></ui:Button>" +
                "<ui:Button name=\"InlineBlocked\" style=\"opacity: 1;\">" +
                "<ui:VisualElement/></ui:Button>" +
                "<ui:Button name=\"Complete\"><ui:VisualElement/></ui:Button>" +
                "<ui:Button name=\"TextOnly\" text=\"Continue\"/>" +
                "<ui:Button name=\"TextHoverOnly\" text=\"Previous\"/>" +
                "<ui:Button name=\"TextSameStates\" text=\"Next\"/>" +
                "<ui:Button name=\"TextComplete\" text=\"Next\"/>" +
                "<ui:Button name=\"OpaqueNoHover\" text=\"Cancel\"/>" +
                "</ui:UXML>", LoadOptions.SetLineInfo));
            var styleText =
                "#Missing, #HoverOnly, #SameStates, #InlineBlocked, #Complete, " +
                "#TextOnly { background-color: rgba(0, 0, 0, 0); }\n" +
                "#HoverOnly:hover { opacity: 0.8; }\n" +
                "#SameStates:hover, #SameStates:active { opacity: 0.8; }\n" +
                "#InlineBlocked:hover { opacity: 0.8; }\n" +
                "#InlineBlocked:active { scale: 0.9 0.9; }\n" +
                "#Complete:hover { opacity: 0.8; }\n" +
                "#Complete:active { scale: 0.9 0.9; }\n" +
                "#TextHoverOnly, #TextSameStates, #TextComplete, " +
                "#OpaqueNoHover { background-color: black; }\n" +
                "#TextHoverOnly:hover { background-color: gray; }\n" +
                "#TextSameStates:hover, #TextSameStates:active " +
                "{ background-color: gray; }\n" +
                "#TextComplete:hover { background-color: gray; }\n" +
                "#TextComplete:active { scale: 0.9 0.9; }\n";
            var cascadeDocument = new UssCascadeDocument(authored);
            cascadeDocument.LoadedAssetPaths.Add(stylePath);
            AppendSelfTestRules(cascadeDocument,
                ParseStyleSheet(stylePath, styleText), 1);
            var cascade = new UssCascadeIndex();
            cascade.Documents.Add(cascadeDocument);
            var report = new VmAutomationUssStyleAuditReport(20);
            Audit(cascade, new[] { stylePath }, report);
            var actual = report.Issues
                .Select(issue => issue.Token + ":" +
                                 (issue.Message.Contains(":hover") ? "hover" : "active"))
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            var cases = new List<Dictionary<string, object>>();
            AddSelfTestCase(cases, "composite icon buttons need hover and press feedback",
                report.Issues.Where(issue =>
                        issue.Kind == "missing-composite-button-feedback")
                    .Select(issue => issue.Token + ":" +
                        (issue.Message.Contains(":hover") ? "hover" : "active"))
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .SequenceEqual(new[]
                {
                    "HoverOnly:active", "InlineBlocked:hover", "Missing:active",
                    "Missing:hover", "SameStates:active"
                }));
            AddSelfTestCase(cases, "hovered text buttons require distinct press feedback",
                actual.Where(value => value.StartsWith("Text", StringComparison.Ordinal))
                    .SequenceEqual(new[]
                    {
                        "TextHoverOnly:active", "TextSameStates:active"
                    }) && report.Issues.Count(issue =>
                    issue.Kind == "missing-button-press-feedback") == 2);
            AddSelfTestCase(cases, "complete states and unstyled text buttons pass",
                report.Issues.All(issue => issue.Token != "Complete" &&
                                           issue.Token != "TextOnly" &&
                                           issue.Token != "TextComplete" &&
                                           issue.Token != "OpaqueNoHover"));
            return cases;
        }

        private static void AppendSelfTestRules(UssCascadeDocument document,
            IEnumerable<UssRule> rules, int origin)
        {
            foreach (var rule in rules)
            {
                foreach (var selectorText in rule.Selectors)
                {
                    document.Rules.Add(new UssCascadeRule
                    {
                        Rule = rule,
                        SelectorText = selectorText,
                        Origin = origin,
                        SourceOrder = document.NextSourceOrder()
                    });
                }
            }
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
    }
}
#endif
