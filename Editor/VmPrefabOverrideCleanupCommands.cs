using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using static VMUnityAutomation.Editor.VmAutomationPrefabCommandUtility;

namespace VMUnityAutomation.Editor
{
    internal static class VmPrefabOverrideCleanupCommands
    {
        private const int MaxOverrides = 2048;
        private const int MaxSignatureCharacters = 1024 * 1024;

        internal static object Cleanup(Dictionary<string, object> args)
        {
            string assetPath = GetString(args, "assetPath");
            string prefabPath = GetString(args, "prefabPath");
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (asset == null)
                return VmAutomationResponse.Error("An existing Prefab assetPath is required.", "invalid_arguments");
            if (string.IsNullOrEmpty(prefabPath) &&
                PrefabUtility.GetPrefabAssetType(asset) != PrefabAssetType.Variant)
                return VmAutomationResponse.Error("The default root scope requires a Prefab Variant.", "invalid_arguments");
            var paths = GetStringList(args, "propertyPaths");
            if (paths.Count > 32 || paths.Any(string.IsNullOrEmpty) ||
                (args.ContainsKey("propertyPaths") && paths.Count == 0))
                return VmAutomationResponse.Error("propertyPaths must contain 1 to 32 exact nonempty paths.", "invalid_arguments");
            var selected = paths.Count == 0 ? null : new HashSet<string>(paths, StringComparer.Ordinal);
            var before = CaptureAssetText(assetPath);
            using (var session = new PrefabMutationSession(assetPath, before, PrefabUtility.LoadPrefabContents(assetPath)))
            {
                GameObject instance = FindInstance(session.Root, prefabPath);
                if (instance == null)
                    return VmAutomationResponse.Error("prefabPath must select an exact Prefab instance root.", "invalid_arguments");
                var modifications = ReadModifications(instance);
                var kept = new List<PropertyModification>();
                var removedKeys = new HashSet<string>(StringComparer.Ordinal);
                var result = new VmPrefabOverrideCleanupResult
                {
                    assetPath = assetPath, prefabPath = prefabPath ?? "",
                    dryRun = GetBool(args, "dryRun", false), beforeCount = modifications.Length
                };
                var validation = CreateValidationTargets(modifications, selected);
                int characters = 0;
                try
                {
                    foreach (var modification in modifications)
                    {
                        string reason = selected != null && !selected.Contains(modification.propertyPath ?? "")
                            ? null : MissingReason(modification, validation);
                        if (reason == null)
                        {
                            kept.Add(modification);
                            continue;
                        }
                        removedKeys.Add(Signature(modification, session.Root, ref characters));
                        result.removed.Add(new VmPrefabOverrideCleanupResult.RemovedOverride
                        {
                            target = modification.target == null ? "" : modification.target.name,
                            targetType = modification.target == null ? "" : modification.target.GetType().FullName,
                            propertyPath = modification.propertyPath ?? "", value = modification.value ?? "", reason = reason
                        });
                    }
                }
                finally
                {
                    foreach (var serialized in validation.Values) serialized.Dispose();
                }
                result.keptCount = kept.Count;
                result.removedCount = result.removed.Count;
                if (!result.dryRun && result.removedCount > 0)
                {
                    var retained = CaptureCounts(kept, session.Root, ref characters);
                    PrefabUtility.SetPropertyModifications(instance, kept.ToArray());
                    if (session.SaveAndClose() == null)
                        throw new InvalidOperationException("Native Prefab save returned no asset.");
                    VerifyPublication(assetPath, prefabPath, retained, removedKeys);
                    session.Commit();
                }
                var transport = (Dictionary<string, object>)VmJsonContract.ToTransportValue(result);
                transport["success"] = true;
                if (!result.dryRun) AddPrefabFileDiff(transport, before, assetPath, args);
                return transport;
            }
        }

        private static GameObject FindInstance(GameObject root, string path)
        {
            GameObject candidate = VmAutomationPrefabBatchEditor.FindInPrefab(root, path);
            return candidate != null && PrefabUtility.GetNearestPrefabInstanceRoot(candidate) == candidate
                ? candidate : null;
        }

        private static PropertyModification[] ReadModifications(GameObject instance)
        {
            var modifications = PrefabUtility.GetPropertyModifications(instance) ?? Array.Empty<PropertyModification>();
            if (modifications.Length > MaxOverrides)
                throw new InvalidOperationException("The selected instance exceeds the 2048-override budget.");
            return modifications;
        }

        private static Dictionary<UnityEngine.Object, SerializedObject> CreateValidationTargets(
            IReadOnlyCollection<PropertyModification> modifications, ISet<string> selected)
        {
            var targets = modifications.Where(mod => mod.target != null &&
                    (selected == null || selected.Contains(mod.propertyPath ?? "")))
                .Select(mod => mod.target).Distinct().ToDictionary(target => target, target => new SerializedObject(target));
            try
            {
                foreach (var serialized in targets.Values) serialized.UpdateIfRequiredOrScript();
                foreach (var modification in modifications.Where(mod => mod.target != null && targets.ContainsKey(mod.target) &&
                             mod.propertyPath != null && mod.propertyPath.EndsWith(".Array.size", StringComparison.Ordinal))
                             .OrderBy(mod => mod.propertyPath.Count(character => character == '.')))
                {
                    if (!int.TryParse(modification.value, out int size) || size < 0) continue;
                    var property = targets[modification.target].FindProperty(modification.propertyPath);
                    if (property != null) property.intValue = size;
                }
                return targets;
            }
            catch
            {
                foreach (var serialized in targets.Values) serialized.Dispose();
                throw;
            }
        }

        private static string MissingReason(PropertyModification modification,
            IReadOnlyDictionary<UnityEngine.Object, SerializedObject> targets)
        {
            if (modification.target == null) return "missing-target";
            if (string.IsNullOrEmpty(modification.propertyPath)) return "missing-property-path";
            return targets[modification.target].FindProperty(modification.propertyPath) == null
                ? "missing-serialized-field" : null;
        }

        private static string Signature(PropertyModification modification, GameObject root, ref int characters)
        {
            string signature = VmPrefabOverrideIdentity.Capture(modification, root);
            characters += signature.Length;
            if (characters > MaxSignatureCharacters)
                throw new InvalidOperationException("Override signatures exceed the 1 Mi-character budget.");
            return signature;
        }

        private static Dictionary<string, int> CaptureCounts(IEnumerable<PropertyModification> modifications,
            GameObject root, ref int characters)
        {
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var modification in modifications)
            {
                string key = Signature(modification, root, ref characters);
                counts.TryGetValue(key, out int count);
                counts[key] = count + 1;
            }
            return counts;
        }

        private static void VerifyPublication(string assetPath, string prefabPath,
            IReadOnlyDictionary<string, int> retained, ISet<string> removed)
        {
            GameObject reopened = PrefabUtility.LoadPrefabContents(assetPath);
            try
            {
                GameObject instance = FindInstance(reopened, prefabPath);
                if (instance == null) throw new InvalidOperationException("Published Prefab instance is missing.");
                int characters = 0;
                var actual = CaptureCounts(ReadModifications(instance), reopened, ref characters);
                if (removed.Any(actual.ContainsKey) || retained.Any(pair =>
                        !actual.TryGetValue(pair.Key, out int count) || count < pair.Value))
                    throw new InvalidOperationException("Published Prefab overrides failed removal or preservation readback.");
            }
            finally { PrefabUtility.UnloadPrefabContents(reopened); }
        }
    }
}
