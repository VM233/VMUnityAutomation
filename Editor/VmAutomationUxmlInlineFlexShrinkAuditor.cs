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
    internal static class VmAutomationUxmlInlineFlexShrinkAuditor
    {
        internal const string KIND = "ineffective-no-pressure-flex-shrink";
        internal const string SUPPRESSION_MARKER =
            "uxml-layout-audit: allow-no-pressure-flex-shrink";

        private const float LayoutEpsilon = 0.01f;

        private static readonly Regex PixelValueRegex =
            new Regex(@"^(?<value>-?(?:\d+(?:\.\d+)?|\.\d+))(?<unit>px)?$",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex StyleDeclarationRegex =
            new Regex(@"(?:^|;)\s*(?<name>[-A-Za-z0-9]+)\s*:\s*(?<value>[^;]+)",
                RegexOptions.Compiled);

        private static readonly Regex SuppressionRegex =
            new Regex(
                @"^\s*uxml-layout-audit:\s*allow-no-pressure-flex-shrink\s+(?<reason>.+?)\s*$",
                RegexOptions.Compiled | RegexOptions.IgnoreCase |
                RegexOptions.Singleline);

        internal static void Audit(string assetPath, XDocument document,
            Func<XElement, IReadOnlyDictionary<string, string>> resolveAuthoredStyle,
            VmAutomationUxmlLayoutAuditReport report, bool includeSuppressed)
        {
            if (document?.Root == null)
            {
                return;
            }

            foreach (var element in document.Root.DescendantsAndSelf()
                         .Where(IsVisualContentElement))
            {
                if (TryGetInlineDeclaration(element, "flex-shrink",
                        out var flexShrink) == false ||
                    IsZero(flexShrink) == false)
                {
                    continue;
                }

                var style = resolveAuthoredStyle(element);
                if (StyleValue(style, "display") == "none")
                {
                    continue;
                }

                if (StyleValue(style, "position") == "absolute")
                {
                    RecordIssue(assetPath, element, flexShrink, "layout",
                        0, 0, element.Parent as XElement, report,
                        includeSuppressed,
                        "The element is outside Flex flow, so flex-shrink cannot participate.");
                    continue;
                }

                if (!(element.Parent is XElement parent) ||
                    IsVisualContentElement(parent) == false ||
                    TryProveNonNegativeFreeSpace(parent, resolveAuthoredStyle,
                        out var axis, out var requiredSize, out var availableSize,
                        out var proofOwner) == false)
                {
                    continue;
                }

                RecordIssue(assetPath, element, flexShrink, axis,
                    requiredSize, availableSize, proofOwner, report,
                    includeSuppressed,
                    $"The authored natural-size chain requires {FormatPixels(requiredSize)} " +
                    $"inside {FormatPixels(availableSize)} of available {axis} extent, " +
                    "so the Flex line has no negative free space for shrink to resolve.");
            }
        }

        internal static IReadOnlyList<Dictionary<string, object>> RunSelfTests()
        {
            var cases = new List<Dictionary<string, object>>();

            const string forgeLikeLayout =
                "<ui:VisualElement style=\"height: 507px;\">" +
                "<ui:VisualElement><ui:VisualElement style=\"height: 42px;\"/>" +
                "</ui:VisualElement>" +
                "<ui:VisualElement style=\"flex-grow: 1;\">" +
                "<ui:VisualElement style=\"height: 60px; margin-top: 15px;\"/>" +
                "<ui:VisualElement style=\"height: 51px; margin-top: 3px;\"/>" +
                "<ui:VisualElement style=\"height: 60px; margin-top: 6px;\"/>" +
                "<ui:VisualElement style=\"height: 30px; margin-top: 9px;\"/>" +
                "<ui:VisualElement style=\"height: 156px; margin-top: 12px;\"/>" +
                "<ui:Button name=\"Action\" style=\"height: 45px; margin-top: 9px; " +
                "flex-shrink: 0;\"/>" +
                "</ui:VisualElement></ui:VisualElement>";
            var forgeLikeReport = AuditFixture(forgeLikeLayout, false);
            AddCase(cases, "fitting fixed forge column inline shrink warns",
                HasActiveFinding(forgeLikeReport, "Action") &&
                forgeLikeReport.Issues.Single(issue => issue.Kind == KIND).Axis ==
                "vertical");

            var directBoundReport = AuditFixture(
                "<ui:VisualElement style=\"height: 100px;\">" +
                "<ui:Button name=\"Action\" style=\"height: 45px; " +
                "flex-shrink: 0;\"/></ui:VisualElement>", false);
            AddCase(cases, "direct bounded fitting column inline shrink warns",
                HasActiveFinding(directBoundReport, "Action"));

            var fittingRowReport = AuditFixture(
                "<ui:VisualElement style=\"width: 200px; flex-direction: row;\">" +
                "<ui:Button name=\"Action\" style=\"width: 60px; " +
                "flex-shrink: 0;\"/>" +
                "<ui:VisualElement style=\"width: 90px;\"/>" +
                "</ui:VisualElement>", false);
            AddCase(cases, "direct bounded fitting row inline shrink warns",
                HasActiveFinding(fittingRowReport, "Action") &&
                fittingRowReport.Issues.Single(issue => issue.Kind == KIND).Axis ==
                "horizontal");

            var pressuredReport = AuditFixture(
                "<ui:VisualElement style=\"height: 60px;\">" +
                "<ui:Button style=\"height: 45px; flex-shrink: 0;\"/>" +
                "<ui:VisualElement style=\"height: 45px;\"/>" +
                "</ui:VisualElement>", false);
            AddCase(cases, "negative free space retains inline shrink",
                HasAnyFinding(pressuredReport) == false);

            var intrinsicUnknownReport = AuditFixture(
                "<ui:VisualElement style=\"height: 100px;\">" +
                "<ui:Button style=\"height: 45px; flex-shrink: 0;\"/>" +
                "<ui:Label text=\"runtime localized text\"/>" +
                "</ui:VisualElement>", false);
            AddCase(cases, "unknown intrinsic sibling remains conservative",
                HasAnyFinding(intrinsicUnknownReport) == false);

            var stylesheetOwnedReport = AuditFixture(
                "<ui:VisualElement style=\"height: 100px;\">" +
                "<ui:Button style=\"height: 45px;\"/>" +
                "</ui:VisualElement>", false,
                element => ResolveInlineStyle(element, "0"));
            AddCase(cases, "non-inline shrink remains owned by USS audit",
                HasAnyFinding(stylesheetOwnedReport) == false);

            var absoluteReport = AuditFixture(
                "<ui:VisualElement><ui:Button name=\"Overlay\" " +
                "style=\"position: absolute; height: 45px; flex-shrink: 0;\"/>" +
                "</ui:VisualElement>", false);
            AddCase(cases, "absolute inline shrink warns",
                HasActiveFinding(absoluteReport, "Overlay"));

            var suppressedReport = AuditFixture(
                "<ui:VisualElement style=\"height: 100px;\">" +
                $"<!-- {SUPPRESSION_MARKER} fixture is resized by runtime code -->" +
                "<ui:Button style=\"height: 45px; flex-shrink: 0;\"/>" +
                "</ui:VisualElement>", true);
            AddCase(cases, "reasoned no-pressure shrink suppression is retained",
                suppressedReport.WarningCount == 0 &&
                suppressedReport.SuppressedCount == 1 &&
                suppressedReport.Issues.Single(issue => issue.Kind == KIND).Suppressed);

            return cases;
        }

        private static bool TryProveNonNegativeFreeSpace(XElement parent,
            Func<XElement, IReadOnlyDictionary<string, string>> resolveAuthoredStyle,
            out string axis, out float requiredSize, out float availableSize,
            out XElement proofOwner)
        {
            var parentStyle = resolveAuthoredStyle(parent);
            axis = IsHorizontalFlow(parentStyle) ? "horizontal" : "vertical";
            requiredSize = 0;
            availableSize = 0;
            proofOwner = null;

            var current = parent;
            while (current != null && IsVisualContentElement(current))
            {
                var currentStyle = resolveAuthoredStyle(current);
                if (StyleValue(currentStyle, "display") == "none" ||
                    HasUnsupportedMainAxisConstraint(currentStyle, axis))
                {
                    return false;
                }

                if (TryGetInnerExtent(currentStyle, axis, out availableSize))
                {
                    if (TryGetNaturalContentExtent(current, axis,
                            resolveAuthoredStyle, out requiredSize) == false ||
                        requiredSize > availableSize + LayoutEpsilon)
                    {
                        return false;
                    }

                    proofOwner = current;
                    return true;
                }

                if (!(current.Parent is XElement owner) ||
                    IsVisualContentElement(owner) == false)
                {
                    return false;
                }

                var ownerStyle = resolveAuthoredStyle(owner);
                var ownerAxis = IsHorizontalFlow(ownerStyle)
                    ? "horizontal"
                    : "vertical";
                if (ownerAxis != axis ||
                    StyleValue(currentStyle, "position") == "absolute" ||
                    HasConcreteValue(currentStyle, "flex-basis"))
                {
                    return false;
                }

                current = owner;
            }

            return false;
        }

        private static bool TryGetNaturalContentExtent(XElement container, string axis,
            Func<XElement, IReadOnlyDictionary<string, string>> resolveAuthoredStyle,
            out float extent)
        {
            extent = 0;
            var style = resolveAuthoredStyle(container);
            var containerAxis = IsHorizontalFlow(style) ? "horizontal" : "vertical";
            if (containerAxis != axis ||
                HasUnsupportedFlowContract(style, axis))
            {
                return false;
            }

            var children = container.Elements()
                .Where(IsVisualContentElement)
                .Where(child =>
                {
                    var childStyle = resolveAuthoredStyle(child);
                    return StyleValue(childStyle, "display") != "none" &&
                           StyleValue(childStyle, "position") != "absolute";
                })
                .ToList();
            foreach (var child in children)
            {
                if (TryGetNaturalOuterExtent(child, axis, resolveAuthoredStyle,
                        out var childExtent) == false)
                {
                    return false;
                }

                extent += childExtent;
            }

            var gap = 0f;
            if (children.Count > 1 &&
                TryGetOptionalPixel(style,
                    axis == "horizontal" ? "column-gap" : "row-gap",
                    out gap) == false)
            {
                return false;
            }

            if (children.Count > 1)
            {
                extent += gap * (children.Count - 1);
            }

            return true;
        }

        private static bool TryGetNaturalOuterExtent(XElement element, string axis,
            Func<XElement, IReadOnlyDictionary<string, string>> resolveAuthoredStyle,
            out float extent)
        {
            extent = 0;
            var style = resolveAuthoredStyle(element);
            if (HasUnsupportedMainAxisConstraint(style, axis) ||
                HasConcreteValue(style, "flex-basis") ||
                TryGetAxisSpacing(style, "margin", axis, out var margin) == false ||
                TryGetAxisSpacing(style, "padding", axis, out var padding) == false ||
                TryGetAxisBorder(style, axis, out var border) == false)
            {
                return false;
            }

            var sizeProperty = axis == "horizontal" ? "width" : "height";
            if (style.TryGetValue(sizeProperty, out var authoredSize) &&
                IsInitialOrAuto(authoredSize) == false)
            {
                if (TryParsePixels(authoredSize, out var fixedSize) == false)
                {
                    return false;
                }

                extent = fixedSize + padding + border + margin;
                return true;
            }

            var children = element.Elements().Where(IsVisualContentElement).ToList();
            if (children.Count == 0)
            {
                if (element.Name.LocalName != "VisualElement")
                {
                    return false;
                }

                extent = margin;
                return true;
            }

            if (HasUnsupportedFlowContract(style, axis))
            {
                return false;
            }

            var sameAxis = (IsHorizontalFlow(style) ? "horizontal" : "vertical") == axis;
            var childExtents = new List<float>();
            foreach (var child in children)
            {
                var childStyle = resolveAuthoredStyle(child);
                if (StyleValue(childStyle, "display") == "none" ||
                    StyleValue(childStyle, "position") == "absolute")
                {
                    continue;
                }

                if (TryGetNaturalOuterExtent(child, axis, resolveAuthoredStyle,
                        out var childExtent) == false)
                {
                    return false;
                }

                childExtents.Add(childExtent);
            }

            var content = childExtents.Count == 0
                ? 0
                : sameAxis ? childExtents.Sum() : childExtents.Max();
            if (sameAxis && childExtents.Count > 1)
            {
                if (TryGetOptionalPixel(style,
                        axis == "horizontal" ? "column-gap" : "row-gap",
                        out var gap) == false)
                {
                    return false;
                }

                content += gap * (childExtents.Count - 1);
            }

            extent = content + padding + border + margin;
            return true;
        }

        private static bool TryGetInnerExtent(
            IReadOnlyDictionary<string, string> style, string axis, out float extent)
        {
            extent = 0;
            var sizeProperty = axis == "horizontal" ? "width" : "height";
            if (style.TryGetValue(sizeProperty, out var authoredSize) == false ||
                IsInitialOrAuto(authoredSize) ||
                TryParsePixels(authoredSize, out var fixedSize) == false ||
                TryGetAxisSpacing(style, "padding", axis, out var padding) == false ||
                TryGetAxisBorder(style, axis, out var border) == false)
            {
                return false;
            }

            extent = Math.Max(0, fixedSize - padding - border);
            return true;
        }

        private static bool HasUnsupportedMainAxisConstraint(
            IReadOnlyDictionary<string, string> style, string axis)
        {
            var sizeProperty = axis == "horizontal" ? "width" : "height";
            return HasConcreteValue(style, "min-" + sizeProperty) ||
                   HasConcreteValue(style, "max-" + sizeProperty);
        }

        private static bool HasUnsupportedFlowContract(
            IReadOnlyDictionary<string, string> style, string axis)
        {
            var wrap = StyleValue(style, "flex-wrap");
            if (string.IsNullOrWhiteSpace(wrap) == false && wrap != "nowrap")
            {
                return true;
            }

            var shorthandGap = StyleValue(style, "gap");
            return string.IsNullOrWhiteSpace(shorthandGap) == false &&
                   style.ContainsKey(axis == "horizontal" ? "column-gap" : "row-gap") ==
                   false;
        }

        private static bool TryGetAxisSpacing(
            IReadOnlyDictionary<string, string> style, string prefix, string axis,
            out float spacing)
        {
            spacing = 0;
            var leading = axis == "horizontal" ? "left" : "top";
            var trailing = axis == "horizontal" ? "right" : "bottom";
            var shorthandLeading = 0f;
            var shorthandTrailing = 0f;
            if (style.TryGetValue(prefix, out var shorthand) &&
                TryParseSpacingShorthand(shorthand, axis, out shorthandLeading,
                    out shorthandTrailing) == false)
            {
                return false;
            }

            if (TryGetOptionalPixel(style, prefix + "-" + leading,
                    out var leadingValue) == false ||
                TryGetOptionalPixel(style, prefix + "-" + trailing,
                    out var trailingValue) == false)
            {
                return false;
            }

            spacing = (style.ContainsKey(prefix + "-" + leading)
                          ? leadingValue
                          : shorthandLeading) +
                      (style.ContainsKey(prefix + "-" + trailing)
                          ? trailingValue
                          : shorthandTrailing);
            return true;
        }

        private static bool TryGetAxisBorder(
            IReadOnlyDictionary<string, string> style, string axis, out float border)
        {
            var leading = axis == "horizontal" ? "left" : "top";
            var trailing = axis == "horizontal" ? "right" : "bottom";
            if (TryGetOptionalPixel(style, "border-" + leading + "-width",
                    out var leadingValue) == false ||
                TryGetOptionalPixel(style, "border-" + trailing + "-width",
                    out var trailingValue) == false)
            {
                border = 0;
                return false;
            }

            border = leadingValue + trailingValue;
            return true;
        }

        private static bool TryParseSpacingShorthand(string rawValue, string axis,
            out float leading, out float trailing)
        {
            leading = 0;
            trailing = 0;
            var parts = (rawValue ?? string.Empty).Split((char[])null,
                StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 1 || parts.Length > 4)
            {
                return false;
            }

            var values = new float[parts.Length];
            for (var index = 0; index < parts.Length; index++)
            {
                if (TryParsePixels(parts[index], out values[index]) == false)
                {
                    return false;
                }
            }

            if (axis == "vertical")
            {
                leading = values[0];
                trailing = values.Length > 2 ? values[2] : values[0];
            }
            else
            {
                trailing = values.Length > 1 ? values[1] : values[0];
                leading = values.Length > 3 ? values[3] : trailing;
            }

            return true;
        }

        private static bool TryGetOptionalPixel(
            IReadOnlyDictionary<string, string> style, string property, out float value)
        {
            value = 0;
            return style.TryGetValue(property, out var rawValue) == false ||
                   IsInitialOrAuto(rawValue) ||
                   TryParsePixels(rawValue, out value);
        }

        private static bool TryParsePixels(string rawValue, out float value)
        {
            value = 0;
            var match = PixelValueRegex.Match((rawValue ?? string.Empty).Trim());
            if (match.Success == false ||
                float.TryParse(match.Groups["value"].Value, NumberStyles.Float,
                    CultureInfo.InvariantCulture, out value) == false)
            {
                return false;
            }

            return match.Groups["unit"].Success || Math.Abs(value) <= LayoutEpsilon;
        }

        private static bool HasConcreteValue(
            IReadOnlyDictionary<string, string> style, string property)
        {
            return style.TryGetValue(property, out var rawValue) &&
                   IsInitialOrAuto(rawValue) == false;
        }

        private static bool IsInitialOrAuto(string value)
        {
            var normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
            return string.IsNullOrWhiteSpace(normalized) || normalized == "auto" ||
                   normalized == "none" || normalized == "initial" ||
                   normalized == "inherit" || normalized == "unset";
        }

        private static bool IsHorizontalFlow(
            IReadOnlyDictionary<string, string> style)
        {
            var direction = StyleValue(style, "flex-direction");
            return direction == "row" || direction == "row-reverse";
        }

        private static string StyleValue(
            IReadOnlyDictionary<string, string> style, string property)
        {
            return style.TryGetValue(property, out var value)
                ? value.Trim().ToLowerInvariant()
                : string.Empty;
        }

        private static bool IsZero(string value)
        {
            return float.TryParse((value ?? string.Empty).Trim(), NumberStyles.Float,
                       CultureInfo.InvariantCulture, out var parsed) &&
                   Math.Abs(parsed) <= LayoutEpsilon;
        }

        private static bool TryGetInlineDeclaration(XElement element, string property,
            out string value)
        {
            value = string.Empty;
            var style = AttributeValue(element, "style");
            foreach (Match declaration in StyleDeclarationRegex.Matches(style))
            {
                if (string.Equals(declaration.Groups["name"].Value.Trim(), property,
                        StringComparison.OrdinalIgnoreCase))
                {
                    value = declaration.Groups["value"].Value.Trim();
                }
            }

            return string.IsNullOrWhiteSpace(value) == false;
        }

        private static void RecordIssue(string assetPath, XElement element,
            string flexShrink, string axis, float requiredSize, float availableSize,
            XElement proofOwner, VmAutomationUxmlLayoutAuditReport report,
            bool includeSuppressed, string proof)
        {
            var name = AttributeValue(element, "name");
            var elementLabel = string.IsNullOrWhiteSpace(name)
                ? $"<{element.Name.LocalName}>"
                : $"#{name}";
            var ownerName = proofOwner == null ? string.Empty :
                AttributeValue(proofOwner, "name");
            var ownerLabel = proofOwner == null
                ? "its authored layout owner"
                : string.IsNullOrWhiteSpace(ownerName)
                    ? $"<{proofOwner.Name.LocalName}>"
                    : $"#{ownerName}";
            var suppressionReason = GetSuppressionReason(element);
            report.Record(new VmAutomationUxmlLayoutAuditIssue
            {
                AssetPath = assetPath,
                Line = GetLineNumber(element),
                Element = elementLabel,
                ElementName = name,
                Kind = KIND,
                Axis = axis,
                ParentSize = availableSize,
                Size = requiredSize,
                FixedProperties = new List<string> { "flex-shrink" },
                InlineDeclarations = new Dictionary<string, string>(
                    StringComparer.OrdinalIgnoreCase)
                {
                    { "flex-shrink", flexShrink }
                },
                Suppressed = string.IsNullOrWhiteSpace(suppressionReason) == false,
                SuppressionReason = suppressionReason,
                Message =
                    $"{elementLabel} authors inline flex-shrink: {flexShrink}, but {proof} " +
                    $"Proof owner: {ownerLabel}. Remove flex-shrink; retain it only when " +
                    "runtime layout can create real shrink pressure and document that " +
                    "contract with a reasoned suppression."
            }, includeSuppressed);
        }

        private static string GetSuppressionReason(XElement element)
        {
            var previous = element.NodesBeforeSelf().Reverse().FirstOrDefault(node =>
                !(node is XText text) || string.IsNullOrWhiteSpace(text.Value) == false);
            if (!(previous is XComment comment))
            {
                return string.Empty;
            }

            var match = SuppressionRegex.Match(comment.Value);
            return match.Success ? match.Groups["reason"].Value.Trim() : string.Empty;
        }

        private static IReadOnlyDictionary<string, string> ResolveInlineStyle(
            XElement element, string syntheticFlexShrink = null)
        {
            var style = new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);
            foreach (Match declaration in StyleDeclarationRegex.Matches(
                         AttributeValue(element, "style")))
            {
                style[declaration.Groups["name"].Value.Trim()] =
                    declaration.Groups["value"].Value.Trim();
            }

            if (syntheticFlexShrink != null && style.ContainsKey("flex-shrink") == false)
            {
                style["flex-shrink"] = syntheticFlexShrink;
            }

            return style;
        }

        private static VmAutomationUxmlLayoutAuditReport AuditFixture(string body,
            bool includeSuppressed,
            Func<XElement, IReadOnlyDictionary<string, string>> styleResolver = null)
        {
            var document = XDocument.Parse(
                "<ui:UXML xmlns:ui=\"UnityEngine.UIElements\">" + body +
                "</ui:UXML>", LoadOptions.SetLineInfo);
            var report = new VmAutomationUxmlLayoutAuditReport(100);
            Audit("Assets/__InlineFlexShrinkSelfTest.uxml", document,
                styleResolver ?? (element => ResolveInlineStyle(element)),
                report, includeSuppressed);
            report.SortIssues();
            return report;
        }

        private static bool HasActiveFinding(
            VmAutomationUxmlLayoutAuditReport report, string elementName)
        {
            return report.Issues.Any(issue => issue.Kind == KIND &&
                                              issue.ElementName == elementName &&
                                              issue.Suppressed == false);
        }

        private static bool HasAnyFinding(VmAutomationUxmlLayoutAuditReport report)
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

        private static bool IsVisualContentElement(XElement element)
        {
            switch (element?.Name.LocalName)
            {
                case null:
                case "UXML":
                case "Bindings":
                case "Style":
                case "Template":
                case "AttributeOverrides":
                    return false;
                default:
                    return true;
            }
        }

        private static string AttributeValue(XElement element, string attributeName)
        {
            return element.Attributes().FirstOrDefault(attribute =>
                    string.Equals(attribute.Name.LocalName, attributeName,
                        StringComparison.OrdinalIgnoreCase))
                ?.Value ?? string.Empty;
        }

        private static int GetLineNumber(XObject value)
        {
            return value is IXmlLineInfo lineInfo && lineInfo.HasLineInfo()
                ? lineInfo.LineNumber
                : 1;
        }

        private static string FormatPixels(float value)
        {
            return value.ToString("0.##", CultureInfo.InvariantCulture) + "px";
        }
    }
}
#endif
