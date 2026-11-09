using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace VMUnityAutomation.Editor
{
    public sealed class VmUiTransitionSourceReport
    {
        public string[] StyleRoots { get; set; } = Array.Empty<string>();
        public string[] StyleSources { get; set; } = Array.Empty<string>();
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
        public VmUiTransitionSourceProtocol RuntimeProtocol { get; set; }
        public string SemanticMode { get; set; }
        public string[] SemanticAssemblies { get; set; }
        public string SemanticContextSha256 { get; set; }
        public int SemanticSourceFiles { get; set; }
        public long SemanticSourceBytes { get; set; }
        public int SemanticSyntaxNodes { get; set; }
        public int SemanticReferences { get; set; }
        public long SemanticReferenceBytes { get; set; }
        public long SemanticReferenceViewBytes { get; set; }
        [Description("Exact source sites. Visibility/ContentReplacement kinds select the corresponding captured RuntimeProtocol family; raw native display/visibility has two logical shown/hidden arcs. GetStateEdges projects one exact site's contextual phase graph without duplicating the relation in Edges.")]
        public List<VmUiTransitionSourceSite> Sites { get; set; } = new();
        [Description("Structural callable, callback and dispatch edges with all actual source locations. Per-site phase edges are projected from Sites and RuntimeProtocol, not stored here.")]
        public List<VmUiTransitionSourceEdge> Edges { get; set; } = new();
        public List<string> Issues { get; set; } = new();
        public List<VmUiTransitionSourceSite> UnresolvedCallSites { get; set; } = new();
        public List<VmUiTransitionSourceSite> DelegateCallSites { get; set; } = new();

        public VmUiTransitionSourceEdge[] GetStateEdges(VmUiTransitionSourceSite site)
        {
            if (site.Kind is "Directdisplay" or "Directvisibility")
                return new[] { Edge("hidden", "shown"), Edge("shown", "hidden") };
            var arcs = site.Kind switch
            {
                "Visibility" => RuntimeProtocol.Visibility,
                "ContentReplacement" => RuntimeProtocol.Content,
                _ => Array.Empty<VmUiTransitionSourceProtocol.Arc>()
            };
            return arcs.Select(arc => Edge(arc.From, arc.To)).ToArray();

            VmUiTransitionSourceEdge Edge(string from, string to) => new()
            {
                From = site.ID + "/" + from, To = site.ID + "/" + to,
                FromOwner = site.Owner, ToOwner = site.Owner, Kind = "StateTransition",
                Locations = new() { new VmUiTransitionSourceLocation
                    { Site = site.ID, Path = site.Path, Line = site.Line, Column = site.Column } }
            };
        }
    }
}
