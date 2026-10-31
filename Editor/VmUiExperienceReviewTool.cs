using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VMUnityAutomation.Runtime;

namespace VMUnityAutomation.Editor
{
    [VmProjectTool("uitoolkit/review-player-experience",
        Description = "Inspect one exact attached game UIDocument through native geometry, clipping/opacity, text, hit tests and declared layout intent. GeometryContracts also check transformed anchor seams/gaps, image-to-frame insets, unsliced Sprite canvas pixel scale and rigid attached controls in the actual current pseudo-state. Capture game-owned IVmUiAuditSource expectations when supplied. Geometry does not qualify eligibility or opaque silhouettes. Bounded to8192 panel/4096 selected nodes,128 hit tests,1024 total expectations/findings and500ms. Reports evaluated GeometryChecks; manual pixel/motion review remains required.",
        RequiresPlayMode = true, ReadOnly = true,
        SideEffects = VmProjectToolSideEffect.ReadsProjectState,
        ErrorCodes = new[] { "ui_audit_document", "ui_audit_arguments", "ui_audit_limit", "ui_audit_semantics", "ui_audit_capability" },
        Preconditions = new[] { "stable Play Mode", "exact attached runtime UIDocument", "settled caller-selected presentation" },
        CompletionEvidence = "Complete native measurement report with actual root coordinates, physical scale, frame/build context, precise findings and semantic coverage. Passed covers measured rules only; ManualVisualReviewRequired remains true.")]
    public sealed class VmUiExperienceReviewTool : IVmProjectTool<VmUiExperienceReviewRequest, VmUiAuditReport>
    {
        public VmUiAuditReport Execute(VmUiExperienceReviewRequest request)
        {
            var document = VmObjectId.ToObject(request.DocumentInstanceId) as UIDocument;
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlaying != EditorApplication.isPlayingOrWillChangePlaymode || document == null ||
                !document.enabled || !document.gameObject.activeInHierarchy || document.rootVisualElement?.panel == null)
                throw new VmProjectToolException("ui_audit_document", "The exact runtime document must be active and attached in stable Play Mode.");
            var args = new Dictionary<string, object>();
            if (!string.IsNullOrEmpty(request.ElementPath)) args.Add("path", request.ElementPath);
            var root = VmAutomationUIToolkitElementUtility.FindRuntimeElement(args, document, out _, out string error);
            if (root == null) throw new VmProjectToolException("ui_audit_document", error);
            if (request.SemanticSourceInstanceIds == null || request.SemanticSourceInstanceIds.Length > 512)
                throw new VmProjectToolException("ui_audit_limit", "Explicit semantic-source identity count must be at most512.");
            var expectations = new List<VmUiAuditExpectation>();
            var identities = new HashSet<string>(StringComparer.Ordinal);
            int capturedCount = 0;
            foreach (string identity in request.SemanticSourceInstanceIds)
            {
                if (string.IsNullOrWhiteSpace(identity) || !identities.Add(identity))
                    throw new VmProjectToolException("ui_audit_semantics", "Semantic source identities must be explicit and unique.");
                var component = VmObjectId.ToObject(identity) as MonoBehaviour;
                if (!(component is IVmUiAuditSource source) || !BelongsToDocument(component.transform, document.transform))
                    throw new VmProjectToolException("ui_audit_semantics", "Every selected semantic source must implement IVmUiAuditSource beneath the actual document.");
                var captured = source.CaptureUiExpectations(document.rootVisualElement);
                if (captured == null) throw new VmProjectToolException("ui_audit_semantics", "A semantic source returned no expectation collection.");
                if (captured.Count > VmUiExperienceAudit.MaximumExpectations - capturedCount)
                    throw new VmProjectToolException("ui_audit_limit", "Document semantic expectations exceed1024.");
                capturedCount += captured.Count;
                foreach (var item in captured)
                {
                    if (item == null || item.Element == null)
                        throw new VmProjectToolException("ui_audit_semantics", "A semantic source returned an absent element product.");
                    if (item.Element != root && !root.Contains(item.Element)) continue;
                    if (expectations.Count >= VmUiExperienceAudit.MaximumExpectations)
                        throw new VmProjectToolException("ui_audit_limit", "Selected semantic expectations exceed1024.");
                    expectations.Add(item);
                }
            }
            if (request.RequireSemanticContracts && !expectations.Exists(item => item.Geometry == null))
                throw new VmProjectToolException("ui_audit_semantics", "This scope has no supplied current game-state contracts.");
            if (request.GeometryContracts == null || request.GeometryContracts.Length >
                VmUiExperienceAudit.MaximumExpectations - expectations.Count)
                throw new VmProjectToolException("ui_audit_limit", "Combined semantic and visual geometry expectations exceed1024.");
            foreach (var contract in request.GeometryContracts)
            {
                if (contract == null) throw new VmProjectToolException("ui_audit_arguments", "Geometry contracts must not contain null entries.");
                expectations.Add(contract.Capture(document));
            }
            float panelWidth = document.rootVisualElement.panel.visualTree.worldBound.width;
            int renderWidth = document.panelSettings.targetTexture != null ? document.panelSettings.targetTexture.width : Screen.width;
            if (panelWidth <= 0 || renderWidth <= 0)
                throw new VmProjectToolException("ui_audit_document", "The actual runtime panel has no measurable target viewport.");
            try
            {
                return VmUiExperienceAudit.Review(root, new VmUiAuditOptions
                {
                    PixelsPerPanelUnit = renderWidth / panelWidth,
                    MinimumHitPixels = request.MinimumHitPixels, MinimumTextOpacity = request.MinimumTextOpacity,
                    GeometryOnly = request.GeometryOnly,
                    SourceIdentity = request.SourceIdentity, Scenario = request.Scenario, LocaleContext = request.LocaleContext
                }, expectations);
            }
            catch (VmUiAuditLimitException failure) { throw new VmProjectToolException("ui_audit_limit", failure.Message); }
            catch (NotSupportedException failure) { throw new VmProjectToolException("ui_audit_capability", failure.Message); }
            catch (ArgumentException failure) { throw new VmProjectToolException("ui_audit_arguments", failure.Message); }
        }

        private static bool BelongsToDocument(Transform source, Transform document)
        {
            int depth = 0;
            for (var node = source; node != null; node = node.parent)
            {
                if (++depth > VmUiExperienceAudit.MaximumDepth)
                    throw new VmProjectToolException("ui_audit_limit", "Semantic source transform ancestry exceeds64.");
                if (node == document) return true;
            }
            return false;
        }
    }
}
