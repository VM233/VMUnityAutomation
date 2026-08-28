using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace VMUnityAutomation.Editor
{
    [InitializeOnLoad]
    internal static class VmAutomationWorkspaceJobRunner
    {
        internal const string AssetRefreshJobType = "asset-refresh";
        internal const string PackageUpdateJobType = "package-update";
        internal const string PackageResolveJobType = "package-resolve";

        private const string QueuedStatus = "queued";
        private const string RunningStatus = "running";
        private const string SucceededStatus = "succeeded";
        private const string FailedStatus = "failed";
        private const string CanceledStatus = "canceled";

        internal const string WaitingForEditorPhase = "waiting-for-editor";
        private const string RefreshingAssetsPhase = "refreshing-assets";
        internal const string RequestingCompilationPhase = "requesting-compilation";
        internal const string AwaitingCompilationStartPhase = "awaiting-compilation-start";
        internal const string CompilingPhase = "compiling";
        internal const string WaitingForDomainReloadPhase = "waiting-for-domain-reload";
        private const string UpdatingPackagePhase = "updating-package";
        private const string ResolvingPackagesPhase = "resolving-packages";
        internal const string VerifyingPhase = "verifying";
        private const string EditModeRequiredBlockedReason =
            "edit-mode-required";
        private const string WaitingForEditModeStatusMessage =
            "Waiting for stable Edit Mode before changing Package Manager " +
            "state. Exit Play Mode to continue this durable job.";
        private const string WaitingForClientAdoptionStatusMessage =
            "Accepted and durably queued. Poll jobs/get once to release execution.";
        private const string WaitingForClientAndEditModeStatusMessage =
            "Accepted and durably queued. Poll jobs/get once to release execution; " +
            "stable Edit Mode is also required.";
        internal const double PackageAdoptionTimeoutSeconds = 300.0;
        private const int MaxPackageRequestAttempts = 2;

        private static bool ticking;
        private static AddRequest activeAddRequest;
        private static string activePackageJobId;
        private static string activeCompilationJobId;
        private static object activeCompilationContext;
        private static readonly List<Dictionary<string, object>> ActiveCompilerMessages = new();

        static VmAutomationWorkspaceJobRunner()
        {
            VmAutomationWorkspaceJobStore.EnsureLoaded();
            VmAutomationAssetTransactionSnapshotStore.CleanupOrphanPreparingDirectories(
                VmAutomationWorkspaceJobStore.GetAll().Select(job => job.JobId));
            RecoverAfterReload();

            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            CompilationPipeline.compilationStarted -= OnCompilationStarted;
            CompilationPipeline.compilationStarted += OnCompilationStarted;
            CompilationPipeline.assemblyCompilationFinished -= OnAssemblyCompilationFinished;
            CompilationPipeline.assemblyCompilationFinished += OnAssemblyCompilationFinished;
            CompilationPipeline.compilationFinished -= OnCompilationFinished;
            CompilationPipeline.compilationFinished += OnCompilationFinished;
            AssemblyReloadEvents.afterAssemblyReload -= OnAfterAssemblyReload;
            AssemblyReloadEvents.afterAssemblyReload += OnAfterAssemblyReload;
            Events.registeredPackages -= OnPackagesRegistered;
            Events.registeredPackages += OnPackagesRegistered;
        }

        internal static object StartAssetRefresh(Dictionary<string, object> args)
        {
            return Start(AssetRefreshJobType, "asset/refresh", args, null);
        }

        internal static object StartAssetTransaction(Dictionary<string, object> args)
        {
            return Start(VmAutomationAssetTransactionJobRunner.JobType,
                VmAutomationAssetTransactionJobRunner.Operation, args, null);
        }

        internal static object StartGitPackageUpdate(Dictionary<string, object> args)
        {
            if (!VmAutomationPackageManagerCommands.TryBuildGitPackageIdentifier(
                    args, out string name, out string identifier, out string revision,
                    out string error))
            {
                return VmAutomationResponse.Error(error, "invalid_arguments");
            }

            var packageTarget = new VmAutomationGitPackageExpectation(name, identifier, revision);
            return Start(PackageUpdateJobType, "packages/update-git", args,
                new List<VmAutomationGitPackageExpectation> { packageTarget });
        }

        internal static object StartPackageResolve(Dictionary<string, object> args)
        {
            if (!VmAutomationPackageManagerCommands.TryBuildExpectedGitPackages(
                    args, out List<VmAutomationGitPackageExpectation> expectations, out string error))
            {
                return VmAutomationResponse.Error(error, "invalid_arguments");
            }

            var manifestStates = expectations.Select(
                VmAutomationPackageManagerCommands.BuildGitPackageResolutionState).ToList();
            if (manifestStates.Any(state => !GetBool(state, "manifestMatches")))
            {
                return VmAutomationResponse.Error(
                    "packages/resolve requires Packages/manifest.json to already declare every exact Git URL and full commit SHA.",
                    "package_manifest_target_mismatch", false,
                    new Dictionary<string, object>
                    {
                        { "packageState", manifestStates.Cast<object>().ToList() },
                    });
            }

            return Start(PackageResolveJobType, "packages/resolve", args, expectations);
        }

        internal static object StartPlayModeTransition(
            Dictionary<string, object> args)
        {
            return Start(
                VmAutomationPlayModeJobRunner.JobType,
                VmAutomationPlayModeJobRunner.Operation,
                args,
                null);
        }

        internal static bool OwnsJobType(string jobType)
        {
            return jobType == AssetRefreshJobType || jobType == PackageUpdateJobType ||
                   jobType == PackageResolveJobType ||
                   jobType == VmAutomationPlayModeJobRunner.JobType ||
                   jobType == VmAutomationAssetTransactionJobRunner.JobType;
        }

        internal static bool ContainsJob(string jobType, string jobId)
        {
            if (string.IsNullOrWhiteSpace(jobType) ||
                string.IsNullOrWhiteSpace(jobId))
                return false;
            VmAutomationWorkspaceJob job = VmAutomationWorkspaceJobStore.Find(jobId);
            return job != null &&
                   string.Equals(job.JobType, jobType, StringComparison.Ordinal);
        }

        internal static bool HasActiveJob =>
            VmAutomationWorkspaceJobStore.GetAll().Any(job => !job.IsTerminal);

        internal static object Get(Dictionary<string, object> args)
        {
            args ??= new Dictionary<string, object>();
            string jobId = GetString(args, "jobId");
            string requestId = GetString(args, "requestId");
            string jobType = GetString(args, "jobType");
            if (string.IsNullOrWhiteSpace(jobId) &&
                string.IsNullOrWhiteSpace(requestId))
            {
                return VmAutomationResponse.Error(
                    "jobs/get requires jobId or requestId.", "invalid_arguments");
            }
            VmAutomationWorkspaceJob job = !string.IsNullOrWhiteSpace(jobId)
                ? VmAutomationWorkspaceJobStore.Find(jobId)
                : VmAutomationWorkspaceJobStore.FindByRequestId(requestId, jobType);
            if (job == null)
            {
                string identity = !string.IsNullOrWhiteSpace(jobId) ? jobId : requestId;
                return VmAutomationResponse.Error(
                    $"Workspace job '{identity}' was not found.", "job_not_found");
            }
            if (!CanAccess(job, args))
            {
                return VmAutomationResponse.Error(
                    "Job belongs to another agent and the jobAccessToken was not supplied.",
                    "job_owner_mismatch");
            }
            Adopt(job);
            return BuildPublicJob(job, includeAccessToken: false);
        }

        internal static object Cancel(Dictionary<string, object> args)
        {
            args ??= new Dictionary<string, object>();
            string jobId = GetString(args, "jobId");
            if (string.IsNullOrWhiteSpace(jobId))
                return VmAutomationResponse.Error("jobId is required.", "invalid_arguments");
            VmAutomationWorkspaceJob job = VmAutomationWorkspaceJobStore.Find(jobId);
            if (job == null)
                return VmAutomationResponse.Error($"Job '{jobId}' was not found.", "job_not_found");
            if (!CanAccess(job, args))
            {
                return VmAutomationResponse.Error(
                    "Job belongs to another agent and the jobAccessToken was not supplied.",
                    "job_owner_mismatch");
            }
            if (job.IsTerminal)
            {
                return VmAutomationResponse.Error(
                    $"Job '{jobId}' is already terminal with status '{job.Status}'.",
                    "job_not_cancellable", false, BuildPublicJob(job, false));
            }
            if (HasCrossedMutationBoundary(job))
            {
                return VmAutomationResponse.Error(
                    $"Job '{jobId}' has crossed its mutation boundary at phase '{job.Phase}'.",
                    "job_not_cancellable", false, BuildPublicJob(job, false));
            }

            VmAutomationAssetTransactionJobRunner.CancelBeforeMutation(job);
            job.Status = CanceledStatus;
            job.StatusMessage = "Canceled before the workspace mutation began.";
            job.CompletedAt = DateTime.UtcNow;
            TouchAndSave(job);
            return BuildPublicJob(job, includeAccessToken: false);
        }

        internal static object Cleanup(Dictionary<string, object> args)
        {
            args ??= new Dictionary<string, object>();
            string jobId = GetString(args, "jobId");
            if (string.IsNullOrWhiteSpace(jobId))
                return VmAutomationResponse.Error("jobId is required.", "invalid_arguments");
            VmAutomationWorkspaceJob job = VmAutomationWorkspaceJobStore.Find(jobId);
            if (job == null)
                return VmAutomationResponse.Error($"Job '{jobId}' was not found.", "job_not_found");
            if (!CanAccess(job, args))
            {
                return VmAutomationResponse.Error(
                    "Job belongs to another agent and the jobAccessToken was not supplied.",
                    "job_owner_mismatch");
            }
            return VmAutomationAssetTransactionJobRunner.CleanupRecoveryArtifacts(job);
        }

        private static object Start(string jobType, string operation,
            Dictionary<string, object> args, List<VmAutomationGitPackageExpectation> expectedPackages)
        {
            args ??= new Dictionary<string, object>();
            string owner = GetString(args, "_agentId", "anonymous");
            string requestId = GetString(args, "_requestId");
            string idempotencyKey = GetString(args, "idempotencyKey");
            Dictionary<string, object> request = StripTransportArguments(args);
            string fingerprint = ComputeFingerprint(jobType, operation, request);

            if (!string.IsNullOrWhiteSpace(requestId))
            {
                VmAutomationWorkspaceJob existingByRequest =
                    VmAutomationWorkspaceJobStore.FindByRequestId(requestId, jobType);
                if (existingByRequest != null)
                    return RequireMatchingExisting(existingByRequest, fingerprint, owner);
            }
            if (!string.IsNullOrWhiteSpace(idempotencyKey))
            {
                VmAutomationWorkspaceJob existingByKey =
                    VmAutomationWorkspaceJobStore.FindByIdempotencyKey(operation, idempotencyKey);
                if (existingByKey != null)
                    return RequireMatchingExisting(existingByKey, fingerprint, owner);
            }

            DateTime now = DateTime.UtcNow;
            var job = new VmAutomationWorkspaceJob
            {
                JobId = Guid.NewGuid().ToString("N"),
                JobAccessToken = Guid.NewGuid().ToString("N"),
                JobType = jobType,
                Operation = operation,
                OwnerAgentId = owner,
                IdempotencyKey = idempotencyKey,
                RequestId = requestId,
                RequestFingerprint = fingerprint,
                Status = QueuedStatus,
                Phase = WaitingForEditorPhase,
                StatusMessage =
                    (operation == "packages/update-git" ||
                     operation == "packages/resolve") &&
                    !VmAutomationRuntimePreconditions.IsStableEditMode
                        ? WaitingForClientAndEditModeStatusMessage
                        : WaitingForClientAdoptionStatusMessage,
                Request = request,
                CreatedAt = now,
                UpdatedAt = now,
                ClientAdopted = false,
                ExpectedPackages = expectedPackages ?? new List<VmAutomationGitPackageExpectation>(),
            };
            if (expectedPackages != null && expectedPackages.Count == 1)
            {
                job.PackageName = expectedPackages[0].Name;
                job.RequestedPackageIdentifier = expectedPackages[0].Identifier;
                job.RequestedPackageRevision = expectedPackages[0].Revision;
            }

            VmAutomationWorkspaceJobStore.Add(job);
            Record(job);
            return BuildPublicJob(job, includeAccessToken: true);
        }

        private static object RequireMatchingExisting(VmAutomationWorkspaceJob existing,
            string fingerprint, string ownerAgentId)
        {
            if (!string.Equals(existing.OwnerAgentId, ownerAgentId,
                    StringComparison.Ordinal))
            {
                return VmAutomationResponse.Error(
                    "The persistent request identity belongs to another agent.",
                    "job_owner_mismatch");
            }
            if (!string.Equals(existing.RequestFingerprint, fingerprint,
                    StringComparison.Ordinal))
            {
                return VmAutomationResponse.Error(
                    "The persistent request identity was already used with different arguments.",
                    "idempotency_conflict", false,
                    new Dictionary<string, object>
                    {
                        { "jobId", existing.JobId },
                        { "operation", existing.Operation },
                    });
            }

            Dictionary<string, object> response = BuildPublicJob(existing, includeAccessToken: true);
            VmAutomationContractMetadata.AddTag(response, VmAutomationContractMetadata.Tag.Reused);
            return response;
        }

        private static void Tick()
        {
            if (ticking)
                return;

            List<VmAutomationWorkspaceJob> pendingJobs =
                VmAutomationWorkspaceJobStore.GetAll()
                    .Where(candidate => !candidate.IsTerminal)
                    .OrderBy(candidate => candidate.CreatedAt)
                    .ToList();
            VmAutomationWorkspaceJob job = pendingJobs
                .FirstOrDefault(IsStopPlayModeTransition) ??
                pendingJobs.FirstOrDefault();
            if (job == null)
                return;

            if (!job.ClientAdopted)
            {
                if (!VmAutomationWorkspaceJobAdoptionStore.IsPublished(job.JobId))
                    return;
                Adopt(job);
            }

            EditorApplication.QueuePlayerLoopUpdate();
            if (job.Status == QueuedStatus)
            {
                job.Status = RunningStatus;
                job.StartedAt = DateTime.UtcNow;
                job.StatusMessage = "Running the accepted workspace operation.";
                TouchAndSave(job);
            }

            if (RequiresStableEditMode(job) &&
                !VmAutomationRuntimePreconditions.IsStableEditMode)
            {
                if (!string.Equals(job.StatusMessage,
                        WaitingForEditModeStatusMessage,
                        StringComparison.Ordinal))
                {
                    job.StatusMessage = WaitingForEditModeStatusMessage;
                    TouchAndSave(job);
                }
                return;
            }

            if (job.Phase == UpdatingPackagePhase)
            {
                ObservePackageUpdate(job);
                return;
            }
            if (job.Phase == ResolvingPackagesPhase)
            {
                ObservePackageResolve(job);
                return;
            }
            if (job.Phase == AwaitingCompilationStartPhase ||
                job.Phase == CompilingPhase ||
                job.Phase == WaitingForDomainReloadPhase)
            {
                return;
            }
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                return;

            ticking = true;
            try
            {
                if (VmAutomationPlayModeJobRunner.ExecutePhase(job))
                    return;
                if (VmAutomationAssetTransactionJobRunner.ExecutePhase(job))
                    return;
                switch (job.Phase)
                {
                    case WaitingForEditorPhase:
                        if (job.Operation == "asset/refresh")
                            ExecuteAssetRefresh(job);
                        else if (job.Operation == "packages/update-git")
                            IssuePackageUpdate(job);
                        else if (job.Operation == "packages/resolve")
                            IssuePackageResolve(job);
                        else
                            throw new InvalidOperationException(
                                $"Unsupported workspace operation '{job.Operation}'.");
                        break;
                    case RefreshingAssetsPhase:
                        ExecuteAssetRefresh(job);
                        break;
                    case RequestingCompilationPhase:
                        RequestCompilation(job);
                        break;
                    case VerifyingPhase:
                        VerifyAndComplete(job);
                        break;
                    default:
                        throw new InvalidOperationException(
                            $"Workspace job '{job.JobId}' has unknown phase '{job.Phase}'.");
                }
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (job.JobType == VmAutomationAssetTransactionJobRunner.JobType)
                    VmAutomationAssetTransactionJobRunner.FailAndRollback(job, exception);
                else
                    Fail(job, VmAutomationResponse.Error(exception.GetBaseException().Message,
                        "workspace_job_execution_failed", false));
            }
            finally
            {
                ticking = false;
            }
        }

        private static void ExecuteAssetRefresh(VmAutomationWorkspaceJob job)
        {
            job.Phase = RefreshingAssetsPhase;
            job.StatusMessage = "Invoking AssetDatabase refresh on the Unity main thread.";
            job.AssetRefreshInvocationCount++;
            TouchAndSave(job);

            object result = VmAutomationAssetCommands.ExecuteRefreshImmediate(job.Request);
            if (VmAutomationResponse.TryGetError(result, out string message, out string errorCode, out _))
            {
                Fail(job, VmAutomationResponse.Error(message,
                    string.IsNullOrEmpty(errorCode) ? "asset_refresh_failed" : errorCode,
                    false));
                return;
            }

            job.AssetRefreshResult = VmAutomationResponse.ToDictionary(result) ??
                                     new Dictionary<string, object>();
            job.AssetRefreshReturned = true;
            job.AssetRefreshReturnedAt = DateTime.UtcNow;
            job.Phase = RequestingCompilationPhase;
            job.StatusMessage = "Asset refresh returned; a clean script compilation is required.";
            TouchAndSave(job);
        }

        private static void RequestCompilation(VmAutomationWorkspaceJob job)
        {
            if (job.CompilationRequested)
                throw new InvalidOperationException(
                    $"Workspace job '{job.JobId}' attempted to request compilation twice.");

            activeCompilationJobId = job.JobId;
            activeCompilationContext = null;
            ActiveCompilerMessages.Clear();
            job.CompilationRequested = true;
            job.CompilationRequestedAt = DateTime.UtcNow;
            job.Phase = AwaitingCompilationStartPhase;
            job.StatusMessage = "Clean script compilation was requested.";
            TouchAndSave(job);

            CompilationPipeline.RequestScriptCompilation(
                RequestScriptCompilationOptions.CleanBuildCache);
            EditorApplication.QueuePlayerLoopUpdate();
        }

        private static void IssuePackageUpdate(VmAutomationWorkspaceJob job)
        {
            if (job.PackageRequestIssued)
                throw new InvalidOperationException(
                    $"Workspace job '{job.JobId}' attempted to issue a package update twice.");

            job.PackageRequestIssued = true;
            job.PackageRequestAttemptCount++;
            job.PackageRequestIssuedAt = DateTime.UtcNow;
            job.Phase = UpdatingPackagePhase;
            job.StatusMessage = $"Updating package '{job.PackageName}' to " +
                                $"'{job.RequestedPackageRevision}' " +
                                $"(attempt {job.PackageRequestAttemptCount} of " +
                                $"{MaxPackageRequestAttempts}).";
            TouchAndSave(job);

            activeAddRequest = Client.Add(job.RequestedPackageIdentifier);
            activePackageJobId = job.JobId;
        }

        private static void ObservePackageUpdate(VmAutomationWorkspaceJob job)
        {
            if (EditorApplication.isUpdating && !job.PackageUpdatingObserved)
            {
                job.PackageUpdatingObserved = true;
                TouchAndSave(job);
            }

            if (activeAddRequest != null && activePackageJobId == job.JobId)
            {
                if (!activeAddRequest.IsCompleted)
                    return;

                if (activeAddRequest.Status == StatusCode.Failure)
                {
                    Error packageError = activeAddRequest.Error;
                    string message = packageError?.message ??
                                     "Unity Package Manager failed to update the package.";
                    string packageErrorCode = packageError?.errorCode.ToString() ?? "";
                    var failure = new Dictionary<string, object>
                    {
                        { "attempt", job.PackageRequestAttemptCount },
                        { "errorCode", packageErrorCode },
                        { "message", message },
                        { "observedAt", DateTime.UtcNow.ToString("O") },
                    };
                    job.PackageRequestFailures.Add(failure);
                    activeAddRequest = null;
                    activePackageJobId = null;

                    // A Package Manager request can report cancellation when an
                    // overlapping internal resolve supersedes it. First accept an
                    // already-adopted target; otherwise make one bounded retry after
                    // the Editor returns to an idle package state.
                    if (TryAdoptPackageTarget(job))
                        return;
                    if (IsTransientPackageCancellation(packageErrorCode, message) &&
                        job.PackageRequestAttemptCount < MaxPackageRequestAttempts)
                    {
                        job.PackageRequestIssued = false;
                        job.PackageRequestCompleted = false;
                        job.PackageUpdatingObserved = false;
                        job.Phase = WaitingForEditorPhase;
                        job.StatusMessage =
                            $"Package Manager cancelled update attempt " +
                            $"{job.PackageRequestAttemptCount}; retrying once after " +
                            $"the Editor is idle.";
                        TouchAndSave(job);
                        return;
                    }

                    Fail(job, VmAutomationResponse.Error(message,
                        "package_update_failed", false,
                        BuildPackageRequestFailureDetails(job, packageErrorCode)));
                    return;
                }

                activeAddRequest = null;
                activePackageJobId = null;
                job.PackageRequestCompleted = true;
                job.PackageRequestCompletedAt = DateTime.UtcNow;
                job.StatusMessage =
                    "Package Manager completed the add request; waiting for Unity to register the exact package target.";
                TouchAndSave(job);
            }

            if (TryAdoptPackageTarget(job))
                return;

            if (ShouldFailPackageAdoption(job, targetsMatch: false, DateTime.UtcNow))
            {
                FailPackageAdoptionTimeout(job);
            }
        }

        internal static bool IsTransientPackageCancellation(
            string errorCode, string message)
        {
            return (!string.IsNullOrWhiteSpace(errorCode) &&
                    errorCode.IndexOf("cancel", StringComparison.OrdinalIgnoreCase) >= 0) ||
                   (!string.IsNullOrWhiteSpace(message) &&
                    message.IndexOf("cancel", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        private static Dictionary<string, object> BuildPackageRequestFailureDetails(
            VmAutomationWorkspaceJob job, string packageErrorCode)
        {
            return new Dictionary<string, object>
            {
                { "packageName", job.PackageName ?? "" },
                { "requestedIdentifier", job.RequestedPackageIdentifier ?? "" },
                { "requestedRevision", job.RequestedPackageRevision ?? "" },
                { "attemptCount", job.PackageRequestAttemptCount },
                { "maximumAttempts", MaxPackageRequestAttempts },
                { "packageManagerErrorCode", packageErrorCode ?? "" },
                { "packageState", job.PackageState },
                { "failures", job.PackageRequestFailures.Cast<object>().ToList() },
            };
        }

        private static void IssuePackageResolve(VmAutomationWorkspaceJob job)
        {
            if (job.PackageResolveInvoked)
                throw new InvalidOperationException(
                    $"Workspace job '{job.JobId}' attempted to resolve packages twice.");

            job.PackageResolveInvoked = true;
            job.PackageRequestIssuedAt = DateTime.UtcNow;
            job.Phase = ResolvingPackagesPhase;
            job.StatusMessage = "Resolving the declared Package Manager target state.";
            TouchAndSave(job);

            Client.Resolve();
            if (EditorApplication.isUpdating)
            {
                job.PackageUpdatingObserved = true;
                TouchAndSave(job);
            }
            ObservePackageResolve(job);
        }

        private static void ObservePackageResolve(VmAutomationWorkspaceJob job)
        {
            if (EditorApplication.isUpdating)
            {
                if (!job.PackageUpdatingObserved)
                {
                    job.PackageUpdatingObserved = true;
                    TouchAndSave(job);
                }
                return;
            }

            if (TryAdoptPackageTarget(job))
                return;

            if (ShouldFailPackageAdoption(job, targetsMatch: false, DateTime.UtcNow))
                FailPackageAdoptionTimeout(job);
        }

        private static bool TryAdoptPackageTarget(VmAutomationWorkspaceJob job)
        {
            if (!PackageTargetsMatch(job, out Dictionary<string, object> state))
            {
                job.PackageState = state;
                return false;
            }

            job.PackageState = state;
            job.PackageRequestCompleted = true;
            job.PackageRequestCompletedAt ??= DateTime.UtcNow;
            job.Phase = RefreshingAssetsPhase;
            job.StatusMessage = "Package target adopted; refreshing assets before compilation.";
            TouchAndSave(job);
            return true;
        }

        internal static bool ShouldFailPackageAdoption(VmAutomationWorkspaceJob job,
            bool targetsMatch, DateTime nowUtc)
        {
            if (job == null || targetsMatch || !job.PackageRequestIssuedAt.HasValue)
                return false;
            return nowUtc - job.PackageRequestIssuedAt.Value >=
                   TimeSpan.FromSeconds(PackageAdoptionTimeoutSeconds);
        }

        private static void FailPackageAdoptionTimeout(VmAutomationWorkspaceJob job)
        {
            Fail(job, VmAutomationResponse.Error(
                "Unity Package Manager did not register the exact manifest, lockfile, " +
                "and resolved package fingerprint target within the package adoption timeout.",
                "package_adoption_timeout", false,
                new Dictionary<string, object> { { "packageState", job.PackageState } }));
        }

        private static bool PackageTargetsMatch(VmAutomationWorkspaceJob job,
            out Dictionary<string, object> state)
        {
            var packageStates = new List<object>();
            bool allMatch = job.ExpectedPackages.Count > 0;
            foreach (VmAutomationGitPackageExpectation expectation in job.ExpectedPackages)
            {
                Dictionary<string, object> packageState =
                    VmAutomationPackageManagerCommands.BuildGitPackageResolutionState(expectation);
                packageStates.Add(packageState);
                allMatch &= GetBool(packageState, "matches");
            }

            state = new Dictionary<string, object>
            {
                { "matches", allMatch },
                { "packages", packageStates },
            };
            return allMatch;
        }

        private static void VerifyAndComplete(VmAutomationWorkspaceJob job)
        {
            if (job.JobType == VmAutomationAssetTransactionJobRunner.JobType)
            {
                VmAutomationAssetTransactionJobRunner.ExecutePhase(job);
                return;
            }
            if (!HasCompleteRefreshCompilationEvidence(job, requireCompilation: false))
            {
                Fail(job, VmAutomationResponse.Error(
                    "Asset refresh execution evidence is incomplete.",
                    "asset_refresh_evidence_incomplete", false));
                return;
            }
            if (!HasCompleteRefreshCompilationEvidence(job, requireCompilation: true))
            {
                Fail(job, VmAutomationResponse.Error(
                    "Compilation or assembly reload execution evidence is incomplete.",
                    "compilation_evidence_incomplete", false));
                return;
            }
            if (job.CompilerErrorCount > 0)
            {
                Fail(job, VmAutomationResponse.Error(
                    $"Compilation produced {job.CompilerErrorCount} error(s).",
                    "compilation_failed", false));
                return;
            }
            if (job.ExpectedPackages.Count > 0 &&
                !PackageTargetsMatch(job, out Dictionary<string, object> packageState))
            {
                job.PackageState = packageState;
                Fail(job, VmAutomationResponse.Error(
                    "Package state changed before final workspace verification.",
                    "package_registration_mismatch", false,
                    new Dictionary<string, object> { { "packageState", packageState } }));
                return;
            }

            job.Result = new Dictionary<string, object>
            {
                { "assetRefreshInvoked", true },
                { "assetRefreshInvocationCount", job.AssetRefreshInvocationCount },
                { "assetRefreshReturnedAt", FormatDate(job.AssetRefreshReturnedAt) },
                { "assetRefreshDomainReloadObserved", job.AssetRefreshDomainReloadObserved },
                { "assetRefresh", job.AssetRefreshResult },
                { "compilationRequested", true },
                { "compilationStartedAt", FormatDate(job.CompilationStartedAt) },
                { "compilationFinishedAt", FormatDate(job.CompilationFinishedAt) },
                { "assemblyReloadObserved", true },
                { "compilerErrorCount", job.CompilerErrorCount },
                { "compilerWarningCount", job.CompilerWarningCount },
                { "compilerMessages", job.CompilerMessages.Cast<object>().ToList() },
                { "packageState", job.PackageState },
            };
            job.Status = SucceededStatus;
            job.Phase = SucceededStatus;
            job.StatusMessage = "Workspace refresh, compilation, and target verification completed.";
            job.CompletedAt = DateTime.UtcNow;
            TouchAndSave(job);
        }

        internal static bool HasCompleteRefreshCompilationEvidence(
            VmAutomationWorkspaceJob job, bool requireCompilation = true)
        {
            if (job == null || job.AssetRefreshInvocationCount != 1 ||
                (!job.AssetRefreshReturned && !job.AssetRefreshDomainReloadObserved))
                return false;
            return !requireCompilation ||
                   (job.CompilationRequested && job.CompilationStarted &&
                    job.CompilationFinished && job.CompilationSucceeded == true &&
                    job.AssemblyReloadObserved);
        }

        internal static bool RecordReloadBeforeCompilation(VmAutomationWorkspaceJob job)
        {
            if (job == null || job.Phase != RequestingCompilationPhase)
                return false;

            job.AssetRefreshDomainReloadObserved = true;
            job.StatusMessage =
                "The published assets are loaded; requesting the explicit clean compilation.";
            return true;
        }

        internal static void AdvanceAfterSuccessfulCompilation(VmAutomationWorkspaceJob job)
        {
            if (job.AssetRefreshDomainReloadObserved)
            {
                job.AssemblyReloadObserved = true;
                job.Phase = VerifyingPhase;
                job.StatusMessage =
                    "Compilation finished in the post-refresh assembly domain; verifying the workspace result.";
                return;
            }

            job.Phase = WaitingForDomainReloadPhase;
            job.StatusMessage = "Compilation finished; waiting for the new assembly domain.";
        }

        private static void OnCompilationStarted(object context)
        {
            VmAutomationWorkspaceJob job = FindActiveCompilationJob();
            if (job == null || job.Phase != AwaitingCompilationStartPhase)
                return;

            activeCompilationContext = context;
            ActiveCompilerMessages.Clear();
            job.CompilationStarted = true;
            job.CompilationStartedAt = DateTime.UtcNow;
            job.Phase = CompilingPhase;
            job.StatusMessage = "Unity script compilation is running.";
            TouchAndSave(job);
        }

        private static void OnAssemblyCompilationFinished(string assemblyPath,
            CompilerMessage[] messages)
        {
            if (FindActiveCompilationJob() == null || messages == null)
                return;

            foreach (CompilerMessage message in messages)
            {
                if (!VmAutomationCompilerDiagnosticUtility.IsDiagnostic(message))
                    continue;

                ActiveCompilerMessages.Add(new Dictionary<string, object>
                {
                    { "assemblyPath", assemblyPath ?? "" },
                    { "type", message.type.ToString() },
                    { "message", message.message ?? "" },
                    { "file", message.file ?? "" },
                    { "line", message.line },
                    { "column", message.column },
                });
            }
        }

        private static void OnCompilationFinished(object context)
        {
            VmAutomationWorkspaceJob job = FindActiveCompilationJob();
            if (job == null || job.Phase != CompilingPhase)
                return;
            if (activeCompilationContext != null &&
                !Equals(activeCompilationContext, context))
                return;

            job.CompilerMessages = ActiveCompilerMessages
                .Select(message => new Dictionary<string, object>(message)).ToList();
            job.CompilerErrorCount = job.CompilerMessages.Count(message =>
                string.Equals(GetString(message, "type"), CompilerMessageType.Error.ToString(),
                    StringComparison.Ordinal));
            job.CompilerWarningCount = job.CompilerMessages.Count(message =>
                string.Equals(GetString(message, "type"), CompilerMessageType.Warning.ToString(),
                    StringComparison.Ordinal));
            job.CompilationFinished = true;
            bool unityScriptCompilationFailed = EditorUtility.scriptCompilationFailed;
            Dictionary<string, object> compilationFailure = BuildCompilationFailure(
                job, unityScriptCompilationFailed);
            job.CompilationSucceeded = compilationFailure == null;
            job.CompilationFinishedAt = DateTime.UtcNow;
            activeCompilationJobId = null;
            activeCompilationContext = null;
            ActiveCompilerMessages.Clear();

            if (compilationFailure != null)
            {
                FailCompilation(job, compilationFailure);
                return;
            }

            AdvanceAfterSuccessfulCompilation(job);
            TouchAndSave(job);
        }

        private static void OnAfterAssemblyReload()
        {
            AdoptCompletedCompilationReloads();
        }

        private static void OnPackagesRegistered(PackageRegistrationEventArgs _)
        {
            VmAutomationWorkspaceJob job = VmAutomationWorkspaceJobStore.GetAll()
                .Where(candidate => !candidate.IsTerminal &&
                                    (candidate.Phase == UpdatingPackagePhase ||
                                     candidate.Phase == ResolvingPackagesPhase))
                .OrderBy(candidate => candidate.CreatedAt).FirstOrDefault();
            if (job == null)
                return;

            job.PackageRegistrationObserved = true;
            TouchAndSave(job);
            TryAdoptPackageTarget(job);
        }

        private static VmAutomationWorkspaceJob FindActiveCompilationJob()
        {
            if (!string.IsNullOrEmpty(activeCompilationJobId))
                return VmAutomationWorkspaceJobStore.Find(activeCompilationJobId);
            return VmAutomationWorkspaceJobStore.GetAll().FirstOrDefault(job =>
                !job.IsTerminal &&
                (job.Phase == AwaitingCompilationStartPhase || job.Phase == CompilingPhase));
        }

        private static void RecoverAfterReload()
        {
            foreach (VmAutomationWorkspaceJob job in VmAutomationWorkspaceJobStore.GetAll()
                         .Where(candidate => !candidate.IsTerminal &&
                                             candidate.Status == RunningStatus))
            {
                job.RecoveredAfterReload = true;
                job.DomainReloadCount++;
                if (job.JobType ==
                    VmAutomationPlayModeJobRunner.JobType)
                {
                    VmAutomationPlayModeJobRunner
                        .RecoverAfterReload(job);
                    continue;
                }
                bool recordedReloadBeforeCompilation = RecordReloadBeforeCompilation(job);
                if (recordedReloadBeforeCompilation)
                    TouchAndSave(job);
                if (job.JobType == VmAutomationAssetTransactionJobRunner.JobType)
                {
                    VmAutomationAssetTransactionJobRunner.RecoverAfterReload(job);
                    continue;
                }
                if (recordedReloadBeforeCompilation)
                    continue;
                if (job.Phase == WaitingForDomainReloadPhase && job.CompilationFinished)
                {
                    if (job.CompilationSucceeded != true)
                    {
                        FailCompilation(job, BuildUnrecordedCompilationOutcomeError(job));
                        continue;
                    }
                    job.AssemblyReloadObserved = true;
                    job.Phase = VerifyingPhase;
                    job.StatusMessage = "Assembly reload observed; verifying the workspace result.";
                    TouchAndSave(job);
                    continue;
                }
                if (job.Phase == CompilingPhase ||
                    job.Phase == AwaitingCompilationStartPhase)
                {
                    Fail(job, VmAutomationResponse.Error(
                        "The Unity domain reloaded before compilation completion was recorded.",
                        "compilation_outcome_uncertain_after_reload", false));
                    continue;
                }
                if (job.Phase == RefreshingAssetsPhase)
                {
                    if (job.AssetRefreshInvocationCount == 0)
                    {
                        job.StatusMessage =
                            "The package target is adopted; the durable refresh is still pending.";
                        TouchAndSave(job);
                        continue;
                    }
                    job.AssetRefreshDomainReloadObserved = true;
                    job.AssetRefreshResult = new Dictionary<string, object>
                    {
                        { "success", true },
                        { "completionEvidence", "domain-reload-during-synchronous-refresh" },
                    };
                    job.Phase = RequestingCompilationPhase;
                    job.StatusMessage =
                        "The refresh-triggered domain reload was observed; requesting the explicit clean compilation.";
                    TouchAndSave(job);
                    continue;
                }
                TouchAndSave(job);
            }
            AdoptCompletedCompilationReloads();
        }

        private static void AdoptCompletedCompilationReloads()
        {
            foreach (VmAutomationWorkspaceJob job in VmAutomationWorkspaceJobStore.GetAll()
                         .Where(candidate => !candidate.IsTerminal &&
                                              candidate.Phase == WaitingForDomainReloadPhase &&
                                              candidate.CompilationFinished))
            {
                if (job.CompilationSucceeded != true)
                {
                    FailCompilation(job, BuildUnrecordedCompilationOutcomeError(job));
                    continue;
                }
                job.AssemblyReloadObserved = true;
                job.Phase = VerifyingPhase;
                job.StatusMessage = "Assembly reload observed; verifying the workspace result.";
                TouchAndSave(job);
            }
        }

        internal static Dictionary<string, object> BuildCompilationFailure(
            VmAutomationWorkspaceJob job, bool unityScriptCompilationFailed)
        {
            int compilerErrorCount = job?.CompilerErrorCount ?? 0;
            if (!unityScriptCompilationFailed && compilerErrorCount == 0)
                return null;

            string message = compilerErrorCount > 0
                ? $"Compilation produced {compilerErrorCount} error(s)."
                : "Unity reported a script compilation failure outside the per-assembly compiler message stream.";
            return VmAutomationResponse.Error(message, "compilation_failed", false,
                new Dictionary<string, object>
                {
                    { "unityScriptCompilationFailed", unityScriptCompilationFailed },
                    { "perAssemblyCompilerErrorCount", compilerErrorCount },
                    { "compilerMessages", (job?.CompilerMessages ??
                        new List<Dictionary<string, object>>()).Cast<object>().ToList() },
                });
        }

        internal static Dictionary<string, object> BuildUnrecordedCompilationOutcomeError(
            VmAutomationWorkspaceJob job)
        {
            if (job?.CompilationSucceeded == false)
                return BuildCompilationFailure(job, unityScriptCompilationFailed: true);
            return VmAutomationResponse.Error(
                "The assembly domain reloaded without a durably recorded Unity compilation outcome.",
                "compilation_outcome_unrecorded_after_reload", false,
                new Dictionary<string, object>
                {
                    { "compilationFinished", job?.CompilationFinished == true },
                    { "compilerErrorCount", job?.CompilerErrorCount ?? 0 },
                });
        }

        private static void FailCompilation(VmAutomationWorkspaceJob job,
            Dictionary<string, object> error)
        {
            if (job.JobType == VmAutomationAssetTransactionJobRunner.JobType)
                VmAutomationAssetTransactionJobRunner.FailAndRollback(job, error);
            else
                Fail(job, error);
        }

        private static bool HasCrossedMutationBoundary(VmAutomationWorkspaceJob job)
        {
            if (job.JobType == VmAutomationPlayModeJobRunner.JobType)
            {
                return VmAutomationPlayModeJobRunner
                    .HasCrossedMutationBoundary(job);
            }
            if (job.JobType == VmAutomationAssetTransactionJobRunner.JobType)
                return VmAutomationAssetTransactionJobRunner.HasCrossedMutationBoundary(job);
            return job.AssetRefreshInvocationCount > 0 || job.PackageRequestIssued ||
                   job.PackageResolveInvoked || job.CompilationRequested;
        }

        private static void Fail(VmAutomationWorkspaceJob job, object error)
        {
            job.Status = FailedStatus;
            job.Phase = FailedStatus;
            job.StatusMessage = "Workspace operation failed.";
            job.Error = VmAutomationResponse.ToDictionary(error) ?? VmAutomationResponse.Error(
                error?.ToString() ?? "Workspace operation failed.",
                "workspace_job_failed", false);
            job.CompletedAt = DateTime.UtcNow;
            TouchAndSave(job);
            if (activePackageJobId == job.JobId)
            {
                activeAddRequest = null;
                activePackageJobId = null;
            }
            if (activeCompilationJobId == job.JobId)
            {
                activeCompilationJobId = null;
                activeCompilationContext = null;
                ActiveCompilerMessages.Clear();
            }
        }

        internal static void Persist(VmAutomationWorkspaceJob job)
        {
            TouchAndSave(job);
        }

        private static void TouchAndSave(VmAutomationWorkspaceJob job)
        {
            job.UpdatedAt = DateTime.UtcNow;
            VmAutomationWorkspaceJobStore.Save(job);
            Record(job);
        }

        private static void Record(VmAutomationWorkspaceJob job)
        {
            VmAutomationJobHistory.Record(job.JobType, job.JobId, job.OwnerAgentId, job.Status,
                BuildPublicJob(job, includeAccessToken: true), job.RequestId);
        }

        private static Dictionary<string, object> BuildPublicJob(VmAutomationWorkspaceJob job,
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
                else if (RequiresStableEditMode(job) &&
                    !VmAutomationRuntimePreconditions.IsStableEditMode)
                {
                    response["blockedReason"] =
                        EditModeRequiredBlockedReason;
                }
                else if (EditorApplication.isCompiling)
                    response["blockedReason"] = "compiling";
                else if (EditorApplication.isUpdating)
                    response["blockedReason"] = "asset-or-package-update";
            }
            return response;
        }

        private static void Adopt(VmAutomationWorkspaceJob job)
        {
            if (job == null || job.ClientAdopted || job.IsTerminal)
                return;

            job.ClientAdopted = true;
            job.StatusMessage = RequiresStableEditMode(job) &&
                                !VmAutomationRuntimePreconditions.IsStableEditMode
                ? WaitingForEditModeStatusMessage
                : "Authorized client poll acknowledged; execution can begin.";
            TouchAndSave(job);
            VmAutomationWorkspaceJobAdoptionStore.Delete(job.JobId);
        }

        private static bool RequiresStableEditMode(
            VmAutomationWorkspaceJob job)
        {
            return job != null &&
                   (job.Operation == "packages/update-git" ||
                    job.Operation == "packages/resolve");
        }

        private static bool IsStopPlayModeTransition(
            VmAutomationWorkspaceJob job)
        {
            return job?.JobType ==
                   VmAutomationPlayModeJobRunner.JobType &&
                   string.Equals(GetString(job.Request, "action"),
                       "stop", StringComparison.Ordinal);
        }

        private static bool CanAccess(VmAutomationWorkspaceJob job,
            Dictionary<string, object> args)
        {
            string agentId = GetString(args, "_agentId", "anonymous");
            if (job.OwnerAgentId == agentId)
                return true;
            return string.Equals(GetString(args, "jobAccessToken"),
                job.JobAccessToken, StringComparison.Ordinal);
        }

        private static Dictionary<string, object> StripTransportArguments(
            Dictionary<string, object> args)
        {
            Dictionary<string, object> result = CloneDictionary(args);
            result.Remove("_agentId");
            result.Remove("_requestId");
            result.Remove("jobAccessToken");
            result.Remove("clearStuck");
            return result;
        }

        private static Dictionary<string, object> CloneDictionary(
            Dictionary<string, object> source)
        {
            return VmAutomationResponse.ToDictionary(MiniJson.Deserialize(MiniJson.Serialize(source))) ??
                   new Dictionary<string, object>();
        }

        private static string ComputeFingerprint(string jobType, string operation,
            Dictionary<string, object> request)
        {
            object canonical = Canonicalize(new Dictionary<string, object>
            {
                { "jobType", jobType },
                { "operation", operation },
                { "request", request },
            });
            byte[] bytes = Encoding.UTF8.GetBytes(MiniJson.Serialize(canonical));
            using (SHA256 hash = SHA256.Create())
            {
                return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "")
                    .ToLowerInvariant();
            }
        }

        private static object Canonicalize(object value)
        {
            Dictionary<string, object> dictionary = VmAutomationResponse.ToDictionary(value);
            if (dictionary != null)
            {
                var result = new Dictionary<string, object>();
                foreach (KeyValuePair<string, object> pair in dictionary
                             .OrderBy(pair => pair.Key, StringComparer.Ordinal))
                    result[pair.Key] = Canonicalize(pair.Value);
                return result;
            }
            if (value is IList list)
                return list.Cast<object>().Select(Canonicalize).ToList();
            return value;
        }

        private static string GetString(Dictionary<string, object> values, string key,
            string fallback = "")
        {
            return values != null && values.TryGetValue(key, out object value) && value != null
                ? value.ToString()
                : fallback;
        }

        private static bool GetBool(Dictionary<string, object> values, string key)
        {
            if (values == null || !values.TryGetValue(key, out object value) || value == null)
                return false;
            return value is bool result
                ? result
                : bool.TryParse(value.ToString(), out result) && result;
        }

        private static string FormatDate(DateTime? value)
        {
            return value.HasValue
                ? value.Value.ToString("O", CultureInfo.InvariantCulture)
                : "";
        }
    }
}
