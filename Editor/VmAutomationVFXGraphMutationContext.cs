using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace VMUnityAutomation.Editor
{
    internal sealed class VmAutomationVFXGraphMutationContext
    {
        private readonly Dictionary<string, UnityEngine.Object> aliases =
            new Dictionary<string, UnityEngine.Object>(StringComparer.Ordinal);
        private readonly Dictionary<string, KeyValuePair<UnityEngine.Object, int>>
            occurrenceAliases = new Dictionary<string,
                KeyValuePair<UnityEngine.Object, int>>(StringComparer.Ordinal);
        private readonly Dictionary<string, object> descriptorCache =
            new Dictionary<string, object>(StringComparer.Ordinal);

        internal VmAutomationVFXGraphMutationContext(VmAutomationVFXGraphSession session)
        {
            Session = session ?? throw new ArgumentNullException(nameof(session));
        }

        internal VmAutomationVFXGraphSession Session { get; }

        internal IReadOnlyDictionary<string, UnityEngine.Object> Aliases => aliases;

        internal UnityEngine.Object ResolveModel(string selector,
            string argumentName = "nodeId")
        {
            if (string.IsNullOrWhiteSpace(selector))
                throw new ArgumentException($"{argumentName} is required.");
            if (aliases.TryGetValue(selector, out UnityEngine.Object aliasModel) &&
                aliasModel != null)
                return aliasModel;
            UnityEngine.Object model = Session.ResolveModel(selector);
            if (model == null)
                throw VmAutomationVFXError.Create(MissingModelCode(selector),
                    $"VFX model '{selector}' from {argumentName} was not found.");
            return model;
        }

        internal UnityEngine.Object ResolveDefinition(string selector,
            string argumentName = "parameterId")
        {
            string definitionId = selector ?? "";
            UnityEngine.Object model = aliases.TryGetValue(definitionId,
                    out UnityEngine.Object alias)
                ? alias : Session.ResolveModel(definitionId);
            if (model == null)
            {
                int separator = definitionId.LastIndexOf(':');
                if (separator > 0)
                {
                    string candidate = definitionId.Substring(0, separator);
                    model = aliases.TryGetValue(candidate,
                            out UnityEngine.Object definitionAlias)
                        ? definitionAlias : Session.ResolveModel(candidate);
                    definitionId = candidate;
                }
            }
            if (model == null)
                throw VmAutomationVFXError.Create(MissingModelCode(definitionId),
                    $"VFX parameter definition '{definitionId}' from {argumentName} was not found.");
            if (!VmAutomationVFXReflection.HasBaseType(model.GetType(),
                    VmAutomationVFXReflection.ParameterTypeName))
                throw new ArgumentException(
                    $"VFX model '{selector}' is not a parameter definition.");
            return model;
        }

        internal UnityEngine.Object ResolveNodeSelector(string selector,
            string argumentName, out int? parameterNodeId)
        {
            parameterNodeId = null;
            if (!string.IsNullOrEmpty(selector))
            {
                UnityEngine.Object direct = aliases.TryGetValue(selector,
                    out UnityEngine.Object alias) ? alias : Session.ResolveModel(selector);
                if (direct != null)
                    return direct;
                if (occurrenceAliases.TryGetValue(selector,
                        out KeyValuePair<UnityEngine.Object, int> occurrence))
                {
                    RequireParameterNode(occurrence.Key, occurrence.Value, selector);
                    parameterNodeId = occurrence.Value;
                    return occurrence.Key;
                }
                int separator = selector.LastIndexOf(':');
                if (separator > 0 && separator < selector.Length - 1 &&
                    int.TryParse(selector.Substring(separator + 1), out int nodeId))
                {
                    UnityEngine.Object definition = ResolveDefinition(
                        selector.Substring(0, separator), argumentName);
                    RequireParameterNode(definition, nodeId, selector);
                    parameterNodeId = nodeId;
                    return definition;
                }
            }
            throw VmAutomationVFXError.Create(MissingModelCode(selector),
                $"VFX model or parameter occurrence '{selector}' from {argumentName} was not found.");
        }

        internal object ResolveParameterNode(string selector,
            out UnityEngine.Object definition)
        {
            if (string.IsNullOrWhiteSpace(selector))
                throw new ArgumentException("parameterNodeId is required.");
            if (occurrenceAliases.TryGetValue(selector,
                    out KeyValuePair<UnityEngine.Object, int> occurrence))
            {
                definition = occurrence.Key;
                return RequireParameterNode(definition, occurrence.Value, selector);
            }
            int separator = selector.LastIndexOf(':');
            if (separator <= 0 || separator == selector.Length - 1)
                throw new ArgumentException(
                    "parameterNodeId must use '<parameterId>:<nodeId>'.");
            definition = ResolveDefinition(selector.Substring(0, separator));
            if (!int.TryParse(selector.Substring(separator + 1), out int nodeId))
                throw new ArgumentException(
                    $"Parameter occurrence '{selector}' has an invalid node ID.");
            return RequireParameterNode(definition, nodeId, selector);
        }

        internal void RegisterAlias(string alias, UnityEngine.Object model)
        {
            if (string.IsNullOrEmpty(alias))
                return;
            if (!IsValidAlias(alias))
                throw new ArgumentException(
                    $"alias '{alias}' must start with a letter and contain only letters, digits, '_' or '-'.");
            if (aliases.ContainsKey(alias) || occurrenceAliases.ContainsKey(alias) ||
                Session.ResolveModel(alias) != null)
                throw new ArgumentException($"alias '{alias}' is already in use.");
            aliases.Add(alias, model ?? throw new ArgumentNullException(nameof(model)));
        }

        internal void RegisterOccurrenceAlias(string alias,
            UnityEngine.Object parameter, int nodeId)
        {
            if (string.IsNullOrEmpty(alias))
                return;
            if (!IsValidAlias(alias))
                throw new ArgumentException(
                    $"alias '{alias}' must start with a letter and contain only letters, digits, '_' or '-'.");
            if (aliases.ContainsKey(alias) || occurrenceAliases.ContainsKey(alias) ||
                Session.ResolveModel(alias) != null)
                throw new ArgumentException($"alias '{alias}' is already in use.");
            occurrenceAliases.Add(alias,
                new KeyValuePair<UnityEngine.Object, int>(parameter, nodeId));
        }

        internal UnityEngine.Object AddCatalogModel(string kind, string catalogId,
            string parentSelector, int index, Dictionary<string, object> operation)
        {
            object descriptor = ResolveDescriptor(kind, catalogId);
            UnityEngine.Object model = VmAutomationVFXReflection.Invoke(descriptor,
                "CreateInstance") as UnityEngine.Object;
            if (model == null)
                throw new InvalidOperationException(
                    $"VFX catalog item '{catalogId}' did not create a model.");
            try
            {
                object parent = kind == "block"
                    ? ResolveModel(parentSelector, "parentContextId")
                    : Session.Graph;
                if (kind == "block" &&
                    !VmAutomationVFXReflection.HasBaseType(parent.GetType(),
                        VmAutomationVFXReflection.ContextTypeName))
                    throw new ArgumentException(
                        $"parentContextId '{parentSelector}' is not a VFX context.");
                if (kind == "block" && !Convert.ToBoolean(
                        VmAutomationVFXReflection.Invoke(parent, "Accept", model, index)))
                    throw VmAutomationVFXError.Create("block_incompatible",
                        $"VFX block '{catalogId}' is not compatible with context '{parentSelector}' at index {index}.");
                VmAutomationVFXReflection.Invoke(parent, "AddChild", model, index);
                ApplyCommonModelFields(model, operation, "operation");
                return model;
            }
            catch
            {
                UnityEngine.Object.DestroyImmediate(model, true);
                throw;
            }
        }

        internal UnityEngine.Object AddParameter(string catalogId,
            Dictionary<string, object> operation)
        {
            object descriptor = ResolveDescriptor("parameter", catalogId);
            UnityEngine.Object parameter = VmAutomationVFXReflection.Invoke(descriptor,
                "CreateInstance") as UnityEngine.Object;
            if (parameter == null)
                throw new InvalidOperationException(
                    $"VFX parameter catalog item '{catalogId}' did not create a model.");
            try
            {
                VmAutomationVFXReflection.Invoke(Session.Graph, "AddChild", parameter, -1);
                SetParameter(parameter, operation, true);
                return parameter;
            }
            catch
            {
                UnityEngine.Object.DestroyImmediate(parameter, true);
                throw;
            }
        }

        internal void SetParameter(UnityEngine.Object parameter,
            Dictionary<string, object> values, bool requireName)
        {
            string exposedName = GetString(values, "name");
            if (requireName && string.IsNullOrWhiteSpace(exposedName))
                throw new ArgumentException("name is required for add-parameter.");
            if (!string.IsNullOrWhiteSpace(exposedName))
            {
                EnsureUniqueParameterName(parameter, exposedName);
                SetSetting(parameter, "m_ExposedName", exposedName,
                    "operation.name");
            }
            SetOptionalMember(parameter, values, "exposed", "m_Exposed");
            SetOptionalMember(parameter, values, "isOutput", "isOutput");
            SetOptionalMember(parameter, values, "category", "category");
            SetOptionalMember(parameter, values, "order", "order");
            SetOptionalMember(parameter, values, "tooltip", "tooltip");
            SetOptionalMember(parameter, values, "valueFilter", "valueFilter");
            SetOptionalMember(parameter, values, "min", "min");
            SetOptionalMember(parameter, values, "max", "max");
            SetOptionalMember(parameter, values, "enumValues", "enumValues");
            if (values.TryGetValue("value", out object rawValue))
            {
                Type valueType = VmAutomationVFXReflection.Get(parameter, "type") as Type ??
                    throw new MissingMemberException(
                        parameter.GetType().FullName, "type");
                object converted = ConvertValue(rawValue, valueType,
                    "operation.value");
                SetRequiredMember(parameter, "value", converted,
                    "operation.value");
            }
            ApplyCommonModelFields(parameter, values, "operation");
        }

        internal int AddParameterNode(UnityEngine.Object parameter, Vector2 position,
            bool expanded, bool superCollapsed)
        {
            int nodeId = Convert.ToInt32(VmAutomationVFXReflection.Invoke(parameter,
                "AddNode", position));
            object node = VmAutomationVFXReflection.Invoke(parameter, "GetNode", nodeId);
            SetRequiredMember(node, "expanded", expanded, "operation.expanded");
            SetRequiredMember(node, "supecollapsed", superCollapsed,
                "operation.superCollapsed");
            return nodeId;
        }

        internal void RemoveParameterNode(string selector)
        {
            object node = ResolveParameterNode(selector,
                out UnityEngine.Object parameter);
            VmAutomationVFXReflection.Invoke(parameter, "RemoveNode", node);
        }

        internal void RemoveModel(UnityEngine.Object model)
        {
            if (VmAutomationVFXReflection.HasBaseType(model.GetType(),
                    VmAutomationVFXReflection.ParameterTypeName))
            {
                foreach (object node in VmAutomationVFXReflection.Enumerate(
                             VmAutomationVFXReflection.Get(model, "nodes")).ToList())
                    VmAutomationVFXReflection.Invoke(model, "RemoveNode", node);
            }
            Type modelType = VmAutomationVFXReflection.RequireType(
                VmAutomationVFXReflection.ModelTypeName);
            VmAutomationVFXReflection.Invoke(modelType, "RemoveModel", model);
        }

        internal void MoveBlock(UnityEngine.Object block,
            UnityEngine.Object context, int index)
        {
            if (!VmAutomationVFXReflection.HasBaseType(block.GetType(),
                    VmAutomationVFXReflection.BlockTypeName))
                throw new ArgumentException("nodeId does not identify a VFX block.");
            if (!VmAutomationVFXReflection.HasBaseType(context.GetType(),
                    VmAutomationVFXReflection.ContextTypeName))
                throw new ArgumentException(
                    "parentContextId does not identify a VFX context.");
            int childCount = VmAutomationVFXReflection.Enumerate(
                VmAutomationVFXReflection.Get(context, "children")).Count();
            if (index < 0 || index > childCount)
                throw new ArgumentOutOfRangeException(nameof(index),
                    $"index must be between 0 and {childCount}.");
            VmAutomationVFXReflection.Invoke(context, "AddChild", block, index);
        }

        internal void ApplyCommonModelFields(UnityEngine.Object model,
            Dictionary<string, object> values, string path)
        {
            bool isContext = VmAutomationVFXReflection.HasBaseType(model.GetType(),
                VmAutomationVFXReflection.ContextTypeName);
            bool isBlock = VmAutomationVFXReflection.HasBaseType(model.GetType(),
                VmAutomationVFXReflection.BlockTypeName);
            if (isContext && values.ContainsKey("collapsed"))
                throw new ArgumentException(
                    path + ".collapsed is not persistent for VFX contexts; use superCollapsed.");
            if (isBlock && values.ContainsKey("position"))
                throw new ArgumentException(
                    path + ".position is not owned by a VFX block; use move-block to set its context and order.");
            if (isBlock && values.ContainsKey("superCollapsed"))
                throw new ArgumentException(
                    path + ".superCollapsed is not supported by VFX blocks; use collapsed.");
            if (values.TryGetValue("position", out object rawPosition))
                SetTypedMember(model, "position", rawPosition, path + ".position");
            SetOptionalMember(model, values, "collapsed", "collapsed");
            SetOptionalMember(model, values, "superCollapsed", "superCollapsed");
            SetOptionalMember(model, values, "enabled", "enabled");
            if (values.TryGetValue("settings", out object rawSettings))
            {
                Dictionary<string, object> settings = AsDictionary(rawSettings) ??
                    throw new ArgumentException(path + ".settings must be an object.");
                foreach (KeyValuePair<string, object> pair in settings)
                    SetSetting(model, pair.Key, pair.Value,
                        path + ".settings." + pair.Key);
            }
            if (values.TryGetValue("slots", out object rawSlots))
            {
                Dictionary<string, object> slots = AsDictionary(rawSlots) ??
                    throw new ArgumentException(path + ".slots must be an object.");
                foreach (KeyValuePair<string, object> pair in slots)
                    SetSlotValue(model, "input", pair.Key, pair.Value,
                        null, Missing.Value, path + ".slots." + pair.Key);
            }
        }

        internal void SetSetting(UnityEngine.Object model, string name,
            object rawValue, string path)
        {
            object settings = VmAutomationVFXReflection.Invoke(model, "GetSettings", true);
            object setting = VmAutomationVFXReflection.Enumerate(settings).FirstOrDefault(item =>
                string.Equals(VmAutomationVFXReflection.Get(item, "name")?.ToString(), name,
                    StringComparison.Ordinal));
            if (setting == null)
                throw VmAutomationVFXError.Create("setting_not_found",
                    $"{path} is not a setting on {model.GetType().FullName}.");
            FieldInfo field = VmAutomationVFXReflection.Get(setting, "field") as FieldInfo;
            Type valueType = field?.FieldType ??
                             VmAutomationVFXReflection.Get(setting, "value")?.GetType();
            object converted = ConvertValue(rawValue, valueType, path);
            VmAutomationVFXReflection.Invoke(model, "SetSettingValue", name, converted);
        }

        internal void SetSlotValue(UnityEngine.Object model, string direction,
            string path, object rawValue, object rawSpace, object rawCollapsed,
            string valuePath)
        {
            object slot = ResolveSlot(model, direction, path);
            if (rawValue != Missing.Value)
            {
                object property = VmAutomationVFXReflection.Get(slot, "property");
                Type valueType = VmAutomationVFXReflection.Get(property, "type") as Type ??
                    throw new MissingMemberException(
                        slot.GetType().FullName, "property.type");
                object converted = ConvertValue(rawValue, valueType, valuePath);
                SetRequiredMember(slot, "value", converted, valuePath);
            }
            if (rawSpace != null)
                SetTypedMember(slot, "space", rawSpace, valuePath + ".space");
            if (rawCollapsed != Missing.Value)
                SetTypedMember(slot, "collapsed", rawCollapsed,
                    valuePath + ".collapsed");
        }

        internal object ResolveSlot(UnityEngine.Object model, string direction,
            string path)
        {
            string member = string.Equals(direction, "output",
                StringComparison.OrdinalIgnoreCase) ? "outputSlots" :
                string.Equals(direction, "input", StringComparison.OrdinalIgnoreCase)
                    ? "inputSlots" : null;
            if (member == null)
                throw new ArgumentException("direction must be input or output.");
            if (string.IsNullOrEmpty(path))
                throw new ArgumentException("slotPath is required.");
            IReadOnlyList<VmAutomationVFXReflection.SlotReference> all =
                VmAutomationVFXReflection.EnumerateSlots(model, member);
            List<VmAutomationVFXReflection.SlotReference> selectorMatches = all
                .Where(item => string.Equals(item.Selector, path,
                    StringComparison.Ordinal)).ToList();
            if (selectorMatches.Count == 1)
                return selectorMatches[0].Slot;
            if (selectorMatches.Count > 1)
                throw new InvalidOperationException(
                    $"Internal VFX slot selector '{path}' is not unique.");
            if (path.StartsWith("[", StringComparison.Ordinal))
                throw VmAutomationVFXError.Create("slot_not_found",
                    $"{direction} slot selector '{path}' was not found on model '{VmAutomationVFXReflection.SemanticName(model)}'.");

            List<VmAutomationVFXReflection.SlotReference> matches = all.Where(item =>
            {
                object slot = item.Slot;
                string slotPath = VmAutomationVFXReflection.Get(slot, "path")?.ToString();
                string slotName = VmAutomationVFXReflection.Get(
                    VmAutomationVFXReflection.Get(slot, "property"), "name")?.ToString();
                return string.Equals(slotPath, path, StringComparison.Ordinal) ||
                       string.Equals(slotName, path, StringComparison.Ordinal);
            }).ToList();
            if (matches.Count == 0)
                throw VmAutomationVFXError.Create("slot_not_found",
                    $"{direction} slot '{path}' was not found on model '{VmAutomationVFXReflection.SemanticName(model)}'.");
            if (matches.Count > 1)
                throw VmAutomationVFXError.Create("slot_not_found",
                    $"{direction} slot path '{path}' is ambiguous; use one of the exact selectors: {string.Join(", ", matches.Select(item => item.Selector))}.");
            return matches[0].Slot;
        }

        internal void ConnectData(UnityEngine.Object fromModel, string fromPath,
            UnityEngine.Object toModel, string toPath, int? fromParameterNodeId,
            int? toParameterNodeId, out string fromType, out string toType,
            out bool dynamicInputSpecialized)
        {
            object output = ResolveSlot(fromModel, "output", fromPath);
            object input = ResolveSlot(toModel, "input", toPath);
            Type inputTypeBefore =
                VmAutomationVFXDynamicOperatorLinking.GetSlotType(input);
            VmAutomationVFXDynamicOperatorLinking.PrepareInput(
                toModel, input, output);
            input = ResolveSlot(toModel, "input", toPath);
            Type inputTypeAfter =
                VmAutomationVFXDynamicOperatorLinking.GetSlotType(input);
            dynamicInputSpecialized = inputTypeBefore != inputTypeAfter;
            bool canLink = Convert.ToBoolean(VmAutomationVFXReflection.Invoke(output,
                "CanLink", input));
            bool reverseCanLink = Convert.ToBoolean(VmAutomationVFXReflection.Invoke(input,
                "CanLink", output));
            if (!canLink || !reverseCanLink)
                throw VmAutomationVFXError.Create("data_link_incompatible",
                    $"Unity rejected data link {fromPath} -> {toPath}.");
            bool linked = Convert.ToBoolean(VmAutomationVFXReflection.Invoke(output,
                "Link", input));
            if (!linked)
                throw VmAutomationVFXError.Create("data_link_incompatible",
                    $"Unity did not create data link {fromPath} -> {toPath}.");
            AssignParameterOccurrence(fromModel, fromParameterNodeId, output, input);
            AssignParameterOccurrence(toModel, toParameterNodeId, output, input);
            fromType = VmAutomationVFXDynamicOperatorLinking
                .GetSlotType(output)?.FullName ?? "";
            toType = inputTypeAfter?.FullName ?? "";
        }

        internal void DisconnectData(UnityEngine.Object fromModel, string fromPath,
            UnityEngine.Object toModel, string toPath)
        {
            object output = ResolveSlot(fromModel, "output", fromPath);
            object input = ResolveSlot(toModel, "input", toPath);
            if (!VmAutomationVFXReflection.Enumerate(VmAutomationVFXReflection.Get(output,
                    "LinkedSlots")).Contains(input))
                throw VmAutomationVFXError.Create("data_link_incompatible",
                    "The requested VFX data link does not exist.");
            VmAutomationVFXReflection.Invoke(output, "Unlink", input);
            RemoveParameterOccurrenceLink(fromModel, output, input);
            RemoveParameterOccurrenceLink(toModel, output, input);
        }

        internal void ConnectFlow(UnityEngine.Object from, int fromIndex,
            UnityEngine.Object to, int toIndex)
        {
            Type contextType = VmAutomationVFXReflection.RequireType(
                VmAutomationVFXReflection.ContextTypeName);
            bool canLink = Convert.ToBoolean(VmAutomationVFXReflection.Invoke(contextType,
                "CanLink", from, to, fromIndex, toIndex));
            if (!canLink)
                throw VmAutomationVFXError.Create("flow_link_incompatible",
                    $"Unity rejected flow link {fromIndex} -> {toIndex}.");
            VmAutomationVFXReflection.Invoke(from, "LinkTo", to, fromIndex, toIndex);
        }

        internal void DisconnectFlow(UnityEngine.Object from, int fromIndex,
            UnityEngine.Object to, int toIndex)
        {
            List<object> outputSlots = VmAutomationVFXReflection.Enumerate(
                VmAutomationVFXReflection.Get(from, "outputFlowSlot")).ToList();
            if (fromIndex < 0 || fromIndex >= outputSlots.Count)
                throw VmAutomationVFXError.Create("flow_link_incompatible",
                    $"fromIndex {fromIndex} is outside the source context's {outputSlots.Count} output flow slots.");
            bool exists = VmAutomationVFXReflection.Enumerate(VmAutomationVFXReflection.Get(
                    outputSlots[fromIndex], "link")).Any(link =>
                    ReferenceEquals(VmAutomationVFXReflection.Get(link, "context"), to) &&
                    Convert.ToInt32(VmAutomationVFXReflection.Get(link, "slotIndex") ??
                        -1) == toIndex);
            if (!exists)
                throw VmAutomationVFXError.Create("flow_link_incompatible",
                    $"The requested VFX flow link {fromIndex} -> {toIndex} does not exist.");
            VmAutomationVFXReflection.Invoke(from, "UnlinkTo", to, fromIndex, toIndex);
        }

        internal Dictionary<string, object> AliasIds()
        {
            Dictionary<string, object> result = aliases.ToDictionary(pair => pair.Key,
                pair => (object)VmAutomationVFXReflection.StableId(pair.Value),
                StringComparer.Ordinal);
            foreach (KeyValuePair<string, KeyValuePair<UnityEngine.Object, int>> pair
                     in occurrenceAliases)
                result[pair.Key] = VmAutomationVFXReflection.StableId(pair.Value.Key) + ":" +
                                   pair.Value.Value;
            return result;
        }

        internal static Dictionary<string, object> AsDictionary(object value)
        {
            return VmAutomationResponse.ToDictionary(value);
        }

        internal static List<object> AsList(object value)
        {
            if (value is IList list)
                return list.Cast<object>().ToList();
            return null;
        }

        internal static string GetString(Dictionary<string, object> values,
            string key, string defaultValue = "")
        {
            return values != null && values.TryGetValue(key, out object value) &&
                   value != null ? value.ToString() : defaultValue;
        }

        internal static int GetInt(Dictionary<string, object> values, string key,
            int defaultValue = 0)
        {
            return values != null && values.TryGetValue(key, out object value) &&
                   value != null ? (int)VmAutomationVFXValueCodec.ConvertTo(value,
                       typeof(int), key) : defaultValue;
        }

        internal static bool GetBool(Dictionary<string, object> values, string key,
            bool defaultValue = false)
        {
            return values != null && values.TryGetValue(key, out object value) &&
                   value != null ? (bool)VmAutomationVFXValueCodec.ConvertTo(value,
                       typeof(bool), key) : defaultValue;
        }

        internal static Vector2 GetVector2(Dictionary<string, object> values,
            string key, Vector2 defaultValue)
        {
            if (values == null || !values.TryGetValue(key, out object raw))
                return defaultValue;
            return (Vector2)ConvertValue(raw, typeof(Vector2),
                "operation." + key);
        }

        private void EnsureUniqueParameterName(UnityEngine.Object current,
            string name)
        {
            bool duplicate = Session.Models.Any(model => model != current &&
                VmAutomationVFXReflection.HasBaseType(model.GetType(),
                    VmAutomationVFXReflection.ParameterTypeName) &&
                string.Equals(VmAutomationVFXReflection.Get(model, "exposedName")?.ToString(),
                    name, StringComparison.Ordinal));
            if (duplicate)
                throw VmAutomationVFXError.Create("parameter_name_conflict",
                    $"A VFX parameter named '{name}' already exists.");
        }

        private static void AssignParameterOccurrence(UnityEngine.Object model,
            int? parameterNodeId, object output, object input)
        {
            if (!VmAutomationVFXReflection.HasBaseType(model.GetType(),
                    VmAutomationVFXReflection.ParameterTypeName))
                return;
            if (!parameterNodeId.HasValue)
                throw new ArgumentException(
                    "A parameter data connection requires the exact parameter occurrence ID.");
            object targetNode = RequireParameterNode(model, parameterNodeId.Value,
                VmAutomationVFXReflection.StableId(model) + ":" + parameterNodeId.Value);
            RemoveParameterOccurrenceLink(model, output, input);
            object list = VmAutomationVFXReflection.Get(targetNode, "linkedSlots");
            if (list == null)
            {
                Type pairType = model.GetType().GetNestedType("NodeLinkedSlot",
                    BindingFlags.Public | BindingFlags.NonPublic);
                Type listType = typeof(List<>).MakeGenericType(pairType);
                list = Activator.CreateInstance(listType);
                SetRequiredMember(targetNode, "linkedSlots", list,
                    "parameterNode.linkedSlots");
            }
            Type elementType = list.GetType().GetGenericArguments()[0];
            object pair = Activator.CreateInstance(elementType);
            SetRequiredMember(pair, "outputSlot", output,
                "parameterNode.linkedSlots.outputSlot");
            SetRequiredMember(pair, "inputSlot", input,
                "parameterNode.linkedSlots.inputSlot");
            list.GetType().GetMethod("Add")?.Invoke(list, new[] { pair });
        }

        private static object RequireParameterNode(UnityEngine.Object parameter,
            int nodeId, string selector)
        {
            object node = VmAutomationVFXReflection.Invoke(parameter, "GetNode", nodeId);
            if (node == null)
                throw VmAutomationVFXError.Create("model_not_found",
                    $"Parameter occurrence '{selector}' was not found.");
            return node;
        }

        private static void RemoveParameterOccurrenceLink(UnityEngine.Object model,
            object output, object input)
        {
            if (!VmAutomationVFXReflection.HasBaseType(model.GetType(),
                    VmAutomationVFXReflection.ParameterTypeName))
                return;
            foreach (object node in VmAutomationVFXReflection.Enumerate(
                         VmAutomationVFXReflection.Get(model, "nodes")))
            {
                object list = VmAutomationVFXReflection.Get(node, "linkedSlots");
                if (!(list is IList mutable))
                    continue;
                for (int index = mutable.Count - 1; index >= 0; index--)
                {
                    object pair = mutable[index];
                    if (ReferenceEquals(VmAutomationVFXReflection.Get(pair, "outputSlot"),
                            output) &&
                        ReferenceEquals(VmAutomationVFXReflection.Get(pair, "inputSlot"),
                            input))
                        mutable.RemoveAt(index);
                }
            }
        }

        private static void SetOptionalMember(object target,
            Dictionary<string, object> values, string inputKey, string memberName)
        {
            if (values.TryGetValue(inputKey, out object rawValue))
                SetTypedMember(target, memberName, rawValue,
                    "operation." + inputKey);
        }

        private static void SetTypedMember(object target, string memberName,
            object rawValue, string path)
        {
            Type type = VmAutomationVFXReflection.GetMemberType(target, memberName);
            if (type == null)
                throw new MissingMemberException(target.GetType().FullName, memberName);
            object converted = ConvertValue(rawValue, type, path);
            SetRequiredMember(target, memberName, converted, path);
        }

        private static void SetRequiredMember(object target, string memberName,
            object value, string path)
        {
            if (!VmAutomationVFXReflection.TrySet(target, memberName, value))
                throw new MissingMemberException(
                    $"Unable to write {path} ({target.GetType().FullName}.{memberName}).");
        }

        private static bool IsValidAlias(string alias)
        {
            if (string.IsNullOrEmpty(alias) || !char.IsLetter(alias[0]))
                return false;
            return alias.All(character => char.IsLetterOrDigit(character) ||
                                           character == '_' || character == '-');
        }

        private static string ErrorText(object response)
        {
            Dictionary<string, object> dictionary = AsDictionary(response);
            return dictionary != null && dictionary.TryGetValue("error", out object error)
                ? error?.ToString() ?? "Unknown VFX error."
                : response?.ToString() ?? "Unknown VFX error.";
        }

        private static string ErrorCode(object response, string fallback)
        {
            return VmAutomationResponse.TryGetError(response, out string _,
                out string errorCode, out bool _) &&
                   !string.IsNullOrEmpty(errorCode) ? errorCode : fallback;
        }

        private static string MissingModelCode(string selector)
        {
            return IsValidAlias(selector ?? "") ? "alias_not_found" :
                "model_not_found";
        }

        private object ResolveDescriptor(string kind, string catalogId)
        {
            string key = kind + "|" + catalogId;
            if (descriptorCache.TryGetValue(key, out object descriptor))
                return descriptor;
            if (!VmAutomationVFXGraphCatalogCommands.TryResolveModelDescriptor(kind,
                    catalogId, true, out descriptor, out object resolveError))
                throw VmAutomationVFXError.Create(ErrorCode(resolveError,
                    "catalog_item_not_found"), ErrorText(resolveError));
            descriptorCache.Add(key, descriptor);
            return descriptor;
        }

        private static object ConvertValue(object rawValue, Type targetType,
            string path)
        {
            try
            {
                return VmAutomationVFXValueCodec.ConvertTo(rawValue, targetType, path);
            }
            catch (VmAutomationVFXError.Failure)
            {
                throw;
            }
            catch (ArgumentException exception)
            {
                throw VmAutomationVFXError.Create("value_type_mismatch",
                    exception.Message);
            }
        }
    }
}
