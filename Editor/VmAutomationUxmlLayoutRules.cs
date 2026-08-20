#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using static VMUnityAutomation.Editor.VmAutomationUxmlLayoutAuditor;
using static VMUnityAutomation.Editor.VmAutomationUxmlLayoutContractIndexer;
using static VMUnityAutomation.Editor.VmAutomationUxmlLayoutModels;

namespace VMUnityAutomation.Editor
{
    internal static class VmAutomationUxmlLayoutRules
    {
    internal static void AuditRedundantInlineDeclarations(string assetPath,
        XDocument document, UxmlInlineStyleContractIndex inlineStyleContracts,
        VmAutomationUxmlLayoutAuditReport report, bool includeSuppressed)
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
            var issue = new VmAutomationUxmlLayoutAuditIssue
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

    internal static void AuditVisuallyInertTextStretch(string assetPath,
        XDocument document, UxmlInlineStyleContractIndex inlineStyleContracts,
        UxmlLayoutContractIndex layoutContracts, VmAutomationUxmlLayoutAuditReport report,
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
            var issue = new VmAutomationUxmlLayoutAuditIssue
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

    internal static void AuditVisuallyInertTextGrow(string assetPath,
        XDocument document, UxmlInlineStyleContractIndex inlineStyleContracts,
        UxmlLayoutContractIndex layoutContracts, VmAutomationUxmlLayoutAuditReport report,
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
            var issue = new VmAutomationUxmlLayoutAuditIssue
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

    internal static void AuditSingleChildCenteringWrapper(string assetPath,
        XDocument document, UxmlInlineStyleContractIndex inlineStyleContracts,
        UxmlLayoutContractIndex layoutContracts, VmAutomationUxmlLayoutAuditReport report,
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
            var issue = new VmAutomationUxmlLayoutAuditIssue
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

    internal static void AuditFixedScrollCrossAxisContentSizes(string assetPath,
        XDocument document, UxmlInlineStyleContractIndex inlineStyleContracts,
        UxmlLayoutContractIndex layoutContracts, VmAutomationUxmlLayoutAuditReport report,
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
                var issue = new VmAutomationUxmlLayoutAuditIssue
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

    internal static bool TryGetSingleAxisScrollCrossAxis(XElement scrollView,
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

    internal static void AuditUnconsumedElementNames(string assetPath,
        XDocument document, UxmlElementNameReferenceIndex elementNameReferences,
        VmAutomationUxmlLayoutAuditReport report, bool includeSuppressed)
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
            var issue = new VmAutomationUxmlLayoutAuditIssue
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

    internal static void AuditFixedFlexPartitions(string assetPath,
        XDocument document, UxmlInlineStyleContractIndex inlineStyleContracts,
        VmAutomationUxmlLayoutAuditReport report, bool includeSuppressed)
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
            var issue = new VmAutomationUxmlLayoutAuditIssue
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


    }
}
#endif
