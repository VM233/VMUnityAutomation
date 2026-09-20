using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace VMUnityAutomation.Editor
{
    [VmProjectTool("asset/sprite-mesh-review",
        Description = "Review every Sprite TextureImporter below the selected Assets roots. Require Full Rect and report Tight or any other mesh type without changing assets.",
        ReadOnly = true,
        SideEffects = VmProjectToolSideEffect.ReadsProjectState,
        ErrorCodes = new[]
        {
            "sprite_mesh_review_invalid_root",
            "sprite_mesh_review_capacity_exceeded"
        },
        Preconditions = new[] { "editor-connected" },
        CompletionEvidence = "passed=true and totalIssues=0 prove that every Sprite TextureImporter in the complete selected scan uses FullRect. Counts remain complete when issue details are truncated.")]
    public sealed class VmSpriteMeshReviewTool :
        IVmProjectTool<VmSpriteMeshReviewRequest, VmSpriteMeshReviewResult>
    {
        internal const int MaximumCandidateTextures = 100000;
        private const int MaximumRoots = 32;

        public VmSpriteMeshReviewResult Execute(VmSpriteMeshReviewRequest request)
        {
            request ??= new VmSpriteMeshReviewRequest();
            string[] roots = NormalizeRoots(request.AssetRoots);
            int maxIssues = request.MaxIssues <= 0 ? 200 : request.MaxIssues;

            var paths = new SortedSet<string>(StringComparer.Ordinal);
            foreach (string guid in AssetDatabase.FindAssets("t:Texture2D", roots))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.IsNullOrEmpty(path))
                    paths.Add(path);
            }

            if (paths.Count > MaximumCandidateTextures)
            {
                throw new VmProjectToolException(
                    "sprite_mesh_review_capacity_exceeded",
                    $"Sprite mesh review found {paths.Count} candidate textures. " +
                    $"The supported maximum is {MaximumCandidateTextures}.");
            }

            int spriteCount = 0;
            int fullRectCount = 0;
            int tightCount = 0;
            int totalIssues = 0;
            var issues = new List<VmSpriteMeshReviewIssue>(
                Math.Min(maxIssues, paths.Count));
            foreach (string path in paths)
            {
                if (AssetImporter.GetAtPath(path) is not TextureImporter importer ||
                    importer.textureType != TextureImporterType.Sprite)
                {
                    continue;
                }

                spriteCount++;
                var settings = new TextureImporterSettings();
                importer.ReadTextureSettings(settings);
                if (settings.spriteMeshType == SpriteMeshType.FullRect)
                {
                    fullRectCount++;
                    continue;
                }

                if (settings.spriteMeshType == SpriteMeshType.Tight)
                    tightCount++;
                totalIssues++;
                if (issues.Count < maxIssues)
                {
                    issues.Add(new VmSpriteMeshReviewIssue(
                        path, settings.spriteMeshType.ToString()));
                }
            }

            return new VmSpriteMeshReviewResult(roots, paths.Count,
                spriteCount, fullRectCount, tightCount, totalIssues,
                issues.ToArray());
        }

        private static string[] NormalizeRoots(string[] requestedRoots)
        {
            string[] values = requestedRoots == null || requestedRoots.Length == 0
                ? new[] { "Assets" }
                : requestedRoots;
            if (values.Length > MaximumRoots)
            {
                throw new VmProjectToolException(
                    "sprite_mesh_review_invalid_root",
                    $"Sprite mesh review accepts at most {MaximumRoots} asset roots.");
            }

            var roots = new SortedSet<string>(StringComparer.Ordinal);
            foreach (string value in values)
            {
                string root = (value ?? "").Trim().Replace('\\', '/').TrimEnd('/');
                bool belowAssets = string.Equals(root, "Assets", StringComparison.Ordinal) ||
                                   root.StartsWith("Assets/", StringComparison.Ordinal);
                if (!belowAssets || !AssetDatabase.IsValidFolder(root))
                {
                    throw new VmProjectToolException(
                        "sprite_mesh_review_invalid_root",
                        $"'{value}' is not an existing project folder below Assets.");
                }
                roots.Add(root);
            }
            return roots.ToArray();
        }
    }
}
