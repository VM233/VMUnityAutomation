using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace VMUnityAutomation.Editor
{
    /// <summary>
    /// Advanced prefab operations: editing, variants, overrides, and nested prefabs.
    /// Basic create/instantiate are in MCPAssetCommands. This handles the advanced workflow.
    /// </summary>
    public static class MCPPrefabCommands
    {
        /// <summary>
        /// Get detailed prefab info: overrides, variant status, nested prefabs.
        /// </summary>
        public static object GetPrefabInfo(Dictionary<string, object> args)
        {
            // Can work on scene instance or asset
            string assetPath = args.ContainsKey("assetPath") ? args["assetPath"].ToString() : "";

            if (!string.IsNullOrEmpty(assetPath))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                if (prefab == null)
                    return new { error = $"Prefab not found at '{assetPath}'" };

                return BuildPrefabInfo(prefab, assetPath, false);
            }

            var go = MCPGameObjectCommands.FindGameObject(args);
            if (go == null)
                return new { error = "GameObject not found. Provide assetPath or path/instanceId." };

            // IsPartOfPrefabInstance is the authoritative "is this tied to a prefab?" check.
            // GetPrefabInstanceStatus has known false-negative cases (returns NotAPrefab for
            // valid prefab instances with non-root children, missing nested assets, etc.),
            // so we don't gate on it here.
            if (!PrefabUtility.IsPartOfPrefabInstance(go))
                return new { error = "GameObject is not a prefab instance" };

            string sourcePath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go);
            return BuildPrefabInfo(go, sourcePath, true);
        }

        private static object BuildPrefabInfo(GameObject go, string assetPath, bool isInstance)
        {
            var result = new Dictionary<string, object>
            {
                { "name", go.name },
                { "assetPath", assetPath },
                { "isInstance", isInstance },
                { "prefabType", PrefabUtility.GetPrefabAssetType(go).ToString() },
            };

            if (isInstance)
            {
                result["instanceStatus"] = PrefabUtility.GetPrefabInstanceStatus(go).ToString();
                result["hasOverrides"] = PrefabUtility.HasPrefabInstanceAnyOverrides(go, false);

                // List property overrides
                var modifications = PrefabUtility.GetPropertyModifications(go);
                if (modifications != null)
                {
                    var overrides = new List<Dictionary<string, object>>();
                    foreach (var mod in modifications)
                    {
                        overrides.Add(new Dictionary<string, object>
                        {
                            { "target", mod.target != null ? mod.target.name : "null" },
                            { "propertyPath", mod.propertyPath },
                            { "value", mod.value },
                        });
                    }
                    result["overrides"] = overrides;
                    result["overrideCount"] = overrides.Count;
                }

                // Added components
                var addedComponents = PrefabUtility.GetAddedComponents(go);
                if (addedComponents != null)
                {
                    var added = new List<string>();
                    foreach (var ac in addedComponents)
                        added.Add(ac.instanceComponent.GetType().Name);
                    result["addedComponents"] = added;
                }

                // Removed components
                var removedComponents = PrefabUtility.GetRemovedComponents(go);
                if (removedComponents != null)
                    result["removedComponentCount"] = removedComponents.Count;
            }

            // Check if variant
            if (!string.IsNullOrEmpty(assetPath))
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                if (asset != null)
                {
                    bool isVariant = PrefabUtility.GetPrefabAssetType(asset) == PrefabAssetType.Variant;
                    result["isVariant"] = isVariant;
                    if (isVariant)
                    {
                        var basePrefab = PrefabUtility.GetCorrespondingObjectFromSource(asset);
                        if (basePrefab != null)
                            result["basePrefabPath"] = AssetDatabase.GetAssetPath(basePrefab);
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Create a prefab variant from an existing prefab.
        /// </summary>
        public static object CreateVariant(Dictionary<string, object> args)
        {
            string basePath = args.ContainsKey("basePrefabPath") ? args["basePrefabPath"].ToString() : "";
            string variantPath = args.ContainsKey("variantPath") ? args["variantPath"].ToString() : "";

            if (string.IsNullOrEmpty(basePath))
                return new { error = "basePrefabPath is required" };
            if (string.IsNullOrEmpty(variantPath))
                return new { error = "variantPath is required" };

            var basePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(basePath);
            if (basePrefab == null)
                return new { error = $"Base prefab not found at '{basePath}'" };

            // Ensure directory
            EnsureDirectory(variantPath);

            // Instantiate, then save as variant
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
            var variant = PrefabUtility.SaveAsPrefabAsset(instance, variantPath);
            UnityEngine.Object.DestroyImmediate(instance);

            return new Dictionary<string, object>
            {
                { "success", variant != null },
                { "variantPath", variantPath },
                { "basePrefabPath", basePath },
                { "name", variant != null ? variant.name : null },
            };
        }

        /// <summary>
        /// Apply all overrides from a prefab instance back to the source prefab asset.
        /// </summary>
        public static object ApplyOverrides(Dictionary<string, object> args)
        {
            var go = MCPGameObjectCommands.FindGameObject(args);
            if (go == null)
                return new { error = "GameObject not found" };

            var status = PrefabUtility.GetPrefabInstanceStatus(go);
            if (status != PrefabInstanceStatus.Connected)
                return new { error = "GameObject is not a connected prefab instance" };

            string assetPath = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(go);
            PrefabUtility.ApplyPrefabInstance(go, InteractionMode.AutomatedAction);

            return new { success = true, gameObject = go.name, appliedTo = assetPath };
        }

        /// <summary>
        /// Revert all overrides on a prefab instance.
        /// </summary>
        public static object RevertOverrides(Dictionary<string, object> args)
        {
            var go = MCPGameObjectCommands.FindGameObject(args);
            if (go == null)
                return new { error = "GameObject not found" };

            var status = PrefabUtility.GetPrefabInstanceStatus(go);
            if (status != PrefabInstanceStatus.Connected)
                return new { error = "GameObject is not a connected prefab instance" };

            PrefabUtility.RevertPrefabInstance(go, InteractionMode.AutomatedAction);

            return new { success = true, gameObject = go.name, message = "All overrides reverted" };
        }

        /// <summary>
        /// Unpack a prefab instance (completely or just the outermost).
        /// </summary>
        public static object Unpack(Dictionary<string, object> args)
        {
            var go = MCPGameObjectCommands.FindGameObject(args);
            if (go == null)
                return new { error = "GameObject not found" };

            bool completely = args.ContainsKey("completely") && Convert.ToBoolean(args["completely"]);

            if (completely)
                PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            else
                PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);

            return new { success = true, gameObject = go.name, mode = completely ? "Completely" : "OutermostRoot" };
        }

        // ─── Helpers ───

        private static void EnsureDirectory(string assetPath)
        {
            string dir = Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(dir) && !AssetDatabase.IsValidFolder(dir))
            {
                string[] parts = dir.Split('/');
                string current = parts[0];
                for (int i = 1; i < parts.Length; i++)
                {
                    string next = current + "/" + parts[i];
                    if (!AssetDatabase.IsValidFolder(next))
                        AssetDatabase.CreateFolder(current, parts[i]);
                    current = next;
                }
            }
        }

    }
}
