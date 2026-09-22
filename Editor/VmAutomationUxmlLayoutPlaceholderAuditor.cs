#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using static VMUnityAutomation.Editor.VmAutomationUxmlLayoutAuditor;

namespace VMUnityAutomation.Editor
{
    internal static class VmAutomationUxmlLayoutPlaceholderAuditor
    {
        internal const string KIND = "layout-only-placeholder-element";

        private static readonly Regex PlaceholderTokenRegex = new Regex(
            @"(?:^|[-_])(?:balance|counterweight|spacer|shim)(?:$|[-_])",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly HashSet<string> LayoutProperties =
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
                "column-gap",
                "display"
            };

        internal static void Audit(string assetPath, XDocument document,
            Func<XElement, IReadOnlyDictionary<string, string>> resolveAuthoredStyle,
            VmAutomationUxmlLayoutAuditReport report)
        {
            if (document?.Root == null)
            {
                return;
            }

            foreach (var element in document.Root.Descendants()
                         .Where(IsForbiddenLayoutPlaceholder))
            {
                var authoredStyle = resolveAuthoredStyle(element);
                if (authoredStyle.Count == 0 ||
                    authoredStyle.Keys.Any(property =>
                        LayoutProperties.Contains(property) == false))
                {
                    continue;
                }

                var marker = GetPlaceholderMarker(element);
                var elementLabel = "." + marker;
                report.Record(new VmAutomationUxmlLayoutAuditIssue
                {
                    AssetPath = assetPath,
                    Line = GetLineNumber(element),
                    Element = elementLabel,
                    ElementName = string.Empty,
                    Kind = KIND,
                    Severity = "error",
                    Axis = GetLayoutAxis(authoredStyle.Keys),
                    FixedProperties = authoredStyle.Keys
                        .OrderBy(property => property, StringComparer.OrdinalIgnoreCase)
                        .ToList(),
                    InlineDeclarations = new Dictionary<string, string>(authoredStyle,
                        StringComparer.OrdinalIgnoreCase),
                    Message =
                        $"{elementLabel} is an unnamed, content-free layout placeholder " +
                        "whose only authored responsibility is balancing or spacing sibling " +
                        "geometry. Remove the placeholder and make the semantic container own " +
                        "centering or distribution; anchor independent edge controls with " +
                        "absolute positioning."
                }, false);
            }
        }

        internal static IReadOnlyList<Dictionary<string, object>> RunSelfTests()
        {
            var cases = new List<Dictionary<string, object>>();

            var headerBalance = AuditFixture(
                "<ui:VisualElement class=\"forge-station-header-balance\" " +
                "picking-mode=\"Ignore\"><ui:VisualElement " +
                "class=\"forge-title-connector\" picking-mode=\"Ignore\"/>" +
                "</ui:VisualElement>");
            AddCase(cases, "header balance counterweight is an error",
                headerBalance.ErrorCount == 1 &&
                headerBalance.Issues.Single().Kind == KIND);

            var spacer = AuditFixture(
                "<ui:VisualElement class=\"toolbar-spacer\" picking-mode=\"Ignore\"/>");
            AddCase(cases, "empty spacer is an error",
                spacer.ErrorCount == 1 && spacer.Issues.Single().Kind == KIND);

            var namedRuntimeRegion = AuditFixture(
                "<ui:VisualElement name=\"BalancePanel\" class=\"account-balance\" " +
                "picking-mode=\"Ignore\"/>");
            AddCase(cases, "named runtime balance region passes",
                namedRuntimeRegion.ErrorCount == 0);

            var semanticBalance = AuditFixture(
                "<ui:VisualElement class=\"account-balance\" picking-mode=\"Ignore\">" +
                "<ui:Label text=\"100\"/></ui:VisualElement>");
            AddCase(cases, "balance region with semantic content passes",
                semanticBalance.ErrorCount == 0);

            var visualSpacer = AuditFixture(
                "<ui:VisualElement class=\"divider-spacer\" picking-mode=\"Ignore\" " +
                "style=\"width: 2px; background-color: white;\"/>");
            AddCase(cases, "independently visible divider passes",
                visualSpacer.ErrorCount == 0);

            var anchoredHeader = AuditFixture(
                "<ui:VisualElement class=\"station-header\">" +
                "<ui:VisualElement class=\"station-title\"/>" +
                "<ui:Button name=\"Help\" text=\"?\" " +
                "style=\"position: absolute; right: -23px;\"/>" +
                "</ui:VisualElement>");
            AddCase(cases, "centered title with anchored help passes",
                anchoredHeader.ErrorCount == 0);

            return cases;
        }

        private static bool IsForbiddenLayoutPlaceholder(XElement element)
        {
            if (element.Name.LocalName != "VisualElement" ||
                string.IsNullOrWhiteSpace(AttributeValue(element, "name")) == false ||
                string.Equals(AttributeValue(element, "picking-mode"), "Ignore",
                    StringComparison.OrdinalIgnoreCase) == false ||
                string.IsNullOrWhiteSpace(GetPlaceholderMarker(element)) ||
                HasSemanticContent(element))
            {
                return false;
            }

            return true;
        }

        private static string GetPlaceholderMarker(XElement element)
        {
            return SplitWhitespace(AttributeValue(element, "class"))
                .FirstOrDefault(className => PlaceholderTokenRegex.IsMatch(className)) ??
                   string.Empty;
        }

        private static bool HasSemanticContent(XElement element)
        {
            if (element.Elements().Any(child => child.Name.LocalName == "Bindings") ||
                HasSemanticAttribute(element))
            {
                return true;
            }

            foreach (var descendant in element.Descendants())
            {
                if (descendant.Name.LocalName == "Bindings" ||
                    IsUxmlMetadataElement(descendant) == false &&
                    descendant.Name.LocalName != "VisualElement" ||
                    HasSemanticAttribute(descendant))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HasSemanticAttribute(XElement element)
        {
            return element.Attributes().Any(attribute =>
            {
                switch (attribute.Name.LocalName.ToLowerInvariant())
                {
                    case "name":
                    case "text":
                    case "value":
                    case "binding-path":
                    case "data-source":
                    case "data-source-type":
                    case "tooltip":
                    case "focusable":
                    case "tabindex":
                    case "src":
                    case "template":
                        return string.IsNullOrWhiteSpace(attribute.Value) == false;
                    default:
                        return false;
                }
            });
        }

        private static VmAutomationUxmlLayoutAuditReport AuditFixture(string element)
        {
            var document = XDocument.Parse(
                "<ui:UXML xmlns:ui=\"UnityEngine.UIElements\">" + element +
                "</ui:UXML>", LoadOptions.SetLineInfo);
            var report = new VmAutomationUxmlLayoutAuditReport(100);
            Audit("Assets/__LayoutPlaceholderAuditSelfTest.uxml", document,
                ResolveSelfTestStyle, report);
            report.SortIssues();
            return report;
        }

        private static IReadOnlyDictionary<string, string> ResolveSelfTestStyle(
            XElement element)
        {
            var style = ParseStyle(AttributeValue(element, "style"));
            var classes = SplitWhitespace(AttributeValue(element, "class")).ToList();
            if (classes.Contains("forge-station-header-balance"))
            {
                style["width"] = "70px";
                style["flex-direction"] = "row";
                style["align-items"] = "center";
                style["justify-content"] = "flex-end";
            }
            else if (classes.Contains("toolbar-spacer"))
            {
                style["flex-grow"] = "1";
            }
            else if (classes.Contains("account-balance"))
            {
                style["flex-direction"] = "row";
            }
            else if (classes.Contains("station-header"))
            {
                style["align-items"] = "center";
                style["justify-content"] = "center";
            }

            return style;
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
