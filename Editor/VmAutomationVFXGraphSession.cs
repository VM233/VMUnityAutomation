using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace VMUnityAutomation.Editor
{
    internal sealed class VmAutomationVFXGraphSession
    {
        private VmAutomationVFXGraphSession(string assetPath, UnityEngine.Object asset,
            object resource, object graph, List<UnityEngine.Object> contents)
        {
            AssetPath = assetPath;
            Asset = asset;
            Resource = resource;
            Graph = graph;
            Contents = contents;
        }

        internal string AssetPath { get; }
        internal UnityEngine.Object Asset { get; }
        internal object Resource { get; }
        internal object Graph { get; }
        internal IReadOnlyList<UnityEngine.Object> Contents { get; }

        internal string AssetKind
        {
            get
            {
                string typeName = Asset?.GetType().Name ?? "";
                if (typeName.Contains("SubgraphBlock"))
                    return "block-subgraph";
                if (typeName.Contains("SubgraphOperator"))
                    return "operator-subgraph";
                return "graph";
            }
        }

        internal IReadOnlyList<UnityEngine.Object> Models => Contents
            .Where(item => item != null &&
                           VmAutomationVFXReflection.HasBaseType(item.GetType(),
                               VmAutomationVFXReflection.ModelTypeName))
            .ToList();

        internal static bool TryOpen(string assetPath,
            out VmAutomationVFXGraphSession session, out object error)
        {
            session = null;
            if (!VmAutomationVFXReflection.IsAvailable)
            {
                error = VmAutomationResponse.Error(
                    "VFX Graph is not available. Install com.unity.visualeffectgraph.",
                    "capability_unavailable");
                return false;
            }
            if (!VmAutomationVFXAssetPath.TryNormalizeFile(assetPath, true,
                    out assetPath, out string pathError))
            {
                error = VmAutomationResponse.Error(pathError, "invalid_arguments");
                return false;
            }

            UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(assetPath);
            if (asset == null)
            {
                error = VmAutomationResponse.Error(
                    $"VFX Graph asset '{assetPath}' was not found.",
                    "asset_not_found");
                return false;
            }
            if (!IsVFXAsset(asset))
            {
                error = VmAutomationResponse.Error(
                    $"Asset '{assetPath}' is not a VFX Graph or VFX subgraph.",
                    "asset_type_mismatch");
                return false;
            }

            Type resourceType = VmAutomationVFXReflection.FindType(
                VmAutomationVFXReflection.ResourceTypeName);
            MethodInfo getResource = resourceType?.GetMethod("GetResourceAtPath",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic,
                null, new[] { typeof(string) }, null);
            object resource;
            try
            {
                resource = getResource?.Invoke(null, new object[] { assetPath });
            }
            catch (TargetInvocationException exception)
            {
                error = VmAutomationResponse.Error(
                    VmAutomationVFXReflection.Unwrap(exception).Message,
                    "vfx_resource_unavailable");
                return false;
            }
            if (resource == null)
            {
                error = VmAutomationResponse.Error(
                    $"VFX Graph resource for '{assetPath}' was not available.",
                    "vfx_resource_unavailable");
                return false;
            }

            object graph;
            try
            {
                Type extensionsType = VmAutomationVFXReflection.RequireType(
                    VmAutomationVFXReflection.ResourceExtensionsTypeName);
                graph = VmAutomationVFXReflection.Invoke(extensionsType,
                    "GetOrCreateGraph", resource);
            }
            catch (Exception exception)
            {
                error = VmAutomationResponse.Error(
                    VmAutomationVFXReflection.Unwrap(exception).Message,
                    "unsupported_vfx_version");
                return false;
            }
            if (graph == null)
            {
                error = VmAutomationResponse.Error(
                    $"VFX Graph model for '{assetPath}' was not available.",
                    "vfx_graph_unavailable");
                return false;
            }

            List<UnityEngine.Object> contents = VmAutomationVFXReflection.Enumerate(
                    VmAutomationVFXReflection.Invoke(resource, "GetContents"))
                .OfType<UnityEngine.Object>()
                .Where(item => item != null)
                .Distinct()
                .Take(VmAutomationVFXLimits.GraphContents + 1)
                .ToList();
            if (contents.Count > VmAutomationVFXLimits.GraphContents)
            {
                error = VmAutomationResponse.Error(
                    $"VFX Graph '{assetPath}' contains {contents.Count} serialized objects; the inspection limit is {VmAutomationVFXLimits.GraphContents}.",
                    "response_too_large");
                return false;
            }
            if (!contents.Contains(asset))
                contents.Insert(0, asset);
            if (graph is UnityEngine.Object graphObject &&
                !contents.Contains(graphObject))
                contents.Add(graphObject);
            if (contents.Count > VmAutomationVFXLimits.GraphContents)
            {
                error = VmAutomationResponse.Error(
                    $"VFX Graph '{assetPath}' contains more than {VmAutomationVFXLimits.GraphContents} serialized objects.",
                    "response_too_large");
                return false;
            }

            session = new VmAutomationVFXGraphSession(assetPath, asset, resource, graph,
                contents);
            error = null;
            return true;
        }

        internal object CaptureGraphBackup()
        {
            return VmAutomationVFXReflection.Invoke(Graph, "Backup");
        }

        internal void RestoreGraphBackup(object backup)
        {
            if (backup == null)
                throw new ArgumentNullException(nameof(backup));
            VmAutomationVFXReflection.Invoke(Graph, "Restore", backup);
        }

        internal void WriteAndImport()
        {
            Type extensionsType = VmAutomationVFXReflection.RequireType(
                VmAutomationVFXReflection.ResourceExtensionsTypeName);
            VmAutomationVFXReflection.Invoke(extensionsType,
                "WriteAssetWithSubAssets", Resource);
            AssetDatabase.ImportAsset(AssetPath,
                ImportAssetOptions.ForceUpdate |
                ImportAssetOptions.ForceSynchronousImport);
        }

        internal Dictionary<UnityEngine.Object, string> BuildModelIds()
        {
            List<UnityEngine.Object> models = Models.ToList();
            return models.Select((model, index) => new { model, index })
                .ToDictionary(item => item.model,
                    item => VmAutomationVFXReflection.StableId(item.model, item.index));
        }

        internal UnityEngine.Object ResolveModel(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            return BuildModelIds().FirstOrDefault(pair =>
                string.Equals(pair.Value, id, StringComparison.Ordinal)).Key;
        }

        internal static bool IsVFXAsset(UnityEngine.Object asset)
        {
            if (asset == null)
                return false;
            Type assetType = asset.GetType();
            Type graphAssetType = VmAutomationVFXReflection.FindType(
                VmAutomationVFXReflection.VisualEffectAssetTypeName);
            Type subgraphAssetType = VmAutomationVFXReflection.FindType(
                VmAutomationVFXReflection.VisualEffectSubgraphTypeName);
            return graphAssetType != null && graphAssetType.IsAssignableFrom(
                       assetType) ||
                   subgraphAssetType != null && subgraphAssetType.IsAssignableFrom(
                       assetType);
        }
    }
}
