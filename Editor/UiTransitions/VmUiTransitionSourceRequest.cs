using System;
using System.ComponentModel;

namespace VMUnityAutomation.Editor
{
    public sealed class VmUiTransitionSourceRequest
    {
        [VmRequired]
        [Description("Complete declared first-party C# files or directories relative to the bound project, up to64 roots. Runtime/host acceptance remains separate.")]
        public string[] SourceRoots { get; set; } = Array.Empty<string>();
        [Description("Explicit excluded source subtrees, up to64. The complete result preserves this coverage boundary.")]
        public string[] ExcludeRoots { get; set; } = Array.Empty<string>();
        [Description("Optional changed files or directories, up to128. Review their sites plus one-hop callers and consumers in the complete declared source graph. A local result never sets Passed.")]
        public string[] AffectedPaths { get; set; } = Array.Empty<string>();
    }
}
