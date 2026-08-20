using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace VMUnityAutomation.Editor
{
    internal static class VmAutomationVFXGraphCatalogCommands
    {
        private static readonly string[] AllowedKinds =
        {
            "asset-kind", "template", "context", "block", "operator",
            "parameter", "property-binder", "event-binder",
            "output-event-handler", "spawner-callback",
        };

        internal static object Catalog(Dictionary<string, object> args)
        {
            if (!TryValidateKeys(args, new[]
                {
                    "kind", "query", "category", "includeExperimental",
                    "contextCatalogId", "catalogId", "includeDetails",
                    "offset", "limit", "settingOffset",
                    "maxSettingsPerItem", "inputSlotOffset",
                    "outputSlotOffset", "maxSlotsPerItem", "_agentId",
                }, out object keyError))
                return keyError;
            if (!VmAutomationVFXReflection.IsAvailable)
                return VmAutomationResponse.Error(
                    "VFX Graph is not available. Install com.unity.visualeffectgraph.",
                    "capability_unavailable");

            string kind = GetString(args, "kind").ToLowerInvariant();
            if (!string.IsNullOrEmpty(kind) && !AllowedKinds.Contains(kind))
                return VmAutomationResponse.Error(
                    $"kind must be one of: {string.Join(", ", AllowedKinds)}.",
                    "invalid_arguments");
            if ((kind == "context" || kind == "block" || kind == "operator" ||
                 kind == "parameter") && MissingTypes(
                    VmAutomationVFXReflection.LibraryTypeName,
                    VmAutomationVFXReflection.ModelTypeName).Count > 0)
                return VmAutomationResponse.Error(
                    "The installed VFX Graph version does not expose graph catalog authoring symbols.",
                    "unsupported_vfx_version");
            if (kind == "template" && MissingTypes(
                    VmAutomationVFXReflection.AssetUtilityTypeName,
                    VmAutomationVFXReflection.TemplateHelperTypeName,
                    VmAutomationVFXReflection.TemplateDescriptorTypeName).Count > 0)
                return VmAutomationResponse.Error(
                    "The installed VFX Graph version does not expose template catalog symbols.",
                    "unsupported_vfx_version");
            if (ExtensionBaseTypeName(kind) is string extensionBaseType &&
                VmAutomationVFXReflection.FindType(extensionBaseType) == null)
                return VmAutomationResponse.Error(
                    $"The installed VFX Graph version does not expose {kind} symbols.",
                    "unsupported_vfx_version");
            int offset;
            int limit;
            int settingOffset;
            int maxSettings;
            int inputSlotOffset;
            int outputSlotOffset;
            int maxSlots;
            try
            {
                if (!TryGetPage(args, out offset, out limit,
                        out object pageError))
                    return pageError;
                if (!TryGetDetailPage(args, out settingOffset,
                        out maxSettings, out inputSlotOffset,
                        out outputSlotOffset, out maxSlots,
                        out object detailPageError))
                    return detailPageError;
            }
            catch (Exception exception)
            {
                return VmAutomationResponse.Error(VmAutomationVFXReflection.Unwrap(exception).Message,
                    "invalid_arguments");
            }

            try
            {
                bool includeDetails = GetBool(args, "includeDetails", false);
                if (includeDetails && limit > 100)
                    return VmAutomationResponse.Error(
                        "includeDetails supports at most 100 catalog items per request; reduce limit or select an exact catalogId.",
                        "invalid_arguments");
                List<Dictionary<string, object>> all = BuildCatalog(kind,
                    GetBool(args, "includeExperimental", false),
                    out Dictionary<string, object> descriptorsById);
                string query = GetString(args, "query");
                string category = GetString(args, "category");
                string catalogId = GetString(args, "catalogId");
                if (!string.IsNullOrEmpty(catalogId))
                {
                    List<Dictionary<string, object>> exact = all.Where(item =>
                        string.Equals(
                        GetItemString(item, "catalogId"), catalogId,
                        StringComparison.Ordinal)).ToList();
                    if (exact.Count == 0)
                        return VmAutomationResponse.Error(
                            $"VFX catalog item '{catalogId}' was not found.",
                            "catalog_item_not_found");
                    if (exact.Count > 1)
                        return VmAutomationResponse.Error(
                            $"VFX catalog item '{catalogId}' is ambiguous.",
                            "catalog_item_ambiguous");
                    all = exact;
                }
                if (!string.IsNullOrEmpty(query))
                {
                    all = all.Where(item => SearchText(item).IndexOf(query,
                        StringComparison.OrdinalIgnoreCase) >= 0).ToList();
                }
                if (!string.IsNullOrEmpty(category))
                {
                    all = all.Where(item => string.Equals(
                        GetItemString(item, "category"), category,
                        StringComparison.OrdinalIgnoreCase)).ToList();
                }

                string contextCatalogId = GetString(args, "contextCatalogId");
                if (!string.IsNullOrEmpty(contextCatalogId))
                {
                    if (!string.IsNullOrEmpty(kind) && kind != "block")
                        return VmAutomationResponse.Error(
                            "contextCatalogId is only valid when kind is block or omitted.",
                            "invalid_arguments");
                    if (!TryResolveModelDescriptor("context", contextCatalogId,
                            true, out object contextDescriptor, out object resolveError))
                        return resolveError;
                    object contextModel = VmAutomationVFXReflection.Get(contextDescriptor,
                        "unTypedModel");
                    all = all.Where(item => GetItemString(item, "kind") != "block" ||
                        descriptorsById.TryGetValue(GetItemString(item,
                            "catalogId"), out object blockDescriptor) &&
                        IsBlockCompatible(contextModel, blockDescriptor)).ToList();
                }

                all = all.OrderBy(item => GetItemString(item, "kind"),
                        StringComparer.Ordinal)
                    .ThenBy(item => GetItemString(item, "category"),
                        StringComparer.OrdinalIgnoreCase)
                    .ThenBy(item => GetItemString(item, "name"),
                        StringComparer.OrdinalIgnoreCase)
                    .ThenBy(item => GetItemString(item, "catalogId"),
                        StringComparer.Ordinal)
                    .ToList();
                List<Dictionary<string, object>> page = all.Skip(offset)
                    .Take(limit).ToList();
                if (includeDetails)
                {
                    page = page.Select(item =>
                    {
                        string itemId = GetItemString(item, "catalogId");
                        return descriptorsById.TryGetValue(itemId,
                                out object descriptor)
                            ? DescriptorSummary(GetItemString(item, "kind"),
                                descriptor, true, settingOffset, maxSettings,
                                inputSlotOffset, outputSlotOffset, maxSlots)
                            : item;
                    }).ToList();
                }
                return new Dictionary<string, object>
                {
                    { "success", true },
                    { "vfxGraphVersion", GetVFXPackageVersion() },
                    { "kind", kind },
                    { "query", query },
                    { "category", category },
                    { "includeExperimental", GetBool(args,
                        "includeExperimental", false) },
                    { "catalogId", catalogId },
                    { "detailsIncluded", includeDetails },
                    { "settingOffset", settingOffset },
                    { "maxSettingsPerItem", maxSettings },
                    { "inputSlotOffset", inputSlotOffset },
                    { "outputSlotOffset", outputSlotOffset },
                    { "maxSlotsPerItem", maxSlots },
                    { "total", all.Count },
                    { "offset", offset },
                    { "limit", limit },
                    { "returned", page.Count },
                    { "hasMore", offset + page.Count < all.Count },
                    { "nextOffset", offset + page.Count < all.Count
                        ? (object)(offset + page.Count) : null },
                    { "items", page },
                    { "capabilities", VmAutomationVFXReflection.CapabilitySummaries() },
                    { "relatedRoutes", RelatedRoutes() },
                };
            }
            catch (Exception exception)
            {
                return VmAutomationVFXError.Response(exception,
                    "unsupported_vfx_version");
            }
        }

        internal static bool TryResolveModelDescriptor(string kind, string catalogId,
            bool includeExperimental, out object descriptor, out object error)
        {
            descriptor = null;
            if (kind != "context" && kind != "block" && kind != "operator" &&
                kind != "parameter")
            {
                error = VmAutomationResponse.Error(
                    $"'{kind}' is not a VFX model catalog kind.",
                    "invalid_arguments");
                return false;
            }
            List<string> missing = MissingTypes(VmAutomationVFXReflection.LibraryTypeName,
                VmAutomationVFXReflection.ModelTypeName);
            if (missing.Count > 0)
            {
                error = VmAutomationResponse.Error(
                    "The installed VFX Graph version does not expose required catalog symbols: " +
                    string.Join(", ", missing) + ".",
                    "unsupported_vfx_version");
                return false;
            }

            List<object> matches = EnumerateDescriptors(kind, includeExperimental)
                .Where(item => string.Equals(CatalogId(kind, item), catalogId,
                    StringComparison.Ordinal))
                .Take(2).ToList();
            if (matches.Count == 0)
            {
                error = VmAutomationResponse.Error(
                    $"VFX {kind} catalog item '{catalogId}' was not found.",
                    "catalog_item_not_found");
                return false;
            }
            if (matches.Count > 1)
            {
                error = VmAutomationResponse.Error(
                    $"VFX {kind} catalog item '{catalogId}' is ambiguous.",
                    "catalog_item_ambiguous");
                return false;
            }
            descriptor = matches[0];
            error = null;
            return true;
        }

        internal static bool TryResolveTemplate(string templateId,
            out string templatePath, out object error)
        {
            templatePath = "";
            List<string> missing = MissingTypes(
                VmAutomationVFXReflection.TemplateHelperTypeName,
                VmAutomationVFXReflection.TemplateDescriptorTypeName);
            if (missing.Count > 0)
            {
                error = VmAutomationResponse.Error(
                    "The installed VFX Graph version does not expose required template symbols: " +
                    string.Join(", ", missing) + ".",
                    "unsupported_vfx_version");
                return false;
            }
            if (string.IsNullOrEmpty(templateId) ||
                !templateId.StartsWith("template:", StringComparison.Ordinal))
            {
                error = VmAutomationResponse.Error(
                    $"VFX template catalog item '{templateId}' was not found.",
                    "catalog_item_not_found");
                return false;
            }
            string guid = templateId.Substring("template:".Length);
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path) ||
                !TryGetTemplateDescriptor(path, out object _))
            {
                error = VmAutomationResponse.Error(
                    $"VFX template catalog item '{templateId}' was not found.",
                    "catalog_item_not_found");
                return false;
            }
            templatePath = path;
            error = null;
            return true;
        }

        internal static string CatalogId(string kind, object descriptor)
        {
            Type modelType = VmAutomationVFXReflection.Get(descriptor, "modelType") as Type;
            object variant = VmAutomationVFXReflection.Get(descriptor, "variant");
            string name = VmAutomationVFXReflection.Get(descriptor, "name")?.ToString() ?? "";
            string category = VmAutomationVFXReflection.Get(descriptor, "category")?.ToString() ?? "";
            string settings = string.Join(";", Bounded(
                    VmAutomationVFXReflection.Enumerate(
                        VmAutomationVFXReflection.Get(variant, "settings")),
                    VmAutomationVFXLimits.CatalogMetadataPerItem, "variant settings")
                .Select(SettingPairText));
            string identity = string.Join("|", kind, modelType?.FullName ?? "",
                category, name, settings);
            return kind + ":" + (modelType?.FullName ?? "unknown") + ":" +
                   Hash128.Compute(identity).ToString().Substring(0, 16);
        }

        private static List<Dictionary<string, object>> BuildCatalog(string kind,
            bool includeExperimental, out Dictionary<string, object> descriptorsById)
        {
            var result = new List<Dictionary<string, object>>();
            descriptorsById = new Dictionary<string, object>(StringComparer.Ordinal);
            Dictionary<string, List<Dictionary<string, object>>> extensionTypes =
                null;
            IEnumerable<string> requested = string.IsNullOrEmpty(kind)
                ? AllowedKinds
                : new[] { kind };
            foreach (string requestedKind in requested)
            {
                switch (requestedKind)
                {
                    case "asset-kind":
                        result.AddRange(AssetKinds());
                        break;
                    case "template":
                        if (MissingTypes(VmAutomationVFXReflection.AssetUtilityTypeName,
                                VmAutomationVFXReflection.TemplateHelperTypeName,
                                VmAutomationVFXReflection.TemplateDescriptorTypeName)
                            .Count == 0)
                            result.AddRange(Templates(
                                VmAutomationVFXLimits.CatalogItems - result.Count));
                        break;
                    case "context":
                    case "block":
                    case "operator":
                    case "parameter":
                        if (MissingTypes(VmAutomationVFXReflection.LibraryTypeName,
                                VmAutomationVFXReflection.ModelTypeName).Count > 0)
                            break;
                        foreach (object descriptor in EnumerateDescriptors(
                                     requestedKind, includeExperimental,
                                     VmAutomationVFXLimits.CatalogItems - result.Count))
                        {
                            Dictionary<string, object> summary = DescriptorSummary(
                                requestedKind, descriptor, false, 0, 1, 0, 0, 1);
                            string id = GetItemString(summary, "catalogId");
                            if (descriptorsById.ContainsKey(id))
                                throw new InvalidOperationException(
                                    $"Duplicate VFX catalog ID '{id}'.");
                            descriptorsById.Add(id, descriptor);
                            result.Add(summary);
                        }
                        break;
                    case "property-binder":
                    case "event-binder":
                    case "output-event-handler":
                    case "spawner-callback":
                        if (extensionTypes == null)
                            extensionTypes = DiscoverExtensionTypes();
                        if (extensionTypes.TryGetValue(requestedKind,
                                out List<Dictionary<string, object>> discovered))
                        {
                            if (discovered.Count > VmAutomationVFXLimits.CatalogItems -
                                result.Count)
                                throw VmAutomationVFXError.Create("response_too_large",
                                    $"The installed {requestedKind} catalog exceeds the remaining catalog budget.");
                            result.AddRange(discovered);
                        }
                        break;
                }
                if (result.Count > VmAutomationVFXLimits.CatalogItems)
                    throw VmAutomationVFXError.Create("response_too_large",
                        $"The installed VFX catalog exposes more than {VmAutomationVFXLimits.CatalogItems} items.");
            }
            return result;
        }

        private static IEnumerable<object> EnumerateDescriptors(string kind,
            bool includeExperimental, int maximum = VmAutomationVFXLimits.CatalogItems)
        {
            Type libraryType = VmAutomationVFXReflection.FindType(
                VmAutomationVFXReflection.LibraryTypeName);
            VmAutomationVFXReflection.Invoke(libraryType, "Load");
            string fieldName;
            switch (kind)
            {
                case "context": fieldName = "m_ContextDescs"; break;
                case "block": fieldName = "m_BlockDescs"; break;
                case "operator": fieldName = "m_OperatorDescs"; break;
                case "parameter": fieldName = "m_ParametersDescs"; break;
                default: return Enumerable.Empty<object>();
            }
            var scanned = new List<object>();
            foreach (object root in VmAutomationVFXReflection.Enumerate(
                         VmAutomationVFXReflection.Get(libraryType, fieldName)))
                AppendDescriptor(root, 0, scanned, maximum);
            return scanned.Where(descriptor => includeExperimental ||
                !GetExperimental(descriptor));
        }

        private static void AppendDescriptor(object descriptor, int depth,
            ICollection<object> result, int maximum)
        {
            if (descriptor == null)
                return;
            if (depth > VmAutomationVFXLimits.SlotDepth)
                throw VmAutomationVFXError.Create("response_too_large",
                    $"A VFX catalog variant tree exceeds depth {VmAutomationVFXLimits.SlotDepth}.");
            if (result.Count >= maximum)
                throw VmAutomationVFXError.Create("response_too_large",
                    $"The installed VFX catalog exposes more than {VmAutomationVFXLimits.CatalogItems} items.");
            result.Add(descriptor);
            foreach (object child in VmAutomationVFXReflection.Enumerate(
                         VmAutomationVFXReflection.Get(descriptor, "subVariantDescriptors")))
                AppendDescriptor(child, depth + 1, result, maximum);
        }

        private static Dictionary<string, object> DescriptorSummary(string kind,
            object descriptor, bool includeDetails, int settingOffset,
            int maxSettings, int inputSlotOffset, int outputSlotOffset,
            int maxSlots)
        {
            Type modelType = VmAutomationVFXReflection.Get(descriptor, "modelType") as Type;
            object variant = VmAutomationVFXReflection.Get(descriptor, "variant");
            bool documentationAvailable = TryDocumentationUrl(variant,
                out string documentationUrl);
            var result = new Dictionary<string, object>
            {
                { "kind", kind },
                { "catalogId", CatalogId(kind, descriptor) },
                { "name", VmAutomationVFXReflection.Get(descriptor, "name")?.ToString() ?? "" },
                { "category", VmAutomationVFXReflection.Get(descriptor, "category")?.ToString() ?? "" },
                { "modelType", modelType?.FullName ?? "" },
                { "synonyms", Bounded(VmAutomationVFXReflection.Enumerate(
                    VmAutomationVFXReflection.Get(descriptor, "synonyms")),
                    VmAutomationVFXLimits.CatalogMetadataPerItem, "synonyms")
                    .Select(value => value?.ToString() ?? "").ToList() },
                { "experimental", GetExperimental(descriptor) },
                { "variantSettings", Bounded(VmAutomationVFXReflection.Enumerate(
                    VmAutomationVFXReflection.Get(variant, "settings")),
                    VmAutomationVFXLimits.CatalogMetadataPerItem, "variant settings")
                    .Select(VariantSettingSummary).ToList() },
                { "documentationUrl", documentationUrl },
                { "documentationUrlAvailable", documentationAvailable },
                { "detailsAvailable", true },
            };
            if (!includeDetails)
                return result;

            object model = VmAutomationVFXReflection.Get(descriptor, "unTypedModel") ??
                throw new InvalidOperationException(
                    $"VFX catalog item '{result["catalogId"]}' could not be instantiated.");
            AddDetailPage(result, "setting", "settings",
                SettingSummaries(model), settingOffset, maxSettings);
            AddDetailPage(result, "inputSlot", "inputs",
                SlotDefinitions(model, "inputSlots"), inputSlotOffset, maxSlots);
            AddDetailPage(result, "outputSlot", "outputs",
                SlotDefinitions(model, "outputSlots"), outputSlotOffset, maxSlots);
            if (kind == "context")
            {
                result["contextType"] = VmAutomationVFXReflection.Get(model,
                    "contextType")?.ToString() ?? "";
                result["compatibleContextType"] = VmAutomationVFXReflection.Get(model,
                    "compatibleContextType")?.ToString() ?? "";
                result["inputDataType"] = VmAutomationVFXReflection.Get(model,
                    "inputType")?.ToString() ?? "";
                result["outputDataType"] = VmAutomationVFXReflection.Get(model,
                    "outputType")?.ToString() ?? "";
                result["ownedDataType"] = VmAutomationVFXReflection.Get(model,
                    "ownedType")?.ToString() ?? "";
            }
            else if (kind == "block")
            {
                result["compatibleContextType"] = VmAutomationVFXReflection.Get(model,
                    "compatibleContexts")?.ToString() ?? "";
                result["compatibleDataType"] = VmAutomationVFXReflection.Get(model,
                    "compatibleData")?.ToString() ?? "";
            }
            else if (kind == "parameter")
            {
                result["valueType"] = VmAutomationVFXReflection.Get(model, "type")
                    is Type valueType ? valueType.FullName : modelType?.FullName ?? "";
            }
            return result;
        }

        private static List<Dictionary<string, object>> SettingSummaries(object model)
        {
            if (model == null)
                return new List<Dictionary<string, object>>();
            object settings = VmAutomationVFXReflection.Invoke(model, "GetSettings", true);
            return Bounded(VmAutomationVFXReflection.Enumerate(settings),
                    VmAutomationVFXLimits.SettingsPerModel, "model settings")
                .Select(setting =>
            {
                FieldInfo field = VmAutomationVFXReflection.Get(setting, "field") as FieldInfo;
                object value = VmAutomationVFXReflection.Get(setting, "value");
                object visibility = VmAutomationVFXReflection.Get(setting, "visibility");
                return new Dictionary<string, object>
                {
                    { "name", VmAutomationVFXReflection.Get(setting, "name")?.ToString() ?? "" },
                    { "type", field?.FieldType.FullName ?? value?.GetType().FullName ?? "" },
                    { "value", VmAutomationVFXValueCodec.Sanitize(value) },
                    { "visibility", visibility?.ToString() ?? "" },
                    { "readOnly", visibility?.ToString().Contains("ReadOnly") == true },
                    { "enumValues", field?.FieldType.IsEnum == true
                        ? (object)Enum.GetNames(field.FieldType).ToList()
                        : new List<string>() },
                };
            }).ToList();
        }

        private static List<Dictionary<string, object>> SlotDefinitions(object model,
            string memberName)
        {
            return VmAutomationVFXReflection.EnumerateSlots(model, memberName)
                .Select(reference => SlotDefinition(reference.Slot,
                    reference.Selector)).ToList();
        }

        private static Dictionary<string, object> SlotDefinition(object slot,
            string selector)
        {
            object property = VmAutomationVFXReflection.Get(slot, "property");
            Type type = VmAutomationVFXReflection.Get(property, "type") as Type;
            int lastSegment = selector.LastIndexOf('[',
                selector.Length - 2);
            string parentSelector = lastSegment >= 0
                ? selector.Substring(0, lastSegment) : "";
            List<object> children = VmAutomationVFXReflection.Enumerate(
                VmAutomationVFXReflection.Get(slot, "children")).ToList();
            return new Dictionary<string, object>
            {
                { "name", VmAutomationVFXReflection.Get(property, "name")?.ToString() ?? "" },
                { "path", VmAutomationVFXReflection.Get(slot, "path")?.ToString() ?? "" },
                { "selector", selector },
                { "parentSelector", parentSelector },
                { "depth", selector.Count(character => character == '[') - 1 },
                { "type", type?.FullName ?? "" },
                { "spaceable", VmAutomationVFXReflection.Get(slot, "spaceable") ?? false },
                { "defaultValue", VmAutomationVFXValueCodec.Sanitize(
                    VmAutomationVFXReflection.Get(slot, "value")) },
                { "childSelectors", children.Select((child, index) =>
                    selector + $"[{index}]").ToList() },
            };
        }

        private static IEnumerable<Dictionary<string, object>> AssetKinds()
        {
            yield return CatalogLiteral("asset-kind", "asset-kind:graph", "graph",
                "Graph", "Visual Effect Graph");
            yield return CatalogLiteral("asset-kind", "asset-kind:block-subgraph",
                "block-subgraph", "Subgraph", "VFX Block Subgraph");
            yield return CatalogLiteral("asset-kind", "asset-kind:operator-subgraph",
                "operator-subgraph", "Subgraph", "VFX Operator Subgraph");
        }

        private static IEnumerable<Dictionary<string, object>> Templates(
            int maximum)
        {
            Type utilityType = VmAutomationVFXReflection.RequireType(
                VmAutomationVFXReflection.AssetUtilityTypeName);
            string templatePath = VmAutomationVFXReflection.Get(utilityType, "templatePath")
                ?.ToString();
            if (string.IsNullOrEmpty(templatePath))
                throw new MissingMemberException(utilityType.FullName,
                    "templatePath");
            string folder = templatePath.TrimEnd('/', '\\');
            string[] guids = AssetDatabase.FindAssets("t:VisualEffectAsset",
                new[] { folder });
            if (guids.Length > maximum)
                throw VmAutomationVFXError.Create("response_too_large",
                    $"The installed VFX template catalog exceeds the remaining {maximum}-item catalog budget.");
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!TryGetTemplateDescriptor(path, out object descriptor))
                    continue;
                yield return new Dictionary<string, object>
                {
                    { "kind", "template" },
                    { "catalogId", "template:" + guid },
                    { "name", VmAutomationVFXReflection.Get(descriptor, "name")?.ToString() ??
                              System.IO.Path.GetFileNameWithoutExtension(path) },
                    { "category", VmAutomationVFXReflection.Get(descriptor, "category")?.ToString() ??
                                  "Default VFX Graph Templates" },
                    { "description", VmAutomationVFXReflection.Get(descriptor,
                        "description")?.ToString() ?? "" },
                    { "assetPath", path },
                    { "assetKind", "graph" },
                    { "experimental", false },
                };
            }
        }

        private static bool TryGetTemplateDescriptor(string path,
            out object descriptor)
        {
            descriptor = null;
            Type helperType = VmAutomationVFXReflection.FindType(
                VmAutomationVFXReflection.TemplateHelperTypeName);
            Type descriptorType = VmAutomationVFXReflection.FindType(
                VmAutomationVFXReflection.TemplateDescriptorTypeName);
            if (helperType == null || descriptorType == null)
                return false;
            MethodInfo method = helperType?.GetMethod("TryGetTemplate",
                BindingFlags.Static | BindingFlags.Public, null,
                new[]
                {
                    typeof(string),
                    descriptorType.MakeByRefType(),
                }, null);
            if (method == null)
                return false;
            descriptorType = method.GetParameters()[1].ParameterType
                .GetElementType();
            object[] arguments = { path, Activator.CreateInstance(descriptorType) };
            bool success = (bool)method.Invoke(null, arguments);
            descriptor = arguments[1];
            return success;
        }

        private static Dictionary<string, List<Dictionary<string, object>>>
            DiscoverExtensionTypes()
        {
            Dictionary<string, Type> bases = AllowedKinds
                .Select(kind => new { kind, typeName = ExtensionBaseTypeName(kind) })
                .Where(item => item.typeName != null)
                .Select(item => new
                {
                    item.kind,
                    type = VmAutomationVFXReflection.FindType(item.typeName),
                })
                .Where(item => item.type != null)
                .ToDictionary(item => item.kind, item => item.type,
                    StringComparer.Ordinal);
            var result = bases.Keys.ToDictionary(kind => kind,
                kind => new List<Dictionary<string, object>>(),
                StringComparer.Ordinal);
            Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
            if (assemblies.Length > VmAutomationVFXLimits.LoadedAssemblies)
                throw VmAutomationVFXError.Create("response_too_large",
                    $"The editor has more than {VmAutomationVFXLimits.LoadedAssemblies} loaded assemblies; VFX extension discovery is bounded.");
            int scannedTypes = 0;
            foreach (Assembly assembly in assemblies)
            foreach (Type type in VmAutomationVFXReflection.GetLoadableTypes(assembly))
            {
                scannedTypes++;
                if (scannedTypes > VmAutomationVFXLimits.LoadedTypes)
                    throw VmAutomationVFXError.Create("response_too_large",
                        $"VFX extension discovery scanned more than {VmAutomationVFXLimits.LoadedTypes} loaded types.");
                if (type == null || type.IsAbstract)
                    continue;
                foreach (KeyValuePair<string, Type> pair in bases)
                {
                    if (!pair.Value.IsAssignableFrom(type))
                        continue;
                    result[pair.Key].Add(ExtensionTypeSummary(pair.Key,
                        pair.Value, type));
                }
            }
            int total = result.Values.Sum(items => items.Count);
            if (total > VmAutomationVFXLimits.CatalogItems)
                throw VmAutomationVFXError.Create("response_too_large",
                    $"Installed VFX extension discovery found more than {VmAutomationVFXLimits.CatalogItems} concrete types.");
            foreach (List<Dictionary<string, object>> items in result.Values)
                items.Sort((left, right) => string.Compare(
                    GetItemString(left, "fullTypeName"),
                    GetItemString(right, "fullTypeName"),
                    StringComparison.Ordinal));
            return result;
        }

        private static Dictionary<string, object> ExtensionTypeSummary(
            string kind, Type baseType, Type type)
        {
            string name = ObjectNames.NicifyVariableName(type.Name
                .Replace("VFX", "").Replace("Binder", "")
                .Replace("Handler", "").Replace("Callbacks", ""));
            string category = "";
            if (kind == "property-binder" || kind == "event-binder")
            {
                object binderAttribute = type.GetCustomAttributes(false)
                    .FirstOrDefault(attribute => attribute.GetType().Name ==
                                                 "VFXBinderAttribute");
                string menuPath = VmAutomationVFXReflection.Get(binderAttribute, "menuPath")
                                  ?.ToString() ??
                                  VmAutomationVFXReflection.Get(binderAttribute, "path")
                                  ?.ToString() ?? "";
                if (!string.IsNullOrEmpty(menuPath))
                {
                    int slash = menuPath.LastIndexOf('/');
                    if (slash >= 0)
                    {
                        category = menuPath.Substring(0, slash);
                        name = menuPath.Substring(slash + 1);
                    }
                    else
                    {
                        name = menuPath;
                    }
                }
            }
            return new Dictionary<string, object>
            {
                { "kind", kind },
                { "catalogId", kind + ":" + type.FullName },
                { "name", name },
                { "category", category },
                { "fullTypeName", type.FullName },
                { "baseTypeName", baseType.FullName },
                { "experimental", false },
                { "ownerRoutes", ExtensionOwnerRoutes(kind) },
            };
        }

        private static List<string> ExtensionOwnerRoutes(string kind)
        {
            if (kind == "spawner-callback")
                return new List<string>
                {
                    "vfxgraph/catalog", "vfxgraph/transaction",
                };
            return new List<string>
            {
                "component/add", "component/get-properties",
                "component/set-property", "component/set-reference",
                "prefab-asset/transaction-edit",
            };
        }

        private static string ExtensionBaseTypeName(string kind)
        {
            switch (kind)
            {
                case "property-binder":
                    return VmAutomationVFXReflection.PropertyBinderBaseTypeName;
                case "event-binder":
                    return VmAutomationVFXReflection.EventBinderBaseTypeName;
                case "output-event-handler":
                    return VmAutomationVFXReflection.OutputEventHandlerBaseTypeName;
                case "spawner-callback":
                    return VmAutomationVFXReflection.SpawnerCallbacksBaseTypeName;
                default:
                    return null;
            }
        }

        private static bool IsBlockCompatible(object contextModel,
            object blockDescriptor)
        {
            object blockModel = VmAutomationVFXReflection.Get(blockDescriptor, "unTypedModel");
            return (bool)VmAutomationVFXReflection.Invoke(contextModel, "Accept", blockModel,
                -1);
        }

        private static bool GetExperimental(object descriptor)
        {
            object infoAttribute = VmAutomationVFXReflection.Get(descriptor,
                "infoAttribute");
            return VmAutomationVFXReflection.Get(infoAttribute, "experimental") is bool value &&
                   value;
        }

        private static Dictionary<string, object> VariantSettingSummary(object pair)
        {
            return new Dictionary<string, object>
            {
                { "name", VmAutomationVFXReflection.Get(pair, "Key")?.ToString() ?? "" },
                { "value", VmAutomationVFXValueCodec.Sanitize(
                    VmAutomationVFXReflection.Get(pair, "Value")) },
            };
        }

        private static string SettingPairText(object pair)
        {
            return (VmAutomationVFXReflection.Get(pair, "Key")?.ToString() ?? "") + "=" +
                   MiniJson.Serialize(VmAutomationVFXValueCodec.Sanitize(
                       VmAutomationVFXReflection.Get(pair, "Value")));
        }

        private static bool TryDocumentationUrl(object variant, out string url)
        {
            url = "";
            if (variant == null)
                return false;
            if (!VmAutomationVFXReflection.TryInvoke(variant, "GetDocumentationLink",
                    out object value))
                return false;
            url = value?.ToString() ?? "";
            return true;
        }

        private static Dictionary<string, object> CatalogLiteral(string kind,
            string id, string name, string category, string description)
        {
            return new Dictionary<string, object>
            {
                { "kind", kind }, { "catalogId", id }, { "name", name },
                { "category", category }, { "description", description },
                { "experimental", false },
            };
        }

        private static void AddDetailPage(
            Dictionary<string, object> result, string prefix,
            string collectionName, List<Dictionary<string, object>> all,
            int offset, int limit)
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

        private static List<Dictionary<string, object>> RelatedRoutes()
        {
            return new List<Dictionary<string, object>>
            {
                Fields("domain", "timeline", "owner", "timeline/*"),
                Fields("domain", "shader-graph", "owner", "shadergraph/*"),
                Fields("domain", "binders", "owner",
                    "component/* and prefab-asset/*"),
                Fields("domain", "six-way-textures", "owner",
                    "asset/import-settings/* and texture/*"),
                Fields("domain", "vector-fields", "owner",
                    "asset/* plus vfxgraph/transaction slot assignment"),
            };
        }

        private static Dictionary<string, object> Fields(string key1, object value1,
            string key2, object value2)
        {
            return new Dictionary<string, object>
            {
                { key1, value1 }, { key2, value2 },
            };
        }

        private static string SearchText(Dictionary<string, object> item)
        {
            return MiniJson.Serialize(item);
        }

        private static List<T> Bounded<T>(IEnumerable<T> values, int maximum,
            string label)
        {
            List<T> result = values.Take(maximum + 1).ToList();
            if (result.Count > maximum)
                throw VmAutomationVFXError.Create("response_too_large",
                    $"A VFX catalog item exposes more than {maximum} {label}.");
            return result;
        }

        private static string GetItemString(Dictionary<string, object> item,
            string key)
        {
            return item.TryGetValue(key, out object value) ? value?.ToString() ?? "" : "";
        }

        private static string GetVFXPackageVersion()
        {
            UnityEditor.PackageManager.PackageInfo package =
                UnityEditor.PackageManager.PackageInfo.FindForAssembly(
                    VmAutomationVFXReflection.FindType(VmAutomationVFXReflection.GraphTypeName)
                        ?.Assembly);
            return package?.version ?? "";
        }

        private static bool TryGetPage(Dictionary<string, object> args,
            out int offset, out int limit, out object error)
        {
            offset = GetInt(args, "offset", 0);
            limit = GetInt(args, "limit", 100);
            if (offset < 0)
            {
                error = VmAutomationResponse.Error("offset must be at least 0.",
                    "invalid_arguments");
                return false;
            }
            if (limit < 1 || limit > 500)
            {
                error = VmAutomationResponse.Error("limit must be between 1 and 500.",
                    "invalid_arguments");
                return false;
            }
            error = null;
            return true;
        }

        private static bool TryGetDetailPage(Dictionary<string, object> args,
            out int settingOffset, out int maxSettings, out int inputSlotOffset,
            out int outputSlotOffset, out int maxSlots, out object error)
        {
            settingOffset = GetInt(args, "settingOffset", 0);
            maxSettings = GetInt(args, "maxSettingsPerItem", 64);
            inputSlotOffset = GetInt(args, "inputSlotOffset", 0);
            outputSlotOffset = GetInt(args, "outputSlotOffset", 0);
            maxSlots = GetInt(args, "maxSlotsPerItem", 64);
            foreach ((string name, int value) in new[]
                     {
                         ("settingOffset", settingOffset),
                         ("inputSlotOffset", inputSlotOffset),
                         ("outputSlotOffset", outputSlotOffset),
                     })
            {
                if (value < 0)
                {
                    error = VmAutomationResponse.Error(name + " must be at least 0.",
                        "invalid_arguments");
                    return false;
                }
            }
            if (maxSettings < 1 || maxSettings > 128)
            {
                error = VmAutomationResponse.Error(
                    "maxSettingsPerItem must be between 1 and 128.",
                    "invalid_arguments");
                return false;
            }
            if (maxSlots < 1 || maxSlots > 256)
            {
                error = VmAutomationResponse.Error(
                    "maxSlotsPerItem must be between 1 and 256.",
                    "invalid_arguments");
                return false;
            }
            error = null;
            return true;
        }

        private static List<string> MissingTypes(params string[] typeNames)
        {
            return typeNames.Where(typeName =>
                VmAutomationVFXReflection.FindType(typeName) == null).Distinct().ToList();
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
            error = VmAutomationResponse.Error($"Unsupported argument '{unknown}'.",
                "invalid_arguments");
            return false;
        }

        private static string GetString(Dictionary<string, object> args, string key)
        {
            return args != null && args.TryGetValue(key, out object value) && value != null
                ? value.ToString() : "";
        }

        private static int GetInt(Dictionary<string, object> args, string key,
            int defaultValue)
        {
            return args != null && args.TryGetValue(key, out object value) && value != null
                ? (int)VmAutomationVFXValueCodec.ConvertTo(value, typeof(int), key)
                : defaultValue;
        }

        private static bool GetBool(Dictionary<string, object> args, string key,
            bool defaultValue)
        {
            return args != null && args.TryGetValue(key, out object value) && value != null
                ? (bool)VmAutomationVFXValueCodec.ConvertTo(value, typeof(bool), key)
                : defaultValue;
        }
    }
}
