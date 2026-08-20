using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace VMUnityAutomation.Editor
{
    internal static class MCPVFXGraphInfoCommands
    {
        private sealed class SlotPage
        {
            internal List<Dictionary<string, object>> Items =
                new List<Dictionary<string, object>>();
            internal int Total;
            internal int Returned;
            internal int Offset;
        }

        private sealed class ParameterOccurrenceIndex
        {
            internal readonly Dictionary<UnityEngine.Object, List<object>>
                Occurrences = new Dictionary<UnityEngine.Object, List<object>>();
            internal readonly Dictionary<UnityEngine.Object,
                Dictionary<ConnectionSlotPair, string>> ConnectionOwners =
                new Dictionary<UnityEngine.Object,
                    Dictionary<ConnectionSlotPair, string>>();
        }

        private sealed class ResponseBudget
        {
            private int nestedMetadataCount;

            internal void ConsumeNestedMetadata(int count, string label)
            {
                if (count < 0 || nestedMetadataCount >
                    MCPVFXLimits.ReturnedNestedMetadataPerRequest - count)
                    throw MCPVFXError.Create("response_too_large",
                        $"VFX Graph info would return more than {MCPVFXLimits.ReturnedNestedMetadataPerRequest} nested metadata records while projecting {label}.");
                nestedMetadataCount += count;
            }
        }

        private sealed class ConnectionSlotPair : IEquatable<ConnectionSlotPair>
        {
            internal ConnectionSlotPair(object output, object input)
            {
                Output = output;
                Input = input;
            }

            private object Output { get; }
            private object Input { get; }

            public bool Equals(ConnectionSlotPair other)
            {
                return other != null && ReferenceEquals(Output, other.Output) &&
                       ReferenceEquals(Input, other.Input);
            }

            public override bool Equals(object value)
            {
                return Equals(value as ConnectionSlotPair);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    return (System.Runtime.CompilerServices.RuntimeHelpers
                        .GetHashCode(Output) * 397) ^
                        System.Runtime.CompilerServices.RuntimeHelpers
                            .GetHashCode(Input);
                }
            }
        }

        internal static object Info(Dictionary<string, object> args)
        {
            if (!TryValidateKeys(args, new[]
                {
                    "assetPath", "nodeOffset", "maxObjects", "parameterOffset",
                    "maxParameters", "connectionOffset",
                    "maxConnections", "uiOffset", "maxUIItems",
                    "dataOffset", "maxDataObjects", "categoryOffset",
                    "maxCategories", "customAttributeOffset",
                    "maxCustomAttributes", "settingOffset",
                    "maxSettingsPerNode", "occurrenceOffset",
                    "maxOccurrencesPerParameter",
                    "inputSlotOffset", "outputSlotOffset",
                    "eventOffset", "maxEvents", "dependencyOffset",
                    "maxDependencies",
                    "diagnosticOffset", "maxDiagnostics", "maxSlotsPerNode",
                    "maxProperties", "includeSlots", "includeDiagnostics",
                    "includeSerialized", "_agentId",
                }, out object keyError))
                return keyError;
            Dictionary<string, int> budgets;
            try
            {
                if (!TryBudgets(args, out budgets, out object budgetError))
                    return budgetError;
            }
            catch (Exception exception)
            {
                return MCPResponse.Error(MCPVFXReflection.Unwrap(exception).Message,
                    "invalid_arguments");
            }
            string assetPath = GetString(args, "assetPath");
            if (!MCPVFXGraphSession.TryOpen(assetPath,
                    out MCPVFXGraphSession session, out object sessionError))
                return sessionError;

            try
            {
                Dictionary<UnityEngine.Object, string> ids = session.BuildModelIds();
                List<UnityEngine.Object> allModels = session.Models.ToList();
                List<UnityEngine.Object> allNodes = allModels
                    .Where(IsSemanticNode).ToList();
                List<UnityEngine.Object> allParameters = allModels
                    .Where(IsParameter).ToList();
                ParameterOccurrenceIndex occurrenceIndex =
                    BuildParameterOccurrenceIndex(allParameters, ids);
                var responseBudget = new ResponseBudget();
                List<UnityEngine.Object> allDataObjects = allModels.Where(model =>
                        MCPVFXReflection.HasBaseType(model.GetType(),
                            MCPVFXReflection.DataTypeName)).ToList();

                List<UnityEngine.Object> nodePage = allNodes
                    .Skip(budgets["nodeOffset"])
                    .Take(budgets["nodeLimit"]).ToList();
                List<UnityEngine.Object> parameterPage = allParameters
                    .Skip(budgets["parameterOffset"])
                    .Take(budgets["parameterLimit"]).ToList();
                bool includeSlots = GetBool(args, "includeSlots", true);
                List<Dictionary<string, object>> nodes = nodePage.Select(node =>
                    NodeSummary(node, ids, includeSlots,
                        budgets["slotsPerNode"], budgets["settingOffset"],
                        budgets["settingsPerNode"], budgets["inputSlotOffset"],
                        budgets["outputSlotOffset"], responseBudget)).ToList();
                List<Dictionary<string, object>> parameters = parameterPage
                    .Select(parameter => ParameterSummary(parameter, ids,
                        includeSlots, budgets["slotsPerNode"],
                        budgets["settingOffset"], budgets["settingsPerNode"],
                        budgets["occurrenceOffset"],
                        budgets["occurrencesPerParameter"],
                        budgets["inputSlotOffset"],
                        budgets["outputSlotOffset"], occurrenceIndex,
                        responseBudget))
                    .ToList();
                List<Dictionary<string, object>> dataObjects = allDataObjects
                    .Skip(budgets["dataOffset"]).Take(budgets["dataLimit"])
                    .Select(model => DataSummary(model, ids,
                        budgets["settingOffset"], budgets["settingsPerNode"],
                        responseBudget))
                    .ToList();
                List<Dictionary<string, object>> allCategories =
                    CategorySummaries(session.Graph);
                List<Dictionary<string, object>> categories = allCategories
                    .Skip(budgets["categoryOffset"])
                    .Take(budgets["categoryLimit"]).ToList();
                List<Dictionary<string, object>> allCustomAttributes =
                    CustomAttributeSummaries(session.Graph, responseBudget);
                List<Dictionary<string, object>> customAttributes =
                    allCustomAttributes.Skip(budgets["customAttributeOffset"])
                        .Take(budgets["customAttributeLimit"]).ToList();

                List<Dictionary<string, object>> allConnections =
                    BuildDataConnections(allModels, ids, occurrenceIndex)
                        .Concat(BuildFlowConnections(allNodes, ids))
                        .OrderBy(connection => GetString(connection, "kind"),
                            StringComparer.Ordinal)
                        .ThenBy(connection => GetString(connection, "fromNodeId"),
                            StringComparer.Ordinal)
                        .ThenBy(connection => GetString(connection, "fromSlot"),
                            StringComparer.Ordinal)
                        .ThenBy(connection => GetString(connection, "toNodeId"),
                            StringComparer.Ordinal)
                        .ThenBy(connection => GetString(connection, "toSlot"),
                            StringComparer.Ordinal)
                        .ToList();
                if (allConnections.Count > MCPVFXLimits.ConnectionsPerGraph)
                    throw MCPVFXError.Create("response_too_large",
                        $"VFX Graph exposes more than {MCPVFXLimits.ConnectionsPerGraph} data and flow connections.");
                List<Dictionary<string, object>> connections = allConnections
                    .Skip(budgets["connectionOffset"])
                    .Take(budgets["connectionLimit"]).ToList();

                List<Dictionary<string, object>> allUIItems =
                    BuildUIItems(session.Graph, ids);
                List<Dictionary<string, object>> uiItems = allUIItems
                    .Skip(budgets["uiOffset"])
                    .Take(budgets["uiLimit"]).ToList();

                List<Dictionary<string, object>> allDiagnostics =
                    GetBool(args, "includeDiagnostics", false)
                        ? BuildDiagnostics(session.Graph, allModels, ids)
                        : new List<Dictionary<string, object>>();
                List<Dictionary<string, object>> diagnostics = allDiagnostics
                    .Skip(budgets["diagnosticOffset"])
                    .Take(budgets["diagnosticLimit"]).ToList();
                bool eventsAvailable = session.AssetKind == "graph";
                List<string> allEvents = eventsAvailable
                    ? GetEvents(session.Asset) : new List<string>();
                List<string> events = allEvents.Skip(budgets["eventOffset"])
                    .Take(budgets["eventLimit"]).ToList();
                List<string> allDependencies = GetDependencies(session.Graph);
                List<string> dependencies = allDependencies
                    .Skip(budgets["dependencyOffset"])
                    .Take(budgets["dependencyLimit"]).ToList();
                Dictionary<string, object> compilationMode =
                    MCPVFXAssetSettings.CompilationModeSummary(session);

                var response = new Dictionary<string, object>
                {
                    { "success", true },
                    { "assetPath", assetPath },
                    { "assetKind", session.AssetKind },
                    { "assetType", session.Asset.GetType().FullName },
                    { "resourceType", session.Resource.GetType().FullName },
                    { "graphType", session.Graph.GetType().FullName },
                    { "graphVersion", MCPVFXReflection.Get(session.Graph,
                        "version") ?? 0 },
                    { "compilationMode", compilationMode["value"] },
                    { "compilationModeAvailable",
                        compilationMode["available"] },
                    { "resourceSettings",
                        MCPVFXAssetSettings.Values(session) },
                    { "resourceSettingDescriptors",
                        MCPVFXAssetSettings.Summaries(session) },
                    { "eventCount", allEvents.Count },
                    { "eventsAvailable", eventsAvailable },
                    { "eventOffset", budgets["eventOffset"] },
                    { "returnedEventCount", events.Count },
                    { "eventsTruncated", budgets["eventOffset"] +
                        events.Count < allEvents.Count },
                    { "nextEventOffset", budgets["eventOffset"] +
                        events.Count < allEvents.Count
                            ? (object)(budgets["eventOffset"] + events.Count)
                            : null },
                    { "events", events },
                    { "dependencyCount", allDependencies.Count },
                    { "dependencyOffset", budgets["dependencyOffset"] },
                    { "returnedDependencyCount", dependencies.Count },
                    { "dependenciesTruncated", budgets["dependencyOffset"] +
                        dependencies.Count < allDependencies.Count },
                    { "nextDependencyOffset", budgets["dependencyOffset"] +
                        dependencies.Count < allDependencies.Count
                            ? (object)(budgets["dependencyOffset"] +
                                       dependencies.Count) : null },
                    { "dependencies", dependencies },
                    { "dataObjectCount", allDataObjects.Count },
                    { "dataOffset", budgets["dataOffset"] },
                    { "returnedDataObjectCount", dataObjects.Count },
                    { "dataObjectsTruncated", budgets["dataOffset"] +
                        dataObjects.Count < allDataObjects.Count },
                    { "nextDataOffset", budgets["dataOffset"] +
                        dataObjects.Count < allDataObjects.Count
                            ? (object)(budgets["dataOffset"] + dataObjects.Count)
                            : null },
                    { "dataObjects", dataObjects },
                    { "nodeCount", allNodes.Count },
                    { "nodeOffset", budgets["nodeOffset"] },
                    { "returnedNodeCount", nodes.Count },
                    { "nodesTruncated", budgets["nodeOffset"] + nodes.Count <
                                          allNodes.Count },
                    { "nextNodeOffset", budgets["nodeOffset"] + nodes.Count <
                                          allNodes.Count
                        ? (object)(budgets["nodeOffset"] + nodes.Count) : null },
                    { "nodes", nodes },
                    { "parameterCount", allParameters.Count },
                    { "exposedPropertyCount", allParameters.Count(parameter =>
                        MCPVFXReflection.Get(parameter, "exposed") is bool exposed &&
                        exposed) },
                    { "parameterOffset", budgets["parameterOffset"] },
                    { "returnedParameterCount", parameters.Count },
                    { "parametersTruncated",
                        budgets["parameterOffset"] + parameters.Count <
                        allParameters.Count },
                    { "nextParameterOffset",
                        budgets["parameterOffset"] + parameters.Count <
                        allParameters.Count
                            ? (object)(budgets["parameterOffset"] +
                                       parameters.Count) : null },
                    { "parameters", parameters },
                    { "exposedProperties", parameters.Where(parameter =>
                        parameter.TryGetValue("exposed", out object value) &&
                        value is bool exposed && exposed).ToList() },
                    { "categoryCount", allCategories.Count },
                    { "categoryOffset", budgets["categoryOffset"] },
                    { "returnedCategoryCount", categories.Count },
                    { "categoriesTruncated", budgets["categoryOffset"] +
                        categories.Count < allCategories.Count },
                    { "nextCategoryOffset", budgets["categoryOffset"] +
                        categories.Count < allCategories.Count
                            ? (object)(budgets["categoryOffset"] + categories.Count)
                            : null },
                    { "categories", categories },
                    { "customAttributeCount", allCustomAttributes.Count },
                    { "customAttributeOffset",
                        budgets["customAttributeOffset"] },
                    { "returnedCustomAttributeCount", customAttributes.Count },
                    { "customAttributesTruncated",
                        budgets["customAttributeOffset"] + customAttributes.Count <
                        allCustomAttributes.Count },
                    { "nextCustomAttributeOffset",
                        budgets["customAttributeOffset"] + customAttributes.Count <
                        allCustomAttributes.Count
                            ? (object)(budgets["customAttributeOffset"] +
                                       customAttributes.Count) : null },
                    { "customAttributes", customAttributes },
                    { "connectionCount", allConnections.Count },
                    { "connectionOffset", budgets["connectionOffset"] },
                    { "returnedConnectionCount", connections.Count },
                    { "connectionsTruncated",
                        budgets["connectionOffset"] + connections.Count <
                        allConnections.Count },
                    { "nextConnectionOffset",
                        budgets["connectionOffset"] + connections.Count <
                        allConnections.Count
                            ? (object)(budgets["connectionOffset"] +
                                       connections.Count) : null },
                    { "connections", connections },
                    { "uiItemCount", allUIItems.Count },
                    { "uiOffset", budgets["uiOffset"] },
                    { "returnedUIItemCount", uiItems.Count },
                    { "uiItemsTruncated", budgets["uiOffset"] + uiItems.Count <
                                            allUIItems.Count },
                    { "nextUIOffset", budgets["uiOffset"] + uiItems.Count <
                                      allUIItems.Count
                        ? (object)(budgets["uiOffset"] + uiItems.Count) : null },
                    { "uiItems", uiItems },
                    { "uiBounds", MCPVFXReflection.RectValue(
                        MCPVFXReflection.Get(MCPVFXReflection.Get(session.Graph,
                            "UIInfos"), "uiBounds")) },
                    { "diagnosticCount", allDiagnostics.Count },
                    { "diagnosticOffset", budgets["diagnosticOffset"] },
                    { "returnedDiagnosticCount", diagnostics.Count },
                    { "diagnosticsTruncated",
                        budgets["diagnosticOffset"] + diagnostics.Count <
                        allDiagnostics.Count },
                    { "nextDiagnosticOffset",
                        budgets["diagnosticOffset"] + diagnostics.Count <
                        allDiagnostics.Count
                            ? (object)(budgets["diagnosticOffset"] +
                                       diagnostics.Count) : null },
                    { "diagnostics", diagnostics },
                };
                AddPage(response, "graphSetting", "graphSettings",
                    GraphSettings(session.Graph, compilationMode,
                        responseBudget),
                    budgets["settingOffset"],
                    budgets["settingsPerNode"]);
                if (GetBool(args, "includeSerialized", false))
                {
                    response["serializedGraph"] = MCPAssetGraphUtility.InspectAsset(
                        assetPath, IsVFXObject, Math.Min(500,
                            budgets["nodeLimit"] + budgets["parameterLimit"]),
                        budgets["serializedProperties"], session.Contents.ToList());
                }
                return response;
            }
            catch (Exception exception)
            {
                return MCPVFXError.Response(exception,
                    "unsupported_vfx_version");
            }
        }

        internal static List<Dictionary<string, object>> BuildDiagnostics(
            object graph, IReadOnlyList<UnityEngine.Object> models,
            IReadOnlyDictionary<UnityEngine.Object, string> ids)
        {
            foreach (UnityEngine.Object model in models)
                MCPVFXReflection.Invoke(model, "RefreshErrors");
            object errorManager = MCPVFXReflection.Get(graph, "errorManager");
            if (errorManager == null)
                return new List<Dictionary<string, object>>();
            MCPVFXReflection.Invoke(errorManager, "GenerateErrors");
            var results = new List<Dictionary<string, object>>();
            foreach (string reporterName in new[] { "errorReporter", "compileReporter" })
            {
                object reporter = MCPVFXReflection.Get(errorManager, reporterName);
                if (reporter == null)
                    continue;
                string origin = MCPVFXReflection.Get(reporter, "origin")?.ToString() ??
                                reporterName;
                foreach (UnityEngine.Object model in models)
                {
                    object errors = MCPVFXReflection.Invoke(reporter,
                        "GetDirtyModelErrors", model);
                    foreach (object item in MCPVFXReflection.Enumerate(errors))
                    {
                        if (results.Count >= MCPVFXLimits.DiagnosticsPerGraph)
                            throw MCPVFXError.Create("response_too_large",
                                $"VFX Graph exposes more than {MCPVFXLimits.DiagnosticsPerGraph} diagnostics.");
                        UnityEngine.Object target = MCPVFXReflection.Get(item,
                            "model") as UnityEngine.Object ?? model;
                        results.Add(new Dictionary<string, object>
                        {
                            { "origin", origin },
                            { "modelId", target != null && ids.TryGetValue(target,
                                out string id) ? id : "" },
                            { "modelType", target?.GetType().FullName ?? "" },
                            { "modelName", MCPVFXReflection.SemanticName(target) },
                            { "severity", MCPVFXReflection.Get(item,
                                "type")?.ToString() ?? "" },
                            { "errorId", MCPVFXReflection.Get(item,
                                "error")?.ToString() ?? "" },
                            { "description", MCPVFXReflection.Get(item,
                                "description")?.ToString() ?? "" },
                        });
                    }
                }
            }
            return results.GroupBy(item => string.Join("|",
                    GetString(item, "origin"), GetString(item, "modelId"),
                    GetString(item, "severity"), GetString(item, "errorId"),
                    GetString(item, "description")))
                .Select(group => group.First())
                .OrderBy(item => GetString(item, "severity"),
                    StringComparer.Ordinal)
                .ThenBy(item => GetString(item, "modelId"),
                    StringComparer.Ordinal)
                .ThenBy(item => GetString(item, "errorId"),
                    StringComparer.Ordinal)
                .ToList();
        }

        private static Dictionary<string, object> NodeSummary(UnityEngine.Object model,
            IReadOnlyDictionary<UnityEngine.Object, string> ids, bool includeSlots,
            int maxSlots, int settingOffset, int maxSettings,
            int inputSlotOffset, int outputSlotOffset,
            ResponseBudget responseBudget)
        {
            object parent = MCPVFXReflection.Invoke(model, "GetParent");
            var result = new Dictionary<string, object>
            {
                { "id", ids[model] },
                { "name", MCPVFXReflection.SemanticName(model) },
                { "type", model.GetType().FullName },
                { "kind", NodeKind(model.GetType()) },
                { "parentId", parent is UnityEngine.Object parentObject &&
                                ids.TryGetValue(parentObject, out string parentId)
                    ? parentId : "" },
                { "index", parent != null
                    ? MCPVFXReflection.Invoke(parent, "GetIndex", model) : -1 },
                { "position", MCPVFXReflection.Vector2Value(
                    MCPVFXReflection.Get(model, "position")) },
                { "collapsed", MCPVFXReflection.Get(model, "collapsed") ?? false },
                { "superCollapsed", MCPVFXReflection.Get(model,
                    "superCollapsed") ?? false },
                { "enabled", MCPVFXReflection.Get(model, "enabled") ?? true },
                { "children", MCPVFXReflection.Enumerate(
                        MCPVFXReflection.Get(model, "children"))
                    .OfType<UnityEngine.Object>()
                    .Where(ids.ContainsKey).Select(child => ids[child]).ToList() },
            };
            responseBudget.ConsumeNestedMetadata(
                ((IList)result["children"]).Count, "node children");
            AddPage(result, "setting", "settings", Settings(model,
                    responseBudget),
                settingOffset, maxSettings);
            if (MCPVFXReflection.HasBaseType(model.GetType(),
                    MCPVFXReflection.ContextTypeName))
            {
                result["contextType"] = MCPVFXReflection.Get(model,
                    "contextType")?.ToString() ?? "";
                result["compatibleContextType"] = MCPVFXReflection.Get(model,
                    "compatibleContextType")?.ToString() ?? "";
                result["inputDataType"] = MCPVFXReflection.Get(model,
                    "inputType")?.ToString() ?? "";
                result["outputDataType"] = MCPVFXReflection.Get(model,
                    "outputType")?.ToString() ?? "";
                result["ownedDataType"] = MCPVFXReflection.Get(model,
                    "ownedType")?.ToString() ?? "";
                result["inputFlowCount"] = MCPVFXReflection.Enumerate(
                    MCPVFXReflection.Get(model, "inputFlowSlot")).Count();
                result["outputFlowCount"] = MCPVFXReflection.Enumerate(
                    MCPVFXReflection.Get(model, "outputFlowSlot")).Count();
            }
            if (includeSlots)
            {
                AddSlots(result, "input", BuildSlotPage(model, "inputSlots",
                    inputSlotOffset, maxSlots));
                AddSlots(result, "output", BuildSlotPage(model, "outputSlots",
                    outputSlotOffset, maxSlots));
            }
            return result;
        }

        private static Dictionary<string, object> ParameterSummary(
            UnityEngine.Object parameter,
            IReadOnlyDictionary<UnityEngine.Object, string> ids, bool includeSlots,
            int maxSlots, int settingOffset, int maxSettings,
            int occurrenceOffset, int maxOccurrences,
            int inputSlotOffset, int outputSlotOffset,
            ParameterOccurrenceIndex occurrenceIndex,
            ResponseBudget responseBudget)
        {
            string definitionId = ids[parameter];
            List<object> occurrences = occurrenceIndex.Occurrences[parameter];
            var result = new Dictionary<string, object>
            {
                { "id", definitionId },
                { "name", MCPVFXReflection.Get(parameter,
                    "exposedName")?.ToString() ?? MCPVFXReflection.SemanticName(parameter) },
                { "type", MCPVFXReflection.Get(parameter, "type") is Type type
                    ? type.FullName : "" },
                { "exposed", MCPVFXReflection.Get(parameter, "exposed") ?? false },
                { "isOutput", MCPVFXReflection.Get(parameter, "isOutput") ?? false },
                { "category", MCPVFXReflection.Get(parameter,
                    "category")?.ToString() ?? "" },
                { "order", MCPVFXReflection.Get(parameter, "order") ?? 0 },
                { "tooltip", MCPVFXReflection.Get(parameter,
                    "tooltip")?.ToString() ?? "" },
                { "value", MCPVFXValueCodec.Sanitize(
                    MCPVFXReflection.Get(parameter, "value")) },
                { "valueFilter", MCPVFXReflection.Get(parameter,
                    "valueFilter")?.ToString() ?? "" },
                { "min", MCPVFXValueCodec.Sanitize(
                    MCPVFXReflection.Get(parameter, "min")) },
                { "max", MCPVFXValueCodec.Sanitize(
                    MCPVFXReflection.Get(parameter, "max")) },
                { "enumValues", Bounded(MCPVFXReflection.Enumerate(
                            MCPVFXReflection.Get(parameter, "enumValues")),
                        MCPVFXLimits.CatalogMetadataPerItem,
                        "parameter enum values")
                    .Select(value => value?.ToString() ?? "").ToList() },
                { "collapsed", MCPVFXReflection.Get(parameter,
                    "collapsed") ?? false },
                { "occurrenceCount", occurrences.Count },
                { "occurrenceOffset", occurrenceOffset },
                { "returnedOccurrenceCount", Math.Min(maxOccurrences,
                    Math.Max(0, occurrences.Count - occurrenceOffset)) },
                { "occurrencesTruncated", occurrenceOffset + maxOccurrences <
                    occurrences.Count },
                { "nextOccurrenceOffset", occurrenceOffset + maxOccurrences <
                    occurrences.Count ? (object)(occurrenceOffset +
                    maxOccurrences) : null },
                { "occurrences", occurrences.Skip(occurrenceOffset)
                    .Take(maxOccurrences).Select(node =>
                        ParameterOccurrenceSummary(definitionId, node)).ToList() },
            };
            responseBudget.ConsumeNestedMetadata(
                ((IList)result["enumValues"]).Count, "parameter enum values");
            AddPage(result, "setting", "settings", Settings(parameter,
                    responseBudget),
                settingOffset, maxSettings);
            if (includeSlots)
            {
                AddSlots(result, "input", BuildSlotPage(parameter, "inputSlots",
                    inputSlotOffset, maxSlots));
                AddSlots(result, "output", BuildSlotPage(parameter, "outputSlots",
                    outputSlotOffset, maxSlots));
            }
            return result;
        }

        private static Dictionary<string, object> ParameterOccurrenceSummary(
            string parameterId, object node)
        {
            int nodeId = Convert.ToInt32(MCPVFXReflection.Get(node, "id") ?? -1);
            return new Dictionary<string, object>
            {
                { "id", parameterId + ":" + nodeId },
                { "nodeId", nodeId },
                { "position", MCPVFXReflection.Vector2Value(
                    MCPVFXReflection.Get(node, "position")) },
                { "expanded", MCPVFXReflection.Get(node, "expanded") ?? true },
                { "superCollapsed", MCPVFXReflection.Get(node,
                    "supecollapsed") ?? false },
                { "linkedSlotCount", MCPVFXReflection.Enumerate(
                    MCPVFXReflection.Get(node, "linkedSlots")).Count() },
            };
        }

        private static SlotPage BuildSlotPage(object model,
            string memberName, int offset, int maxSlots)
        {
            IReadOnlyList<MCPVFXReflection.SlotReference> all =
                MCPVFXReflection.EnumerateSlots(model, memberName);
            var page = new SlotPage
            {
                Total = all.Count,
                Offset = offset,
            };
            page.Items = all.Skip(offset).Take(maxSlots)
                .Select(reference => SlotSummary(reference.Slot,
                    reference.Selector)).ToList();
            page.Returned = page.Items.Count;
            return page;
        }

        private static Dictionary<string, object> SlotSummary(object slot,
            string selector)
        {
            object property = MCPVFXReflection.Get(slot, "property");
            Type valueType = MCPVFXReflection.Get(property, "type") as Type;
            int lastSegment = selector.LastIndexOf('[', selector.Length - 2);
            string parentSelector = lastSegment >= 0
                ? selector.Substring(0, lastSegment) : "";
            List<object> children = MCPVFXReflection.Enumerate(
                MCPVFXReflection.Get(slot, "children")).ToList();
            return new Dictionary<string, object>
            {
                { "name", MCPVFXReflection.Get(property, "name")?.ToString() ?? "" },
                { "path", MCPVFXReflection.Get(slot, "path")?.ToString() ?? "" },
                { "selector", selector },
                { "parentSelector", parentSelector },
                { "depth", selector.Count(character => character == '[') - 1 },
                { "type", valueType?.FullName ?? "" },
                { "direction", MCPVFXReflection.Get(slot,
                    "direction")?.ToString() ?? "" },
                { "value", MCPVFXValueCodec.Sanitize(
                    MCPVFXReflection.Get(slot, "value")) },
                { "spaceable", MCPVFXReflection.Get(slot, "spaceable") ?? false },
                { "space", MCPVFXReflection.Get(slot, "space")?.ToString() ?? "" },
                { "collapsed", MCPVFXReflection.Get(slot, "collapsed") ?? false },
                { "linkCount", MCPVFXReflection.Invoke(slot, "GetNbLinks") },
                { "childSelectors", children.Select((child, index) =>
                    selector + $"[{index}]").ToList() },
            };
        }

        private static void AddSlots(Dictionary<string, object> result,
            string direction, SlotPage page)
        {
            string collectionName = direction == "input" ? "inputs" : "outputs";
            result[collectionName] = page.Items;
            result[direction + "SlotCount"] = page.Total;
            result[direction + "SlotOffset"] = page.Offset;
            result["returned" + char.ToUpperInvariant(direction[0]) +
                   direction.Substring(1) + "SlotCount"] = page.Returned;
            result[collectionName + "Truncated"] = page.Offset + page.Returned <
                                                       page.Total;
            result["next" + char.ToUpperInvariant(direction[0]) +
                   direction.Substring(1) + "SlotOffset"] =
                page.Offset + page.Returned < page.Total
                    ? (object)(page.Offset + page.Returned) : null;
        }

        private static List<Dictionary<string, object>> Settings(object model,
            ResponseBudget responseBudget)
        {
            if (model == null)
                return new List<Dictionary<string, object>>();
            object settings = MCPVFXReflection.Invoke(model, "GetSettings", true);
            List<object> bounded = Bounded(MCPVFXReflection.Enumerate(settings),
                MCPVFXLimits.SettingsPerModel,
                $"settings on '{MCPVFXReflection.SemanticName(model)}'");
            return bounded.Select(setting =>
            {
                FieldInfo field = MCPVFXReflection.Get(setting, "field") as FieldInfo;
                object value = MCPVFXReflection.Get(setting, "value");
                List<string> enumValues = field?.FieldType.IsEnum == true
                    ? Bounded(Enum.GetNames(field.FieldType),
                            MCPVFXLimits.CatalogMetadataPerItem,
                            "setting enum values").ToList()
                    : new List<string>();
                responseBudget.ConsumeNestedMetadata(enumValues.Count,
                    "setting enum values");
                return new Dictionary<string, object>
                {
                    { "name", MCPVFXReflection.Get(setting,
                        "name")?.ToString() ?? "" },
                    { "type", field?.FieldType.FullName ?? value?.GetType().FullName ?? "" },
                    { "value", MCPVFXValueCodec.Sanitize(value) },
                    { "visibility", MCPVFXReflection.Get(setting,
                        "visibility")?.ToString() ?? "" },
                    { "enumValues", enumValues },
                };
            }).ToList();
        }

        private static void AddPage(Dictionary<string, object> result,
            string prefix, string collectionName,
            List<Dictionary<string, object>> all, int offset, int limit)
        {
            List<Dictionary<string, object>> page = all.Skip(offset)
                .Take(limit).ToList();
            string titlePrefix = char.ToUpperInvariant(prefix[0]) +
                                 prefix.Substring(1);
            result[prefix + "Count"] = all.Count;
            result[prefix + "Offset"] = offset;
            result["returned" + titlePrefix + "Count"] = page.Count;
            result[collectionName + "Truncated"] = offset + page.Count < all.Count;
            result["next" + titlePrefix + "Offset"] =
                offset + page.Count < all.Count
                    ? (object)(offset + page.Count) : null;
            result[collectionName] = page;
        }

        private static ParameterOccurrenceIndex BuildParameterOccurrenceIndex(
            IReadOnlyList<UnityEngine.Object> parameters,
            IReadOnlyDictionary<UnityEngine.Object, string> ids)
        {
            var result = new ParameterOccurrenceIndex();
            int occurrenceCount = 0;
            int linkOwnershipCount = 0;
            foreach (UnityEngine.Object parameter in parameters)
            {
                List<object> occurrences = MCPVFXReflection.Enumerate(
                        MCPVFXReflection.Get(parameter, "nodes"))
                    .Take(MCPVFXLimits.CollectionItems + 1).ToList();
                occurrenceCount += occurrences.Count;
                if (occurrenceCount > MCPVFXLimits.CollectionItems)
                    throw MCPVFXError.Create("response_too_large",
                        $"VFX Graph exposes more than {MCPVFXLimits.CollectionItems} parameter occurrences.");
                result.Occurrences.Add(parameter, occurrences);
                var owners = new Dictionary<ConnectionSlotPair, string>();
                foreach (object occurrence in occurrences)
                {
                    int nodeId = Convert.ToInt32(MCPVFXReflection.Get(
                        occurrence, "id") ?? -1);
                    string occurrenceId = ids[parameter] + ":" + nodeId;
                    foreach (object link in MCPVFXReflection.Enumerate(
                                 MCPVFXReflection.Get(occurrence, "linkedSlots")))
                    {
                        linkOwnershipCount++;
                        if (linkOwnershipCount >
                            MCPVFXLimits.ConnectionsPerGraph * 2)
                            throw MCPVFXError.Create("response_too_large",
                                $"VFX Graph exposes more than {MCPVFXLimits.ConnectionsPerGraph * 2} parameter-link ownership records.");
                        var pair = new ConnectionSlotPair(
                            MCPVFXReflection.Get(link, "outputSlot"),
                            MCPVFXReflection.Get(link, "inputSlot"));
                        if (owners.ContainsKey(pair))
                            throw new InvalidOperationException(
                                $"VFX parameter '{ids[parameter]}' assigns one data connection to multiple occurrences.");
                        owners.Add(pair, occurrenceId);
                    }
                }
                result.ConnectionOwners.Add(parameter, owners);
            }
            return result;
        }

        private static List<Dictionary<string, object>> BuildDataConnections(
            IReadOnlyList<UnityEngine.Object> models,
            IReadOnlyDictionary<UnityEngine.Object, string> ids,
            ParameterOccurrenceIndex occurrenceIndex)
        {
            var results = new List<Dictionary<string, object>>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            int slotCount = 0;
            var inputSlots = new Dictionary<UnityEngine.Object,
                IReadOnlyList<MCPVFXReflection.SlotReference>>();
            foreach (UnityEngine.Object model in models)
                inputSlots.Add(model, BoundedSlots(model, "inputSlots",
                    ref slotCount));
            foreach (UnityEngine.Object model in models)
            {
                foreach (MCPVFXReflection.SlotReference outputReference in
                         BoundedSlots(model, "outputSlots", ref slotCount))
                {
                    object outputSlot = outputReference.Slot;
                    foreach (object inputSlot in MCPVFXReflection.Enumerate(
                                 MCPVFXReflection.Get(outputSlot, "LinkedSlots")))
                    {
                        object inputOwner = MCPVFXReflection.Get(inputSlot, "owner");
                        if (!(inputOwner is UnityEngine.Object inputModel) ||
                            !ids.TryGetValue(inputModel, out string inputId))
                            continue;
                        string outputId = ids[model];
                        string fromNode = ResolveParameterOccurrenceId(model, outputId,
                            outputSlot, inputSlot, occurrenceIndex);
                        string toNode = ResolveParameterOccurrenceId(inputModel,
                            inputId, outputSlot, inputSlot, occurrenceIndex);
                        string fromSelector = outputReference.Selector;
                        string toSelector = FindSlotSelector(inputSlots[inputModel],
                            inputSlot, inputModel);
                        string key = string.Join("|", fromNode, fromSelector,
                            toNode, toSelector);
                        if (!seen.Add(key))
                            continue;
                        if (results.Count >= MCPVFXLimits.ConnectionsPerGraph)
                            throw MCPVFXError.Create("response_too_large",
                                $"VFX Graph exposes more than {MCPVFXLimits.ConnectionsPerGraph} data connections.");
                        results.Add(new Dictionary<string, object>
                        {
                            { "kind", "data" },
                            { "fromNodeId", fromNode },
                            { "fromSlot", fromSelector },
                            { "fromSlotPath", MCPVFXReflection.Get(outputSlot,
                                "path")?.ToString() ?? "" },
                            { "toNodeId", toNode },
                            { "toSlot", toSelector },
                            { "toSlotPath", MCPVFXReflection.Get(inputSlot,
                                "path")?.ToString() ?? "" },
                        });
                    }
                }
            }
            return results;
        }

        private static IReadOnlyList<MCPVFXReflection.SlotReference> BoundedSlots(
            UnityEngine.Object model, string memberName, ref int total)
        {
            IReadOnlyList<MCPVFXReflection.SlotReference> slots =
                MCPVFXReflection.EnumerateSlots(model, memberName);
            total += slots.Count;
            if (total > MCPVFXLimits.SlotsPerGraph)
                throw MCPVFXError.Create("response_too_large",
                    $"VFX Graph exposes more than {MCPVFXLimits.SlotsPerGraph} recursive slots while enumerating connections.");
            return slots;
        }

        private static List<Dictionary<string, object>> BuildFlowConnections(
            IReadOnlyList<UnityEngine.Object> nodes,
            IReadOnlyDictionary<UnityEngine.Object, string> ids)
        {
            var results = new List<Dictionary<string, object>>();
            foreach (UnityEngine.Object context in nodes.Where(node =>
                         MCPVFXReflection.HasBaseType(node.GetType(),
                             MCPVFXReflection.ContextTypeName)))
            {
                List<object> outputSlots = MCPVFXReflection.Enumerate(
                    MCPVFXReflection.Get(context, "outputFlowSlot")).ToList();
                for (int fromIndex = 0; fromIndex < outputSlots.Count; fromIndex++)
                {
                    foreach (object link in MCPVFXReflection.Enumerate(
                                 MCPVFXReflection.Get(outputSlots[fromIndex], "link")))
                    {
                        UnityEngine.Object target = MCPVFXReflection.Get(link,
                            "context") as UnityEngine.Object;
                        if (target == null || !ids.TryGetValue(target,
                                out string targetId))
                            continue;
                        if (results.Count >= MCPVFXLimits.ConnectionsPerGraph)
                            throw MCPVFXError.Create("response_too_large",
                                $"VFX Graph exposes more than {MCPVFXLimits.ConnectionsPerGraph} flow connections.");
                        int toIndex = Convert.ToInt32(MCPVFXReflection.Get(link,
                            "slotIndex") ?? 0);
                        results.Add(new Dictionary<string, object>
                        {
                            { "kind", "flow" },
                            { "fromNodeId", ids[context] },
                            { "fromSlot", fromIndex.ToString() },
                            { "fromFlowIndex", fromIndex },
                            { "toNodeId", targetId },
                            { "toSlot", toIndex.ToString() },
                            { "toFlowIndex", toIndex },
                        });
                    }
                }
            }
            return results;
        }

        private static string ResolveParameterOccurrenceId(UnityEngine.Object model,
            string definitionId, object outputSlot, object inputSlot,
            ParameterOccurrenceIndex occurrenceIndex)
        {
            if (!IsParameter(model))
                return definitionId;
            var pair = new ConnectionSlotPair(outputSlot, inputSlot);
            if (!occurrenceIndex.ConnectionOwners[model].TryGetValue(pair,
                    out string occurrenceId))
                throw new InvalidOperationException(
                    $"VFX data connection owned by parameter '{definitionId}' has no exact parameter occurrence.");
            return occurrenceId;
        }

        private static string FindSlotSelector(
            IReadOnlyList<MCPVFXReflection.SlotReference> references,
            object slot, UnityEngine.Object owner)
        {
            List<MCPVFXReflection.SlotReference> matches = references
                .Where(item => ReferenceEquals(item.Slot, slot)).ToList();
            if (matches.Count != 1)
                throw new InvalidOperationException(
                    $"VFX slot ownership for '{MCPVFXReflection.SemanticName(owner)}' resolved to {matches.Count} selectors.");
            return matches[0].Selector;
        }

        private static List<Dictionary<string, object>> BuildUIItems(object graph,
            IReadOnlyDictionary<UnityEngine.Object, string> ids)
        {
            object ui = MCPVFXReflection.Get(graph, "UIInfos");
            var results = new List<Dictionary<string, object>>();
            List<object> stickyNotes = Bounded(MCPVFXReflection.Enumerate(
                    MCPVFXReflection.Get(ui, "stickyNoteInfos")),
                MCPVFXLimits.CollectionItems, "sticky notes");
            for (int index = 0; index < stickyNotes.Count; index++)
            {
                object note = stickyNotes[index];
                results.Add(new Dictionary<string, object>
                {
                    { "kind", "sticky-note" },
                    { "id", "sticky:" + index },
                    { "index", index },
                    { "title", MCPVFXReflection.Get(note, "title")?.ToString() ?? "" },
                    { "position", MCPVFXReflection.RectValue(
                        MCPVFXReflection.Get(note, "position")) },
                    { "contents", MCPVFXReflection.Get(note,
                        "contents")?.ToString() ?? "" },
                    { "theme", MCPVFXReflection.Get(note,
                        "theme")?.ToString() ?? "" },
                    { "textSize", MCPVFXReflection.Get(note,
                        "textSize")?.ToString() ?? "" },
                    { "colorTheme", MCPVFXReflection.Get(note,
                        "colorTheme") ?? 0 },
                });
            }
            List<object> groups = Bounded(MCPVFXReflection.Enumerate(
                    MCPVFXReflection.Get(ui, "groupInfos")),
                MCPVFXLimits.CollectionItems - stickyNotes.Count, "UI items");
            for (int index = 0; index < groups.Count; index++)
            {
                object group = groups[index];
                results.Add(new Dictionary<string, object>
                {
                    { "kind", "group" },
                    { "id", "group:" + index },
                    { "index", index },
                    { "title", MCPVFXReflection.Get(group, "title")?.ToString() ?? "" },
                    { "position", MCPVFXReflection.RectValue(
                        MCPVFXReflection.Get(group, "position")) },
                    { "contents", MCPVFXReflection.Enumerate(
                            MCPVFXReflection.Get(group, "contents"))
                        .Select(content => UIContentId(content, ids)).ToList() },
                });
            }
            return results;
        }

        private static string UIContentId(object content,
            IReadOnlyDictionary<UnityEngine.Object, string> ids)
        {
            if (MCPVFXReflection.Get(content, "isStickyNote") is bool sticky &&
                sticky)
                return "sticky:" + Convert.ToInt32(MCPVFXReflection.Get(content,
                    "id") ?? -1);
            UnityEngine.Object model = MCPVFXReflection.Get(content,
                "model") as UnityEngine.Object;
            if (model == null || !ids.TryGetValue(model, out string id))
                return "";
            if (IsParameter(model))
                return id + ":" + Convert.ToInt32(MCPVFXReflection.Get(content,
                    "id") ?? -1);
            return id;
        }

        private static List<Dictionary<string, object>> CategorySummaries(object graph)
        {
            object ui = MCPVFXReflection.Get(graph, "UIInfos");
            return Bounded(MCPVFXReflection.Enumerate(
                    MCPVFXReflection.Get(ui, "categories")),
                    MCPVFXLimits.CollectionItems, "blackboard categories")
                .Select((category, index) => new Dictionary<string, object>
                {
                    { "index", index },
                    { "name", MCPVFXReflection.Get(category,
                        "name")?.ToString() ?? "" },
                    { "collapsed", MCPVFXReflection.Get(category,
                        "collapsed") ?? false },
                }).ToList();
        }

        private static List<Dictionary<string, object>> CustomAttributeSummaries(
            object graph, ResponseBudget responseBudget)
        {
            return Bounded(MCPVFXReflection.Enumerate(
                    MCPVFXReflection.Get(graph, "customAttributes")),
                    MCPVFXLimits.CollectionItems, "custom attributes")
                .Select((attribute, index) =>
                {
                    List<string> usages = Bounded(MCPVFXReflection.Enumerate(
                                MCPVFXReflection.Get(attribute,
                                    "usedInSubgraphs")),
                            MCPVFXLimits.CollectionItems,
                            "custom attribute subgraph usages")
                        .Select(value => value?.ToString() ?? "").ToList();
                    responseBudget.ConsumeNestedMetadata(usages.Count,
                        "custom attribute subgraph usages");
                    return new Dictionary<string, object>
                    {
                    { "index", index },
                    { "name", MCPVFXReflection.Get(attribute,
                        "attributeName")?.ToString() ?? "" },
                    { "type", MCPVFXReflection.Get(attribute,
                        "type")?.ToString() ?? "" },
                    { "description", MCPVFXReflection.Get(attribute,
                        "description")?.ToString() ?? "" },
                    { "readOnly", MCPVFXReflection.Get(attribute,
                        "isReadOnly") ?? false },
                    { "expanded", MCPVFXReflection.Get(attribute,
                        "isExpanded") ?? false },
                    { "usedInSubgraphs", usages },
                    { "inUse", Convert.ToBoolean(MCPVFXReflection.Invoke(graph,
                        "IsCustomAttributeUsed", MCPVFXReflection.Get(attribute,
                            "attributeName")?.ToString() ?? "")) },
                    };
                }).ToList();
        }

        private static Dictionary<string, object> DataSummary(UnityEngine.Object model,
            IReadOnlyDictionary<UnityEngine.Object, string> ids,
            int settingOffset, int maxSettings, ResponseBudget responseBudget)
        {
            var result = new Dictionary<string, object>
            {
                { "id", ids.TryGetValue(model, out string id) ? id : "" },
                { "type", model.GetType().FullName },
                { "name", MCPVFXReflection.SemanticName(model) },
            };
            AddPage(result, "setting", "settings", Settings(model,
                    responseBudget),
                settingOffset, maxSettings);
            return result;
        }

        private static List<Dictionary<string, object>> GraphSettings(
            object graph, Dictionary<string, object> compilationMode,
            ResponseBudget responseBudget)
        {
            List<Dictionary<string, object>> result = Settings(graph,
                responseBudget);
            if (!result.Any(setting => string.Equals(
                    setting.TryGetValue("name", out object name)
                        ? name?.ToString() : "", "compilationMode",
                    StringComparison.Ordinal)))
                result.Insert(0, compilationMode);
            return result;
        }

        private static List<string> GetEvents(UnityEngine.Object asset)
        {
            MethodInfo method = asset.GetType().GetMethods(BindingFlags.Instance |
                    BindingFlags.Public | BindingFlags.NonPublic)
                .FirstOrDefault(candidate => candidate.Name == "GetEvents" &&
                                              candidate.GetParameters().Length == 1);
            if (method == null)
                throw new MissingMethodException(asset.GetType().FullName,
                    "GetEvents");
            Type argumentType = method.GetParameters()[0].ParameterType;
            object list = Activator.CreateInstance(argumentType);
            try
            {
                method.Invoke(asset, new[] { list });
            }
            catch (TargetInvocationException exception)
            {
                throw MCPVFXReflection.Unwrap(exception);
            }
            return Bounded(MCPVFXReflection.Enumerate(list),
                    MCPVFXLimits.CollectionItems, "events")
                .Select(value => value?.ToString() ?? "").ToList();
        }

        private static List<string> GetDependencies(object graph)
        {
            var dependencies = new HashSet<string>(StringComparer.Ordinal);
            MCPVFXReflection.Invoke(graph, "GetSourceDependentAssets",
                dependencies);
            if (dependencies.Count > MCPVFXLimits.CollectionItems)
                throw MCPVFXError.Create("response_too_large",
                    $"VFX Graph exposes more than {MCPVFXLimits.CollectionItems} source dependencies.");
            return dependencies.OrderBy(path => path, StringComparer.Ordinal)
                .ToList();
        }

        private static List<T> Bounded<T>(IEnumerable<T> source, int maximum,
            string label)
        {
            List<T> result = source.Take(maximum + 1).ToList();
            if (result.Count > maximum)
                throw MCPVFXError.Create("response_too_large",
                    $"VFX Graph exposes more than {maximum} {label}.");
            return result;
        }

        private static bool TryBudgets(Dictionary<string, object> args,
            out Dictionary<string, int> values, out object error)
        {
            values = new Dictionary<string, int>
            {
                { "nodeOffset", GetInt(args, "nodeOffset", 0) },
                { "nodeLimit", GetInt(args, "maxObjects", 250) },
                { "parameterOffset", GetInt(args, "parameterOffset", 0) },
                { "parameterLimit", GetInt(args, "maxParameters", 100) },
                { "connectionOffset", GetInt(args, "connectionOffset", 0) },
                { "connectionLimit", GetInt(args, "maxConnections", 500) },
                { "uiOffset", GetInt(args, "uiOffset", 0) },
                { "uiLimit", GetInt(args, "maxUIItems", 250) },
                { "dataOffset", GetInt(args, "dataOffset", 0) },
                { "dataLimit", GetInt(args, "maxDataObjects", 100) },
                { "categoryOffset", GetInt(args, "categoryOffset", 0) },
                { "categoryLimit", GetInt(args, "maxCategories", 100) },
                { "customAttributeOffset", GetInt(args,
                    "customAttributeOffset", 0) },
                { "customAttributeLimit", GetInt(args,
                    "maxCustomAttributes", 100) },
                { "settingOffset", GetInt(args, "settingOffset", 0) },
                { "settingsPerNode", GetInt(args, "maxSettingsPerNode", 64) },
                { "occurrenceOffset", GetInt(args, "occurrenceOffset", 0) },
                { "occurrencesPerParameter", GetInt(args,
                    "maxOccurrencesPerParameter", 100) },
                { "inputSlotOffset", GetInt(args, "inputSlotOffset", 0) },
                { "outputSlotOffset", GetInt(args, "outputSlotOffset", 0) },
                { "eventOffset", GetInt(args, "eventOffset", 0) },
                { "eventLimit", GetInt(args, "maxEvents", 100) },
                { "dependencyOffset", GetInt(args, "dependencyOffset", 0) },
                { "dependencyLimit", GetInt(args, "maxDependencies", 100) },
                { "diagnosticOffset", GetInt(args, "diagnosticOffset", 0) },
                { "diagnosticLimit", GetInt(args, "maxDiagnostics", 250) },
                { "slotsPerNode", GetInt(args, "maxSlotsPerNode", 50) },
                { "serializedProperties", GetInt(args, "maxProperties", 40) },
            };
            foreach (string offset in new[]
                     {
                         "nodeOffset", "parameterOffset", "connectionOffset",
                         "uiOffset", "dataOffset", "categoryOffset",
                          "customAttributeOffset", "settingOffset",
                          "occurrenceOffset", "inputSlotOffset",
                          "outputSlotOffset", "eventOffset",
                          "dependencyOffset", "diagnosticOffset",
                     })
            {
                if (values[offset] < 0)
                {
                    error = MCPResponse.Error($"{offset} must be at least 0.",
                        "invalid_arguments");
                    return false;
                }
            }
            foreach ((string key, int max) in new[]
                     {
                         ("nodeLimit", 1000), ("parameterLimit", 1000),
                          ("connectionLimit", 5000), ("uiLimit", 1000),
                          ("dataLimit", 1000), ("categoryLimit", 1000),
                           ("customAttributeLimit", 1000),
                           ("settingsPerNode", 128),
                           ("occurrencesPerParameter", 256),
                           ("eventLimit", 1000),
                           ("dependencyLimit", 1000),
                           ("diagnosticLimit", 1000), ("slotsPerNode", 256),
                         ("serializedProperties", 500),
                     })
            {
                if (values[key] < 1 || values[key] > max)
                {
                    error = MCPResponse.Error(
                        $"{key} must be between 1 and {max}.",
                        "invalid_arguments");
                    return false;
                }
            }
            if (GetBool(args, "includeSlots", true) &&
                (long)(values["nodeLimit"] + values["parameterLimit"]) *
                values["slotsPerNode"] * 2 >
                MCPVFXLimits.ReturnedSlotsPerRequest)
            {
                error = MCPResponse.Error(
                    $"The requested node, parameter, and slot page sizes can return more than {MCPVFXLimits.ReturnedSlotsPerRequest} slots. Reduce maxObjects, maxParameters, or maxSlotsPerNode.",
                    "invalid_arguments");
                return false;
            }
            error = null;
            return true;
        }

        private static string NodeKind(Type type)
        {
            if (MCPVFXReflection.HasBaseType(type,
                    MCPVFXReflection.ContextTypeName)) return "context";
            if (MCPVFXReflection.HasBaseType(type,
                    MCPVFXReflection.BlockTypeName)) return "block";
            if (MCPVFXReflection.HasBaseType(type,
                    MCPVFXReflection.OperatorTypeName)) return "operator";
            return "node";
        }

        private static bool IsSemanticNode(UnityEngine.Object model)
        {
            return model != null &&
                   (MCPVFXReflection.HasBaseType(model.GetType(),
                        MCPVFXReflection.ContextTypeName) ||
                    MCPVFXReflection.HasBaseType(model.GetType(),
                        MCPVFXReflection.BlockTypeName) ||
                    MCPVFXReflection.HasBaseType(model.GetType(),
                        MCPVFXReflection.OperatorTypeName));
        }

        private static bool IsParameter(UnityEngine.Object model)
        {
            return model != null && MCPVFXReflection.HasBaseType(model.GetType(),
                MCPVFXReflection.ParameterTypeName);
        }

        private static bool IsVFXObject(UnityEngine.Object value)
        {
            return MCPAssetGraphUtility.IsTypeOrNamespace(value,
                "UnityEngine.VFX.", "UnityEditor.VFX.");
        }

        private static bool TryValidateKeys(Dictionary<string, object> values,
            IEnumerable<string> allowed, out object error)
        {
            var set = new HashSet<string>(allowed, StringComparer.Ordinal);
            string unknown = values?.Keys.FirstOrDefault(key => !set.Contains(key));
            if (string.IsNullOrEmpty(unknown))
            {
                error = null;
                return true;
            }
            error = MCPResponse.Error($"Unsupported argument '{unknown}'.",
                "invalid_arguments");
            return false;
        }

        private static string GetString(Dictionary<string, object> values, string key)
        {
            return values != null && values.TryGetValue(key, out object value) &&
                   value != null ? value.ToString() : "";
        }

        private static int GetInt(Dictionary<string, object> values, string key,
            int defaultValue)
        {
            return values != null && values.TryGetValue(key, out object value) &&
                   value != null ? (int)MCPVFXValueCodec.ConvertTo(value,
                       typeof(int), key) : defaultValue;
        }

        private static bool GetBool(Dictionary<string, object> values, string key,
            bool defaultValue)
        {
            return values != null && values.TryGetValue(key, out object value) &&
                   value != null ? (bool)MCPVFXValueCodec.ConvertTo(value,
                       typeof(bool), key) : defaultValue;
        }
    }
}
