using System;
using System.IO;
using UnityEngine;

namespace VMUnityAutomation.Editor
{
    [VmProjectTool("asset/review-folder-organization",
        Description = "Review immediate file/subfolder crowding only inside folders configured in ProjectSettings/VMUnityAutomationFolderAudit.json. Excludes .meta companions and configured subtrees, never follows links or implicitly scans external/vendor/package directories. Counts above authored limits require classification review; no file moves. Bounded at 4096 directories and 32768 entries. Missing/invalid policy and incomplete scans fail explicitly.",
        ReadOnly = true,
        ErrorCodes = new[] { "folder_review_policy_invalid", "folder_review_io_failed" },
        CompletionEvidence = "Complete configured-scope counts, effective folder limits, filename examples and explicit findings/truncation. A finding calls for semantic organization review, not mechanical splitting.")]
    public sealed class VmFolderOrganizationReviewTool : IVmProjectTool<VmFolderOrganizationRequest, VmFolderOrganizationResult>
    {
        public VmFolderOrganizationResult Execute(VmFolderOrganizationRequest request)
        {
            string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string config = Path.Combine(root, VmFolderOrganizationReview.ConfigurationPath);
            try
            {
                if (!File.Exists(config) || new FileInfo(config).Length > VmFolderOrganizationReview.MaximumConfigurationBytes)
                    throw new InvalidDataException("A folder policy of at most 64 KiB is required at " + VmFolderOrganizationReview.ConfigurationPath);
                return VmFolderOrganizationReview.Review(root, File.ReadAllText(config), request.Paths, request.MaxIssues);
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
