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
using static VMUnityAutomation.Editor.VmAutomationUxmlLayoutContractIndexer;
using static VMUnityAutomation.Editor.VmAutomationUxmlLayoutModels;
using static VMUnityAutomation.Editor.VmAutomationUxmlLayoutRules;

namespace VMUnityAutomation.Editor
{
    internal static class VmAutomationUxmlLayoutAuditor
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

        internal const float CENTER_EPSILON = 0.01f;

        internal static readonly Regex styleDeclarationRegex =
            new Regex(@"(?:^|;)\s*(?<name>[-A-Za-z0-9]+)\s*:\s*(?<value>[^;]+)",
                RegexOptions.Compiled);

        internal static readonly Regex pixelValueRegex =
            new Regex(@"^(?<value>-?(?:\d+(?:\.\d+)?|\.\d+))px$",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        internal static readonly Regex suppressionRegex =
            new Regex(@"^\s*uxml-layout-audit:\s*allow-manual-center\s+(?<reason>.+?)\s*$",
                RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

        internal static readonly Regex repeatedInlineSuppressionRegex =
            new Regex(@"^\s*uxml-layout-audit:\s*allow-repeated-inline\s+(?<reason>.+?)\s*$",
                RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

        internal static readonly Regex redundantInlineSuppressionRegex =
            new Regex(@"^\s*uxml-layout-audit:\s*allow-redundant-inline\s+(?<reason>.+?)\s*$",
                RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

        internal static readonly Regex inertTextStretchSuppressionRegex =
            new Regex(@"^\s*uxml-layout-audit:\s*allow-inert-text-stretch\s+(?<reason>.+?)\s*$",
                RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

        internal static readonly Regex inertTextGrowSuppressionRegex =
            new Regex(@"^\s*uxml-layout-audit:\s*allow-inert-text-grow\s+(?<reason>.+?)\s*$",
                RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

        internal static readonly Regex singleChildCenteringWrapperSuppressionRegex =
            new Regex(
                @"^\s*uxml-layout-audit:\s*allow-single-child-centering-wrapper\s+(?<reason>.+?)\s*$",
                RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

        internal static readonly Regex fixedScrollCrossAxisSizeSuppressionRegex =
            new Regex(
                @"^\s*uxml-layout-audit:\s*allow-fixed-scroll-cross-axis-size\s+(?<reason>.+?)\s*$",
                RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

        internal static readonly Regex unconsumedElementNameSuppressionRegex =
            new Regex(
                @"^\s*uxml-layout-audit:\s*allow-unconsumed-element-name\s+(?<reason>.+?)\s*$",
                RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

        internal static readonly Regex fixedFlexPartitionSuppressionRegex =
            new Regex(
                @"^\s*uxml-layout-audit:\s*allow-fixed-flex-partition\s+(?<reason>.+?)\s*$",
                RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

        internal static readonly Regex pixelGridSuppressionRegex =
            new Regex(
                @"^\s*uxml-layout-audit:\s*allow-off-grid-pixels\s+(?<reason>.+?)\s*$",
                RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

        internal static readonly Regex tooltipAttributeSuppressionRegex =
            new Regex(
                @"^\s*uxml-layout-audit:\s*allow-tooltip\s+(?<reason>.+?)\s*$",
                RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

        internal static readonly Regex ussCommentRegex =
            new Regex(@"/\*.*?\*/", RegexOptions.Compiled | RegexOptions.Singleline);

        internal static readonly Regex ussRuleRegex =
            new Regex(@"(?<selector>[^{}]+)\{(?<body>[^{}]*)\}",
                RegexOptions.Compiled | RegexOptions.Singleline);

        internal static readonly Regex classTokenRegex =
            new Regex(@"(?<![A-Za-z0-9_-])\.(?<token>[A-Za-z_][A-Za-z0-9_-]*)",
                RegexOptions.Compiled);

        internal static readonly Regex idTokenRegex =
            new Regex(@"(?<![A-Za-z0-9_-])#(?<token>[A-Za-z_][A-Za-z0-9_-]*)",
                RegexOptions.Compiled);

        internal static readonly Regex quotedNameReferenceRegex =
            new Regex(@"[""'](?<token>[^""'\r\n]+)[""']",
                RegexOptions.Compiled);

        internal static readonly Regex yamlListNameReferenceRegex =
            new Regex(@"^\s*-\s*(?<token>[^#\r\n]+?)\s*$",
                RegexOptions.Compiled | RegexOptions.Multiline);

        internal static readonly Regex yamlScalarNameReferenceRegex =
            new Regex(@"^\s*[A-Za-z_][A-Za-z0-9_]*:\s*(?<token>[^#\r\n]+?)\s*$",
                RegexOptions.Compiled | RegexOptions.Multiline);

        internal static readonly Dictionary<string, IReadOnlyList<string>>
            implicitElementClassesByType =
                new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);

        internal static readonly HashSet<string> variantLayoutProperties =
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

        internal static VmAutomationUxmlLayoutAuditReport Audit(IEnumerable<string> requestedPaths,
            bool includeSuppressed, int maxIssues, VmAutomationUIToolkitAuditOptions options)
        {
            options = options ?? VmAutomationUIToolkitAuditOptions.FromArguments(
                new Dictionary<string, object>());
            var report = new VmAutomationUxmlLayoutAuditReport(maxIssues);
            var requestedPathList = (requestedPaths ?? Array.Empty<string>())
                .Where(path => string.IsNullOrWhiteSpace(path) == false).ToList();
            var requested = NormalizeRequestedPaths(requestedPathList, report.Errors);
            var allUxmlPaths = VmAutomationUIToolkitAuditUtility.FindAssetFiles(".uxml", options)
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
                        File.ReadAllText(VmAutomationUIToolkitAuditUtility.ToFullPath(path)),
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

        internal static void AuditText(string assetPath, string text,
            UxmlLayoutContractIndex layoutContracts,
            UxmlElementNameReferenceIndex elementNameReferences,
            VmAutomationUxmlLayoutAuditReport report, bool includeSuppressed,
            UxmlInlineStyleContractIndex inlineStyleContracts = null,
            VmAutomationUIToolkitAuditOptions options = null)
        {
            var document = XDocument.Parse(text, LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo);
            IndexUxmlDocument(document, layoutContracts);
            options = options ?? VmAutomationUIToolkitAuditOptions.FromArguments(
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
            VmAutomationUxmlComponentInitialStyleAuditor.Audit(assetPath, document,
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
            VmAutomationUxmlNaturalFlowLayoutAuditor.Audit(assetPath, document,
                element => ResolveAuthoredStyle(element, inlineStyleContracts),
                (element, style) => HasVisualBoxContract(element, style, layoutContracts),
                report, includeSuppressed);
            VmAutomationUxmlInlineFlexShrinkAuditor.Audit(assetPath, document,
                element => ResolveAuthoredStyle(element, inlineStyleContracts),
                report, includeSuppressed);
            AuditRepeatedInlineLayoutVariants(assetPath, document, layoutContracts, report,
                includeSuppressed);
        }

        internal static void AuditTooltipAttributes(string assetPath, XDocument document,
            VmAutomationUxmlLayoutAuditReport report, bool includeSuppressed)
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
                    var issue = new VmAutomationUxmlLayoutAuditIssue
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

        internal static void AuditPixelGridDeclarations(string assetPath, XDocument document,
            VmAutomationUIToolkitAuditOptions options, VmAutomationUxmlLayoutAuditReport report,
            bool includeSuppressed)
        {
            if (options.PixelGridEnabled == false)
                return;

            foreach (var element in document.Descendants())
            {
                var offGridDeclarations =
                    VmAutomationUIToolkitPixelGridAuditUtility.FindOffGridDeclarations(
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
                var issue = new VmAutomationUxmlLayoutAuditIssue
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

        internal static void AuditElement(string assetPath, XElement element,
            UxmlLayoutContractIndex layoutContracts, VmAutomationUxmlLayoutAuditReport report,
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
            var issue = new VmAutomationUxmlLayoutAuditIssue
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

        internal static bool HasVisualChildren(XElement element)
        {
            return element.Elements().Any(child =>
                child.Name.LocalName != "Bindings" &&
                child.Name.LocalName != "Style" &&
                child.Name.LocalName != "Template" &&
                child.Name.LocalName != "AttributeOverrides");
        }

        internal static bool HasBoxContract(XElement element, IReadOnlyDictionary<string, string> style,
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

        internal static bool HasExplicitZeroFlexShrink(
            IReadOnlyDictionary<string, string> style)
        {
            return style.TryGetValue("flex-shrink", out var value) &&
                   float.TryParse(value, NumberStyles.Float,
                       CultureInfo.InvariantCulture, out var shrink) &&
                   float.IsNaN(shrink) == false &&
                   float.IsInfinity(shrink) == false &&
                   Math.Abs(shrink) <= CENTER_EPSILON;
        }

        internal static bool HasNonZeroPartitionSpacing(
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

        internal static bool HasNonZeroMainAxisMargins(
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

        internal static bool HasUnexpectedCenteringWrapperStyle(
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

        internal static bool HasInteractionContract(XElement element)
        {
            return element.Elements().Any(child => child.Name.LocalName == "Bindings") ||
                   string.Equals(AttributeValue(element, "focusable"), "true",
                       StringComparison.OrdinalIgnoreCase) ||
                   string.IsNullOrWhiteSpace(AttributeValue(element, "tabindex")) == false ||
                   string.IsNullOrWhiteSpace(AttributeValue(element, "tooltip")) == false ||
                   string.Equals(AttributeValue(element, "picking-mode"), "Position",
                       StringComparison.OrdinalIgnoreCase);
        }

        internal static bool HasNonZeroMargin(IReadOnlyDictionary<string, string> style)
        {
            return style.Any(property =>
                (string.Equals(property.Key, "margin", StringComparison.OrdinalIgnoreCase) ||
                 property.Key.StartsWith("margin-", StringComparison.OrdinalIgnoreCase)) &&
                IsZeroBoxValue(property.Value) == false);
        }

        internal static bool HasPositiveFlexGrow(IReadOnlyDictionary<string, string> style)
        {
            return style.TryGetValue("flex-grow", out var value) &&
                   float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture,
                       out var grow) &&
                   float.IsNaN(grow) == false &&
                   float.IsInfinity(grow) == false &&
                   grow > 0;
        }

        internal static bool TryGetPixelLength(IReadOnlyDictionary<string, string> style,
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

        internal static bool IsOnlyVisualContentChild(XElement parent, XElement element)
        {
            var visualChildren = GetVisualContentChildren(parent);
            return visualChildren.Count == 1 && visualChildren[0] == element;
        }

        internal static List<XElement> GetVisualContentChildren(XElement parent)
        {
            return parent.Elements()
                .Where(child => IsUxmlMetadataElement(child) == false)
                .ToList();
        }

        internal static bool IsUxmlMetadataElement(XElement element)
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

        internal static Dictionary<string, string> ResolveAuthoredStyle(XElement element,
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

        internal static bool HasAxisSizeContract(
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

        internal static bool HasVisualBoxContract(XElement element,
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

        internal static bool IsMeaningfulVisualBoxProperty(string property, string value)
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

        internal static bool IsZeroBoxValue(string value)
        {
            var parts = SplitWhitespace(value).ToList();
            return parts.Count > 0 && parts.All(part =>
                Regex.IsMatch(part, @"^[+-]?0(?:\.0+)?(?:px|%|em|rem)?$",
                    RegexOptions.IgnoreCase));
        }

        internal static bool HasSymmetricCrossAxisMargins(
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

        internal static bool TryGetBoxSideValue(
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

        internal static bool StyleValuesEqual(string left, string right)
        {
            return string.Equals(
                Regex.Replace((left ?? "").Trim(), @"\s+", " "),
                Regex.Replace((right ?? "").Trim(), @"\s+", " "),
                StringComparison.OrdinalIgnoreCase);
        }

        internal static IReadOnlyCollection<string> GetElementClasses(XElement element)
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

        internal static IReadOnlyList<string> GetImplicitElementClasses(XElement element)
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

        internal static Type ResolveVisualElementType(XElement element)
        {
            var namespaceName = element?.Name.NamespaceName;
            return string.IsNullOrWhiteSpace(namespaceName)
                ? null
                : ResolveVisualElementType(namespaceName + "." + element.Name.LocalName);
        }

        internal static Type ResolveVisualElementType(string fullTypeName)
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

        internal static void AuditRepeatedInlineLayoutVariants(string assetPath,
            XDocument document, UxmlLayoutContractIndex layoutContracts,
            VmAutomationUxmlLayoutAuditReport report, bool includeSuppressed)
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
                var issue = new VmAutomationUxmlLayoutAuditIssue
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

        internal static string BuildDeclarationSignature(
            IReadOnlyDictionary<string, string> declarations)
        {
            return string.Join(";", declarations
                .OrderBy(declaration => declaration.Key, StringComparer.OrdinalIgnoreCase)
                .Select(declaration =>
                    declaration.Key.Trim().ToLowerInvariant() + ":" +
                    Regex.Replace(declaration.Value.Trim(), @"\s+", " ")
                        .ToLowerInvariant()));
        }

        internal static string FormatDeclarations(
            IReadOnlyDictionary<string, string> declarations)
        {
            return "{" + string.Join("; ", declarations
                .OrderBy(declaration => declaration.Key, StringComparer.OrdinalIgnoreCase)
                .Select(declaration => declaration.Key + ": " + declaration.Value)) + "}";
        }

        internal static string FormatElementLabel(XElement element, string fallbackClass)
        {
            var name = AttributeValue(element, "name");
            return string.IsNullOrWhiteSpace(name) ? "." + fallbackClass : "#" + name;
        }

        internal static string GetLayoutAxis(IEnumerable<string> properties)
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

        internal static bool IsVariantLayoutProperty(string property)
        {
            return variantLayoutProperties.Contains((property ?? "").Trim());
        }

        internal static bool IsBoxContractProperty(string property, string value)
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

        internal static Dictionary<string, string> ParseStyle(string style)
        {
            var properties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (Match declaration in styleDeclarationRegex.Matches(style ?? ""))
            {
                properties[declaration.Groups["name"].Value.Trim()] =
                    declaration.Groups["value"].Value.Trim();
            }

            return properties;
        }

        internal static string StyleValue(IReadOnlyDictionary<string, string> style, string property)
        {
            return style.TryGetValue(property, out var value)
                ? value.Trim().ToLowerInvariant()
                : "";
        }

        internal static bool TryGetPixels(IReadOnlyDictionary<string, string> style, string property,
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

        internal static string FormatPixels(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture) + "px";
        }

        internal static string GetSuppressionReason(XElement element)
        {
            return GetSuppressionReason(element, suppressionRegex);
        }

        internal static string GetSuppressionReason(XElement element, Regex markerRegex)
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

        internal static string AttributeValue(XElement element, string attributeName)
        {
            return element.Attributes().FirstOrDefault(attribute =>
                    string.Equals(attribute.Name.LocalName, attributeName,
                        StringComparison.OrdinalIgnoreCase))
                ?.Value ?? "";
        }

        internal static int GetLineNumber(XObject value)
        {
            return value is IXmlLineInfo lineInfo && lineInfo.HasLineInfo()
                ? lineInfo.LineNumber
                : 1;
        }

        internal static List<string> NormalizeRequestedPaths(IEnumerable<string> requestedPaths,
            ICollection<string> errors)
        {
            var requested = (requestedPaths ?? Array.Empty<string>())
                .Where(path => string.IsNullOrWhiteSpace(path) == false)
                .Select(VmAutomationUIToolkitAuditUtility.NormalizeAssetPath)
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
                else if (File.Exists(VmAutomationUIToolkitAuditUtility.ToFullPath(path)) == false)
                {
                    errors.Add($"UXML asset does not exist: {path}");
                }
            }

            return requested
                .Where(path => File.Exists(VmAutomationUIToolkitAuditUtility.ToFullPath(path)))
                .ToList();
        }

        internal static IEnumerable<string> SplitWhitespace(string value)
        {
            return (value ?? "").Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
        }

        internal static void AddSelfTestCase(ICollection<Dictionary<string, object>> cases, string name,
            bool passed)
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
