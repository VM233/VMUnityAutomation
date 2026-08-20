#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor.Compilation;

namespace VMUnityAutomation.Editor
{
    internal static class VmAutomationPackageTestAssemblyProduct
    {
        internal static bool AreAssembliesCompiled(
            IEnumerable<string> assemblyNames)
        {
            return AreRequestedAssembliesCompiled(assemblyNames,
                GetCompiledAssemblyNames());
        }

        internal static bool AreRequestedAssembliesCompiled(
            IEnumerable<string> requestedAssemblyNames,
            IEnumerable<string> compiledAssemblyNames)
        {
            var requested = new HashSet<string>(
                requestedAssemblyNames ?? Array.Empty<string>(),
                StringComparer.Ordinal);
            if (requested.Count == 0)
            {
                return true;
            }

            foreach (var assemblyName in
                     compiledAssemblyNames ?? Array.Empty<string>())
            {
                requested.Remove(assemblyName);
            }

            return requested.Count == 0;
        }

        internal static bool IsCompiledAssemblyOutputAvailable(string outputPath)
        {
            return string.IsNullOrWhiteSpace(outputPath) == false &&
                   File.Exists(outputPath);
        }

        private static IEnumerable<string> GetCompiledAssemblyNames()
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var assembly in
                     CompilationPipeline.GetAssemblies(AssembliesType.Editor))
            {
                if (IsCompiledAssemblyOutputAvailable(assembly.outputPath))
                {
                    names.Add(assembly.name);
                }
            }

            foreach (var assembly in
                     CompilationPipeline.GetAssemblies(AssembliesType.Player))
            {
                if (IsCompiledAssemblyOutputAvailable(assembly.outputPath))
                {
                    names.Add(assembly.name);
                }
            }

            return names;
        }
    }
}
#endif
