using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace VMUnityAutomation.Editor
{
    public static class VmMissingScriptsRemoval
    {
        public static VmRemoveMissingScriptsResult Execute(GameObject target, string hierarchyPath)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return VmRemoveMissingScriptsResult.Failure("edit_mode_required",
                    "Missing-script cleanup requires stable Edit Mode.");
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                return VmRemoveMissingScriptsResult.Failure("editor_not_stable",
                    "Missing-script cleanup cannot run while Unity is compiling or updating assets.");

            var scene = target.scene;
            if (!scene.IsValid() || !scene.isLoaded)
                return VmRemoveMissingScriptsResult.Failure("loaded_scene_target_required",
                    "The target must belong to a loaded scene; prefab assets are not accepted.");

            int missingCount = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(target);
            if (missingCount == 0)
                return VmRemoveMissingScriptsResult.Success(0, hierarchyPath, scene.path, scene.isDirty);

            Undo.RegisterCompleteObjectUndo(target, "Remove Missing Scripts");
            int removed = GameObjectUtility.RemoveMonoBehavioursWithMissingScript(target);
            EditorUtility.SetDirty(target);
            EditorSceneManager.MarkSceneDirty(scene);
            return VmRemoveMissingScriptsResult.Success(removed, hierarchyPath, scene.path, scene.isDirty);
        }
    }
}
