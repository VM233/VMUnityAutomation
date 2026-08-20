using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace VMUnityAutomation.Editor
{
    /// <summary>
    /// Bounded inspection and transactional SerializedObject editing for optional-package
    /// graph assets whose public Editor APIs differ across Unity/package versions.
    /// </summary>
    internal static class VmAutomationAssetGraphUtility
    {
        private const int MaxNestedValueDepth = 4;
        private const int MaxNestedValueItems = 16;
        private const int MaxValueStringLength = 512;

        internal static Type FindType(params string[] fullNames)
        {
            foreach (string fullName in fullNames ?? Array.Empty<string>())
            {
                if (string.IsNullOrEmpty(fullName))
                    continue;
                foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    try
                    {
                        Type type = assembly.GetType(fullName, false);
                        if (type != null)
                            return type;
                    }
                    catch
                    {
                        // Optional package assemblies can be mid-reload.
                    }
                }

                foreach (string assemblyName in GetOptionalAssemblyNames(fullName))
                {
                    try
                    {
                        Assembly assembly = LoadOptionalAssembly(assemblyName);
                        Type type = assembly?.GetType(fullName, false);
                        if (type != null)
                            return type;
                    }
                    catch
                    {
                        // Installed optional assemblies are loaded lazily and can be mid-reload.
                    }
                }
            }
            return null;
        }

        private static Assembly LoadOptionalAssembly(string assemblyName)
        {
            try
            {
                return Assembly.Load(assemblyName);
            }
            catch
            {
                string projectRoot = System.IO.Path.GetDirectoryName(Application.dataPath);
                string assemblyPath = System.IO.Path.Combine(projectRoot ?? "",
                    "Library", "ScriptAssemblies", assemblyName + ".dll");
                return System.IO.File.Exists(assemblyPath)
                    ? Assembly.LoadFrom(assemblyPath)
                    : null;
            }
        }

        private static IEnumerable<string> GetOptionalAssemblyNames(string fullName)
        {
            if (fullName.StartsWith("UnityEditor.AddressableAssets.",
                    StringComparison.Ordinal))
                yield return "Unity.Addressables.Editor";
            if (fullName.StartsWith("UnityEngine.AddressableAssets.",
                    StringComparison.Ordinal))
                yield return "Unity.Addressables";
            if (fullName.StartsWith("UnityEngine.Timeline.", StringComparison.Ordinal))
                yield return "Unity.Timeline";
            if (fullName.StartsWith("UnityEditor.Timeline.", StringComparison.Ordinal))
                yield return "Unity.Timeline.Editor";
            if (fullName.StartsWith("Unity.Cinemachine.", StringComparison.Ordinal))
                yield return "Unity.Cinemachine";
            if (fullName.StartsWith("Cinemachine.", StringComparison.Ordinal))
                yield return "Cinemachine";
            if (fullName.StartsWith("UnityEngine.VFX.", StringComparison.Ordinal))
                yield return "Unity.VisualEffectGraph.Runtime";
            if (fullName.StartsWith("UnityEditor.VFX.", StringComparison.Ordinal))
                yield return "Unity.VisualEffectGraph.Editor";
        }

        internal static object InspectAsset(string assetPath, Func<UnityEngine.Object, bool> includeObject,
            int maxObjects, int maxProperties,
            IEnumerable<UnityEngine.Object> additionalObjects = null)
        {
            if (string.IsNullOrEmpty(assetPath))
                return VmAutomationResponse.Error("assetPath is required.", "invalid_arguments");
            UnityEngine.Object main = AssetDatabase.LoadMainAssetAtPath(assetPath);
            if (main == null)
                return VmAutomationResponse.Error($"Asset '{assetPath}' was not found.", "asset_not_found");

            var candidates = AssetDatabase.LoadAllAssetsAtPath(assetPath)
                .Concat(additionalObjects ?? Enumerable.Empty<UnityEngine.Object>())
                .Where(item => item != null)
                .Distinct()
                .Where(item => item != null && (includeObject == null || includeObject(item)))
                .ToList();
            var allObjects = candidates.Take(Math.Max(1, Math.Min(maxObjects, 500))).ToList();
            if (!allObjects.Contains(main) && (includeObject == null || includeObject(main)))
                allObjects.Insert(0, main);

            var objectIds = allObjects.ToDictionary(item => item, GetLocalId);
            var edges = new List<Dictionary<string, object>>();
            var objects = allObjects.Select(item =>
                    InspectObject(item, objectIds, edges, maxProperties))
                .ToList();
            return new Dictionary<string, object>
            {
                { "success", true },
                { "assetPath", assetPath },
                { "mainType", main.GetType().FullName },
                { "objectCount", objects.Count },
                { "objects", objects },
                { "edgeCount", edges.Count },
                { "edges", edges },
                { "truncatedObjects", candidates.Count > objects.Count },
                { "valueBudget", new Dictionary<string, object>
                    {
                        { "maxNestedDepth", MaxNestedValueDepth },
                        { "maxNestedItems", MaxNestedValueItems },
                        { "maxStringLength", MaxValueStringLength },
                    }
                },
            };
        }

        internal static object ApplyTransaction(string assetPath, List<object> rawOperations,
            Func<UnityEngine.Object, bool> includeObject, bool dryRun, string undoName,
            IEnumerable<UnityEngine.Object> additionalObjects = null, Action commit = null)
        {
            if (string.IsNullOrEmpty(assetPath))
                return VmAutomationResponse.Error("assetPath is required.", "invalid_arguments");
            if (rawOperations == null || rawOperations.Count == 0)
                return VmAutomationResponse.Error("operations must contain at least one operation.",
                    "invalid_arguments");

            UnityEngine.Object[] allAssets = AssetDatabase.LoadAllAssetsAtPath(assetPath);
            if (allAssets.Length == 0)
                return VmAutomationResponse.Error($"Asset '{assetPath}' was not found.", "asset_not_found");
            var candidates = allAssets.Concat(additionalObjects ??
                                               Enumerable.Empty<UnityEngine.Object>())
                .Where(item => item != null)
                .Distinct()
                .Where(item => includeObject == null || includeObject(item))
                .ToList();
            var prepared = new List<PreparedOperation>();
            try
            {
                for (int index = 0; index < rawOperations.Count; index++)
                {
                    var operation = rawOperations[index] as Dictionary<string, object>;
                    if (operation == null)
                        throw new ArgumentException($"operations[{index}] must be an object.");
                    prepared.Add(PrepareOperation(index, operation, candidates));
                }
            }
            catch (Exception exception)
            {
                return VmAutomationResponse.Error(exception.GetBaseException().Message,
                    "asset_graph_transaction_invalid");
            }

            if (dryRun)
            {
                return new Dictionary<string, object>
                {
                    { "success", true },
                    { "dryRun", true },
                    { "assetPath", assetPath },
                    { "operations", prepared.Select(item => item.Describe()).ToList() },
                };
            }

            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(undoName);
            try
            {
                var results = new List<Dictionary<string, object>>();
                foreach (PreparedOperation operation in prepared)
                    results.Add(operation.Apply(undoName));
                commit?.Invoke();
                AssetDatabase.SaveAssets();
                Undo.CollapseUndoOperations(undoGroup);
                return new Dictionary<string, object>
                {
                    { "success", true },
                    { "assetPath", assetPath },
                    { "operationCount", results.Count },
                    { "results", results },
                };
            }
            catch (Exception exception)
            {
                Undo.RevertAllDownToGroup(undoGroup);
                return VmAutomationResponse.Error(exception.GetBaseException().Message,
                    "asset_graph_transaction_failed");
            }
        }

        internal static bool IsTypeOrNamespace(UnityEngine.Object value, params string[] prefixes)
        {
            string fullName = value?.GetType().FullName ?? "";
            return prefixes != null && prefixes.Any(prefix =>
                !string.IsNullOrEmpty(prefix) &&
                fullName.StartsWith(prefix, StringComparison.Ordinal));
        }

        private static Dictionary<string, object> InspectObject(UnityEngine.Object target,
            IReadOnlyDictionary<UnityEngine.Object, long> objectIds,
            List<Dictionary<string, object>> edges, int maxProperties)
        {
            var properties = new List<Dictionary<string, object>>();
            var serialized = new SerializedObject(target);
            SerializedProperty iterator = serialized.GetIterator();
            int count = 0;
            int visibleCount = 0;
            if (iterator.NextVisible(true))
            {
                do
                {
                    if (iterator.propertyPath == "m_Script")
                        continue;
                    visibleCount++;
                    if (count >= Math.Max(1, Math.Min(maxProperties, 500)))
                        continue;
                    var property = new Dictionary<string, object>
                    {
                        { "path", iterator.propertyPath },
                        { "type", iterator.propertyType.ToString() },
                        { "value", SanitizeValue(
                            VmAutomationComponentCommands.GetSerializedValue(iterator, 2, 32), 0) },
                    };
                    properties.Add(property);
                    count++;

                    if (iterator.propertyType == SerializedPropertyType.ObjectReference &&
                        iterator.objectReferenceValue != null &&
                        objectIds.TryGetValue(iterator.objectReferenceValue, out long targetId))
                    {
                        edges.Add(new Dictionary<string, object>
                        {
                            { "fromLocalId", objectIds[target] },
                            { "propertyPath", iterator.propertyPath },
                            { "toLocalId", targetId },
                            { "toType", iterator.objectReferenceValue.GetType().FullName },
                        });
                    }
                } while (iterator.NextVisible(false));
            }

            return new Dictionary<string, object>
            {
                { "localId", objectIds[target].ToString() },
                { "name", target.name ?? "" },
                { "type", target.GetType().FullName },
                { "mainAsset", AssetDatabase.IsMainAsset(target) },
                { "hideFlags", target.hideFlags.ToString() },
                { "properties", properties },
                { "propertiesTruncated", visibleCount > count },
            };
        }

        internal static object SanitizeValue(object value, int depth = 0)
        {
            if (value == null)
                return null;
            if (value is UnityEngine.Object unityObject)
            {
                string assetPath = AssetDatabase.GetAssetPath(unityObject);
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(unityObject, out string guid,
                    out long localId);
                return new Dictionary<string, object>
                {
                    { "name", unityObject.name ?? "" },
                    { "type", unityObject.GetType().FullName },
                    { "assetPath", assetPath ?? "" },
                    { "guid", guid ?? "" },
                    { "localId", localId.ToString() },
                };
            }
            if (value is Enum)
                return value.ToString();
            if (value is Hash128 hash)
                return hash.ToString();
            if (value is AnimationCurve curve)
            {
                return new Dictionary<string, object>
                {
                    { "keyCount", curve.length },
                    { "preWrapMode", curve.preWrapMode.ToString() },
                    { "postWrapMode", curve.postWrapMode.ToString() },
                };
            }
            if (value is Gradient gradient)
            {
                return new Dictionary<string, object>
                {
                    { "colorKeyCount", gradient.colorKeys.Length },
                    { "alphaKeyCount", gradient.alphaKeys.Length },
                    { "mode", gradient.mode.ToString() },
                };
            }
            if (value is string text)
            {
                if (text.Length <= MaxValueStringLength)
                    return text;
                return new Dictionary<string, object>
                {
                    { "preview", text.Substring(0, MaxValueStringLength) },
                    { "length", text.Length },
                    { "truncated", true },
                };
            }
            if (depth >= MaxNestedValueDepth)
            {
                return new Dictionary<string, object>
                {
                    { "type", value.GetType().Name },
                    { "truncated", true },
                    { "reason", "maxNestedDepth" },
                };
            }
            if (value is Dictionary<string, object> dictionary)
            {
                var result = new Dictionary<string, object>();
                int index = 0;
                foreach (var pair in dictionary)
                {
                    if (index >= MaxNestedValueItems)
                        break;
                    result[pair.Key] = SanitizeValue(pair.Value, depth + 1);
                    index++;
                }
                if (dictionary.Count > result.Count)
                {
                    result["_truncated"] = true;
                    result["_totalFields"] = dictionary.Count;
                }
                return result;
            }
            if (value is IDictionary nonGenericDictionary)
            {
                var result = new Dictionary<string, object>();
                int index = 0;
                foreach (DictionaryEntry pair in nonGenericDictionary)
                {
                    if (index >= MaxNestedValueItems)
                        break;
                    result[pair.Key?.ToString() ?? "null"] =
                        SanitizeValue(pair.Value, depth + 1);
                    index++;
                }
                if (nonGenericDictionary.Count > result.Count)
                {
                    result["_truncated"] = true;
                    result["_totalFields"] = nonGenericDictionary.Count;
                }
                return result;
            }
            if (value is IEnumerable enumerable && !(value is UnityEngine.Object))
            {
                var items = new List<object>();
                int total = 0;
                foreach (object item in enumerable)
                {
                    if (items.Count < MaxNestedValueItems)
                        items.Add(SanitizeValue(item, depth + 1));
                    total++;
                }
                if (total <= items.Count)
                    return items;
                return new Dictionary<string, object>
                {
                    { "items", items },
                    { "totalItems", total },
                    { "truncated", true },
                };
            }
            return value;
        }

        private static PreparedOperation PrepareOperation(int index,
            Dictionary<string, object> operation, IReadOnlyList<UnityEngine.Object> candidates)
        {
            string action = GetString(operation, "action").ToLowerInvariant();
            ValidateOperationKeys(operation, action);
            UnityEngine.Object target = ResolveTarget(operation, candidates);
            if (target == null)
                throw new ArgumentException($"operations[{index}] target was not found.");

            switch (action)
            {
                case "rename":
                    string name = GetString(operation, "name");
                    if (string.IsNullOrEmpty(name))
                        throw new ArgumentException($"operations[{index}].name is required.");
                    return PreparedOperation.Rename(index, target, name);
                case "set-property":
                    string propertyPath = GetString(operation, "propertyPath");
                    if (string.IsNullOrEmpty(propertyPath))
                        throw new ArgumentException(
                            $"operations[{index}].propertyPath is required.");
                    var serialized = new SerializedObject(target);
                    SerializedProperty property = serialized.FindProperty(propertyPath);
                    if (property == null)
                        throw new ArgumentException(
                            $"operations[{index}] property '{propertyPath}' was not found on {target.GetType().Name}.");
                    if (!operation.TryGetValue("value", out object value))
                        throw new ArgumentException($"operations[{index}].value is required.");
                    object before = VmAutomationComponentCommands.GetSerializedValue(property, 2, 32);
                    VmAutomationComponentCommands.SetSerializedValue(property, value);
                    return PreparedOperation.SetProperty(index, target, propertyPath, value,
                        before);
                default:
                    throw new ArgumentException(
                        $"operations[{index}].action must be rename or set-property.");
            }
        }

        private static UnityEngine.Object ResolveTarget(Dictionary<string, object> operation,
            IReadOnlyList<UnityEngine.Object> candidates)
        {
            string localIdText = GetString(operation, "localId");
            if (!string.IsNullOrEmpty(localIdText))
            {
                if (!long.TryParse(localIdText, out long localId))
                    throw new ArgumentException(
                        $"localId '{localIdText}' must be an integer string.");
                return candidates.FirstOrDefault(candidate => GetLocalId(candidate) == localId);
            }

            string typeName = GetString(operation, "type");
            string name = GetString(operation, "targetName");
            if (string.IsNullOrEmpty(typeName) && string.IsNullOrEmpty(name))
                return null;
            List<UnityEngine.Object> matches = candidates.Where(candidate =>
                (string.IsNullOrEmpty(typeName) ||
                 string.Equals(candidate.GetType().FullName, typeName, StringComparison.Ordinal) ||
                 string.Equals(candidate.GetType().Name, typeName, StringComparison.Ordinal)) &&
                (string.IsNullOrEmpty(name) ||
                  string.Equals(candidate.name, name, StringComparison.Ordinal)))
                .Take(2).ToList();
            if (matches.Count > 1)
                throw new ArgumentException(
                    $"Target selector matched multiple graph objects. Use localId to disambiguate.");
            return matches.SingleOrDefault();
        }

        private static void ValidateOperationKeys(Dictionary<string, object> operation,
            string action)
        {
            string[] common = { "action", "localId", "type", "targetName" };
            string[] allowed = action == "rename"
                ? common.Concat(new[] { "name" }).ToArray()
                : action == "set-property"
                    ? common.Concat(new[] { "propertyPath", "value" }).ToArray()
                    : common;
            var allowedSet = new HashSet<string>(allowed, StringComparer.Ordinal);
            string unexpected = operation.Keys.FirstOrDefault(key =>
                !allowedSet.Contains(key));
            if (!string.IsNullOrEmpty(unexpected))
                throw new ArgumentException(
                    $"Unsupported field '{unexpected}' for graph action '{action}'. " +
                    $"Allowed fields: {string.Join(", ", allowed.OrderBy(item => item))}.");
        }

        private static long GetLocalId(UnityEngine.Object target)
        {
            return AssetDatabase.TryGetGUIDAndLocalFileIdentifier(target, out string _, out long localId)
                ? localId
                : 0L;
        }

        private static string GetString(Dictionary<string, object> values, string key)
        {
            return values != null && values.TryGetValue(key, out object value) && value != null
                ? value.ToString()
                : "";
        }

        private sealed class PreparedOperation
        {
            private readonly int index;
            private readonly string action;
            private readonly UnityEngine.Object target;
            private readonly string name;
            private readonly string propertyPath;
            private readonly object value;
            private readonly object before;

            private PreparedOperation(int index, string action, UnityEngine.Object target,
                string name, string propertyPath, object value, object before)
            {
                this.index = index;
                this.action = action;
                this.target = target;
                this.name = name;
                this.propertyPath = propertyPath;
                this.value = value;
                this.before = before;
            }

            internal static PreparedOperation Rename(int index, UnityEngine.Object target,
                string name)
            {
                return new PreparedOperation(index, "rename", target, name, "", null,
                    target.name);
            }

            internal static PreparedOperation SetProperty(int index, UnityEngine.Object target,
                string propertyPath, object value, object before)
            {
                return new PreparedOperation(index, "set-property", target, "", propertyPath,
                    value, before);
            }

            internal Dictionary<string, object> Describe()
            {
                return new Dictionary<string, object>
                {
                    { "index", index },
                    { "action", action },
                    { "targetLocalId", GetLocalId(target).ToString() },
                    { "targetType", target.GetType().FullName },
                    { "targetName", target.name ?? "" },
                    { "propertyPath", propertyPath },
                    { "before", before },
                    { "requested", action == "rename" ? (object)name : value },
                };
            }

            internal Dictionary<string, object> Apply(string undoName)
            {
                Undo.RecordObject(target, undoName);
                if (action == "rename")
                {
                    target.name = name;
                    EditorUtility.SetDirty(target);
                    var renameResult = Describe();
                    renameResult["after"] = target.name;
                    return renameResult;
                }

                var serialized = new SerializedObject(target);
                serialized.Update();
                SerializedProperty property = serialized.FindProperty(propertyPath);
                if (property == null)
                    throw new InvalidOperationException(
                        $"Property '{propertyPath}' disappeared from {target.GetType().Name}.");
                VmAutomationComponentCommands.SetSerializedValue(property, value);
                serialized.ApplyModifiedProperties();
                EditorUtility.SetDirty(target);
                var result = Describe();
                result["after"] = VmAutomationComponentCommands.GetSerializedValue(
                    new SerializedObject(target).FindProperty(propertyPath), 2, 32);
                return result;
            }
        }
    }
}
