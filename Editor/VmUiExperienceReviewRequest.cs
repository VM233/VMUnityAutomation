using System.ComponentModel;

namespace VMUnityAutomation.Editor
{
    public sealed class VmUiExperienceReviewRequest
    {
        [VmRequired, VmMinLength(1)]
        [Description("Exact attached runtime UIDocument identity returned by runtime-documents.")]
        public string DocumentInstanceId { get; set; }
        [Description("Optional native element path from runtime-tree. Omit for the complete selected document.")]
        public string ElementPath { get; set; }
        [VmRequired, VmMinLength(1)]
        [Description("Caller-owned immutable source/build fingerprint. The report separately retains actual Application build GUID and frame.")]
        public string SourceIdentity { get; set; }
        [VmRequired, VmMinLength(1)]
        [Description("Actual tested scene/state/viewport scenario identity.")]
        public string Scenario { get; set; }
        [Description("Caller-supplied Locale context; actual loaded values must be asserted by game-owned semantic sources.")]
        public string LocaleContext { get; set; } = "";
        [Description("Configured minimum physical hit size in pixels; not an accessibility certification.")]
        public float MinimumHitPixels { get; set; } = 24;
        [Description("Configured minimum effective text opacity, in0..1. Semantic sources can require stronger readability.")]
        public float MinimumTextOpacity { get; set; } = .25f;
        [Description("Require at least one current game-owned semantic expectation. Geometry alone cannot qualify eligibility/state.")]
        public bool RequireSemanticContracts { get; set; }
    }
}
