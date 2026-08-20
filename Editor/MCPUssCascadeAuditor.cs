#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UssRule = VMUnityAutomation.Editor.MCPUssAuditContext.UssRule;
using UssSimpleSelector = VMUnityAutomation.Editor.MCPUssAuditContext.UssSimpleSelector;
using UssAuthoredElement = VMUnityAutomation.Editor.MCPUssAuditContext.UssAuthoredElement;
using UssResolvedDeclaration = VMUnityAutomation.Editor.MCPUssAuditContext.UssResolvedDeclaration;
using UssCascadeRule = VMUnityAutomation.Editor.MCPUssAuditContext.UssCascadeRule;
using UssCascadeDocument = VMUnityAutomation.Editor.MCPUssAuditContext.UssCascadeDocument;
using UssCascadeIndex = VMUnityAutomation.Editor.MCPUssAuditContext.UssCascadeIndex;
using UssUsageIndex = VMUnityAutomation.Editor.MCPUssAuditContext.UssUsageIndex;
using static VMUnityAutomation.Editor.MCPUssStyleAuditor;
using static VMUnityAutomation.Editor.MCPUssStyleSheetParser;

namespace VMUnityAutomation.Editor
{
    internal static class MCPUssCascadeAuditor
    {
        private static readonly Regex classTokenRegex =
            new Regex(@"(?<![A-Za-z0-9_-])\.(?<token>[A-Za-z_][A-Za-z0-9_-]*)",
                RegexOptions.Compiled);

        private static readonly Regex idTokenRegex =
            new Regex(@"(?<![A-Za-z0-9_-])#(?<token>[A-Za-z_][A-Za-z0-9_-]*)",
                RegexOptions.Compiled);

        internal static UssCascadeIndex BuildCascadeIndex(string commonThemePath,
            IReadOnlyDictionary<string, List<UssRule>> rulesByPath, UssUsageIndex usageIndex,
            ICollection<string> errors)
        {
            var index = new UssCascadeIndex();
            var additionalRules =
                new Dictionary<string, List<UssRule>>(StringComparer.OrdinalIgnoreCase);
            var reportedErrors = new HashSet<string>(StringComparer.Ordinal);

            foreach (var authoredDocument in usageIndex.Documents)
            {
                var cascadeDocument = new UssCascadeDocument(authoredDocument);
                if (string.IsNullOrWhiteSpace(commonThemePath) == false)
                {
                    AppendStyleSheetCascade(commonThemePath, 0, cascadeDocument, rulesByPath,
                        additionalRules, new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                        errors, reportedErrors);
                }

                foreach (var stylePath in authoredDocument.StylePaths)
                {
                    AppendStyleSheetCascade(stylePath, 1, cascadeDocument, rulesByPath,
                        additionalRules, new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                        errors, reportedErrors);
                }

                index.Documents.Add(cascadeDocument);
            }

            return index;
        }

        private static void AppendStyleSheetCascade(string assetPath, int origin,
            UssCascadeDocument document,
            IReadOnlyDictionary<string, List<UssRule>> rulesByPath,
            IDictionary<string, List<UssRule>> additionalRules,
            ISet<string> importStack, ICollection<string> errors, ISet<string> reportedErrors)
        {
            assetPath = MCPUIToolkitAuditUtility.NormalizeAssetPath(assetPath);
            if (string.IsNullOrWhiteSpace(assetPath) || importStack.Add(assetPath) == false)
            {
                return;
            }

            try
            {
                var fullPath = MCPUIToolkitAuditUtility.ToFullPath(assetPath);
                if (File.Exists(fullPath) == false)
                {
                    var message = $"Referenced stylesheet does not exist: {assetPath}";
                    if (reportedErrors.Add(message))
                    {
                        errors.Add(message);
                    }

                    return;
                }

                var text = File.ReadAllText(fullPath);
                foreach (var importPath in GetImportedStylePaths(assetPath, text))
                {
                    AppendStyleSheetCascade(importPath, origin, document, rulesByPath,
                        additionalRules, importStack, errors, reportedErrors);
                }

                if (rulesByPath.TryGetValue(assetPath, out var rules) == false &&
                    additionalRules.TryGetValue(assetPath, out rules) == false)
                {
                    rules = ParseStyleSheet(assetPath, text);
                    additionalRules[assetPath] = rules;
                }

                document.LoadedAssetPaths.Add(assetPath);
                foreach (var rule in rules)
                {
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
            catch (Exception exception)
            {
                var message = $"Failed to index stylesheet cascade '{assetPath}': " +
                              exception.Message;
                if (reportedErrors.Add(message))
                {
                    errors.Add(message);
                }
            }
            finally
            {
                importStack.Remove(assetPath);
            }
        }

        internal static void AuditRedundantDeclarations(IReadOnlyList<UssRule> rules,
            UssUsageIndex usageIndex, UssCascadeIndex cascadeIndex,
            MCPUssStyleAuditReport report, bool includeSuppressed)
        {
            foreach (var rule in rules)
            {
                var selectors = new List<UssSimpleSelector>();
                var fullySupported = true;
                foreach (var selectorText in rule.Selectors)
                {
                    if (TryParseSimpleSelector(selectorText, out var selector) == false)
                    {
                        fullySupported = false;
                        break;
                    }

                    selectors.Add(selector);
                }

                if (fullySupported == false || selectors.Count == 0 ||
                    selectors.All(selector => selector.Specificity == 0) ||
                    rule.Selectors.Any(selector =>
                        SelectorHasRuntimeClassContract(selector, usageIndex)))
                {
                    continue;
                }

                foreach (var declaration in rule.Declarations)
                {
                    var authoredUsages = new List<UssUsageLocation>();
                    var fallbackRules = new List<UssResolvedDeclaration>();
                    var targetWon = false;
                    var uncertain = false;

                    foreach (var document in cascadeIndex.Documents.Where(document =>
                                 document.LoadedAssetPaths.Contains(rule.AssetPath)))
                    {
                        if (document.HasUnsupportedCompetingDeclaration(
                                declaration.Key, declaration.Value))
                        {
                            uncertain = true;
                            break;
                        }

                        foreach (var element in document.AuthoredDocument.Elements.Where(element =>
                                     selectors.Any(selector => selector.Matches(element))))
                        {
                            if (element.InlineDeclarations.ContainsKey(declaration.Key))
                            {
                                continue;
                            }

                            var current = document.Resolve(element, declaration.Key, null);
                            if (current == null || ReferenceEquals(current.Rule, rule) == false)
                            {
                                continue;
                            }

                            targetWon = true;
                            var fallback = document.Resolve(element, declaration.Key, rule);
                            if (fallback == null &&
                                MCPUIToolkitInitialStyleComparer.IsInitialValue(
                                    declaration.Key, declaration.Value))
                            {
                                var initialRule = new UssRule
                                {
                                    AssetPath = $"unity-initial://{element.ComponentTypeName}",
                                    Line = 0
                                };
                                initialRule.Declarations[declaration.Key] =
                                    declaration.Value;
                                fallback = new UssResolvedDeclaration
                                {
                                    Rule = initialRule,
                                    SelectorText = $"<{element.ComponentTypeName} initial style>",
                                    Value = declaration.Value,
                                    Origin = -1,
                                    Specificity = -1,
                                    SourceOrder = -1
                                };
                            }

                            if (fallback == null ||
                                StyleValuesEqual(fallback.Value, declaration.Value) == false)
                            {
                                uncertain = true;
                                break;
                            }

                            authoredUsages.Add(new UssUsageLocation(
                                document.AuthoredDocument.AssetPath, element.Line, element.Column));
                            fallbackRules.Add(fallback);
                        }

                        if (uncertain)
                        {
                            break;
                        }
                    }

                    if (uncertain || targetWon == false || fallbackRules.Count == 0)
                    {
                        continue;
                    }

                    AddRedundantDeclarationIssue(report, rule, declaration.Key,
                        declaration.Value, authoredUsages, fallbackRules, includeSuppressed);
                }
            }
        }

        private static void AddRedundantDeclarationIssue(MCPUssStyleAuditReport report,
            UssRule rule, string property, string value,
            IEnumerable<UssUsageLocation> authoredUsages,
            IEnumerable<UssResolvedDeclaration> fallbackDeclarations,
            bool includeSuppressed)
        {
            var usages = authoredUsages
                .GroupBy(usage => $"{usage.Path}:{usage.Line}:{usage.Column}",
                    StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();
            var fallbackRules = fallbackDeclarations
                .GroupBy(fallback =>
                        $"{fallback.Rule.AssetPath}\n{fallback.SelectorText}\n{property}",
                    StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .OrderBy(fallback => fallback.Rule.AssetPath, StringComparer.Ordinal)
                .ThenBy(fallback => fallback.Rule.Line)
                .ThenBy(fallback => fallback.SelectorText, StringComparer.Ordinal)
                .ToList();
            var sourceLabels = fallbackRules
                .Select(fallback =>
                    $"'{fallback.SelectorText}' in {fallback.Rule.AssetPath}")
                .ToList();
            var selectorLabel = string.Join(", ", rule.Selectors);
            var issue = new MCPUssStyleAuditIssue
            {
                AssetPath = rule.AssetPath,
                Line = rule.Line,
                Selector = selectorLabel,
                Token = property,
                Kind = "redundant-declaration",
                Property = property,
                Value = value,
                AuthoredUsageCount = usages.Count,
                UsageLocations = usages.Take(20)
                    .Select(location => location.ToDictionary()).ToList(),
                StylesheetRules = fallbackRules.Select(fallback =>
                    new Dictionary<string, object>
                    {
                        { "property", property },
                        { "value", fallback.Value },
                        { "selector", fallback.SelectorText },
                        { "sourcePath", fallback.Rule.AssetPath },
                        { "line", fallback.Rule.Line },
                        { "sourceKind", fallback.Rule.AssetPath.StartsWith(
                            "unity-initial://", StringComparison.Ordinal)
                            ? "initial-style"
                            : "stylesheet" }
                    }).ToList(),
                Suppressed = string.IsNullOrWhiteSpace(
                    rule.RedundantDeclarationSuppressionReason) == false,
                SuppressionReason = rule.RedundantDeclarationSuppressionReason,
                Message =
                    $"Declaration '{property}: {value}' in selector '{selectorLabel}' repeats " +
                    $"the same effective baseline value already supplied by {string.Join(", ", sourceLabels)} " +
                    $"for {usages.Count} authored UXML element(s). Remove the duplicate declaration " +
                    "so the component default, implicit-class theme rule, or broader loaded style " +
                    "remains the single owner."
            };
            report.Record(issue, includeSuppressed);
        }

        internal static bool TryParseSimpleSelector(string rawSelector,
            out UssSimpleSelector selector)
        {
            selector = null;
            var value = (rawSelector ?? "").Trim();
            var match = Regex.Match(value,
                @"^(?<type>\*|[A-Za-z_][A-Za-z0-9_-]*)?" +
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
            if (typeName == "*")
            {
                typeName = "";
            }

            selector = new UssSimpleSelector
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

        internal static bool StyleValuesEqual(string left, string right)
        {
            return string.Equals(
                Regex.Replace((left ?? "").Trim(), @"\s+", " "),
                Regex.Replace((right ?? "").Trim(), @"\s+", " "),
                StringComparison.OrdinalIgnoreCase);
        }

        internal static bool IsDynamicStateSelector(string selector)
        {
            var dynamicStates = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "active",
                "checked",
                "disabled",
                "enabled",
                "focus",
                "focus-visible",
                "focus-within",
                "hover",
                "inactive",
                "selected"
            };
            return Regex.Matches(selector ?? "",
                    @":{1,2}(?<state>[A-Za-z_][A-Za-z0-9_-]*)")
                .Cast<Match>()
                .Any(match => dynamicStates.Contains(match.Groups["state"].Value));
        }
    }
}
#endif
