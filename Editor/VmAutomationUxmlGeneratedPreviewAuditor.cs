#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using UnityEditor;
using UnityEngine;

namespace VMUnityAutomation.Editor
{
    internal static class VmAutomationUxmlGeneratedPreviewAuditor
    {
        internal sealed class Target
        {
            internal string ElementName;
            internal string Producer;
            internal string PrefabPath;
            internal bool RequiresImage;
        }

        private static readonly Regex GuidRegex = new Regex(
            @"\bguid\s*[:=]\s*(?<guid>[0-9a-f]{32})\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex SourceAssetRegex = new Regex(
            @"\bsourceAsset:\s*\{[^}\r\n]*\bguid:\s*(?<guid>[0-9a-f]{32})\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex TemplateAssetRegex = new Regex(
            @"^  (?:entryAsset|[A-Za-z0-9_]*Template):\s*\{[^}\r\n]*\bguid:\s*(?<guid>[0-9a-f]{32})\b",
            RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.IgnoreCase);
        private static readonly Regex PathRegex = new Regex(
            @"^  (?<field>[A-Za-z0-9_]*[Cc]ontainer[Pp]ath|iconPath):\s*\r?\n    names:\s*\r?\n(?<names>(?:    - [^\r\n]+\r?\n?)+)",
            RegexOptions.Compiled | RegexOptions.Multiline);
        private static readonly Regex ListNameRegex = new Regex(
            @"^    - (?<name>[^\r\n]+)\r?$", RegexOptions.Compiled | RegexOptions.Multiline);
        private static readonly Regex SlotParentRegex = new Regex(
            @"^  - parentName:\s*(?<name>[^\r\n]+)",
            RegexOptions.Compiled | RegexOptions.Multiline);
        private static readonly Regex ComponentClassRegex = new Regex(
            @"^  m_EditorClassIdentifier:\s*(?<name>[^\r\n]+)",
            RegexOptions.Compiled | RegexOptions.Multiline);
        private static readonly Regex ImageRegex = new Regex(
            @"(?:^|;)\s*background-image\s*:\s*url\(\s*[""']?(?<uri>[^)""']+)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex FileIdRegex = new Regex(
            @"\bfileID=(?<id>-?\d+)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex DisplayNoneRegex = new Regex(
            @"(?:^|;)\s*display\s*:\s*none\s*(?:;|$)",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        internal static IReadOnlyDictionary<string, IReadOnlyList<Target>> BuildIndex(
            IReadOnlyCollection<string> uxmlPaths, VmAutomationUIToolkitAuditOptions options)
        {
            var pathsByGuid = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string path in uxmlPaths)
            {
                string metaPath = VmAutomationUIToolkitAuditUtility.ToFullPath(path) + ".meta";
                if (!File.Exists(metaPath))
                    continue;
                Match guid = GuidRegex.Match(File.ReadAllText(metaPath));
                if (guid.Success)
                    pathsByGuid[guid.Groups["guid"].Value] = path;
            }

            var targets = new Dictionary<string, List<Target>>(StringComparer.OrdinalIgnoreCase);
            foreach (string prefabPath in VmAutomationUIToolkitAuditUtility.FindAssetFiles(".prefab", options))
            {
                string text = File.ReadAllText(VmAutomationUIToolkitAuditUtility.ToFullPath(prefabPath));
                IndexPrefab(prefabPath, text, pathsByGuid, targets);
            }

            return targets.ToDictionary(pair => pair.Key,
                pair => (IReadOnlyList<Target>)pair.Value,
                StringComparer.OrdinalIgnoreCase);
        }

        internal static void IndexPrefab(string prefabPath, string text,
            IReadOnlyDictionary<string, string> pathsByGuid,
            IDictionary<string, List<Target>> targets)
        {
            var panels = SourceAssetRegex.Matches(text).Cast<Match>()
                    .Select(match => match.Groups["guid"].Value)
                    .Where(pathsByGuid.ContainsKey)
                    .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (panels.Count != 1)
                return;

            string panelPath = pathsByGuid[panels[0]];
            foreach (string block in Regex.Split(text, @"(?m)^--- !u!"))
            {
                    Match producer = ComponentClassRegex.Match(block);
                    if (!producer.Success)
                        continue;
                    string producerName = producer.Groups["name"].Value.Trim();
                    bool hasEntryTemplate = TemplateAssetRegex.Matches(block).Cast<Match>()
                        .Any(match => pathsByGuid.ContainsKey(match.Groups["guid"].Value));
                    if (hasEntryTemplate)
                    {
                        foreach (Match path in PathRegex.Matches(block))
                        {
                            string field = path.Groups["field"].Value;
                            if (string.Equals(field, "iconPath", StringComparison.Ordinal) ||
                                (string.Equals(field, "parentContainerPath", StringComparison.Ordinal) &&
                                 !Regex.IsMatch(block, @"(?m)^  useParentObject: 1\s*$")))
                                continue;
                            AddTarget(targets, panelPath, prefabPath, producerName,
                                LastName(path.Groups["names"].Value), false);
                        }
                    }

                    if (block.Contains("slotDistributorConfigs:"))
                    {
                        foreach (Match match in SlotParentRegex.Matches(block))
                            AddTarget(targets, panelPath, prefabPath, producerName,
                                match.Groups["name"].Value.Trim(), true);
                    }

                    if (producerName.IndexOf("RenderTexture", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        foreach (Match path in PathRegex.Matches(block).Cast<Match>()
                                     .Where(match => match.Groups["field"].Value == "iconPath"))
                            AddTarget(targets, panelPath, prefabPath, producerName,
                                LastName(path.Groups["names"].Value), true);
                    }
            }
        }

        private static string LastName(string names)
        {
            return ListNameRegex.Matches(names).Cast<Match>()
                .Select(match => match.Groups["name"].Value.Trim()).LastOrDefault();
        }

        private static void AddTarget(IDictionary<string, List<Target>> targets,
            string panelPath, string prefabPath, string producer, string elementName,
            bool requiresImage)
        {
            if (string.IsNullOrWhiteSpace(elementName))
                return;
            if (!targets.TryGetValue(panelPath, out List<Target> list))
                targets[panelPath] = list = new List<Target>();
            if (list.Any(target => target.ElementName == elementName &&
                                   target.RequiresImage == requiresImage))
                return;
            list.Add(new Target
            {
                ElementName = elementName,
                Producer = producer,
                PrefabPath = prefabPath,
                RequiresImage = requiresImage
            });
        }

        internal static void Audit(string assetPath, XDocument document,
            IReadOnlyDictionary<string, IReadOnlyList<Target>> index,
            VmAutomationUxmlLayoutAuditReport report)
        {
            if (index == null || !index.TryGetValue(assetPath, out IReadOnlyList<Target> targets))
                return;
            foreach (Target target in targets)
            {
                var hosts = document.Descendants().Where(element =>
                    string.Equals((string)element.Attribute("name"), target.ElementName,
                        StringComparison.Ordinal)).ToList();
                if (hosts.Count != 1)
                    continue; // The existing element-name contract audit owns missing/ambiguous paths.
                XElement host = hosts[0];
                if (host.AncestorsAndSelf().Any(element =>
                        DisplayNoneRegex.IsMatch((string)element.Attribute("style") ?? string.Empty)))
                    continue;

                var previewImages = host.Descendants().Where(element =>
                    HasPreviewClass(element) &&
                    ImageRegex.IsMatch((string)element.Attribute("style") ?? string.Empty)).ToList();
                bool unresolvedImage = false;
                foreach (XElement image in previewImages)
                {
                    if (HasResolvedImage(image))
                        continue;
                    unresolvedImage = true;
                    report.Record(new VmAutomationUxmlLayoutAuditIssue
                    {
                        AssetPath = assetPath,
                        Line = ((IXmlLineInfo)image).LineNumber,
                        Element = image.Name.LocalName,
                        ElementName = target.ElementName,
                        Kind = "unresolved-generated-ui-builder-preview-image",
                        Severity = "error",
                        Message = $"'{target.ElementName}' has a preview background image that " +
                                  "does not resolve to an imported Sprite or Texture2D."
                    }, false);
                }

                bool populated = target.RequiresImage
                    ? previewImages.Any(HasResolvedImage)
                    : HasAuthoredPayload(host, document, new HashSet<string>(
                        StringComparer.OrdinalIgnoreCase));
                if (populated)
                    continue;
                if (target.RequiresImage && unresolvedImage)
                    continue;

                report.Record(new VmAutomationUxmlLayoutAuditIssue
                {
                    AssetPath = assetPath,
                    Line = ((IXmlLineInfo)host).LineNumber,
                    Element = host.Name.LocalName,
                    ElementName = target.ElementName,
                    Kind = "missing-generated-ui-builder-preview",
                    Severity = "error",
                    Message = $"'{target.ElementName}' is populated by {target.Producer} in " +
                              $"'{target.PrefabPath}', but its visible UI Builder host has no " +
                              (target.RequiresImage ? "resolvable authored image preview." :
                                  "meaningful authored content preview.")
                }, false);
            }
        }

        private static bool HasPreviewClass(XElement element)
        {
            return ((string)element.Attribute("class") ?? string.Empty)
                .Split((char[])null, StringSplitOptions.RemoveEmptyEntries)
                .Contains("ui-builder-preview-content");
        }

        private static bool HasAuthoredPayload(XElement element, XDocument document,
            HashSet<string> visitedTemplates)
        {
            if (DisplayNoneRegex.IsMatch((string)element.Attribute("style") ?? string.Empty))
                return false;

            string name = element.Name.LocalName;
            string text = ((string)element.Attribute("text") ?? string.Empty).Trim();
            if ((name == "Label" || name == "Button" || name == "Toggle" ||
                 name == "AttributeOverrides") &&
                text.Length > 0 && text != "Label" && text != "Button")
                return true;
            if (HasResolvedImage(element))
                return true;

            if (name == "Instance")
            {
                string alias = (string)element.Attribute("template");
                XElement declaration = document.Root?.Elements().FirstOrDefault(candidate =>
                    candidate.Name.LocalName == "Template" &&
                    string.Equals((string)candidate.Attribute("name"), alias,
                        StringComparison.Ordinal));
                Match guid = GuidRegex.Match((string)declaration?.Attribute("src") ?? string.Empty);
                if (guid.Success)
                {
                    string templatePath = AssetDatabase.GUIDToAssetPath(guid.Groups["guid"].Value);
                    if (!string.IsNullOrEmpty(templatePath) && visitedTemplates.Add(templatePath))
                    {
                        string fullPath = VmAutomationUIToolkitAuditUtility.ToFullPath(templatePath);
                        if (File.Exists(fullPath))
                        {
                            var template = XDocument.Parse(File.ReadAllText(fullPath));
                            if (template.Root.Elements().Any(child =>
                                    HasAuthoredPayload(child, template, visitedTemplates)))
                                return true;
                        }
                    }
                }
            }

            return element.Elements().Any(child =>
                HasAuthoredPayload(child, document, visitedTemplates));
        }

        private static bool HasResolvedImage(XElement element)
        {
            Match image = ImageRegex.Match((string)element.Attribute("style") ?? string.Empty);
            if (!image.Success)
                return false;
            string uri = image.Groups["uri"].Value.Trim();
            Match guid = GuidRegex.Match(uri);
            Match fileId = FileIdRegex.Match(uri);
            if (!guid.Success || !fileId.Success ||
                !long.TryParse(fileId.Groups["id"].Value, out long expectedId))
                return false;
            string assetPath = AssetDatabase.GUIDToAssetPath(guid.Groups["guid"].Value);
            if (string.IsNullOrEmpty(assetPath))
                return false;
            return AssetDatabase.LoadAllAssetsAtPath(assetPath).Any(asset =>
                (asset is Sprite || asset is Texture2D) &&
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string _,
                    out long actualId) && actualId == expectedId);
        }
    }
}
#endif
