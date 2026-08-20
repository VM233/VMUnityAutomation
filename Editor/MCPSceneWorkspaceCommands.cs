using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace VMUnityAutomation.Editor
{
    /// <summary>
    /// Modal-free multi-scene workspace management.
    /// Dirty scene decisions must be explicit; this command never opens a save dialog.
    /// </summary>
    internal static class MCPSceneWorkspaceCommands
    {
        public static object Execute(Dictionary<string, object> args)
        {
            string action = GetString(args, "action", "list").ToLowerInvariant();
            try
            {
                ValidateKeys(args, action);
            }
            catch (Exception exception)
            {
                return MCPResponse.Error(exception.Message, "invalid_arguments");
            }
            switch (action)
            {
                case "list":
                    return BuildWorkspaceResponse();
                case "open":
                    return Open(args);
                case "close":
                    return Close(args);
                case "set-active":
                case "setactive":
                    return SetActive(args);
                default:
                    return MCPResponse.Error(
                        $"Unknown scene workspace action '{action}'. Use list, open, close, or set-active.",
                        "invalid_arguments");
            }
        }

        private static object Open(Dictionary<string, object> args)
        {
            string path = NormalizeAssetPath(GetString(args, "path"));
            if (string.IsNullOrEmpty(path))
                return MCPResponse.Error("path is required for action=open.", "invalid_arguments");
            if (!path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase) ||
                AssetDatabase.LoadAssetAtPath<SceneAsset>(path) == null)
                return MCPResponse.Error($"Scene asset '{path}' was not found.", "scene_not_found");

            Scene existing = FindScenes(path, "").FirstOrDefault();
            if (existing.IsValid() && existing.isLoaded)
            {
                var alreadyLoaded = BuildWorkspaceResponse();
                alreadyLoaded["alreadyLoaded"] = true;
                alreadyLoaded["openedScene"] = SceneInfo(existing);
                return alreadyLoaded;
            }

            string modeValue = GetString(args, "mode", "additive").ToLowerInvariant();
            OpenSceneMode mode;
            switch (modeValue)
            {
                case "single":
                    mode = OpenSceneMode.Single;
                    break;
                case "additive":
                    mode = OpenSceneMode.Additive;
                    break;
                default:
                    return MCPResponse.Error("mode must be single or additive.", "invalid_arguments");
            }

            if (mode == OpenSceneMode.Single)
            {
                bool saveModified = GetBool(args, "saveModified", false);
                bool discardModified = GetBool(args, "discardModified", false);
                if (saveModified && discardModified)
                {
                    return MCPResponse.Error(
                        "saveModified and discardModified are mutually exclusive.",
                        "invalid_arguments");
                }
                var dirtyScenes = LoadedScenes().Where(scene => scene.isDirty).ToList();
                if (dirtyScenes.Count > 0)
                {
                    if (!saveModified && !discardModified)
                    {
                        return MCPResponse.Error(
                            "Opening a scene in single mode would replace dirty loaded scenes. Set saveModified=true or discardModified=true explicitly.",
                            "dirty_scenes_require_decision", false,
                            new Dictionary<string, object>
                            {
                                { "dirtyScenes", dirtyScenes.Select(SceneInfo).ToList() },
                            });
                    }

                    if (saveModified)
                    {
                        foreach (Scene dirtyScene in dirtyScenes)
                        {
                            if (string.IsNullOrEmpty(dirtyScene.path) ||
                                !EditorSceneManager.SaveScene(dirtyScene))
                            {
                                return MCPResponse.Error(
                                    $"Failed to save dirty scene '{dirtyScene.name}'.",
                                    "scene_save_failed");
                            }
                        }
                    }
                }
            }

            Scene opened = EditorSceneManager.OpenScene(path, mode);
            var response = BuildWorkspaceResponse();
            response["openedScene"] = SceneInfo(opened);
            response["mode"] = modeValue;
            return response;
        }

        private static object Close(Dictionary<string, object> args)
        {
            if (!TryResolveScene(args, out Scene scene, out object resolutionError))
                return resolutionError;

            bool save = GetBool(args, "save", false);
            bool discardChanges = GetBool(args, "discardChanges", false);
            if (save && discardChanges)
                return MCPResponse.Error(
                    "save and discardChanges are mutually exclusive.",
                    "invalid_arguments");
            if (scene.isDirty)
            {
                if (!save && !discardChanges)
                {
                    return MCPResponse.Error(
                        "The scene is dirty. Set save=true or discardChanges=true explicitly.",
                        "dirty_scene_requires_decision", false,
                        new Dictionary<string, object> { { "scene", SceneInfo(scene) } });
                }
                if (save && (string.IsNullOrEmpty(scene.path) || !EditorSceneManager.SaveScene(scene)))
                    return MCPResponse.Error($"Failed to save scene '{scene.name}'.",
                        "scene_save_failed");
            }

            var closed = SceneInfo(scene);
            bool removeScene = GetBool(args, "removeScene", true);
            bool success = EditorSceneManager.CloseScene(scene, removeScene);
            if (!success)
                return MCPResponse.Error($"Unity failed to close scene '{scene.name}'.",
                    "scene_close_failed");
            var response = BuildWorkspaceResponse();
            response["closedScene"] = closed;
            response["removed"] = removeScene;
            return response;
        }

        private static object SetActive(Dictionary<string, object> args)
        {
            if (!TryResolveScene(args, out Scene scene, out object resolutionError))
                return resolutionError;
            if (!SceneManager.SetActiveScene(scene))
                return MCPResponse.Error($"Unity failed to activate scene '{scene.name}'.",
                    "scene_activate_failed");

            var response = BuildWorkspaceResponse();
            response["activeScene"] = SceneInfo(scene);
            return response;
        }

        private static Dictionary<string, object> BuildWorkspaceResponse()
        {
            Scene active = SceneManager.GetActiveScene();
            var scenes = LoadedScenes().Select(SceneInfo).ToList();
            return new Dictionary<string, object>
            {
                { "success", true },
                { "loadedSceneCount", scenes.Count },
                { "activeScenePath", active.IsValid() ? active.path : "" },
                { "scenes", scenes },
            };
        }

        private static IEnumerable<Scene> LoadedScenes()
        {
            for (int index = 0; index < SceneManager.sceneCount; index++)
            {
                Scene scene = SceneManager.GetSceneAt(index);
                if (scene.IsValid() && scene.isLoaded)
                    yield return scene;
            }
        }

        private static List<Scene> FindScenes(string path, string name)
        {
            string normalizedPath = NormalizeAssetPath(path);
            return LoadedScenes().Where(scene =>
                    (string.IsNullOrEmpty(normalizedPath) ||
                     string.Equals(NormalizeAssetPath(scene.path), normalizedPath,
                         StringComparison.OrdinalIgnoreCase)) &&
                    (string.IsNullOrEmpty(name) ||
                     string.Equals(scene.name, name, StringComparison.Ordinal)))
                .ToList();
        }

        private static bool TryResolveScene(Dictionary<string, object> args,
            out Scene scene, out object error)
        {
            scene = default;
            string path = GetString(args, "path");
            string name = GetString(args, "name");
            if (string.IsNullOrEmpty(path) && string.IsNullOrEmpty(name))
            {
                error = MCPResponse.Error(
                    "path or name is required to select a loaded scene.",
                    "invalid_arguments");
                return false;
            }

            List<Scene> matches = FindScenes(path, name);
            if (matches.Count == 0)
            {
                error = MCPResponse.Error(
                    "The requested loaded scene was not found.", "scene_not_found");
                return false;
            }
            if (matches.Count > 1)
            {
                error = MCPResponse.Error(
                    $"Scene selector matched {matches.Count} loaded scenes. Use path to disambiguate.",
                    "scene_selector_ambiguous", false,
                    new Dictionary<string, object>
                    {
                        { "matches", matches.Select(SceneInfo).ToList() },
                    });
                return false;
            }

            scene = matches[0];
            error = null;
            return true;
        }

        private static void ValidateKeys(Dictionary<string, object> args, string action)
        {
            string[] allowed;
            switch (action)
            {
                case "list":
                    allowed = new[] { "action", "_agentId" };
                    break;
                case "open":
                    allowed = new[]
                    {
                        "action", "path", "mode", "saveModified",
                        "discardModified", "_agentId",
                    };
                    break;
                case "close":
                    allowed = new[]
                    {
                        "action", "path", "name", "save", "discardChanges",
                        "removeScene", "_agentId",
                    };
                    break;
                case "set-active":
                case "setactive":
                    allowed = new[] { "action", "path", "name", "_agentId" };
                    break;
                default:
                    return;
            }

            var allowedSet = new HashSet<string>(allowed, StringComparer.Ordinal);
            string unexpected = args.Keys.FirstOrDefault(key =>
                !allowedSet.Contains(key));
            if (!string.IsNullOrEmpty(unexpected))
                throw new ArgumentException(
                    $"Unsupported field '{unexpected}' for scene workspace action '{action}'. " +
                    $"Allowed fields: {string.Join(", ", allowed.Where(item => item != "_agentId").OrderBy(item => item))}.");
        }

        private static Dictionary<string, object> SceneInfo(Scene scene)
        {
            return new Dictionary<string, object>
            {
                { "name", scene.name ?? "" },
                { "path", scene.path ?? "" },
                { "loaded", scene.isLoaded },
                { "dirty", scene.isDirty },
                { "active", scene == SceneManager.GetActiveScene() },
                { "rootCount", scene.isLoaded ? scene.rootCount : 0 },
            };
        }

        private static string NormalizeAssetPath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return "";
            string normalized = path.Replace('\\', '/').Trim();
            if (Path.IsPathRooted(normalized))
            {
                string projectRoot = Path.GetFullPath(Path.Combine(UnityEngine.Application.dataPath, ".."))
                    .Replace('\\', '/').TrimEnd('/');
                string absolute = Path.GetFullPath(normalized).Replace('\\', '/');
                if (absolute.StartsWith(projectRoot + "/", StringComparison.OrdinalIgnoreCase))
                    normalized = absolute.Substring(projectRoot.Length + 1);
            }
            return normalized;
        }

        private static string GetString(Dictionary<string, object> values, string key,
            string defaultValue = "")
        {
            return values != null && values.TryGetValue(key, out object value) && value != null
                ? value.ToString()
                : defaultValue;
        }

        private static bool GetBool(Dictionary<string, object> values, string key, bool defaultValue)
        {
            return values != null && values.TryGetValue(key, out object value) && value != null
                ? Convert.ToBoolean(value)
                : defaultValue;
        }
    }
}
