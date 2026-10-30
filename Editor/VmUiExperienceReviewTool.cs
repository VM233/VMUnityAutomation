using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using VMUnityAutomation.Runtime;

namespace VMUnityAutomation.Editor
{
    [VmProjectTool("uitoolkit/review-player-experience",
        Description = "Inspect one exact attached game UIDocument through native UI Toolkit geometry, ancestor clipping/opacity, text measurement, physical hit size and center hit testing. Capture current game-owned IVmUiAuditSource expectations when supplied. The runtime core is Player-compatible. Bounded to8192 panel/4096 selected nodes,128 hit tests,1024 expectations/findings and500ms. Reject empty/incomplete reviews. Manual image, hierarchy, icon and motion review remains required; LocaleContext is caller context, not loaded-table proof.",
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
            var components = document.GetComponentsInChildren<MonoBehaviour>(true);
            if (components.Length > 512) throw new VmProjectToolException("ui_audit_limit", "Document semantic-source component inventory exceeds512.");
            var expectations = new List<VmUiAuditExpectation>();
            int capturedCount = 0;
            foreach (var component in components)
            {
                if (!(component is IVmUiAuditSource source)) continue;
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
            if (request.RequireSemanticContracts && expectations.Count == 0)
                throw new VmProjectToolException("ui_audit_semantics", "This scope has no supplied current game-state contracts.");
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
                    SourceIdentity = request.SourceIdentity, Scenario = request.Scenario, LocaleContext = request.LocaleContext
                }, expectations);
            }
            catch (VmUiAuditLimitException failure) { throw new VmProjectToolException("ui_audit_limit", failure.Message); }
            catch (NotSupportedException failure) { throw new VmProjectToolException("ui_audit_capability", failure.Message); }
            catch (ArgumentException failure) { throw new VmProjectToolException("ui_audit_arguments", failure.Message); }
        }
    }
}
