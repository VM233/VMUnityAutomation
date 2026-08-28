using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Compilation;
using UnityEditor.PackageManager;
using UnityEngine;

namespace VMUnityAutomation.Editor
{
    [InitializeOnLoad]
    public static class VmAutomationPackageTestCommands
    {
        internal const string JobType = "package-test";
        private const string DefaultPackageName = "com.vm233.unity-automation";
        private const string DefaultTestAssembly = "VMUnityAutomation.Editor.Tests";
        internal const string DefaultPackageSmokeCategory = "VMUnityAutomation.PackageSmoke";
        internal const string FullPackageRegressionCategory = "VMUnityAutomation.FullRegression";
        private const double WorkflowTimeoutMinutes = 10;
        private const double ManifestResolveActivityObservationTimeoutSeconds = 30;
        private const string WaitingForAssemblyState = "waiting-for-assembly";
        private const string WaitingForEditorAdoptionState = "waiting-for-editor-adoption";

        private static PackageTestWorkflow _workflow;
        private static bool _updateRegistered;

        static VmAutomationPackageTestCommands()
        {
            _workflow = LoadWorkflow();
            CompilationPipeline.compilationStarted -= OnCompilationStarted;
            CompilationPipeline.compilationStarted += OnCompilationStarted;
            AssemblyReloadEvents.beforeAssemblyReload -= OnBeforeAssemblyReload;
            AssemblyReloadEvents.beforeAssemblyReload += OnBeforeAssemblyReload;
            if (_workflow != null && !_workflow.IsTerminal)
                EnsureUpdateRegistered();
        }

        public static object RunPackageTests(Dictionary<string, object> args)
        {
            if (_workflow != null && !_workflow.IsTerminal)
            {
                if (!string.Equals(_workflow.OwnerAgentId ?? "anonymous",
                        GetString(args, "_agentId", "anonymous"), StringComparison.Ordinal))
                    return VmAutomationResponse.Error("Package test workflow belongs to another agent.",
                        "job_owner_mismatch");
                EnsureUpdateRegistered();
                ContinueWorkflow();
                return new Dictionary<string, object>
                {
                    { "error", "A package test workflow is already running" },
                    { "workflow", BuildResponse(_workflow) }
                };
            }

            string packageName = GetString(args, "packageName", DefaultPackageName);
            string[] assemblies = ParseStringArray(args, "assemblies");
            string[] testNames = ParseStringArray(args, "testNames");
            string[] categories = ParseStringArray(args, "categories");
            string[] groupNames = ParseStringArray(args, "groupNames");
            categories = ResolvePackageTestCategories(
                packageName, testNames, categories, groupNames);
            if ((assemblies == null || assemblies.Length == 0) && packageName == DefaultPackageName)
                assemblies = new[] { DefaultTestAssembly };
            if (assemblies == null || assemblies.Length == 0)
                return new { error = "assemblies is required for package tests outside the VM Unity Automation package" };
            if (!TryValidatePackageAssemblyNames(packageName, assemblies,
                    out string assemblyError, out string[] packageTestAssemblies))
            {
                return VmAutomationResponse.Error(assemblyError, "test_assembly_not_declared", false,
                    new Dictionary<string, object>
                    {
                        { "packageName", packageName },
                        { "requestedAssemblies", assemblies },
                        { "declaredAssemblies", packageTestAssemblies },
                    });
            }

            string manifestPath = GetManifestPath();
            if (!File.Exists(manifestPath))
                return new { error = $"Package manifest not found at '{manifestPath}'" };

            byte[] manifestBytes = File.ReadAllBytes(manifestPath);
            string manifestText = File.ReadAllText(manifestPath);
            if (!TryParseManifest(manifestText, out var manifest, out string manifestError))
                return new { error = manifestError };

            bool alreadyTestable = IsPackageTestable(manifest, packageName);
            _workflow = new PackageTestWorkflow
            {
                WorkflowId = Guid.NewGuid().ToString("N").Substring(0, 12),
                PackageName = packageName,
                Mode = GetString(args, "mode", "EditMode"),
                Assemblies = assemblies,
                PackageTestAssemblies = packageTestAssemblies,
                TestNames = testNames,
                Categories = categories,
                GroupNames = groupNames,
                ManifestPath = manifestPath,
                OriginalManifestBase64 = Convert.ToBase64String(manifestBytes),
                OriginalManifestHadUtf8Bom = HasUtf8Bom(manifestBytes),
                State = alreadyTestable ? WaitingForAssemblyState : "enabling",
                StartedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                OwnerAgentId = GetString(args, "_agentId", "anonymous"),
            };
            SaveWorkflow();
            EnsureUpdateRegistered();
            Debug.Log($"[Automation Package Tests] Started workflow {_workflow.WorkflowId} for {_workflow.PackageName}");

            return BuildResponse(_workflow);
        }

        public static object GetPackageTestJob(Dictionary<string, object> args)
        {
            if (_workflow == null)
                _workflow = LoadWorkflow();
            if (_workflow == null)
                return new { error = "No package test workflow found" };
            string jobId = GetString(args, "jobId");
            if (!string.IsNullOrEmpty(jobId) && jobId != _workflow.WorkflowId)
                return new { error = $"Package test job '{jobId}' not found" };
            if (!VmAutomationJobHistory.CanAccess(JobType, _workflow.WorkflowId,
                    _workflow.OwnerAgentId, args))
                return VmAutomationResponse.Error(
                    "Package test workflow belongs to another agent and the jobAccessToken was not supplied.",
                    "job_owner_mismatch");

            if (!_workflow.IsTerminal)
            {
                EnsureUpdateRegistered();
                ContinueWorkflow();
            }

            bool clear = GetBool(args, "clear", false);
            var response = BuildResponse(_workflow);
            if (clear && _workflow.IsTerminal)
            {
                DeleteWorkflowFile();
                _workflow = null;
                response["cleared"] = true;
            }
            return response;
        }

        internal static object CancelPackageTest(Dictionary<string, object> args)
        {
            if (_workflow == null)
                _workflow = LoadWorkflow();
            if (_workflow == null)
                return VmAutomationResponse.Error("No package test workflow was found.", "job_not_found");

            string workflowId = GetString(args, "jobId");
            if (!string.IsNullOrEmpty(workflowId) && workflowId != _workflow.WorkflowId)
                return VmAutomationResponse.Error($"Package test workflow '{workflowId}' was not found.",
                    "job_not_found");
            if (!VmAutomationJobHistory.CanAccess(JobType, _workflow.WorkflowId,
                    _workflow.OwnerAgentId, args))
                return VmAutomationResponse.Error(
                    "Package test workflow belongs to another agent and the jobAccessToken was not supplied.",
                    "job_owner_mismatch");
            if (_workflow.IsTerminal)
                return VmAutomationResponse.Error("Package test workflow is already terminal.",
                    "job_already_terminal", false, BuildResponse(_workflow));

            _workflow.CancelRequested = true;
            _workflow.Error = "Canceled by request.";

            object underlyingResult = null;
            if (!string.IsNullOrEmpty(_workflow.TestJobId))
            {
                underlyingResult = VmAutomationTestRunnerCommands.CancelTestJob(new Dictionary<string, object>
                {
                    { "jobId", _workflow.TestJobId },
                    { "_agentId", _workflow.OwnerAgentId ?? "anonymous" },
                });
                Dictionary<string, object> underlying =
                    VmAutomationResponse.ToDictionary(underlyingResult);
                if (underlying == null ||
                    !underlying.TryGetValue("success", out object successValue) ||
                    !(successValue is bool success) || !success)
                {
                    _workflow.CancelRequested = false;
                    _workflow.Error = "";
                    TouchAndSaveWorkflow();
                    return VmAutomationResponse.Error(
                        "The underlying Unity Test Runner did not accept cancellation.",
                        "job_cancel_rejected", true,
                        new Dictionary<string, object>
                        {
                            { "jobId", _workflow.WorkflowId },
                            { "underlyingJob", underlyingResult },
                        });
                }
            }
            else if (_workflow.NeedsManifestRestore)
            {
                BeginRestore();
            }
            else
            {
                _workflow.State = "canceled";
                TouchAndSaveWorkflow();
                UnregisterUpdate();
            }

            return BuildAcceptedCancellationResponse(_workflow, underlyingResult);
        }

        internal static bool TryGetActiveWorkflow(out string workflowId, out string packageName,
            out string state)
        {
            if (_workflow == null)
                _workflow = LoadWorkflow();

            if (_workflow == null || _workflow.IsTerminal)
            {
                workflowId = "";
                packageName = "";
                state = "";
                return false;
            }

            workflowId = _workflow.WorkflowId ?? "";
            packageName = _workflow.PackageName ?? "";
            state = _workflow.State ?? "";
            return true;
        }

        private static void EnablePackageTests()
        {
            if (_workflow == null || _workflow.State != "enabling")
                return;

            try
            {
                byte[] originalManifest = Convert.FromBase64String(
                    _workflow.OriginalManifestBase64);
                byte[] modifiedManifest = BuildModifiedManifestBytes(_workflow,
                    originalManifest);
                byte[] currentManifest = VmAutomationPersistenceFile.ReadAllBytes(
                    _workflow.ManifestPath);

                if (_workflow.ManifestPublication == ManifestPublicationState.Original)
                {
                    if (!currentManifest.SequenceEqual(originalManifest))
                        throw new InvalidOperationException(
                            "Packages/manifest.json changed after the package-test workflow captured its original bytes.");
                    _workflow.BeginManifestModification();
                    TouchAndSaveWorkflow();
                }
                else
                {
                    _workflow.RequireManifestPublication(ManifestPublicationState.Modified,
                        "resume manifest testables publication");
                }

                currentManifest = VmAutomationPersistenceFile.ReadAllBytes(_workflow.ManifestPath);
                if (currentManifest.SequenceEqual(originalManifest))
                {
                    VmAutomationPersistenceFile.WriteAllBytes(_workflow.ManifestPath, modifiedManifest);
                }
                else if (!currentManifest.SequenceEqual(modifiedManifest))
                {
                    throw new InvalidOperationException(
                        "Packages/manifest.json changed outside the package-test manifest transaction before testables publication completed.");
                }

                if (!VmAutomationPersistenceFile.ReadAllBytes(_workflow.ManifestPath)
                        .SequenceEqual(modifiedManifest))
                    throw new IOException(
                        "Package-test manifest publication did not adopt the exact modified bytes.");
                _workflow.State = WaitingForAssemblyState;
                _workflow.BeginManifestResolve(ManifestResolveTarget.Modified);
                TouchAndSaveWorkflow();
                ResolvePollResult resolve = PollManifestResolve(
                    ManifestResolveTarget.Modified,
                    IsManifestResolveProductAdopted(
                        ManifestResolveTarget.Modified,
                        VmAutomationPackageTestAssemblyProduct.AreAssembliesCompiled(
                            _workflow.Assemblies),
                        resolveIssued: false,
                        assemblyReloadObserved: false,
                        editorStable: false),
                    out string resolveError);
                if (resolve == ResolvePollResult.Failed)
                    throw new InvalidOperationException(resolveError);
            }
            catch (Exception ex)
            {
                FailWorkflow($"Failed to enable package tests: {ex.Message}");
            }
        }

        private static void ContinueWorkflow()
        {
            if (_workflow == null || _workflow.IsTerminal)
            {
                UnregisterUpdate();
                return;
            }

            if (HasWorkflowResourceDeadlineExpired(
                    _workflow.State, _workflow.StartedAt, DateTime.UtcNow))
            {
                string timeout =
                    $"Package test workflow exceeded {WorkflowTimeoutMinutes:0} minutes";
                FailWorkflow(timeout);
                return;
            }

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                ObserveManifestResolveActivity();
                return;
            }

            try
            {
                switch (_workflow.State)
                {
                    case "enabling":
                        EnablePackageTests();
                        break;
                    case WaitingForAssemblyState:
                    case WaitingForEditorAdoptionState:
                        AdvanceAssemblyAdoption();
                        break;
                    case "running":
                        UpdateRunningTestJob();
                        break;
                    case "restoring":
                        CompleteRestoreWhenReady();
                        break;
                    default:
                        FailWorkflow($"Unknown package test workflow state '{_workflow.State}'");
                        break;
                }
            }
            catch (Exception ex)
            {
                FailWorkflow(ex.GetBaseException().Message);
            }
        }

        private static void StartTestRun()
        {
            var runArgs = new Dictionary<string, object>
            {
                { "mode", _workflow.Mode },
                { "assemblies", _workflow.Assemblies.Cast<object>().ToList() },
                { "_agentId", _workflow.OwnerAgentId ?? "anonymous" }
            };
            AddArray(runArgs, "testNames", _workflow.TestNames);
            AddArray(runArgs, "categories", _workflow.Categories);
            AddArray(runArgs, "groupNames", _workflow.GroupNames);

            var runResult = VmAutomationResponse.ToDictionary(VmAutomationTestRunnerCommands.RunTests(runArgs));
            if (runResult == null || !GetBool(runResult, "success", false))
            {
                string error = runResult != null ? GetString(runResult, "error", "Failed to start tests") :
                    "Failed to start tests";
                FailWorkflow(error);
                return;
            }

            _workflow.TestJobId = GetString(runResult, "jobId");
            _workflow.State = "running";
            TouchAndSaveWorkflow();
            Debug.Log($"[Automation Package Tests] Workflow {_workflow.WorkflowId} started test job " +
                      _workflow.TestJobId);
        }

        private static void AdvanceAssemblyAdoption()
        {
            if (!TryValidatePackageAssemblyNames(
                    _workflow.PackageName,
                    _workflow.Assemblies,
                    out string assemblyError,
                    out string[] packageTestAssemblies))
            {
                FailWorkflow(assemblyError);
                return;
            }

            if (_workflow.PackageTestAssemblies == null ||
                _workflow.PackageTestAssemblies.Length == 0)
            {
                _workflow.PackageTestAssemblies = packageTestAssemblies;
                TouchAndSaveWorkflow();
            }

            bool assembliesAvailable =
                VmAutomationPackageTestAssemblyProduct.AreAssembliesCompiled(
                    _workflow.Assemblies);
            if (_workflow.ManifestResolve == ManifestResolveTarget.Modified)
            {
                ResolvePollResult resolve = PollManifestResolve(
                    ManifestResolveTarget.Modified,
                    IsManifestResolveProductAdopted(
                        ManifestResolveTarget.Modified, assembliesAvailable,
                        resolveIssued: _workflow.ManifestResolveIssued,
                        assemblyReloadObserved:
                            _workflow.ManifestResolveAssemblyReloadObserved,
                        editorStable: !EditorApplication.isCompiling &&
                                      !EditorApplication.isUpdating),
                    out string resolveError);
                if (resolve == ResolvePollResult.Pending)
                    return;
                if (resolve == ResolvePollResult.Failed)
                {
                    FailWorkflow(resolveError);
                    return;
                }
            }
            else if (_workflow.ManifestResolve != ManifestResolveTarget.None)
            {
                FailWorkflow(
                    $"Package-test assembly adoption encountered unexpected manifest resolve target '{_workflow.ManifestResolve}'.");
                return;
            }

            if (!assembliesAvailable)
            {
                if (TryGetCompilationFailure(out string compilationError))
                {
                    FailWorkflow(compilationError);
                    return;
                }

                if (_workflow.State != WaitingForAssemblyState)
                {
                    _workflow.State = WaitingForAssemblyState;
                    TouchAndSaveWorkflow();
                }
                return;
            }

            if (!CanStartTestRunFromAssemblyState(_workflow.State, assembliesAvailable))
            {
                _workflow.State = WaitingForEditorAdoptionState;
                TouchAndSaveWorkflow();
                return;
            }

            StartTestRun();
        }

        internal static bool CanStartTestRunFromAssemblyState(string state,
            bool assembliesAvailable)
        {
            return assembliesAvailable && state == WaitingForEditorAdoptionState;
        }

        internal static bool HasWorkflowResourceDeadlineExpired(
            string state, DateTime startedAt, DateTime now)
        {
            // Once Test Runner owns the child job, its durable terminal state and
            // explicit cancellation own that lifetime. Manifest restoration has its
            // own publication deadline. The package workflow deadline only bounds
            // acquisition of the runnable package-test product.
            return state != "running" &&
                   state != "restoring" &&
                   (now - startedAt).TotalMinutes > WorkflowTimeoutMinutes;
        }

        private static ResolvePollResult PollManifestResolve(
            ManifestResolveTarget expectedTarget, bool targetProductAdopted,
            out string error)
        {
            error = "";
            _workflow.RequireManifestResolve(expectedTarget,
                "poll Package Manager adoption");

            if (targetProductAdopted)
            {
                _workflow.CompleteManifestResolve(expectedTarget);
                TouchAndSaveWorkflow();
                return ResolvePollResult.Succeeded;
            }

            if (_workflow.HasManifestResolveTimedOut(
                    DateTime.UtcNow, TimeSpan.FromMinutes(WorkflowTimeoutMinutes)))
            {
                error =
                    $"Package Manager did not adopt the {expectedTarget.ToString().ToLowerInvariant()} " +
                    $"manifest product within the {WorkflowTimeoutMinutes:0}-minute package-test " +
                    "resource deadline after one resolve request.";
                return ResolvePollResult.Failed;
            }

            if (_workflow.HasManifestResolveActivityTimedOut(
                    DateTime.UtcNow,
                    TimeSpan.FromSeconds(
                        ManifestResolveActivityObservationTimeoutSeconds)))
            {
                error =
                    $"Package Manager resolve for the {expectedTarget.ToString().ToLowerInvariant()} " +
                    "manifest did not enter an observable package update, compilation, or assembly " +
                    $"reload within {ManifestResolveActivityObservationTimeoutSeconds:0} seconds; " +
                    "the resolve may have been canceled before Editor adoption.";
                return ResolvePollResult.Failed;
            }

            if (!_workflow.ManifestResolveIssued)
            {
                _workflow.MarkManifestResolveIssued();
                TouchAndSaveWorkflow();
                try
                {
                    Client.Resolve();
                    CompilationPipeline.RequestScriptCompilation(
                        RequestScriptCompilationOptions.CleanBuildCache);
                    return ResolvePollResult.Pending;
                }
                catch (Exception ex)
                {
                    error =
                        $"Package Manager resolve or required clean compilation could not start: {ex.Message}";
                    return ResolvePollResult.Failed;
                }
            }

            return ResolvePollResult.Pending;
        }

        private static void OnCompilationStarted(object _)
        {
            ObserveManifestResolveActivity();
        }

        private static void OnBeforeAssemblyReload()
        {
            if (_workflow == null || _workflow.IsTerminal ||
                _workflow.ManifestResolve == ManifestResolveTarget.None ||
                !_workflow.ManifestResolveIssued ||
                _workflow.ManifestResolveAssemblyReloadObserved)
                return;
            _workflow.MarkManifestResolveAssemblyReloadObserved();
            TouchAndSaveWorkflow();
        }

        private static void ObserveManifestResolveActivity()
        {
            if (_workflow == null || _workflow.IsTerminal ||
                _workflow.ManifestResolve == ManifestResolveTarget.None ||
                _workflow.ManifestResolveActivityObserved)
                return;
            _workflow.MarkManifestResolveActivityObserved();
            TouchAndSaveWorkflow();
        }

        internal static bool IsManifestResolveProductAdopted(
            ManifestResolveTarget target, bool packageTestAssembliesAvailable,
            bool resolveIssued, bool assemblyReloadObserved, bool editorStable)
        {
            return target switch
            {
                ManifestResolveTarget.Modified => packageTestAssembliesAvailable,
                // Unity 6.4 can keep a package test assembly discoverable for the
                // remainder of the current Editor session after testables is removed.
                // Exact original manifest bytes are verified by the restoration owner;
                // a resolve-owned clean compile and assembly reload is the durable
                // adoption witness when the old assembly product remains loaded.
                ManifestResolveTarget.Original => !packageTestAssembliesAvailable ||
                    (resolveIssued && assemblyReloadObserved && editorStable),
                _ => throw new ArgumentOutOfRangeException(nameof(target), target,
                    "A manifest adoption product requires a concrete resolve target."),
            };
        }

        private static void UpdateRunningTestJob()
        {
            var jobResult = VmAutomationResponse.ToDictionary(VmAutomationTestRunnerCommands.GetTestJob(
                new Dictionary<string, object>
                {
                    { "jobId", _workflow.TestJobId },
                    { "includeDetails", false },
                    { "includeFailedOnly", false },
                    { "includeStackTrace", false },
                    { "failureLimit", 50 },
                    { "_agentId", _workflow.OwnerAgentId ?? "anonymous" },
                }));
            if (jobResult == null)
                return;

            string status = GetString(jobResult, "status");
            if (status == "running" || status == "canceling")
                return;

            _workflow.TestResult = CompactStoredTestResult(jobResult);
            _workflow.TestSucceeded = status == "succeeded";
            if (!_workflow.TestSucceeded)
                _workflow.Error = GetString(jobResult, "error", "Package tests failed");
            if (_workflow.NeedsManifestRestore)
                BeginRestore();
            else
                CompleteWorkflow();
        }

        private static void BeginRestore()
        {
            _workflow.BeginManifestRestore();
            _workflow.State = "restoring";
            TouchAndSaveWorkflow();
            Debug.Log($"[Automation Package Tests] Workflow {_workflow.WorkflowId} restoring package manifest");
        }

        private static void CompleteRestoreWhenReady()
        {
            byte[] originalBytes = Convert.FromBase64String(_workflow.OriginalManifestBase64);
            byte[] modifiedBytes = BuildModifiedManifestBytes(_workflow, originalBytes);

            if (_workflow.ManifestResolve == ManifestResolveTarget.Modified)
            {
                // Cancellation and failure restoration owns the manifest bytes. The modified
                // resolve invocation is abandoned, never reissued, before original publication.
                _workflow.AbandonModifiedManifestResolveForRestore();
                TouchAndSaveWorkflow();
            }

            byte[] currentBytes = VmAutomationPersistenceFile.ReadAllBytes(_workflow.ManifestPath);

            if (currentBytes.SequenceEqual(modifiedBytes))
            {
                VmAutomationPersistenceFile.WriteAllBytes(_workflow.ManifestPath, originalBytes);
                if (!VmAutomationPersistenceFile.ReadAllBytes(_workflow.ManifestPath)
                        .SequenceEqual(originalBytes))
                    throw new IOException(
                        "Package-test manifest restoration did not adopt the exact original bytes.");
                TouchAndSaveWorkflow();
            }
            else if (!currentBytes.SequenceEqual(originalBytes))
            {
                FailManifestRestore(
                    "Packages/manifest.json changed outside the package-test manifest transaction; external bytes were left untouched.");
                return;
            }

            string[] packageTestAssemblies = _workflow.PackageTestAssemblies;
            if (packageTestAssemblies == null || packageTestAssemblies.Length == 0)
            {
                if (!TryValidatePackageAssemblyNames(
                        _workflow.PackageName,
                        _workflow.Assemblies,
                        out string assemblyError,
                        out packageTestAssemblies))
                {
                    FailManifestRestore(assemblyError);
                    return;
                }
                _workflow.PackageTestAssemblies = packageTestAssemblies;
                TouchAndSaveWorkflow();
            }

            bool packageTestAssembliesAvailable =
                VmAutomationPackageTestAssemblyProduct.AreAssembliesCompiled(
                    packageTestAssemblies);
            bool originalProductAdopted = IsManifestResolveProductAdopted(
                ManifestResolveTarget.Original, packageTestAssembliesAvailable,
                resolveIssued: _workflow.ManifestResolveIssued,
                assemblyReloadObserved:
                    _workflow.ManifestResolveAssemblyReloadObserved,
                editorStable: !EditorApplication.isCompiling &&
                              !EditorApplication.isUpdating);
            if (originalProductAdopted)
            {
                if (_workflow.ManifestResolve == ManifestResolveTarget.Original)
                    _workflow.CompleteManifestResolve(ManifestResolveTarget.Original);
                else if (_workflow.ManifestResolve != ManifestResolveTarget.None)
                {
                    FailManifestRestore(
                        $"Original manifest adoption encountered unexpected resolve target '{_workflow.ManifestResolve}'.");
                    return;
                }

                _workflow.MarkManifestRestored();
                CompleteWorkflow();
                return;
            }

            if (_workflow.ManifestResolve == ManifestResolveTarget.None)
            {
                _workflow.BeginManifestResolve(ManifestResolveTarget.Original);
                TouchAndSaveWorkflow();
            }
            else
            {
                _workflow.RequireManifestResolve(ManifestResolveTarget.Original,
                    "resume original manifest adoption");
            }

            ResolvePollResult originalResolve = PollManifestResolve(
                ManifestResolveTarget.Original, targetProductAdopted: false,
                out string resolveError);
            if (originalResolve == ResolvePollResult.Pending)
                return;
            if (originalResolve == ResolvePollResult.Failed)
            {
                FailManifestRestore(resolveError);
                return;
            }
        }

        private static void CompleteWorkflow()
        {
            _workflow.State = _workflow.CancelRequested
                ? "canceled"
                : string.IsNullOrEmpty(_workflow.Error) && _workflow.TestSucceeded
                    ? "succeeded"
                    : "failed";
            TouchAndSaveWorkflow();
            UnregisterUpdate();
            Debug.Log($"[Automation Package Tests] Workflow {_workflow.WorkflowId} finished with state {_workflow.State}");
        }

        private static void FailWorkflow(string error)
        {
            if (_workflow == null)
                return;

            _workflow.Error = error;
            _workflow.TestSucceeded = false;
            switch (_workflow.ManifestPublication)
            {
                case ManifestPublicationState.Modified:
                    BeginRestore();
                    return;
                case ManifestPublicationState.Restoring:
                    FailManifestRestore(error);
                    return;
                default:
                    _workflow.State = "failed";
                    TouchAndSaveWorkflow();
                    UnregisterUpdate();
                    return;
            }
        }

        private static void FailManifestRestore(string error)
        {
            _workflow.MarkManifestRestoreFailed();
            _workflow.Error = $"Failed to restore package manifest: {error}";
            _workflow.State = "failed";
            TouchAndSaveWorkflow();
            UnregisterUpdate();
        }

        private static Dictionary<string, object> BuildResponse(PackageTestWorkflow workflow)
        {
            var response = new Dictionary<string, object>
            {
                // Reading a workflow snapshot succeeded even when the workflow outcome did not.
                // Callers inspect status and error for the package-test result itself.
                { "success", true },
                { "jobId", workflow.WorkflowId },
                { "jobType", JobType },
                { "status", workflow.State },
                { "pollRoute", "jobs/get" },
                { "pollArgs", new Dictionary<string, object>
                    {
                        { "jobId", workflow.WorkflowId },
                        { "jobType", JobType },
                    }
                },
                { "packageName", workflow.PackageName },
                { "mode", workflow.Mode },
                { "assemblies", workflow.Assemblies ?? Array.Empty<string>() },
                { "startedAt", workflow.StartedAt.ToString("O") },
                { "updatedAt", workflow.UpdatedAt.ToString("O") },
                { "compilationDiagnostics", VmAutomationConsoleCommands.GetCompilationDiagnosticsSummary() },
            };
            var tags = new List<string>();
            if (workflow.CancelRequested)
                tags.Add(VmAutomationContractMetadata.Tag.CancellationRequested);
            switch (workflow.ManifestPublication)
            {
                case ManifestPublicationState.Modified:
                case ManifestPublicationState.Restoring:
                    tags.Add(VmAutomationContractMetadata.Tag.ManifestModified);
                    break;
                case ManifestPublicationState.Restored:
                    tags.Add(VmAutomationContractMetadata.Tag.ManifestRestored);
                    break;
                case ManifestPublicationState.RestoreFailed:
                    tags.Add(VmAutomationContractMetadata.Tag.ManifestRestoreFailed);
                    break;
            }
            VmAutomationContractMetadata.SetTags(response, tags);
            if (!string.IsNullOrEmpty(workflow.TestJobId))
                response["testJobId"] = workflow.TestJobId;
            if (!string.IsNullOrEmpty(workflow.Error))
                response["error"] = workflow.Error;
            if (workflow.TestResult != null)
                response["testResult"] = workflow.TestResult;
            VmAutomationJobHistory.PublishAccessToken(response, JobType, workflow.WorkflowId,
                workflow.OwnerAgentId);
            return response;
        }

        private static Dictionary<string, object> BuildAcceptedCancellationResponse(
            PackageTestWorkflow workflow, object underlyingResult)
        {
            var response = BuildResponse(workflow);

            // This route reports whether the cancellation request was accepted. A canceled
            // workflow remains a non-successful terminal result when read through jobs/get,
            // but accepting the cancellation itself is a successful jobs/cancel operation.
            response["success"] = true;
            response.Remove("error");
            response["cancelRequested"] = true;
            response["cancelMode"] = string.IsNullOrEmpty(workflow.TestJobId)
                ? "workflow"
                : "unity-test-runner";
            if (underlyingResult != null)
                response["underlyingJob"] = underlyingResult;
            return response;
        }

        private static Dictionary<string, object> CompactStoredTestResult(
            Dictionary<string, object> jobResult)
        {
            if (jobResult == null)
                return null;

            var compact = new Dictionary<string, object>();
            foreach (string key in new[]
                     {
                         "jobId", "status", "mode", "startedAt", "completedAt",
                         "totalDuration", "progress", "summary", "error", "errorCode",
                         "completionRecoveredFromLeafResults",
                     })
            {
                if (jobResult.TryGetValue(key, out object value))
                    compact[key] = value;
            }
            return compact;
        }

        private static byte[] BuildModifiedManifestBytes(PackageTestWorkflow workflow,
            byte[] originalBytes)
        {
            int offset = workflow.OriginalManifestHadUtf8Bom ? 3 : 0;
            if (offset > originalBytes.Length)
                throw new InvalidDataException("The captured package manifest has an invalid UTF-8 BOM.");
            string originalText = new UTF8Encoding(false, true).GetString(
                originalBytes, offset, originalBytes.Length - offset);
            if (!TryParseManifest(originalText, out var manifest, out string error))
                throw new InvalidDataException(error);
            if (IsPackageTestable(manifest, workflow.PackageName))
                throw new InvalidOperationException(
                    $"Package '{workflow.PackageName}' was already testable in the captured manifest.");

            List<object> testables;
            if (!manifest.TryGetValue("testables", out object rawTestables))
            {
                testables = new List<object>();
                manifest["testables"] = testables;
            }
            else if (rawTestables is List<object> existingTestables)
            {
                testables = existingTestables;
            }
            else
            {
                throw new InvalidDataException(
                    "Packages/manifest.json field 'testables' must be a JSON array; " +
                    "the package-test manifest transaction left it unchanged.");
            }
            testables.Add(workflow.PackageName);

            string modifiedText = SerializePrettyJson(manifest, 0) + "\n";
            Encoding encoding = new UTF8Encoding(workflow.OriginalManifestHadUtf8Bom);
            byte[] preamble = encoding.GetPreamble();
            byte[] body = encoding.GetBytes(modifiedText);
            var result = new byte[preamble.Length + body.Length];
            Buffer.BlockCopy(preamble, 0, result, 0, preamble.Length);
            Buffer.BlockCopy(body, 0, result, preamble.Length, body.Length);
            return result;
        }

        private static bool TryParseManifest(string text, out Dictionary<string, object> manifest,
            out string error)
        {
            try
            {
                manifest = MiniJson.Deserialize(text) as Dictionary<string, object>;
                error = manifest == null ? "Packages/manifest.json is not a JSON object" : null;
                return manifest != null;
            }
            catch (Exception ex)
            {
                manifest = null;
                error = $"Packages/manifest.json could not be parsed: {ex.Message}";
                return false;
            }
        }

        private static bool IsPackageTestable(Dictionary<string, object> manifest, string packageName)
        {
            return manifest.TryGetValue("testables", out var rawTestables) &&
                   rawTestables is List<object> testables &&
                   testables.Any(value => value?.ToString() == packageName);
        }

        private static bool TryValidatePackageAssemblyNames(string packageName,
            IEnumerable<string> requestedAssemblyNames, out string error,
            out string[] declaredAssemblyNames)
        {
            declaredAssemblyNames = Array.Empty<string>();
            UnityEditor.PackageManager.PackageInfo package;
            try
            {
                package = UnityEditor.PackageManager.PackageInfo
                    .GetAllRegisteredPackages()
                    .FirstOrDefault(candidate =>
                        string.Equals(candidate.name, packageName,
                            StringComparison.Ordinal));
            }
            catch (Exception ex)
            {
                error =
                    $"Could not inspect package '{packageName}' test assemblies: {ex.Message}";
                return false;
            }

            if (package == null || string.IsNullOrWhiteSpace(package.resolvedPath) ||
                Directory.Exists(package.resolvedPath) == false)
            {
                error =
                    $"Package '{packageName}' is not resolved, so its test assemblies cannot be validated.";
                return false;
            }

            var declared = new HashSet<string>(StringComparer.Ordinal);
            try
            {
                foreach (string asmdefPath in Directory.EnumerateFiles(
                             package.resolvedPath, "*.asmdef",
                             SearchOption.AllDirectories))
                {
                    if (MiniJson.Deserialize(File.ReadAllText(asmdefPath)) is
                            Dictionary<string, object> asmdef &&
                        asmdef.TryGetValue("name", out object rawName) &&
                        string.IsNullOrWhiteSpace(rawName?.ToString()) == false &&
                        IsPackageTestAssemblyDefinition(asmdefPath, asmdef))
                    {
                        declared.Add(rawName.ToString());
                    }
                }
            }
            catch (Exception ex)
            {
                error =
                    $"Could not read package '{packageName}' assembly definitions: {ex.Message}";
                return false;
            }

            declaredAssemblyNames = declared.OrderBy(value => value,
                StringComparer.Ordinal).ToArray();
            return TryValidateRequestedAssemblyNames(
                requestedAssemblyNames, declaredAssemblyNames, out error);
        }

        private static bool IsPackageTestAssemblyDefinition(string asmdefPath,
            Dictionary<string, object> asmdef)
        {
            if (HasAssemblyDefinitionValue(
                    asmdef, "optionalUnityReferences", "TestAssemblies") ||
                HasAssemblyDefinitionValue(
                    asmdef, "defineConstraints", "UNITY_INCLUDE_TESTS"))
            {
                return true;
            }

            string[] pathSegments = (asmdefPath ?? "").Split(
                Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return pathSegments.Any(segment => string.Equals(
                segment, "Tests", StringComparison.OrdinalIgnoreCase));
        }

        private static bool HasAssemblyDefinitionValue(
            Dictionary<string, object> asmdef, string key, string expected)
        {
            return asmdef != null && asmdef.TryGetValue(key, out object rawValues) &&
                   rawValues is List<object> values && values.Any(value =>
                       string.Equals(value?.ToString(), expected,
                           StringComparison.Ordinal));
        }

        private static bool TryValidateRequestedAssemblyNames(
            IEnumerable<string> requestedAssemblyNames,
            IEnumerable<string> declaredAssemblyNames, out string error)
        {
            var requested = new HashSet<string>(
                requestedAssemblyNames ?? Array.Empty<string>(),
                StringComparer.Ordinal);
            var declared = new HashSet<string>(
                declaredAssemblyNames ?? Array.Empty<string>(),
                StringComparer.Ordinal);
            string[] missing = requested.Except(declared)
                .OrderBy(value => value, StringComparer.Ordinal).ToArray();
            if (missing.Length == 0)
            {
                error = null;
                return true;
            }

            string available = declared.Count == 0
                ? "(none)"
                : string.Join(", ", declared.OrderBy(value => value,
                    StringComparer.Ordinal));
            error =
                $"Requested package test assembly is not declared: {string.Join(", ", missing)}. " +
                $"Declared test assemblies: {available}. Read the package test asmdef name instead of guessing from its namespace.";
            return false;
        }

        private static bool TryGetCompilationFailure(out string error)
        {
            var result = VmAutomationResponse.ToDictionary(VmAutomationConsoleCommands.GetCompilationErrors(
                new Dictionary<string, object>
                {
                    { "severity", "error" },
                    { "count", 20 },
                }));
            return TryBuildAuthoritativeCompilationFailure(
                result, EditorUtility.scriptCompilationFailed, out error);
        }

        private static bool TryBuildAuthoritativeCompilationFailure(
            Dictionary<string, object> result, bool pipelineFailed, out string error)
        {
            if (TryBuildCompilationFailure(result, out error))
                return true;
            if (!pipelineFailed)
                return false;

            error =
                "Unity reported a pipeline-level package-test compilation failure without a per-assembly compiler message.";
            return true;
        }

        private static bool TryBuildCompilationFailure(Dictionary<string, object> result, out string error)
        {
            error = null;
            if (result == null || !result.TryGetValue("entries", out object rawEntries) ||
                rawEntries is not IEnumerable entries)
            {
                return false;
            }

            var messages = new List<string>();
            foreach (object rawEntry in entries)
            {
                var entry = VmAutomationResponse.ToDictionary(rawEntry);
                if (entry == null || !string.Equals(GetString(entry, "severity"), "error",
                        StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string assembly = GetString(entry, "assembly", "unknown assembly");
                string file = GetString(entry, "file");
                string message = GetString(entry, "message", "Unknown compiler error");
                string location = string.IsNullOrEmpty(file) ? assembly : $"{assembly}: {file}";
                messages.Add($"{location}: {message}");
            }

            if (messages.Count == 0)
            {
                return false;
            }

            error = "Package test assemblies failed to compile: " + string.Join(" | ", messages);
            return true;
        }

        private static string SerializePrettyJson(object value, int depth)
        {
            string indent = new string(' ', depth * 2);
            string childIndent = new string(' ', (depth + 1) * 2);
            if (value is Dictionary<string, object> dictionary)
            {
                if (dictionary.Count == 0) return "{}";
                var entries = dictionary.Select(pair => childIndent + MiniJson.Serialize(pair.Key) + ": " +
                                                        SerializePrettyJson(pair.Value, depth + 1));
                return "{\n" + string.Join(",\n", entries) + "\n" + indent + "}";
            }

            if (value is IList list)
            {
                if (list.Count == 0) return "[]";
                var entries = list.Cast<object>().Select(item => childIndent + SerializePrettyJson(item, depth + 1));
                return "[\n" + string.Join(",\n", entries) + "\n" + indent + "]";
            }

            return MiniJson.Serialize(value);
        }

        private static void EnsureUpdateRegistered()
        {
            if (_updateRegistered)
                return;
            EditorApplication.update += ContinueWorkflow;
            _updateRegistered = true;
        }

        private static void UnregisterUpdate()
        {
            if (!_updateRegistered)
                return;
            EditorApplication.update -= ContinueWorkflow;
            _updateRegistered = false;
        }

        private static void TouchAndSaveWorkflow()
        {
            _workflow.UpdatedAt = DateTime.UtcNow;
            SaveWorkflow();
        }

        private static void SaveWorkflow()
        {
            if (_workflow == null)
                return;
            string path = GetWorkflowPath();
            VmAutomationPersistenceFile.WriteAllText(path, MiniJson.Serialize(_workflow.ToDictionary()));
            VmAutomationJobHistory.Record(JobType, _workflow.WorkflowId, _workflow.OwnerAgentId,
                _workflow.State, BuildResponse(_workflow));
        }

        private static PackageTestWorkflow LoadWorkflow()
        {
            try
            {
                string path = GetWorkflowPath();
                if (!VmAutomationPersistenceFile.TryReadAllText(path, out string contents))
                    return null;
                var values = MiniJson.Deserialize(contents) as Dictionary<string, object>;
                return values != null ? PackageTestWorkflow.FromDictionary(values) : null;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Automation Package Tests] Failed to restore workflow: {ex.Message}");
                return null;
            }
        }

        private static void DeleteWorkflowFile()
        {
            VmAutomationPersistenceFile.DeleteIfExists(GetWorkflowPath());
        }

        private static string GetManifestPath()
        {
            return Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Packages", "manifest.json");
        }

        private static string GetWorkflowPath()
        {
            return Path.Combine(Directory.GetParent(Application.dataPath).FullName, "Library", "VMUnityAutomation",
                "package-test-workflow.json");
        }

        private static bool HasUtf8Bom(byte[] bytes)
        {
            return bytes != null && bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
        }

        private static string[] ParseStringArray(Dictionary<string, object> args, string key)
        {
            if (args == null || !args.TryGetValue(key, out var value) || value == null)
                return null;
            if (value is string text)
                return text.Split(',').Select(item => item.Trim()).Where(item => item.Length > 0).ToArray();
            if (value is List<object> list)
                return list.Select(item => item?.ToString()).Where(item => !string.IsNullOrEmpty(item)).ToArray();
            return null;
        }

        internal static string[] ResolvePackageTestCategories(string packageName,
            string[] testNames, string[] categories, string[] groupNames)
        {
            bool hasExplicitSelection = (testNames?.Length ?? 0) > 0 ||
                                        (categories?.Length ?? 0) > 0 ||
                                        (groupNames?.Length ?? 0) > 0;
            if (!hasExplicitSelection &&
                string.Equals(packageName, DefaultPackageName, StringComparison.Ordinal))
            {
                return new[] { DefaultPackageSmokeCategory };
            }

            return categories;
        }

        private static void AddArray(Dictionary<string, object> args, string key, string[] values)
        {
            if (values != null && values.Length > 0)
                args[key] = values.Cast<object>().ToList();
        }

        private static string GetString(Dictionary<string, object> args, string key, string defaultValue = "")
        {
            return args != null && args.TryGetValue(key, out var value) && value != null
                ? value.ToString()
                : defaultValue;
        }

        private static bool GetBool(Dictionary<string, object> args, string key, bool defaultValue)
        {
            return args != null && args.TryGetValue(key, out var value) && value != null
                ? Convert.ToBoolean(value)
                : defaultValue;
        }

    }
}
