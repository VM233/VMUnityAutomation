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
        private static List<ProjectToolDescriptor> _cachedProjectTools;

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
                return MCPResponse.Error($"Project tool '{toolName}' was not found.",
                    "project_tool_not_found");
            }

            if (matches.Count > 1)
            {
                return MCPResponse.Error($"Project tool '{toolName}' is registered more than once.",
                    "duplicate_project_tool", false, new Dictionary<string, object>
                    {
                        { "matches", matches.Select(tool => tool.ToSummaryDictionary()).ToList() }
                    });
            }

            var descriptor = matches[0];
            if (!string.IsNullOrEmpty(descriptor.ValidationError))
                return MCPResponse.Error(descriptor.ValidationError, "invalid_project_tool", false,
                    new Dictionary<string, object> { { "tool", descriptor.ToSummaryDictionary() } });

            if (!descriptor.TryValidateArguments(toolArgs, out var argumentError))
            {
                return MCPResponse.Error(argumentError, "invalid_arguments", false,
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
                return MCPPersistentJobRunner.StartProjectTool(
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
                return MCPResponse.Error($"Project tool '{toolName}' was not found.", "project_tool_not_found");
            if (matches.Count > 1)
                return MCPResponse.Error($"Project tool '{toolName}' is registered more than once.",
                    "duplicate_project_tool");

            var descriptor = matches[0];
            if (!string.IsNullOrEmpty(descriptor.ValidationError))
                return MCPResponse.Error(descriptor.ValidationError, "invalid_project_tool");
            if (!descriptor.TryValidateArguments(toolArgs, out var argumentError))
                return MCPResponse.Error(argumentError, "invalid_arguments");

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
                MCPResponse.Success(step.Result, new Dictionary<string, object>
                {
                    { "toolName", descriptor.ToolName },
                }),
                step.CleanupToken);
        }

        private static object InvokeTool(ProjectToolDescriptor descriptor, Dictionary<string, object> toolArgs)
        {
            try
            {
                object result = descriptor.Invoke(toolArgs);
                if (!descriptor.TryValidateResult(result, out var resultError))
                {
                    return MCPResponse.Error(resultError, "project_tool_output_schema_mismatch", false,
                        new Dictionary<string, object>
                        {
                            { "toolName", descriptor.ToolName },
                        });
                }
                return MCPResponse.Success(result, new Dictionary<string, object>
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
                return MCPResponse.Error(inner.Message, "project_tool_exception", false,
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
                return MCPResponse.Error(ex.Message, "project_tool_exception", false,
                    new Dictionary<string, object>
                    {
                        { "toolName", descriptor.ToolName }
                    });
            }
        }

        private static bool TryValidateExecutionPreconditions(ProjectToolDescriptor descriptor,
            out VmProjectToolException error)
        {
            if (!descriptor.RequiresPlayMode || MCPRuntimePreconditions.IsStablePlayMode)
            {
                error = null;
                return true;
            }

            Dictionary<string, object> details = MCPRuntimePreconditions.CreatePlayModeStateDetails();
            details["toolName"] = descriptor.ToolName;
            error = new VmProjectToolException(
                MCPRuntimePreconditions.PlayModeRequiredErrorCode,
                $"Project tool '{descriptor.ToolName}' requires stable Play Mode.",
                false,
                details);
            return false;
        }

        private static object CreateProjectToolErrorResponse(VmProjectToolException error,
            string toolName)
        {
            return MCPResponse.Error(
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

        private static List<ProjectToolDescriptor> FindTools(string toolName)
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

        private static List<ProjectToolDescriptor> DiscoverTools()
        {
            if (_cachedProjectTools != null)
                return _cachedProjectTools;

            var tools = new List<ProjectToolDescriptor>();

            foreach (var method in TypeCache.GetMethodsWithAttribute<VmProjectToolAttribute>())
            {
                var attribute = method.GetCustomAttribute<VmProjectToolAttribute>(false);
                if (attribute != null)
                    tools.Add(ProjectToolDescriptor.FromMethod(attribute, method));
            }

            foreach (var type in TypeCache.GetTypesWithAttribute<VmProjectToolAttribute>())
            {
                var attribute = type.GetCustomAttribute<VmProjectToolAttribute>(false);
                if (attribute != null)
                    tools.Add(ProjectToolDescriptor.FromType(attribute, type));
            }

            foreach (ProjectToolDescriptor descriptor in tools)
            {
                if (string.IsNullOrWhiteSpace(descriptor.CleanupToolName))
                    continue;

                List<ProjectToolDescriptor> cleanupMatches = tools
                    .Where(candidate => string.Equals(candidate.ToolName,
                        descriptor.CleanupToolName, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                string cleanupError = cleanupMatches.Count == 1
                    ? cleanupMatches[0].ValidateCleanupInputContract()
                    : cleanupMatches.Count == 0
                        ? $"Cleanup tool '{descriptor.CleanupToolName}' was not found."
                        : $"Cleanup tool '{descriptor.CleanupToolName}' is registered more than once.";
                descriptor.ValidationError = ProjectToolDescriptor.CombineValidationErrors(
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

        private sealed class ProjectToolDescriptor
        {
            public string ToolName;
            public string Description;
            public string ShortName;
            public string ModuleId;
            public string Capability;
            public string OperationKind;
            public string WhenToUse;
            public string NotFor;
            public string CompletionEvidence;
            public List<string> Aliases;
            public List<string> SearchTerms;
            public List<string> Preconditions;
            public MCPTransactionProfile Transaction;
            public string Source;
            public string ValidationError;
            public Dictionary<string, object> InputSchema;
            public Dictionary<string, object> OutputSchema;
            public bool EnforcesOutputSchema;
            public bool ReadOnly;
            public bool MutatesAssets;
            public bool MutatesRuntime;
            public bool MutatesProjectFiles;
            public bool Dangerous;
            public bool LongRunning;
            public bool MayReloadDomain;
            public bool RequiresPlayMode;
            public string CleanupToolName;
            public VmProjectToolSideEffect SideEffects;
            public List<string> ErrorCodes;
            public bool SupportsIncrementalJobs =>
                type != null && typeof(IVmPersistentProjectTool).IsAssignableFrom(type);

            private MethodInfo method;
            private Type type;
            private Type requestType;
            private Type resultType;
            private Type typedToolInterface;

            public static ProjectToolDescriptor FromMethod(VmProjectToolAttribute attribute, MethodInfo method)
            {
                var descriptor = new ProjectToolDescriptor
                {
                    ToolName = attribute.ToolName,
                    ShortName = attribute.ShortName ?? "",
                    Description = attribute.Description ?? "",
                    ModuleId = VmProjectToolCatalogMetadata.ResolveModuleId(attribute.ModuleId, attribute.ToolName,
                        method.DeclaringType.Assembly),
                    Capability = VmProjectToolCatalogMetadata.ResolveCapability(attribute.Capability, attribute.ToolName),
                    OperationKind = VmProjectToolCatalogMetadata.ResolveOperationKind(attribute.OperationKind, attribute.ReadOnly,
                        attribute.LongRunning),
                    WhenToUse = string.IsNullOrWhiteSpace(attribute.WhenToUse)
                        ? attribute.Description ?? ""
                        : attribute.WhenToUse.Trim(),
                    NotFor = attribute.NotFor?.Trim() ?? "",
                    CompletionEvidence = attribute.CompletionEvidence?.Trim() ?? "",
                    Aliases = VmProjectToolCatalogMetadata.NormalizeStringList(attribute.Aliases),
                    SearchTerms = VmProjectToolCatalogMetadata.NormalizeSearchTerms(attribute.SearchTerms, attribute.ToolName,
                        attribute.Description),
                    Preconditions = VmProjectToolCatalogMetadata.NormalizePreconditions(attribute.Preconditions,
                        attribute.RequiresPlayMode),
                    Transaction = VmProjectToolTransactionMetadata.Build(attribute),
                    Source = method.DeclaringType.FullName + "." + method.Name,
                    ReadOnly = attribute.ReadOnly,
                    MutatesAssets = attribute.MutatesAssets,
                    MutatesRuntime = attribute.MutatesRuntime,
                    MutatesProjectFiles = attribute.MutatesProjectFiles,
                    Dangerous = attribute.Dangerous,
                    LongRunning = attribute.LongRunning,
                    MayReloadDomain = attribute.MayReloadDomain,
                    RequiresPlayMode = attribute.RequiresPlayMode,
                    CleanupToolName = attribute.CleanupToolName ?? "",
                    SideEffects = attribute.SideEffects,
                    ErrorCodes = NormalizeErrorCodes(attribute.ErrorCodes, attribute.RequiresPlayMode),
                    method = method,
                    requestType = ResolveMethodRequestType(method),
                    resultType = method.ReturnType,
                };

                descriptor.ValidationError = descriptor.ValidateMethod();
                descriptor.ValidationError = CombineValidationErrors(descriptor.ValidationError,
                    descriptor.ValidateOperationProfile());
                descriptor.ValidationError = CombineValidationErrors(descriptor.ValidationError,
                    descriptor.TrySetInputSchema(attribute.InputSchemaJson));
                descriptor.ValidationError = CombineValidationErrors(descriptor.ValidationError,
                    descriptor.TrySetOutputSchema(attribute.OutputSchemaJson));
                return descriptor;
            }

            public static ProjectToolDescriptor FromType(VmProjectToolAttribute attribute, Type type)
            {
                var descriptor = new ProjectToolDescriptor
                {
                    ToolName = attribute.ToolName,
                    ShortName = attribute.ShortName ?? "",
                    Description = attribute.Description ?? "",
                    ModuleId = VmProjectToolCatalogMetadata.ResolveModuleId(attribute.ModuleId, attribute.ToolName, type.Assembly),
                    Capability = VmProjectToolCatalogMetadata.ResolveCapability(attribute.Capability, attribute.ToolName),
                    OperationKind = VmProjectToolCatalogMetadata.ResolveOperationKind(attribute.OperationKind, attribute.ReadOnly,
                        attribute.LongRunning),
                    WhenToUse = string.IsNullOrWhiteSpace(attribute.WhenToUse)
                        ? attribute.Description ?? ""
                        : attribute.WhenToUse.Trim(),
                    NotFor = attribute.NotFor?.Trim() ?? "",
                    CompletionEvidence = attribute.CompletionEvidence?.Trim() ?? "",
                    Aliases = VmProjectToolCatalogMetadata.NormalizeStringList(attribute.Aliases),
                    SearchTerms = VmProjectToolCatalogMetadata.NormalizeSearchTerms(attribute.SearchTerms, attribute.ToolName,
                        attribute.Description),
                    Preconditions = VmProjectToolCatalogMetadata.NormalizePreconditions(attribute.Preconditions,
                        attribute.RequiresPlayMode),
                    Transaction = VmProjectToolTransactionMetadata.Build(attribute),
                    Source = type.FullName,
                    ReadOnly = attribute.ReadOnly,
                    MutatesAssets = attribute.MutatesAssets,
                    MutatesRuntime = attribute.MutatesRuntime,
                    MutatesProjectFiles = attribute.MutatesProjectFiles,
                    Dangerous = attribute.Dangerous,
                    LongRunning = attribute.LongRunning,
                    MayReloadDomain = attribute.MayReloadDomain,
                    RequiresPlayMode = attribute.RequiresPlayMode,
                    CleanupToolName = attribute.CleanupToolName ?? "",
                    SideEffects = attribute.SideEffects,
                    ErrorCodes = NormalizeErrorCodes(attribute.ErrorCodes, attribute.RequiresPlayMode),
                    type = type,
                    typedToolInterface = FindTypedToolInterface(type),
                };

                if (descriptor.typedToolInterface != null)
                {
                    Type[] contractTypes = descriptor.typedToolInterface.GetGenericArguments();
                    descriptor.requestType = contractTypes[0];
                    descriptor.resultType = contractTypes[1];
                }

                descriptor.ValidationError = descriptor.ValidateType();
                descriptor.ValidationError = CombineValidationErrors(descriptor.ValidationError,
                    descriptor.ValidateOperationProfile());
                descriptor.ValidationError = CombineValidationErrors(descriptor.ValidationError,
                    descriptor.TrySetInputSchema(attribute.InputSchemaJson));
                descriptor.ValidationError = CombineValidationErrors(descriptor.ValidationError,
                    descriptor.TrySetOutputSchema(attribute.OutputSchemaJson));
                return descriptor;
            }

            public object Invoke(Dictionary<string, object> args)
            {
                if (method != null)
                {
                    var parameters = method.GetParameters();
                    object result = parameters.Length == 0
                        ? method.Invoke(null, null)
                        : method.Invoke(null, new[]
                        {
                            requestType == typeof(Dictionary<string, object>)
                                ? args
                                : VmJsonContract.Bind(args, requestType)
                        });

                    if (method.ReturnType == typeof(void))
                        return "ok";
                    return HasTypedResult ? VmJsonContract.ToTransportValue(result) : result;
                }

                if (typedToolInterface != null)
                {
                    object typedInstance = Activator.CreateInstance(type);
                    MethodInfo executeMethod = typedToolInterface.GetMethod("Execute");
                    object typedRequest = VmJsonContract.Bind(args, requestType);
                    object typedResult = executeMethod.Invoke(typedInstance, new[] { typedRequest });
                    return HasTypedResult
                        ? VmJsonContract.ToTransportValue(typedResult)
                        : typedResult;
                }

                var instance = Activator.CreateInstance(type) as IVmProjectTool;
                object typeResult = instance.Execute(args);
                return typeResult;
            }

            public VmProjectToolJobStep InvokeJobStep(Dictionary<string, object> args,
                Dictionary<string, object> state)
            {
                if (!SupportsIncrementalJobs)
                    return VmProjectToolJobStep.Complete(Invoke(args));

                var instance = Activator.CreateInstance(type) as IVmPersistentProjectTool;
                if (instance == null)
                {
                    throw new InvalidOperationException(
                        $"Could not create persistent project tool instance '{Source}'.");
                }

                return instance.ExecuteJobStep(args, state);
            }

            public Dictionary<string, object> ToSummaryDictionary()
            {
                var summary = new Dictionary<string, object>
                {
                    { "toolName", ToolName },
                    { "description", Description },
                    { "moduleId", ModuleId },
                    { "capability", Capability },
                    { "operationKind", OperationKind },
                };
                MCPContractMetadata.SetTags(summary, MCPContractMetadata.BuildToolTags(
                    readOnly: ReadOnly,
                    dangerous: Dangerous,
                    longRunning: LongRunning,
                    requiresPlayMode: RequiresPlayMode,
                    cleanup: string.IsNullOrEmpty(CleanupToolName) == false,
                    incrementalJob: SupportsIncrementalJobs,
                    outputSchema: EnforcesOutputSchema,
                    invalid: string.IsNullOrEmpty(ValidationError) == false));
                MCPContractMetadata.AddOptionalList(summary, "sideEffects", GetSideEffectNames());
                MCPContractMetadata.AddOptionalList(summary, "errorCodes", GetAdditionalErrorCodes());
                MCPContractMetadata.AddOptionalList(summary, "aliases", Aliases);
                MCPContractMetadata.AddOptionalList(summary, "searchTerms", SearchTerms);
                MCPContractMetadata.AddOptionalList(summary, "preconditions", Preconditions);
                MCPContractMetadata.AddOptionalString(summary, "whenToUse", WhenToUse);
                MCPContractMetadata.AddOptionalString(summary, "notFor", NotFor);
                MCPContractMetadata.AddOptionalString(summary, "completionEvidence", CompletionEvidence);
                MCPContractMetadata.AddOptionalString(summary, "cleanupToolName", CleanupToolName);
                if (Transaction != null)
                    summary["transaction"] = Transaction.ToDictionary();
                MCPContractMetadata.AddOptionalString(summary, "validationError", ValidationError);
                return summary;
            }

            public Dictionary<string, object> ToDetailDictionary()
            {
                var descriptor = new Dictionary<string, object>
                {
                    { "toolName", ToolName },
                    { "description", Description },
                    { "moduleId", ModuleId },
                    { "capability", Capability },
                    { "operationKind", OperationKind },
                    { "executeRoute", GetDirectRoute(ToolName) },
                    { "inputSchema", InputSchema ?? CreateClosedEmptyObjectSchema() },
                    { "outputSchema", OutputSchema ?? CreateClosedEmptyObjectSchema() },
                };
                MCPContractMetadata.AddOptionalString(descriptor, "shortName", ShortName);
                MCPContractMetadata.AddOptionalString(descriptor, "source", Source);
                MCPContractMetadata.SetTags(descriptor, MCPContractMetadata.BuildToolTags(
                    readOnly: ReadOnly,
                    dangerous: Dangerous,
                    longRunning: LongRunning,
                    requiresPlayMode: RequiresPlayMode,
                    cleanup: string.IsNullOrEmpty(CleanupToolName) == false,
                    incrementalJob: SupportsIncrementalJobs,
                    outputSchema: EnforcesOutputSchema,
                    invalid: string.IsNullOrEmpty(ValidationError) == false));
                MCPContractMetadata.AddOptionalList(descriptor, "sideEffects", GetSideEffectNames());
                MCPContractMetadata.AddOptionalList(descriptor, "errorCodes", GetAdditionalErrorCodes());
                MCPContractMetadata.AddOptionalList(descriptor, "aliases", Aliases);
                MCPContractMetadata.AddOptionalList(descriptor, "searchTerms", SearchTerms);
                MCPContractMetadata.AddOptionalList(descriptor, "preconditions", Preconditions);
                MCPContractMetadata.AddOptionalString(descriptor, "whenToUse", WhenToUse);
                MCPContractMetadata.AddOptionalString(descriptor, "notFor", NotFor);
                MCPContractMetadata.AddOptionalString(descriptor, "completionEvidence", CompletionEvidence);
                MCPContractMetadata.AddOptionalString(descriptor, "cleanupToolName", CleanupToolName);
                if (Transaction != null)
                    descriptor["transaction"] = Transaction.ToDictionary();
                MCPContractMetadata.AddOptionalString(descriptor, "validationError", ValidationError);
                return descriptor;
            }

            public Dictionary<string, object> ToJobMetadata()
            {
                var metadata = new Dictionary<string, object>
                {
                    { "toolName", ToolName },
                };
                MCPContractMetadata.AddOptionalList(metadata, "sideEffects", GetSideEffectNames());
                MCPContractMetadata.AddOptionalString(metadata, "cleanupToolName", CleanupToolName);
                MCPContractMetadata.SetTags(metadata, MCPContractMetadata.BuildToolTags(
                    cleanup: string.IsNullOrEmpty(CleanupToolName) == false,
                    incrementalJob: SupportsIncrementalJobs));
                return metadata;
            }

            public string ValidateCleanupInputContract()
            {
                Dictionary<string, object> schema = InputSchema ?? CreateClosedEmptyObjectSchema();
                Dictionary<string, object> properties = GetSchemaProperties(schema);
                bool allowsAdditionalProperties = !schema.TryGetValue("additionalProperties",
                    out object additionalProperties) ||
                    !(additionalProperties is bool additionalPropertiesFlag) ||
                    additionalPropertiesFlag;
                if (!allowsAdditionalProperties &&
                    (properties == null ||
                     !properties.ContainsKey("action") ||
                     !properties.ContainsKey("cleanupToken")))
                {
                    return $"Cleanup tool '{ToolName}' must accept action and cleanupToken.";
                }
                return null;
            }

            private string TrySetInputSchema(string inputSchemaJson)
            {
                bool hasTypedRequest = requestType != null &&
                                       requestType != typeof(Dictionary<string, object>);
                if (hasTypedRequest)
                {
                    string error = null;
                    try
                    {
                        InputSchema = VmJsonContract.CreateSchema(requestType);
                        error = ValidateInputSchema(InputSchema);
                    }
                    catch (Exception exception)
                    {
                        InputSchema = CreateClosedEmptyObjectSchema();
                        error = $"Typed project-tool request contract '{requestType.FullName}' is invalid: " +
                                exception.Message;
                    }

                    if (string.IsNullOrEmpty(inputSchemaJson) == false)
                    {
                        error = CombineValidationErrors(error,
                            "Typed project-tool requests generate their input schema from the request contract; " +
                            "InputSchemaJson must not declare a second contract.");
                    }
                    return error;
                }

                if (string.IsNullOrEmpty(inputSchemaJson))
                {
                    InputSchema = CreateClosedEmptyObjectSchema();
                    bool acceptsDictionary = requestType == typeof(Dictionary<string, object>) ||
                                             method == null && typedToolInterface == null;
                    return acceptsDictionary
                        ? "Dictionary project-tool requests must declare InputSchemaJson."
                        : null;
                }

                try
                {
                    InputSchema = MiniJson.Deserialize(inputSchemaJson) as Dictionary<string, object>;
                    if (InputSchema == null)
                        return "InputSchemaJson must deserialize to a JSON object.";

                    return ValidateInputSchema(InputSchema);
                }
                catch (Exception ex)
                {
                    InputSchema = CreateClosedEmptyObjectSchema();
                    return $"InputSchemaJson is invalid JSON: {ex.Message}";
                }
            }

            private string TrySetOutputSchema(string outputSchemaJson)
            {
                bool hasTypedResult = HasTypedResult;
                if (hasTypedResult)
                {
                    string error = null;
                    try
                    {
                        OutputSchema = VmJsonContract.CreateSchema(resultType);
                        EnforcesOutputSchema = true;
                    }
                    catch (Exception exception)
                    {
                        OutputSchema = CreateClosedEmptyObjectSchema();
                        EnforcesOutputSchema = false;
                        error = $"Typed project-tool result contract '{resultType.FullName}' is invalid: " +
                                exception.Message;
                    }

                    if (string.IsNullOrEmpty(outputSchemaJson) == false)
                    {
                        error = CombineValidationErrors(error,
                            "Typed project-tool results generate their output schema from the result contract; " +
                            "OutputSchemaJson must not declare a second contract.");
                    }
                    return error;
                }

                if (string.IsNullOrEmpty(outputSchemaJson))
                {
                    OutputSchema = CreateClosedEmptyObjectSchema();
                    EnforcesOutputSchema = false;
                    return "Untyped project-tool results must declare OutputSchemaJson.";
                }

                try
                {
                    OutputSchema = MiniJson.Deserialize(outputSchemaJson) as Dictionary<string, object>;
                    if (OutputSchema == null)
                        return "OutputSchemaJson must deserialize to a JSON object.";

                    EnforcesOutputSchema = true;
                    return ValidateOutputSchema(OutputSchema);
                }
                catch (Exception ex)
                {
                    OutputSchema = CreateClosedEmptyObjectSchema();
                    EnforcesOutputSchema = false;
                    return $"OutputSchemaJson is invalid JSON: {ex.Message}";
                }
            }

            public bool TryValidateArguments(Dictionary<string, object> args, out string error)
            {
                args = args ?? new Dictionary<string, object>();
                var schema = InputSchema ?? CreateClosedEmptyObjectSchema();
                var errors = new List<string>();
                ValidateValueAgainstSchema(args, schema, "$", errors, true);

                if (errors.Count == 0)
                {
                    error = null;
                    return true;
                }

                error = string.Join(" ", errors);
                return false;
            }

            public bool TryValidateResult(object result, out string error)
            {
                if (!EnforcesOutputSchema)
                {
                    error = null;
                    return true;
                }

                var errors = new List<string>();
                ValidateValueAgainstSchema(result, OutputSchema ?? CreateClosedEmptyObjectSchema(), "$", errors, true);
                if (errors.Count == 0)
                {
                    error = null;
                    return true;
                }

                error = string.Join(" ", errors);
                return false;
            }

            private string ValidateMethod()
            {
                if (string.IsNullOrEmpty(ToolName))
                    return "VmProjectToolAttribute toolName cannot be empty.";

                if (!method.IsStatic)
                    return $"Project tool method '{Source}' must be static.";

                var parameters = method.GetParameters();
                if (parameters.Length > 1)
                    return $"Project tool method '{Source}' must accept zero parameters or one request contract.";

                if (parameters.Length == 1 && parameters[0].ParameterType == typeof(object))
                    return $"Project tool method '{Source}' request contract cannot be object.";

                if (parameters.Length == 1 && parameters[0].ParameterType != typeof(Dictionary<string, object>) &&
                    parameters[0].ParameterType.GetConstructor(Type.EmptyTypes) == null &&
                    parameters[0].ParameterType.IsValueType == false)
                {
                    return $"Project tool method '{Source}' request contract requires a public parameterless constructor.";
                }

                return null;
            }

            private string ValidateType()
            {
                if (string.IsNullOrEmpty(ToolName))
                    return "VmProjectToolAttribute toolName cannot be empty.";

                if (!typeof(IVmProjectTool).IsAssignableFrom(type) && typedToolInterface == null)
                    return $"Project tool type '{Source}' must implement IVmProjectTool or IVmProjectTool<TRequest, TResult>.";

                if (GetTypedToolInterfaces(type).Length > 1)
                    return $"Project tool type '{Source}' must implement exactly one IVmProjectTool<TRequest, TResult>.";

                if (type.IsAbstract)
                    return $"Project tool type '{Source}' cannot be abstract.";

                if (type.GetConstructor(Type.EmptyTypes) == null)
                    return $"Project tool type '{Source}' must have a public parameterless constructor.";

                return null;
            }

            private string ValidateOperationProfile()
            {
                int operationKinds = (ReadOnly ? 1 : 0) +
                                     (MutatesAssets ? 1 : 0) +
                                     (MutatesRuntime ? 1 : 0) +
                                     (MutatesProjectFiles ? 1 : 0);
                if (operationKinds > 1)
                    return $"Project tool '{ToolName}' declares conflicting operation kinds.";

                if (operationKinds == 0)
                    return $"Project tool '{ToolName}' must explicitly declare ReadOnly, MutatesAssets, MutatesRuntime, or MutatesProjectFiles.";

                VmProjectToolSideEffect effects = GetEffectiveSideEffects();
                VmProjectToolSideEffect writes = VmProjectToolSideEffect.WritesAssets |
                                                  VmProjectToolSideEffect.WritesScene |
                                                  VmProjectToolSideEffect.ChangesRuntimeState |
                                                  VmProjectToolSideEffect.AdvancesEditorFrames |
                                                  VmProjectToolSideEffect.AdvancesLogicTicks |
                                                   VmProjectToolSideEffect.CreatesTemporaryObjects |
                                                   VmProjectToolSideEffect.PerformsExternalIO |
                                                   VmProjectToolSideEffect.ReloadsDomain |
                                                   VmProjectToolSideEffect.ExecutesArbitraryCode |
                                                   VmProjectToolSideEffect.WritesProjectFiles;
                if (ReadOnly && (effects & writes) != 0)
                    return $"Read-only project tool '{ToolName}' declares mutating side effects.";

                string transactionError = VmProjectToolTransactionMetadata.Validate(
                    Transaction, ReadOnly, ToolName);
                if (transactionError != null)
                    return transactionError;

                return null;
            }

            private static Dictionary<string, object> CreateClosedEmptyObjectSchema()
            {
                return new Dictionary<string, object>
                {
                    { "type", "object" },
                    { "properties", new Dictionary<string, object>() },
                    { "additionalProperties", false }
                };
            }

            private bool HasTypedResult => resultType != null && resultType != typeof(void) &&
                                           resultType != typeof(object) &&
                                           resultType != typeof(Dictionary<string, object>);

            private static Type ResolveMethodRequestType(MethodInfo targetMethod)
            {
                ParameterInfo[] parameters = targetMethod.GetParameters();
                return parameters.Length == 1 ? parameters[0].ParameterType : null;
            }

            private static Type FindTypedToolInterface(Type targetType)
            {
                return GetTypedToolInterfaces(targetType).FirstOrDefault();
            }

            private static Type[] GetTypedToolInterfaces(Type targetType)
            {
                return targetType.GetInterfaces().Where(candidate =>
                    candidate.IsGenericType &&
                    candidate.GetGenericTypeDefinition() == typeof(IVmProjectTool<,>)).ToArray();
            }

            private static string ValidateOutputSchema(Dictionary<string, object> schema)
            {
                var schemaErrors = new List<string>();
                ValidateSchemaNode(schema, "$", schemaErrors, schema);
                return schemaErrors.Count == 0 ? null : string.Join(" ", schemaErrors);
            }

            private VmProjectToolSideEffect GetEffectiveSideEffects()
            {
                VmProjectToolSideEffect effects = SideEffects;
                if (ReadOnly)
                    effects |= VmProjectToolSideEffect.ReadsProjectState;
                if (MutatesAssets)
                    effects |= VmProjectToolSideEffect.WritesAssets;
                if (MutatesRuntime)
                    effects |= VmProjectToolSideEffect.ChangesRuntimeState;
                if (MutatesProjectFiles)
                    effects |= VmProjectToolSideEffect.WritesProjectFiles;
                if (MayReloadDomain)
                    effects |= VmProjectToolSideEffect.ReloadsDomain;
                return effects;
            }

            private List<string> GetSideEffectNames()
            {
                VmProjectToolSideEffect effects = GetEffectiveSideEffects();
                return Enum.GetValues(typeof(VmProjectToolSideEffect))
                    .Cast<VmProjectToolSideEffect>()
                    .Where(effect => effect != VmProjectToolSideEffect.None && (effects & effect) == effect)
                    .Select(effect => ToCamelCase(effect.ToString()))
                    .ToList();
            }

            private List<string> GetAdditionalErrorCodes()
            {
                return ErrorCodes
                    .Where(code =>
                        !string.Equals(code, "invalid_arguments", StringComparison.Ordinal) &&
                        !string.Equals(code, "project_tool_exception", StringComparison.Ordinal) &&
                        !string.Equals(code, "project_tool_output_schema_mismatch", StringComparison.Ordinal))
                    .ToList();
            }

            private static string ToCamelCase(string value)
            {
                return string.IsNullOrEmpty(value)
                    ? value
                    : char.ToLowerInvariant(value[0]) + value.Substring(1);
            }

            private static List<string> NormalizeErrorCodes(IEnumerable<string> errorCodes,
                bool requiresPlayMode)
            {
                var result = new List<string>
                {
                    "invalid_arguments",
                    "project_tool_exception",
                    "project_tool_output_schema_mismatch",
                };
                if (errorCodes != null)
                {
                    result.AddRange(errorCodes.Where(code => string.IsNullOrWhiteSpace(code) == false)
                        .Select(code => code.Trim()));
                }
                if (requiresPlayMode)
                    result.Add(MCPRuntimePreconditions.PlayModeRequiredErrorCode);
                return result.Distinct(StringComparer.Ordinal).OrderBy(code => code, StringComparer.Ordinal).ToList();
            }

            private static string ValidateInputSchema(Dictionary<string, object> schema)
            {
                if (schema.TryGetValue("type", out var type) && type != null &&
                    type.ToString() != "object")
                    return "InputSchemaJson root type must be object.";

                var properties = GetSchemaProperties(schema);
                if (properties == null)
                    return "InputSchemaJson properties must be a JSON object.";

                var required = GetRequiredProperties(schema);
                foreach (string requiredName in required)
                {
                    if (!properties.ContainsKey(requiredName))
                        return $"InputSchemaJson required property '{requiredName}' is not declared in properties.";
                }

                var schemaErrors = new List<string>();
                ValidateSchemaNode(schema, "$", schemaErrors, schema);
                if (schemaErrors.Count > 0)
                    return string.Join(" ", schemaErrors);

                return null;
            }

            private static void ValidateSchemaNode(Dictionary<string, object> schema, string path,
                List<string> errors, Dictionary<string, object> root,
                bool allowConstraintOnly = false)
            {
                if (schema == null)
                    return;

                if (schema.TryGetValue("x-unityMcpOpaque", out object opaque) &&
                    opaque is bool opaqueFlag && opaqueFlag)
                {
                    errors.Add($"{path} is explicitly opaque.");
                }

                List<string> allowedTypes = GetAllowedTypes(
                    schema.TryGetValue("type", out object rawType) ? rawType : null);
                foreach (string typeName in allowedTypes)
                {
                    if (typeName != "string" && typeName != "number" && typeName != "integer" &&
                        typeName != "boolean" && typeName != "object" && typeName != "array" &&
                        typeName != "null")
                        errors.Add($"{path} declares unsupported schema type '{typeName}'.");
                }

                bool hasCombinator = new[] { "allOf", "anyOf", "oneOf" }
                    .Any(schema.ContainsKey);
                bool hasShape = allowedTypes.Count > 0 || schema.ContainsKey("$ref") ||
                                schema.ContainsKey("const") || schema.ContainsKey("enum") ||
                                hasCombinator;
                if (!hasShape && !allowConstraintOnly)
                    errors.Add($"{path} must declare a value shape.");

                if (schema.TryGetValue("$defs", out object definitionsValue))
                {
                    if (definitionsValue is Dictionary<string, object> definitions)
                    {
                        foreach (KeyValuePair<string, object> definition in definitions)
                        {
                            if (definition.Value is Dictionary<string, object> definitionSchema)
                                ValidateSchemaNode(definitionSchema,
                                    $"{path}.$defs.{definition.Key}", errors, root);
                            else
                                errors.Add($"{path}.$defs.{definition.Key} must be an object.");
                        }
                    }
                    else
                    {
                        errors.Add($"{path}.$defs must be an object.");
                    }
                }

                if (schema.TryGetValue("$ref", out object referenceValue))
                {
                    string reference = referenceValue?.ToString() ?? "";
                    const string prefix = "#/$defs/";
                    if (!reference.StartsWith(prefix, StringComparison.Ordinal) ||
                        reference.Substring(prefix.Length).Contains("/"))
                    {
                        errors.Add($"{path} uses unsupported schema reference '{reference}'.");
                    }
                    else
                    {
                        string definitionName = reference.Substring(prefix.Length);
                        if (!root.TryGetValue("$defs", out object rootDefinitionsValue) ||
                            !(rootDefinitionsValue is Dictionary<string, object> rootDefinitions) ||
                            !(rootDefinitions.TryGetValue(definitionName, out object definitionValue) &&
                              definitionValue is Dictionary<string, object>))
                        {
                            errors.Add($"{path} references missing local definition '{definitionName}'.");
                        }
                    }
                }

                if (schema.TryGetValue("enum", out object enumValue) &&
                    (!(enumValue is IList enumList) || enumList.Count == 0))
                    errors.Add($"{path}.enum must be a non-empty array.");

                if (allowedTypes.Contains("object"))
                {
                    Dictionary<string, object> properties = null;
                    if (schema.TryGetValue("properties", out object propertiesValue))
                    {
                        properties = propertiesValue as Dictionary<string, object>;
                        if (properties == null)
                            errors.Add($"{path}.properties must be an object.");
                    }
                    else
                    {
                        properties = new Dictionary<string, object>();
                    }

                    foreach (KeyValuePair<string, object> pair in properties)
                    {
                        if (pair.Value is Dictionary<string, object> propertySchema)
                            ValidateSchemaNode(propertySchema, path + "." + pair.Key,
                                errors, root);
                        else
                            errors.Add($"{path}.{pair.Key} schema must be an object.");
                    }

                    foreach (string requiredName in GetRequiredProperties(schema))
                    {
                        if (!properties.ContainsKey(requiredName))
                            errors.Add($"{path} requires undeclared property '{requiredName}'.");
                    }

                    if (!schema.TryGetValue("additionalProperties", out object additional))
                    {
                        errors.Add($"{path} must close object properties or declare a typed map value schema.");
                    }
                    else if (additional is bool additionalFlag)
                    {
                        if (additionalFlag)
                            errors.Add($"{path} cannot accept arbitrary additional properties.");
                    }
                    else if (additional is Dictionary<string, object> additionalSchema)
                    {
                        if (additionalSchema.Count == 0)
                            errors.Add($"{path}.additionalProperties cannot be an empty schema.");
                        else
                            ValidateSchemaNode(additionalSchema,
                                path + ".additionalProperties", errors, root);
                    }
                    else
                    {
                        errors.Add($"{path}.additionalProperties must be false or a schema object.");
                    }
                }

                if (allowedTypes.Contains("array"))
                {
                    if (!schema.TryGetValue("items", out object items) ||
                        !(items is Dictionary<string, object> itemSchema) ||
                        itemSchema.Count == 0)
                    {
                        errors.Add($"{path}.items must declare an exact item schema.");
                    }
                    else
                    {
                        ValidateSchemaNode(itemSchema, path + "[]", errors, root);
                    }
                }

                foreach (string keyword in new[] { "allOf", "anyOf", "oneOf" })
                {
                    if (!schema.TryGetValue(keyword, out object variantsValue))
                        continue;
                    if (!(variantsValue is IList variants) || variants.Count == 0)
                    {
                        errors.Add($"{path}.{keyword} must be a non-empty array of schemas.");
                        continue;
                    }

                    for (int index = 0; index < variants.Count; index++)
                    {
                        if (variants[index] is Dictionary<string, object> variantSchema)
                            ValidateSchemaNode(variantSchema, $"{path}.{keyword}[{index}]",
                                errors, root, true);
                        else
                            errors.Add($"{path}.{keyword}[{index}] must be an object.");
                    }
                }

                if (schema.TryGetValue("not", out object notValue))
                {
                    if (notValue is Dictionary<string, object> notSchema)
                        ValidateSchemaNode(notSchema, path + ".not", errors, root, true);
                    else
                        errors.Add($"{path}.not must be an object.");
                }

                if (schema.TryGetValue("pattern", out object pattern) && pattern != null)
                {
                    try
                    {
                        _ = new Regex(pattern.ToString());
                    }
                    catch (ArgumentException ex)
                    {
                        errors.Add($"{path}.pattern is invalid: {ex.Message}");
                    }
                }
            }

            private static void ValidateValueAgainstSchema(object value,
                Dictionary<string, object> schema, string path, List<string> errors,
                bool allowInternalProperties = false)
            {
                if (schema == null)
                    return;

                ValidateSchemaCombinators(value, schema, path, errors);

                if (!MatchesSchemaType(value, schema, out string typeError))
                {
                    errors.Add($"{path} {typeError}");
                    return;
                }

                if (schema.TryGetValue("const", out object constantValue) &&
                    !ValuesEqual(value, constantValue))
                    errors.Add($"{path} must equal its const value.");

                if (schema.TryGetValue("enum", out object enumValue) && enumValue is IList allowedValues)
                {
                    bool matched = allowedValues.Cast<object>().Any(allowed => ValuesEqual(value, allowed));
                    if (!matched)
                        errors.Add($"{path} must be one of [{string.Join(", ", allowedValues.Cast<object>())}].");
                }

                if (value is string stringValue)
                {
                    if (TryGetDouble(schema, "minLength", out double minLength) &&
                        stringValue.Length < minLength)
                        errors.Add($"{path} must contain at least {minLength:0} characters.");
                    if (TryGetDouble(schema, "maxLength", out double maxLength) &&
                        stringValue.Length > maxLength)
                        errors.Add($"{path} must contain at most {maxLength:0} characters.");
                    if (schema.TryGetValue("pattern", out object pattern) && pattern != null &&
                        !MatchesPattern(stringValue, pattern.ToString(), out bool timedOut))
                    {
                        errors.Add(timedOut
                            ? $"{path} pattern validation exceeded the 100 ms match budget."
                            : $"{path} must match pattern '{pattern}'.");
                    }
                }

                if (IsNumber(value))
                {
                    double numericValue = Convert.ToDouble(value);
                    if (TryGetDouble(schema, "minimum", out double minimum) && numericValue < minimum)
                        errors.Add($"{path} must be greater than or equal to {minimum}.");
                    if (TryGetDouble(schema, "maximum", out double maximum) && numericValue > maximum)
                        errors.Add($"{path} must be less than or equal to {maximum}.");
                }

                if (value is IDictionary dictionary)
                {
                    var properties = GetSchemaProperties(schema) ?? new Dictionary<string, object>();
                    foreach (string requiredName in GetRequiredProperties(schema))
                    {
                        if (!dictionary.Contains(requiredName))
                            errors.Add($"{path}.{requiredName} is required.");
                    }

                    foreach (DictionaryEntry pair in dictionary)
                    {
                        string key = pair.Key?.ToString() ?? "";
                        if (allowInternalProperties && key.StartsWith("_", StringComparison.Ordinal))
                            continue;
                        if (!properties.TryGetValue(key, out object propertySchemaValue))
                        {
                            if (IsAdditionalPropertiesFalse(schema))
                                errors.Add($"{path}.{key} is not allowed.");
                            else if (schema.TryGetValue("additionalProperties",
                                         out object additionalSchemaValue) &&
                                     additionalSchemaValue is Dictionary<string, object> additionalSchema)
                                ValidateValueAgainstSchema(
                                    pair.Value, additionalSchema, path + "." + key, errors, false);
                            continue;
                        }
                        if (propertySchemaValue is Dictionary<string, object> propertySchema)
                            ValidateValueAgainstSchema(pair.Value, propertySchema, path + "." + key,
                                errors, false);
                    }
                }

                if (value is IList list && !(value is string))
                {
                    if (TryGetDouble(schema, "minItems", out double minItems) && list.Count < minItems)
                        errors.Add($"{path} must contain at least {minItems:0} items.");
                    if (TryGetDouble(schema, "maxItems", out double maxItems) && list.Count > maxItems)
                        errors.Add($"{path} must contain at most {maxItems:0} items.");
                    if (schema.TryGetValue("items", out object itemSchemaValue) &&
                        itemSchemaValue is Dictionary<string, object> itemSchema)
                    {
                        for (int index = 0; index < list.Count; index++)
                            ValidateValueAgainstSchema(list[index], itemSchema, $"{path}[{index}]",
                                errors, false);
                    }
                }
            }

            private static void ValidateSchemaCombinators(
                object value,
                Dictionary<string, object> schema,
                string path,
                List<string> errors)
            {
                if (schema.TryGetValue("allOf", out object allOfValue) && allOfValue is IList allOf)
                {
                    for (int index = 0; index < allOf.Count; index++)
                    {
                        if (allOf[index] is Dictionary<string, object> variant)
                            ValidateValueAgainstSchema(value, variant, path, errors, false);
                    }
                }

                ValidateAlternativeSchemas(value, schema, path, "anyOf", false, errors);
                ValidateAlternativeSchemas(value, schema, path, "oneOf", true, errors);

                if (schema.TryGetValue("not", out object notValue) &&
                    notValue is Dictionary<string, object> notSchema)
                {
                    var notErrors = new List<string>();
                    ValidateValueAgainstSchema(value, notSchema, path, notErrors, false);
                    if (notErrors.Count == 0)
                        errors.Add($"{path} must not match the schema in not.");
                }
            }

            private static void ValidateAlternativeSchemas(
                object value,
                Dictionary<string, object> schema,
                string path,
                string keyword,
                bool requireExactlyOne,
                List<string> errors)
            {
                if (!schema.TryGetValue(keyword, out object variantsValue) ||
                    !(variantsValue is IList variants))
                    return;

                int matches = 0;
                foreach (object variantValue in variants)
                {
                    if (!(variantValue is Dictionary<string, object> variant))
                        continue;
                    var variantErrors = new List<string>();
                    ValidateValueAgainstSchema(value, variant, path, variantErrors, false);
                    if (variantErrors.Count == 0)
                        matches++;
                }

                bool valid = requireExactlyOne ? matches == 1 : matches > 0;
                if (!valid)
                {
                    errors.Add(requireExactlyOne
                        ? $"{path} must match exactly one schema in oneOf."
                        : $"{path} must match at least one schema in anyOf.");
                }
            }

            private static Dictionary<string, object> GetSchemaProperties(Dictionary<string, object> schema)
            {
                if (!schema.TryGetValue("properties", out var propertiesObj) || propertiesObj == null)
                    return new Dictionary<string, object>();

                return propertiesObj as Dictionary<string, object>;
            }

            private static List<string> GetRequiredProperties(Dictionary<string, object> schema)
            {
                if (!schema.TryGetValue("required", out var requiredObj) || requiredObj == null)
                    return new List<string>();

                var list = requiredObj as IList;
                if (list == null)
                    return new List<string>();

                return list.Cast<object>()
                    .Where(item => item != null)
                    .Select(item => item.ToString())
                    .Where(item => !string.IsNullOrEmpty(item))
                    .ToList();
            }

            private static bool IsAdditionalPropertiesFalse(Dictionary<string, object> schema)
            {
                return schema.TryGetValue("additionalProperties", out var value) &&
                       value is bool boolValue &&
                       boolValue == false;
            }

            private static bool MatchesSchemaType(object value, Dictionary<string, object> propertySchema,
                out string error)
            {
                error = null;
                if (!propertySchema.TryGetValue("type", out var typeObj) || typeObj == null)
                    return true;

                var allowedTypes = GetAllowedTypes(typeObj);
                if (allowedTypes.Count == 0)
                    return true;
                if (value == null)
                {
                    if (allowedTypes.Contains("null"))
                        return true;
                    error = $"must be {string.Join(" or ", allowedTypes)}.";
                    return false;
                }

                foreach (string allowedType in allowedTypes)
                {
                    if (MatchesType(value, allowedType))
                        return true;
                }

                error = $"must be {string.Join(" or ", allowedTypes)}.";
                return false;
            }

            private static List<string> GetAllowedTypes(object typeObj)
            {
                if (typeObj is string typeString)
                    return new List<string> { typeString };

                var list = typeObj as IList;
                if (list == null)
                    return new List<string>();

                return list.Cast<object>()
                    .Where(item => item != null)
                    .Select(item => item.ToString())
                    .Where(item => !string.IsNullOrEmpty(item))
                    .ToList();
            }

            private static bool MatchesType(object value, string type)
            {
                switch (type)
                {
                    case "string":
                        return value is string;
                    case "number":
                        return IsNumber(value);
                    case "integer":
                        return value is byte || value is sbyte || value is short || value is ushort ||
                               value is int || value is uint || value is long || value is ulong;
                    case "boolean":
                        return value is bool;
                    case "object":
                        return value is IDictionary;
                    case "array":
                        return value is IList && !(value is string);
                    case "null":
                        return value == null;
                    default:
                        return true;
                }
            }

            private static bool IsNumber(object value)
            {
                return value is byte || value is sbyte || value is short || value is ushort ||
                       value is int || value is uint || value is long || value is ulong ||
                        value is float || value is double || value is decimal;
            }

            private static bool MatchesPattern(string value, string pattern, out bool timedOut)
            {
                timedOut = false;
                try
                {
                    return Regex.IsMatch(value, pattern, RegexOptions.None,
                        TimeSpan.FromMilliseconds(100));
                }
                catch (RegexMatchTimeoutException)
                {
                    timedOut = true;
                    return false;
                }
            }

            private static bool TryGetDouble(Dictionary<string, object> dictionary, string key,
                out double value)
            {
                value = 0;
                return dictionary != null && dictionary.TryGetValue(key, out object raw) &&
                       raw != null && double.TryParse(raw.ToString(),
                           System.Globalization.NumberStyles.Float,
                           System.Globalization.CultureInfo.InvariantCulture, out value);
            }

            private static bool ValuesEqual(object left, object right)
            {
                if (ReferenceEquals(left, right))
                    return true;
                if (left == null || right == null)
                    return false;
                if (IsNumber(left) && IsNumber(right))
                    return Math.Abs(Convert.ToDouble(left) - Convert.ToDouble(right)) < 0.0000001;
                if (left is string leftString && right is string rightString)
                    return string.Equals(leftString, rightString, StringComparison.Ordinal);
                if (left is bool leftBoolean && right is bool rightBoolean)
                    return leftBoolean == rightBoolean;
                if (left is IDictionary leftDictionary && right is IDictionary rightDictionary)
                {
                    if (leftDictionary.Count != rightDictionary.Count)
                        return false;
                    foreach (DictionaryEntry pair in leftDictionary)
                    {
                        if (!rightDictionary.Contains(pair.Key) ||
                            !ValuesEqual(pair.Value, rightDictionary[pair.Key]))
                            return false;
                    }
                    return true;
                }
                if (left is IList leftList && right is IList rightList &&
                    !(left is string) && !(right is string))
                {
                    if (leftList.Count != rightList.Count)
                        return false;
                    for (int index = 0; index < leftList.Count; index++)
                    {
                        if (!ValuesEqual(leftList[index], rightList[index]))
                            return false;
                    }
                    return true;
                }
                return left.Equals(right);
            }

            internal static string CombineValidationErrors(string first, string second)
            {
                if (string.IsNullOrEmpty(first))
                    return second;

                if (string.IsNullOrEmpty(second))
                    return first;

                return first + " " + second;
            }
        }
    }
}
