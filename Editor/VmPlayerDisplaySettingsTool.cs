using System;
using UnityEditor;

namespace VMUnityAutomation.Editor
{
    [VmProjectTool("player/display-settings",
        Description = "Read or configure native PlayerSettings orientation, default dimensions and autorotation flags. Validate before writing and return the native saved snapshot.",
        MutatesProjectFiles = true,
        SideEffects = VmProjectToolSideEffect.ReadsProjectState | VmProjectToolSideEffect.WritesProjectFiles,
        ErrorCodes = new[] { "invalid_display_settings", "display_settings_editor_not_stable" },
        Preconditions = new[] { "editor-connected", "Configure requires a stable Editor outside Play Mode" },
        CompletionEvidence = "The output reads the native PlayerSettings after saving; runtime display adoption requires separate Simulator or device verification.")]
    public sealed class VmPlayerDisplaySettingsTool :
        IVmProjectTool<VmPlayerDisplaySettingsRequest, VmPlayerDisplaySettingsResult>
    {
        public VmPlayerDisplaySettingsResult Execute(VmPlayerDisplaySettingsRequest request)
        {
            if (!Enum.IsDefined(typeof(VmPlayerDisplaySettingsAction), request.Action))
                throw Invalid("The display settings action is undefined.");
            if (request.Action == VmPlayerDisplaySettingsAction.State)
            {
                if (request.HasChanges)
                    throw Invalid("State does not accept configuration fields.");
                return Read();
            }

            if (EditorApplication.isPlayingOrWillChangePlaymode ||
                EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new VmProjectToolException("display_settings_editor_not_stable",
                    "Configure requires a stable Editor outside Play Mode.");
            if (!request.HasChanges)
                throw Invalid("Configure requires at least one display settings field.");
            if (request.DefaultScreenWidth <= 0 || request.DefaultScreenHeight <= 0)
                throw Invalid("Default screen dimensions must be positive integers.");
            if (request.DefaultInterfaceOrientation.HasValue &&
                !Enum.IsDefined(typeof(UIOrientation), request.DefaultInterfaceOrientation.Value))
                throw Invalid("The native interface orientation is undefined.");

            if (request.DefaultInterfaceOrientation.HasValue)
                PlayerSettings.defaultInterfaceOrientation = request.DefaultInterfaceOrientation.Value;
            if (request.DefaultScreenWidth.HasValue)
                PlayerSettings.defaultScreenWidth = request.DefaultScreenWidth.Value;
            if (request.DefaultScreenHeight.HasValue)
                PlayerSettings.defaultScreenHeight = request.DefaultScreenHeight.Value;
            if (request.AllowedAutorotateToPortrait.HasValue)
                PlayerSettings.allowedAutorotateToPortrait = request.AllowedAutorotateToPortrait.Value;
            if (request.AllowedAutorotateToPortraitUpsideDown.HasValue)
                PlayerSettings.allowedAutorotateToPortraitUpsideDown = request.AllowedAutorotateToPortraitUpsideDown.Value;
            if (request.AllowedAutorotateToLandscapeLeft.HasValue)
                PlayerSettings.allowedAutorotateToLandscapeLeft = request.AllowedAutorotateToLandscapeLeft.Value;
            if (request.AllowedAutorotateToLandscapeRight.HasValue)
                PlayerSettings.allowedAutorotateToLandscapeRight = request.AllowedAutorotateToLandscapeRight.Value;
            AssetDatabase.SaveAssets();
            return Read();
        }

        private static VmPlayerDisplaySettingsResult Read() => new()
        {
            DefaultInterfaceOrientation = PlayerSettings.defaultInterfaceOrientation,
            DefaultScreenWidth = PlayerSettings.defaultScreenWidth,
            DefaultScreenHeight = PlayerSettings.defaultScreenHeight,
            AllowedAutorotateToPortrait = PlayerSettings.allowedAutorotateToPortrait,
            AllowedAutorotateToPortraitUpsideDown = PlayerSettings.allowedAutorotateToPortraitUpsideDown,
            AllowedAutorotateToLandscapeLeft = PlayerSettings.allowedAutorotateToLandscapeLeft,
            AllowedAutorotateToLandscapeRight = PlayerSettings.allowedAutorotateToLandscapeRight
        };

        private static VmProjectToolException Invalid(string message) =>
            new("invalid_display_settings", message);
    }
}
