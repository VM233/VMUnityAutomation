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
        public static Task<VmUiTransitionSourceReport> Start(VmUiTransitionSourceRequest request, CancellationToken token)
        {
            if (request.SourceRoots == null || request.SourceRoots.Length == 0 || request.SourceRoots.Length > 64 ||
                request.ExcludeRoots == null || request.ExcludeRoots.Length > 64 || request.AffectedPaths == null || request.AffectedPaths.Length > 128)
                throw new VmProjectToolException("ui_transition_source_arguments", "Declare1..64 source roots, up to64 exclusions and128 affected paths.");
            if (!VmAutomationEditorCommands.TryGetRoslynAssemblies(out var csharp, out var core))
                throw new VmProjectToolException("ui_transition_source_capability", "The current Editor has no Roslyn source-analysis capability.");
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            object options = VmAutomationEditorCommands.CreateRoslynParseOptions(csharp);
            var assemblies = AppDomain.CurrentDomain.GetAssemblies().Where(item => !item.IsDynamic && !string.IsNullOrEmpty(item.Location)).ToArray();
            if (assemblies.Length > 512) throw new VmProjectToolException("ui_transition_source_limit", "The semantic context exceeds512 loaded assemblies.");
            string[] references = assemblies.Select(item => item.Location).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            long bytes = references.Sum(path => new FileInfo(path).Length);
            if (bytes > 512L * 1024 * 1024) throw new VmProjectToolException("ui_transition_source_limit", "The semantic reference context exceeds512MiB of native metadata images.");
            var analyzer = new VmUiTransitionSourceAnalyzer(request, project, csharp, core, options, references, token);
            return Task.Run(analyzer.Run, token);
        }
    }
}
