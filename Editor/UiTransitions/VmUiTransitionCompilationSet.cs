using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace VMUnityAutomation.Editor
{
    internal sealed class VmUiTransitionCompilationSet
    {
        private readonly Dictionary<string, string> texts = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, (long Bytes, long Ticks)> references = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, object> trees = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, VmUiCSharpSemanticContext> semantics = new(StringComparer.OrdinalIgnoreCase);
        private readonly CancellationToken token;
        internal readonly Dictionary<object, string> TreePaths = new();
        internal long SourceBytes { get; private set; }
        internal int SyntaxNodes { get; private set; }
        internal int FileCount => texts.Count;
        internal int ReferenceCount => references.Count;
        internal long ReferenceBytes => references.Values.Sum(value => value.Bytes);
        internal long ReferenceViewBytes { get; private set; }
        internal string[] AssemblyNames { get; private set; }
        internal string Sha256 { get; private set; }

        internal VmUiTransitionCompilationSet(string project, string[] selected, Assembly csharp, Assembly core, object parseOptions,
            string[] fixtureReferences, VmUiNativeCompilationSnapshot snapshot, CancellationToken token)
        {
            this.token = token;
            VmUiNativeCompilationSnapshot.Compilation[] contexts;
            if (snapshot == null)
                contexts = new[] { new VmUiNativeCompilationSnapshot.Compilation
                { Name = "UiTransitionFixture", Files = selected, References = fixtureReferences } };
            else
            {
                var owners = new HashSet<VmUiNativeCompilationSnapshot.Compilation>();
                foreach (string path in selected)
                {
                    if (!snapshot.Owners.TryGetValue(path, out var owner))
                        throw new InvalidOperationException("Selected source has no native owning compilation: " + path);
                    owners.Add(owner);
                }
                if (owners.Count > 8) throw new InvalidOperationException("UI semantic context exceeds 8 selected owning compilations.");
                contexts = owners.OrderBy(value => value.Name, StringComparer.Ordinal).ToArray();
            }
            AssemblyNames = contexts.Select(value => value.Name).ToArray();
            var selectedSet = new HashSet<string>(selected, StringComparer.OrdinalIgnoreCase);
            int parsed = 0;
            foreach (var context in contexts)
            {
                if (context.References.Length > 512) throw new InvalidOperationException("A native UI semantic context exceeds 512 references.");
                long contextReferenceBytes = 0;
                foreach (string path in context.References)
                {
                    token.ThrowIfCancellationRequested();
                    var file = new FileInfo(path); contextReferenceBytes += file.Length;
                    if (!references.ContainsKey(path)) references.Add(path, (file.Length, file.LastWriteTimeUtc.Ticks));
                }
                if (contextReferenceBytes > 512L * 1024 * 1024 || references.Count > 2048 || ReferenceBytes > 1024L * 1024 * 1024)
                    throw new InvalidOperationException("UI semantic metadata exceeds its per-context or total byte/path budget.");
                ReferenceViewBytes += contextReferenceBytes;
                object options = snapshot == null ? parseOptions : VmAutomationEditorCommands.WithRoslynPreprocessorSymbols(parseOptions, context.Defines);
                var contextTrees = new List<object>(context.Files.Length);
                foreach (string path in context.Files)
                {
                    token.ThrowIfCancellationRequested();
                    if (++parsed > 4096) throw new InvalidOperationException("UI semantic context exceeds 4096 source parses.");
                    if (!texts.TryGetValue(path, out string text))
                    {
                        SourceBytes += new FileInfo(path).Length;
                        if (SourceBytes > 64L * 1024 * 1024) throw new InvalidOperationException("UI semantic context exceeds 64MiB of source.");
                        text = File.ReadAllText(path); texts.Add(path, text);
                    }
                    object tree = VmAutomationCSharpSyntax.Parse(csharp, text, options);
                    foreach (object unused in VmAutomationCSharpSyntax.Nodes(VmAutomationCSharpSyntax.Invoke(tree, "GetRoot")))
                    {
                        token.ThrowIfCancellationRequested();
                        if (++SyntaxNodes > 2000000) throw new InvalidOperationException("UI semantic context exceeds 2000000 syntax nodes.");
                    }
                    contextTrees.Add(tree); TreePaths.Add(tree, Relative(project, path));
                    if (selectedSet.Contains(path)) trees.Add(path, tree);
                }
                var semantic = new VmUiCSharpSemanticContext(csharp, core, contextTrees.ToArray(), context.References, token, context.Name, context.AllowUnsafe);
                foreach (string path in context.Files) if (selectedSet.Contains(path)) semantics.Add(path, semantic);
            }
            using var sha = SHA256.Create();
            Hash(sha, csharp.FullName + "\n" + core.FullName + "\n" + VmAutomationCSharpSyntax.Property(parseOptions, "LanguageVersion"));
            foreach (var pair in texts.OrderBy(value => value.Key, StringComparer.Ordinal)) Hash(sha, pair.Key + "\n" + pair.Value + "\n");
            foreach (var pair in references.OrderBy(value => value.Key, StringComparer.Ordinal)) Hash(sha, pair.Key + "\n" + pair.Value.Bytes + ":" + pair.Value.Ticks + "\n");
            foreach (var context in contexts) Hash(sha, context.Name + "\n" + context.AllowUnsafe + "\n" + string.Join("\n", context.Defines ?? Array.Empty<string>()));
            sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            Sha256 = BitConverter.ToString(sha.Hash).Replace("-", "").ToLowerInvariant();
        }

        internal string Text(string path) => texts[path];
        internal object Tree(string path) => trees[path];
        internal VmUiCSharpSemanticContext Semantic(string path) => semantics[path];

        internal void VerifyUnchanged()
        {
            foreach (var pair in texts)
            {
                token.ThrowIfCancellationRequested();
                if (File.ReadAllText(pair.Key) != pair.Value) throw new InvalidOperationException("Semantic source changed during UI graph capture: " + pair.Key);
            }
            foreach (var pair in references)
            {
                var current = new FileInfo(pair.Key);
                if (current.Length != pair.Value.Bytes || current.LastWriteTimeUtc.Ticks != pair.Value.Ticks)
                    throw new InvalidOperationException("Native semantic reference changed during UI graph capture: " + pair.Key);
            }
        }

        private static void Hash(HashAlgorithm sha, string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value); sha.TransformBlock(bytes, 0, bytes.Length, bytes, 0);
        }

        private static string Relative(string project, string path) => path.StartsWith(project + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            ? path.Substring(project.Length + 1).Replace('\\', '/') : path.Replace('\\', '/');
    }
}
