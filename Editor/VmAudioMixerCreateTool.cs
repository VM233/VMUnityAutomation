using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine.Audio;

namespace VMUnityAutomation.Editor
{
    [VmProjectTool("audio/create-mixer", MutatesAssets = true,
        Description = "Create one new native AudioMixer with its default Master group and snapshot, save it and read its actual asset identity back. Existing assets are never overwritten; use audio-mixer/transaction for its authored graph.",
        Preconditions = new[] { "stable-edit-mode", "existing-parent-folder", "new-asset-path" },
        SideEffects = VmProjectToolSideEffect.WritesAssets | VmProjectToolSideEffect.ReadsProjectState,
        ErrorCodes = new[] { "audio_mixer_create_path", "audio_mixer_edit_mode", "audio_mixer_api_unavailable", "audio_mixer_create_readback" },
        CompletionEvidence = "SavedAndReadBack=true with the native GUID and exactly one default Master group.")]
    public sealed class VmAudioMixerCreateTool : IVmProjectTool<VmAudioMixerCreateRequest, VmAudioMixerCreateResult>
    {
        public VmAudioMixerCreateResult Execute(VmAudioMixerCreateRequest request)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new VmProjectToolException("audio_mixer_edit_mode", "Mixer creation requires stable Edit Mode.");
            string path = request.AssetPath?.Replace('\\', '/');
            if (path == null || !path.StartsWith("Assets/", StringComparison.Ordinal) ||
                !path.EndsWith(".mixer", StringComparison.OrdinalIgnoreCase) || path.Contains("/../") ||
                !AssetDatabase.IsValidFolder(Path.GetDirectoryName(path)?.Replace('\\', '/')) || File.Exists(path))
                throw new VmProjectToolException("audio_mixer_create_path", "A new .mixer path below an existing Assets folder is required.");
            var controller = VmAutomationAssetGraphUtility.FindType("UnityEditor.Audio.AudioMixerController");
            var create = controller?.GetMethod("CreateMixerControllerAtPath", BindingFlags.Public | BindingFlags.Static,
                null, new[] { typeof(string) }, null);
            if (create == null)
                throw new VmProjectToolException("audio_mixer_api_unavailable", "The native AudioMixer creation API is unavailable in this Editor.");
            try { create.Invoke(null, new object[] { path }); }
            catch (TargetInvocationException error) { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error.InnerException).Throw(); throw; }
            AssetDatabase.SaveAssets();
            var mixer = AssetDatabase.LoadAssetAtPath<AudioMixer>(path);
            if (mixer == null)
                throw new VmProjectToolException("audio_mixer_create_readback", "The native mixer asset could not be read after creation.");
            var groups = mixer.FindMatchingGroups("Master");
            string guid = AssetDatabase.AssetPathToGUID(path);
            if (groups.Length != 1 || string.IsNullOrEmpty(guid))
                throw new VmProjectToolException("audio_mixer_create_readback", "The saved native mixer did not publish its default Master group and GUID.");
            return new VmAudioMixerCreateResult { AssetPath = path, Guid = guid, MasterGroup = groups[0].name,
                GroupCount = groups.Length, SavedAndReadBack = true };
        }
    }
}
