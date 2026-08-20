using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;

namespace VMUnityAutomation.Editor
{
    /// <summary>
    /// Durable write-ahead state machine for asset/transaction. The workspace Job owns
    /// serialization; this type owns transaction phases and terminal outcome evidence.
    /// </summary>
    internal static class VmAutomationAssetTransactionJobRunner
    {
        internal const string JobType = "asset-transaction";
        internal const string Operation = "asset/transaction";
        internal const string PreparedPhase = "asset-transaction-prepared";
        internal const string ApplyingPhase = "asset-transaction-applying";
        internal const string SavingPhase = "asset-transaction-saving";
        internal const string PublishingPhase = "asset-transaction-publishing";
        internal const string RollingBackPhase = "asset-transaction-rolling-back";
        internal const string CommittedPhase = "committed";
        internal const string RolledBackPhase = "rolled_back";
        internal const string RollbackFailedPhase = "rollback_failed";
        internal const string OutcomeUncertainPhase = "outcome_uncertain";

        internal static Func<string, int, Exception> FaultInjector;

        internal static object DryRun(Dictionary<string, object> args)
        {
            try
            {
                VmAutomationAssetTransactionPlan plan = VmAutomationAssetTransactionPlan.Build(args);
                return new Dictionary<string, object>
                {
                    { "success", true },
                    { "dryRun", true },
                    { "validation", "passed" },
                    { "operationCount", plan.Operations.Count },
                    { "operations", plan.Operations.Cast<object>().ToList() },
                    { "compilationRequired", plan.CompilationRequired },
                };
            }
            catch (VmAutomationAssetTransactionPlan.ValidationException exception)
            {
                return VmAutomationResponse.Error(exception.Message, exception.ErrorCode);
            }
        }

        internal static bool TryValidateStart(Dictionary<string, object> args,
            out object error)
        {
            try
            {
                VmAutomationAssetTransactionPlan.Build(args);
                error = null;
                return true;
            }
            catch (VmAutomationAssetTransactionPlan.ValidationException exception)
            {
                error = VmAutomationResponse.Error(exception.Message, exception.ErrorCode);
                return false;
            }
        }

        internal static bool ExecutePhase(VmAutomationWorkspaceJob job)
        {
            if (job == null || job.JobType != JobType) return false;
            switch (job.Phase)
            {
                case VmAutomationWorkspaceJobRunner.WaitingForEditorPhase:
                    Prepare(job);
                    return true;
                case PreparedPhase:
                    job.Phase = ApplyingPhase;
                    job.StatusMessage = "Prepared snapshots are durable; applying operation 0.";
                    VmAutomationWorkspaceJobRunner.Persist(job);
                    return true;
                case ApplyingPhase:
                    ApplyNext(job);
                    return true;
                case SavingPhase:
                    SaveAndPublish(job);
                    return true;
                case PublishingPhase:
                    ResumePublishing(job);
                    return true;
                case VmAutomationWorkspaceJobRunner.VerifyingPhase:
                    VerifyAndCommit(job);
                    return true;
                case RollingBackPhase:
                    Rollback(job, GetOriginalError(job));
                    return true;
                default:
                    return false;
            }
        }

        internal static void FailAndRollback(VmAutomationWorkspaceJob job, Exception exception)
        {
            if (job == null) return;
            Dictionary<string, object> originalError = exception is
                VmAutomationAssetTransactionPlan.ValidationException validation
                ? VmAutomationResponse.Error(validation.Message, validation.ErrorCode, false)
                : VmAutomationResponse.Error(exception.GetBaseException().Message,
                    "asset_transaction_failed", false);
            Rollback(job, originalError);
        }

        internal static void FailAndRollback(VmAutomationWorkspaceJob job,
            Dictionary<string, object> originalError)
        {
            if (job == null) return;
            Rollback(job, originalError ?? VmAutomationResponse.Error(
                "Asset transaction compilation failed.", "compilation_failed", false));
        }

        internal static void RecoverAfterReload(VmAutomationWorkspaceJob job)
        {
            if (job == null || job.JobType != JobType || job.IsTerminal) return;
            Dictionary<string, object> state = job.TransactionState;
            if (state == null)
            {
                FailWithoutMutation(job, VmAutomationResponse.Error(
                    "The asset transaction reloaded before its durable snapshot state was published.",
                    "asset_transaction_prepare_interrupted", false));
                return;
            }

            if (job.Phase == PreparedPhase)
            {
                try
                {
                    VmAutomationAssetTransactionSnapshotStore.VerifySnapshotArtifacts(job.JobId, state);
                    job.StatusMessage =
                        "Prepared transaction recovered after reload; no mutation had started.";
                    VmAutomationWorkspaceJobRunner.Persist(job);
                }
                catch (Exception exception)
                {
                    CompleteRollbackFailed(job, VmAutomationResponse.Error(
                            "Prepared transaction snapshot verification failed after reload.",
                            "transaction_snapshot_invalid", false),
                        new List<string> { exception.GetBaseException().Message });
                }
                return;
            }

            if (job.Phase == ApplyingPhase || job.Phase == SavingPhase ||
                job.Phase == RollingBackPhase)
            {
                Rollback(job, VmAutomationResponse.Error(
                    "The asset transaction was interrupted before a commit candidate was durably published.",
                    "asset_transaction_interrupted_during_apply", false));
                return;
            }

            if (job.Phase == PublishingPhase)
            {
                ReconcilePublishedCandidate(job);
                return;
            }

            if (job.Phase == VmAutomationWorkspaceJobRunner.AwaitingCompilationStartPhase ||
                job.Phase == VmAutomationWorkspaceJobRunner.CompilingPhase)
            {
                if (!VerifyCommitEvidence(state, out List<string> differences))
                {
                    CompleteOutcomeUncertain(job,
                        "The transaction changed after its commit candidate was published while compilation was interrupted.",
                        differences);
                    return;
                }
                ResetCompilationEvidence(job);
                job.Phase = VmAutomationWorkspaceJobRunner.RequestingCompilationPhase;
                job.StatusMessage =
                    "Commit candidate recovered; requesting a fresh authoritative compilation.";
                VmAutomationWorkspaceJobRunner.Persist(job);
                return;
            }

            if (job.Phase == VmAutomationWorkspaceJobRunner.WaitingForDomainReloadPhase &&
                job.CompilationFinished)
            {
                if (job.CompilationSucceeded != true)
                {
                    Rollback(job,
                        VmAutomationWorkspaceJobRunner.BuildUnrecordedCompilationOutcomeError(job));
                    return;
                }
                job.AssemblyReloadObserved = true;
                job.Phase = VmAutomationWorkspaceJobRunner.VerifyingPhase;
                job.StatusMessage =
                    "Compilation reload observed; verifying transaction commit evidence.";
                VmAutomationWorkspaceJobRunner.Persist(job);
            }
        }

        internal static bool HasCrossedMutationBoundary(VmAutomationWorkspaceJob job)
        {
            if (job?.JobType != JobType) return false;
            if (job.Phase == ApplyingPhase || job.Phase == SavingPhase ||
                job.Phase == PublishingPhase || job.Phase == RollingBackPhase ||
                job.Phase == VmAutomationWorkspaceJobRunner.RequestingCompilationPhase ||
                job.Phase == VmAutomationWorkspaceJobRunner.AwaitingCompilationStartPhase ||
                job.Phase == VmAutomationWorkspaceJobRunner.CompilingPhase ||
                job.Phase == VmAutomationWorkspaceJobRunner.WaitingForDomainReloadPhase ||
                job.Phase == VmAutomationWorkspaceJobRunner.VerifyingPhase)
                return true;
            return GetInt(job.TransactionState, "nextOperationIndex") > 0;
        }

        internal static void CancelBeforeMutation(VmAutomationWorkspaceJob job)
        {
            if (job?.JobType != JobType) return;
            VmAutomationAssetTransactionSnapshotStore.Cleanup(job.JobId);
            if (job.TransactionState != null)
                job.TransactionState["recoveryArtifactsRetained"] = false;
        }

        internal static object CleanupRecoveryArtifacts(VmAutomationWorkspaceJob job)
        {
            if (job?.JobType != JobType)
                return VmAutomationResponse.Error("The job is not an asset transaction.", "job_not_cleanable");
            if (!job.IsTerminal)
                return VmAutomationResponse.Error("The transaction is not terminal.", "job_not_terminal");
            if (!HasRecoveryArtifacts(job))
                return VmAutomationResponse.Error("The transaction has no retained recovery artifacts.",
                    "job_not_cleanable");

            VmAutomationAssetTransactionSnapshotStore.Cleanup(job.JobId);
            job.TransactionState["recoveryArtifactsRetained"] = false;
            job.TransactionState["cleanupStatus"] = "succeeded";
            VmAutomationWorkspaceJobRunner.Persist(job);
            return new Dictionary<string, object>
            {
                { "success", true },
                { "jobId", job.JobId },
                { "jobType", job.JobType },
                { "cleanupStatus", "succeeded" },
            };
        }

        internal static bool HasRecoveryArtifacts(VmAutomationWorkspaceJob job)
        {
            return job?.JobType == JobType && job.TransactionState != null &&
                   GetBool(job.TransactionState, "recoveryArtifactsRetained") &&
                   VmAutomationAssetTransactionSnapshotStore.SnapshotDirectoryExists(job.JobId);
        }

        private static void Prepare(VmAutomationWorkspaceJob job)
        {
            VmAutomationAssetTransactionPlan plan = VmAutomationAssetTransactionPlan.Build(job.Request);
            Dictionary<string, object> state = plan.ToPersistentState();
            VmAutomationAssetTransactionSnapshotStore.CaptureBaseline(job.JobId, plan, state);
            state["recoveryArtifactsRetained"] = true;
            state["cleanupStatus"] = "available";
            job.TransactionState = state;
            job.Phase = PreparedPhase;
            job.StatusMessage =
                $"Prepared {plan.Operations.Count} operation(s) and durably verified every baseline snapshot.";
            try
            {
                VmAutomationWorkspaceJobRunner.Persist(job);
            }
            catch
            {
                VmAutomationAssetTransactionSnapshotStore.Cleanup(job.JobId);
                throw;
            }
        }

        private static void ApplyNext(VmAutomationWorkspaceJob job)
        {
            Dictionary<string, object> state = RequireState(job);
            List<Dictionary<string, object>> operations = GetDictionaryList(state, "operations");
            int index = GetInt(state, "nextOperationIndex");
            if (index >= operations.Count)
            {
                job.Phase = SavingPhase;
                job.StatusMessage = "All operations applied; publishing the saved commit candidate.";
                VmAutomationWorkspaceJobRunner.Persist(job);
                return;
            }

            ThrowInjected("before-operation", index);
            object result = ExecuteOperation(operations[index]);
            ThrowInjected("after-operation", index);

            List<object> results = GetObjectList(state, "results");
            results.Add(result);
            state["results"] = results;
            state["nextOperationIndex"] = index + 1;
            state["checkpointEvidence"] =
                VmAutomationAssetTransactionSnapshotStore.CaptureCurrentEvidence(state);
            job.StatusMessage = $"Applied and checkpointed operation {index + 1} of {operations.Count}.";
            VmAutomationWorkspaceJobRunner.Persist(job);
        }

        private static void SaveAndPublish(VmAutomationWorkspaceJob job)
        {
            Dictionary<string, object> state = RequireState(job);
            ThrowInjected("before-commit-evidence", GetInt(state, "nextOperationIndex"));
            state["commitEvidence"] =
                VmAutomationAssetTransactionSnapshotStore.CaptureCurrentEvidence(state);
            ThrowInjected("after-commit-evidence", GetInt(state, "nextOperationIndex"));
            job.AssetRefreshInvocationCount = 1;
            job.Phase = PublishingPhase;
            job.StatusMessage =
                "Commit candidate hashes are durable; synchronously importing the published asset state.";
            VmAutomationWorkspaceJobRunner.Persist(job);

            ThrowInjected("before-refresh", GetInt(state, "nextOperationIndex"));
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport |
                                  ImportAssetOptions.ForceUpdate);
            job.AssetRefreshReturned = true;
            job.AssetRefreshReturnedAt = DateTime.UtcNow;
            ThrowInjected("after-refresh", GetInt(state, "nextOperationIndex"));
            VmAutomationAssetTransactionPlan.VerifyPostconditions(state);
            MoveToCompilationOrVerification(job);
        }

        private static void ResumePublishing(VmAutomationWorkspaceJob job)
        {
            Dictionary<string, object> state = RequireState(job);
            if (!VerifyCommitEvidence(state, out List<string> differences))
            {
                CompleteOutcomeUncertain(job,
                    "The persisted commit candidate no longer matches the asset workspace.",
                    differences);
                return;
            }
            VmAutomationAssetTransactionPlan.VerifyPostconditions(state);
            MoveToCompilationOrVerification(job);
        }

        private static void ReconcilePublishedCandidate(VmAutomationWorkspaceJob job)
        {
            Dictionary<string, object> state = RequireState(job);
            if (VerifyCommitEvidence(state, out _))
            {
                job.AssetRefreshDomainReloadObserved = true;
                job.AssetRefreshResult = new Dictionary<string, object>
                {
                    { "success", true },
                    { "completionEvidence", "domain-reload-after-persisted-commit-candidate" },
                };
                try
                {
                    VmAutomationAssetTransactionPlan.VerifyPostconditions(state);
                    MoveToCompilationOrVerification(job);
                }
                catch (Exception exception)
                {
                    Rollback(job, VmAutomationResponse.Error(exception.GetBaseException().Message,
                        "transaction_postcondition_failed", false));
                }
                return;
            }

            if (VmAutomationAssetTransactionSnapshotStore.VerifyEvidence(
                    state.TryGetValue("baselineEvidence", out object baseline) ? baseline : null,
                    out _))
            {
                CompleteRolledBack(job, VmAutomationResponse.Error(
                    "The Editor reloaded while publishing; the baseline state is intact.",
                    "asset_transaction_interrupted_before_publish", false));
                return;
            }

            VerifyCommitEvidence(state, out List<string> differences);
            CompleteOutcomeUncertain(job,
                "The Editor reloaded during publish and the workspace matches neither the baseline nor the commit candidate.",
                differences);
        }

        private static void MoveToCompilationOrVerification(VmAutomationWorkspaceJob job)
        {
            if (GetBool(RequireState(job), "compilationRequired"))
            {
                job.Phase = VmAutomationWorkspaceJobRunner.RequestingCompilationPhase;
                job.StatusMessage =
                    "Code-affecting asset transaction published; requesting a clean compilation.";
            }
            else
            {
                job.Phase = VmAutomationWorkspaceJobRunner.VerifyingPhase;
                job.StatusMessage = "Published assets imported; verifying final commit evidence.";
            }
            VmAutomationWorkspaceJobRunner.Persist(job);
        }

        private static void VerifyAndCommit(VmAutomationWorkspaceJob job)
        {
            Dictionary<string, object> state = RequireState(job);
            if (!VerifyCommitEvidence(state, out List<string> differences))
            {
                CompleteOutcomeUncertain(job,
                    "The asset workspace changed after the commit candidate was published.",
                    differences);
                return;
            }

            try
            {
                VmAutomationAssetTransactionPlan.VerifyPostconditions(state);
            }
            catch (Exception exception)
            {
                Rollback(job, VmAutomationResponse.Error(exception.GetBaseException().Message,
                    "transaction_postcondition_failed", false));
                return;
            }

            if (GetBool(state, "compilationRequired"))
            {
                if (!job.CompilationRequested || !job.CompilationStarted ||
                    !job.CompilationFinished || job.CompilationSucceeded != true ||
                    !job.AssemblyReloadObserved)
                {
                    Rollback(job, VmAutomationResponse.Error(
                        "Compilation evidence is incomplete for the code-affecting transaction.",
                        "compilation_evidence_incomplete", false));
                    return;
                }
                if (job.CompilerErrorCount > 0)
                {
                    Rollback(job, VmAutomationResponse.Error(
                        $"Compilation produced {job.CompilerErrorCount} error(s).",
                        "compilation_failed", false,
                        new Dictionary<string, object>
                        {
                            { "compilerMessages", job.CompilerMessages.Cast<object>().ToList() },
                        }));
                    return;
                }
            }

            job.Result = BuildTerminalResult(job, CommittedPhase);
            job.Error = null;
            job.Status = "succeeded";
            job.Phase = CommittedPhase;
            job.StatusMessage =
                "Transaction committed with verified asset, meta, postcondition, and compilation evidence.";
            job.CompletedAt = DateTime.UtcNow;
            VmAutomationAssetTransactionSnapshotStore.Cleanup(job.JobId);
            state["recoveryArtifactsRetained"] = false;
            state["cleanupStatus"] = "succeeded";
            VmAutomationWorkspaceJobRunner.Persist(job);
        }

        private static void Rollback(VmAutomationWorkspaceJob job,
            Dictionary<string, object> originalError)
        {
            Dictionary<string, object> state = job.TransactionState;
            if (state == null ||
                !state.TryGetValue("assetSnapshots", out object snapshots) || snapshots == null)
            {
                FailWithoutMutation(job, originalError);
                return;
            }

            state["originalError"] = originalError;
            job.Phase = RollingBackPhase;
            job.StatusMessage = "Restoring the durable baseline snapshot.";
            VmAutomationWorkspaceJobRunner.Persist(job);

            var errors = new List<string>();
            try
            {
                ThrowInjected("during-rollback", GetInt(state, "nextOperationIndex"));
                errors.AddRange(VmAutomationAssetTransactionSnapshotStore.RestoreAndVerify(
                    job.JobId, state));
                ThrowInjected("after-rollback-readback", GetInt(state, "nextOperationIndex"));
            }
            catch (Exception exception)
            {
                errors.Add(exception.GetBaseException().Message);
            }

            if (errors.Count == 0)
                CompleteRolledBack(job, originalError);
            else
                CompleteRollbackFailed(job, originalError, errors);
        }

        private static void CompleteRolledBack(VmAutomationWorkspaceJob job,
            Dictionary<string, object> originalError)
        {
            Dictionary<string, object> state = RequireState(job);
            job.Result = BuildTerminalResult(job, RolledBackPhase);
            job.Error = new Dictionary<string, object>(originalError ?? VmAutomationResponse.Error(
                "The transaction failed and was rolled back.", "asset_transaction_failed"))
            {
                ["terminalState"] = RolledBackPhase,
                ["rollbackVerified"] = true,
            };
            job.Status = "failed";
            job.Phase = RolledBackPhase;
            job.StatusMessage = "Transaction failed; the complete baseline was restored and read back.";
            job.CompletedAt = DateTime.UtcNow;
            VmAutomationAssetTransactionSnapshotStore.Cleanup(job.JobId);
            state["recoveryArtifactsRetained"] = false;
            state["cleanupStatus"] = "succeeded";
            VmAutomationWorkspaceJobRunner.Persist(job);
        }

        private static void CompleteRollbackFailed(VmAutomationWorkspaceJob job,
            Dictionary<string, object> originalError, List<string> rollbackErrors)
        {
            Dictionary<string, object> state = job.TransactionState ?? new Dictionary<string, object>();
            job.TransactionState = state;
            state["originalError"] = originalError;
            state["rollbackErrors"] = rollbackErrors.Cast<object>().ToList();
            state["recoveryArtifactsRetained"] =
                VmAutomationAssetTransactionSnapshotStore.SnapshotDirectoryExists(job.JobId);
            state["cleanupStatus"] = state["recoveryArtifactsRetained"] is true
                ? "available"
                : "succeeded";
            job.Result = BuildTerminalResult(job, RollbackFailedPhase);
            job.Error = VmAutomationResponse.Error(
                "The asset transaction failed and its baseline could not be fully restored.",
                "rollback_failed", false,
                new Dictionary<string, object>
                {
                    { "terminalState", RollbackFailedPhase },
                    { "originalError", originalError },
                    { "rollbackErrors", rollbackErrors.Cast<object>().ToList() },
                });
            job.Status = "failed";
            job.Phase = RollbackFailedPhase;
            job.StatusMessage =
                "Rollback verification failed; recovery snapshots are retained for explicit reconciliation.";
            job.CompletedAt = DateTime.UtcNow;
            VmAutomationWorkspaceJobRunner.Persist(job);
        }

        private static void CompleteOutcomeUncertain(VmAutomationWorkspaceJob job,
            string message, List<string> differences)
        {
            Dictionary<string, object> state = RequireState(job);
            state["outcomeDifferences"] = (differences ?? new List<string>())
                .Cast<object>().ToList();
            state["recoveryArtifactsRetained"] =
                VmAutomationAssetTransactionSnapshotStore.SnapshotDirectoryExists(job.JobId);
            state["cleanupStatus"] = state["recoveryArtifactsRetained"] is true
                ? "available"
                : "succeeded";
            job.Result = BuildTerminalResult(job, OutcomeUncertainPhase);
            job.Error = VmAutomationResponse.Error(message, "outcome_uncertain", false,
                new Dictionary<string, object>
                {
                    { "terminalState", OutcomeUncertainPhase },
                    { "differences", (differences ?? new List<string>()).Cast<object>().ToList() },
                    { "requiresReconciliation", true },
                });
            job.Status = "failed";
            job.Phase = OutcomeUncertainPhase;
            job.StatusMessage =
                "Transaction outcome is uncertain; snapshots are retained and no destructive reconciliation was attempted.";
            job.CompletedAt = DateTime.UtcNow;
            VmAutomationWorkspaceJobRunner.Persist(job);
        }

        private static void FailWithoutMutation(VmAutomationWorkspaceJob job,
            Dictionary<string, object> originalError)
        {
            job.Result = new Dictionary<string, object>
            {
                { "transactionId", job.JobId },
                { "terminalState", RolledBackPhase },
                { "operationCount", 0 },
                { "completedOperationCount", 0 },
                { "rollbackVerified", true },
            };
            job.Error = new Dictionary<string, object>(originalError ?? VmAutomationResponse.Error(
                "Asset transaction preparation failed.", "asset_transaction_failed"))
            {
                ["terminalState"] = RolledBackPhase,
                ["rollbackVerified"] = true,
            };
            job.Status = "failed";
            job.Phase = RolledBackPhase;
            job.StatusMessage = "Transaction failed before mutation; the baseline remained unchanged.";
            job.CompletedAt = DateTime.UtcNow;
            if (VmAutomationAssetTransactionSnapshotStore.SnapshotDirectoryExists(job.JobId))
                VmAutomationAssetTransactionSnapshotStore.Cleanup(job.JobId);
            VmAutomationWorkspaceJobRunner.Persist(job);
        }

        private static Dictionary<string, object> BuildTerminalResult(VmAutomationWorkspaceJob job,
            string terminalState)
        {
            Dictionary<string, object> state = job.TransactionState ?? new Dictionary<string, object>();
            var result = new Dictionary<string, object>
            {
                { "transactionId", job.JobId },
                { "terminalState", terminalState },
                { "operationCount", GetDictionaryList(state, "operations").Count },
                { "completedOperationCount", GetInt(state, "nextOperationIndex") },
                { "results", GetObjectList(state, "results") },
                { "baselineEvidence", state.TryGetValue("baselineEvidence", out object baseline)
                    ? baseline : new List<object>() },
            };
            if (state.TryGetValue("commitEvidence", out object commitEvidence))
                result["commitEvidence"] = commitEvidence;
            if (terminalState == RolledBackPhase)
                result["rollbackVerified"] = true;
            if (GetBool(state, "compilationRequired"))
            {
                result["compilationEvidence"] = new Dictionary<string, object>
                {
                    { "requested", job.CompilationRequested },
                    { "started", job.CompilationStarted },
                    { "finished", job.CompilationFinished },
                    { "assemblyReloadObserved", job.AssemblyReloadObserved },
                    { "compilerErrorCount", job.CompilerErrorCount },
                    { "compilerWarningCount", job.CompilerWarningCount },
                };
            }
            return result;
        }

        private static bool VerifyCommitEvidence(Dictionary<string, object> state,
            out List<string> differences)
        {
            return VmAutomationAssetTransactionSnapshotStore.VerifyEvidence(
                state != null && state.TryGetValue("commitEvidence", out object evidence)
                    ? evidence
                    : null, out differences);
        }

        private static object ExecuteOperation(Dictionary<string, object> operation)
        {
            string type = GetString(operation, "type");
            switch (type)
            {
                case "ensure-folder":
                {
                    string path = GetString(operation, "path");
                    List<string> created = EnsureFolderPath(path);
                    return new Dictionary<string, object>
                    {
                        { "type", type }, { "path", path },
                        { "created", created.Cast<object>().ToList() },
                    };
                }
                case "copy":
                {
                    string sourcePath = GetString(operation, "sourcePath");
                    string targetPath = GetString(operation, "targetPath");
                    EnsureFolderPath(VmAutomationAssetTransactionPlan.NormalizeAssetPath(
                        Path.GetDirectoryName(targetPath)));
                    if (!AssetDatabase.CopyAsset(sourcePath, targetPath))
                        throw new InvalidOperationException(
                            $"Copy failed: '{sourcePath}' -> '{targetPath}'.");
                    return new Dictionary<string, object>
                    {
                        { "type", type }, { "sourcePath", sourcePath },
                        { "targetPath", targetPath },
                    };
                }
                case "move":
                {
                    string sourcePath = GetString(operation, "sourcePath");
                    string targetPath = GetString(operation, "targetPath");
                    EnsureFolderPath(VmAutomationAssetTransactionPlan.NormalizeAssetPath(
                        Path.GetDirectoryName(targetPath)));
                    string error = AssetDatabase.MoveAsset(sourcePath, targetPath);
                    if (!string.IsNullOrEmpty(error))
                        throw new InvalidOperationException(error);
                    return new Dictionary<string, object>
                    {
                        { "type", type }, { "sourcePath", sourcePath },
                        { "targetPath", targetPath },
                    };
                }
                case "delete":
                {
                    string path = GetString(operation, "path");
                    if (!AssetDatabase.DeleteAsset(path))
                        throw new InvalidOperationException($"Delete failed: '{path}'.");
                    return new Dictionary<string, object>
                    {
                        { "type", type }, { "path", path },
                    };
                }
                case "serialized-set":
                {
                    object result = VmAutomationSerializedObjectCommands.SetForTransaction(operation);
                    if (VmAutomationResponse.TryGetError(result, out string message,
                            out string errorCode, out _))
                    {
                        throw new InvalidOperationException(
                            $"{errorCode ?? "serialized_object_set_failed"}: {message}");
                    }
                    return result;
                }
                default:
                    throw new InvalidOperationException(
                        $"Unsupported asset transaction operation '{type}'.");
            }
        }

        private static List<string> EnsureFolderPath(string path)
        {
            var created = new List<string>();
            if (string.IsNullOrEmpty(path) || path == "Assets" ||
                AssetDatabase.IsValidFolder(path))
                return created;
            string[] parts = path.Split('/');
            string current = parts[0];
            for (int index = 1; index < parts.Length; index++)
            {
                string next = current + "/" + parts[index];
                if (!AssetDatabase.IsValidFolder(next))
                {
                    string guid = AssetDatabase.CreateFolder(current, parts[index]);
                    if (string.IsNullOrEmpty(guid))
                        throw new InvalidOperationException(
                            $"Failed to create folder '{next}'.");
                    created.Add(next);
                }
                current = next;
            }
            return created;
        }

        private static void ResetCompilationEvidence(VmAutomationWorkspaceJob job)
        {
            job.CompilationRequested = false;
            job.CompilationStarted = false;
            job.CompilationFinished = false;
            job.CompilationSucceeded = null;
            job.AssemblyReloadObserved = false;
            job.CompilationRequestedAt = null;
            job.CompilationStartedAt = null;
            job.CompilationFinishedAt = null;
            job.CompilerErrorCount = 0;
            job.CompilerWarningCount = 0;
            job.CompilerMessages = new List<Dictionary<string, object>>();
        }

        private static Dictionary<string, object> GetOriginalError(VmAutomationWorkspaceJob job)
        {
            return job?.TransactionState != null &&
                   job.TransactionState.TryGetValue("originalError", out object value)
                ? VmAutomationResponse.ToDictionary(value)
                : VmAutomationResponse.Error("Asset transaction rollback resumed after reload.",
                    "asset_transaction_interrupted_during_rollback", false);
        }

        private static Dictionary<string, object> RequireState(VmAutomationWorkspaceJob job)
        {
            return job?.TransactionState ?? throw new InvalidDataException(
                $"Asset transaction '{job?.JobId}' has no persistent transaction state.");
        }

        private static void ThrowInjected(string boundary, int operationIndex)
        {
            Exception exception = FaultInjector?.Invoke(boundary, operationIndex);
            if (exception != null) throw exception;
        }

        private static string GetString(Dictionary<string, object> values, string key)
        {
            return VmAutomationAssetTransactionPlan.GetString(values, key);
        }

        private static int GetInt(Dictionary<string, object> values, string key)
        {
            return VmAutomationAssetTransactionPlan.GetInt(values, key);
        }

        private static bool GetBool(Dictionary<string, object> values, string key)
        {
            return VmAutomationAssetTransactionPlan.GetBool(values, key);
        }

        private static List<Dictionary<string, object>> GetDictionaryList(
            Dictionary<string, object> values, string key)
        {
            return VmAutomationAssetTransactionPlan.GetDictionaryList(values, key);
        }

        private static List<object> GetObjectList(Dictionary<string, object> values, string key)
        {
            if (values == null || !values.TryGetValue(key, out object value) ||
                !(value is IList list))
                return new List<object>();
            return list.Cast<object>().ToList();
        }
    }
}
