using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace VMUnityAutomation.Editor
{
    public static class VmProjectToolRegistry
    {
        public const string DirectRoutePrefix = "project-tools/call/";
        private static List<VmProjectToolDescriptor> _cachedProjectTools;

        internal static void InvalidateCache()
        {
            _cachedProjectTools = null;
        }

        private static readonly string[] ProjectBindingArgumentNames =
        {
            "expectedProjectPath",
            "targetProjectPath",
            "unityProjectPath",
            "_agentId",
            "_requestId",
            "_jobId",
            "idempotencyKey",
            "runAsJob",
            "jobAccessToken",
        };

        public static List<Dictionary<string, object>> GetToolSummaries(bool validOnly)
        {
            return DiscoverTools()
                .Where(tool => validOnly == false || string.IsNullOrEmpty(tool.ValidationError))
                .OrderBy(tool => tool.ToolName, StringComparer.OrdinalIgnoreCase)
                .Select(tool => tool.ToSummaryDictionary())
                .ToList();
        }

        public static List<Dictionary<string, object>> GetToolDetails(bool validOnly)
        {
            return DiscoverTools()
                .Where(tool => validOnly == false || string.IsNullOrEmpty(tool.ValidationError))
                .OrderBy(tool => tool.ToolName, StringComparer.OrdinalIgnoreCase)
                .Select(tool => tool.ToDetailDictionary())
                .ToList();
        }

        public static List<string> GetDirectRoutePaths()
        {
            return DiscoverTools()
                .Where(tool => string.IsNullOrEmpty(tool.ValidationError))
                .GroupBy(tool => tool.ToolName, StringComparer.OrdinalIgnoreCase)
                .Where(group => group.Count() == 1)
                .Select(group => group.Single())
                .OrderBy(tool => tool.ToolName, StringComparer.OrdinalIgnoreCase)
                .Select(tool => GetDirectRoute(tool.ToolName))
                .ToList();
        }

        public static bool TryGetToolDetailForDirectRoute(string path, out Dictionary<string, object> tool)
        {
            tool = null;

            if (TryGetToolNameFromDirectRoute(path, out var toolName) == false)
                return false;

            var matches = FindTools(toolName);
            if (matches.Count != 1)
                return false;

            var descriptor = matches[0];
            if (string.IsNullOrEmpty(descriptor.ValidationError) == false)
                return false;

            tool = descriptor.ToDetailDictionary();
            return true;
        }

        public static bool TryExecuteDirectRoute(string path, Dictionary<string, object> args, out object result)
        {
            result = null;

            if (TryGetToolNameFromDirectRoute(path, out var toolName) == false)
                return false;

            if (string.IsNullOrEmpty(toolName))
            {
                result = new { error = "Project tool route is missing a tool name." };
                return true;
            }

            var matches = FindTools(toolName);
            if (matches.Count != 1)
                return false;

            var descriptor = matches[0];
            if (string.IsNullOrEmpty(descriptor.ValidationError) == false)
                return false;

            result = ExecuteTool(toolName, args ?? new Dictionary<string, object>());
            return true;
        }

        public static string GetDirectRoute(string toolName)
        {
            return DirectRoutePrefix + (toolName ?? "").TrimStart('/');
        }

        private static object ExecuteTool(string toolName, Dictionary<string, object> toolArgs)
        {
            var executionArguments = toolArgs != null
                ? new Dictionary<string, object>(toolArgs)
                : new Dictionary<string, object>();
            toolArgs = RemoveProjectBindingArguments(executionArguments);

            var matches = FindTools(toolName);

            if (matches.Count == 0)
            {
                return VmAutomationResponse.Error($"Project tool '{toolName}' was not found.",
                    "project_tool_not_found");
            }

            if (matches.Count > 1)
            {
                return VmAutomationResponse.Error($"Project tool '{toolName}' is registered more than once.",
                    "duplicate_project_tool", false, new Dictionary<string, object>
                    {
                        { "matches", matches.Select(tool => tool.ToSummaryDictionary()).ToList() }
                    });
            }

            var descriptor = matches[0];
            if (!string.IsNullOrEmpty(descriptor.ValidationError))
                return VmAutomationResponse.Error(descriptor.ValidationError, "invalid_project_tool", false,
                    new Dictionary<string, object> { { "tool", descriptor.ToSummaryDictionary() } });

            if (!descriptor.TryValidateArguments(toolArgs, out var argumentError))
            {
                return VmAutomationResponse.Error(argumentError, "invalid_arguments", false,
                    new Dictionary<string, object> { { "toolName", descriptor.ToolName } });
            }

            if (!TryValidateExecutionPreconditions(descriptor, out VmProjectToolException preconditionError))
                return CreateProjectToolErrorResponse(preconditionError, descriptor.ToolName);

            if (descriptor.LongRunning || GetBool(executionArguments, "runAsJob"))
            {
                var metadata = descriptor.ToJobMetadata();
                CopyExecutionArgument(executionArguments, metadata, "_agentId");
                CopyExecutionArgument(executionArguments, metadata, "_requestId");
                CopyExecutionArgument(executionArguments, metadata, "idempotencyKey");
                toolArgs["_agentId"] = GetString(executionArguments, "_agentId") ?? "anonymous";
                toolArgs["idempotencyKey"] = GetString(executionArguments, "idempotencyKey") ?? "";
                return VmAutomationPersistentJobRunner.StartProjectTool(
                    descriptor.ToolName,
                    toolArgs,
                    metadata);
            }

            return InvokeTool(descriptor, toolArgs);
        }

        internal static object ExecuteJobInline(string toolName, Dictionary<string, object> toolArgs)
        {
            toolArgs = RemoveProjectBindingArguments(toolArgs);
            var matches = FindTools(toolName);
            if (matches.Count == 0)
                return VmAutomationResponse.Error($"Project tool '{toolName}' was not found.", "project_tool_not_found");
            if (matches.Count > 1)
                return VmAutomationResponse.Error($"Project tool '{toolName}' is registered more than once.",
                    "duplicate_project_tool");

            var descriptor = matches[0];
            if (!string.IsNullOrEmpty(descriptor.ValidationError))
                return VmAutomationResponse.Error(descriptor.ValidationError, "invalid_project_tool");
            if (!descriptor.TryValidateArguments(toolArgs, out var argumentError))
                return VmAutomationResponse.Error(argumentError, "invalid_arguments");

            if (!TryValidateExecutionPreconditions(descriptor, out VmProjectToolException preconditionError))
                return CreateProjectToolErrorResponse(preconditionError, descriptor.ToolName);

            return InvokeTool(descriptor, toolArgs);
        }

        internal static VmProjectToolJobStep ExecuteJobStepInline(string toolName,
            Dictionary<string, object> toolArgs, Dictionary<string, object> state)
        {
            toolArgs = RemoveProjectBindingArguments(toolArgs);
            var matches = FindTools(toolName);
            if (matches.Count == 0)
            {
                throw new VmProjectToolException("project_tool_not_found",
                    $"Project tool '{toolName}' was not found.");
            }
            if (matches.Count > 1)
            {
                throw new VmProjectToolException("duplicate_project_tool",
                    $"Project tool '{toolName}' is registered more than once.");
            }

            var descriptor = matches[0];
            if (!string.IsNullOrEmpty(descriptor.ValidationError))
                throw new VmProjectToolException("invalid_project_tool", descriptor.ValidationError);
            if (!descriptor.TryValidateArguments(toolArgs, out string argumentError))
                throw new VmProjectToolException("invalid_arguments", argumentError);

            if (!TryValidateExecutionPreconditions(descriptor, out VmProjectToolException preconditionError))
                throw preconditionError;

            if (!descriptor.SupportsIncrementalJobs)
                return VmProjectToolJobStep.Complete(InvokeTool(descriptor, toolArgs));

            VmProjectToolJobStep step;
            try
            {
                step = descriptor.InvokeJobStep(toolArgs,
                    state ?? new Dictionary<string, object>());
            }
            catch (TargetInvocationException exception)
            {
                Exception inner = exception.InnerException ?? exception;
                if (inner is VmProjectToolException projectToolException)
                    throw projectToolException;
                throw new VmProjectToolException("project_tool_exception", inner.Message);
            }

            if (step == null)
            {
                throw new VmProjectToolException("invalid_project_tool_job_step",
                    $"Persistent project tool '{descriptor.ToolName}' returned a null job step.");
            }
            if (!step.IsComplete)
                return step;

            if (!descriptor.TryValidateResult(step.Result, out string resultError))
            {
                throw new VmProjectToolException("project_tool_output_schema_mismatch",
                    resultError, false, new Dictionary<string, object>
                    {
                        { "toolName", descriptor.ToolName },
                    });
            }

            return VmProjectToolJobStep.Complete(
                VmAutomationResponse.Success(step.Result, new Dictionary<string, object>
                {
                    { "toolName", descriptor.ToolName },
                }),
                step.CleanupToken);
        }

        private static object InvokeTool(VmProjectToolDescriptor descriptor, Dictionary<string, object> toolArgs)
        {
            try
            {
                object result = descriptor.Invoke(toolArgs);
                if (!descriptor.TryValidateResult(result, out var resultError))
                {
                    return VmAutomationResponse.Error(resultError, "project_tool_output_schema_mismatch", false,
                        new Dictionary<string, object>
                        {
                            { "toolName", descriptor.ToolName },
                        });
                }
                return VmAutomationResponse.Success(result, new Dictionary<string, object>
                {
                    { "toolName", descriptor.ToolName },
                });
            }
            catch (TargetInvocationException ex)
            {
                Exception inner = ex.InnerException ?? ex;
                Debug.LogException(inner);
                if (inner is VmProjectToolException projectToolException)
                    return CreateProjectToolErrorResponse(projectToolException, descriptor.ToolName);
                return VmAutomationResponse.Error(inner.Message, "project_tool_exception", false,
                    new Dictionary<string, object>
                    {
                        { "toolName", descriptor.ToolName }
                    });
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                if (ex is VmProjectToolException projectToolException)
                    return CreateProjectToolErrorResponse(projectToolException, descriptor.ToolName);
                return VmAutomationResponse.Error(ex.Message, "project_tool_exception", false,
                    new Dictionary<string, object>
                    {
                        { "toolName", descriptor.ToolName }
                    });
            }
        }

        private static bool TryValidateExecutionPreconditions(VmProjectToolDescriptor descriptor,
            out VmProjectToolException error)
        {
            if (!descriptor.RequiresPlayMode || VmAutomationRuntimePreconditions.IsStablePlayMode)
            {
                error = null;
                return true;
            }

            Dictionary<string, object> details = VmAutomationRuntimePreconditions.CreatePlayModeStateDetails();
            details["toolName"] = descriptor.ToolName;
            error = new VmProjectToolException(
                VmAutomationRuntimePreconditions.PlayModeRequiredErrorCode,
                $"Project tool '{descriptor.ToolName}' requires stable Play Mode.",
                false,
                details);
            return false;
        }

        private static object CreateProjectToolErrorResponse(VmProjectToolException error,
            string toolName)
        {
            return VmAutomationResponse.Error(
                error.Message,
                error.ErrorCode,
                error.Retryable,
                MergeErrorDetails(error.Details, toolName));
        }

        private static Dictionary<string, object> MergeErrorDetails(
            Dictionary<string, object> details, string toolName)
        {
            var merged = details != null
                ? new Dictionary<string, object>(details)
                : new Dictionary<string, object>();
            merged["toolName"] = toolName;
            return merged;
        }

        private static void CopyExecutionArgument(Dictionary<string, object> source,
            Dictionary<string, object> destination, string key)
        {
            if (source != null && source.TryGetValue(key, out object value) && value != null)
                destination[key] = value;
        }

        private static Dictionary<string, object> RemoveProjectBindingArguments(
            Dictionary<string, object> toolArgs)
        {
            var businessArguments = toolArgs != null
                ? new Dictionary<string, object>(toolArgs)
                : new Dictionary<string, object>();

            foreach (string argumentName in ProjectBindingArgumentNames)
                businessArguments.Remove(argumentName);

            return businessArguments;
        }

        private static List<VmProjectToolDescriptor> FindTools(string toolName)
        {
            return DiscoverTools()
                .Where(tool => string.Equals(tool.ToolName, toolName, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        private static bool TryGetToolNameFromDirectRoute(string path, out string toolName)
        {
            toolName = null;

            if (string.IsNullOrEmpty(path) || path.StartsWith(DirectRoutePrefix, StringComparison.Ordinal) == false)
                return false;

            var encodedToolName = path.Substring(DirectRoutePrefix.Length);
            toolName = Uri.UnescapeDataString(encodedToolName);
            return true;
        }

        private static List<VmProjectToolDescriptor> DiscoverTools()
        {
            if (_cachedProjectTools != null)
                return _cachedProjectTools;

            var tools = new List<VmProjectToolDescriptor>();

            foreach (var method in TypeCache.GetMethodsWithAttribute<VmProjectToolAttribute>())
            {
                var attribute = method.GetCustomAttribute<VmProjectToolAttribute>(false);
                if (attribute != null)
                    tools.Add(VmProjectToolDescriptor.FromMethod(attribute, method));
            }

            foreach (var type in TypeCache.GetTypesWithAttribute<VmProjectToolAttribute>())
            {
                var attribute = type.GetCustomAttribute<VmProjectToolAttribute>(false);
                if (attribute != null)
                    tools.Add(VmProjectToolDescriptor.FromType(attribute, type));
            }

            foreach (VmProjectToolDescriptor descriptor in tools)
            {
                if (string.IsNullOrWhiteSpace(descriptor.CleanupToolName))
                    continue;

                List<VmProjectToolDescriptor> cleanupMatches = tools
                    .Where(candidate => string.Equals(candidate.ToolName,
                        descriptor.CleanupToolName, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                string cleanupError = cleanupMatches.Count == 1
                    ? cleanupMatches[0].ValidateCleanupInputContract()
                    : cleanupMatches.Count == 0
                        ? $"Cleanup tool '{descriptor.CleanupToolName}' was not found."
                        : $"Cleanup tool '{descriptor.CleanupToolName}' is registered more than once.";
                descriptor.ValidationError = VmProjectToolDescriptor.CombineValidationErrors(
                    descriptor.ValidationError, cleanupError);
            }

            _cachedProjectTools = tools;
            return _cachedProjectTools;
        }

        private static string GetString(Dictionary<string, object> args, string key)
        {
            if (args == null || !args.TryGetValue(key, out object value) || value == null)
                return null;

            return value.ToString();
        }

        private static bool GetBool(Dictionary<string, object> args, string key)
        {
            if (args == null || !args.TryGetValue(key, out object value) || value == null)
                return false;
            if (value is bool boolValue)
                return boolValue;
            return bool.TryParse(value.ToString(), out bool parsed) && parsed;
        }

    }
}
