#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UssRule = VMUnityAutomation.Editor.VmAutomationUssAuditContext.UssRule;
using UssSimpleSelector = VMUnityAutomation.Editor.VmAutomationUssAuditContext.UssSimpleSelector;
using UssAuthoredElement = VMUnityAutomation.Editor.VmAutomationUssAuditContext.UssAuthoredElement;
using UssAuthoredElementUsage = VMUnityAutomation.Editor.VmAutomationUssAuditContext.UssAuthoredElementUsage;
using UssAuthoredDocument = VMUnityAutomation.Editor.VmAutomationUssAuditContext.UssAuthoredDocument;
using UssCascadeRule = VMUnityAutomation.Editor.VmAutomationUssAuditContext.UssCascadeRule;
using UssResolvedDeclaration = VMUnityAutomation.Editor.VmAutomationUssAuditContext.UssResolvedDeclaration;
using UssCascadeDocument = VMUnityAutomation.Editor.VmAutomationUssAuditContext.UssCascadeDocument;
using UssCascadeIndex = VMUnityAutomation.Editor.VmAutomationUssAuditContext.UssCascadeIndex;
using UssUsageIndex = VMUnityAutomation.Editor.VmAutomationUssAuditContext.UssUsageIndex;
using static VMUnityAutomation.Editor.VmAutomationUssStyleSheetParser;
using static VMUnityAutomation.Editor.VmAutomationUssCascadeAuditor;

namespace VMUnityAutomation.Editor
{
    internal static class VmAutomationUssStyleAuditor
    {
        internal const string SUPPRESSION_MARKER = "uss-audit: allow-single-use";
        internal const string REDUNDANT_DECLARATION_SUPPRESSION_MARKER =
            "uss-audit: allow-redundant-declaration";
        internal const string ANCESTOR_DEFAULT_RESET_SUPPRESSION_MARKER =
            "uss-audit: allow-ancestor-default-reset";
        internal const string PIXEL_GRID_SUPPRESSION_MARKER =
            "uss-audit: allow-off-grid-pixels";
        internal const string TEXT_STYLE_CONTRACT_SUPPRESSION_MARKER =
            "uss-audit: allow-text-style-contract";

        private static readonly Regex panelThemeGuidRegex =
            new Regex(@"^\s*themeUss:\s*\{[^}\r\n]*\bguid:\s*(?<guid>[0-9a-fA-F]{32})\b[^}\r\n]*\}",
                RegexOptions.Compiled | RegexOptions.Multiline);

        private static readonly Regex simpleClassSelectorRegex =
            new Regex(@"^\.(?<token>[A-Za-z_][A-Za-z0-9_-]*)$", RegexOptions.Compiled);

        private static readonly Regex simpleIdSelectorRegex =
            new Regex(@"^#(?<token>[A-Za-z_][A-Za-z0-9_-]*)$", RegexOptions.Compiled);

        private static readonly Regex classTokenRegex =
            new Regex(@"(?<![A-Za-z0-9_-])\.(?<token>[A-Za-z_][A-Za-z0-9_-]*)",
                RegexOptions.Compiled);

        private static readonly Regex idTokenRegex =
            new Regex(@"(?<![A-Za-z0-9_-])#(?<token>[A-Za-z_][A-Za-z0-9_-]*)",
                RegexOptions.Compiled);

        private static readonly Regex relationalAnchorClassTokenRegex =
            new Regex(@"(?<![A-Za-z0-9_-])\.(?<token>[A-Za-z_][A-Za-z0-9_-]*)(?=\s|[>+~])",
                RegexOptions.Compiled);

        private static readonly Regex relationalTargetClassTokenRegex =
            new Regex(@"(?<![A-Za-z0-9_-])\.(?<token>[A-Za-z_][A-Za-z0-9_-]*)\s*$",
                RegexOptions.Compiled);

        private static readonly Regex relationalTargetIdTokenRegex =
            new Regex(@"(?<![A-Za-z0-9_-])#(?<token>[A-Za-z_][A-Za-z0-9_-]*)\s*$",
                RegexOptions.Compiled);

        private static readonly Regex quotedTokenRegex =
            new Regex(@"[""'](?<token>[A-Za-z_][A-Za-z0-9_-]*)[""']",
                RegexOptions.Compiled);

        private static readonly Regex yamlListTokenRegex =
            new Regex(@"^\s*-\s*(?<token>[A-Za-z_][A-Za-z0-9_-]*)\s*$",
                RegexOptions.Compiled | RegexOptions.Multiline);

        private static readonly string[] runtimeClassApiMarkers =
        {
            "AddToClassList",
            "RemoveFromClassList",
            "EnableInClassList",
            "ClassListContains",
            "classList",
            "ussClassName",
            "UssClassName"
        };

        private static readonly string[] inheritedTextStyleProperties =
        {
            "color",
            "font-size",
            "-unity-font",
            "-unity-font-definition",
            "-unity-font-style",
            "white-space",
            "letter-spacing",
            "word-spacing",
            "-unity-paragraph-spacing",
            "-unity-text-outline-color",
            "-unity-text-outline-width"
        };

        internal static VmAutomationUssStyleAuditReport Audit(IEnumerable<string> requestedPaths,
            bool includeSuppressed, int maxIssues, VmAutomationUIToolkitAuditOptions options)
        {
            options = options ?? VmAutomationUIToolkitAuditOptions.FromArguments(
                new Dictionary<string, object>());
            var report = new VmAutomationUssStyleAuditReport(maxIssues);
            var requestedPathList = (requestedPaths ?? Array.Empty<string>())
                .Where(path => string.IsNullOrWhiteSpace(path) == false).ToList();
            var requested = NormalizeRequestedPaths(requestedPathList, report.Errors);
            var commonThemePath = FindCommonPanelThemePath(options);
            var commonThemeStylePaths = EnumerateImportedStylePaths(commonThemePath,
                report.Errors);
            var allStyleSheetPaths = VmAutomationUIToolkitAuditUtility.FindAssetFiles(".uss", options)
                .Concat(requested)
                .Concat(commonThemeStylePaths)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToList();
            var targetPaths = requestedPathList.Count == 0 ? allStyleSheetPaths : requested;
            report.ScannedStyleSheetCount = targetPaths.Count;
            report.IndexedStyleSheetCount = allStyleSheetPaths.Count;

            var rulesByPath = new Dictionary<string, List<UssRule>>(StringComparer.OrdinalIgnoreCase);
            foreach (var path in allStyleSheetPaths)
            {
                try
                {
                    rulesByPath[path] = ParseStyleSheet(path,
                        File.ReadAllText(VmAutomationUIToolkitAuditUtility.ToFullPath(path)));
                }
                catch (Exception exception)
                {
                    report.Errors.Add($"Failed to read '{path}': {exception.Message}");
                }
            }

            var usageIndex = BuildUsageIndex(rulesByPath.Values.SelectMany(rules => rules), options);
            var cascadeIndex = BuildCascadeIndex(commonThemePath, rulesByPath, usageIndex,
                report.Errors);
            report.IndexedUxmlCount = usageIndex.IndexedUxmlCount;
            report.IndexedRuntimeSourceCount = usageIndex.IndexedRuntimeSourceCount;

            foreach (var path in targetPaths)
            {
                if (rulesByPath.TryGetValue(path, out var rules) == false)
                {
                    if (File.Exists(VmAutomationUIToolkitAuditUtility.ToFullPath(path)) == false)
                    {
                        report.Errors.Add($"USS asset does not exist: {path}");
                    }

                    continue;
                }

                AuditPixelGridDeclarations(rules, options, report, includeSuppressed);
                AuditTextStyleContracts(rules, usageIndex, cascadeIndex, report,
                    includeSuppressed);
                AuditRules(rules, usageIndex, report, includeSuppressed);
                AuditRedundantDeclarations(rules, usageIndex, cascadeIndex, report,
                    includeSuppressed);
                VmAutomationUssSharedClassDeclarationAuditor.Audit(rules, usageIndex,
                    report);
                VmAutomationUssGeneratedChildNaturalSizeAuditor.Audit(rules, usageIndex,
                    cascadeIndex, report, includeSuppressed);
                VmAutomationUssAncestorDefaultResetAuditor.Audit(rules, usageIndex,
                    cascadeIndex, report, includeSuppressed);
            }

            report.SortIssues();
            return report;
        }

        internal static Dictionary<string, object> RunSelfTests()
        {
            const string path = "Assets/__UssAuditSelfTest.uss";
            const string text =
                ".single { width: 10px; }\n" +
                "#Unique { height: 10px; }\n" +
                "#IdContainer { width: 20px; }\n" +
                "#IdContainer .generated { height: 10px; }\n" +
                "#Parent #UniqueChild { margin-left: 10px; }\n" +
                "#interactive-id { color: white; }\n" +
                "#interactive-id:hover { color: red; }\n" +
                ".shared { color: white; }\n" +
                ".interactive { color: white; }\n" +
                ".interactive:hover { color: red; }\n" +
                ".runtime-state { display: none; }\n" +
                ".container { width: 20px; }\n" +
                ".container .child { width: 10px; }\n" +
                "/* uss-audit: allow-single-use fixture requires authored semantic state */\n" +
                ".suppressed { opacity: 0.5; }\n" +
                ".unused { width: 1px; }\n";

            var rules = ParseStyleSheet(path, text);
            var index = new UssUsageIndex();
            CollectSelectorContracts(rules, index);
            index.AddClassUsage("single", "Assets/Single.uxml", 1);
            index.AddIdUsage("Unique", "Assets/Single.uxml", 2);
            index.AddIdUsage("IdContainer", "Assets/IdContainer.uxml", 1);
            index.AddIdUsage("Parent", "Assets/Parent.uxml", 1);
            index.AddIdUsage("UniqueChild", "Assets/Parent.uxml", 2);
            index.AddIdUsage("interactive-id", "Assets/InteractiveId.uxml", 1);
            index.AddClassUsage("shared", "Assets/SharedA.uxml", 1);
            index.AddClassUsage("shared", "Assets/SharedB.uxml", 1);
            index.AddClassUsage("interactive", "Assets/Interactive.uxml", 1);
            index.AddClassUsage("runtime-state", "Assets/Runtime.uxml", 1);
            index.AddRuntimeClassReference("runtime-state", "Assets/Scripts/Runtime.cs", 4);
            index.AddClassUsage("container", "Assets/Container.uxml", 1, "Container");
            index.AddClassUsage("child", "Assets/Container.uxml", 2);
            index.AddClassUsage("suppressed", "Assets/Suppressed.uxml", 1);

            var report = new VmAutomationUssStyleAuditReport(100)
            {
                ScannedStyleSheetCount = 1,
                IndexedStyleSheetCount = 1
            };
            AuditRules(rules, index, report, true);
            report.SortIssues();

            const string themePath = "Assets/__UssAuditSelfTestTheme.uss";
            const string duplicatePath = "Assets/__UssAuditSelfTestDuplicate.uss";
            var themeRules = ParseStyleSheet(themePath,
                "* { -unity-slice-scale: 3px; }\n");
            var duplicateRules = ParseStyleSheet(duplicatePath,
                ".duplicate { -unity-slice-scale: 3px; }\n" +
                ".different { -unity-slice-scale: 2px; }\n" +
                ".initial-default { position: relative; }\n" +
                ".initial-margin { margin-top: 0; }\n" +
                "/* uss-audit: allow-redundant-declaration fixture documents ownership */\n" +
                ".suppressed-duplicate { -unity-slice-scale: 3px; }\n");
            var duplicateUsageIndex = new UssUsageIndex();
            CollectSelectorContracts(duplicateRules, duplicateUsageIndex);
            var duplicateDocument = new UssAuthoredDocument("Assets/Duplicate.uxml",
                XDocument.Parse(
                    "<ui:UXML xmlns:ui=\"UnityEngine.UIElements\">" +
                    "<ui:VisualElement class=\"duplicate\"/>" +
                    "<ui:VisualElement class=\"different\"/>" +
                    "<ui:VisualElement class=\"initial-default\"/>" +
                    "<ui:VisualElement class=\"initial-margin\"/>" +
                    "<ui:VisualElement class=\"suppressed-duplicate\"/>" +
                    "</ui:UXML>", LoadOptions.SetLineInfo));
            duplicateUsageIndex.Documents.Add(duplicateDocument);
            var duplicateCascade = new UssCascadeIndex();
            var duplicateCascadeDocument = new UssCascadeDocument(duplicateDocument);
            AppendSelfTestRules(duplicateCascadeDocument, themeRules, 0);
            AppendSelfTestRules(duplicateCascadeDocument, duplicateRules, 1);
            duplicateCascade.Documents.Add(duplicateCascadeDocument);
            var duplicateReport = new VmAutomationUssStyleAuditReport(100)
            {
                ScannedStyleSheetCount = 1,
                IndexedStyleSheetCount = 2,
                IndexedUxmlCount = 1
            };
            AuditRedundantDeclarations(duplicateRules, duplicateUsageIndex, duplicateCascade,
                duplicateReport, true);
            duplicateReport.SortIssues();

            var pixelGridRules = ParseStyleSheet(path,
                ".grid-pass { left: -6px; margin-right: 9px; padding: 3px 6px; }\n" +
                ".grid-fail { top: 4px; padding: 3px 7px; }\n" +
                ".grid-non-pixel { left: 50%; font-size: 7px; }\n" +
                "/* uss-audit: allow-off-grid-pixels fixture documents optical alignment */\n" +
                ".grid-suppressed { margin-left: 1px; }\n");
            var pixelGridOptions = VmAutomationUIToolkitAuditOptions.FromArguments(
                new Dictionary<string, object>
                {
                    { "useProjectSettings", false },
                    { "pixelGridEnabled", true },
                    { "pixelGridStep", 3 }
                });
            var pixelGridReport = new VmAutomationUssStyleAuditReport(100);
            AuditPixelGridDeclarations(pixelGridRules, pixelGridOptions, pixelGridReport, true);
            pixelGridReport.SortIssues();

            const string textContractPath = "Assets/__UssAuditSelfTestTextContracts.uss";
            var textContractRules = ParseStyleSheet(textContractPath,
                ".centered-text-owner { align-items: center; justify-content: center; }\n" +
                ".problem-text { color: white; font-size: 18px; -unity-font-style: bold; " +
                "-unity-text-generator: advanced; -unity-text-align: middle-center; }\n" +
                ".auto-sized-text { -unity-text-generator: advanced; " +
                "-unity-text-auto-size: best-fit 8px 18px; }\n" +
                ".boxed-text { width: 30px; -unity-text-align: middle-center; }\n" +
                ".sibling-text { color: white; }\n" +
                "/* uss-audit: allow-text-style-contract fixture documents advanced shaping */\n" +
                ".suppressed-text { -unity-text-generator: advanced; }\n");
            var textContractUsageIndex = new UssUsageIndex();
            CollectSelectorContracts(textContractRules, textContractUsageIndex);
            var textContractDocument = new UssAuthoredDocument(
                "Assets/TextContracts.uxml",
                XDocument.Parse(
                    "<ui:UXML xmlns:ui=\"UnityEngine.UIElements\">" +
                    "<ui:VisualElement class=\"centered-text-owner\">" +
                    "<ui:Label class=\"problem-text\" text=\"1\"/>" +
                    "</ui:VisualElement>" +
                    "<ui:VisualElement><ui:Label class=\"auto-sized-text\" text=\"Auto\"/>" +
                    "</ui:VisualElement>" +
                    "<ui:VisualElement class=\"centered-text-owner\">" +
                    "<ui:Label class=\"boxed-text\" text=\"Box\"/>" +
                    "</ui:VisualElement>" +
                    "<ui:VisualElement><ui:Label class=\"sibling-text\" text=\"Sibling\"/>" +
                    "<ui:VisualElement/></ui:VisualElement>" +
                    "<ui:VisualElement class=\"centered-text-owner\">" +
                    "<ui:Label class=\"suppressed-text\" text=\"Suppressed\"/>" +
                    "</ui:VisualElement>" +
                    "</ui:UXML>", LoadOptions.SetLineInfo));
            textContractUsageIndex.Documents.Add(textContractDocument);
            var textContractCascade = new UssCascadeIndex();
            var textContractCascadeDocument =
                new UssCascadeDocument(textContractDocument);
            AppendSelfTestRules(textContractCascadeDocument, textContractRules, 1);
            textContractCascade.Documents.Add(textContractCascadeDocument);
            var textContractReport = new VmAutomationUssStyleAuditReport(100);
            AuditTextStyleContracts(textContractRules, textContractUsageIndex,
                textContractCascade, textContractReport, true);
            textContractReport.SortIssues();

            var activeTokens = report.Issues.Where(issue => issue.Suppressed == false)
                .Select(issue => issue.Token).OrderBy(token => token, StringComparer.Ordinal).ToArray();
            var suppressedTokens = report.Issues.Where(issue => issue.Suppressed)
                .Select(issue => issue.Token).OrderBy(token => token, StringComparer.Ordinal).ToArray();
            var activeRedundantSelectors = duplicateReport.Issues
                .Where(issue => issue.Suppressed == false)
                .Select(issue => issue.Selector)
                .OrderBy(selector => selector, StringComparer.Ordinal)
                .ToArray();
            var suppressedRedundantSelectors = duplicateReport.Issues
                .Where(issue => issue.Suppressed)
                .Select(issue => issue.Selector)
                .OrderBy(selector => selector, StringComparer.Ordinal)
                .ToArray();
            var activePixelGridSelectors = pixelGridReport.Issues
                .Where(issue => issue.Suppressed == false)
                .Select(issue => issue.Selector)
                .OrderBy(selector => selector, StringComparer.Ordinal)
                .ToArray();
            var suppressedPixelGridSelectors = pixelGridReport.Issues
                .Where(issue => issue.Suppressed)
                .Select(issue => issue.Selector)
                .OrderBy(selector => selector, StringComparer.Ordinal)
                .ToArray();
            var activeTextContractKinds = textContractReport.Issues
                .Where(issue => issue.Suppressed == false)
                .Select(issue => issue.Kind)
                .OrderBy(kind => kind, StringComparer.Ordinal)
                .ToArray();
            var suppressedTextContractKinds = textContractReport.Issues
                .Where(issue => issue.Suppressed)
                .Select(issue => issue.Kind)
                .OrderBy(kind => kind, StringComparer.Ordinal)
                .ToArray();
            var cases = new List<Dictionary<string, object>>();
            cases.AddRange(VmAutomationUssAncestorDefaultResetAuditor.RunSelfTests());
            cases.AddRange(VmAutomationUssGeneratedChildNaturalSizeAuditor.RunSelfTests());
            cases.AddRange(VmAutomationUssGeneratedChildStyleOwnershipAuditor.RunSelfTests());
            cases.AddRange(VmAutomationUssVariantFamilyContract.RunSelfTests());
            cases.AddRange(VmAutomationUssRedundantComponentClassAuditor.RunSelfTests());
            cases.AddRange(VmAutomationUssSharedClassDeclarationAuditor.RunSelfTests());

            AddSelfTestCase(cases, "single class warns", activeTokens.Contains("single"));
            AddSelfTestCase(cases, "single ID warns", activeTokens.Contains("Unique"));
            AddSelfTestCase(cases, "simple ID with relational contract still warns",
                activeTokens.Contains("IdContainer"));
            AddSelfTestCase(cases, "single relational ID target warns",
                activeTokens.Contains("UniqueChild"));
            AddSelfTestCase(cases, "pseudo ID contract passes",
                activeTokens.Contains("interactive-id") == false);
            AddSelfTestCase(cases, "zero-consumer selector is outside the single-use gate",
                activeTokens.Contains("unused") == false);
            AddSelfTestCase(cases, "shared class passes", activeTokens.Contains("shared") == false);
            AddSelfTestCase(cases, "pseudo contract passes", activeTokens.Contains("interactive") == false);
            AddSelfTestCase(cases, "runtime class passes", activeTokens.Contains("runtime-state") == false);
            AddSelfTestCase(cases, "single named relational anchor warns", activeTokens.Contains("container"));
            AddSelfTestCase(cases, "single relational target warns", activeTokens.Contains("child"));
            AddSelfTestCase(cases, "reasoned suppression is reported as suppressed",
                suppressedTokens.SequenceEqual(new[] { "suppressed" }));
            AddSelfTestCase(cases, "active finding set is exact",
                activeTokens.SequenceEqual(
                    new[] { "IdContainer", "Unique", "UniqueChild", "child", "container", "single" }));
            AddSelfTestCase(cases, "same winning theme value warns",
                activeRedundantSelectors.SequenceEqual(
                    new[] { ".duplicate", ".initial-default", ".initial-margin" }));
            AddSelfTestCase(cases, "Unity engine initial value warns",
                duplicateReport.Issues.Any(issue =>
                    issue.Selector == ".initial-default" &&
                    issue.StylesheetRules.Any(source =>
                        source.TryGetValue("sourceKind", out var sourceKind) &&
                        Equals(sourceKind, "initial-style"))));
            AddSelfTestCase(cases, "engine initial margin warns",
                duplicateReport.Issues.Any(issue =>
                    issue.Selector == ".initial-margin" &&
                    issue.Property == "margin-top" &&
                    issue.StylesheetRules.Any(source =>
                        source.TryGetValue("sourceKind", out var sourceKind) &&
                        Equals(sourceKind, "initial-style"))));
            AddSelfTestCase(cases, "different-value override passes",
                activeRedundantSelectors.Contains(".different") == false);
            AddSelfTestCase(cases, "reasoned redundant-declaration suppression is retained",
                suppressedRedundantSelectors.SequenceEqual(new[] { ".suppressed-duplicate" }));
            AddSelfTestCase(cases, "only off-grid structural declarations warn",
                activePixelGridSelectors.SequenceEqual(new[] { ".grid-fail" }));
            AddSelfTestCase(cases, "off-grid shorthand values are retained",
                pixelGridReport.Issues.Single(issue => issue.Suppressed == false)
                    .OffGridDeclarations.Keys.OrderBy(value => value, StringComparer.Ordinal)
                    .SequenceEqual(new[] { "padding", "top" }));
            AddSelfTestCase(cases, "reasoned pixel-grid suppression is retained",
                suppressedPixelGridSelectors.SequenceEqual(new[] { ".grid-suppressed" }));
            AddSelfTestCase(cases,
                "advanced generator without auto size warns independently",
                activeTextContractKinds.Contains(
                    "advanced-text-generator-without-auto-size"));
            AddSelfTestCase(cases,
                "text align on shrink-wrapped centered label warns independently",
                activeTextContractKinds.Contains(
                    "ineffective-text-align-on-shrink-wrapped-label"));
            AddSelfTestCase(cases,
                "inheritable only-child text styles warn independently",
                activeTextContractKinds.Contains(
                    "inheritable-text-style-on-only-child-label"));
            AddSelfTestCase(cases, "text contract active finding set is exact",
                activeTextContractKinds.SequenceEqual(new[]
                {
                    "advanced-text-generator-without-auto-size",
                    "ineffective-text-align-on-shrink-wrapped-label",
                    "inheritable-text-style-on-only-child-label"
                }));
            AddSelfTestCase(cases,
                "advanced generator with auto size passes",
                textContractReport.Issues.All(issue =>
                    issue.Selector != ".auto-sized-text"));
            AddSelfTestCase(cases,
                "text align with an explicit box passes",
                textContractReport.Issues.All(issue =>
                    issue.Selector != ".boxed-text"));
            AddSelfTestCase(cases,
                "inheritable text style with a sibling passes",
                textContractReport.Issues.All(issue =>
                    issue.Selector != ".sibling-text"));
            AddSelfTestCase(cases,
                "reasoned text style contract suppression is retained",
                suppressedTextContractKinds.SequenceEqual(new[]
                {
                    "advanced-text-generator-without-auto-size"
                }));

            return new Dictionary<string, object>
            {
                { "passed", cases.All(testCase => (bool)testCase["passed"]) },
                { "cases", cases },
                { "activeTokens", activeTokens },
                { "suppressedTokens", suppressedTokens },
                { "activeRedundantSelectors", activeRedundantSelectors },
                { "suppressedRedundantSelectors", suppressedRedundantSelectors },
                { "activePixelGridSelectors", activePixelGridSelectors },
                { "suppressedPixelGridSelectors", suppressedPixelGridSelectors },
                { "activeTextContractKinds", activeTextContractKinds },
                { "suppressedTextContractKinds", suppressedTextContractKinds }
            };
        }

        private static void AppendSelfTestRules(UssCascadeDocument document,
            IEnumerable<UssRule> rules, int origin)
        {
            foreach (var rule in rules)
            {
                document.LoadedAssetPaths.Add(rule.AssetPath);
                foreach (var selectorText in rule.Selectors)
                {
                    TryParseSimpleSelector(selectorText, out var selector);
                    document.Rules.Add(new UssCascadeRule
                    {
                        Rule = rule,
                        SelectorText = selectorText,
                        Selector = selector,
                        Origin = origin,
                        SourceOrder = document.NextSourceOrder()
                    });
                }
            }
        }

        private static UssUsageIndex BuildUsageIndex(IEnumerable<UssRule> allRules,
            VmAutomationUIToolkitAuditOptions options)
        {
            var index = new UssUsageIndex();
            var rules = allRules.ToList();
            CollectSelectorContracts(rules, index);
            IndexUxmlUsage(index, options);
            IndexRuntimeClassReferences(index, options);
            return index;
        }

        private static void CollectSelectorContracts(IEnumerable<UssRule> rules, UssUsageIndex index)
        {
            foreach (var rule in rules)
            {
                foreach (var selector in rule.Selectors)
                {
                    var simpleClass = simpleClassSelectorRegex.Match(selector);
                    var simpleId = simpleIdSelectorRegex.Match(selector);
                    foreach (Match match in classTokenRegex.Matches(selector))
                    {
                        var token = match.Groups["token"].Value;
                        index.AllClassTokens.Add(token);
                        if (simpleClass.Success == false)
                        {
                            index.ComplexClassTokens.Add(token);
                        }
                    }

                    foreach (Match match in idTokenRegex.Matches(selector))
                    {
                        var token = match.Groups["token"].Value;
                        index.AllIdTokens.Add(token);
                        var tokenEnd = match.Index + match.Length;
                        if (tokenEnd < selector.Length && selector[tokenEnd] == ':')
                        {
                            index.PseudoIdTokens.Add(token);
                        }

                        if (simpleId.Success == false)
                        {
                            index.ComplexIdTokens.Add(token);
                        }
                    }
                }
            }
        }

        private static void IndexUxmlUsage(UssUsageIndex index, VmAutomationUIToolkitAuditOptions options)
        {
            foreach (var path in VmAutomationUIToolkitAuditUtility.FindAssetFiles(".uxml", options))
            {
                try
                {
                    string text = File.ReadAllText(VmAutomationUIToolkitAuditUtility.ToFullPath(path));
                    var document = XDocument.Parse(text,
                        LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo);
                    index.IndexedUxmlCount++;
                    index.AddDocument(path, document);
                    foreach (XElement element in document.Descendants())
                    {
                        XAttribute classAttribute = GetAttribute(element, "class");
                        if (classAttribute != null)
                        {
                            string elementName = GetAttributeValue(element, "name");
                            foreach (string token in SplitWhitespace(classAttribute.Value))
                            {
                                if (index.AllClassTokens.Contains(token))
                                {
                                    index.AddClassUsage(token, path,
                                        GetLineNumber(classAttribute),
                                        GetColumnNumber(classAttribute), elementName);
                                }
                            }
                        }

                        XAttribute nameAttribute = GetAttribute(element, "name");
                        if (nameAttribute == null)
                            continue;

                        string name = nameAttribute.Value.Trim();
                        if (index.AllIdTokens.Contains(name))
                        {
                            index.AddIdUsage(name, path,
                                GetLineNumber(nameAttribute),
                                GetColumnNumber(nameAttribute));
                        }
                    }
                }
                catch
                {
                    continue;
                }
            }
        }

        private static void IndexRuntimeClassReferences(UssUsageIndex index,
            VmAutomationUIToolkitAuditOptions options)
        {
            foreach (var path in VmAutomationUIToolkitAuditUtility.FindRuntimeSourceFiles(options))
            {
                string text;
                try
                {
                    text = File.ReadAllText(VmAutomationUIToolkitAuditUtility.ToFullPath(path));
                }
                catch
                {
                    continue;
                }

                if (runtimeClassApiMarkers.Any(marker =>
                        text.Contains(marker, StringComparison.Ordinal)) == false)
                {
                    continue;
                }

                index.IndexedRuntimeSourceCount++;
                foreach (Match match in quotedTokenRegex.Matches(text).Cast<Match>()
                             .Concat(yamlListTokenRegex.Matches(text).Cast<Match>()))
                {
                    var token = match.Groups["token"].Value;
                    if (index.AllClassTokens.Contains(token))
                    {
                        index.AddRuntimeClassReference(token, path,
                            GetLineNumber(text, match.Index),
                            GetColumnNumber(text, match.Index));
                    }
                }

                VmAutomationUssRuntimeClassReferenceClassifier.Index(path, text, index);
            }
        }

        private static string FindCommonPanelThemePath(VmAutomationUIToolkitAuditOptions options)
        {
            var themePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var assetPath in VmAutomationUIToolkitAuditUtility.FindAssetFiles(".asset", options))
            {
                string text;
                try
                {
                    text = File.ReadAllText(VmAutomationUIToolkitAuditUtility.ToFullPath(assetPath));
                }
                catch
                {
                    continue;
                }

                if (text.Contains("UnityEngine.UIElements.PanelSettings",
                        StringComparison.Ordinal) == false)
                {
                    continue;
                }

                var match = panelThemeGuidRegex.Match(text);
                if (match.Success == false)
                {
                    continue;
                }

                var themePath = AssetDatabase.GUIDToAssetPath(match.Groups["guid"].Value);
                if (string.IsNullOrWhiteSpace(themePath) == false)
                {
                    themePaths.Add(VmAutomationUIToolkitAuditUtility.NormalizeAssetPath(themePath));
                }
            }

            return themePaths.Count == 1 ? themePaths.Single() : "";
        }

        private static IReadOnlyCollection<string> EnumerateImportedStylePaths(string rootPath,
            ICollection<string> errors)
        {
            var stylePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            CollectImportedStylePaths(rootPath, stylePaths, visited, errors);
            return stylePaths;
        }

        private static void CollectImportedStylePaths(string assetPath,
            ISet<string> stylePaths, ISet<string> visited, ICollection<string> errors)
        {
            if (string.IsNullOrWhiteSpace(assetPath) || visited.Add(assetPath) == false)
            {
                return;
            }

            string text;
            try
            {
                text = File.ReadAllText(VmAutomationUIToolkitAuditUtility.ToFullPath(assetPath));
            }
            catch (Exception exception)
            {
                errors.Add($"Failed to read theme stylesheet '{assetPath}': {exception.Message}");
                return;
            }

            foreach (var importPath in GetImportedStylePaths(assetPath, text))
            {
                if (importPath.EndsWith(".uss", StringComparison.OrdinalIgnoreCase))
                {
                    stylePaths.Add(importPath);
                }

                CollectImportedStylePaths(importPath, stylePaths, visited, errors);
            }
        }

        private static void AuditRules(IEnumerable<UssRule> rules, UssUsageIndex usageIndex,
            VmAutomationUssStyleAuditReport report, bool includeSuppressed)
        {
            var ruleList = rules.ToList();
            var variantFamilyTokens =
                VmAutomationUssVariantFamilyContract.FindContractTokens(ruleList, usageIndex);
            VmAutomationUssRedundantComponentClassAuditor.Audit(ruleList, usageIndex,
                report, includeSuppressed);
            VmAutomationUssGeneratedChildStyleOwnershipAuditor.Audit(ruleList, usageIndex,
                report, includeSuppressed);
            AuditRelationalSelectorContracts(ruleList, usageIndex, report, includeSuppressed);

            foreach (var rule in ruleList)
            {
                foreach (var selector in rule.Selectors)
                {
                    var classMatch = simpleClassSelectorRegex.Match(selector);
                    if (classMatch.Success)
                    {
                        var token = classMatch.Groups["token"].Value;
                        if (usageIndex.ComplexClassTokens.Contains(token))
                        {
                            continue;
                        }

                        var authored = usageIndex.GetClassUsages(token);
                        var runtime = usageIndex.GetRuntimeClassReferences(token);
                        if (authored.Count == 1 && runtime.Count == 0 &&
                            variantFamilyTokens.Contains(token) == false)
                        {
                            AddIssue(report, rule, selector, token, "single-use-class", authored, runtime,
                                $"Class selector '{selector}' serves one authored UXML element and has no pseudo, " +
                                "relational, or runtime class contract. Move its declarations to that element's inline style.",
                                includeSuppressed);
                        }

                        continue;
                    }

                    var idMatch = simpleIdSelectorRegex.Match(selector);
                    if (idMatch.Success == false)
                    {
                        continue;
                    }

                    var idToken = idMatch.Groups["token"].Value;
                    if (usageIndex.PseudoIdTokens.Contains(idToken))
                    {
                        continue;
                    }

                    var idUsages = usageIndex.GetIdUsages(idToken);
                    if (idUsages.Count == 1)
                    {
                        AddIssue(report, rule, selector, idToken, "single-use-id", idUsages,
                            Array.Empty<UssUsageLocation>(),
                            $"ID selector '{selector}' serves one authored UXML element and has no direct " +
                            "pseudo-state contract. Move its ordinary declarations to that element's inline style; " +
                            "relational use of the same ID does not justify a separate simple selector block.",
                            includeSuppressed);
                    }
                }
            }
        }

        private static void AuditPixelGridDeclarations(IEnumerable<UssRule> rules,
            VmAutomationUIToolkitAuditOptions options, VmAutomationUssStyleAuditReport report,
            bool includeSuppressed)
        {
            if (options.PixelGridEnabled == false)
                return;

            foreach (var rule in rules)
            {
                var offGridDeclarations =
                    VmAutomationUIToolkitPixelGridAuditUtility.FindOffGridDeclarations(
                        rule.Declarations, options.PixelGridStep);
                if (offGridDeclarations.Count == 0)
                    continue;

                var orderedProperties = offGridDeclarations.Keys
                    .OrderBy(property => property, StringComparer.Ordinal)
                    .ToList();
                var selector = string.Join(", ", rule.Selectors);
                var suppressionReason = rule.PixelGridSuppressionReason;
                var issue = new VmAutomationUssStyleAuditIssue
                {
                    AssetPath = rule.AssetPath,
                    Line = rule.Line,
                    Selector = selector,
                    Token = string.Join(", ", orderedProperties),
                    Kind = "off-grid-pixel-declarations",
                    GridStep = options.PixelGridStep,
                    OffGridDeclarations = offGridDeclarations,
                    Suppressed = string.IsNullOrWhiteSpace(suppressionReason) == false,
                    SuppressionReason = suppressionReason,
                    Message =
                        $"Selector '{selector}' has structural offset, spacing, or padding " +
                        $"declarations outside the configured {options.PixelGridStep}px grid: " +
                        $"{string.Join(", ", orderedProperties)}. Align them to the project grid " +
                        "or add a reasoned suppression for a measured optical or seam correction."
                };
                report.Record(issue, includeSuppressed);
            }
        }

        private static void AuditTextStyleContracts(IReadOnlyList<UssRule> rules,
            UssUsageIndex usageIndex, UssCascadeIndex cascadeIndex,
            VmAutomationUssStyleAuditReport report, bool includeSuppressed)
        {
            foreach (var rule in rules)
            {
                if (TryGetSupportedTextContractSelectors(rule, out var selectors) == false)
                {
                    continue;
                }

                AuditAdvancedTextGenerator(rule, selectors, usageIndex, cascadeIndex,
                    report, includeSuppressed);
                AuditShrinkWrappedTextAlignment(rule, selectors, usageIndex, cascadeIndex,
                    report, includeSuppressed);
                AuditInheritableOnlyChildTextStyles(rule, selectors, usageIndex,
                    cascadeIndex, report, includeSuppressed);
            }
        }

        private static void AuditAdvancedTextGenerator(UssRule rule,
            IReadOnlyCollection<UssSimpleSelector> selectors, UssUsageIndex usageIndex,
            UssCascadeIndex cascadeIndex, VmAutomationUssStyleAuditReport report,
            bool includeSuppressed)
        {
            const string property = "-unity-text-generator";
            if (rule.Declarations.TryGetValue(property, out var value) == false ||
                StyleValuesEqual(value, "advanced") == false)
            {
                return;
            }

            var usages = FindWinningElementUsages(rule, property, selectors, cascadeIndex)
                .Where(usage => IsAuthoredTextElement(usage.Element))
                .Where(usage => IsTextAutoSizeEnabled(
                    ResolveEffectiveTextStyle(usage.Document, usage.Element,
                        "-unity-text-auto-size")) == false)
                .ToList();
            if (usages.Count == 0)
            {
                return;
            }

            RecordTextStyleContractIssue(rule, usageIndex, report, includeSuppressed,
                usages, property, value, "advanced-text-generator-without-auto-size",
                $"Selector '{string.Join(", ", rule.Selectors)}' enables the advanced text " +
                $"generator for {usages.Count} authored Label element(s) without effective " +
                "-unity-text-auto-size. Keep the default generator unless auto sizing or another " +
                "documented advanced-text requirement owns this setting.");
        }

        private static void AuditShrinkWrappedTextAlignment(UssRule rule,
            IReadOnlyCollection<UssSimpleSelector> selectors, UssUsageIndex usageIndex,
            UssCascadeIndex cascadeIndex, VmAutomationUssStyleAuditReport report,
            bool includeSuppressed)
        {
            const string property = "-unity-text-align";
            if (rule.Declarations.TryGetValue(property, out var value) == false)
            {
                return;
            }

            var usages = FindWinningElementUsages(rule, property, selectors, cascadeIndex)
                .Where(usage => IsAuthoredTextElement(usage.Element))
                .Where(IsShrinkWrappedBySoleCenteredParent)
                .ToList();
            if (usages.Count == 0)
            {
                return;
            }

            RecordTextStyleContractIssue(rule, usageIndex, report, includeSuppressed,
                usages, property, value, "ineffective-text-align-on-shrink-wrapped-label",
                $"Selector '{string.Join(", ", rule.Selectors)}' sets text alignment on " +
                $"{usages.Count} shrink-wrapped Label element(s). Each Label is the sole child of " +
                "a parent that already centers it on both flex axes, and the Label has no authored " +
                "box expansion for text alignment to act within. Remove the ineffective text-align " +
                "declaration.");
        }

        private static void AuditInheritableOnlyChildTextStyles(UssRule rule,
            IReadOnlyCollection<UssSimpleSelector> selectors, UssUsageIndex usageIndex,
            UssCascadeIndex cascadeIndex, VmAutomationUssStyleAuditReport report,
            bool includeSuppressed)
        {
            var declarations = new Dictionary<string, string>(
                StringComparer.OrdinalIgnoreCase);
            var relatedUsages = new List<UssAuthoredElementUsage>();
            foreach (var property in inheritedTextStyleProperties)
            {
                if (rule.Declarations.TryGetValue(property, out var value) == false ||
                    IsConcreteStyleValue(value) == false)
                {
                    continue;
                }

                var usages = FindWinningElementUsages(rule, property, selectors, cascadeIndex);
                if (usages.Count == 0 || usages.All(IsOnlyAuthoredChildLabel) == false)
                {
                    continue;
                }

                declarations[property] = value;
                relatedUsages.AddRange(usages);
            }

            if (declarations.Count == 0)
            {
                return;
            }

            var usagesForIssue = DistinctElementUsages(relatedUsages);
            var selectorLabel = string.Join(", ", rule.Selectors);
            var runtimeReferences = GetRuntimeReferences(rule, usageIndex);
            var suppressionReason = rule.TextStyleContractSuppressionReason;
            var issue = new VmAutomationUssStyleAuditIssue
            {
                AssetPath = rule.AssetPath,
                Line = rule.Line,
                Selector = selectorLabel,
                Token = string.Join(", ", declarations.Keys
                    .OrderBy(property => property, StringComparer.Ordinal)),
                Kind = "inheritable-text-style-on-only-child-label",
                RelatedDeclarations = declarations,
                AuthoredUsageCount = usagesForIssue.Count,
                RuntimeReferenceCount = runtimeReferences.Count,
                UsageLocations = ToUsageLocations(usagesForIssue)
                    .Concat(runtimeReferences.Select(location => location.ToDictionary()))
                    .Take(20).ToList(),
                Suppressed = string.IsNullOrWhiteSpace(suppressionReason) == false,
                SuppressionReason = suppressionReason,
                Message =
                    $"Selector '{selectorLabel}' owns inheritable text declarations on " +
                    $"{usagesForIssue.Count} Label element(s), each the sole authored child of its " +
                    $"parent: {string.Join(", ", declarations.Keys.OrderBy(property => property, StringComparer.Ordinal))}. " +
                    "Move those declarations to the parent so the Label inherits them, then remove " +
                    "the child-only class if it has no remaining contract."
            };
            report.Record(issue, includeSuppressed);
        }

        private static bool TryGetSupportedTextContractSelectors(UssRule rule,
            out IReadOnlyCollection<UssSimpleSelector> selectors)
        {
            var parsed = new List<UssSimpleSelector>();
            foreach (var selectorText in rule.Selectors)
            {
                if (TryParseSimpleSelector(selectorText, out var selector) == false ||
                    selector.ClassNames.Count == 0 &&
                    string.IsNullOrWhiteSpace(selector.Id))
                {
                    selectors = Array.Empty<UssSimpleSelector>();
                    return false;
                }

                parsed.Add(selector);
            }

            selectors = parsed;
            return parsed.Count > 0;
        }

        private static List<UssAuthoredElementUsage> FindWinningElementUsages(
            UssRule rule, string property,
            IReadOnlyCollection<UssSimpleSelector> selectors, UssCascadeIndex cascadeIndex)
        {
            var usages = new List<UssAuthoredElementUsage>();
            foreach (var document in cascadeIndex.Documents.Where(document =>
                         document.LoadedAssetPaths.Contains(rule.AssetPath)))
            {
                foreach (var element in document.AuthoredDocument.Elements.Where(element =>
                             selectors.Any(selector => selector.Matches(element))))
                {
                    if (element.InlineDeclarations.ContainsKey(property))
                    {
                        continue;
                    }

                    var winner = document.Resolve(element, property, null);
                    if (winner != null && ReferenceEquals(winner.Rule, rule))
                    {
                        usages.Add(new UssAuthoredElementUsage(document, element));
                    }
                }
            }

            return DistinctElementUsages(usages);
        }

        private static List<UssAuthoredElementUsage> DistinctElementUsages(
            IEnumerable<UssAuthoredElementUsage> usages)
        {
            return usages.GroupBy(usage =>
                    $"{usage.Document.AuthoredDocument.AssetPath}:{usage.Element.Line}:{usage.Element.Column}",
                    StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .OrderBy(usage => usage.Document.AuthoredDocument.AssetPath,
                    StringComparer.Ordinal)
                .ThenBy(usage => usage.Element.Line)
                .ThenBy(usage => usage.Element.Column)
                .ToList();
        }

        private static bool IsShrinkWrappedBySoleCenteredParent(
            UssAuthoredElementUsage usage)
        {
            var element = usage.Element;
            var parent = element.Parent;
            if (IsOnlyAuthoredChildLabel(usage) == false ||
                StyleValuesEqual(ResolveOwnStyle(usage.Document, parent, "align-items"),
                    "center") == false ||
                StyleValuesEqual(ResolveOwnStyle(usage.Document, parent, "justify-content"),
                    "center") == false)
            {
                return false;
            }

            var whiteSpace = ResolveEffectiveTextStyle(usage.Document, element,
                "white-space");
            if (StyleValuesEqual(whiteSpace, "normal") ||
                string.IsNullOrEmpty(element.Text) == false &&
                (element.Text.Contains('\n') || element.Text.Contains('\r')))
            {
                return false;
            }

            if (HasConcreteOwnStyle(usage.Document, element, "width") ||
                HasConcreteOwnStyle(usage.Document, element, "height") ||
                HasConcreteOwnStyle(usage.Document, element, "min-width") ||
                HasConcreteOwnStyle(usage.Document, element, "min-height") ||
                HasConcreteOwnStyle(usage.Document, element, "max-width") ||
                HasConcreteOwnStyle(usage.Document, element, "max-height") ||
                HasConcreteOwnStyle(usage.Document, element, "flex-basis"))
            {
                return false;
            }

            var alignSelf = ResolveOwnStyle(usage.Document, element, "align-self");
            if (StyleValuesEqual(alignSelf, "stretch"))
            {
                return false;
            }

            var flexGrow = ResolveOwnStyle(usage.Document, element, "flex-grow");
            if (HasPositiveNumber(flexGrow))
            {
                return false;
            }

            return new[]
                {
                    "padding", "padding-left", "padding-right", "padding-top",
                    "padding-bottom"
                }
                .All(property => HasNonZeroLength(
                    ResolveOwnStyle(usage.Document, element, property)) == false);
        }

        private static bool IsOnlyAuthoredChildLabel(UssAuthoredElementUsage usage)
        {
            var element = usage.Element;
            return IsAuthoredTextElement(element) &&
                   element.Parent != null &&
                   IsAuthoredTextElement(element.Parent) == false &&
                   element.Parent.Children.Count == 1 &&
                   ReferenceEquals(element.Parent.Children[0], element);
        }

        private static bool IsAuthoredTextElement(UssAuthoredElement element)
        {
            if (element == null)
            {
                return false;
            }

            return string.Equals(element.TypeName, "Label", StringComparison.Ordinal) ||
                   string.Equals(element.TypeName, "TextElement", StringComparison.Ordinal) ||
                   element.TypeName.EndsWith(".Label", StringComparison.Ordinal) ||
                   element.TypeName.EndsWith(".TextElement", StringComparison.Ordinal);
        }

        private static string ResolveOwnStyle(UssCascadeDocument document,
            UssAuthoredElement element, string property)
        {
            if (element == null)
            {
                return "";
            }

            if (element.InlineDeclarations.TryGetValue(property, out var inlineValue))
            {
                return inlineValue;
            }

            return document.Resolve(element, property, null)?.Value ?? "";
        }

        private static string ResolveEffectiveTextStyle(UssCascadeDocument document,
            UssAuthoredElement element, string property)
        {
            for (var current = element; current != null; current = current.Parent)
            {
                var value = ResolveOwnStyle(document, current, property);
                if (string.IsNullOrWhiteSpace(value) == false)
                {
                    return value;
                }
            }

            return "";
        }

        private static bool IsTextAutoSizeEnabled(string value)
        {
            return (value ?? "").Trim().StartsWith("best-fit",
                StringComparison.OrdinalIgnoreCase);
        }

        private static bool HasConcreteOwnStyle(UssCascadeDocument document,
            UssAuthoredElement element, string property)
        {
            return IsConcreteStyleValue(ResolveOwnStyle(document, element, property));
        }

        private static bool IsConcreteStyleValue(string value)
        {
            var normalized = (value ?? "").Trim();
            return normalized.Length > 0 &&
                   string.Equals(normalized, "auto", StringComparison.OrdinalIgnoreCase) == false &&
                   string.Equals(normalized, "none", StringComparison.OrdinalIgnoreCase) == false &&
                   string.Equals(normalized, "initial", StringComparison.OrdinalIgnoreCase) == false &&
                   string.Equals(normalized, "inherit", StringComparison.OrdinalIgnoreCase) == false &&
                   string.Equals(normalized, "unset", StringComparison.OrdinalIgnoreCase) == false;
        }

        private static bool HasPositiveNumber(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            return float.TryParse(value.Trim(), NumberStyles.Float,
                       CultureInfo.InvariantCulture, out var parsed)
                ? parsed > 0
                : IsConcreteStyleValue(value);
        }

        private static bool HasNonZeroLength(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var matches = Regex.Matches(value,
                @"[-+]?(?:\d+(?:\.\d*)?|\.\d+)");
            if (matches.Count == 0)
            {
                return IsConcreteStyleValue(value);
            }

            foreach (Match match in matches)
            {
                if (float.TryParse(match.Value, NumberStyles.Float,
                        CultureInfo.InvariantCulture, out var parsed) == false ||
                    Math.Abs(parsed) > 0.0001f)
                {
                    return true;
                }
            }

            return false;
        }

        private static List<UssUsageLocation> GetRuntimeReferences(UssRule rule,
            UssUsageIndex usageIndex)
        {
            return rule.Selectors
                .SelectMany(selector => classTokenRegex.Matches(selector).Cast<Match>())
                .Select(match => match.Groups["token"].Value)
                .Distinct(StringComparer.Ordinal)
                .SelectMany(usageIndex.GetRuntimeClassReferences)
                .GroupBy(location =>
                        $"{location.Path}:{location.Line}:{location.Column}",
                    StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();
        }

        private static IEnumerable<Dictionary<string, object>> ToUsageLocations(
            IEnumerable<UssAuthoredElementUsage> usages)
        {
            return usages.Select(usage => new UssUsageLocation(
                    usage.Document.AuthoredDocument.AssetPath,
                    usage.Element.Line, usage.Element.Column))
                .Select(location => location.ToDictionary());
        }

        private static void RecordTextStyleContractIssue(UssRule rule,
            UssUsageIndex usageIndex, VmAutomationUssStyleAuditReport report,
            bool includeSuppressed, IEnumerable<UssAuthoredElementUsage> usages,
            string property, string value, string kind, string message)
        {
            var authoredUsages = DistinctElementUsages(usages);
            var runtimeReferences = GetRuntimeReferences(rule, usageIndex);
            var suppressionReason = rule.TextStyleContractSuppressionReason;
            var issue = new VmAutomationUssStyleAuditIssue
            {
                AssetPath = rule.AssetPath,
                Line = rule.Line,
                Selector = string.Join(", ", rule.Selectors),
                Token = property,
                Kind = kind,
                Property = property,
                Value = value,
                AuthoredUsageCount = authoredUsages.Count,
                RuntimeReferenceCount = runtimeReferences.Count,
                UsageLocations = ToUsageLocations(authoredUsages)
                    .Concat(runtimeReferences.Select(location => location.ToDictionary()))
                    .Take(20).ToList(),
                Suppressed = string.IsNullOrWhiteSpace(suppressionReason) == false,
                SuppressionReason = suppressionReason,
                Message = message
            };
            report.Record(issue, includeSuppressed);
        }

        private static void AuditRelationalSelectorContracts(IReadOnlyList<UssRule> rules,
            UssUsageIndex usageIndex, VmAutomationUssStyleAuditReport report, bool includeSuppressed)
        {
            var reportedAnchors = new HashSet<string>(StringComparer.Ordinal);
            var reportedClassTargets = new HashSet<string>(StringComparer.Ordinal);
            var reportedIdTargets = new HashSet<string>(StringComparer.Ordinal);

            foreach (var rule in rules)
            {
                foreach (var selector in rule.Selectors)
                {
                    if (simpleClassSelectorRegex.IsMatch(selector) ||
                        simpleIdSelectorRegex.IsMatch(selector) ||
                        selector.Contains(':') ||
                        SelectorHasRuntimeClassContract(selector, usageIndex))
                    {
                        continue;
                    }

                    foreach (Match anchorMatch in relationalAnchorClassTokenRegex.Matches(selector))
                    {
                        var token = anchorMatch.Groups["token"].Value;
                        if (reportedAnchors.Contains(token))
                        {
                            continue;
                        }

                        var authored = usageIndex.GetClassUsages(token);
                        var runtime = usageIndex.GetRuntimeClassReferences(token);
                        var elementName = usageIndex.GetSingleClassUsageName(token);
                        if (authored.Count != 1 || runtime.Count != 0 ||
                            string.IsNullOrWhiteSpace(elementName))
                        {
                            continue;
                        }

                        var issueRule = rule;
                        var issueSelector = selector;
                        foreach (var candidateRule in rules)
                        {
                            var candidateSelector = candidateRule.Selectors.FirstOrDefault(candidate =>
                            {
                                var match = simpleClassSelectorRegex.Match(candidate);
                                return match.Success && match.Groups["token"].Value == token;
                            });
                            if (candidateSelector == null)
                            {
                                continue;
                            }

                            issueRule = candidateRule;
                            issueSelector = candidateSelector;
                            break;
                        }

                        AddIssue(report, issueRule, issueSelector, token,
                            "single-use-relational-class-anchor", authored, runtime,
                            $"Class anchor '.{token}' identifies one named authored UXML element " +
                            $"'{elementName}'. Move its ordinary declarations inline and replace the class " +
                            $"anchor in relational selectors with '#{elementName}'.",
                            includeSuppressed);
                        reportedAnchors.Add(token);
                    }

                    var targetMatch = relationalTargetClassTokenRegex.Match(selector);
                    if (targetMatch.Success)
                    {
                        var targetToken = targetMatch.Groups["token"].Value;
                        if (reportedClassTargets.Contains(targetToken) == false)
                        {
                            var targetAuthored = usageIndex.GetClassUsages(targetToken);
                            var targetRuntime = usageIndex.GetRuntimeClassReferences(targetToken);
                            if (targetAuthored.Count == 1 && targetRuntime.Count == 0)
                            {
                                AddIssue(report, rule, selector, targetToken,
                                    "single-use-relational-class-target", targetAuthored, targetRuntime,
                                    $"Class target '.{targetToken}' in relational selector '{selector}' serves one " +
                                    "authored UXML element and has no pseudo or runtime contract. Move the declarations " +
                                    "to that element's inline style and remove the class token.",
                                    includeSuppressed);
                                reportedClassTargets.Add(targetToken);
                            }
                        }
                    }

                    var idTargetMatch = relationalTargetIdTokenRegex.Match(selector);
                    if (idTargetMatch.Success == false)
                    {
                        continue;
                    }

                    var idTargetToken = idTargetMatch.Groups["token"].Value;
                    if (reportedIdTargets.Contains(idTargetToken))
                    {
                        continue;
                    }

                    var idTargetUsages = usageIndex.GetIdUsages(idTargetToken);
                    if (idTargetUsages.Count != 1)
                    {
                        continue;
                    }

                    AddIssue(report, rule, selector, idTargetToken,
                        "single-use-relational-id-target", idTargetUsages,
                        Array.Empty<UssUsageLocation>(),
                        $"ID target '#{idTargetToken}' in relational selector '{selector}' identifies one " +
                        "authored UXML element. Move the declarations to that element's inline style; keep its " +
                        "name only when binding, lookup, or another real consumer still requires it.",
                        includeSuppressed);
                    reportedIdTargets.Add(idTargetToken);
                }
            }
        }

        internal static bool SelectorHasRuntimeClassContract(string selector, UssUsageIndex usageIndex)
        {
            return classTokenRegex.Matches(selector).Cast<Match>().Any(match =>
                usageIndex.GetRuntimeClassReferences(match.Groups["token"].Value).Count > 0);
        }

        internal static void AddIssue(VmAutomationUssStyleAuditReport report, UssRule rule, string selector,
            string token, string kind, IReadOnlyCollection<UssUsageLocation> authored,
            IReadOnlyCollection<UssUsageLocation> runtime, string message, bool includeSuppressed)
        {
            var issue = new VmAutomationUssStyleAuditIssue
            {
                AssetPath = rule.AssetPath,
                Line = rule.Line,
                Selector = selector,
                Token = token,
                Kind = kind,
                AuthoredUsageCount = authored.Count,
                RuntimeReferenceCount = runtime.Count,
                UsageLocations = authored.Concat(runtime).Take(20).Select(location => location.ToDictionary()).ToList(),
                Suppressed = string.IsNullOrEmpty(rule.SuppressionReason) == false,
                SuppressionReason = rule.SuppressionReason,
                Message = message
            };
            report.Record(issue, includeSuppressed);
        }

        private static List<string> NormalizeRequestedPaths(IEnumerable<string> requestedPaths,
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
                    path.EndsWith(".uss", StringComparison.OrdinalIgnoreCase) == false)
                {
                    errors.Add($"USS audit path must be an Assets-relative .uss path: {path}");
                }
                else if (File.Exists(VmAutomationUIToolkitAuditUtility.ToFullPath(path)) == false)
                {
                    errors.Add($"USS asset does not exist: {path}");
                }
            }

            return requested
                .Where(path => File.Exists(VmAutomationUIToolkitAuditUtility.ToFullPath(path)))
                .ToList();
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
    }
}
#endif
