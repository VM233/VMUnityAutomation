using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;

namespace VMUnityAutomation.Editor
{
    internal sealed class VmUiTransitionStyleSources
    {
        private static readonly Regex className = new(@"\.([A-Za-z_][A-Za-z0-9_-]*)", RegexOptions.CultureInvariant);
        private readonly Dictionary<string, HashSet<string>> owners = new(StringComparer.Ordinal);
        private int publishedLinks;
        internal Dictionary<string, string> Text { get; } = new(StringComparer.Ordinal);

        internal VmUiTransitionStyleSources(string project, string[] roots, CancellationToken token)
        {
            long bytes = 0; int rules = 0, matches = 0;
            foreach (string root in roots)
            {
                string absolute = Path.GetFullPath(Path.Combine(project, root));
                if (!absolute.StartsWith(project + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException("UI stylesheet roots must remain inside the bound project.");
                var paths = File.Exists(absolute) ? new[] { absolute } : Directory.EnumerateFiles(absolute, "*.uss", SearchOption.AllDirectories);
                foreach (string path in paths)
                {
                    token.ThrowIfCancellationRequested();
                    if (!string.Equals(Path.GetExtension(path), ".uss", StringComparison.OrdinalIgnoreCase))
                        throw new ArgumentException("UI stylesheet source must be a USS file: " + path);
                    string relative = path.Substring(project.Length + 1).Replace('\\', '/');
                    if (Text.ContainsKey(relative)) continue;
                    bytes += new FileInfo(path).Length;
                    if (Text.Count == 1024 || bytes > 16L * 1024 * 1024) throw new InvalidOperationException("UI stylesheet graph exceeds1024 files/16MiB.");
                    string text = File.ReadAllText(path); Text.Add(relative, text);
                    foreach (var rule in VmAutomationUssStyleSheetParser.ParseStyleSheet(relative, text))
                    {
                        if (++rules > 8192) throw new InvalidOperationException("UI stylesheet graph exceeds8192 rules.");
                        bool hides = rule.Declarations.ContainsKey("display") || rule.Declarations.ContainsKey("visibility") ||
                            rule.Declarations.TryGetValue("opacity", out var opacity) && float.TryParse(opacity, NumberStyles.Float, CultureInfo.InvariantCulture, out float value) && value == 0;
                        if (!hides) continue;
                        foreach (Match match in className.Matches(rule.SelectorGroup))
                        {
                            if (++matches > 32768) throw new InvalidOperationException("UI stylesheet graph exceeds32768 presentation class references.");
                            string name = match.Groups[1].Value;
                            if (!owners.TryGetValue(name, out var sources)) owners.Add(name, sources = new HashSet<string>(StringComparer.Ordinal));
                            sources.Add(relative + ":" + rule.Line);
                        }
                    }
                }
            }
        }

        internal string[] For(string name)
        {
            if (!owners.TryGetValue(name, out var sources)) return Array.Empty<string>();
            publishedLinks += sources.Count;
            if (publishedLinks > 32768) throw new InvalidOperationException("UI stylesheet graph exceeds32768 published site/rule links.");
            return sources.OrderBy(value => value, StringComparer.Ordinal).ToArray();
        }

        internal void VerifyUnchanged(string project)
        {
            foreach (var pair in Text)
                if (File.ReadAllText(Path.Combine(project, pair.Key)) != pair.Value)
                    throw new InvalidOperationException("UI stylesheet changed during source review: " + pair.Key);
        }
    }
}
