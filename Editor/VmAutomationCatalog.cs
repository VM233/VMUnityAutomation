using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace VMUnityAutomation.Editor
{
    public static class VmAutomationCatalog
    {
        private static List<string> _cachedRoutes;
        private static List<Dictionary<string, object>> _cachedTools;
        private static string _cachedCatalogRevision;

        public static string CatalogRevision
        {
            get
            {
                EnsureToolMetadataCache();
                if (_cachedCatalogRevision != null)
                    return _cachedCatalogRevision;
                byte[] payload = Encoding.UTF8.GetBytes(MiniJson.Serialize(_cachedTools));
                using (SHA256 sha256 = SHA256.Create())
                {
                    _cachedCatalogRevision = BitConverter.ToString(
                            sha256.ComputeHash(payload))
                        .Replace("-", "")
                        .ToLowerInvariant();
                }
                return _cachedCatalogRevision;
            }
        }

        public static int Count
        {
            get
            {
                EnsureToolMetadataCache();
                return _cachedTools.Count;
            }
        }

        private static string ExtractCategory(string path)
        {
            int slash = path.IndexOf('/');
            return slash > 0 ? path.Substring(0, slash) : path;
        }

        public static object GetRegisteredTools(bool compact = true,
            bool includeSchema = false, int offset = 0, int limit = 10,
            string category = null, string queryText = null, string tag = null,
            string sideEffect = null,
            bool includeMetadataIssues = false)
        {
            EnsureToolMetadataCache();
            IEnumerable<Dictionary<string, object>> query = _cachedTools;
            if (!string.IsNullOrEmpty(category))
            {
                query = query.Where(tool => string.Equals(
                    tool.TryGetValue("category", out var value) ? value?.ToString() : "",
                    category, StringComparison.OrdinalIgnoreCase));
            }

            if (!string.IsNullOrWhiteSpace(queryText))
                query = query.Where(tool => MatchesQuery(tool, queryText));
            if (!string.IsNullOrWhiteSpace(tag))
                query = query.Where(tool => ContainsString(tool, "tags", tag));
            if (!string.IsNullOrWhiteSpace(sideEffect))
                query = query.Where(tool => ContainsString(tool, "sideEffects", sideEffect));

            var tools = query.ToList();
            offset = Math.Max(0, offset);
            limit = Math.Max(1, Math.Min(limit, 50));
            var page = tools.Skip(offset).Take(limit).ToList();
            int nextOffset = offset + page.Count;
            var result = new Dictionary<string, object>
            {
                { "schemaVersion", VmAutomationContractMetadata.ToolMetadataSchemaVersion },
                { "catalogRevision", CatalogRevision },
                { "totalTools", tools.Count },
                { "offset", offset },
                { "limit", limit },
                { "count", page.Count },
            };
            foreach (KeyValuePair<string, object> identity in VmAutomationIdentity.ToDictionary())
                result[identity.Key] = identity.Value;
            if (!string.IsNullOrEmpty(category))
                result["category"] = category;
            if (nextOffset < tools.Count)
                result["nextOffset"] = nextOffset;

            if (compact)
            {
                result["tools"] = page.Select(tool => ToCompactToolDescriptor(tool, includeSchema)).ToList();
                return result;
            }

            result["metadataSource"] = "VmAutomationToolProfileCatalog";
            result["tools"] = page.Select(tool => ToDetailedToolDescriptor(tool, includeSchema)).ToList();
            if (includeMetadataIssues)
                result["metadataIssues"] = BuildMetadataIssues(page);
            return result;
        }

        internal static object GetRegisteredTools(Dictionary<string, object> arguments)
        {
            arguments = arguments ?? new Dictionary<string, object>();
            bool compact = !arguments.TryGetValue("compact", out object value) ||
                           value == null || Convert.ToBoolean(value);
            bool includeSchema = arguments.TryGetValue("includeSchema", out value) &&
                                 value != null && Convert.ToBoolean(value);
            bool includeMetadataIssues =
                arguments.TryGetValue("includeMetadataIssues", out value) &&
                value != null && Convert.ToBoolean(value);
            int offset = arguments.TryGetValue("offset", out value) && value != null
                ? Convert.ToInt32(value)
                : 0;
            int limit = arguments.TryGetValue("limit", out value) && value != null
                ? Convert.ToInt32(value)
                : 50;
            string category = arguments.TryGetValue("category", out value)
                ? value?.ToString()
                : null;
            string queryText = arguments.TryGetValue("query", out value)
                ? value?.ToString()
                : null;
            string tag = arguments.TryGetValue("tag", out value)
                ? value?.ToString()
                : null;
            string sideEffect = arguments.TryGetValue("sideEffect", out value)
                ? value?.ToString()
                : null;
            return GetRegisteredTools(compact, includeSchema, offset, limit, category,
                queryText, tag, sideEffect,
                includeMetadataIssues);
        }

        public static bool TryGetTool(
            string identifier,
            bool includeSchema,
            out Dictionary<string, object> tool)
        {
            tool = null;
            if (string.IsNullOrWhiteSpace(identifier))
                return false;

            EnsureToolMetadataCache();
            List<Dictionary<string, object>> matches = _cachedTools
                .Where(candidate => MatchesIdentifier(candidate, identifier))
                .ToList();
            if (matches.Count == 0)
                return false;
            if (matches.Count > 1)
            {
                throw new InvalidOperationException(
                    $"Automation identifier '{identifier}' resolves to more than one command.");
            }

            tool = ToDetailedToolDescriptor(matches[0], includeSchema);
            return true;
        }

        public static bool TryResolveRoute(string identifier, out string route)
        {
            route = null;
            if (!TryGetTool(identifier, false, out Dictionary<string, object> tool))
                return false;

            route = tool["route"].ToString();
            return !string.IsNullOrEmpty(route);
        }

        private static Dictionary<string, object> ToCompactToolDescriptor(Dictionary<string, object> tool,
            bool includeSchema)
        {
            var descriptor = new Dictionary<string, object>
            {
                { "route", tool["route"] },
                { "toolName", tool["toolName"] },
                { "category", tool["category"] },
                { "moduleId", tool["moduleId"] },
                { "capability", tool["capability"] },
                { "operationKind", tool["operationKind"] },
                { "description", tool["description"] },
            };
            foreach (string key in new[]
                     {
                         "whenToUse", "notFor", "completionEvidence", "cleanupToolName",
                     })
            {
                if (tool.TryGetValue(key, out object value))
                    VmAutomationContractMetadata.AddOptionalString(descriptor, key, value?.ToString());
            }
            foreach (string key in new[]
                     {
                         "aliases", "searchTerms", "preconditions", "errorCodes",
                     })
            {
                if (tool.TryGetValue(key, out object value))
                    VmAutomationContractMetadata.AddOptionalList(descriptor, key,
                        value as System.Collections.IEnumerable);
            }
            if (tool.TryGetValue("tags", out object tags))
                VmAutomationContractMetadata.SetTags(descriptor, tags as IEnumerable<string>);
            if (tool.TryGetValue("sideEffects", out object sideEffects))
                VmAutomationContractMetadata.AddOptionalList(descriptor, "sideEffects", sideEffects as System.Collections.IEnumerable);
            if (tool.TryGetValue("transaction", out object transaction))
                descriptor["transaction"] = transaction;
            if (tool.ContainsKey("projectToolName") &&
                tool.TryGetValue("errorCodes", out object errorCodes))
                VmAutomationContractMetadata.AddOptionalList(descriptor, "errorCodes", errorCodes as System.Collections.IEnumerable);
            if (tool.TryGetValue("annotations", out object annotations) &&
                annotations is IDictionary<string, object> annotationDictionary &&
                annotationDictionary.Count > 0)
                descriptor["annotations"] = annotations;
            if (includeSchema)
            {
                descriptor["inputSchema"] = tool["inputSchema"];
                descriptor["outputSchema"] = tool["outputSchema"];
            }
            if (tool.TryGetValue("projectToolName", out var projectToolName))
                descriptor["projectToolName"] = projectToolName;
            return descriptor;
        }

        private static Dictionary<string, object> ToDetailedToolDescriptor(Dictionary<string, object> tool,
            bool includeSchema)
        {
            var descriptor = tool.ToDictionary(pair => pair.Key, pair => pair.Value);
            if (!includeSchema)
            {
                descriptor.Remove("inputSchema");
                descriptor.Remove("outputSchema");
            }
            return descriptor;
        }

        private static bool MatchesIdentifier(
            IReadOnlyDictionary<string, object> tool,
            string identifier)
        {
            foreach (string key in new[] { "route", "toolName", "projectToolName" })
            {
                if (tool.TryGetValue(key, out object value) &&
                    string.Equals(value?.ToString(), identifier,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool MatchesQuery(
            IReadOnlyDictionary<string, object> tool,
            string query)
        {
            foreach (string key in new[]
                     {
                         "route", "toolName", "projectToolName", "category",
                         "moduleId", "capability", "description", "whenToUse"
                     })
            {
                if (tool.TryGetValue(key, out object value) &&
                    value != null &&
                    value.ToString().IndexOf(query,
                        StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return ContainsSubstring(tool, "aliases", query) ||
                   ContainsSubstring(tool, "searchTerms", query) ||
                   ContainsSubstring(tool, "tags", query);
        }

        private static bool ContainsString(
            IReadOnlyDictionary<string, object> tool,
            string key,
            string expected)
        {
            if (!tool.TryGetValue(key, out object value) ||
                !(value is IEnumerable values))
            {
                return false;
            }

            foreach (object item in values)
            {
                if (string.Equals(item?.ToString(), expected,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsSubstring(
            IReadOnlyDictionary<string, object> tool,
            string key,
            string expected)
        {
            if (!tool.TryGetValue(key, out object value) ||
                !(value is IEnumerable values))
            {
                return false;
            }

            foreach (object item in values)
            {
                if (item != null &&
                    item.ToString().IndexOf(expected,
                        StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
            }

            return false;
        }

        private static void EnsureToolMetadataCache()
        {
            EnsureRouteCache();
            if (_cachedTools != null)
                return;

            _cachedTools = _cachedRoutes.Select(BuildToolMetadata).ToList();
        }

        private static void EnsureRouteCache()
        {
            if (_cachedRoutes == null)
                _cachedRoutes = GetRegisteredRouteList();
        }

        private static List<string> GetRegisteredRouteList()
        {
            var routes = VmAutomationBuiltInRouteDescriptorRegistry.Routes.ToList();
            routes.AddRange(VmProjectToolRegistry.GetDirectRoutePaths());
            return routes
                .Where(route => !string.IsNullOrEmpty(route))
                .Where(VmAutomationCapabilityRegistry.IsRouteAvailable)
                .Distinct()
                .OrderBy(route => route)
                .ToList();
        }

        private static Dictionary<string, object> BuildToolMetadata(string route)
        {
            if (VmProjectToolRegistry.TryGetToolDetailForDirectRoute(route, out var projectTool))
                return BuildProjectToolMetadata(route, projectTool);

            if (!VmAutomationBuiltInRouteDescriptorRegistry.TryGet(route, out var descriptor))
                throw new InvalidOperationException(
                    $"Executable built-in route '{route}' has no typed descriptor.");

            string toolName = RouteToToolName(route);
            string description = descriptor.Description;
            VmAutomationToolProfile profile = descriptor.Profile;
            Dictionary<string, object> inputSchema = AddTargetBindingSchema(
                descriptor.InputSchema, !profile.ReadOnly);
            VmAutomationToolConfigurationPolicy.AnnotateInputSchema(route, inputSchema);
            var metadata = new Dictionary<string, object>
            {
                { "route", route },
                { "toolName", toolName },
                { "category", ExtractCategory(route) },
                { "moduleId", "unity." + ExtractCategory(route) },
                { "capability", VmAutomationCapabilityRegistry.GetCapabilityName(route) },
                { "operationKind", ResolveOperationKind(profile) },
                { "whenToUse", description },
                { "searchTerms", BuildSearchTerms(route, toolName) },
                { "description", description },
                { "inputSchema", inputSchema },
                { "outputSchema", descriptor.OutputSchema },
                { "errorCodes", GetStandardErrorCodes(route) },
            };
            VmAutomationContractMetadata.SetTags(metadata, VmAutomationContractMetadata.BuildToolTags(
                readOnly: profile.ReadOnly,
                dangerous: profile.Dangerous,
                longRunning: profile.LongRunning,
                requiresPlayMode: profile.RequiresPlayMode));
            VmAutomationContractMetadata.AddOptionalList(metadata, "sideEffects",
                VmAutomationContractMetadata.BuildSideEffects(
                    null,
                    readOnly: profile.ReadOnly,
                    mutatesAssets: profile.MutatesAssets,
                    mutatesRuntime: profile.MutatesRuntime,
                     mayReloadDomain: profile.MayReloadDomain));
            var preconditions = new List<string>();
            if (profile.RequiresPlayMode)
                preconditions.Add("playMode");
            if (profile.RequiresEditMode)
                preconditions.Add("stableEditMode");
            if (preconditions.Count > 0)
                metadata["preconditions"] = preconditions;
            Dictionary<string, object> annotations = profile.ToAnnotations();
            if (annotations.Count > 0)
                metadata["annotations"] = annotations;
            if (profile.Transaction != null)
                metadata["transaction"] = profile.Transaction.ToDictionary();
            return metadata;
        }

        private static Dictionary<string, object> BuildProjectToolMetadata(string route,
            Dictionary<string, object> projectTool)
        {
            string projectToolName = projectTool["toolName"].ToString();
            string description = projectTool["description"].ToString();
            var inputSchema = (Dictionary<string, object>)projectTool["inputSchema"];

            string shortName = projectTool.TryGetValue("shortName", out object shortNameValue)
                ? shortNameValue.ToString()
                : "";
            string toolName = ProjectToolNameToToolName(projectToolName, shortName);
            var tags = VmAutomationContractMetadata.ReadTags(projectTool);
            var sideEffectValues = projectTool.TryGetValue("sideEffects", out object declaredSideEffects)
                ? declaredSideEffects as System.Collections.IEnumerable
                : null;
            var sideEffects = VmAutomationContractMetadata.BuildSideEffects(sideEffectValues);
            bool readOnly = tags.Contains(VmAutomationContractMetadata.Tag.ReadOnly, StringComparer.Ordinal);
            bool mutatesAssets =
                sideEffects.Contains("writesAssets", StringComparer.Ordinal) ||
                sideEffects.Contains("writesScene", StringComparer.Ordinal);
            bool mutatesRuntime = sideEffects.Contains("changesRuntimeState", StringComparer.Ordinal);
            bool dangerous = tags.Contains(VmAutomationContractMetadata.Tag.Dangerous, StringComparer.Ordinal);
            bool longRunning = tags.Contains(VmAutomationContractMetadata.Tag.LongRunning, StringComparer.Ordinal);
            bool mayReloadDomain = sideEffects.Contains("reloadsDomain", StringComparer.Ordinal);
            bool requiresPlayMode =
                tags.Contains(VmAutomationContractMetadata.Tag.RequiresPlayMode, StringComparer.Ordinal);
            var profile = new VmAutomationToolProfile
            {
                ReadOnly = readOnly,
                MutatesAssets = mutatesAssets,
                MutatesRuntime = mutatesRuntime,
                Dangerous = dangerous,
                LongRunning = longRunning,
                MayReloadDomain = mayReloadDomain,
                RequiresPlayMode = requiresPlayMode,
            };
            inputSchema = AddTargetBindingSchema(inputSchema, !profile.ReadOnly);
            inputSchema = AddProjectToolExecutionSchema(inputSchema);
            var businessOutputSchema =
                (Dictionary<string, object>)projectTool["outputSchema"];
            var outputSchema = ComposeProjectToolOutputSchema(
                businessOutputSchema, GetPersistentJobOutputSchema());

            var metadata = new Dictionary<string, object>
            {
                { "route", route },
                { "toolName", toolName },
                { "category", "project-tools" },
                { "moduleId", projectTool["moduleId"].ToString() },
                { "capability", projectTool["capability"].ToString() },
                { "operationKind", projectTool["operationKind"].ToString() },
                { "description", description },
                { "inputSchema", inputSchema },
                { "outputSchema", outputSchema },
                { "projectToolName", projectToolName },
            };
            CopyOptionalString(projectTool, metadata, "package");
            VmAutomationContractMetadata.SetTags(metadata, tags);
            VmAutomationContractMetadata.AddOptionalList(metadata, "sideEffects", sideEffects);
            if (projectTool.TryGetValue("errorCodes", out object errorCodes))
                VmAutomationContractMetadata.AddOptionalList(metadata, "errorCodes",
                    errorCodes as System.Collections.IEnumerable);
            Dictionary<string, object> annotations = profile.ToAnnotations();
            if (annotations.Count > 0)
                metadata["annotations"] = annotations;
            if (projectTool.TryGetValue("cleanupToolName", out object cleanupToolName))
                VmAutomationContractMetadata.AddOptionalString(metadata, "cleanupToolName", cleanupToolName?.ToString());
            if (projectTool.TryGetValue("transaction", out object transaction))
                metadata["transaction"] = transaction;
            if (projectTool.TryGetValue("source", out var source))
                VmAutomationContractMetadata.AddOptionalString(metadata, "source", source?.ToString());
            CopyOptionalString(projectTool, metadata, "whenToUse");
            CopyOptionalString(projectTool, metadata, "notFor");
            CopyOptionalString(projectTool, metadata, "completionEvidence");
            CopyOptionalList(projectTool, metadata, "aliases");
            CopyOptionalList(projectTool, metadata, "searchTerms");
            CopyOptionalList(projectTool, metadata, "preconditions");
            return metadata;
        }

        internal static Dictionary<string, object> ComposeProjectToolOutputSchema(
            Dictionary<string, object> businessSchema,
            Dictionary<string, object> jobSchema)
        {
            var definitions = new Dictionary<string, object>(StringComparer.Ordinal);
            Dictionary<string, object> businessBranch =
                HoistDefinitions(businessSchema, definitions, "business");
            Dictionary<string, object> jobBranch =
                HoistDefinitions(jobSchema, definitions, "job");
            var result = new Dictionary<string, object>
            {
                { "oneOf", new List<object> { businessBranch, jobBranch } },
            };
            if (definitions.Count > 0)
                result["$defs"] = definitions;
            return result;
        }

        private static Dictionary<string, object> HoistDefinitions(
            Dictionary<string, object> schema,
            IDictionary<string, object> destination,
            string branchName)
        {
            if (schema == null)
                throw new InvalidOperationException(
                    $"Project tool {branchName} output schema is missing.");
            var branch = new Dictionary<string, object>(schema);
            if (!branch.TryGetValue("$defs", out object definitionsValue))
                return branch;
            if (!(definitionsValue is Dictionary<string, object> definitions))
                throw new InvalidOperationException(
                    $"Project tool {branchName} output schema has invalid $defs.");
            branch.Remove("$defs");
            foreach (KeyValuePair<string, object> definition in definitions)
            {
                if (destination.TryGetValue(definition.Key, out object existingDefinition))
                {
                    if (SchemaNodeEquals(existingDefinition, definition.Value))
                        continue;
                    throw new InvalidOperationException(
                        $"Project tool output schemas declare conflicting local definition " +
                        $"'{definition.Key}'.");
                }
                destination.Add(definition.Key, definition.Value);
            }
            return branch;
        }

        private static bool SchemaNodeEquals(object left, object right)
        {
            if (ReferenceEquals(left, right))
                return true;
            if (left == null || right == null)
                return false;
            if (left is IDictionary<string, object> leftDictionary &&
                right is IDictionary<string, object> rightDictionary)
            {
                if (leftDictionary.Count != rightDictionary.Count)
                    return false;
                foreach (KeyValuePair<string, object> pair in leftDictionary)
                {
                    if (!rightDictionary.TryGetValue(pair.Key, out object rightValue) ||
                        !SchemaNodeEquals(pair.Value, rightValue))
                        return false;
                }
                return true;
            }
            if (left is IEnumerable leftSequence && !(left is string) &&
                right is IEnumerable rightSequence && !(right is string))
            {
                IEnumerator leftEnumerator = leftSequence.GetEnumerator();
                IEnumerator rightEnumerator = rightSequence.GetEnumerator();
                while (true)
                {
                    bool leftHasValue = leftEnumerator.MoveNext();
                    bool rightHasValue = rightEnumerator.MoveNext();
                    if (leftHasValue != rightHasValue)
                        return false;
                    if (!leftHasValue)
                        return true;
                    if (!SchemaNodeEquals(leftEnumerator.Current, rightEnumerator.Current))
                        return false;
                }
            }
            return Equals(left, right);
        }

        private static void CopyOptionalString(Dictionary<string, object> source,
            Dictionary<string, object> destination, string key)
        {
            if (source.TryGetValue(key, out object value))
                VmAutomationContractMetadata.AddOptionalString(destination, key, value?.ToString());
        }

        private static void CopyOptionalList(Dictionary<string, object> source,
            Dictionary<string, object> destination, string key)
        {
            if (source.TryGetValue(key, out object value))
                VmAutomationContractMetadata.AddOptionalList(destination, key,
                    value as System.Collections.IEnumerable);
        }

        private static string ResolveOperationKind(VmAutomationToolProfile profile)
        {
            if (profile.LongRunning)
                return "job";
            if (profile.ReadOnly)
                return "inspect";
            return "mutate";
        }

        private static List<string> BuildSearchTerms(string route, string toolName)
        {
            return new[] { route, toolName }
                .Where(value => string.IsNullOrWhiteSpace(value) == false)
                .SelectMany(value => value.Split(new[] { '/', '-', '_', '.' },
                    StringSplitOptions.RemoveEmptyEntries))
                .Select(value => value.ToLowerInvariant())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToList();
        }

        private static Dictionary<string, object> GetPersistentJobOutputSchema()
        {
            if (VmAutomationGeneratedRouteContracts.TryGetOutput("jobs/get", out var schema))
                return schema;
            throw new InvalidOperationException(
                "The canonical persistent Job snapshot contract is not registered.");
        }

        private static List<string> GetStandardErrorCodes(string route)
        {
            var codes = new List<string>
            {
                "invalid_arguments",
                "target_project_required",
                "wrong_unity_project",
                "tool_execution_failed",
                "response_too_large",
            };
            if (route == "editor/execute-code" ||
                route != null && route.StartsWith(VmProjectToolRegistry.DirectRoutePrefix,
                    StringComparison.Ordinal))
            {
                codes.Add("idempotency_conflict");
            }
            if (route == "editor/play-mode")
            {
                codes.AddRange(new[]
                {
                    "invalid_play_mode_action",
                    "play_mode_required",
                    "play_mode_state_timeout",
                    "play_mode_step_timeout",
                });
            }
            if (route == "editor/play-mode-options")
            {
                codes.AddRange(new[]
                {
                    "edit_mode_required",
                    "play_mode_options_update_failed",
                });
            }
            if (route == "packages/add" ||
                route == "packages/remove" ||
                route == "packages/resolve" ||
                route == "packages/update-git")
            {
                codes.Add("edit_mode_required");
            }
            if (route != null && route.StartsWith("jobs/", StringComparison.Ordinal))
            {
                codes.AddRange(new[]
                {
                    "job_not_found",
                    "job_owner_mismatch",
                    "job_not_cancellable",
                    "job_not_cleanable",
                    "job_not_terminal",
                    "job_cleanup_token_missing",
                });
            }
            if (route == "asset/transaction")
            {
                codes.AddRange(new[]
                {
                    "invalid_operation",
                    "transaction_preflight_failed",
                    "transaction_snapshot_invalid",
                    "asset_transaction_prepare_interrupted",
                    "asset_transaction_interrupted_during_apply",
                    "asset_transaction_interrupted_before_publish",
                    "asset_transaction_interrupted_during_rollback",
                    "asset_transaction_failed",
                    "transaction_postcondition_failed",
                    "rollback_failed",
                    "outcome_uncertain",
                    "compilation_evidence_incomplete",
                    "compilation_failed",
                    "idempotency_conflict",
                    "job_owner_mismatch",
                    "loaded_scene_asset_mutation",
                });
            }
            if (route == "project-auditor/audit")
            {
                codes.AddRange(new[]
                {
                    "project_auditor_unavailable",
                    "project_auditor_busy",
                    "project_auditor_result_failed",
                });
            }
            if (route == "undo/perform")
                codes.AddRange(new[] { "undo_request_not_available", "undo_request_not_latest" });
            if (route == "undo/redo")
                codes.AddRange(new[] { "undo_request_not_available", "redo_request_not_available" });
            if (route == "undo/clear")
                codes.AddRange(new[] { "confirmation_required", "gameobject_not_found" });
            return codes.Distinct(StringComparer.Ordinal).OrderBy(code => code, StringComparer.Ordinal).ToList();
        }

        private static Dictionary<string, object> AddTargetBindingSchema(
            Dictionary<string, object> inputSchema, bool requiresTargetBinding)
        {
            if (!requiresTargetBinding)
                return inputSchema;

            var schema = inputSchema != null
                ? new Dictionary<string, object>(inputSchema)
                : new Dictionary<string, object> { { "type", "object" } };
            var properties = schema.TryGetValue("properties", out object propertiesValue) &&
                             propertiesValue is Dictionary<string, object> existingProperties
                ? new Dictionary<string, object>(existingProperties)
                : new Dictionary<string, object>();
            if (!properties.ContainsKey("expectedProjectPath"))
            {
                KeyValuePair<string, object> bindingProperty = VmAutomationToolSchemaFactory.Prop("expectedProjectPath", "string",
                    "Expected Unity project root; rejects cross-project mutation.");
                properties[bindingProperty.Key] = bindingProperty.Value;
            }
            schema["properties"] = properties;

            var required = schema.TryGetValue("required", out object requiredValue) &&
                           requiredValue is IEnumerable existingRequired
                ? existingRequired.Cast<object>()
                    .Select(value => Convert.ToString(value))
                    .Where(value => !string.IsNullOrEmpty(value))
                    .Distinct(StringComparer.Ordinal)
                    .ToList()
                : new List<string>();
            if (!required.Contains("expectedProjectPath", StringComparer.Ordinal))
                required.Add("expectedProjectPath");
            schema["required"] = required;
            return schema;
        }

        private static Dictionary<string, object> AddProjectToolExecutionSchema(
            Dictionary<string, object> inputSchema)
        {
            var schema = inputSchema != null
                ? new Dictionary<string, object>(inputSchema)
                : new Dictionary<string, object> { { "type", "object" } };
            var properties = schema.TryGetValue("properties", out object propertiesValue) &&
                             propertiesValue is Dictionary<string, object> existingProperties
                ? new Dictionary<string, object>(existingProperties)
                : new Dictionary<string, object>();
            if (!properties.ContainsKey("runAsJob"))
            {
                KeyValuePair<string, object> property = VmAutomationToolSchemaFactory.Prop("runAsJob", "boolean",
                    "Run this invocation through the persistent project-tool job owner. Long-running tools always do this.");
                properties[property.Key] = property.Value;
            }
            if (!properties.ContainsKey("idempotencyKey"))
            {
                KeyValuePair<string, object> property = VmAutomationToolSchemaFactory.Prop("idempotencyKey", "string",
                    "Optional project-scoped key used to reuse an existing persistent invocation.");
                properties[property.Key] = property.Value;
            }
            schema["properties"] = properties;
            return schema;
        }

        private static VmAutomationToolProfile GetToolProfile(string route)
        {
            if (VmAutomationBuiltInRouteDescriptorRegistry.TryGet(route, out var descriptor))
                return descriptor.Profile;
            // Main-thread context and HTTP queue control routes are not advertised tools,
            // but they still have an explicit effect profile for target-binding policy.
            return VmAutomationToolProfileCatalog.Get(route);
        }

        internal static bool IsRouteReadOnly(string route)
        {
            if (VmProjectToolRegistry.TryGetToolDetailForDirectRoute(route, out var projectTool))
                return VmAutomationContractMetadata.HasTag(projectTool, VmAutomationContractMetadata.Tag.ReadOnly);
            return GetToolProfile(route).ReadOnly;
        }

        internal static bool RouteMutatesAssets(string route)
        {
            if (VmProjectToolRegistry.TryGetToolDetailForDirectRoute(route, out var projectTool))
            {
                var sideEffects = projectTool.TryGetValue("sideEffects", out object value)
                    ? value as System.Collections.IEnumerable
                    : null;
                return VmAutomationContractMetadata.HasString(sideEffects, "writesAssets") ||
                       VmAutomationContractMetadata.HasString(sideEffects, "writesScene");
            }
            return GetToolProfile(route).MutatesAssets;
        }

        internal static bool RouteMutatesRuntime(string route)
        {
            if (VmProjectToolRegistry.TryGetToolDetailForDirectRoute(route, out var projectTool))
            {
                var sideEffects = projectTool.TryGetValue("sideEffects", out object value)
                    ? value as System.Collections.IEnumerable
                    : null;
                return VmAutomationContractMetadata.HasString(sideEffects, "changesRuntimeState");
            }
            return GetToolProfile(route).MutatesRuntime;
        }

        internal static bool RouteIsDangerous(string route)
        {
            if (VmProjectToolRegistry.TryGetToolDetailForDirectRoute(route, out var projectTool))
                return VmAutomationContractMetadata.HasTag(projectTool, VmAutomationContractMetadata.Tag.Dangerous);
            return GetToolProfile(route).Dangerous;
        }

        internal static bool RouteRequiresTargetBinding(string route)
        {
            return !IsRouteReadOnly(route);
        }

        internal static bool RouteMayReloadDomain(string route)
        {
            if (VmProjectToolRegistry.TryGetToolDetailForDirectRoute(route, out var projectTool))
            {
                var sideEffects = projectTool.TryGetValue("sideEffects", out object value)
                    ? value as System.Collections.IEnumerable
                    : null;
                return VmAutomationContractMetadata.HasString(sideEffects, "reloadsDomain");
            }
            return GetToolProfile(route).MayReloadDomain;
        }

        internal static bool RouteIsLongRunning(string route)
        {
            if (VmProjectToolRegistry.TryGetToolDetailForDirectRoute(route, out var projectTool))
                return VmAutomationContractMetadata.HasTag(projectTool, VmAutomationContractMetadata.Tag.LongRunning);
            return GetToolProfile(route).LongRunning;
        }

        internal static bool RouteRequiresPlayMode(string route)
        {
            if (VmProjectToolRegistry.TryGetToolDetailForDirectRoute(route, out var projectTool))
                return VmAutomationContractMetadata.HasTag(
                    projectTool, VmAutomationContractMetadata.Tag.RequiresPlayMode);
            return GetToolProfile(route).RequiresPlayMode;
        }

        private static List<Dictionary<string, object>> BuildMetadataIssues(List<Dictionary<string, object>> tools)
        {
            var issues = new List<Dictionary<string, object>>();
            foreach (var tool in tools)
            {
                string route = tool.TryGetValue("route", out var routeObj) ? routeObj?.ToString() : "";
                string description = tool.TryGetValue("description", out var descObj) ? descObj?.ToString() : "";
                if (description.StartsWith(
                        "Execute the canonical VM Unity Automation tool registered for route ",
                        StringComparison.Ordinal))
                {
                    issues.Add(new Dictionary<string, object>
                    {
                        { "route", route },
                        { "issue", "default_description" },
                    });
                }

                if (tool.TryGetValue("inputSchema", out var schemaObj) &&
                    schemaObj is Dictionary<string, object> schema)
                {
                    CollectSchemaIssues(route, schema, "$", false, issues);
                }
            }

            return issues;
        }

        private static void CollectSchemaIssues(
            string route,
            Dictionary<string, object> schema,
            string path,
            bool isProperty,
            List<Dictionary<string, object>> issues)
        {
            string type = schema.TryGetValue("type", out var typeObj) ? typeObj?.ToString() : "";
            if (isProperty &&
                (!schema.TryGetValue("description", out var descriptionObj) ||
                 string.IsNullOrWhiteSpace(descriptionObj?.ToString())))
            {
                issues.Add(new Dictionary<string, object>
                {
                    { "route", route },
                    { "issue", "property_without_description" },
                    { "path", path },
                });
            }

            if (string.Equals(type, "array", StringComparison.Ordinal) &&
                !schema.ContainsKey("items"))
            {
                issues.Add(new Dictionary<string, object>
                {
                    { "route", route },
                    { "issue", "array_without_items" },
                    { "path", path },
                });
            }

            if (schema.TryGetValue("properties", out var propertiesObj) &&
                propertiesObj is Dictionary<string, object> properties)
            {
                foreach (var property in properties)
                {
                    if (property.Value is Dictionary<string, object> propertySchema)
                        CollectSchemaIssues(route, propertySchema, path + "." + property.Key, true, issues);
                }
            }

            if (schema.TryGetValue("items", out var itemsObj) &&
                itemsObj is Dictionary<string, object> itemsSchema)
            {
                CollectSchemaIssues(route, itemsSchema, path + "[]", false, issues);
            }

            foreach (string keyword in new[] { "allOf", "anyOf", "oneOf" })
            {
                if (!schema.TryGetValue(keyword, out var variantsObj) ||
                    !(variantsObj is IEnumerable<object> variants))
                {
                    continue;
                }

                int index = 0;
                foreach (object variant in variants)
                {
                    if (variant is Dictionary<string, object> variantSchema)
                        CollectSchemaIssues(route, variantSchema, path + "." + keyword + "[" + index + "]", false, issues);
                    index++;
                }
            }
        }

        private static string RouteToToolName(string route)
        {
            return "vm_auto_" + route.Replace("/", "_").Replace("-", "_");
        }

        internal static string ProjectToolNameToToolName(string projectToolName, string shortName = "")
        {
            var normalized = NormalizeProjectToolName(string.IsNullOrEmpty(shortName) ? projectToolName : shortName);

            if (string.IsNullOrEmpty(normalized))
                normalized = "tool";

            var tokens = normalized.Split(new[] { '_' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(CompactProjectToolToken)
                .ToArray();
            string compact = "vm_pt_" + string.Join("_", tokens);
            const int maxLength = 48;
            if (compact.Length <= maxLength)
                return compact;

            string hash = ComputeStableNameHash(normalized);
            int prefixLength = maxLength - hash.Length - 1;
            return compact.Substring(0, prefixLength).TrimEnd('_') + "_" + hash;
        }

        private static string NormalizeProjectToolName(string projectToolName)
        {
            return Regex.Replace(projectToolName ?? "", "[^A-Za-z0-9]+", "_")
                .Trim('_')
                .ToLowerInvariant();
        }

        private static string CompactProjectToolToken(string token)
        {
            switch (token)
            {
                case "vmframework": return "vmf";
                case "battleidle": return "battle";
                case "visual": return "ui";
                case "element": return "el";
                case "elements": return "els";
                case "property": return "prop";
                case "properties": return "props";
                case "configuration": return "config";
                case "configurations": return "configs";
                case "wrapper": return "wrap";
                case "wrappers": return "wraps";
                default: return token;
            }
        }

        private static string ComputeStableNameHash(string value)
        {
            unchecked
            {
                uint hash = 2166136261;
                foreach (char character in value)
                {
                    hash ^= character;
                    hash *= 16777619;
                }

                return hash.ToString("x8");
            }
        }


    }
}
