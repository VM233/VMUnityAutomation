using System;
using System.Collections.Generic;

namespace VMUnityAutomation.Editor
{
    [VmProjectTool("texture/review-nine-slice", ReadOnly = true,
        Description = "Review up to 256 explicit PNG Sprites for exact minimal nine-slice sources. Reuse duplicate fingerprints, remove only one repeated interior band per axis, and verify every reconstructed source pixel. Return hashes, dimensions and borders without modifying assets. Review consumers separately before applying.",
        SideEffects = VmProjectToolSideEffect.ReadsProjectState,
        Preconditions = new[] { "editor-connected", "stable-edit-mode" },
        ErrorCodes = new[] { "nine_slice_invalid_input", "image_size_limit", "image_format_unsupported" },
        CompletionEvidence = "Every eligible plan reconstructs the source visible RGBA exactly and includes its source hash.")]
    public sealed class VmNineSliceReviewTool : IVmProjectTool<VmNineSliceReviewRequest, VmNineSliceReviewResult>
    {
        public VmNineSliceReviewResult Execute(VmNineSliceReviewRequest request)
        {
            if (request.TexturePaths.Length == 0 || request.TexturePaths.Length > 256)
                throw new VmProjectToolException("nine_slice_invalid_input", "Review accepts 1..256 explicit texture paths.");
            var paths = new HashSet<string>(StringComparer.Ordinal);
            var plans = new VmNineSliceCompactPlan[request.TexturePaths.Length];
            var budget = new VmImageFingerprintBudget();
            for (int index = 0; index < request.TexturePaths.Length; index++)
            {
                string path = request.TexturePaths[index];
                if (!paths.Add(path))
                    throw new VmProjectToolException("nine_slice_invalid_input", $"Duplicate texture path '{path}'.");
                plans[index] = VmNineSliceCompaction.Review(path, budget, out _, out _);
            }
            return new VmNineSliceReviewResult { Plans = plans };
        }
    }
}
