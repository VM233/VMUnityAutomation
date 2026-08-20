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
            int timeoutSeconds = DefaultTimeoutSeconds)
        {
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
                    identifier.Trim(), false, out Dictionary<string, object> metadata))
            {
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
            invocationArguments["_agentId"] = agentId;
            invocationArguments["_requestId"] = requestId;
            if (!invocationArguments.ContainsKey("idempotencyKey"))
                invocationArguments["idempotencyKey"] = requestId;

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
                    timeoutSeconds),
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
            int timeoutSeconds)
        {
            Stopwatch stopwatch = Stopwatch.StartNew();
            if (!TryValidateProjectBinding(route, arguments, out VmAutomationInvocationResult bindingError))
                return bindingError;

            if (VmAutomationCatalog.RouteRequiresPlayMode(route) &&
                !MCPRuntimePreconditions.IsStablePlayMode)
            {
                return VmAutomationInvocationResult.Failure(
                    command,
                    route,
                    requestId,
                    MCPRuntimePreconditions.PlayModeRequiredErrorCode,
                    $"Automation command '{command}' requires stable Play Mode.",
                    false,
                    MCPRuntimePreconditions.CreatePlayModeStateDetails(),
                    stopwatch.ElapsedMilliseconds);
            }

            if (VmAutomationCatalog.RouteIsDangerous(route) &&
                !GetBool(arguments, "confirm"))
            {
                return VmAutomationInvocationResult.Failure(
                    command,
                    route,
                    requestId,
                    "confirmation_required",
                    $"Automation command '{command}' requires confirm=true.",
                    false,
                    null,
                    stopwatch.ElapsedMilliseconds);
            }

            if (!VmAutomationCatalog.IsRouteReadOnly(route) &&
                MCPWorkspaceJobRunner.HasActiveJob &&
                !route.StartsWith("jobs/", StringComparison.Ordinal))
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

            MCPToolConfigurationPolicy.ApplyDefaults(route, arguments);
            long actionId = Interlocked.Increment(ref s_ActionSequence);
            MCPRequestUndoCoordinator.Ownership undoOwnership = null;
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
                        rawResult = MCPResponse.Error(
                            $"Project tool route '{route}' is unavailable.",
                            "project_tool_not_found");
                    }
                }
                else if (!MCPBuiltInRouteDescriptorRegistry.TryGet(
                             route, out MCPBuiltInRouteDescriptor descriptor))
                {
                    rawResult = MCPResponse.Error(
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
                rawResult = MCPResponse.Error(
                    exception.GetBaseException().Message,
                    "command_exception");
            }

            bool succeeded = !MCPResponse.TryGetError(
                rawResult,
                out string errorMessage,
                out string errorCode,
                out bool retryable);
            MCPRequestUndoCoordinator.Complete(undoOwnership, succeeded);

            object transportedResult = succeeded
                ? MCPResponse.CompactForTransport(rawResult)
                : MCPResponse.NormalizeError(rawResult, errorCode, retryable);
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

            Dictionary<string, object> details = MCPResponse.ToDictionary(transportedResult);
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

        private static MCPRequestUndoCoordinator.Ownership BeginUndo(
            long actionId,
            string route,
            bool deferred)
        {
            return MCPRequestUndoCoordinator.Begin(
                actionId,
                route,
                !VmAutomationCatalog.IsRouteReadOnly(route) &&
                !deferred &&
                !MCPRequestUndoCoordinator.IsControlRoute(route) &&
                !VmAutomationCatalog.RouteIsLongRunning(route));
        }

        private static async Task<object> ExecuteDeferredAsync(
            MCPBuiltInRouteDescriptor descriptor,
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

            return MCPResponse.Error(
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
            string route,
            IReadOnlyDictionary<string, object> arguments,
            out VmAutomationInvocationResult error)
        {
            string expected = GetString(arguments, "expectedProjectPath");
            bool bindingRequired = VmAutomationCatalog.RouteRequiresTargetBinding(route);
            if (bindingRequired && string.IsNullOrWhiteSpace(expected))
            {
                error = VmAutomationInvocationResult.Failure(
                    route,
                    route,
                    GetString(arguments, "_requestId"),
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
            try
            {
                normalizedExpected = NormalizeProjectPath(expected);
            }
            catch (Exception exception)
            {
                error = VmAutomationInvocationResult.Failure(
                    route,
                    route,
                    GetString(arguments, "_requestId"),
                    "invalid_project_path",
                    exception.GetBaseException().Message);
                return false;
            }

            StringComparison comparison = Application.platform ==
                                          RuntimePlatform.WindowsEditor
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal;
            if (string.Equals(actual, normalizedExpected, comparison))
            {
                error = null;
                return true;
            }

            error = VmAutomationInvocationResult.Failure(
                route,
                route,
                GetString(arguments, "_requestId"),
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
            MCPRequestUndoCoordinator.Ownership undoOwnership,
            bool deferred)
        {
            try
            {
                var record = new MCPActionRecord
                {
                    Timestamp = DateTime.UtcNow,
                    AgentId = agentId,
                    ActionName = route,
                    Category = MCPActionRecord.ExtractCategory(route),
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
                MCPActionHistory.RecordAction(record);
                MCPRequestUndoCoordinator.RegisterAction(record, undoOwnership);
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

        private static bool GetBool(
            IReadOnlyDictionary<string, object> arguments,
            string key)
        {
            if (arguments == null ||
                !arguments.TryGetValue(key, out object value) ||
                value == null)
            {
                return false;
            }

            return value is bool boolean
                ? boolean
                : bool.TryParse(value.ToString(), out bool parsed) && parsed;
        }
    }
}
