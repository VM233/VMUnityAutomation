#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using static VMUnityAutomation.Editor.VmAutomationUxmlLayoutModels;

namespace VMUnityAutomation.Editor
{
    internal sealed class VmAutomationUxmlPrefabImpactIndex
    {
        private const int MaximumTextBytes = 128 * 1024 * 1024;
        private const int MaximumFileBytes = 4 * 1024 * 1024;
        private static readonly Regex GuidPattern = new Regex(
            @"\bguid\s*[:=]\s*(?<guid>[0-9a-f]{32})\b",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private readonly Dictionary<string, bool> contributors =
            new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
        private readonly UxmlElementNameReferenceIndex names = new UxmlElementNameReferenceIndex(true);
        private readonly HashSet<string> uxmlGuids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        internal string Scope { get; }

        internal VmAutomationUxmlPrefabImpactIndex(VmAutomationUIToolkitAuditOptions options)
        {
            Scope = ScopeIdentity(options);
            long bytes = 0;
            int nameCharacters = 0;
            string[] layouts = VmAutomationUIToolkitAuditUtility.FindAssetFiles(".uxml", options).ToArray();
            string[] prefabs = VmAutomationUIToolkitAuditUtility.FindAssetFiles(".prefab", options).ToArray();
            if (layouts.Length > 512 || prefabs.Length > 5000)
                throw new InvalidDataException("Automatic UI impact index exceeds512 layouts or5000 prefabs.");
            foreach (string path in layouts)
            {
                string text = ReadBounded(path, ref bytes);
                XDocument document = XDocument.Parse(text);
                foreach (XElement element in document.Descendants().Where(
                             VmAutomationUxmlLayoutContractIndexer.IsAuditableNamedElement))
                {
                    string name = (string)element.Attribute("name");
                    int previousCount = names.DefinitionCount;
                    names.AddDefinition(name);
                    if (names.DefinitionCount != previousCount) nameCharacters += name.Length;
                }
                if (names.DefinitionCount > 8192)
                    throw new InvalidDataException("Automatic UI impact index exceeds8192 element names.");
                if (nameCharacters > 1024 * 1024)
                    throw new InvalidDataException("Automatic UI impact index exceeds1MiB of element-name characters.");
                string meta = ReadBounded(path + ".meta", ref bytes);
                Match guid = GuidPattern.Match(meta);
                if (!guid.Success) throw new InvalidDataException("UXML has no imported GUID: " + path);
                uxmlGuids.Add(guid.Groups["guid"].Value);
            }
            foreach (string path in prefabs)
                contributors[path] = Contributes(ReadBounded(path, ref bytes));
        }

        internal static string ScopeIdentity(VmAutomationUIToolkitAuditOptions options) =>
            string.Join("\n", options.AssetRoots) + "\n--exclude--\n" + string.Join("\n", options.ExcludePaths);

        internal bool Observe(string path)
        {
            contributors.TryGetValue(path, out bool previous);
            string fullPath = VmAutomationUIToolkitAuditUtility.ToFullPath(path);
            bool current = false;
            if (File.Exists(fullPath))
            {
                long bytes = 0;
                current = Contributes(ReadBounded(path, ref bytes));
                contributors[path] = current;
            }
            else contributors.Remove(path);
            return previous || current;
        }

        internal bool Contributes(string text)
        {
            names.ClearReferences();
            VmAutomationUxmlLayoutContractIndexer.IndexTextNameReferences(text, names, true);
            if (names.HasReferences) return true;
            foreach (Match match in GuidPattern.Matches(text))
                if (uxmlGuids.Contains(match.Groups["guid"].Value)) return true;
            return false;
        }

        private static string ReadBounded(string path, ref long aggregateBytes)
        {
            string fullPath = VmAutomationUIToolkitAuditUtility.ToFullPath(path);
            long length = new FileInfo(fullPath).Length;
            aggregateBytes += length;
            if (length > MaximumFileBytes || aggregateBytes > MaximumTextBytes)
                throw new InvalidDataException("Automatic UI impact text exceeds4MiB per file or128MiB total: " + path);
            return File.ReadAllText(fullPath);
        }
    }
}
#endif
