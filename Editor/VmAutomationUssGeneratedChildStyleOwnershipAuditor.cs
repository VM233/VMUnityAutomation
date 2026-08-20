#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using static VMUnityAutomation.Editor.VmAutomationUssAuditContext;

namespace VMUnityAutomation.Editor
{
    internal static class VmAutomationUssGeneratedChildStyleOwnershipAuditor
    {
        internal const string KIND = "over-scoped-generated-component-style";

        private static readonly Regex ClassTokenRegex = new Regex(
            @"(?<![A-Za-z0-9_-])\.(?<token>[A-Za-z_][A-Za-z0-9_-]*)",
            RegexOptions.Compiled);

        private static readonly Regex ScopedParentRegex = new Regex(
            @"^(?<scope>.+)\s+(?<component>[^\s>+~]+)$",
            RegexOptions.Compiled);

        internal static void Audit(IReadOnlyList<UssRule> rules,
            UssUsageIndex usageIndex, VmAutomationUssStyleAuditReport report,
            bool includeSuppressed)
        {
            var reportedParents = new HashSet<string>(StringComparer.Ordinal);
            foreach (var rule in rules)
            {
                foreach (var selector in rule.Selectors)
                {
                    if (TryGetContract(selector, usageIndex, out var contract) == false ||
                        reportedParents.Add(rule.AssetPath + "\n" + contract.ParentSelector) == false)
                    {
                        continue;
                    }

                    var authored = string.IsNullOrWhiteSpace(contract.ComponentClass)
                        ? Array.Empty<UssUsageLocation>()
                        : usageIndex.GetClassUsages(contract.ComponentClass);
                    var runtime = contract.GeneratedClasses
                        .SelectMany(usageIndex.GetRuntimeClassAssignments)
                        .GroupBy(location =>
                            $"{location.Path}:{location.Line}:{location.Column}",
                            StringComparer.OrdinalIgnoreCase)
                        .Select(group => group.First())
                        .ToList();
                    var token = string.IsNullOrWhiteSpace(contract.ComponentClass)
                        ? contract.GeneratedClasses[0]
                        : contract.ComponentClass;

                    VmAutomationUssStyleAuditor.AddIssue(report, rule, selector, token, KIND,
                        authored, runtime,
                        $"Selector family rooted at '{contract.ParentSelector}' crosses " +
                        $"feature/page scope '{contract.ScopeSelector}' before styling " +
                        $"runtime-generated child class(es) " +
                        $"{string.Join(", ", contract.GeneratedClasses.Select(value => "." + value))}. " +
                        "Move the generated-child geometry, visibility, and sprite/state mapping " +
                        "to a standalone component or skin USS rooted at an explicit variant " +
                        "class on the component itself. Keep the feature USS responsible only " +
                        "for placing the component root.",
                        includeSuppressed);
                }
            }
        }

        internal static bool ClassAnchorsGeneratedChildStyle(
            IReadOnlyList<UssRule> rules, UssUsageIndex usageIndex, string className)
        {
            foreach (var rule in rules)
            {
                foreach (var selector in rule.Selectors)
                {
                    if (TrySplitDirectChildSelector(selector, out var parent,
                            out var child) == false ||
                        GetClassTokens(GetTargetCompound(parent)).Contains(className) == false)
                    {
                        continue;
                    }

                    if (GetClassTokens(child).Any(token =>
                            usageIndex.GetRuntimeClassAssignments(token).Count > 0))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        internal static IReadOnlyList<Dictionary<string, object>> RunSelfTests()
        {
            const string path = "Assets/__GeneratedChildStyleOwnershipAudit.uss";
            var usageIndex = new UssUsageIndex();
            usageIndex.AddClassUsage("rank", "Assets/Profile.uxml", 3, 1);
            usageIndex.AddRuntimeClassAssignment("glyph", "Assets/SpriteNumber.cs", 8, 1);

            var scopedRules = VmAutomationUssStyleSheetParser.ParseStyleSheet(path,
                "#SkillTree .rank > .glyph { width: 8px; height: 12px; }\n" +
                "#SkillTree .rank > .glyph-0 { background-image: url(\"0.png\"); }\n");
            var scopedReport = AuditForSelfTest(scopedRules, usageIndex);

            var variantRules = VmAutomationUssStyleSheetParser.ParseStyleSheet(path,
                ".sprite-number-skill-tree > .glyph { width: 8px; height: 12px; }\n" +
                ".sprite-number-skill-tree > .glyph-0 { background-image: url(\"0.png\"); }\n");
            var variantReport = AuditForSelfTest(variantRules, usageIndex);

            var authoredChildRules = VmAutomationUssStyleSheetParser.ParseStyleSheet(path,
                "#SkillTree .rank > .authored-glyph { width: 8px; }\n");
            var authoredChildReport = AuditForSelfTest(authoredChildRules, usageIndex);

            var directPageOwnerRules = VmAutomationUssStyleSheetParser.ParseStyleSheet(path,
                "#SkillTree > .glyph { width: 8px; }\n");
            var directPageOwnerReport = AuditForSelfTest(directPageOwnerRules, usageIndex);

            var suppressedRules = VmAutomationUssStyleSheetParser.ParseStyleSheet(path,
                "/* uss-audit: allow-single-use measured page-owned exception */\n" +
                "#SkillTree .rank > .glyph { width: 8px; }\n");
            var suppressedReport = AuditForSelfTest(suppressedRules, usageIndex, true);

            return new[]
            {
                TestCase("page-scoped generated component style warns",
                    HasActiveFinding(scopedReport)),
                TestCase("explicit component skin variant passes",
                    HasActiveFinding(variantReport) == false),
                TestCase("authored child selector passes",
                    HasActiveFinding(authoredChildReport) == false),
                TestCase("page-owned direct child passes",
                    HasActiveFinding(directPageOwnerReport) == false),
                TestCase("reasoned ownership suppression is retained",
                    suppressedReport.WarningCount == 0 &&
                    suppressedReport.SuppressedCount == 1 &&
                    suppressedReport.Issues.Single(issue => issue.Kind == KIND).Suppressed)
            };
        }

        private static bool TryGetContract(string selector, UssUsageIndex usageIndex,
            out ScopedGeneratedChildContract contract)
        {
            contract = null;
            if (TrySplitDirectChildSelector(selector, out var parent,
                    out var child) == false)
            {
                return false;
            }

            var parentMatch = ScopedParentRegex.Match(parent);
            if (parentMatch.Success == false)
            {
                return false;
            }

            var scope = parentMatch.Groups["scope"].Value.Trim();
            if (scope.EndsWith(">", StringComparison.Ordinal) ||
                scope.EndsWith("+", StringComparison.Ordinal) ||
                scope.EndsWith("~", StringComparison.Ordinal))
            {
                return false;
            }

            var generatedClasses = GetClassTokens(child)
                .Where(token => usageIndex.GetRuntimeClassAssignments(token).Count > 0)
                .OrderBy(token => token, StringComparer.Ordinal)
                .ToList();
            if (generatedClasses.Count == 0)
            {
                return false;
            }

            var componentClass = GetClassTokens(parentMatch.Groups["component"].Value)
                .LastOrDefault() ?? "";
            contract = new ScopedGeneratedChildContract(parent, scope,
                componentClass, generatedClasses);
            return true;
        }

        private static bool TrySplitDirectChildSelector(string selector,
            out string parent, out string child)
        {
            parent = "";
            child = "";
            var value = (selector ?? "").Trim();
            var directChildIndex = value.IndexOf('>');
            if (directChildIndex <= 0 || directChildIndex != value.LastIndexOf('>'))
            {
                return false;
            }

            parent = value.Substring(0, directChildIndex).Trim();
            child = value.Substring(directChildIndex + 1).Trim();
            return parent.Length > 0 && child.Length > 0 &&
                   child.IndexOfAny(new[] { ' ', '>', '+', '~' }) < 0;
        }

        private static string GetTargetCompound(string selector)
        {
            var value = (selector ?? "").Trim();
            var splitIndex = value.LastIndexOfAny(new[] { ' ', '>', '+', '~' });
            return splitIndex < 0 ? value : value.Substring(splitIndex + 1).Trim();
        }

        private static IReadOnlyList<string> GetClassTokens(string selector)
        {
            return ClassTokenRegex.Matches(selector ?? "").Cast<Match>()
                .Select(match => match.Groups["token"].Value)
                .Distinct(StringComparer.Ordinal)
                .ToList();
        }

        private static VmAutomationUssStyleAuditReport AuditForSelfTest(
            IReadOnlyList<UssRule> rules, UssUsageIndex usageIndex,
            bool includeSuppressed = false)
        {
            var report = new VmAutomationUssStyleAuditReport(100);
            Audit(rules, usageIndex, report, includeSuppressed);
            report.SortIssues();
            return report;
        }

        private static bool HasActiveFinding(VmAutomationUssStyleAuditReport report)
        {
            return report.Issues.Any(issue => issue.Kind == KIND &&
                                              issue.Suppressed == false);
        }

        private static Dictionary<string, object> TestCase(string name, bool passed)
        {
            return new Dictionary<string, object>
            {
                { "name", name },
                { "passed", passed }
            };
        }

        private sealed class ScopedGeneratedChildContract
        {
            internal readonly string ParentSelector;
            internal readonly string ScopeSelector;
            internal readonly string ComponentClass;
            internal readonly IReadOnlyList<string> GeneratedClasses;

            internal ScopedGeneratedChildContract(string parentSelector,
                string scopeSelector, string componentClass,
                IReadOnlyList<string> generatedClasses)
            {
                ParentSelector = parentSelector;
                ScopeSelector = scopeSelector;
                ComponentClass = componentClass;
                GeneratedClasses = generatedClasses;
            }
        }
    }
}
#endif
