#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UssAuthoredElement = VMUnityAutomation.Editor.VmAutomationUssAuditContext.UssAuthoredElement;
using UssCascadeDocument = VMUnityAutomation.Editor.VmAutomationUssAuditContext.UssCascadeDocument;
using UssCascadeIndex = VMUnityAutomation.Editor.VmAutomationUssAuditContext.UssCascadeIndex;
using UssCascadeRule = VMUnityAutomation.Editor.VmAutomationUssAuditContext.UssCascadeRule;
using UssRule = VMUnityAutomation.Editor.VmAutomationUssAuditContext.UssRule;
using UssUsageIndex = VMUnityAutomation.Editor.VmAutomationUssAuditContext.UssUsageIndex;
using static VMUnityAutomation.Editor.VmAutomationUssCascadeAuditor;
using static VMUnityAutomation.Editor.VmAutomationUssStyleSheetParser;

namespace VMUnityAutomation.Editor
{
    internal static class VmAutomationUssBackgroundScaleModeAuditor
    {
        internal const string KIND = "ineffective-background-scale-mode";

        private const string ScaleModeProperty = "-unity-background-scale-mode";
        private const float RatioEpsilon = 0.0001f;

        private static readonly Regex PixelValueRegex = new Regex(
            @"^(?<value>-?(?:\d+(?:\.\d+)?|\.\d+))px$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex UrlValueRegex = new Regex(
            "^url\\(\\s*(?:\"(?<double>[^\"]+)\"|'(?<single>[^']+)'|(?<plain>[^)\\s]+))\\s*\\)$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        internal static void Audit(IReadOnlyList<UssRule> rules,
            UssUsageIndex usageIndex, UssCascadeIndex cascadeIndex,
            VmAutomationUssStyleAuditReport report, bool includeSuppressed)
        {
            Audit(rules, usageIndex, cascadeIndex, report, includeSuppressed,
                ResolveImageDimensions);
        }

        internal static IReadOnlyList<Dictionary<string, object>> RunSelfTests()
        {
            var cases = new List<Dictionary<string, object>>();

            var sameAspect = AuditFixture(
                ".icon { width: 190px; height: 190px; " +
                "-unity-background-scale-mode: scale-to-fit; }\n",
                Icon("icon", "Assets/Square.png"),
                Images(("Assets/Square.png", 190f, 190f)));
            AddCase(cases, "same-aspect fixed image makes scale-to-fit ineffective",
                HasActiveFinding(sameAspect));

            var sameAspectCrop = AuditFixture(
                ".icon { width: 95px; height: 95px; " +
                "-unity-background-scale-mode: scale-and-crop; }\n",
                Icon("icon", "Assets/Square.png"),
                Images(("Assets/Square.png", 190f, 190f)));
            AddCase(cases, "same-aspect fixed image makes scale-and-crop ineffective",
                HasActiveFinding(sameAspectCrop));

            var differentAspect = AuditFixture(
                ".icon { width: 190px; height: 190px; " +
                "-unity-background-scale-mode: scale-to-fit; }\n",
                Icon("icon", "Assets/Wide.png"),
                Images(("Assets/Wide.png", 380f, 190f)));
            AddCase(cases, "different-aspect image retains scale mode",
                HasActiveFinding(differentAspect) == false);

            var unresolvedImage = AuditFixture(
                ".icon { width: 190px; height: 190px; " +
                "-unity-background-scale-mode: scale-to-fit; }\n",
                Icon("icon", "Assets/Unknown.png"),
                Images());
            AddCase(cases, "unresolved image does not warn",
                HasActiveFinding(unresolvedImage) == false);

            var naturalSize = AuditFixture(
                ".icon { width: 190px; " +
                "-unity-background-scale-mode: scale-to-fit; }\n",
                Icon("icon", "Assets/Square.png"),
                Images(("Assets/Square.png", 190f, 190f)));
            AddCase(cases, "non-fixed image box does not warn",
                HasActiveFinding(naturalSize) == false);

            var mixedConsumers = AuditFixture(
                ".icon { width: 190px; height: 190px; " +
                "-unity-background-scale-mode: scale-to-fit; }\n",
                Icon("icon", "Assets/Square.png") +
                Icon("icon", "Assets/Wide.png"),
                Images(("Assets/Square.png", 190f, 190f),
                    ("Assets/Wide.png", 380f, 190f)));
            AddCase(cases, "one aspect-dependent consumer retains shared scale mode",
                HasActiveFinding(mixedConsumers) == false);

            var runtimeConsumer = AuditFixture(
                ".icon { width: 190px; height: 190px; " +
                "-unity-background-scale-mode: scale-to-fit; }\n",
                Icon("icon", "Assets/Square.png"),
                Images(("Assets/Square.png", 190f, 190f)), false, "icon");
            AddCase(cases, "runtime-assigned class remains conservative",
                HasActiveFinding(runtimeConsumer) == false);

            var suppressed = AuditFixture(
                "/* uss-audit: allow-redundant-declaration runtime swaps non-square art */\n" +
                ".icon { width: 190px; height: 190px; " +
                "-unity-background-scale-mode: scale-to-fit; }\n",
                Icon("icon", "Assets/Square.png"),
                Images(("Assets/Square.png", 190f, 190f)), true);
            AddCase(cases, "reasoned scale-mode suppression is retained",
                suppressed.WarningCount == 0 && suppressed.SuppressedCount == 1 &&
                suppressed.Issues.Single(issue => issue.Kind == KIND).Suppressed);

            return cases;
        }

        private static void Audit(IReadOnlyList<UssRule> rules,
            UssUsageIndex usageIndex, UssCascadeIndex cascadeIndex,
            VmAutomationUssStyleAuditReport report, bool includeSuppressed,
            Func<string, string, ImageDimensions> imageResolver)
        {
            foreach (var rule in rules)
            {
                if (rule.Declarations.TryGetValue(ScaleModeProperty,
                        out var rawScaleMode) == false)
                {
                    continue;
                }

                var scaleMode = rawScaleMode.Trim().ToLowerInvariant();
                if (scaleMode != "scale-to-fit" && scaleMode != "scale-and-crop")
                {
                    continue;
                }

                foreach (var selectorText in rule.Selectors)
                {
                    if (TryParseStaticSelector(selectorText, out var selector) == false ||
                        selector.Target.ClassNames.Any(className =>
                            usageIndex.GetRuntimeClassAssignments(className).Count > 0))
                    {
                        continue;
                    }

                    var usages = new List<ProvenUsage>();
                    var allWinningUsagesAreProven = true;
                    foreach (var document in cascadeIndex.Documents.Where(document =>
                                 document.LoadedAssetPaths.Contains(rule.AssetPath)))
                    {
                        foreach (var element in document.AuthoredDocument.Elements.Where(
                                     selector.Matches))
                        {
                            if (IsWinningScaleMode(document, element, rule,
                                    selectorText, scaleMode) == false)
                            {
                                continue;
                            }

                            if (TryProveSameAspect(document, element, imageResolver,
                                    out var usage) == false)
                            {
                                allWinningUsagesAreProven = false;
                                break;
                            }

                            usages.Add(usage);
                        }

                        if (allWinningUsagesAreProven == false)
                        {
                            break;
                        }
                    }

                    if (usages.Count == 0 || allWinningUsagesAreProven == false)
                    {
                        continue;
                    }

                    var issue = new VmAutomationUssStyleAuditIssue
                    {
                        AssetPath = rule.AssetPath,
                        Line = rule.Line,
                        Selector = selectorText,
                        Token = ScaleModeProperty,
                        Kind = KIND,
                        Property = ScaleModeProperty,
                        Value = rawScaleMode,
                        AuthoredUsageCount = usages.Count,
                        UsageLocations = usages.Select(usage => usage.ToLocation()).
                            Take(20).ToList(),
                        StylesheetRules = usages.Select(usage => usage.ToEvidence()).
                            Take(20).ToList(),
                        Suppressed = string.IsNullOrWhiteSpace(
                            rule.RedundantDeclarationSuppressionReason) == false,
                        SuppressionReason = rule.RedundantDeclarationSuppressionReason,
                        Message = $"Declaration '{ScaleModeProperty}: {rawScaleMode}' in " +
                                  $"selector '{selectorText}' cannot change the current " +
                                  "rendering: every statically authored consumer has a " +
                                  "fixed box with the same aspect ratio as its resolved " +
                                  "background image. Remove the declaration. If runtime " +
                                  "code intentionally supplies different-aspect images, " +
                                  "document that contract with a reasoned " +
                                  "allow-redundant-declaration suppression."
                    };
                    report.Record(issue, includeSuppressed);
                }
            }
        }

        private static bool IsWinningScaleMode(UssCascadeDocument document,
            UssAuthoredElement element, UssRule expectedRule, string expectedSelector,
            string expectedValue)
        {
            if (element.InlineDeclarations.ContainsKey(ScaleModeProperty))
            {
                return false;
            }

            var winner = document.Resolve(element, ScaleModeProperty, null);
            return winner != null && ReferenceEquals(winner.Rule, expectedRule) &&
                   string.Equals(winner.SelectorText, expectedSelector,
                       StringComparison.Ordinal) &&
                   string.Equals(winner.Value.Trim(), expectedValue,
                       StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryProveSameAspect(UssCascadeDocument document,
            UssAuthoredElement element,
            Func<string, string, ImageDimensions> imageResolver,
            out ProvenUsage usage)
        {
            usage = null;
            if (TryGetPositivePixels(StyleValue(document, element, "width"),
                    out var boxWidth) == false ||
                TryGetPositivePixels(StyleValue(document, element, "height"),
                    out var boxHeight) == false)
            {
                return false;
            }

            var backgroundImage = StyleValue(document, element, "background-image");
            var dimensions = imageResolver(backgroundImage,
                document.AuthoredDocument.AssetPath);
            if (dimensions == null || dimensions.Width <= 0 || dimensions.Height <= 0)
            {
                return false;
            }

            var boxRatio = boxWidth / boxHeight;
            var imageRatio = dimensions.Width / dimensions.Height;
            if (Math.Abs(boxRatio - imageRatio) > RatioEpsilon)
            {
                return false;
            }

            usage = new ProvenUsage(document.AuthoredDocument.AssetPath, element.Line,
                element.Column, boxWidth, boxHeight, dimensions);
            return true;
        }

        private static string StyleValue(UssCascadeDocument document,
            UssAuthoredElement element, string property)
        {
            if (element.InlineDeclarations.TryGetValue(property, out var inlineValue))
            {
                return inlineValue.Trim();
            }

            return document.Resolve(element, property, null)?.Value?.Trim() ?? "";
        }

        private static bool TryGetPositivePixels(string rawValue, out float value)
        {
            value = 0;
            var match = PixelValueRegex.Match((rawValue ?? "").Trim());
            return match.Success && float.TryParse(match.Groups["value"].Value,
                       NumberStyles.Float, CultureInfo.InvariantCulture, out value) &&
                   value > 0;
        }

        private static ImageDimensions ResolveImageDimensions(string rawValue,
            string ownerAssetPath)
        {
            var assetPath = ResolveImageAssetPath(rawValue, ownerAssetPath);
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                return null;
            }

            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (sprite != null && sprite.rect.width > 0 && sprite.rect.height > 0)
            {
                return new ImageDimensions(assetPath, sprite.rect.width,
                    sprite.rect.height);
            }

            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(assetPath);
            return texture != null && texture.width > 0 && texture.height > 0
                ? new ImageDimensions(assetPath, texture.width, texture.height)
                : null;
        }

        private static string ResolveImageAssetPath(string rawValue,
            string ownerAssetPath)
        {
            var match = UrlValueRegex.Match((rawValue ?? "").Trim());
            if (match.Success == false)
            {
                return "";
            }

            var rawPath = match.Groups["double"].Success
                ? match.Groups["double"].Value
                : match.Groups["single"].Success
                    ? match.Groups["single"].Value
                    : match.Groups["plain"].Value;
            return ResolveStyleReference(rawPath, ownerAssetPath);
        }

        private static string Icon(string className, string assetPath)
        {
            return $"<ui:VisualElement class=\"{className}\" " +
                   $"style=\"background-image: url(&quot;{assetPath}&quot;);\"/>";
        }

        private static Dictionary<string, ImageDimensions> Images(
            params (string path, float width, float height)[] images)
        {
            return images.ToDictionary(image => image.path,
                image => new ImageDimensions(image.path, image.width, image.height),
                StringComparer.OrdinalIgnoreCase);
        }

        private static VmAutomationUssStyleAuditReport AuditFixture(string uss,
            string uxmlBody, IReadOnlyDictionary<string, ImageDimensions> images,
            bool includeSuppressed = false, string runtimeAssignedClass = "")
        {
            const string ussPath = "Assets/__BackgroundScaleModeSelfTest.uss";
            var rules = ParseStyleSheet(ussPath, uss);
            var usageIndex = new UssUsageIndex();
            if (string.IsNullOrWhiteSpace(runtimeAssignedClass) == false)
            {
                usageIndex.AddRuntimeClassAssignment(runtimeAssignedClass,
                    "Assets/__BackgroundScaleModeSelfTest.cs", 1, 1);
            }

            var authoredDocument = new VmAutomationUssAuditContext.UssAuthoredDocument(
                "Assets/__BackgroundScaleModeSelfTest.uxml",
                System.Xml.Linq.XDocument.Parse(
                    "<ui:UXML xmlns:ui=\"UnityEngine.UIElements\">" + uxmlBody +
                    "</ui:UXML>", System.Xml.Linq.LoadOptions.SetLineInfo));
            usageIndex.Documents.Add(authoredDocument);
            var cascadeDocument = new UssCascadeDocument(authoredDocument);
            cascadeDocument.LoadedAssetPaths.Add(ussPath);
            foreach (var rule in rules)
            {
                foreach (var selectorText in rule.Selectors)
                {
                    cascadeDocument.Rules.Add(new UssCascadeRule
                    {
                        Rule = rule,
                        SelectorText = selectorText,
                        Origin = 1,
                        SourceOrder = cascadeDocument.NextSourceOrder()
                    });
                }
            }

            var cascadeIndex = new UssCascadeIndex();
            cascadeIndex.Documents.Add(cascadeDocument);
            var report = new VmAutomationUssStyleAuditReport(100);
            Audit(rules, usageIndex, cascadeIndex, report, includeSuppressed,
                (rawValue, ownerPath) =>
                {
                    var assetPath = ResolveImageAssetPath(rawValue, ownerPath);
                    return images.TryGetValue(assetPath, out var dimensions)
                        ? dimensions
                        : null;
                });
            report.SortIssues();
            return report;
        }

        private static bool HasActiveFinding(VmAutomationUssStyleAuditReport report)
        {
            return report.Issues.Any(issue => issue.Kind == KIND &&
                issue.Suppressed == false);
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

        private sealed class ImageDimensions
        {
            public readonly string AssetPath;
            public readonly float Width;
            public readonly float Height;

            public ImageDimensions(string assetPath, float width, float height)
            {
                AssetPath = assetPath;
                Width = width;
                Height = height;
            }
        }

        private sealed class ProvenUsage
        {
            private readonly string documentPath;
            private readonly int line;
            private readonly int column;
            private readonly float boxWidth;
            private readonly float boxHeight;
            private readonly ImageDimensions image;

            public ProvenUsage(string documentPath, int line, int column,
                float boxWidth, float boxHeight, ImageDimensions image)
            {
                this.documentPath = documentPath;
                this.line = line;
                this.column = column;
                this.boxWidth = boxWidth;
                this.boxHeight = boxHeight;
                this.image = image;
            }

            public Dictionary<string, object> ToLocation()
            {
                return new Dictionary<string, object>
                {
                    { "path", documentPath },
                    { "line", line },
                    { "column", column },
                    { "boxWidth", boxWidth },
                    { "boxHeight", boxHeight },
                    { "imagePath", image.AssetPath },
                    { "imageWidth", image.Width },
                    { "imageHeight", image.Height }
                };
            }

            public Dictionary<string, object> ToEvidence()
            {
                return new Dictionary<string, object>
                {
                    { "property", "background-image" },
                    { "value", image.AssetPath },
                    { "sourcePath", documentPath },
                    { "line", line },
                    { "sourceKind", "same-aspect-authored-background" },
                    { "boxSize", $"{boxWidth}x{boxHeight}" },
                    { "imageSize", $"{image.Width}x{image.Height}" }
                };
            }
        }
    }
}
#endif
