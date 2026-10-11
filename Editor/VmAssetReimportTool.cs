using System;
using System.IO;
using UnityEditor;

namespace VMUnityAutomation.Editor
{
    [VmProjectTool("asset/reimport",
        Description = "Reimport one existing non-compilation asset through its native importer. Accept its asset or .meta path, preserve unrelated assets, and do not request a clean script rebuild. Use asset/refresh for code or compilation-contract changes.",
        MutatesAssets = true, LongRunning = true,
        SideEffects = VmProjectToolSideEffect.ReadsProjectState |
                      VmProjectToolSideEffect.WritesAssets,
        ErrorCodes = new[] { "asset_reimport_invalid_path", "asset_reimport_compilation_asset",
            "asset_reimport_edit_mode_required", "loaded_scene_asset_mutation_blocked" },
        Preconditions = new[] { "stable-edit-mode", "existing-non-compilation-file" },
        CompletionEvidence = "The terminal job result reports imported=true, the exact canonical asset path, native main asset type and before/after GUIDs. No explicit script compilation or global SaveAssets is requested.")]
    public sealed class VmAssetReimportTool :
        IVmProjectTool<VmAssetReimportRequest, VmAssetReimportResult>
    {
        public VmAssetReimportResult Execute(VmAssetReimportRequest request)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode ||
                EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new VmProjectToolException("asset_reimport_edit_mode_required",
                    "Asset reimport requires stable Edit Mode.");

            string path = VmAutomationAssetCommands.ResolveTargetedImportPath(request.AssetPath);
            if (!path.StartsWith("Assets/", StringComparison.Ordinal) ||
                !File.Exists(path) || AssetDatabase.IsValidFolder(path))
                throw new VmProjectToolException("asset_reimport_invalid_path",
                    $"Asset reimport requires one existing file below Assets/: '{path}'.");
            if (VmAutomationAssetCommands.IsCompilationAssetPath(path))
                throw new VmProjectToolException("asset_reimport_compilation_asset",
                    $"'{path}' affects script compilation. Use asset/refresh for authoritative compilation and reload.");
            if (VmAutomationSceneCommands.TryRejectLoadedSceneAssetMutation(new[] { path },
                    "reimport assets", out object rejection))
            {
                VmAutomationResponse.TryGetError(rejection, out string message,
                    out string code, out bool retryable);
                throw new VmProjectToolException(code, message, retryable);
            }

            string before = AssetDatabase.AssetPathToGUID(path);
            VmAutomationAssetCommands.ImportTargetedAsset(path, request.ForceUpdate);
            return new VmAssetReimportResult
            {
                AssetPath = path,
                GuidBefore = before,
                GuidAfter = AssetDatabase.AssetPathToGUID(path),
                AssetType = AssetDatabase.GetMainAssetTypeAtPath(path)?.FullName,
                ForceUpdate = request.ForceUpdate,
                Imported = true
            };
        }
    }
}
