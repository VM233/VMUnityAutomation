using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor.Compilation;
using UnityEditor.PackageManager;

namespace VMUnityAutomation.Editor
{
    internal sealed class VmUiNativeCompilationSnapshot
    {
        internal readonly Dictionary<string, Compilation> Owners = new(StringComparer.OrdinalIgnoreCase);

        internal static VmUiNativeCompilationSnapshot Capture(string project, VmUiTransitionSourceRequest request)
        {
            var snapshot = new VmUiNativeCompilationSnapshot();
            var assemblies = CompilationPipeline.GetAssemblies(AssembliesType.Editor);
            if (assemblies.Length > 256) throw new InvalidOperationException("UI semantic context exceeds 256 native compilations.");
            var packages = PackageInfo.GetAllRegisteredPackages();
            if (packages.Length > 512) throw new InvalidOperationException("UI semantic path context exceeds 512 packages.");
            var packagePaths = packages.ToDictionary(value => value.name, value => value.resolvedPath, StringComparer.Ordinal);
            string[] roots = request.SourceRoots.Select(path => Absolute(project, path)).ToArray();
            string[] excluded = request.ExcludeRoots.Select(path => Absolute(project, path)).ToArray();
            int membership = 0;
            int selectedOwners = 0;
            foreach (var assembly in assemblies)
            {
                membership += assembly.sourceFiles.Length;
                if (membership > 32768) throw new InvalidOperationException("UI semantic context exceeds 32768 native source membership records.");
                string[] files = assembly.sourceFiles.Select(path => Resolve(project, path, packagePaths)).ToArray();
                if (!files.Any(path => roots.Any(root => InRoot(path, root)) && !excluded.Any(root => InRoot(path, root)))) continue;
                if (++selectedOwners > 8) throw new InvalidOperationException("UI semantic context exceeds 8 selected owning compilations.");
                if (assembly.allReferences.Length > 512) throw new InvalidOperationException("A native UI semantic context exceeds 512 references.");
                var compilation = new Compilation
                {
                    Name = assembly.name, Defines = assembly.defines.ToArray(), AllowUnsafe = assembly.compilerOptions.AllowUnsafeCode,
                    Files = files,
                    References = assembly.allReferences.Select(path => Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(project, path)))
                        .Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
                };
                foreach (string path in compilation.Files)
                {
                    if (snapshot.Owners.ContainsKey(path)) throw new InvalidOperationException("A source belongs to multiple native compilations: " + path);
                    snapshot.Owners.Add(path, compilation);
                }
            }
            return snapshot;
        }

        private static string Absolute(string project, string path) => Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(project, path))
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

        private static bool InRoot(string path, string root) => string.Equals(path, root, StringComparison.OrdinalIgnoreCase) ||
            path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);

        private static string Resolve(string project, string path, IReadOnlyDictionary<string, string> packages)
        {
            string normalized = path.Replace('\\', '/');
            if (normalized.StartsWith("Packages/", StringComparison.Ordinal))
            {
                int separator = normalized.IndexOf('/', 9);
                string name = separator < 0 ? normalized.Substring(9) : normalized.Substring(9, separator - 9);
                return Path.GetFullPath(Path.Combine(packages[name], normalized.Substring(separator + 1)));
            }
            return Path.GetFullPath(Path.IsPathRooted(path) ? path : Path.Combine(project, path));
        }

        internal sealed class Compilation
        {
            internal string Name;
            internal string[] Files, References, Defines;
            internal bool AllowUnsafe;
        }
    }
}
