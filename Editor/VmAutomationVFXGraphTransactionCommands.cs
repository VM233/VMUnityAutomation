using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;

namespace VMUnityAutomation.Editor
{
    internal static class VmAutomationVFXGraphTransactionCommands
    {
        private const int MaxOperations = 256;

        private sealed class ModelIdentitySnapshot
        {
            internal UnityEngine.Object Model;
            internal string Id;
            internal List<int> ParameterNodeIds;
        }

        private static readonly Dictionary<string, string[]> OperationKeys =
            new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                { "add-node", Keys("catalogId", "kind", "parentContextId",
                    "index", "position", "collapsed", "superCollapsed", "enabled",
                    "settings", "slots", "alias") },
                { "remove-node", Keys("nodeId", "parameterNodeId") },
                { "set-node", Keys("nodeId", "position", "collapsed",
                    "superCollapsed", "enabled", "name", "settings") },
                { "set-slot", Keys("nodeId", "direction", "slotPath", "value",
                    "space", "collapsed") },
                { "connect-data", Keys("fromNodeId", "fromSlot", "toNodeId",
                    "toSlot") },
                { "disconnect-data", Keys("fromNodeId", "fromSlot", "toNodeId",
                    "toSlot") },
                { "connect-flow", Keys("fromContextId", "fromIndex",
                    "toContextId", "toIndex") },
                { "disconnect-flow", Keys("fromContextId", "fromIndex",
                    "toContextId", "toIndex") },
                { "move-block", Keys("nodeId", "parentContextId", "index") },
                { "add-parameter", Keys("catalogId", "name", "value", "exposed",
                    "isOutput", "category", "order", "tooltip", "valueFilter",
                    "min", "max", "enumValues", "position", "collapsed",
                    "superCollapsed", "alias") },
                { "set-parameter", Keys("parameterId", "name", "value", "exposed",
                    "isOutput", "category", "order", "tooltip", "valueFilter",
                    "min", "max", "enumValues", "position", "collapsed",
                    "superCollapsed") },
                { "add-parameter-node", Keys("parameterId", "position", "expanded",
                    "superCollapsed", "alias") },
                { "remove-parameter-node", Keys("parameterNodeId") },
                { "add-category", Keys("name", "collapsed", "index") },
                { "set-category", Keys("categoryName", "categoryIndex", "name",
                    "collapsed") },
                { "remove-category", Keys("categoryName", "categoryIndex",
                    "parameterDisposition") },
                { "move-category", Keys("categoryName", "categoryIndex", "index") },
                { "add-custom-attribute", Keys("name", "valueType", "description",
                    "expanded", "index") },
                { "set-custom-attribute", Keys("attributeName", "name", "valueType",
                    "description", "expanded", "index", "removeUsages") },
                { "remove-custom-attribute", Keys("attributeName", "removeUsages") },
                { "move-custom-attribute", Keys("attributeName", "index") },
                { "add-group", Keys("title", "position", "contents", "index") },
                { "set-group", Keys("groupIndex", "title", "position", "contents") },
                { "remove-group", Keys("groupIndex") },
                { "add-sticky-note", Keys("title", "position", "contents", "theme",
                    "textSize", "colorTheme", "index") },
                { "set-sticky-note", Keys("stickyNoteIndex", "title", "position",
                    "contents", "theme", "textSize", "colorTheme") },
                { "remove-sticky-note", Keys("stickyNoteIndex") },
                { "set-ui-bounds", Keys("bounds") },
                { "set-graph-setting", Keys("name", "value") },
                { "set-asset-setting", Keys("name", "value") },
            };

        internal static object Transaction(Dictionary<string, object> args)
        {
            if (!ValidateKeys(args, new[]
                {
                    "assetPath", "operations", "dryRun", "_agentId",
                }, out object keyError))
                return keyError;
            string assetPath = VmAutomationVFXGraphMutationContext.GetString(args,
                "assetPath");
            if (!VmAutomationVFXAssetPath.TryNormalizeFile(assetPath, false,
                    out assetPath, out string pathError))
                return VmAutomationResponse.Error(pathError, "invalid_arguments");
            List<object> rawOperations = args != null &&
                args.TryGetValue("operations", out object operationsValue)
                    ? VmAutomationVFXGraphMutationContext.AsList(operationsValue)
                    : null;
            if (rawOperations == null || rawOperations.Count == 0)
                return VmAutomationResponse.Error("operations must be a non-empty array.",
                    "invalid_arguments");
            if (rawOperations.Count > MaxOperations)
                return VmAutomationResponse.Error(
                    $"operations contains {rawOperations.Count} entries; the maximum is {MaxOperations}.",
                    "invalid_arguments");
            if (!VmAutomationVFXGraphSession.TryOpen(assetPath,
                    out VmAutomationVFXGraphSession session, out object openError))
                return openError;
            if (!AssetDatabase.IsOpenForEdit(session.Asset,
                    StatusQueryOptions.UseCachedIfPossible))
                return VmAutomationResponse.Error(
                    $"VFX Graph asset '{assetPath}' is not open for edit.",
                    "asset_not_editable");

            List<Dictionary<string, object>> operations;
            try
            {
                operations = rawOperations.Select((raw, index) =>
                    ValidateOperation(raw, index)).ToList();
            }
            catch (Exception exception)
            {
                return VmAutomationResponse.Error(VmAutomationVFXReflection.Unwrap(exception).Message,
                    "invalid_arguments");
            }

            bool dryRun;
            try
            {
                dryRun = VmAutomationVFXGraphMutationContext.GetBool(args, "dryRun", false);
            }
            catch (Exception exception)
            {
                return VmAutomationResponse.Error(VmAutomationVFXReflection.Unwrap(exception).Message,
                    "invalid_arguments");
            }
            object backup = null;
            Dictionary<string, object> assetSettingsBackup = null;
            byte[] originalBytes = null;
            string absolutePath = VmAutomationVFXAssetPath.ToAbsoluteAssetsPath(assetPath);
            string originalHash = "";
            var results = new List<Dictionary<string, object>>();
            var mutation = new VmAutomationVFXGraphMutationContext(session);
            List<ModelIdentitySnapshot> identitySnapshots = null;
            try
            {
                identitySnapshots = CaptureModelIdentities(session);
                backup = session.CaptureGraphBackup();
                assetSettingsBackup = VmAutomationVFXAssetSettings.Capture(session);
                originalBytes = File.ReadAllBytes(absolutePath);
                originalHash = Hash(originalBytes);
                for (int index = 0; index < operations.Count; index++)
                {
                    Dictionary<string, object> operation = operations[index];
                    string op = VmAutomationVFXGraphMutationContext.GetString(operation, "op");
                    Dictionary<string, object> result = Apply(mutation, op,
                        operation);
                    result["index"] = index;
                    result["op"] = op;
                    results.Add(result);
                }

                if (dryRun)
                {
                    session.RestoreGraphBackup(backup);
                    VmAutomationVFXAssetSettings.Restore(session, assetSettingsBackup);
                    return new Dictionary<string, object>
                    {
                        { "success", true }, { "dryRun", true },
                        { "assetPath", assetPath },
                        { "assetKind", session.AssetKind },
                        { "operationCount", operations.Count },
                        { "results", results },
                        { "aliases", mutation.AliasIds() },
                        { "idRemap", new Dictionary<string, object>() },
                        { "deferredChecks", new List<string>
                            {
                                "post-save local file IDs",
                                "importer compilation and shader generation",
                            } },
                    };
                }

                session.WriteAndImport();
                string savedHash = Hash(File.ReadAllBytes(absolutePath));
                Dictionary<string, object> idRemap = BuildIdentityRemap(
                    session, identitySnapshots);
                return new Dictionary<string, object>
                {
                    { "success", true }, { "dryRun", false },
                    { "assetPath", assetPath },
                    { "assetKind", session.AssetKind },
                    { "operationCount", operations.Count },
                    { "results", results },
                    { "aliases", mutation.AliasIds() },
                    { "idRemap", idRemap },
                    { "previousAssetHash", originalHash },
                    { "assetHash", savedHash },
                    { "changed", !string.Equals(originalHash, savedHash,
                        StringComparison.Ordinal) },
                };
            }
            catch (Exception exception)
            {
                Exception failure = VmAutomationVFXReflection.Unwrap(exception);
                try
                {
                    if (backup != null)
                        session.RestoreGraphBackup(backup);
                    if (assetSettingsBackup != null)
                        VmAutomationVFXAssetSettings.Restore(session, assetSettingsBackup);
                    if (originalBytes != null)
                    {
                        File.WriteAllBytes(absolutePath, originalBytes);
                        AssetDatabase.ImportAsset(assetPath,
                            ImportAssetOptions.ForceUpdate |
                            ImportAssetOptions.ForceSynchronousImport);
                        string restoredHash = Hash(File.ReadAllBytes(absolutePath));
                        if (!string.Equals(originalHash, restoredHash,
                                StringComparison.Ordinal))
                            throw new InvalidOperationException(
                                $"Rollback hash '{restoredHash}' did not match original '{originalHash}'.");
                    }
                }
                catch (Exception rollbackException)
                {
                    return VmAutomationResponse.Error(
                        $"VFX transaction failed: {failure.Message}. Rollback failed: {VmAutomationVFXReflection.Unwrap(rollbackException).Message}",
                        "vfx_transaction_rollback_failed");
                }
                return VmAutomationResponse.Error(failure.Message,
                    VmAutomationVFXError.Code(failure, "vfx_transaction_failed"), false,
                    new Dictionary<string, object>
                    {
                        { "assetPath", assetPath },
                        { "failedOperationIndex", results.Count },
                        { "rolledBack", true },
                        { "assetHash", originalHash },
                    });
            }
        }

        private static Dictionary<string, object> Apply(
            VmAutomationVFXGraphMutationContext context, string op,
            Dictionary<string, object> operation)
        {
            if (VmAutomationVFXGraphBlackboardMutations.IsBlackboardOrUIOperation(op))
                return VmAutomationVFXGraphBlackboardMutations.Apply(context, op, operation);
            switch (op)
            {
                case "add-node": return AddNode(context, operation);
                case "remove-node": return RemoveNode(context, operation);
                case "set-node": return SetNode(context, operation);
                case "set-slot": return SetSlot(context, operation);
                case "connect-data": return ConnectData(context, operation, true);
                case "disconnect-data": return ConnectData(context, operation, false);
                case "connect-flow": return ConnectFlow(context, operation, true);
                case "disconnect-flow": return ConnectFlow(context, operation, false);
                case "move-block": return MoveBlock(context, operation);
                case "add-parameter": return AddParameter(context, operation);
                case "set-parameter": return SetParameter(context, operation);
                case "add-parameter-node": return AddParameterNode(context, operation);
                case "remove-parameter-node":
                    context.RemoveParameterNode(RequireString(operation,
                        "parameterNodeId"));
                    return Result("removed", true);
                case "set-graph-setting":
                    string name = RequireString(operation, "name");
                    object value = Required(operation, "value");
                    if (!VmAutomationVFXAssetSettings.TrySetCompilationMode(
                            context.Session, name, value))
                        context.SetSetting(
                            context.Session.Graph as UnityEngine.Object,
                            name, value, "operation.value");
                    return Result("setting", name);
                case "set-asset-setting":
                    return VmAutomationVFXAssetSettings.Set(context.Session,
                        RequireString(operation, "name"),
                        Required(operation, "value"));
                default:
                    throw new ArgumentException(
                        $"Unsupported VFX transaction operation '{op}'.");
            }
        }

        private static Dictionary<string, object> AddNode(
            VmAutomationVFXGraphMutationContext context, Dictionary<string, object> operation)
        {
            string kind = RequireString(operation, "kind").ToLowerInvariant();
            if (kind != "context" && kind != "block" && kind != "operator")
                throw new ArgumentException(
                    "add-node kind must be context, block, or operator.");
            string parent = kind == "block"
                ? RequireString(operation, "parentContextId") : "";
            UnityEngine.Object model = context.AddCatalogModel(kind,
                RequireString(operation, "catalogId"), parent,
                VmAutomationVFXGraphMutationContext.GetInt(operation, "index", -1),
                operation);
            string alias = VmAutomationVFXGraphMutationContext.GetString(operation, "alias");
            context.RegisterAlias(alias, model);
            return new Dictionary<string, object>
            {
                { "alias", alias }, { "kind", kind },
                { "type", model.GetType().FullName },
                { "name", VmAutomationVFXReflection.SemanticName(model) },
            };
        }

        private static Dictionary<string, object> RemoveNode(
            VmAutomationVFXGraphMutationContext context, Dictionary<string, object> operation)
        {
            if (operation.TryGetValue("parameterNodeId", out object occurrence))
            {
                context.RemoveParameterNode(occurrence?.ToString());
                return Result("removedParameterNode", occurrence?.ToString() ?? "");
            }
            UnityEngine.Object model = context.ResolveModel(
                RequireString(operation, "nodeId"));
            context.RemoveModel(model);
            return Result("removedNodeType", model.GetType().FullName);
        }

        private static Dictionary<string, object> SetNode(
            VmAutomationVFXGraphMutationContext context, Dictionary<string, object> operation)
        {
            UnityEngine.Object model = context.ResolveModel(
                RequireString(operation, "nodeId"));
            context.ApplyCommonModelFields(model, operation, "operation");
            if (operation.TryGetValue("name", out object name))
                model.name = name?.ToString() ?? "";
            return new Dictionary<string, object>
            {
                { "nodeType", model.GetType().FullName },
                { "name", VmAutomationVFXReflection.SemanticName(model) },
            };
        }

        private static Dictionary<string, object> SetSlot(
            VmAutomationVFXGraphMutationContext context, Dictionary<string, object> operation)
        {
            UnityEngine.Object model = context.ResolveNodeSelector(
                RequireString(operation, "nodeId"), "nodeId", out int? _);
            bool hasValue = operation.TryGetValue("value", out object value);
            bool hasSpace = operation.TryGetValue("space", out object space);
            bool hasCollapsed = operation.TryGetValue("collapsed",
                out object collapsed);
            if (!hasValue && !hasSpace && !hasCollapsed)
                throw new ArgumentException(
                    "set-slot requires value, space, collapsed, or a combination of them.");
            context.SetSlotValue(model, RequireString(operation, "direction"),
                RequireString(operation, "slotPath"),
                hasValue ? value : System.Reflection.Missing.Value,
                hasSpace ? space : null,
                hasCollapsed ? collapsed : System.Reflection.Missing.Value,
                "operation.value");
            return Result("slotPath", RequireString(operation, "slotPath"));
        }

        private static Dictionary<string, object> ConnectData(
            VmAutomationVFXGraphMutationContext context, Dictionary<string, object> operation,
            bool connect)
        {
            string fromSelector = RequireString(operation, "fromNodeId");
            string toSelector = RequireString(operation, "toNodeId");
            UnityEngine.Object from = context.ResolveNodeSelector(fromSelector,
                "fromNodeId", out int? fromParameterNodeId);
            UnityEngine.Object to = context.ResolveNodeSelector(toSelector,
                "toNodeId", out int? toParameterNodeId);
            string fromSlot = RequireString(operation, "fromSlot");
            string toSlot = RequireString(operation, "toSlot");
            if (connect)
                context.ConnectData(from, fromSlot, to, toSlot,
                    fromParameterNodeId, toParameterNodeId);
            else
                context.DisconnectData(from, fromSlot, to, toSlot);
            return new Dictionary<string, object>
            {
                { "fromNodeId", fromSelector }, { "fromSlot", fromSlot },
                { "toNodeId", toSelector }, { "toSlot", toSlot },
                { "connected", connect },
            };
        }

        private static Dictionary<string, object> ConnectFlow(
            VmAutomationVFXGraphMutationContext context, Dictionary<string, object> operation,
            bool connect)
        {
            UnityEngine.Object from = RequireContext(context,
                RequireString(operation, "fromContextId"));
            UnityEngine.Object to = RequireContext(context,
                RequireString(operation, "toContextId"));
            int fromIndex = VmAutomationVFXGraphMutationContext.GetInt(operation,
                "fromIndex", 0);
            int toIndex = VmAutomationVFXGraphMutationContext.GetInt(operation,
                "toIndex", 0);
            if (fromIndex < 0 || toIndex < 0)
                throw new ArgumentException("Flow indices must be at least 0.");
            if (connect)
                context.ConnectFlow(from, fromIndex, to, toIndex);
            else
                context.DisconnectFlow(from, fromIndex, to, toIndex);
            return new Dictionary<string, object>
            {
                { "fromIndex", fromIndex }, { "toIndex", toIndex },
                { "connected", connect },
            };
        }

        private static Dictionary<string, object> MoveBlock(
            VmAutomationVFXGraphMutationContext context, Dictionary<string, object> operation)
        {
            UnityEngine.Object block = context.ResolveModel(
                RequireString(operation, "nodeId"));
            UnityEngine.Object parent = RequireContext(context,
                RequireString(operation, "parentContextId"));
            int index = VmAutomationVFXGraphMutationContext.GetInt(operation, "index", -1);
            context.MoveBlock(block, parent, index);
            return Result("index", index);
        }

        private static Dictionary<string, object> AddParameter(
            VmAutomationVFXGraphMutationContext context, Dictionary<string, object> operation)
        {
            UnityEngine.Object parameter = context.AddParameter(
                RequireString(operation, "catalogId"), operation);
            string alias = VmAutomationVFXGraphMutationContext.GetString(operation, "alias");
            context.RegisterAlias(alias, parameter);
            return new Dictionary<string, object>
            {
                { "alias", alias },
                { "name", VmAutomationVFXReflection.Get(parameter, "exposedName")?.ToString() ?? "" },
                { "valueType", (VmAutomationVFXReflection.Get(parameter, "type") as Type)?.FullName ?? "" },
            };
        }

        private static Dictionary<string, object> SetParameter(
            VmAutomationVFXGraphMutationContext context, Dictionary<string, object> operation)
        {
            UnityEngine.Object parameter = context.ResolveDefinition(
                RequireString(operation, "parameterId"));
            context.SetParameter(parameter, operation, false);
            return Result("name", VmAutomationVFXReflection.Get(parameter,
                "exposedName")?.ToString() ?? "");
        }

        private static Dictionary<string, object> AddParameterNode(
            VmAutomationVFXGraphMutationContext context, Dictionary<string, object> operation)
        {
            UnityEngine.Object parameter = context.ResolveDefinition(
                RequireString(operation, "parameterId"));
            int nodeId = context.AddParameterNode(parameter,
                VmAutomationVFXGraphMutationContext.GetVector2(operation, "position",
                    Vector2.zero),
                VmAutomationVFXGraphMutationContext.GetBool(operation, "expanded", true),
                VmAutomationVFXGraphMutationContext.GetBool(operation, "superCollapsed",
                    false));
            string alias = VmAutomationVFXGraphMutationContext.GetString(operation, "alias");
            context.RegisterOccurrenceAlias(alias, parameter, nodeId);
            return new Dictionary<string, object>
            {
                { "alias", alias }, { "nodeId", nodeId },
                { "parameterNodeId", VmAutomationVFXReflection.StableId(parameter) + ":" + nodeId },
            };
        }

        private static UnityEngine.Object RequireContext(
            VmAutomationVFXGraphMutationContext context, string selector)
        {
            UnityEngine.Object model = context.ResolveModel(selector);
            if (!VmAutomationVFXReflection.HasBaseType(model.GetType(),
                    VmAutomationVFXReflection.ContextTypeName))
                throw new ArgumentException(
                    $"VFX model '{selector}' is not a context.");
            return model;
        }

        private static Dictionary<string, object> ValidateOperation(object raw,
            int index)
        {
            Dictionary<string, object> operation =
                VmAutomationVFXGraphMutationContext.AsDictionary(raw) ??
                throw new ArgumentException(
                    $"operations[{index}] must be an object.");
            string op = VmAutomationVFXGraphMutationContext.GetString(operation, "op")
                .ToLowerInvariant();
            if (!OperationKeys.TryGetValue(op, out string[] allowed))
                throw new ArgumentException(
                    $"operations[{index}].op '{op}' is not supported.");
            var keySet = new HashSet<string>(allowed.Concat(new[] { "op" }),
                StringComparer.Ordinal);
            string unknown = operation.Keys.FirstOrDefault(key =>
                !keySet.Contains(key));
            if (unknown != null)
                throw new ArgumentException(
                    $"operations[{index}] contains unsupported field '{unknown}' for '{op}'.");
            operation["op"] = op;
            return operation;
        }

        private static bool ValidateKeys(Dictionary<string, object> values,
            IEnumerable<string> allowed, out object error)
        {
            var allowedSet = new HashSet<string>(allowed, StringComparer.Ordinal);
            string unknown = values?.Keys.FirstOrDefault(key =>
                !allowedSet.Contains(key));
            if (unknown == null)
            {
                error = null;
                return true;
            }
            error = VmAutomationResponse.Error($"Unsupported argument '{unknown}'.",
                "invalid_arguments");
            return false;
        }

        private static string[] Keys(params string[] values)
        {
            return values;
        }

        private static string RequireString(Dictionary<string, object> operation,
            string key)
        {
            string value = VmAutomationVFXGraphMutationContext.GetString(operation, key);
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException(key + " is required.");
            return value;
        }

        private static object Required(Dictionary<string, object> operation,
            string key)
        {
            if (!operation.TryGetValue(key, out object value))
                throw new ArgumentException(key + " is required.");
            return value;
        }

        private static Dictionary<string, object> Result(string key, object value)
        {
            return new Dictionary<string, object> { { key, value } };
        }

        private static List<ModelIdentitySnapshot> CaptureModelIdentities(
            VmAutomationVFXGraphSession session)
        {
            Dictionary<UnityEngine.Object, string> ids = session.BuildModelIds();
            return ids.Select(pair => new ModelIdentitySnapshot
            {
                Model = pair.Key,
                Id = pair.Value,
                ParameterNodeIds = VmAutomationVFXReflection.HasBaseType(
                        pair.Key.GetType(), VmAutomationVFXReflection.ParameterTypeName)
                    ? VmAutomationVFXReflection.Enumerate(VmAutomationVFXReflection.Get(pair.Key,
                            "nodes"))
                        .Select(node => Convert.ToInt32(
                            VmAutomationVFXReflection.Get(node, "id") ?? -1))
                        .Where(nodeId => nodeId >= 0).ToList()
                    : new List<int>(),
            }).ToList();
        }

        private static Dictionary<string, object> BuildIdentityRemap(
            VmAutomationVFXGraphSession session,
            IEnumerable<ModelIdentitySnapshot> snapshots)
        {
            var result = new Dictionary<string, object>(StringComparer.Ordinal);
            foreach (ModelIdentitySnapshot snapshot in snapshots)
            {
                if (snapshot.Model == null ||
                    !string.Equals(AssetDatabase.GetAssetPath(snapshot.Model),
                        session.AssetPath, StringComparison.Ordinal))
                    continue;
                string currentId = VmAutomationVFXReflection.StableId(snapshot.Model);
                if (string.IsNullOrEmpty(currentId) ||
                    string.Equals(snapshot.Id, currentId,
                        StringComparison.Ordinal))
                    continue;
                result[snapshot.Id] = currentId;
                if (!VmAutomationVFXReflection.HasBaseType(snapshot.Model.GetType(),
                        VmAutomationVFXReflection.ParameterTypeName))
                    continue;
                var currentNodeIds = new HashSet<int>(VmAutomationVFXReflection.Enumerate(
                        VmAutomationVFXReflection.Get(snapshot.Model, "nodes"))
                    .Select(node => Convert.ToInt32(
                        VmAutomationVFXReflection.Get(node, "id") ?? -1))
                    .Where(nodeId => nodeId >= 0));
                foreach (int nodeId in snapshot.ParameterNodeIds.Where(
                             currentNodeIds.Contains))
                    result[snapshot.Id + ":" + nodeId] =
                        currentId + ":" + nodeId;
            }
            return result;
        }

        private static string Hash(byte[] bytes)
        {
            using (SHA256 sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(bytes ??
                    Array.Empty<byte>())).Replace("-", "").ToLowerInvariant();
        }
    }
}
