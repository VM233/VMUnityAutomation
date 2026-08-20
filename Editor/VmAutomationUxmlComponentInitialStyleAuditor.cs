#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using static VMUnityAutomation.Editor.VmAutomationUssCascadeAuditor;
using static VMUnityAutomation.Editor.VmAutomationUssStyleSheetParser;

namespace VMUnityAutomation.Editor
{
    internal static class VmAutomationUxmlComponentInitialStyleAuditor
    {
        private static readonly Regex SuppressionRegex =
            new Regex(
                @"^\s*uxml-layout-audit:\s*allow-redundant-inline\s+(?<reason>.+?)\s*$",
                RegexOptions.Compiled | RegexOptions.IgnoreCase |
                RegexOptions.Singleline);

        internal static void Audit(string assetPath, XDocument document,
            Func<XElement, IReadOnlyDictionary<string, string>> resolveStylesheetStyle,
            VmAutomationUxmlLayoutAuditReport report, bool includeSuppressed)
        {
            foreach (var element in document.Descendants().Where(IsAuthoredVisualElement))
            {
                var inlineDeclarations = ParseDeclarations(
                    GetAttributeValue(element, "style"));
                if (inlineDeclarations.Count == 0)
                {
                    continue;
                }

                var identity = VmAutomationUIToolkitElementStyleBaseline.Resolve(
                    element.Name.NamespaceName, element.Name.LocalName);
                if (identity.ImplicitClasses.Count > 0)
                {
                    continue;
                }

                var stylesheetDeclarations = resolveStylesheetStyle(element);
                var redundant = inlineDeclarations
                    .Where(declaration =>
                        stylesheetDeclarations.ContainsKey(declaration.Key) == false &&
                        VmAutomationUIToolkitInitialStyleComparer.IsInitialValue(
                            declaration.Key, declaration.Value))
                    .OrderBy(declaration => declaration.Key,
                        StringComparer.OrdinalIgnoreCase)
                    .ToDictionary(declaration => declaration.Key,
                        declaration => declaration.Value,
                        StringComparer.OrdinalIgnoreCase);
                if (redundant.Count == 0)
                {
                    continue;
                }

                RecordIssue(assetPath, element, identity, redundant, report,
                    includeSuppressed);
            }
        }

        internal static IReadOnlyList<Dictionary<string, object>> RunSelfTests()
        {
            var cases = new List<Dictionary<string, object>>();
            var defaultColumn = AuditFixture(
                "<ui:VisualElement name=\"Tree\" style=\"flex-direction: column;\"/>",
                EmptyStylesheetStyle, false);
            AddSelfTestCase(cases, "VisualElement inline default column warns",
                defaultColumn.WarningCount == 1 &&
                defaultColumn.Issues.Single().Kind ==
                "redundant-inline-declaration" &&
                defaultColumn.Issues.Single().InlineDeclarations
                    .ContainsKey("flex-direction"));

            var engineDefaults = AuditFixture(
                "<ui:VisualElement style=\"margin-top: 0; padding-left: 0px; " +
                "width: auto; flex-shrink: 1;\"/>",
                EmptyStylesheetStyle, false);
            AddSelfTestCase(cases, "engine inline defaults warn as one finding",
                engineDefaults.WarningCount == 1 &&
                engineDefaults.Issues.Single().InlineDeclarations.Keys
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .SequenceEqual(new[]
                    {
                        "flex-shrink", "margin-top", "padding-left", "width"
                    }));

            var authoredRow = AuditFixture(
                "<ui:VisualElement style=\"flex-direction: row;\"/>",
                EmptyStylesheetStyle, false);
            AddSelfTestCase(cases, "non-default inline row passes",
                authoredRow.WarningCount == 0);

            var stylesheetReset = AuditFixture(
                "<ui:VisualElement style=\"flex-direction: column;\"/>",
                _ => new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    { "flex-direction", "row" }
                }, false);
            AddSelfTestCase(cases,
                "inline column that resets a loaded row passes",
                stylesheetReset.WarningCount == 0);

            var implicitClassControl = AuditFixture(
                "<ui:Button style=\"flex-direction: column;\"/>",
                EmptyStylesheetStyle, false);
            AddSelfTestCase(cases,
                "control with implicit component classes remains TSS-owned",
                implicitClassControl.WarningCount == 0);

            var suppressed = AuditFixture(
                $"<!-- {VmAutomationUxmlLayoutAuditor.REDUNDANT_INLINE_SUPPRESSION_MARKER} " +
                "fixture documents an explicit reset -->" +
                "<ui:VisualElement style=\"flex-direction: column;\"/>",
                EmptyStylesheetStyle, true);
            AddSelfTestCase(cases,
                "reasoned component-initial suppression is retained",
                suppressed.WarningCount == 0 &&
                suppressed.SuppressedCount == 1 &&
                suppressed.Issues.Single().Suppressed);

            return cases;
        }

        private static void RecordIssue(string assetPath, XElement element,
            VmAutomationUIToolkitElementStyleBaseline.ElementIdentity identity,
            Dictionary<string, string> redundant,
            VmAutomationUxmlLayoutAuditReport report, bool includeSuppressed)
        {
            var name = GetAttributeValue(element, "name");
            var elementLabel = string.IsNullOrWhiteSpace(name)
                ? $"<{element.Name.LocalName}>"
                : $"#{name}";
            var sourcePath = $"unity-initial://{identity.ComponentTypeName}";
            var sourceSelector = $"<{identity.ComponentTypeName} initial style>";
            var suppressionReason = GetSuppressionReason(element);
            var issue = new VmAutomationUxmlLayoutAuditIssue
            {
                AssetPath = assetPath,
                Line = GetLineNumber(element),
                Element = elementLabel,
                ElementName = name,
                Kind = "redundant-inline-declaration",
                Axis = "layout",
                FixedProperties = redundant.Keys.ToList(),
                InlineDeclarations = redundant,
                Suppressed = string.IsNullOrWhiteSpace(suppressionReason) == false,
                SuppressionReason = suppressionReason,
                Message =
                    $"Inline style {FormatDeclarations(redundant)} on {elementLabel} " +
                    $"repeats the Unity engine initial style of " +
                    $"{identity.ComponentTypeName}. Remove the redundant declaration; " +
                    "keep an explicit reset only when a loaded component class or " +
                    "stylesheet supplies a different effective value."
            };
            foreach (var property in redundant.Keys)
            {
                issue.StylesheetRules.Add(new Dictionary<string, object>
                {
                    { "property", property },
                    { "selector", sourceSelector },
                    { "sourcePath", sourcePath },
                    { "sourceKind", "initial-style" }
                });
            }

            report.Record(issue, includeSuppressed);
        }

        private static VmAutomationUxmlLayoutAuditReport AuditFixture(string element,
            Func<XElement, IReadOnlyDictionary<string, string>> resolveStylesheetStyle,
            bool includeSuppressed)
        {
            var document = XDocument.Parse(
                "<ui:UXML xmlns:ui=\"UnityEngine.UIElements\">" + element +
                "</ui:UXML>", LoadOptions.PreserveWhitespace | LoadOptions.SetLineInfo);
            var report = new VmAutomationUxmlLayoutAuditReport(100);
            Audit("Assets/__ComponentInitialStyleAuditSelfTest.uxml", document,
                resolveStylesheetStyle, report, includeSuppressed);
            report.SortIssues();
            return report;
        }

        private static IReadOnlyDictionary<string, string> EmptyStylesheetStyle(
            XElement element)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        private static bool IsAuthoredVisualElement(XElement element)
        {
            switch (element.Name.LocalName)
            {
                case "UXML":
                case "Style":
                case "Template":
                case "AttributeOverrides":
                case "Bindings":
                    return false;
                default:
                    return true;
            }
        }

        private static string GetSuppressionReason(XElement element)
        {
            var previous = element.NodesBeforeSelf().Reverse().FirstOrDefault(node =>
                !(node is XText text) || string.IsNullOrWhiteSpace(text.Value) == false);
            if (!(previous is XComment comment))
            {
                return "";
            }

            var match = SuppressionRegex.Match(comment.Value);
            return match.Success ? match.Groups["reason"].Value.Trim() : "";
        }

        private static string FormatDeclarations(
            IReadOnlyDictionary<string, string> declarations)
        {
            return string.Join("; ", declarations.Select(declaration =>
                $"{declaration.Key}: {declaration.Value}"));
        }

        private static void AddSelfTestCase(
            ICollection<Dictionary<string, object>> cases, string name, bool passed)
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
