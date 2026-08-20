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
    internal static class MCPWorkspaceJobRunner
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
        internal const double PackageAdoptionTimeoutSeconds = 300.0;

        private static bool ticking;
        private static AddRequest activeAddRequest;
        private static string activePackageJobId;
        private static string activeCompilationJobId;
        private static object activeCompilationContext;
        private static readonly List<Dictionary<string, object>> ActiveCompilerMessages = new();

        static MCPWorkspaceJobRunner()
        {
            MCPWorkspaceJobStore.EnsureLoaded();
            MCPAssetTransactionSnapshotStore.CleanupOrphanPreparingDirectories(
                MCPWorkspaceJobStore.GetAll().Select(job => job.JobId));
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
            return Start(MCPAssetTransactionJobRunner.JobType,
                MCPAssetTransactionJobRunner.Operation, args, null);
        }

        internal static object StartGitPackageUpdate(Dictionary<string, object> args)
        {
            if (!MCPPackageManagerCommands.TryBuildGitPackageIdentifier(
                    args, out string name, out string identifier, out string revision,
                    out string error))
            {
                return MCPResponse.Error(error, "invalid_arguments");
            }

            var packageTarget = new MCPGitPackageExpectation(name, identifier, revision);
            return Start(PackageUpdateJobType, "packages/update-git", args,
                new List<MCPGitPackageExpectation> { packageTarget });
        }

        internal static object StartPackageResolve(Dictionary<string, object> args)
        {
            if (!MCPPackageManagerCommands.TryBuildExpectedGitPackages(
                    args, out List<MCPGitPackageExpectation> expectations, out string error))
            {
                return MCPResponse.Error(error, "invalid_arguments");
            }

            var manifestStates = expectations.Select(
                MCPPackageManagerCommands.BuildGitPackageResolutionState).ToList();
            if (manifestStates.Any(state => !GetBool(state, "manifestMatches")))
            {
                return MCPResponse.Error(
                    "packages/resolve requires Packages/manifest.json to already declare every exact Git URL and full commit SHA.",
                    "package_manifest_target_mismatch", false,
                    new Dictionary<string, object>
                    {
                        { "packageState", manifestStates.Cast<object>().ToList() },
                    });
            }

            return Start(PackageResolveJobType, "packages/resolve", args, expectations);
        }

        internal static bool OwnsJobType(string jobType)
        {
            return jobType == AssetRefreshJobType || jobType == PackageUpdateJobType ||
                   jobType == PackageResolveJobType ||
                   jobType == MCPAssetTransactionJobRunner.JobType;
        }

        internal static bool ContainsJob(string jobType, string jobId)
        {
            if (string.IsNullOrWhiteSpace(jobType) ||
                string.IsNullOrWhiteSpace(jobId))
                return false;
            MCPWorkspaceJob job = MCPWorkspaceJobStore.Find(jobId);
            return job != null &&
                   string.Equals(job.JobType, jobType, StringComparison.Ordinal);
        }

        internal static bool HasActiveJob =>
            MCPWorkspaceJobStore.GetAll().Any(job => !job.IsTerminal);

        internal static object Get(Dictionary<string, object> args)
        {
            args ??= new Dictionary<string, object>();
            string jobId = GetString(args, "jobId");
            string requestId = GetString(args, "requestId");
            string jobType = GetString(args, "jobType");
            if (string.IsNullOrWhiteSpace(jobId) &&
                string.IsNullOrWhiteSpace(requestId))
            {
                return MCPResponse.Error(
                    "jobs/get requires jobId or requestId.", "invalid_arguments");
            }
            MCPWorkspaceJob job = !string.IsNullOrWhiteSpace(jobId)
                ? MCPWorkspaceJobStore.Find(jobId)
                : MCPWorkspaceJobStore.FindByRequestId(requestId, jobType);
            if (job == null)
            {
                string identity = !string.IsNullOrWhiteSpace(jobId) ? jobId : requestId;
                return MCPResponse.Error(
                    $"Workspace job '{identity}' was not found.", "job_not_found");
            }
            if (!CanAccess(job, args))
            {
                return MCPResponse.Error(
                    "Job belongs to another agent and the jobAccessToken was not supplied.",
                    "job_owner_mismatch");
            }
            return BuildPublicJob(job, includeAccessToken: false);
        }

        internal static object Cancel(Dictionary<string, object> args)
        {
            args ??= new Dictionary<string, object>();
            string jobId = GetString(args, "jobId");
            if (string.IsNullOrWhiteSpace(jobId))
                return MCPResponse.Error("jobId is required.", "invalid_arguments");
            MCPWorkspaceJob job = MCPWorkspaceJobStore.Find(jobId);
            if (job == null)
                return MCPResponse.Error($"Job '{jobId}' was not found.", "job_not_found");
            if (!CanAccess(job, args))
            {
                return MCPResponse.Error(
                    "Job belongs to another agent and the jobAccessToken was not supplied.",
                    "job_owner_mismatch");
            }
            if (job.IsTerminal)
            {
                return MCPResponse.Error(
                    $"Job '{jobId}' is already terminal with status '{job.Status}'.",
                    "job_not_cancellable", false, BuildPublicJob(job, false));
            }
            if (HasCrossedMutationBoundary(job))
            {
                return MCPResponse.Error(
                    $"Job '{jobId}' has crossed its mutation boundary at phase '{job.Phase}'.",
                    "job_not_cancellable", false, BuildPublicJob(job, false));
            }

            MCPAssetTransactionJobRunner.CancelBeforeMutation(job);
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
                return MCPResponse.Error("jobId is required.", "invalid_arguments");
            MCPWorkspaceJob job = MCPWorkspaceJobStore.Find(jobId);
            if (job == null)
                return MCPResponse.Error($"Job '{jobId}' was not found.", "job_not_found");
            if (!CanAccess(job, args))
            {
                return MCPResponse.Error(
                    "Job belongs to another agent and the jobAccessToken was not supplied.",
                    "job_owner_mismatch");
            }
            return MCPAssetTransactionJobRunner.CleanupRecoveryArtifacts(job);
        }

        private static object Start(string jobType, string operation,
            Dictionary<string, object> args, List<MCPGitPackageExpectation> expectedPackages)
        {
            args ??= new Dictionary<string, object>();
            string owner = GetString(args, "_agentId", "anonymous");
            string requestId = GetString(args, "_requestId");
            string idempotencyKey = GetString(args, "idempotencyKey");
            Dictionary<string, object> request = StripTransportArguments(args);
            string fingerprint = ComputeFingerprint(jobType, operation, request);

            if (!string.IsNullOrWhiteSpace(requestId))
            {
                MCPWorkspaceJob existingByRequest =
                    MCPWorkspaceJobStore.FindByRequestId(requestId, jobType);
                if (existingByRequest != null)
                    return RequireMatchingExisting(existingByRequest, fingerprint, owner);
            }
            if (!string.IsNullOrWhiteSpace(idempotencyKey))
            {
                MCPWorkspaceJob existingByKey =
                    MCPWorkspaceJobStore.FindByIdempotencyKey(operation, idempotencyKey);
                if (existingByKey != null)
                    return RequireMatchingExisting(existingByKey, fingerprint, owner);
            }

            DateTime now = DateTime.UtcNow;
            var job = new MCPWorkspaceJob
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
                StatusMessage = "Accepted and durably queued.",
                Request = request,
                CreatedAt = now,
                UpdatedAt = now,
                ExpectedPackages = expectedPackages ?? new List<MCPGitPackageExpectation>(),
            };
            if (expectedPackages != null && expectedPackages.Count == 1)
            {
                job.PackageName = expectedPackages[0].Name;
                job.RequestedPackageIdentifier = expectedPackages[0].Identifier;
                job.RequestedPackageRevision = expectedPackages[0].Revision;
            }

            MCPWorkspaceJobStore.Add(job);
            Record(job);
            return BuildPublicJob(job, includeAccessToken: true);
        }

        private static object RequireMatchingExisting(MCPWorkspaceJob existing,
            string fingerprint, string ownerAgentId)
        {
            if (!string.Equals(existing.OwnerAgentId, ownerAgentId,
                    StringComparison.Ordinal))
            {
                return MCPResponse.Error(
                    "The persistent request identity belongs to another agent.",
                    "job_owner_mismatch");
            }
            if (!string.Equals(existing.RequestFingerprint, fingerprint,
                    StringComparison.Ordinal))
            {
                return MCPResponse.Error(
                    "The persistent request identity was already used with different arguments.",
                    "idempotency_conflict", false,
                    new Dictionary<string, object>
                    {
                        { "jobId", existing.JobId },
                        { "operation", existing.Operation },
                    });
            }

            Dictionary<string, object> response = BuildPublicJob(existing, includeAccessToken: true);
            MCPContractMetadata.AddTag(response, MCPContractMetadata.Tag.Reused);
            return response;
        }

        private static void Tick()
        {
            if (ticking)
                return;

            MCPWorkspaceJob job = MCPWorkspaceJobStore.GetAll()
                .Where(candidate => !candidate.IsTerminal)
                .OrderBy(candidate => candidate.CreatedAt)
                .FirstOrDefault();
            if (job == null)
                return;

            EditorApplication.QueuePlayerLoopUpdate();
            if (job.Status == QueuedStatus)
            {
                job.Status = RunningStatus;
                job.StartedAt = DateTime.UtcNow;
                job.StatusMessage = "Running the accepted workspace operation.";
                TouchAndSave(job);
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
                if (MCPAssetTransactionJobRunner.ExecutePhase(job))
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
                if (job.JobType == MCPAssetTransactionJobRunner.JobType)
                    MCPAssetTransactionJobRunner.FailAndRollback(job, exception);
                else
                    Fail(job, MCPResponse.Error(exception.GetBaseException().Message,
                        "workspace_job_execution_failed", false));
            }
            finally
            {
                ticking = false;
            }
        }

        private static void ExecuteAssetRefresh(MCPWorkspaceJob job)
        {
            job.Phase = RefreshingAssetsPhase;
            job.StatusMessage = "Invoking AssetDatabase refresh on the Unity main thread.";
            job.AssetRefreshInvocationCount++;
            TouchAndSave(job);

            object result = MCPAssetCommands.ExecuteRefreshImmediate(job.Request);
            if (MCPResponse.TryGetError(result, out string message, out string errorCode, out _))
            {
                Fail(job, MCPResponse.Error(message,
                    string.IsNullOrEmpty(errorCode) ? "asset_refresh_failed" : errorCode,
                    false));
                return;
            }

            job.AssetRefreshResult = MCPResponse.ToDictionary(result) ??
                                     new Dictionary<string, object>();
            job.AssetRefreshReturned = true;
            job.AssetRefreshReturnedAt = DateTime.UtcNow;
            job.Phase = RequestingCompilationPhase;
            job.StatusMessage = "Asset refresh returned; a clean script compilation is required.";
            TouchAndSave(job);
        }

        private static void RequestCompilation(MCPWorkspaceJob job)
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

        private static void IssuePackageUpdate(MCPWorkspaceJob job)
        {
            if (job.PackageRequestIssued)
                throw new InvalidOperationException(
                    $"Workspace job '{job.JobId}' attempted to issue a package update twice.");

            job.PackageRequestIssued = true;
            job.PackageRequestIssuedAt = DateTime.UtcNow;
            job.Phase = UpdatingPackagePhase;
            job.StatusMessage = $"Updating package '{job.PackageName}' to " +
                                $"'{job.RequestedPackageRevision}'.";
            TouchAndSave(job);

            activeAddRequest = Client.Add(job.RequestedPackageIdentifier);
            activePackageJobId = job.JobId;
        }

        private static void ObservePackageUpdate(MCPWorkspaceJob job)
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
                    string message = activeAddRequest.Error?.message ??
                                     "Unity Package Manager failed to update the package.";
                    activeAddRequest = null;
                    activePackageJobId = null;
                    Fail(job, MCPResponse.Error(message, "package_update_failed", false));
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

        private static void IssuePackageResolve(MCPWorkspaceJob job)
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

        private static void ObservePackageResolve(MCPWorkspaceJob job)
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

        private static bool TryAdoptPackageTarget(MCPWorkspaceJob job)
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

        internal static bool ShouldFailPackageAdoption(MCPWorkspaceJob job,
            bool targetsMatch, DateTime nowUtc)
        {
            if (job == null || targetsMatch || !job.PackageRequestIssuedAt.HasValue)
                return false;
            return nowUtc - job.PackageRequestIssuedAt.Value >=
                   TimeSpan.FromSeconds(PackageAdoptionTimeoutSeconds);
        }

        private static void FailPackageAdoptionTimeout(MCPWorkspaceJob job)
        {
            Fail(job, MCPResponse.Error(
                "Unity Package Manager did not register the exact manifest and lockfile target within the package adoption timeout.",
                "package_adoption_timeout", false,
                new Dictionary<string, object> { { "packageState", job.PackageState } }));
        }

        private static bool PackageTargetsMatch(MCPWorkspaceJob job,
            out Dictionary<string, object> state)
        {
            var packageStates = new List<object>();
            bool allMatch = job.ExpectedPackages.Count > 0;
            foreach (MCPGitPackageExpectation expectation in job.ExpectedPackages)
            {
                Dictionary<string, object> packageState =
                    MCPPackageManagerCommands.BuildGitPackageResolutionState(expectation);
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

        private static void VerifyAndComplete(MCPWorkspaceJob job)
        {
            if (job.JobType == MCPAssetTransactionJobRunner.JobType)
            {
                MCPAssetTransactionJobRunner.ExecutePhase(job);
                return;
            }
            if (!HasCompleteRefreshCompilationEvidence(job, requireCompilation: false))
            {
                Fail(job, MCPResponse.Error(
                    "Asset refresh execution evidence is incomplete.",
                    "asset_refresh_evidence_incomplete", false));
                return;
            }
            if (!HasCompleteRefreshCompilationEvidence(job, requireCompilation: true))
            {
                Fail(job, MCPResponse.Error(
                    "Compilation or assembly reload execution evidence is incomplete.",
                    "compilation_evidence_incomplete", false));
                return;
            }
            if (job.CompilerErrorCount > 0)
            {
                Fail(job, MCPResponse.Error(
                    $"Compilation produced {job.CompilerErrorCount} error(s).",
                    "compilation_failed", false));
                return;
            }
            if (job.ExpectedPackages.Count > 0 &&
                !PackageTargetsMatch(job, out Dictionary<string, object> packageState))
            {
                job.PackageState = packageState;
                Fail(job, MCPResponse.Error(
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
            MCPWorkspaceJob job, bool requireCompilation = true)
        {
            if (job == null || job.AssetRefreshInvocationCount != 1 ||
                (!job.AssetRefreshReturned && !job.AssetRefreshDomainReloadObserved))
                return false;
            return !requireCompilation ||
                   (job.CompilationRequested && job.CompilationStarted &&
                    job.CompilationFinished && job.CompilationSucceeded == true &&
                    job.AssemblyReloadObserved);
        }

        internal static bool RecordReloadBeforeCompilation(MCPWorkspaceJob job)
        {
            if (job == null || job.Phase != RequestingCompilationPhase)
                return false;

            job.AssetRefreshDomainReloadObserved = true;
            job.StatusMessage =
                "The published assets are loaded; requesting the explicit clean compilation.";
            return true;
        }

        internal static void AdvanceAfterSuccessfulCompilation(MCPWorkspaceJob job)
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
            MCPWorkspaceJob job = FindActiveCompilationJob();
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
                if (!MCPCompilerDiagnosticUtility.IsDiagnostic(message))
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
            MCPWorkspaceJob job = FindActiveCompilationJob();
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
            MCPWorkspaceJob job = MCPWorkspaceJobStore.GetAll()
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

        private static MCPWorkspaceJob FindActiveCompilationJob()
        {
            if (!string.IsNullOrEmpty(activeCompilationJobId))
                return MCPWorkspaceJobStore.Find(activeCompilationJobId);
            return MCPWorkspaceJobStore.GetAll().FirstOrDefault(job =>
                !job.IsTerminal &&
                (job.Phase == AwaitingCompilationStartPhase || job.Phase == CompilingPhase));
        }

        private static void RecoverAfterReload()
        {
            foreach (MCPWorkspaceJob job in MCPWorkspaceJobStore.GetAll()
                         .Where(candidate => !candidate.IsTerminal &&
                                             candidate.Status == RunningStatus))
            {
                job.RecoveredAfterReload = true;
                job.DomainReloadCount++;
                bool recordedReloadBeforeCompilation = RecordReloadBeforeCompilation(job);
                if (recordedReloadBeforeCompilation)
                    TouchAndSave(job);
                if (job.JobType == MCPAssetTransactionJobRunner.JobType)
                {
                    MCPAssetTransactionJobRunner.RecoverAfterReload(job);
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
                    Fail(job, MCPResponse.Error(
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
            foreach (MCPWorkspaceJob job in MCPWorkspaceJobStore.GetAll()
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
            MCPWorkspaceJob job, bool unityScriptCompilationFailed)
        {
            int compilerErrorCount = job?.CompilerErrorCount ?? 0;
            if (!unityScriptCompilationFailed && compilerErrorCount == 0)
                return null;

            string message = compilerErrorCount > 0
                ? $"Compilation produced {compilerErrorCount} error(s)."
                : "Unity reported a script compilation failure outside the per-assembly compiler message stream.";
            return MCPResponse.Error(message, "compilation_failed", false,
                new Dictionary<string, object>
                {
                    { "unityScriptCompilationFailed", unityScriptCompilationFailed },
                    { "perAssemblyCompilerErrorCount", compilerErrorCount },
                    { "compilerMessages", (job?.CompilerMessages ??
                        new List<Dictionary<string, object>>()).Cast<object>().ToList() },
                });
        }

        internal static Dictionary<string, object> BuildUnrecordedCompilationOutcomeError(
            MCPWorkspaceJob job)
        {
            if (job?.CompilationSucceeded == false)
                return BuildCompilationFailure(job, unityScriptCompilationFailed: true);
            return MCPResponse.Error(
                "The assembly domain reloaded without a durably recorded Unity compilation outcome.",
                "compilation_outcome_unrecorded_after_reload", false,
                new Dictionary<string, object>
                {
                    { "compilationFinished", job?.CompilationFinished == true },
                    { "compilerErrorCount", job?.CompilerErrorCount ?? 0 },
                });
        }

        private static void FailCompilation(MCPWorkspaceJob job,
            Dictionary<string, object> error)
        {
            if (job.JobType == MCPAssetTransactionJobRunner.JobType)
                MCPAssetTransactionJobRunner.FailAndRollback(job, error);
            else
                Fail(job, error);
        }

        private static bool HasCrossedMutationBoundary(MCPWorkspaceJob job)
        {
            if (job.JobType == MCPAssetTransactionJobRunner.JobType)
                return MCPAssetTransactionJobRunner.HasCrossedMutationBoundary(job);
            return job.AssetRefreshInvocationCount > 0 || job.PackageRequestIssued ||
                   job.PackageResolveInvoked || job.CompilationRequested;
        }

        private static void Fail(MCPWorkspaceJob job, object error)
        {
            job.Status = FailedStatus;
            job.Phase = FailedStatus;
            job.StatusMessage = "Workspace operation failed.";
            job.Error = MCPResponse.ToDictionary(error) ?? MCPResponse.Error(
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

        internal static void Persist(MCPWorkspaceJob job)
        {
            TouchAndSave(job);
        }

        private static void TouchAndSave(MCPWorkspaceJob job)
        {
            job.UpdatedAt = DateTime.UtcNow;
            MCPWorkspaceJobStore.Save(job);
            Record(job);
        }

        private static void Record(MCPWorkspaceJob job)
        {
            MCPJobHistory.Record(job.JobType, job.JobId, job.OwnerAgentId, job.Status,
                BuildPublicJob(job, includeAccessToken: true), job.RequestId);
        }

        private static Dictionary<string, object> BuildPublicJob(MCPWorkspaceJob job,
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
            if (job.JobType == MCPAssetTransactionJobRunner.JobType)
            {
                response["transactionId"] = job.JobId;
                if (job.Phase == MCPAssetTransactionJobRunner.CommittedPhase ||
                    job.Phase == MCPAssetTransactionJobRunner.RolledBackPhase ||
                    job.Phase == MCPAssetTransactionJobRunner.RollbackFailedPhase ||
                    job.Phase == MCPAssetTransactionJobRunner.OutcomeUncertainPhase)
                    response["terminalState"] = job.Phase;
                if (MCPAssetTransactionJobRunner.HasRecoveryArtifacts(job))
                {
                    response["cleanupStatus"] = "available";
                    if (includeAccessToken)
                        response["cleanupToken"] = job.JobAccessToken;
                }
            }
            if (!job.IsTerminal)
            {
                if (EditorApplication.isCompiling)
                    response["blockedReason"] = "compiling";
                else if (EditorApplication.isUpdating)
                    response["blockedReason"] = "asset-or-package-update";
            }
            return response;
        }

        private static bool CanAccess(MCPWorkspaceJob job,
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
            return MCPResponse.ToDictionary(MiniJson.Deserialize(MiniJson.Serialize(source))) ??
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
            Dictionary<string, object> dictionary = MCPResponse.ToDictionary(value);
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
