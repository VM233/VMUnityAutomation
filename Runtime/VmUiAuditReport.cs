using System.Collections.Generic;

namespace VMUnityAutomation.Runtime
{
    public sealed class VmUiAuditReport
    {
        public bool Passed { get; set; }
        public bool Complete { get; set; }
        public bool ManualVisualReviewRequired { get; set; } = true;
        public string SourceIdentity { get; set; }
        public string Scenario { get; set; }
        public string LocaleContext { get; set; }
        public string BuildGuid { get; set; }
        public string UnityVersion { get; set; }
        public string Platform { get; set; }
        public int Frame { get; set; }
        public double PixelsPerPanelUnit { get; set; }
        public double RootX { get; set; }
        public double RootY { get; set; }
        public double RootWidth { get; set; }
        public double RootHeight { get; set; }
        public int PanelElements { get; set; }
        public int ScannedElements { get; set; }
        public int VisibleElements { get; set; }
        public int MeasuredTexts { get; set; }
        public int HitTests { get; set; }
        public int SemanticExpectations { get; set; }
        public double ElapsedMilliseconds { get; set; }
        public string SemanticCoverage { get; set; }
        public string ContrastCoverage { get; set; } = "ManualPixelReviewRequired";
        public List<VmUiAuditFinding> Findings { get; set; } = new List<VmUiAuditFinding>();
    }
}
