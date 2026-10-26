using System;
using System.IO;
using UnityEngine;

namespace VMUnityAutomation.Editor
{
    [VmProjectTool("asset/review-folder-organization",
        Description = "Review immediate file/subfolder crowding using ProjectSettings/VMUnityAutomationFolderAudit.json, or built-in defaults if absent: shallow Assets root (8 files/16 folders), recursive Scripts/Editor and Resources/GameResources/Game Resources when present. .meta companions and excluded subtrees do not count; links are never followed. Assets root counts physical immediate entries without traversing vendor/plugin folders. Limits prompt project-owned classification review, never file moves. Bounded at 4096 directories/32768 entries; invalid policy or incomplete scan fails.",
        ReadOnly = true,
        ErrorCodes = new[] { "folder_review_policy_invalid", "folder_review_io_failed" },
        CompletionEvidence = "Configuration source, complete scoped counts, effective folder limits, filename examples and explicit findings/truncation. A finding calls for semantic project-owned organization review, not mechanical splitting or vendor reorganization.")]
    public sealed class VmFolderOrganizationReviewTool : IVmProjectTool<VmFolderOrganizationRequest, VmFolderOrganizationResult>
    {
        public VmFolderOrganizationResult Execute(VmFolderOrganizationRequest request)
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            try
            {
                return VmFolderOrganizationPolicy.Review(root, request.Paths, request.MaxIssues);
            }
            catch (InvalidDataException exception)
            {
                throw new VmProjectToolException("folder_review_policy_invalid", exception.Message);
            }
            catch (IOException exception)
            {
                throw new VmProjectToolException("folder_review_io_failed", exception.Message);
            }
            catch (UnauthorizedAccessException exception)
            {
                throw new VmProjectToolException("folder_review_io_failed", exception.Message);
            }
        }
    }
}
