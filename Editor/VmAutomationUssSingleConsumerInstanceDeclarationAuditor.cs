#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using static VMUnityAutomation.Editor.VmAutomationUssAuditContext;

namespace VMUnityAutomation.Editor
{
    internal static class VmAutomationUssSingleConsumerInstanceDeclarationAuditor
    {
        internal const string KIND = "single-consumer-class-instance-declarations";

        private static readonly Regex SimpleClassSelectorRegex = new Regex(
            @"^\.(?<token>[A-Za-z_][A-Za-z0-9_-]*)$", RegexOptions.Compiled);

        private static readonly Regex ClassTokenRegex = new Regex(
            @"(?<![A-Za-z0-9_-])\.(?<token>[A-Za-z_][A-Za-z0-9_-]*)",
            RegexOptions.Compiled);

        private static readonly HashSet<string> InstanceOwnedProperties =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "display",
                "margin",
                "margin-top",
                "margin-right",
                "margin-bottom",
                "margin-left"
            };

        internal static void Audit(IReadOnlyList<UssRule> rules,
            UssUsageIndex usageIndex, IReadOnlyCollection<string> variantFamilyTokens,
            VmAutomationUssStyleAuditReport report)
        {
            var dependentContracts = BuildDependentContracts(rules);
            foreach (var rule in rules)
            {
                if (TryGetSimpleClassToken(rule, out var token) == false ||
                    dependentContracts.TryGetValue(token, out var dependentContract) == false ||
                    variantFamilyTokens.Contains(token))
                {
                    continue;
                }

                var authored = usageIndex.GetClassUsages(token);
                var runtime = usageIndex.GetRuntimeClassReferences(token);
                if (authored.Count != 1 || runtime.Count != 0)
                {
                    continue;
                }

                var instanceDeclarations = rule.Declarations
                    .Where(declaration =>
                        InstanceOwnedProperties.Contains(declaration.Key) &&
                        dependentContract.Owns(declaration.Key) == false)
                    .OrderBy(declaration => declaration.Key,
                        StringComparer.OrdinalIgnoreCase)
                    .ToList();
                if (instanceDeclarations.Count == 0)
                {
                    continue;
                }

                RecordIssue(rule, token, authored, runtime, dependentContract,
                    instanceDeclarations, report);
            }
        }

        internal static IReadOnlyList<Dictionary<string, object>> RunSelfTests()
        {
            const string uss =
                "/* uss-audit: allow-single-use fixture must not suppress ownership */\n" +
                ".tooltip-resource-warning { display: none; margin-top: 3px; " +
                "color: red; }\n" +
                ".tooltip-resource-warning.tooltip-maximum-rank { color: orange; }\n" +
                ".visual-warning { color: red; white-space: normal; }\n" +
                ".visual-warning.maximum { color: orange; }\n" +
                ".state-owned { display: none; margin-top: 3px; }\n" +
                ".state-owned.expanded { display: flex; margin-top: 0; }\n" +
                ".shared-warning { display: none; margin-left: 3px; }\n" +
                ".shared-warning.maximum { color: orange; }\n" +
                ".runtime-warning { display: none; margin-right: 3px; }\n" +
                ".runtime-warning.maximum { color: orange; }\n" +
                ".skin-compact { display: none; margin-bottom: 3px; }\n" +
                ".skin-compact:hover { color: white; }\n";
            const string uxml =
                "<ui:Label class=\"tooltip-resource-warning\"/>" +
                "<ui:Label class=\"visual-warning\"/>" +
                "<ui:VisualElement class=\"state-owned\"/>" +
                "<ui:Label class=\"shared-warning\"/>" +
                "<ui:Label class=\"shared-warning\"/>" +
                "<ui:Label class=\"runtime-warning\"/>" +
                "<ui:VisualElement class=\"skin-compact\"/>";

            var report = AuditFixture(uss, uxml,
                new[] { "runtime-warning" }, new[] { "skin-compact" });
            var issue = report.Issues.SingleOrDefault(item => item.Kind == KIND);
            return new[]
            {
                TestCase("single-consumer anchor reports instance declarations",
                    issue != null && issue.IsError &&
                    issue.RelatedDeclarations.Keys.OrderBy(value => value,
                            StringComparer.Ordinal)
                        .SequenceEqual(new[] { "display", "margin-top" })),
                TestCase("instance declaration ownership is unsuppressible",
                    issue != null && issue.Suppressed == false &&
                    report.ErrorCount == 1 && report.WarningCount == 0),
                TestCase("visual declarations retained by a state anchor pass",
                    report.Issues.All(item => item.Token != "visual-warning")),
                TestCase("dependent selector may own display and margin state",
                    report.Issues.All(item => item.Token != "state-owned")),
                TestCase("shared class instance declarations pass",
                    report.Issues.All(item => item.Token != "shared-warning")),
                TestCase("runtime class instance declarations pass",
                    report.Issues.All(item => item.Token != "runtime-warning")),
                TestCase("variant family instance declarations pass",
                    report.Issues.All(item => item.Token != "skin-compact"))
            };
        }

        private static Dictionary<string, DependentContract> BuildDependentContracts(
            IEnumerable<UssRule> rules)
        {
            var result = new Dictionary<string, DependentContract>(
                StringComparer.Ordinal);
            foreach (var rule in rules)
            {
                foreach (var selector in rule.Selectors)
                {
                    if (SimpleClassSelectorRegex.IsMatch(selector))
                    {
                        continue;
                    }

                    foreach (var token in ClassTokenRegex.Matches(selector).Cast<Match>()
                                 .Select(match => match.Groups["token"].Value)
                                 .Distinct(StringComparer.Ordinal))
                    {
                        if (result.TryGetValue(token, out var contract) == false)
                        {
                            contract = new DependentContract();
                            result[token] = contract;
                        }

                        contract.Selectors.Add(selector);
                        foreach (var declaration in rule.Declarations.Keys.Where(
                                     InstanceOwnedProperties.Contains))
                        {
                            contract.Properties.Add(declaration);
                        }
                    }
                }
            }

            return result;
        }

        private static bool TryGetSimpleClassToken(UssRule rule, out string token)
        {
            token = "";
            if (rule.Selectors.Count != 1 || rule.Declarations.Count == 0)
            {
                return false;
            }

            var match = SimpleClassSelectorRegex.Match(rule.Selectors[0]);
            if (match.Success == false)
            {
                return false;
            }

            token = match.Groups["token"].Value;
            return true;
        }

        private static void RecordIssue(UssRule rule, string token,
            IReadOnlyCollection<UssUsageLocation> authored,
            IReadOnlyCollection<UssUsageLocation> runtime,
            DependentContract dependentContract,
            IEnumerable<KeyValuePair<string, string>> declarations,
            VmAutomationUssStyleAuditReport report)
        {
            var declarationList = declarations.ToList();
            var properties = declarationList.Select(declaration => declaration.Key).ToList();
            var selector = rule.Selectors[0];
            var issue = new VmAutomationUssStyleAuditIssue
            {
                AssetPath = rule.AssetPath,
                Line = rule.Line,
                Selector = selector,
                Token = token,
                Kind = KIND,
                Severity = "error",
                AuthoredUsageCount = authored.Count,
                RuntimeReferenceCount = runtime.Count,
                UsageLocations = authored.Concat(runtime).Take(20)
                    .Select(location => location.ToDictionary()).ToList(),
                RelatedSelectors = dependentContract.Selectors
                    .OrderBy(value => value, StringComparer.Ordinal).ToList(),
                Suppressed = false,
                SuppressionReason = "",
                Message =
                    $"Class selector '{selector}' serves one authored UXML element and has no " +
                    "runtime class reference, but its related selector contract does not own " +
                    $"the instance layout or initial visibility declarations: " +
                    $"{string.Join(", ", properties)}. Move those declarations to the " +
                    "consumer's inline style and retain the class only for its reusable visual " +
                    "or selector-state contract. This ownership error cannot be suppressed."
            };
            foreach (var declaration in declarationList)
            {
                issue.RelatedDeclarations[declaration.Key] = declaration.Value;
            }

            report.Record(issue, false);
        }

        private static VmAutomationUssStyleAuditReport AuditFixture(string ussBody,
            string uxmlBody, IEnumerable<string> runtimeTokens,
            IEnumerable<string> variantFamilyTokens)
        {
            const string ussPath = "Assets/__SingleConsumerInstanceAudit.uss";
            var rules = VmAutomationUssStyleSheetParser.ParseStyleSheet(ussPath, ussBody);
            var usageIndex = new UssUsageIndex();
            var document = new UssAuthoredDocument(
                "Assets/__SingleConsumerInstanceAudit.uxml",
                XDocument.Parse(
                    "<ui:UXML xmlns:ui=\"UnityEngine.UIElements\">" +
                    uxmlBody + "</ui:UXML>", LoadOptions.SetLineInfo));
            usageIndex.Documents.Add(document);
            foreach (var element in document.Elements)
            {
                foreach (var token in element.AuthoredClasses)
                {
                    usageIndex.AddClassUsage(token, document.AssetPath,
                        element.Line, element.Column, element.Name);
                }
            }

            foreach (var token in runtimeTokens)
            {
                usageIndex.AddRuntimeClassReference(token,
                    "Assets/__SingleConsumerInstanceAudit.cs", 1);
            }

            var report = new VmAutomationUssStyleAuditReport(100);
            Audit(rules, usageIndex,
                new HashSet<string>(variantFamilyTokens, StringComparer.Ordinal), report);
            report.SortIssues();
            return report;
        }

        private static Dictionary<string, object> TestCase(string name, bool passed)
        {
            return new Dictionary<string, object>
            {
                { "name", name },
                { "passed", passed }
            };
        }

        private static bool IsMarginProperty(string property)
        {
            return string.Equals(property, "margin", StringComparison.OrdinalIgnoreCase) ||
                   property.StartsWith("margin-", StringComparison.OrdinalIgnoreCase);
        }

        private sealed class DependentContract
        {
            internal readonly HashSet<string> Properties =
                new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            internal readonly HashSet<string> Selectors =
                new HashSet<string>(StringComparer.Ordinal);

            internal bool Owns(string property)
            {
                foreach (var dependentProperty in Properties)
                {
                    if (string.Equals(property, dependentProperty,
                            StringComparison.OrdinalIgnoreCase) ||
                        IsMarginProperty(property) && IsMarginProperty(dependentProperty) &&
                        (string.Equals(property, "margin",
                             StringComparison.OrdinalIgnoreCase) ||
                         string.Equals(dependentProperty, "margin",
                             StringComparison.OrdinalIgnoreCase)))
                    {
                        return true;
                    }
                }

                return false;
            }
        }
    }
}
#endif
