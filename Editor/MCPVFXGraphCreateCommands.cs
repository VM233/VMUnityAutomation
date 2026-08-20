using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace VMUnityAutomation.Editor
{
    internal static class MCPVFXGraphCreateCommands
    {
        internal static object Create(Dictionary<string, object> args)
        {
            if (!TryValidateKeys(args, new[]
                {
                    "assetPath", "assetKind", "templateId", "overwrite",
                    "_agentId",
                }, out object keyError))
                return keyError;
            if (!MCPVFXReflection.IsAvailable)
                return MCPResponse.Error(
                    "VFX Graph is not available. Install com.unity.visualeffectgraph.",
                    "capability_unavailable");

            string assetPath = GetString(args, "assetPath");
            string assetKind = GetString(args, "assetKind").ToLowerInvariant();
            string templateId = GetString(args, "templateId");
            bool overwrite;
            try
            {
                overwrite = GetBool(args, "overwrite", false);
            }
            catch (Exception exception)
            {
                return MCPResponse.Error(MCPVFXReflection.Unwrap(exception).Message,
                    "invalid_arguments");
            }
            if (!MCPVFXAssetPath.TryNormalizeFile(assetPath, false,
                    out assetPath, out string pathError))
                return MCPResponse.Error(pathError, "invalid_arguments");
            if (!TryValidateRequest(assetPath, assetKind, templateId,
                    out object validationError))
                return validationError;
            string templatePath = "";
            if (!string.IsNullOrEmpty(templateId) &&
                !MCPVFXGraphCatalogCommands.TryResolveTemplate(templateId,
                    out templatePath, out object templateError))
                return templateError;

            string absolutePath = MCPVFXAssetPath.ToAbsoluteAssetsPath(assetPath);
            string absoluteMetaPath = absolutePath + ".meta";
            bool existed = AssetDatabase.LoadMainAssetAtPath(assetPath) != null ||
                           File.Exists(absolutePath);
            if (existed && !overwrite)
                return MCPResponse.Error(
                    $"Asset '{assetPath}' already exists. Set overwrite=true to replace its contents while preserving its meta identity.",
                    "asset_already_exists");
            UnityEngine.Object existing = AssetDatabase.LoadMainAssetAtPath(assetPath);
            if (existing != null &&
                !AssetDatabase.IsOpenForEdit(existing,
                    StatusQueryOptions.UseCachedIfPossible))
                return MCPResponse.Error(
                    $"Asset '{assetPath}' is not open for edit.",
                    "asset_not_editable");

            byte[] previousBytes = existed && File.Exists(absolutePath)
                ? File.ReadAllBytes(absolutePath) : null;
            byte[] previousMetaBytes = existed && File.Exists(absoluteMetaPath)
                ? File.ReadAllBytes(absoluteMetaPath) : null;
            string previousGuid = existed
                ? AssetDatabase.AssetPathToGUID(assetPath)
                : "";
            IReadOnlyList<string> createdFolders = Array.Empty<string>();
            try
            {
                createdFolders = MCPVFXAssetPath.EnsureParentFolder(assetPath);
                Type utilityType = MCPVFXReflection.RequireType(
                    MCPVFXReflection.AssetUtilityTypeName);
                if (!string.IsNullOrEmpty(templateId))
                {
                    MCPVFXReflection.Invoke(utilityType, "CreateTemplateAsset",
                        assetPath, templatePath);
                }
                else if (assetKind == "graph")
                {
                    MCPVFXReflection.Invoke(utilityType, "CreateNewAsset", assetPath);
                }
                else
                {
                    string templateRoot = MCPVFXReflection.Get(utilityType,
                        "templatePath")?.ToString() ?? "";
                    string templateName = MCPVFXReflection.Get(utilityType,
                        assetKind == "block-subgraph"
                            ? "templateBlockSubgraphAssetName"
                            : "templateOperatorSubgraphAssetName")?.ToString() ?? "";
                    if (string.IsNullOrEmpty(templateRoot) ||
                        string.IsNullOrEmpty(templateName))
                        throw new MissingMemberException(utilityType.FullName,
                            "default subgraph template");
                    MCPVFXReflection.Invoke(utilityType, "CreateTemplateAsset",
                        assetPath, templateRoot + templateName);
                }

                AssetDatabase.ImportAsset(assetPath,
                    ImportAssetOptions.ForceUpdate |
                    ImportAssetOptions.ForceSynchronousImport);
                if (!MCPVFXGraphSession.TryOpen(assetPath,
                        out MCPVFXGraphSession session, out object sessionError))
                    throw new InvalidOperationException(ErrorMessage(sessionError));
                if (!string.Equals(session.AssetKind, assetKind,
                        StringComparison.Ordinal))
                    throw new InvalidOperationException(
                        $"Created asset kind '{session.AssetKind}' did not match requested '{assetKind}'.");
                VerifyAssetStructure(session);
                string guid = AssetDatabase.AssetPathToGUID(assetPath);
                if (existed && !string.IsNullOrEmpty(previousGuid) &&
                    !string.Equals(previousGuid, guid, StringComparison.Ordinal))
                    throw new InvalidOperationException(
                        "Overwriting the VFX asset changed its meta GUID.");
                return new Dictionary<string, object>
                {
                    { "success", true },
                    { "assetPath", assetPath },
                    { "assetKind", session.AssetKind },
                    { "assetType", session.Asset.GetType().FullName },
                    { "guid", guid },
                    { "templateId", templateId },
                    { "overwritten", existed },
                    { "graphType", session.Graph.GetType().FullName },
                    { "modelCount", session.Models.Count },
                };
            }
            catch (Exception exception)
            {
                try
                {
                    if (existed)
                    {
                        RestoreSnapshot(absolutePath, previousBytes,
                            "VFX asset");
                        RestoreSnapshot(absoluteMetaPath, previousMetaBytes,
                            "VFX asset meta");
                        AssetDatabase.ImportAsset(assetPath,
                            ImportAssetOptions.ForceUpdate |
                            ImportAssetOptions.ForceSynchronousImport);
                        VerifySnapshot(absolutePath, previousBytes,
                            "VFX asset");
                        VerifySnapshot(absoluteMetaPath, previousMetaBytes,
                            "VFX asset meta");
                    }
                    else if (AssetDatabase.AssetPathExists(assetPath) ||
                             File.Exists(MCPVFXAssetPath.ToAbsoluteAssetsPath(
                                 assetPath)))
                    {
                        AssetDatabase.DeleteAsset(assetPath);
                    }
                    MCPVFXAssetPath.RollBackCreatedFolders(createdFolders);
                }
                catch (Exception rollbackException)
                {
                    return MCPResponse.Error(
                        $"VFX asset creation failed: {MCPVFXReflection.Unwrap(exception).Message}. Rollback also failed: {MCPVFXReflection.Unwrap(rollbackException).Message}",
                        "vfx_transaction_rollback_failed");
                }
                return MCPVFXError.Response(exception,
                    "vfx_transaction_failed");
            }
        }

        private static bool TryValidateRequest(string assetPath, string assetKind,
            string templateId, out object error)
        {
            if (assetKind != "graph" && assetKind != "block-subgraph" &&
                assetKind != "operator-subgraph")
            {
                error = MCPResponse.Error(
                    "assetKind must be graph, block-subgraph, or operator-subgraph.",
                    "invalid_arguments");
                return false;
            }
            string requiredExtension = assetKind == "graph" ? ".vfx" :
                assetKind == "block-subgraph" ? ".vfxblock" : ".vfxoperator";
            if (!assetPath.EndsWith(requiredExtension,
                    StringComparison.OrdinalIgnoreCase))
            {
                error = MCPResponse.Error(
                    $"assetPath for {assetKind} must end with '{requiredExtension}'.",
                    "invalid_arguments");
                return false;
            }
            if (!string.IsNullOrEmpty(templateId) && assetKind != "graph")
            {
                error = MCPResponse.Error(
                    "templateId is currently valid only for graph assets; subgraph creation uses Unity's required default subgraph context template.",
                    "invalid_arguments");
                return false;
            }
            error = null;
            return true;
        }

        private static void VerifyAssetStructure(MCPVFXGraphSession session)
        {
            bool isSubgraph = Convert.ToBoolean(MCPVFXReflection.Get(
                session.Resource, "isSubgraph"));
            if (session.AssetKind == "graph")
            {
                if (isSubgraph)
                    throw new InvalidOperationException(
                        "Created graph resource was marked as a subgraph.");
                return;
            }
            if (!isSubgraph)
                throw new InvalidOperationException(
                    $"Created {session.AssetKind} resource was not marked as a subgraph.");
            if (MCPVFXReflection.Get(session.Resource, "visualEffectObject")
                    is not UnityEngine.Object owner || owner != session.Asset)
                throw new InvalidOperationException(
                    $"Created {session.AssetKind} resource did not adopt its subgraph asset.");
            if (session.AssetKind == "block-subgraph" &&
                !session.Models.Any(model => model.GetType().Name ==
                                             "VFXBlockSubgraphContext"))
                throw new InvalidOperationException(
                    "Created block-subgraph is missing required VFXBlockSubgraphContext.");
        }

        private static string ErrorMessage(object response)
        {
            if (response is Dictionary<string, object> dictionary &&
                dictionary.TryGetValue("error", out object error))
                return error?.ToString() ?? "Unknown VFX session error.";
            return response?.ToString() ?? "Unknown VFX session error.";
        }

        private static void RestoreSnapshot(string path, byte[] bytes,
            string label)
        {
            if (bytes == null)
                throw new InvalidOperationException(
                    $"Cannot restore {label}; its original bytes were unavailable.");
            File.WriteAllBytes(path, bytes);
        }

        private static void VerifySnapshot(string path, byte[] bytes,
            string label)
        {
            if (!File.Exists(path) || !File.ReadAllBytes(path).SequenceEqual(bytes))
                throw new InvalidOperationException(
                    $"Rollback did not restore the original {label} bytes.");
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

        private static string GetString(Dictionary<string, object> args, string key)
        {
            return args != null && args.TryGetValue(key, out object value) && value != null
                ? value.ToString() : "";
        }

        private static bool GetBool(Dictionary<string, object> args, string key,
            bool defaultValue)
        {
            return args != null && args.TryGetValue(key, out object value) && value != null
                ? (bool)MCPVFXValueCodec.ConvertTo(value, typeof(bool), key)
                : defaultValue;
        }
    }
}
