#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using static VMUnityAutomation.Editor.VmAutomationUxmlLayoutAuditor;

namespace VMUnityAutomation.Editor
{
    internal static class VmAutomationUxmlLocalizationAuditor
    {
        private const string InvariantKind = "allow-locale-invariant";
        internal const string INVARIANT_SUPPRESSION_MARKER = "uxml-layout-audit: " + InvariantKind;
        private static readonly string[] TextAttributes = { "text", "label", "tooltip", "value" };
        private static readonly Regex OwnerMarker = new Regex(
            @"^\s*(?:uxml-layout-audit:\s*(?<kind>allow-runtime-text|" + InvariantKind +
            @")\s+|ui-builder-preview:\s*(?<kind>runtime-replaced)\s+)(?<reason>\S[\s\S]*?)\s*$",
            RegexOptions.CultureInvariant);

        internal static void Audit(string path, XDocument document,
            VmAutomationUxmlLayoutAuditReport report, bool includeSuppressed)
        {
            Visit(path, document.Root, false, report, includeSuppressed);
        }

        private static void Visit(string path, XElement parent, bool inheritedPreview,
            VmAutomationUxmlLayoutAuditReport report, bool includeSuppressed)
        {
            bool preview = false;
            bool runtime = false;
            string invariantReason = null;
            foreach (XNode node in parent.Nodes())
            {
                if (node is XText whitespace && string.IsNullOrWhiteSpace(whitespace.Value)) continue;
                if (node is XComment comment)
                {
                    Match match = OwnerMarker.Match(comment.Value);
                    if (match.Success)
                    {
                        string kind = match.Groups["kind"].Value;
                        preview |= kind == "runtime-replaced";
                        runtime |= kind == "allow-runtime-text";
                        if (kind == InvariantKind) invariantReason = match.Groups["reason"].Value;
                    }
                    continue;
                }
                if (node is XElement element)
                {
                    if (!inheritedPreview && !preview && !runtime)
                        AuditElement(path, element, invariantReason, report, includeSuppressed);
                    Visit(path, element, inheritedPreview || preview, report, includeSuppressed);
                }
                preview = runtime = false;
                invariantReason = null;
            }
        }

        private static void AuditElement(string path, XElement element, string invariantReason,
            VmAutomationUxmlLayoutAuditReport report, bool includeSuppressed)
        {
            foreach (string attribute in TextAttributes)
            {
                if (attribute == "value" && !element.Name.LocalName.EndsWith("TextField", StringComparison.Ordinal))
                    continue;
                string value = (string)element.Attribute(attribute);
                if (string.IsNullOrEmpty(value) || !value.Any(char.IsLetter)) continue;
                bool bound = element.Elements().Where(child => child.Name.LocalName == "Bindings")
                    .SelectMany(child => child.Descendants()).Any(binding =>
                        (string)binding.Attribute("property") == attribute);
                if (bound) continue;
                report.Record(new VmAutomationUxmlLayoutAuditIssue
                {
                    AssetPath = path,
                    Line = GetLineNumber(element),
                    Element = element.Name.LocalName,
                    ElementName = (string)element.Attribute("name"),
                    Kind = "unlocalized-player-text",
                    Severity = "error",
                    AttributeName = attribute,
                    AttributeValue = value,
                    Suppressed = invariantReason != null,
                    SuppressionReason = invariantReason,
                    Message = $"Authored '{attribute}' literal '{value}' has no text owner. " +
                              "Bind fixed player text to Localization. Declare reasoned runtime ownership " +
                              "for replaced previews, or an adjacent allow-locale-invariant reason for " +
                              "deliberately unchanged command syntax, identifiers or names."
                }, includeSuppressed);
            }
        }

        internal static IEnumerable<Dictionary<string, object>> RunSelfTests()
        {
            var syntax = (string[])new VmAutomationUxmlLayoutAuditReport(1).ToDictionary()["suppressionSyntax"];
            yield return new Dictionary<string, object>
            {
                { "name", "public audit response advertises the locale-invariant exception" },
                { "passed", syntax.Contains($"<!-- {INVARIANT_SUPPRESSION_MARKER} <reason> -->") }
            };
            var fixtures = new[]
            {
                ("console title is rejected", "<ui:Label text=\"CONSOLE\"/>", 1),
                ("instruction prose is rejected", "<ui:Label text=\"Enter to execute\"/>", 1),
                ("translated literals still need an owner", "<ui:Button text=\"确定\"/>", 1),
                ("command syntax is not guessed invariant", "<ui:Label text=\"/help\"/>", 1),
                ("reasoned protocol literal is allowed", "<!-- uxml-layout-audit: allow-locale-invariant This is the literal command token. --><ui:Label text=\"/help\"/>", 0),
                ("empty invariant reason is rejected", "<!-- uxml-layout-audit: allow-locale-invariant --><ui:Label text=\"CONSOLE\"/>", 1),
                ("invariance does not leak to a sibling", "<!-- uxml-layout-audit: allow-locale-invariant Protocol token. --><ui:Label text=\"/help\"/><ui:Label text=\"CONSOLE\"/>", 1),
                ("invariance does not exempt a subtree", "<!-- uxml-layout-audit: allow-locale-invariant Protocol container. --><ui:VisualElement><ui:Label text=\"CONSOLE\"/></ui:VisualElement>", 1),
                ("runtime generated preview has an owner", "<!-- ui-builder-preview: runtime-replaced Results.ClearAndRebuild --><ui:VisualElement><ui:Label text=\"Gold = 12\"/></ui:VisualElement>", 0),
                ("runtime owner is local to its element", "<!-- uxml-layout-audit: allow-runtime-text NameModifier.Open replaces this name --><ui:Label text=\"Warrior\"/><ui:Label text=\"CONSOLE\"/>", 1),
                ("localized binding has no literal", "<ui:Label><Bindings><LocalizedString property=\"text\" table=\"UI\" entry=\"ConsoleTitle\"/></Bindings></ui:Label>", 0),
                ("symbols and numeric values are invariant", "<ui:Label text=\"×\"/><ui:Label text=\"0 / 100\"/>", 0),
                ("input command preview needs an owner", "<ui:TextField value=\"/give\"/>", 1),
                ("non-text Boolean value is not prose", "<ui:Toggle value=\"true\"/>", 0)
            };
            foreach (var fixture in fixtures)
            {
                var report = new VmAutomationUxmlLayoutAuditReport(20);
                var document = XDocument.Parse("<ui:UXML xmlns:ui=\"UnityEngine.UIElements\">" +
                                               fixture.Item2 + "</ui:UXML>", LoadOptions.SetLineInfo);
                Audit("Assets/LocalizationFixture.uxml", document, report, false);
                yield return new Dictionary<string, object>
                {
                    { "name", fixture.Item1 }, { "passed", report.ErrorCount == fixture.Item3 }
                };
            }
        }
    }
}
#endif
