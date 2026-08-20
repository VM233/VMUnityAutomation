using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using static VMUnityAutomation.Editor.VmAutomationPrefabCommandUtility;

namespace VMUnityAutomation.Editor
{
    public static class VmAutomationPrefabVariantCommands
    {
    public static object GetVariantInfo(Dictionary<string, object> args)
    {
        string assetPath = GetString(args, "assetPath");
        if (string.IsNullOrEmpty(assetPath))
            return new { error = "assetPath is required" };

        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        if (asset == null)
            return new { error = $"Prefab not found at '{assetPath}'" };

        var assetType = PrefabUtility.GetPrefabAssetType(asset);
        bool isVariant = assetType == PrefabAssetType.Variant;

        var result = new Dictionary<string, object>
        {
            { "prefab", asset.name },
            { "assetPath", assetPath },
            { "isVariant", isVariant },
            { "assetType", assetType.ToString() },
        };

        if (isVariant)
        {
            var basePrefab = PrefabUtility.GetCorrespondingObjectFromOriginalSource(asset);
            if (basePrefab != null)
            {
                string basePath = AssetDatabase.GetAssetPath(basePrefab);
                result["basePrefabPath"] = basePath;
                result["basePrefabName"] = basePrefab.name;
            }
        }

        // Find all variants of this prefab (or of the base if this is already a variant)
        string searchBasePath = assetPath;
        if (isVariant)
        {
            var basePrefab = PrefabUtility.GetCorrespondingObjectFromOriginalSource(asset);
            if (basePrefab != null)
                searchBasePath = AssetDatabase.GetAssetPath(basePrefab);
        }

        var variants = new List<Dictionary<string, object>>();
        var allPrefabs = AssetDatabase.FindAssets("t:Prefab");
        foreach (var guid in allPrefabs)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (path == searchBasePath) continue;

            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null) continue;

            if (PrefabUtility.GetPrefabAssetType(go) != PrefabAssetType.Variant)
                continue;

            var source = PrefabUtility.GetCorrespondingObjectFromOriginalSource(go);
            if (source != null && AssetDatabase.GetAssetPath(source) == searchBasePath)
            {
                variants.Add(new Dictionary<string, object>
                {
                    { "name", go.name },
                    { "assetPath", path },
                });
            }
        }

        result["basePrefab"] = searchBasePath;
        result["variants"] = variants;
        result["variantCount"] = variants.Count;

        return result;
    }

    /// <summary>
    /// Compare a variant to its base prefab — list all property overrides, added/removed components, added/removed GameObjects.
    /// </summary>
    public static object CompareVariantToBase(Dictionary<string, object> args)
    {
        string assetPath = GetString(args, "assetPath");
        if (string.IsNullOrEmpty(assetPath))
            return new { error = "assetPath is required" };

        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        if (asset == null)
            return new { error = $"Prefab not found at '{assetPath}'" };

        if (PrefabUtility.GetPrefabAssetType(asset) != PrefabAssetType.Variant)
            return new { error = $"'{assetPath}' is not a variant prefab" };

        // Instantiate to read overrides (PrefabUtility override APIs need an instance or asset)
        var instance = PrefabUtility.InstantiatePrefab(asset) as GameObject;
        if (instance == null)
            return new { error = "Failed to instantiate variant for comparison" };

        try
        {
            var basePrefab = PrefabUtility.GetCorrespondingObjectFromOriginalSource(asset);
            string basePath = basePrefab != null ? AssetDatabase.GetAssetPath(basePrefab) : "unknown";

            // Property overrides
            var propertyOverrides = PrefabUtility.GetPropertyModifications(asset);
            var overrideList = new List<Dictionary<string, object>>();
            if (propertyOverrides != null)
            {
                foreach (var mod in propertyOverrides)
                {
                    if (mod.target == null) continue;
                    // Skip internal Transform position/rotation on root (always present)
                    overrideList.Add(new Dictionary<string, object>
                    {
                        { "targetType", mod.target.GetType().Name },
                        { "targetName", mod.target.name },
                        { "propertyPath", mod.propertyPath },
                        { "value", mod.value ?? "null" },
                    });
                }
            }

            // Added components
            var addedComponents = PrefabUtility.GetAddedComponents(instance);
            var addedCompList = new List<Dictionary<string, object>>();
            foreach (var added in addedComponents)
            {
                addedCompList.Add(new Dictionary<string, object>
                {
                    { "componentType", added.instanceComponent.GetType().Name },
                    { "gameObject", added.instanceComponent.gameObject.name },
                });
            }

            // Removed components
            var removedComponents = PrefabUtility.GetRemovedComponents(instance);
            var removedCompList = new List<Dictionary<string, object>>();
            foreach (var removed in removedComponents)
            {
                removedCompList.Add(new Dictionary<string, object>
                {
                    { "componentType", removed.assetComponent.GetType().Name },
                    { "gameObject", removed.assetComponent.gameObject.name },
                });
            }

            // Added GameObjects
            var addedGOs = PrefabUtility.GetAddedGameObjects(instance);
            var addedGOList = new List<Dictionary<string, object>>();
            foreach (var added in addedGOs)
            {
                addedGOList.Add(new Dictionary<string, object>
                {
                    { "name", added.instanceGameObject.name },
                    { "childCount", added.instanceGameObject.transform.childCount },
                });
            }

            // Removed GameObjects
            var removedGOList = new List<Dictionary<string, object>>();
#if UNITY_2022_1_OR_NEWER
            var removedGOs = PrefabUtility.GetRemovedGameObjects(instance);
            foreach (var removed in removedGOs)
            {
                removedGOList.Add(new Dictionary<string, object>
                {
                    { "name", removed.assetGameObject.name },
                });
            }
#else
            // Fallback for Unity < 2022.1: compare asset children vs instance children
            var assetSource = PrefabUtility.GetCorrespondingObjectFromSource(instance);
            if (assetSource != null)
            {
                foreach (Transform assetChild in assetSource.transform)
                {
                    var correspondingInInstance = instance.transform.Find(assetChild.name);
                    if (correspondingInInstance == null)
                    {
                        removedGOList.Add(new Dictionary<string, object>
                        {
                            { "name", assetChild.name },
                        });
                    }
                }
            }
#endif

            return new Dictionary<string, object>
            {
                { "variant", asset.name },
                { "variantPath", assetPath },
                { "basePrefab", basePrefab != null ? basePrefab.name : "unknown" },
                { "basePrefabPath", basePath },
                { "propertyOverrides", overrideList },
                { "propertyOverrideCount", overrideList.Count },
                { "addedComponents", addedCompList },
                { "removedComponents", removedCompList },
                { "addedGameObjects", addedGOList },
                { "removedGameObjects", removedGOList },
            };
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(instance);
        }
    }

    /// <summary>
    /// Apply a specific override from a variant back to its base prefab, or apply all overrides.
    /// </summary>
    public static object ApplyVariantOverride(Dictionary<string, object> args)
    {
        string assetPath = GetString(args, "assetPath");
        if (string.IsNullOrEmpty(assetPath))
            return new { error = "assetPath is required" };

        bool applyAll = args.ContainsKey("applyAll") && Convert.ToBoolean(args["applyAll"]);
        string propertyPath = GetString(args, "propertyPath");
        string targetComponentType = GetString(args, "targetComponentType");
        string targetGameObject = GetString(args, "targetGameObject");

        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        if (asset == null)
            return new { error = $"Prefab not found at '{assetPath}'" };

        if (PrefabUtility.GetPrefabAssetType(asset) != PrefabAssetType.Variant)
            return new { error = $"'{assetPath}' is not a variant prefab" };

        var instance = PrefabUtility.InstantiatePrefab(asset) as GameObject;
        if (instance == null)
            return new { error = "Failed to instantiate variant" };

        try
        {
            var basePrefab = PrefabUtility.GetCorrespondingObjectFromOriginalSource(asset);
            string basePath = basePrefab != null ? AssetDatabase.GetAssetPath(basePrefab) : null;
            if (string.IsNullOrEmpty(basePath))
                return new { error = "Could not determine base prefab path" };
            var beforeSnapshot = CaptureAssetText(basePath);

            int appliedCount = 0;

            if (applyAll)
            {
                // Apply everything to base
                PrefabUtility.ApplyPrefabInstance(instance, InteractionMode.AutomatedAction);
                appliedCount = -1; // signals "all"
            }
            else
            {
                // Apply specific overrides matching filters
                var objectOverrides = PrefabUtility.GetObjectOverrides(instance);
                foreach (var ov in objectOverrides)
                {
                    bool matches = true;
                    if (!string.IsNullOrEmpty(targetComponentType))
                    {
                        var comp = ov.instanceObject as Component;
                        if (comp == null || comp.GetType().Name != targetComponentType)
                            matches = false;
                    }
                    if (!string.IsNullOrEmpty(targetGameObject))
                    {
                        var comp = ov.instanceObject as Component;
                        var go = ov.instanceObject as GameObject;
                        string goName = comp != null ? comp.gameObject.name : go != null ? go.name : "";
                        if (goName != targetGameObject)
                            matches = false;
                    }
                    if (matches)
                    {
                        ov.Apply(basePath, InteractionMode.AutomatedAction);
                        appliedCount++;
                    }
                }

                // Apply added components
                var addedComps = PrefabUtility.GetAddedComponents(instance);
                foreach (var ac in addedComps)
                {
                    bool matches = true;
                    if (!string.IsNullOrEmpty(targetComponentType) && ac.instanceComponent.GetType().Name != targetComponentType)
                        matches = false;
                    if (!string.IsNullOrEmpty(targetGameObject) && ac.instanceComponent.gameObject.name != targetGameObject)
                        matches = false;
                    if (matches)
                    {
                        ac.Apply(basePath, InteractionMode.AutomatedAction);
                        appliedCount++;
                    }
                }

                // Apply added GameObjects
                var addedGOs = PrefabUtility.GetAddedGameObjects(instance);
                foreach (var ag in addedGOs)
                {
                    bool matches = true;
                    if (!string.IsNullOrEmpty(targetGameObject) && ag.instanceGameObject.name != targetGameObject)
                        matches = false;
                    if (matches)
                    {
                        ag.Apply(basePath, InteractionMode.AutomatedAction);
                        appliedCount++;
                    }
                }
            }

            var result = new Dictionary<string, object>
            {
                { "success", true },
                { "variant", asset.name },
                { "basePrefab", basePrefab != null ? basePrefab.name : "unknown" },
                { "appliedCount", appliedCount == -1 ? "all" : (object)appliedCount },
            };
            AddPrefabFileDiff(result, beforeSnapshot, basePath, args);
            return result;
        }
        catch (Exception ex)
        {
            return new { error = $"Failed to apply overrides: {ex.Message}" };
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(instance);
        }
    }

    /// <summary>
    /// Revert a variant's overrides so it matches the base prefab again.
    /// Can revert all or only specific overrides by component/gameObject filter.
    /// </summary>
    public static object RevertVariantOverride(Dictionary<string, object> args)
    {
        string assetPath = GetString(args, "assetPath");
        if (string.IsNullOrEmpty(assetPath))
            return new { error = "assetPath is required" };
        var beforeSnapshot = CaptureAssetText(assetPath);

        bool revertAll = args.ContainsKey("revertAll") && Convert.ToBoolean(args["revertAll"]);
        string targetComponentType = GetString(args, "targetComponentType");
        string targetGameObject = GetString(args, "targetGameObject");

        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        if (asset == null)
            return new { error = $"Prefab not found at '{assetPath}'" };

        if (PrefabUtility.GetPrefabAssetType(asset) != PrefabAssetType.Variant)
            return new { error = $"'{assetPath}' is not a variant prefab" };

        var instance = PrefabUtility.InstantiatePrefab(asset) as GameObject;
        if (instance == null)
            return new { error = "Failed to instantiate variant" };

        try
        {
            int revertedCount = 0;

            if (revertAll)
            {
                PrefabUtility.RevertPrefabInstance(instance, InteractionMode.AutomatedAction);
                revertedCount = -1;
            }
            else
            {
                var objectOverrides = PrefabUtility.GetObjectOverrides(instance);
                foreach (var ov in objectOverrides)
                {
                    bool matches = true;
                    if (!string.IsNullOrEmpty(targetComponentType))
                    {
                        var comp = ov.instanceObject as Component;
                        if (comp == null || comp.GetType().Name != targetComponentType)
                            matches = false;
                    }
                    if (!string.IsNullOrEmpty(targetGameObject))
                    {
                        var comp = ov.instanceObject as Component;
                        var go = ov.instanceObject as GameObject;
                        string goName = comp != null ? comp.gameObject.name : go != null ? go.name : "";
                        if (goName != targetGameObject)
                            matches = false;
                    }
                    if (matches)
                    {
                        ov.Revert();
                        revertedCount++;
                    }
                }

                var addedComps = PrefabUtility.GetAddedComponents(instance);
                foreach (var ac in addedComps)
                {
                    bool matches = true;
                    if (!string.IsNullOrEmpty(targetComponentType) && ac.instanceComponent.GetType().Name != targetComponentType)
                        matches = false;
                    if (!string.IsNullOrEmpty(targetGameObject) && ac.instanceComponent.gameObject.name != targetGameObject)
                        matches = false;
                    if (matches)
                    {
                        ac.Revert();
                        revertedCount++;
                    }
                }

                var addedGOs = PrefabUtility.GetAddedGameObjects(instance);
                foreach (var ag in addedGOs)
                {
                    bool matches = true;
                    if (!string.IsNullOrEmpty(targetGameObject) && ag.instanceGameObject.name != targetGameObject)
                        matches = false;
                    if (matches)
                    {
                        ag.Revert();
                        revertedCount++;
                    }
                }
            }

            // Save the reverted variant back to disk
            PrefabUtility.ApplyPrefabInstance(instance, InteractionMode.AutomatedAction);

            var result = new Dictionary<string, object>
            {
                { "success", true },
                { "variant", asset.name },
                { "revertedCount", revertedCount == -1 ? "all" : (object)revertedCount },
            };
            AddPrefabFileDiff(result, beforeSnapshot, assetPath, args);
            return result;
        }
        catch (Exception ex)
        {
            return new { error = $"Failed to revert overrides: {ex.Message}" };
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(instance);
        }
    }

    /// <summary>
    /// Transfer (copy) overrides from one variant to another variant of the same base.
    /// Reads properties from source variant and applies them to target variant.
    /// </summary>
    public static object TransferVariantOverrides(Dictionary<string, object> args)
    {
        string sourceAssetPath = GetString(args, "sourceAssetPath");
        string targetAssetPath = GetString(args, "targetAssetPath");

        if (string.IsNullOrEmpty(sourceAssetPath))
            return new { error = "sourceAssetPath is required" };
        if (string.IsNullOrEmpty(targetAssetPath))
            return new { error = "targetAssetPath is required" };
        var beforeSnapshot = CaptureAssetText(targetAssetPath);

        var sourceAsset = AssetDatabase.LoadAssetAtPath<GameObject>(sourceAssetPath);
        var targetAsset = AssetDatabase.LoadAssetAtPath<GameObject>(targetAssetPath);

        if (sourceAsset == null) return new { error = $"Source prefab not found at '{sourceAssetPath}'" };
        if (targetAsset == null) return new { error = $"Target prefab not found at '{targetAssetPath}'" };

        // Get source overrides
        var sourceMods = PrefabUtility.GetPropertyModifications(sourceAsset);
        if (sourceMods == null || sourceMods.Length == 0)
            return new { error = "Source variant has no overrides to transfer" };

        // Filter by component/property if requested
        string filterComponentType = GetString(args, "filterComponentType");
        string filterPropertyPath = GetString(args, "filterPropertyPath");

        // Load target for editing
        var targetRoot = PrefabUtility.LoadPrefabContents(targetAssetPath);
        if (targetRoot == null)
            return new { error = "Failed to load target prefab for editing" };

        string sourceName = sourceAsset.name;
        string targetName = targetAsset.name;
        using (var session = new PrefabMutationSession(targetAssetPath, beforeSnapshot, targetRoot))
        {
            try
            {
                int transferred = 0;

            // Get the existing modifications on target
            var targetMods = PrefabUtility.GetPropertyModifications(targetAsset);
            var newMods = new List<PropertyModification>(targetMods ?? new PropertyModification[0]);

            foreach (var mod in sourceMods)
            {
                if (mod.target == null) continue;

                // Apply filters
                if (!string.IsNullOrEmpty(filterComponentType) && mod.target.GetType().Name != filterComponentType)
                    continue;
                if (!string.IsNullOrEmpty(filterPropertyPath) && !mod.propertyPath.Contains(filterPropertyPath))
                    continue;

                // Check if this override already exists on target, replace or add
                bool found = false;
                for (int i = 0; i < newMods.Count; i++)
                {
                    if (newMods[i].target != null &&
                        newMods[i].target.GetType() == mod.target.GetType() &&
                        newMods[i].propertyPath == mod.propertyPath)
                    {
                        newMods[i] = mod;
                        found = true;
                        break;
                    }
                }
                if (!found) newMods.Add(mod);
                transferred++;
            }

                PrefabUtility.SetPropertyModifications(targetRoot, newMods.ToArray());
                if (session.SaveAndClose() == null)
                    throw new InvalidOperationException(
                        $"SaveAsPrefabAsset returned null for '{targetAssetPath}'.");

                var result = new Dictionary<string, object>
                {
                    { "success", true },
                    { "source", sourceName },
                    { "target", targetName },
                    { "transferredOverrides", transferred },
                };
                AddPrefabFileDiff(result, beforeSnapshot, targetAssetPath, args);
                session.Commit();
                return result;
            }
            catch (Exception ex)
            {
                return new { error = $"Failed to transfer overrides: {ex.Message}" };
            }
        }
    }

    public static object CleanupMissingVariantOverrides(Dictionary<string, object> args)
    {
        string assetPath = GetString(args, "assetPath");
        if (string.IsNullOrEmpty(assetPath))
            return new { error = "assetPath is required" };

        var prefabAsset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
        if (prefabAsset == null)
            return new { error = $"Prefab not found at '{assetPath}'" };
        if (PrefabUtility.GetPrefabAssetType(prefabAsset) != PrefabAssetType.Variant)
            return new { error = $"Prefab at '{assetPath}' is not a Prefab Variant" };

        var beforeSnapshot = CaptureAssetText(assetPath);
        var modifications = PrefabUtility.GetPropertyModifications(prefabAsset) ??
                            Array.Empty<PropertyModification>();
        var kept = new List<PropertyModification>();
        var removed = new List<Dictionary<string, object>>();
        var validationTargets = CreateOverrideValidationTargets(modifications);

        try
        {
            foreach (var modification in modifications)
            {
                string reason = null;
                if (modification.target == null)
                {
                    reason = "missing-target";
                }
                else if (string.IsNullOrEmpty(modification.propertyPath))
                {
                    reason = "missing-property-path";
                }
                else
                {
                    try
                    {
                        var serializedTarget = validationTargets[modification.target];
                        if (serializedTarget.FindProperty(modification.propertyPath) == null)
                            reason = "missing-serialized-field";
                    }
                    catch (Exception ex)
                    {
                        reason = $"unreadable-target: {ex.Message}";
                    }
                }

                if (reason == null)
                {
                    kept.Add(modification);
                    continue;
                }

                removed.Add(new Dictionary<string, object>
                {
                    { "target", modification.target == null ? "" : modification.target.name },
                    { "targetType", modification.target == null ? "" : modification.target.GetType().FullName },
                    { "propertyPath", modification.propertyPath ?? "" },
                    { "value", modification.value ?? "" },
                    { "reason", reason }
                });
            }
        }
        finally
        {
            foreach (var serializedTarget in validationTargets.Values)
                serializedTarget.Dispose();
        }

        bool dryRun = GetBool(args, "dryRun", false);
        if (!dryRun && removed.Count > 0)
        {
            PrefabUtility.SetPropertyModifications(prefabAsset, kept.ToArray());
            EditorUtility.SetDirty(prefabAsset);
            AssetDatabase.SaveAssets();
            ImportPrefabAssetSynchronously(assetPath);
        }

        var result = new Dictionary<string, object>
        {
            { "success", true },
            { "assetPath", assetPath },
            { "dryRun", dryRun },
            { "beforeCount", modifications.Length },
            { "keptCount", kept.Count },
            { "removedCount", removed.Count },
            { "removed", removed }
        };
        if (!dryRun)
            AddPrefabFileDiff(result, beforeSnapshot, assetPath, args);
        return result;
    }

    private static Dictionary<UnityEngine.Object, SerializedObject> CreateOverrideValidationTargets(
        IReadOnlyCollection<PropertyModification> modifications)
    {
        var validationTargets = modifications
            .Where(modification => modification.target != null)
            .Select(modification => modification.target)
            .Distinct()
            .ToDictionary(target => target, target => new SerializedObject(target));

        foreach (var serializedTarget in validationTargets.Values)
            serializedTarget.UpdateIfRequiredOrScript();

        var arraySizeOverrides = modifications
            .Where(modification => modification.target != null &&
                                   modification.propertyPath != null &&
                                   modification.propertyPath.EndsWith(".Array.size", StringComparison.Ordinal))
            .OrderBy(modification => modification.propertyPath.Count(character => character == '.'));

        foreach (var modification in arraySizeOverrides)
        {
            if (!int.TryParse(modification.value, out int arraySize) || arraySize < 0)
                continue;

            var sizeProperty = validationTargets[modification.target].FindProperty(modification.propertyPath);
            if (sizeProperty != null)
                sizeProperty.intValue = arraySize;
        }

        return validationTargets;
    }

    /// <summary>
    /// Apply multiple prefab asset edits in one load/save transaction.
    /// If any operation fails, no prefab asset save is performed.
    /// </summary>
    }
}
