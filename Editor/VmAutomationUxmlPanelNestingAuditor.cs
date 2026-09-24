#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using static VMUnityAutomation.Editor.VmAutomationUxmlLayoutAuditor;

namespace VMUnityAutomation.Editor
{
    internal static class VmAutomationUxmlPanelNestingAuditor
    {
        internal const string Kind = "nested-ui-panel-uxml";

        private static readonly Regex GuidRegex = new Regex(
            @"\bguid\s*[:=]\s*(?<guid>[0-9a-f]{32})\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex SourceAssetRegex = new Regex(
            @"\bsourceAsset:\s*\{[^}\r\n]*\bguid:\s*(?<guid>[0-9a-f]{32})\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        internal static void AuditProject(IEnumerable<string> uxmlPaths,
            VmAutomationUxmlLayoutAuditReport report)
        {
            var pathsByGuid = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string path in uxmlPaths)
            {
                string metaPath = VmAutomationUIToolkitAuditUtility.ToFullPath(path) + ".meta";
                if (File.Exists(metaPath) == false)
                    continue;

                Match guid = GuidRegex.Match(File.ReadAllText(metaPath));
                if (guid.Success)
                    pathsByGuid[guid.Groups["guid"].Value] = path;
            }
            var guidsByPath = pathsByGuid.ToDictionary(
                pair => pair.Value.Replace('\\', '/'), pair => pair.Key,
                StringComparer.OrdinalIgnoreCase);

            string assetsPath = VmAutomationUIToolkitAuditUtility.ToFullPath("Assets");
            var panelGuids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string prefabPath in Directory.EnumerateFiles(assetsPath,
                         "*.prefab", SearchOption.AllDirectories))
            {
                foreach (Match match in SourceAssetRegex.Matches(File.ReadAllText(prefabPath)))
                    panelGuids.Add(match.Groups["guid"].Value);
            }

            var documents = new Dictionary<string, XDocument>(StringComparer.OrdinalIgnoreCase);
            foreach (string panelGuid in panelGuids.OrderBy(guid => guid, StringComparer.Ordinal))
            {
                if (pathsByGuid.TryGetValue(panelGuid, out string panelPath) == false)
                    continue;

                try
                {
                    AuditPanel(panelGuid, pathsByGuid, guidsByPath, panelGuids,
                        documents, report);
                }
                catch (Exception exception)
                {
                    report.Errors.Add($"Failed to audit panel UXML '{panelPath}': {exception.Message}");
                }
            }
        }

        private static void AuditPanel(string panelGuid,
            IReadOnlyDictionary<string, string> pathsByGuid,
            IReadOnlyDictionary<string, string> guidsByPath,
            IReadOnlyCollection<string> panelGuids,
            IDictionary<string, XDocument> documents,
            VmAutomationUxmlLayoutAuditReport report)
        {
            string panelPath = pathsByGuid[panelGuid];
            var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            AuditImports(panelGuid, panelPath, null, pathsByGuid, guidsByPath,
                panelGuids, documents, visited, report);
        }

        private static void AuditImports(string currentGuid, string panelPath,
            XElement originTemplate,
            IReadOnlyDictionary<string, string> pathsByGuid,
            IReadOnlyDictionary<string, string> guidsByPath,
            IReadOnlyCollection<string> panelGuids,
            IDictionary<string, XDocument> documents,
            ISet<string> visited, VmAutomationUxmlLayoutAuditReport report)
        {
            if (visited.Add(currentGuid) == false)
                return;

            string currentPath = pathsByGuid[currentGuid];
            if (documents.TryGetValue(currentGuid, out XDocument document) == false)
            {
                document = XDocument.Parse(
                    File.ReadAllText(VmAutomationUIToolkitAuditUtility.ToFullPath(currentPath)),
                    LoadOptions.SetLineInfo);
                documents[currentGuid] = document;
            }

            foreach (XElement template in document.Root.Elements()
                         .Where(element => element.Name.LocalName == "Template"))
            {
                string childGuid = ResolveTemplateGuid((string)template.Attribute("src"), guidsByPath);
                if (childGuid == null ||
                    pathsByGuid.TryGetValue(childGuid, out string childPath) == false)
                    continue;

                XElement sourceTemplate = originTemplate ?? template;
                if (panelGuids.Contains(childGuid))
                {
                    report.Record(new VmAutomationUxmlLayoutAuditIssue
                    {
                        AssetPath = panelPath,
                        Line = GetLineNumber(sourceTemplate),
                        Element = $"<Template name=\"{(string)sourceTemplate.Attribute("name")}\">",
                        Kind = Kind,
                        Severity = "error",
                        Message = $"Panel UXML '{panelPath}' imports panel UXML '{childPath}' " +
                                  $"through '{currentPath}'. Give each panel its own UIDocument " +
                                  "and open it through the panel manager."
                    }, false);
                    continue;
                }

                AuditImports(childGuid, panelPath, sourceTemplate,
                    pathsByGuid, guidsByPath, panelGuids, documents, visited, report);
            }

            visited.Remove(currentGuid);
        }

        private static string ResolveTemplateGuid(string source,
            IReadOnlyDictionary<string, string> guidsByPath)
        {
            if (string.IsNullOrWhiteSpace(source))
                return null;

            Match guid = GuidRegex.Match(source);
            if (guid.Success)
                return guid.Groups["guid"].Value;

            const string projectPrefix = "project://database/";
            string path = source.StartsWith(projectPrefix, StringComparison.OrdinalIgnoreCase)
                ? source.Substring(projectPrefix.Length)
                : source;
            int suffix = path.IndexOfAny(new[] { '?', '#' });
            if (suffix >= 0)
                path = path.Substring(0, suffix);
            path = Uri.UnescapeDataString(path).Replace('\\', '/');
            return guidsByPath.TryGetValue(path, out string found) ? found : null;
        }

        internal static IReadOnlyList<Dictionary<string, object>> RunSelfTests()
        {
            const string main = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
            const string log = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
            const string card = "cccccccccccccccccccccccccccccccc";
            var cases = new List<Dictionary<string, object>>();
            var paths = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { main, "Assets/Main.uxml" },
                { log, "Assets/Log.uxml" },
                { card, "Assets/Card.uxml" }
            };
            var panels = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { main, log };

            var direct = Fixture(paths, panels, main,
                Uxml($"<ui:Template name=\"Log\" src=\"guid={log}\"/>" +
                     "<ui:Instance template=\"Log\"/>"),
                Uxml("<ui:VisualElement/>"), Uxml("<ui:VisualElement/>"));
            AddCase(cases, "panel nested directly is an error",
                direct.ErrorCount == 1 && direct.Issues[0].Kind == Kind);

            var indirect = Fixture(paths, panels, main,
                Uxml($"<ui:Template name=\"Card\" src=\"guid={card}\"/>" +
                     "<ui:Instance template=\"Card\"/>"),
                Uxml("<ui:VisualElement/>"),
                Uxml($"<ui:Template name=\"Log\" src=\"guid={log}\"/>" +
                     "<ui:Instance template=\"Log\"/>"));
            AddCase(cases, "panel nested through a shared template is an error",
                indirect.ErrorCount == 1 && indirect.Issues[0].Kind == Kind);

            var shared = Fixture(paths, panels, main,
                Uxml($"<ui:Template name=\"Card\" src=\"guid={card}\"/>" +
                     "<ui:Instance template=\"Card\"/>"),
                Uxml("<ui:VisualElement/>"), Uxml("<ui:VisualElement/>"));
            AddCase(cases, "reusable card template is allowed", shared.ErrorCount == 0);

            var unused = Fixture(paths, panels, main,
                Uxml($"<ui:Template name=\"Log\" src=\"guid={log}\"/>"),
                Uxml("<ui:VisualElement/>"), Uxml("<ui:VisualElement/>"));
            AddCase(cases, "unused panel template declaration is an error",
                unused.ErrorCount == 1 && unused.Issues[0].Kind == Kind);

            var indirectUnused = Fixture(paths, panels, main,
                Uxml($"<ui:Template name=\"Card\" src=\"guid={card}\"/>"),
                Uxml("<ui:VisualElement/>"),
                Uxml($"<ui:Template name=\"Log\" src=\"guid={log}\"/>"));
            AddCase(cases, "indirect unused panel import is an error",
                indirectUnused.ErrorCount == 1 && indirectUnused.Issues[0].Kind == Kind);

            var pathReference = Fixture(paths, panels, main,
                Uxml("<ui:Template name=\"Log\" src=\"Assets/Log.uxml\"/>" +
                     "<ui:Instance template=\"Log\"/>"),
                Uxml("<ui:VisualElement/>"), Uxml("<ui:VisualElement/>"));
            AddCase(cases, "panel template referenced by path is an error",
                pathReference.ErrorCount == 1 && pathReference.Issues[0].Kind == Kind);
            return cases;
        }

        private static VmAutomationUxmlLayoutAuditReport Fixture(
            IReadOnlyDictionary<string, string> paths,
            IReadOnlyCollection<string> panels, string rootGuid,
            string mainDocument, string logDocument, string cardDocument)
        {
            var documents = new Dictionary<string, XDocument>(StringComparer.OrdinalIgnoreCase)
            {
                { "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", XDocument.Parse(mainDocument, LoadOptions.SetLineInfo) },
                { "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb", XDocument.Parse(logDocument, LoadOptions.SetLineInfo) },
                { "cccccccccccccccccccccccccccccccc", XDocument.Parse(cardDocument, LoadOptions.SetLineInfo) }
            };
            var report = new VmAutomationUxmlLayoutAuditReport(100);
            var guidsByPath = paths.ToDictionary(pair => pair.Value, pair => pair.Key,
                StringComparer.OrdinalIgnoreCase);
            AuditPanel(rootGuid, paths, guidsByPath, panels, documents, report);
            return report;
        }

        private static string Uxml(string body) =>
            "<ui:UXML xmlns:ui=\"UnityEngine.UIElements\">" + body + "</ui:UXML>";

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
