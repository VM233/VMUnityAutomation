using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace VMUnityAutomation.Editor
{
    /// <summary>
    /// The single execution boundary for built-in routes and reflected project tools.
    /// It owns project binding, idempotency, preconditions, undo isolation, error
    /// normalization, and callback-to-Task adaptation. It does not own a transport.
    /// </summary>
    public static class VmAutomationExecutor
    {
        private const int DefaultTimeoutSeconds = 120;
        private const int MaximumTimeoutSeconds = 3600;
        private static long s_ActionSequence = DateTime.UtcNow.Ticks;

        public static Task<VmAutomationInvocationResult> ExecuteAsync(
            string identifier,
            IDictionary<string, object> arguments = null,
            string requestId = null,
            string agentId = null,
            int timeoutSeconds = DefaultTimeoutSeconds,
            string expectedProjectPath = null)
        {
            if (!VmAutomationEditorProcess.OwnsAutomationState)
                return Task.FromResult(VmAutomationInvocationResult.Failure(
                    identifier ?? "", "", requestId ?? "", "requires_main_editor",
                    "Automation execution belongs to the main Unity Editor process."));

            requestId = string.IsNullOrWhiteSpace(requestId)
                ? Guid.NewGuid().ToString("N")
                : requestId.Trim();
            agentId = string.IsNullOrWhiteSpace(agentId) ? "cli" : agentId.Trim();

            if (string.IsNullOrWhiteSpace(identifier))
            {
                return Task.FromResult(VmAutomationInvocationResult.Failure(
                    "",
                    "",
                    requestId,
                    "command_required",
                    "An automation command name or route is required."));
            }

            if (timeoutSeconds < 1 || timeoutSeconds > MaximumTimeoutSeconds)
            {
                return Task.FromResult(VmAutomationInvocationResult.Failure(
                    identifier,
                    "",
                    requestId,
                    "invalid_timeout",
                    $"timeoutSeconds must be between 1 and {MaximumTimeoutSeconds}."));
            }

            if (!VmAutomationCatalog.TryGetTool(
                    identifier.Trim(), true, out Dictionary<string, object> metadata))
            {
                if (VmProjectToolRegistry.TryGetUnavailableToolFailure(
                        identifier,
                        out string projectToolErrorCode,
                        out string projectToolMessage,
                        out Dictionary<string, object> projectToolDetails))
                {
                    return Task.FromResult(
                        VmAutomationInvocationResult.Failure(
                            identifier,
                            projectToolDetails.TryGetValue(
                                "executeRoute", out object routeValue)
                                ? routeValue?.ToString() ?? ""
                                : "",
                            requestId,
                            projectToolErrorCode,
                            projectToolMessage,
                            false,
                            projectToolDetails));
                }

                return Task.FromResult(VmAutomationInvocationResult.Failure(
                    identifier,
                    "",
                    requestId,
                    "command_not_found",
                    $"Automation command '{identifier}' was not found."));
            }

            string route = metadata["route"].ToString();
            string command = metadata["toolName"].ToString();
            var invocationArguments = arguments != null
                ? new Dictionary<string, object>(arguments)
                : new Dictionary<string, object>();
            if (!TryValidateProjectBinding(
                    command, route, requestId, invocationArguments,
                    expectedProjectPath, out string canonicalProjectPath,
                    out VmAutomationInvocationResult bindingError))
                return Task.FromResult(bindingError);
            if (canonicalProjectPath != null && !invocationArguments.ContainsKey("expectedProjectPath"))
                invocationArguments["expectedProjectPath"] = canonicalProjectPath;

            if (VmAutomationCatalog.RouteIsDangerous(route) &&
                (!invocationArguments.TryGetValue("confirm", out object confirmation) ||
                 confirmation is false))
            {
                return Task.FromResult(VmAutomationInvocationResult.Failure(
                    command, route, requestId, "confirmation_required",
                    $"Automation command '{command}' requires confirm=true."));
            }
            if (!VmAutomationInputValidator.TryValidate(invocationArguments,
                    (Dictionary<string, object>)metadata["inputSchema"],
                    out string inputErrorCode, out string inputMessage,
                    out Dictionary<string, object> inputDetails))
            {
                return Task.FromResult(VmAutomationInvocationResult.Failure(
                    command, route, requestId, inputErrorCode, inputMessage,
                    false, inputDetails));
            }
            if (canonicalProjectPath != null)
                invocationArguments["expectedProjectPath"] = canonicalProjectPath;

            invocationArguments["_agentId"] = agentId;
            if (DeclaresInputArgument(metadata, "idempotencyKey"))
            {
                invocationArguments["_requestId"] = requestId;
                if (!invocationArguments.ContainsKey("idempotencyKey"))
                    invocationArguments["idempotencyKey"] = requestId;
            }

            string fingerprint = VmAutomationCanonicalJson.ComputeSha256(
                new Dictionary<string, object>
                {
                    { "route", route },
                    { "arguments", invocationArguments },
                });

            return VmAutomationRequestRegistry.Execute(
                requestId,
                fingerprint,
                () => ExecuteCoreAsync(
                    command,
                    route,
                    invocationArguments,
                    requestId,
                    agentId,
                    timeoutSeconds,
                    (Dictionary<string, object>)metadata["outputSchema"]),
                () => VmAutomationInvocationResult.Failure(
                    command,
                    route,
                    requestId,
                    "request_id_conflict",
                    "The same requestId was already used with different command arguments."));
        }

        private static async Task<VmAutomationInvocationResult> ExecuteCoreAsync(
            string command,
            string route,
            Dictionary<string, object> arguments,
            string requestId,
            string agentId,
            int timeoutSeconds,
            Dictionary<string, object> outputSchema)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            // Project binding belongs to this execution boundary. Business
            // owners publish closed argument contracts and must never receive
            // executor-only routing metadata after the binding is validated.
            arguments.Remove("expectedProjectPath");

            if (VmAutomationCatalog.RouteRequiresPlayMode(route) &&
                !VmAutomationRuntimePreconditions.IsStablePlayMode)
            {
                return VmAutomationInvocationResult.Failure(
                    command,
                    route,
                    requestId,
                    VmAutomationRuntimePreconditions.PlayModeRequiredErrorCode,
                    $"Automation command '{command}' requires stable Play Mode.",
                    false,
                    VmAutomationRuntimePreconditions.CreatePlayModeStateDetails(),
                    stopwatch.ElapsedMilliseconds);
            }

            bool isDangerous =
                VmAutomationCatalog.RouteIsDangerous(route);
            if (isDangerous)
            {
                // Confirmation is execution-boundary metadata. Closed owner
                // argument contracts must not receive it after the boundary
                // has consumed the caller's explicit consent.
                arguments.Remove("confirm");
            }

            if (!VmAutomationCatalog.IsRouteReadOnly(route) &&
                !route.StartsWith("jobs/", StringComparison.Ordinal) &&
                VmAutomationWorkspaceJobRunner.HasActiveJob &&
                !(route == VmAutomationPlayModeJobRunner.Operation &&
                  VmAutomationPlayModeJobRunner.IsStopRequest(arguments)))
            {
                return VmAutomationInvocationResult.Failure(
                    command,
                    route,
                    requestId,
                    "workspace_job_active",
                    "A reload-resumable workspace mutation is already active.",
                    true,
                    null,
                    stopwatch.ElapsedMilliseconds);
            }

            VmAutomationToolConfigurationPolicy.ApplyDefaults(route, arguments);
            long actionId = Interlocked.Increment(ref s_ActionSequence);
            VmAutomationRequestUndoCoordinator.Ownership undoOwnership = null;
            bool deferred = false;
            object rawResult;

            try
            {
                if (route.StartsWith(
                        VmProjectToolRegistry.DirectRoutePrefix,
                        StringComparison.Ordinal))
                {
                    undoOwnership = BeginUndo(actionId, route, false);
                    if (!VmProjectToolRegistry.TryExecuteDirectRoute(
                            route, arguments, out rawResult))
                    {
                        rawResult = VmAutomationResponse.Error(
                            $"Project tool route '{route}' is unavailable.",
                            "project_tool_not_found");
                    }
                }
                else if (!VmAutomationBuiltInRouteDescriptorRegistry.TryGet(
                             route, out VmAutomationBuiltInRouteDescriptor descriptor))
                {
                    rawResult = VmAutomationResponse.Error(
                        $"Automation route '{route}' is unavailable.",
                        "command_not_found");
                }
                else if (!descriptor.IsDeferred)
                {
                    undoOwnership = BeginUndo(actionId, route, false);
                    rawResult = descriptor.Immediate(arguments);
                }
                else
                {
                    deferred = true;
                    rawResult = await ExecuteDeferredAsync(
                        descriptor,
                        arguments,
                        timeoutSeconds);
                }
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogException(exception);
                rawResult = VmAutomationResponse.Error(
                    exception.GetBaseException().Message,
                    "command_exception");
            }

            bool succeeded = !VmAutomationResponse.TryGetError(
                rawResult,
                out string errorMessage,
                out string errorCode,
                out bool retryable);
            VmAutomationRequestUndoCoordinator.Complete(undoOwnership, succeeded);

            object transportedResult = succeeded
                ? VmAutomationResponse.CompactForTransport(rawResult, outputSchema)
                : VmAutomationResponse.NormalizeError(rawResult, errorCode, retryable);
            stopwatch.Stop();
            RecordAction(
                actionId,
                agentId,
                route,
                succeeded,
                errorMessage,
                stopwatch.ElapsedMilliseconds,
                transportedResult,
                undoOwnership,
                deferred);

            if (succeeded)
            {
                return VmAutomationInvocationResult.Success(
                    command,
                    route,
                    requestId,
                    transportedResult,
                    stopwatch.ElapsedMilliseconds);
            }

            Dictionary<string, object> details = VmAutomationResponse.ToDictionary(transportedResult);
            return VmAutomationInvocationResult.Failure(
                command,
                route,
                requestId,
                errorCode,
                errorMessage,
                retryable,
                details,
                stopwatch.ElapsedMilliseconds);
        }

        private static VmAutomationRequestUndoCoordinator.Ownership BeginUndo(
            long actionId,
            string route,
            bool deferred)
        {
            return VmAutomationRequestUndoCoordinator.Begin(
                actionId,
                route,
                !VmAutomationCatalog.IsRouteReadOnly(route) &&
                !deferred &&
                !VmAutomationRequestUndoCoordinator.IsControlRoute(route) &&
                !VmAutomationCatalog.RouteIsLongRunning(route));
        }

        private static async Task<object> ExecuteDeferredAsync(
            VmAutomationBuiltInRouteDescriptor descriptor,
            Dictionary<string, object> arguments,
            int timeoutSeconds)
        {
            var completion = new TaskCompletionSource<object>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            object latestProgress = null;
            descriptor.Deferred(
                arguments,
                result => completion.TrySetResult(result),
                progress => latestProgress = progress);

            Task timeout = Task.Delay(TimeSpan.FromSeconds(timeoutSeconds));
            Task finished = await Task.WhenAny(completion.Task, timeout);
            if (ReferenceEquals(finished, completion.Task))
                return await completion.Task;

            return VmAutomationResponse.Error(
                $"Automation route '{descriptor.Route}' did not complete within " +
                $"{timeoutSeconds} seconds. The underlying Editor operation may still finish; " +
                "do not retry a mutation without checking its published state.",
                "automation_timeout",
                false,
                new Dictionary<string, object>
                {
                    { "mayStillComplete", true },
                    { "latestProgress", latestProgress },
                });
        }

        private static bool TryValidateProjectBinding(
            string command,
            string route,
            string requestId,
            Dictionary<string, object> arguments,
            string expectedProjectPath,
            out string canonicalProjectPath,
            out VmAutomationInvocationResult error)
        {
            canonicalProjectPath = null;
            string argumentPath = GetString(arguments, "expectedProjectPath");
            string expected = string.IsNullOrWhiteSpace(expectedProjectPath)
                ? argumentPath
                : expectedProjectPath;
            bool bindingRequired = VmAutomationCatalog.RouteRequiresTargetBinding(route);
            if (bindingRequired && string.IsNullOrWhiteSpace(expected))
            {
                error = VmAutomationInvocationResult.Failure(
                    command,
                    route,
                    requestId,
                    "project_binding_required",
                    "Mutating automation commands require expectedProjectPath.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(expected))
            {
                error = null;
                return true;
            }

            string actual = GetProjectPath();
            string normalizedExpected;
            StringComparison comparison = Application.platform ==
                                          RuntimePlatform.WindowsEditor
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            try
            {
                normalizedExpected = NormalizeProjectPath(expected);
                if (!string.IsNullOrWhiteSpace(expectedProjectPath) &&
                    !string.IsNullOrWhiteSpace(argumentPath) &&
                    !string.Equals(normalizedExpected,
                        NormalizeProjectPath(argumentPath), comparison))
                {
                    error = VmAutomationInvocationResult.Failure(
                        command, route, requestId, "argument_conflict",
                        "expected_project_path conflicts with arguments_json.expectedProjectPath.");
                    return false;
                }
            }
            catch (Exception exception)
            {
                error = VmAutomationInvocationResult.Failure(
                    command,
                    route,
                    requestId,
                    "invalid_project_path",
                    exception.GetBaseException().Message);
                return false;
            }

            if (string.Equals(actual, normalizedExpected, comparison))
            {
                canonicalProjectPath = actual;
                error = null;
                return true;
            }

            error = VmAutomationInvocationResult.Failure(
                command,
                route,
                requestId,
                "project_mismatch",
                "The requested project path does not match the connected Editor.",
                false,
                new Dictionary<string, object>
                {
                    { "expectedProjectPath", normalizedExpected },
                    { "actualProjectPath", actual },
                });
            return false;
        }

        private static void RecordAction(
            long actionId,
            string agentId,
            string route,
            bool succeeded,
            string errorMessage,
            long elapsedMilliseconds,
            object result,
            VmAutomationRequestUndoCoordinator.Ownership undoOwnership,
            bool deferred)
        {
            try
            {
                var record = new VmAutomationActionRecord
                {
                    Timestamp = DateTime.UtcNow,
                    AgentId = agentId,
                    ActionName = route,
                    Category = VmAutomationActionRecord.ExtractCategory(route),
                    Status = succeeded ? "Completed" : "Failed",
                    ExecutionTimeMs = elapsedMilliseconds,
                    ErrorMessage = errorMessage,
                    RequestId = actionId,
                    UndoUnavailableReason = deferred
                        ? "deferred_operation"
                        : null,
                };
                if (succeeded)
                    record.ExtractTargetFromResult(result);
                VmAutomationActionHistory.RecordAction(record);
                VmAutomationRequestUndoCoordinator.RegisterAction(record, undoOwnership);
            }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogWarning(
                    $"[VM Unity Automation] Failed to record action history: " +
                    exception.GetBaseException().Message);
            }
        }

        private static string GetProjectPath()
        {
            string dataPath = Application.dataPath.Replace('\\', '/');
            string root = dataPath.EndsWith(
                "/Assets", StringComparison.OrdinalIgnoreCase)
                ? dataPath.Substring(0, dataPath.Length - "/Assets".Length)
                : dataPath;
            return NormalizeProjectPath(root);
        }

        private static string NormalizeProjectPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("A project path is required.");
            string root = Path.GetPathRoot(path);
            if (!Path.IsPathRooted(path) || root.EndsWith(":", StringComparison.Ordinal) ||
                Application.platform == RuntimePlatform.WindowsEditor &&
                (root == "\\" || root == "/"))
                throw new ArgumentException("An absolute project path is required.");
            return Path.GetFullPath(path).Replace('\\', '/').TrimEnd('/');
        }

        private static string GetString(
            IReadOnlyDictionary<string, object> arguments,
            string key)
        {
            return arguments != null &&
                   arguments.TryGetValue(key, out object value) &&
                   value != null
                ? value.ToString()
                : "";
        }

        private static bool DeclaresInputArgument(
            IReadOnlyDictionary<string, object> metadata,
            string argumentName)
        {
            return metadata != null &&
                   metadata.TryGetValue("inputSchema", out object schemaValue) &&
                   schemaValue is IReadOnlyDictionary<string, object> schema &&
                   schema.TryGetValue("properties", out object propertiesValue) &&
                   propertiesValue is IReadOnlyDictionary<string, object> properties &&
                   properties.ContainsKey(argumentName);
        }
    }
}
