#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using static VMUnityAutomation.Editor.MCPUssAuditContext;
using static VMUnityAutomation.Editor.MCPUssCascadeAuditor;

namespace VMUnityAutomation.Editor
{
    internal static class MCPUssSharedClassDeclarationAuditor
    {
        internal const string KIND = "duplicate-simple-class-declarations";
        private const int MinimumSharedDeclarationCount = 2;

        internal static void Audit(IReadOnlyList<UssRule> rules,
            UssUsageIndex usageIndex, MCPUssStyleAuditReport report)
        {
            var candidates = rules
                .Select(rule => TryCreateCandidate(rule, usageIndex,
                    out var candidate) ? candidate : null)
                .Where(candidate => candidate != null)
                .GroupBy(candidate => candidate.Token, StringComparer.Ordinal)
                .Where(group => group.Count() == 1)
                .Select(group => group.Single())
                .OrderBy(candidate => candidate.Token, StringComparer.Ordinal)
                .ToList();
            var ownershipByDeclaration = BuildDeclarationOwnership(candidates);
            var contracts = ownershipByDeclaration.Values
                .Where(ownership => ownership.Owners.Count > 1)
                .GroupBy(ownership => CreateOwnerKey(ownership.Owners),
                    StringComparer.Ordinal)
                .Select(group => new SharedDeclarationContract(
                    group.First().Owners,
                    group.OrderBy(ownership => ownership.Property,
                        StringComparer.OrdinalIgnoreCase).ToList()))
                .Where(contract => contract.Declarations.Count >=
                                   MinimumSharedDeclarationCount)
                .OrderBy(contract => contract.Owners.Min(owner => owner.Rule.Line))
                .ThenBy(contract => contract.Owners[0].Token,
                    StringComparer.Ordinal)
                .ToList();

            foreach (var contract in contracts)
            {
                RecordIssue(contract, usageIndex, report);
            }
        }

        internal static IReadOnlyList<Dictionary<string, object>> RunSelfTests()
        {
            var repeated = AuditFixture(
                ".normal-toggle { font-size: 34px; margin-top: 18px; " +
                "margin-bottom: 18px; }\n" +
                ".normal-slider { font-size: 34px; margin-top: 18px; " +
                "margin-bottom: 18px; }\n" +
                ".settings-dropdown-field { font-size: 34px; margin-top: 18px; " +
                "margin-bottom: 18px; flex-direction: row; }\n",
                "<ui:VisualElement class=\"normal-toggle\"/>" +
                "<ui:VisualElement class=\"normal-slider\"/>" +
                "<ui:VisualElement class=\"settings-dropdown-field\"/>");
            var extracted = AuditFixture(
                ".settings-field { font-size: 34px; margin-top: 18px; " +
                "margin-bottom: 18px; }\n" +
                ".normal-toggle { width: 101px; }\n" +
                ".normal-slider { height: 102px; }\n" +
                ".settings-dropdown-field { flex-direction: row; }\n",
                "<ui:VisualElement class=\"settings-field normal-toggle\"/>" +
                "<ui:VisualElement class=\"settings-field normal-slider\"/>" +
                "<ui:VisualElement class=\"settings-field settings-dropdown-field\"/>");
            var singleDeclaration = AuditFixture(
                ".first { margin-top: 18px; }\n" +
                ".second { margin-top: 18px; }\n",
                "<ui:VisualElement class=\"first\"/>" +
                "<ui:VisualElement class=\"second\"/>");
            var pseudoStates = AuditFixture(
                ".control:hover { background-color: white; opacity: 1; }\n" +
                ".control:focus { background-color: white; opacity: 1; }\n",
                "<ui:VisualElement class=\"control\"/>");

            var issue = repeated.Issues.SingleOrDefault(item => item.Kind == KIND);
            return new[]
            {
                TestCase("repeated simple-class declaration bundle is an error",
                    issue != null && issue.IsError && repeated.ErrorCount == 1 &&
                    repeated.WarningCount == 0),
                TestCase("error identifies every selector and declaration",
                    issue != null && issue.RelatedSelectors.Count == 3 &&
                    issue.RelatedDeclarations.Count == 3),
                TestCase("extracted shared semantic class passes",
                    extracted.ErrorCount == 0),
                TestCase("one coincidental declaration does not form a bundle",
                    singleDeclaration.ErrorCount == 0),
                TestCase("independent pseudo-state blocks pass",
                    pseudoStates.ErrorCount == 0)
            };
        }

        private static Dictionary<string, DeclarationOwnership>
            BuildDeclarationOwnership(IEnumerable<Candidate> candidates)
        {
            var result = new Dictionary<string, DeclarationOwnership>(
                StringComparer.Ordinal);
            foreach (var candidate in candidates)
            {
                foreach (var declaration in candidate.Rule.Declarations)
                {
                    var key = CreateDeclarationKey(declaration.Key,
                        declaration.Value);
                    if (result.TryGetValue(key, out var ownership) == false)
                    {
                        ownership = new DeclarationOwnership(declaration.Key,
                            declaration.Value);
                        result[key] = ownership;
                    }

                    ownership.Owners.Add(candidate);
                }
            }

            return result;
        }

        private static bool TryCreateCandidate(UssRule rule,
            UssUsageIndex usageIndex, out Candidate candidate)
        {
            candidate = null;
            if (rule.Selectors.Count != 1 || rule.Declarations.Count == 0 ||
                TryParseSimpleSelector(rule.Selectors[0], out var selector) == false ||
                string.IsNullOrWhiteSpace(selector.TypeName) == false ||
                string.IsNullOrWhiteSpace(selector.Id) == false ||
                selector.ClassNames.Count != 1)
            {
                return false;
            }

            var token = selector.ClassNames[0];
            if (usageIndex.GetClassUsages(token).Count == 0 &&
                usageIndex.GetRuntimeClassAssignments(token).Count == 0)
            {
                return false;
            }

            candidate = new Candidate(rule, token);
            return true;
        }

        private static void RecordIssue(SharedDeclarationContract contract,
            UssUsageIndex usageIndex, MCPUssStyleAuditReport report)
        {
            var primary = contract.Owners
                .OrderBy(owner => owner.Rule.Line)
                .ThenBy(owner => owner.Token, StringComparer.Ordinal)
                .First();
            var authored = DistinctLocations(contract.Owners.SelectMany(owner =>
                usageIndex.GetClassUsages(owner.Token)));
            var runtime = DistinctLocations(contract.Owners.SelectMany(owner =>
                usageIndex.GetRuntimeClassReferences(owner.Token)));
            var selectors = contract.Owners
                .Select(owner => owner.Rule.Selectors[0])
                .OrderBy(selector => selector, StringComparer.Ordinal)
                .ToList();
            var properties = contract.Declarations
                .Select(declaration => declaration.Property)
                .OrderBy(property => property, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var issue = new MCPUssStyleAuditIssue
            {
                AssetPath = primary.Rule.AssetPath,
                Line = primary.Rule.Line,
                Selector = primary.Rule.Selectors[0],
                Token = primary.Token,
                Kind = KIND,
                Severity = "error",
                AuthoredUsageCount = authored.Count,
                RuntimeReferenceCount = runtime.Count,
                UsageLocations = authored.Concat(runtime).Take(20)
                    .Select(location => location.ToDictionary()).ToList(),
                RelatedSelectors = selectors,
                Message =
                    $"Simple class selectors {string.Join(", ", selectors)} repeat the same " +
                    $"{properties.Count} declarations ({string.Join(", ", properties)}). " +
                    "This is a hard USS ownership error: move the shared declarations to one " +
                    "semantic class assigned to every authored or runtime consumer, and leave " +
                    "only selector-specific declarations in these selectors."
            };
            foreach (var declaration in contract.Declarations)
            {
                issue.RelatedDeclarations[declaration.Property] = declaration.Value;
            }

            report.Record(issue, false);
        }

        private static List<UssUsageLocation> DistinctLocations(
            IEnumerable<UssUsageLocation> locations)
        {
            return locations.GroupBy(location =>
                    $"{location.Path}\u001f{location.Line}\u001f{location.Column}",
                    StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .OrderBy(location => location.Path, StringComparer.Ordinal)
                .ThenBy(location => location.Line)
                .ThenBy(location => location.Column)
                .ToList();
        }

        private static string CreateDeclarationKey(string property, string value)
        {
            return property.Trim().ToLowerInvariant() + "\u001f" +
                   Regex.Replace(value.Trim(), @"\s+", " ").ToLowerInvariant();
        }

        private static string CreateOwnerKey(IEnumerable<Candidate> owners)
        {
            return string.Join("\u001e", owners.Select(owner => owner.Token));
        }

        private static MCPUssStyleAuditReport AuditFixture(string ussBody,
            string uxmlBody)
        {
            const string ussPath = "Assets/__SharedClassDeclarationAudit.uss";
            var rules = MCPUssStyleSheetParser.ParseStyleSheet(ussPath, ussBody);
            var usageIndex = new UssUsageIndex();
            var document = new UssAuthoredDocument(
                "Assets/__SharedClassDeclarationAudit.uxml",
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

            var report = new MCPUssStyleAuditReport(100);
            Audit(rules, usageIndex, report);
            report.SortIssues();
            return report;
        }

        private static Dictionary<string, object> TestCase(string name,
            bool passed)
        {
            return new Dictionary<string, object>
            {
                { "name", name },
                { "passed", passed }
            };
        }

        private sealed class Candidate
        {
            internal readonly UssRule Rule;
            internal readonly string Token;

            internal Candidate(UssRule rule, string token)
            {
                Rule = rule;
                Token = token;
            }
        }

        private sealed class DeclarationOwnership
        {
            internal readonly string Property;
            internal readonly string Value;
            internal readonly List<Candidate> Owners = new List<Candidate>();

            internal DeclarationOwnership(string property, string value)
            {
                Property = property;
                Value = value;
            }
        }

        private sealed class SharedDeclarationContract
        {
            internal readonly IReadOnlyList<Candidate> Owners;
            internal readonly IReadOnlyList<DeclarationOwnership> Declarations;

            internal SharedDeclarationContract(IReadOnlyList<Candidate> owners,
                IReadOnlyList<DeclarationOwnership> declarations)
            {
                Owners = owners;
                Declarations = declarations;
            }
        }
    }
}
#endif
