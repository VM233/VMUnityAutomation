using System.ComponentModel;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using VMUnityAutomation.Runtime;

namespace VMUnityAutomation.Editor
{
    public sealed class VmUiExperienceReviewRequest
    {
        [VmRequired, VmMinLength(1)]
        [Description("Exact attached runtime UIDocument identity returned by runtime-documents.")]
        public string DocumentInstanceId { get; set; }
        [Description("Optional native element path from runtime-tree. Omit for the complete selected document.")]
        public string ElementPath { get; set; }
        [Description("Explicit exact component identities implementing IVmUiAuditSource beneath this document. At most512; no scene-wide component search is performed.")]
        public string[] SemanticSourceInstanceIds { get; set; } = Array.Empty<string>();
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

        [Description("Explicit visual geometry contracts, at most1024. Native paths, local UI-unit offsets/insets and source-pixel canvas scale; these do not assert game eligibility or infer opaque silhouettes.")]
        public GeometryContract[] GeometryContracts { get; set; } = Array.Empty<GeometryContract>();

        public sealed class GeometryContract
        {
            [VmRequired, VmMinLength(1)] public string ElementPath { get; set; }
            public string ReferencePath { get; set; }
            [VmRequired, VmMinLength(1)] public string Owner { get; set; }
            [VmRequired] public VmUiAuditExpectation.GeometryKind Kind { get; set; }
            public float ElementAnchorX { get; set; }
            public float ElementAnchorY { get; set; }
            public float ReferenceAnchorX { get; set; }
            public float ReferenceAnchorY { get; set; }
            public float ElementOffsetX { get; set; }
            public float ElementOffsetY { get; set; }
            public float ReferenceOffsetX { get; set; }
            public float ReferenceOffsetY { get; set; }
            public float Left { get; set; }
            public float Top { get; set; }
            public float Right { get; set; }
            public float Bottom { get; set; }
            public float PixelUnits { get; set; } = 1;
            public float TolerancePixels { get; set; } = .25f;
            public bool CheckX { get; set; } = true;
            public bool CheckY { get; set; } = true;

            internal VmUiAuditExpectation Capture(UIDocument document)
            {
                var element = Find(document, ElementPath);
                var reference = string.IsNullOrEmpty(ReferencePath) ? null : Find(document, ReferencePath);
                return new VmUiAuditExpectation { Owner = Owner, Element = element,
                    Geometry = new VmUiAuditExpectation.GeometryContract
                    {
                        Kind = Kind, Reference = reference,
                        ElementAnchor = new Vector2(ElementAnchorX, ElementAnchorY),
                        ReferenceAnchor = new Vector2(ReferenceAnchorX, ReferenceAnchorY),
                        ElementOffset = new Vector2(ElementOffsetX, ElementOffsetY),
                        ReferenceOffset = new Vector2(ReferenceOffsetX, ReferenceOffsetY),
                        Insets = new Vector4(Left, Top, Right, Bottom), PixelUnits = PixelUnits,
                        TolerancePixels = TolerancePixels,
                        CheckX = CheckX, CheckY = CheckY
                    } };
            }

            private static VisualElement Find(UIDocument document, string path)
            {
                var element = VmAutomationUIToolkitElementUtility.FindRuntimeElement(
                    new Dictionary<string, object> { ["path"] = path }, document, out _, out string error);
                if (element == null) throw new VmProjectToolException("ui_audit_arguments", error);
                return element;
            }
        }
    }
}
