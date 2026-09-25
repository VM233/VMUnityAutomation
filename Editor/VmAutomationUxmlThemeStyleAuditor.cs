#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using UnityEditor;

namespace VMUnityAutomation.Editor
{
    internal static class VmAutomationUxmlThemeStyleAuditor
    {
        internal const string Kind = "duplicate-theme-stylesheet";

        private static readonly Regex DefaultThemeGuidRegex = new Regex(
            @"\bm_DefaultRuntimeTheme:\s*\{[^}\r\n]*\bguid:\s*(?<guid>[0-9a-f]{32})",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex ImportRegex = new Regex(
            @"@import\s+url\(\s*['""']?(?<path>[^)'""']+)['""']?\s*\)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex CommentRegex = new Regex(
            @"/\*.*?\*/", RegexOptions.Compiled | RegexOptions.Singleline);

        internal static void AuditProject(IEnumerable<string> uxmlPaths,
            VmAutomationUxmlLayoutAuditReport report)
        {
            string settingsPath = Path.Combine(
                VmAutomationUIToolkitAuditUtility.GetProjectRoot(),
                "ProjectSettings/UIToolkitProjectSettings.asset");
            if (!File.Exists(settingsPath))
                return;

            Match themeGuid = DefaultThemeGuidRegex.Match(File.ReadAllText(settingsPath));
            if (!themeGuid.Success ||
                themeGuid.Groups["guid"].Value == new string('0', 32))
                return;

            string themePath = VmAutomationUIToolkitAuditUtility.NormalizeAssetPath(
                AssetDatabase.GUIDToAssetPath(themeGuid.Groups["guid"].Value));
            if (string.IsNullOrEmpty(themePath))
            {
                report.Errors.Add("The project default UI Toolkit theme GUID does not resolve: " +
                                  themeGuid.Groups["guid"].Value);
                return;
            }

            var importedStyles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                CollectImportedStyles(themePath, File.ReadAllText, importedStyles,
                    new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            }
            catch (Exception exception)
            {
                report.Errors.Add($"Failed to read default theme imports in '{themePath}': " +
                                  exception.Message);
                return;
            }

            foreach (string path in uxmlPaths)
            {
                try
                {
                    var document = XDocument.Parse(
                        File.ReadAllText(VmAutomationUIToolkitAuditUtility.ToFullPath(path)),
                        LoadOptions.SetLineInfo);
                    AuditDocument(path, document, themePath, importedStyles, report);
                }
                catch (Exception exception)
                {
                    report.Errors.Add($"Failed to audit theme styles in '{path}': " +
                                      exception.Message);
                }
            }
        }

        private static void CollectImportedStyles(string stylesheetPath,
            Func<string, string> readText, ISet<string> importedStyles,
            ISet<string> visited)
        {
            if (!visited.Add(stylesheetPath))
                return;

            string stylesheetText = CommentRegex.Replace(readText(
                VmAutomationUIToolkitAuditUtility.ToFullPath(stylesheetPath)), "");
            foreach (Match match in ImportRegex.Matches(stylesheetText))
            {
                string source = match.Groups["path"].Value.Trim();
                if (source.StartsWith("unity-theme://", StringComparison.OrdinalIgnoreCase))
                    continue;
                string importedPath =
                    VmAutomationUxmlLayoutContractIndexer.ResolveStyleReference(
                        source, stylesheetPath);
                if (string.IsNullOrEmpty(importedPath))
                    continue;

                importedStyles.Add(importedPath);
                CollectImportedStyles(importedPath, readText, importedStyles, visited);
            }
        }

        private static void AuditDocument(string uxmlPath, XDocument document,
            string themePath, ISet<string> importedStyles,
            VmAutomationUxmlLayoutAuditReport report)
        {
            foreach (XElement style in document.Descendants()
                         .Where(element => element.Name.LocalName == "Style"))
            {
                string directPath =
                    VmAutomationUxmlLayoutContractIndexer.ResolveStyleReference(
                        (string)style.Attribute("src"), uxmlPath);
                if (string.IsNullOrEmpty(directPath) ||
                    !importedStyles.Contains(directPath))
                    continue;

                report.Record(new VmAutomationUxmlLayoutAuditIssue
                {
                    AssetPath = uxmlPath,
                    Line = (style as IXmlLineInfo)?.LineNumber ?? 0,
                    Element = "<Style>",
                    Kind = Kind,
                    Severity = "error",
                    Message = $"'{directPath}' is loaded both by this UXML and by " +
                              $"the project default theme '{themePath}' through @import. " +
                              "Keep one stylesheet owner to avoid UI Builder " +
                              "SelectorAccelerationCache duplicate associations."
                }, false);
            }
        }

        internal static IReadOnlyList<Dictionary<string, object>> RunSelfTests()
        {
            var cases = new List<Dictionary<string, object>>();
            string theme = "Assets/UI/Default Style.tss";
            string text = "Assets/UI/Text Style.uss";
            string nested = "Assets/UI/Nested.uss";
            var content = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { VmAutomationUIToolkitAuditUtility.ToFullPath(theme),
                    "@import url(\"unity-theme://default\"); @import url(\"Nested.uss\");" },
                { VmAutomationUIToolkitAuditUtility.ToFullPath(nested),
                    "@import url(\"Text Style.uss\");" },
                { VmAutomationUIToolkitAuditUtility.ToFullPath(text),
                    ".normal-label { font-size: 24px; }" }
            };
            var imports = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            CollectImportedStyles(theme, path => content[path], imports,
                new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            AddCase(cases, "default theme follows nested local USS imports",
                imports.SetEquals(new[] { nested, text }));

            var duplicate = new VmAutomationUxmlLayoutAuditReport(10);
            AuditDocument("Assets/UI/Page.uxml", XDocument.Parse(
                "<UXML><Style src=\"project://database/Assets/UI/Text%20Style.uss?guid=x\"/></UXML>",
                LoadOptions.SetLineInfo), theme, imports, duplicate);
            AddCase(cases, "UXML direct and theme imported style is an error",
                duplicate.ErrorCount == 1 && duplicate.Issues[0].Kind == Kind);

            var separate = new VmAutomationUxmlLayoutAuditReport(10);
            AuditDocument("Assets/UI/Page.uxml", XDocument.Parse(
                "<UXML><Style src=\"Assets/UI/Button.uss\"/></UXML>"),
                theme, imports, separate);
            AddCase(cases, "UXML style outside the theme is allowed",
                separate.ErrorCount == 0);
            return cases;
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
    }
}
#endif
