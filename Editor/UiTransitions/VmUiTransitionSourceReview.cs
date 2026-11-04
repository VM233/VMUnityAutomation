using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace VMUnityAutomation.Editor
{
    public static class VmUiTransitionSourceReview
    {
        public static Task<VmUiTransitionSourceReport> Start(VmUiTransitionSourceRequest request, CancellationToken token) => Prepare(request, token)();

        internal static Func<Task<VmUiTransitionSourceReport>> Prepare(VmUiTransitionSourceRequest request, CancellationToken token)
        {
            if (!Enum.IsDefined(typeof(VmUiTransitionSemanticMode), request.SemanticMode) ||
                request.SourceRoots == null || request.SourceRoots.Length == 0 || request.SourceRoots.Length > 64 ||
                request.ExcludeRoots == null || request.ExcludeRoots.Length > 64 || request.AffectedPaths == null || request.AffectedPaths.Length > 128)
                throw new VmProjectToolException("ui_transition_source_arguments", "Declare1..64 source roots, up to64 exclusions and128 affected paths.");
            if (!VmAutomationEditorCommands.TryGetRoslynAssemblies(out var csharp, out var core))
                throw new VmProjectToolException("ui_transition_source_capability", "The current Editor has no Roslyn source-analysis capability.");
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            object options = VmAutomationEditorCommands.CreateRoslynParseOptions(csharp);
            VmUiNativeCompilationSnapshot snapshot = null;
            string[] references = Array.Empty<string>();
            if (request.SemanticMode == VmUiTransitionSemanticMode.NativeAssemblies) snapshot = VmUiNativeCompilationSnapshot.Capture(project, request);
            else
            {
                var assemblies = AppDomain.CurrentDomain.GetAssemblies().Where(item => !item.IsDynamic && !string.IsNullOrEmpty(item.Location)).ToArray();
                if (assemblies.Length > 512) throw new VmProjectToolException("ui_transition_source_limit", "The fixture context exceeds 512 loaded assemblies.");
                references = assemblies.Select(item => item.Location).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                if (references.Sum(path => new FileInfo(path).Length) > 512L * 1024 * 1024)
                    throw new VmProjectToolException("ui_transition_source_limit", "The fixture references exceed 512MiB.");
            }
            var analyzer = new VmUiTransitionSourceAnalyzer(request, project, csharp, core, options, references, token, snapshot);
            return () => Task.Run(analyzer.Run, token);
        }
    }
}
