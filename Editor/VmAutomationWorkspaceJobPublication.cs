using System;
using System.Collections.Generic;
using UnityEditor;

namespace VMUnityAutomation.Editor
{
    internal static class VmAutomationWorkspaceJobPublication
    {
        internal static Dictionary<string, object> Create(VmAutomationWorkspaceJob job,
            bool includeAccessToken)
        {
            var response = new Dictionary<string, object>
            {
                { "success", true },
                { "jobId", job.JobId },
                { "jobType", job.JobType },
                { "operation", job.Operation },
                { "status", job.Status },
                { "phase", job.Phase },
                { "statusMessage", job.StatusMessage ?? "" },
                { "pollRoute", "jobs/get" },
                { "createdAt", job.CreatedAt.ToString("O") },
                { "updatedAt", job.UpdatedAt.ToString("O") },
                { "recoveredAfterReload", job.RecoveredAfterReload },
                { "domainReloadCount", job.DomainReloadCount },
            };
            if (includeAccessToken)
                response["jobAccessToken"] = job.JobAccessToken;
            if (!string.IsNullOrWhiteSpace(job.IdempotencyKey))
                response["idempotencyKey"] = job.IdempotencyKey;
            if (job.StartedAt.HasValue)
                response["startedAt"] = job.StartedAt.Value.ToString("O");
            if (job.CompletedAt.HasValue)
                response["completedAt"] = job.CompletedAt.Value.ToString("O");
            if (job.Result != null)
                response["result"] = job.Result;
            if (job.Error != null)
                response["error"] = job.Error;
            if (job.JobType == VmAutomationAssetTransactionJobRunner.JobType)
            {
                response["transactionId"] = job.JobId;
                if (job.Phase == VmAutomationAssetTransactionJobRunner.CommittedPhase ||
                    job.Phase == VmAutomationAssetTransactionJobRunner.RolledBackPhase ||
                    job.Phase == VmAutomationAssetTransactionJobRunner.RollbackFailedPhase ||
                    job.Phase == VmAutomationAssetTransactionJobRunner.OutcomeUncertainPhase)
                    response["terminalState"] = job.Phase;
                if (VmAutomationAssetTransactionJobRunner.HasRecoveryArtifacts(job))
                {
                    response["cleanupStatus"] = "available";
                    if (includeAccessToken)
                        response["cleanupToken"] = job.JobAccessToken;
                }
            }
            if (!job.IsTerminal)
            {
                if (!job.ClientAdopted)
                    response["blockedReason"] = "awaiting-client-poll";
                else if (job.RequiresStableEditMode &&
                    !VmAutomationRuntimePreconditions.IsStableEditMode)
                {
                    response["blockedReason"] =
                        VmAutomationWorkspaceJobRunner.EditModeRequiredBlockedReason;
                }
                else if (EditorApplication.isCompiling)
                    response["blockedReason"] = "compiling";
                else if (EditorApplication.isUpdating)
                    response["blockedReason"] = "asset-or-package-update";
            }
            return response;
        }
    }
}
