using System.Collections.Generic;

namespace VMUnityAutomation.Editor
{
    public sealed class VmUiTransitionSourceReport
    {
        public bool Complete { get; set; }
        public bool Truncated { get; set; }
        public bool Passed { get; set; }
        public bool SelectedPassed { get; set; }
        public string Coverage { get; set; }
        public string SourceSha256 { get; set; }
        public string[] SourceRoots { get; set; }
        public string[] ExcludeRoots { get; set; }
        public string[] SelectedPaths { get; set; }
        public int ScannedFiles { get; set; }
        public int SyntaxNodes { get; set; }
        public int ResolvedCalls { get; set; }
        public int UnresolvedCalls { get; set; }
        public int CompilerIntrinsicCalls { get; set; }
        public bool CallGraphComplete { get; set; }
        public bool RuntimeAccepted { get; set; }
        public List<VmUiTransitionSourceSite> Sites { get; set; } = new();
        public List<VmUiTransitionSourceEdge> Edges { get; set; } = new();
        public List<string> Issues { get; set; } = new();
        public List<VmUiTransitionSourceSite> UnresolvedCallSites { get; set; } = new();
    }
}
