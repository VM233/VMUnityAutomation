using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace VMUnityAutomation.Editor
{
    internal static class VmAutomationBuildProfileCommands
    {
        private const string BuildProfileTypeName = "UnityEditor.Build.Profile.BuildProfile";

        public static object Execute(Dictionary<string, object> args)
        {
            Type profileType = VmAutomationAssetGraphUtility.FindType(BuildProfileTypeName);
            if (profileType == null)
                return VmAutomationResponse.Error(
                    "Build Profiles are unavailable in this Unity version.",
                    "capability_unavailable");

            string action = GetString(args, "action", "info").ToLowerInvariant();
            switch (action)
            {
                case "info":
                    return Info(profileType, args);
                case "transaction":
                    return Transaction(profileType, args);
                default:
                    return VmAutomationResponse.Error("action must be info or transaction.",
                        "invalid_arguments");
            }
        }

        private static object Info(Type profileType,
            Dictionary<string, object> args = null)
        {
            args = args ?? new Dictionary<string, object>();
            UnityEngine.Object active = GetActiveProfile(profileType);
            int offset = Math.Max(0, GetInt(args, "offset", 0));
            int limit = Math.Max(1, Math.Min(200, GetInt(args, "limit", 50)));
            var allProfiles = AssetDatabase.FindAssets("t:BuildProfile")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => !string.IsNullOrEmpty(path))
                .Select(path => AssetDatabase.LoadMainAssetAtPath(path))
                .Where(asset => asset != null && profileType.IsInstanceOfType(asset))
                .OrderBy(asset => AssetDatabase.GetAssetPath(asset), StringComparer.Ordinal)
                .ToList();
            var profiles = allProfiles
                .Skip(offset)
                .Take(limit)
                .Select(asset => ProfileInfo(profileType, asset,
                    asset == active, AssetDatabase.GetAssetPath(asset)))
                .ToList();
            return new Dictionary<string, object>
            {
                { "success", true },
                { "available", true },
                { "activeProfile", active != null
                    ? new Dictionary<string, object>
                    {
                        { "name", active.name ?? "" },
                        { "assetPath", AssetDatabase.GetAssetPath(active) ?? "" },
                    }
                    : null },
                { "profileCount", allProfiles.Count },
                { "offset", offset },
                { "limit", limit },
                { "profiles", profiles },
                { "hasMore", offset + profiles.Count < allProfiles.Count },
                { "nextOffset", offset + profiles.Count < allProfiles.Count
                    ? (object)(offset + profiles.Count)
                    : null },
                { "installedPlatforms", GetInstalledPlatforms(profileType) },
                { "globalScenes", EditorBuildSettings.scenes.Select(SceneInfo).ToList() },
            };
        }

        private static object Transaction(Type profileType, Dictionary<string, object> args)
        {
            List<object> operations = GetList(args, "operations");
            if (operations == null || operations.Count == 0)
                return VmAutomationResponse.Error("operations must contain at least one operation.",
                    "invalid_arguments");
            bool dryRun = GetBool(args, "dryRun", false);
            var prepared = new List<Dictionary<string, object>>();
            try
            {
                for (int index = 0; index < operations.Count; index++)
                {
                    if (!(operations[index] is Dictionary<string, object> operation))
                        throw new ArgumentException($"operations[{index}] must be an object.");
                    string action = GetString(operation, "action").ToLowerInvariant();
                    if (action != "create" && action != "set-active" && action != "set-scenes" &&
                        action != "set-scripting-defines" && action != "set-global-scenes" &&
                        action != "set-property")
                    {
                        throw new ArgumentException(
                            $"operations[{index}].action must be create, set-active, set-scenes, set-scripting-defines, set-global-scenes, or set-property.");
                    }
                    ValidateOperationKeys(operation, action);
                    prepared.Add(ValidateOperation(profileType, operation));
                }
                string duplicateCreatePath = prepared
                    .Where(operation => string.Equals(GetString(operation, "action"),
                        "create", StringComparison.Ordinal))
                    .GroupBy(operation => GetString(operation, "assetPath"),
                        StringComparer.Ordinal)
                    .Where(group => group.Count() > 1)
                    .Select(group => group.Key)
                    .FirstOrDefault();
                if (!string.IsNullOrEmpty(duplicateCreatePath))
                    throw new ArgumentException(
                        $"Multiple create operations target BuildProfile '{duplicateCreatePath}'.");
                int definesIndex = operations.FindIndex(item =>
                    item is Dictionary<string, object> operation &&
                    string.Equals(GetString(operation, "action"),
                        "set-scripting-defines", StringComparison.OrdinalIgnoreCase));
                if (definesIndex >= 0 && definesIndex != operations.Count - 1)
                    throw new ArgumentException(
                        "set-scripting-defines must be the final operation because applying defines can start compilation and a domain reload.");
            }
            catch (Exception exception)
            {
                return VmAutomationResponse.Error(exception.Message, "build_profile_transaction_invalid");
            }

            if (dryRun)
            {
                return new Dictionary<string, object>
                {
                    { "success", true },
                    { "dryRun", true },
                    { "operationCount", prepared.Count },
                    { "operations", prepared },
                    { "activeProfile", GetActiveProfile(profileType) is UnityEngine.Object active
                        ? new Dictionary<string, object>
                        {
                            { "name", active.name ?? "" },
                            { "assetPath", AssetDatabase.GetAssetPath(active) ?? "" },
                        }
                        : null },
                };
            }

            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("VM Unity Automation Edit Build Profiles");
            EditorBuildSettingsScene[] originalGlobalScenes = EditorBuildSettings.scenes;
            UnityEngine.Object originalActive = GetActiveProfile(profileType);
            string[] createdAssetPaths = prepared
                .Where(operation => string.Equals(GetString(operation, "action"),
                    "create", StringComparison.Ordinal))
                .Select(operation => GetString(operation, "assetPath"))
                .Where(path => !string.IsNullOrEmpty(path))
                .ToArray();
            var results = new List<Dictionary<string, object>>();
            try
            {
                foreach (Dictionary<string, object> operation in operations
                             .Cast<Dictionary<string, object>>())
                    results.Add(ApplyOperation(profileType, operation));
                AssetDatabase.SaveAssets();
                Undo.CollapseUndoOperations(undoGroup);
                var response = new Dictionary<string, object>
                {
                    { "success", true },
                    { "operationCount", results.Count },
                    { "results", results },
                };
                if (GetBool(args, "includeAfter", false))
                    response["after"] = Info(profileType, args);
                return response;
            }
            catch (Exception exception)
            {
                Undo.RevertAllDownToGroup(undoGroup);
                EditorBuildSettings.scenes = originalGlobalScenes;
                TryRestoreActiveProfile(profileType, originalActive);
                foreach (string createdAssetPath in createdAssetPaths.Reverse())
                    AssetDatabase.DeleteAsset(createdAssetPath);
                return VmAutomationResponse.Error(exception.GetBaseException().Message,
                    "build_profile_transaction_failed");
            }
        }

        private static void ValidateOperationKeys(Dictionary<string, object> operation,
            string action)
        {
            string[] allowed;
            switch (action)
            {
                case "create":
                    allowed = new[] { "action", "profileName", "platformId" };
                    break;
                case "set-global-scenes":
                    allowed = new[] { "action", "scenes" };
                    break;
                case "set-active":
                    allowed = new[] { "action", "assetPath" };
                    break;
                case "set-scenes":
                    allowed = new[]
                    {
                        "action", "assetPath", "scenes", "overrideGlobalScenes",
                    };
                    break;
                case "set-scripting-defines":
                    allowed = new[] { "action", "assetPath", "defines" };
                    break;
                default:
                    allowed = new[]
                    {
                        "action", "assetPath", "propertyPath", "value",
                    };
                    break;
            }

            var allowedSet = new HashSet<string>(allowed, StringComparer.Ordinal);
            string unexpected = operation.Keys.FirstOrDefault(key =>
                !allowedSet.Contains(key));
            if (!string.IsNullOrEmpty(unexpected))
                throw new ArgumentException(
                    $"Unsupported field '{unexpected}' for Build Profile action '{action}'. " +
                    $"Allowed fields: {string.Join(", ", allowed.OrderBy(item => item))}.");
        }

        private static Dictionary<string, object> ValidateOperation(Type profileType,
            Dictionary<string, object> operation)
        {
            string action = GetString(operation, "action").ToLowerInvariant();
            var result = new Dictionary<string, object> { { "action", action } };
            if (action == "create")
            {
                string profileName = ValidateProfileName(GetString(operation, "profileName"));
                Dictionary<string, object> platform = ResolveInstalledPlatform(profileType,
                    GetString(operation, "platformId"), out _);
                string createAssetPath = ExpectedProfileAssetPath(profileName);
                if (AssetDatabase.LoadMainAssetAtPath(createAssetPath) != null)
                    throw new ArgumentException($"BuildProfile '{createAssetPath}' already exists.");
                RequireCreateBuildProfileMethod(profileType);
                result["assetPath"] = createAssetPath;
                result["profileName"] = profileName;
                result["platformId"] = platform["platformId"];
                result["platformDisplayName"] = platform["displayName"];
                return result;
            }
            if (action == "set-global-scenes")
            {
                EditorBuildSettingsScene[] scenes = ReadScenes(operation);
                result["scenes"] = scenes.Select(SceneInfo).ToList();
                return result;
            }

            string assetPath = GetString(operation, "assetPath");
            UnityEngine.Object profile = LoadProfile(profileType, assetPath);
            if (profile == null)
                throw new ArgumentException($"BuildProfile '{assetPath}' was not found.");
            result["assetPath"] = assetPath;
            result["profileName"] = profile.name ?? "";
            switch (action)
            {
                case "set-active":
                    if (profileType.GetMethod("SetActiveBuildProfile",
                            BindingFlags.Static | BindingFlags.Public |
                            BindingFlags.NonPublic) == null)
                        throw new MissingMethodException(profileType.FullName,
                            "SetActiveBuildProfile");
                    break;
                case "set-scenes":
                    result["overrideGlobalScenes"] =
                        GetBool(operation, "overrideGlobalScenes", true);
                    result["scenes"] = ReadScenes(operation).Select(SceneInfo).ToList();
                    RequireWritableProperty(profileType, "overrideGlobalScenes");
                    RequireWritableProperty(profileType, "scenes");
                    break;
                case "set-scripting-defines":
                    if (!operation.ContainsKey("defines") ||
                        !TryGetStringArray(operation, "defines", out string[] defines))
                        throw new ArgumentException("defines must be a string array.");
                    if (profileType.GetMethod("SetAndApplyScriptingDefines",
                            BindingFlags.Instance | BindingFlags.Public |
                            BindingFlags.NonPublic) == null)
                        throw new MissingMethodException(profileType.FullName,
                            "SetAndApplyScriptingDefines");
                    result["defines"] = defines;
                    break;
                case "set-property":
                    string propertyPath = GetString(operation, "propertyPath");
                    if (string.IsNullOrEmpty(propertyPath) ||
                        !operation.TryGetValue("value", out object value))
                        throw new ArgumentException(
                            "set-property requires propertyPath and value.");
                    var serialized = new SerializedObject(profile);
                    SerializedProperty property = serialized.FindProperty(propertyPath);
                    if (property == null)
                        throw new ArgumentException(
                            $"BuildProfile serialized property '{propertyPath}' was not found.");
                    object before = VmAutomationComponentCommands.GetSerializedValue(property, 2, 32);
                    VmAutomationComponentCommands.SetSerializedValue(property, value);
                    result["propertyPath"] = propertyPath;
                    result["before"] = before;
                    result["requested"] = value;
                    break;
            }
            return result;
        }

        private static void RequireWritableProperty(Type type, string name)
        {
            PropertyInfo property = type.GetProperty(name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property == null || !property.CanWrite)
                throw new MissingMemberException(type.FullName, name);
        }

        private static void TryRestoreActiveProfile(Type profileType,
            UnityEngine.Object originalActive)
        {
            if (originalActive == null)
                return;
            try
            {
                profileType.GetMethod("SetActiveBuildProfile",
                        BindingFlags.Static | BindingFlags.Public |
                        BindingFlags.NonPublic)
                    ?.Invoke(null, new object[] { originalActive });
            }
            catch
            {
                // Preserve the primary transaction failure.
            }
        }

        private static Dictionary<string, object> ApplyOperation(Type profileType,
            Dictionary<string, object> operation)
        {
            string action = GetString(operation, "action").ToLowerInvariant();
            if (action == "create")
            {
                string profileName = ValidateProfileName(GetString(operation, "profileName"));
                Dictionary<string, object> platform = ResolveInstalledPlatform(profileType,
                    GetString(operation, "platformId"), out UnityEngine.GUID platformGuid);
                string expectedAssetPath = ExpectedProfileAssetPath(profileName);
                MethodInfo create = RequireCreateBuildProfileMethod(profileType);
                UnityEngine.Object createdProfile = create.Invoke(null,
                    new object[] { platformGuid, profileName, null }) as UnityEngine.Object;
                if (createdProfile == null || !profileType.IsInstanceOfType(createdProfile))
                    throw new InvalidOperationException(
                        $"Unity did not return the created BuildProfile '{profileName}'.");
                string assetPath = AssetDatabase.GetAssetPath(createdProfile);
                if (!string.Equals(assetPath, expectedAssetPath, StringComparison.Ordinal))
                {
                    if (!string.IsNullOrEmpty(assetPath))
                        AssetDatabase.DeleteAsset(assetPath);
                    throw new InvalidOperationException(
                        $"Unity created BuildProfile '{profileName}' at unexpected path '{assetPath}'.");
                }
                EditorUtility.SetDirty(createdProfile);
                return new Dictionary<string, object>
                {
                    { "action", action },
                    { "assetPath", assetPath },
                    { "profileName", profileName },
                    { "platformId", platform["platformId"] },
                    { "platformDisplayName", platform["displayName"] },
                    { "profile", ProfileInfo(profileType, createdProfile,
                        createdProfile == GetActiveProfile(profileType), assetPath) },
                };
            }
            if (action == "set-global-scenes")
            {
                EditorBuildSettings.scenes = ReadScenes(operation);
                return new Dictionary<string, object>
                {
                    { "action", action },
                    { "sceneCount", EditorBuildSettings.scenes.Length },
                };
            }

            UnityEngine.Object profile = LoadProfile(profileType, GetString(operation, "assetPath"));
            if (profile == null)
                throw new ArgumentException(
                    $"BuildProfile '{GetString(operation, "assetPath")}' was not found.");
            Undo.RecordObject(profile, "VM Unity Automation Edit Build Profile");

            switch (action)
            {
                case "set-active":
                    MethodInfo setActive = profileType.GetMethod("SetActiveBuildProfile",
                        BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
                    if (setActive == null)
                        throw new MissingMethodException(profileType.FullName,
                            "SetActiveBuildProfile");
                    setActive.Invoke(null, new object[] { profile });
                    break;
                case "set-scenes":
                    SetProperty(profileType, profile, "overrideGlobalScenes",
                        GetBool(operation, "overrideGlobalScenes", true));
                    SetProperty(profileType, profile, "scenes", ReadScenes(operation));
                    break;
                case "set-scripting-defines":
                    MethodInfo setDefines = profileType.GetMethod("SetAndApplyScriptingDefines",
                        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                    if (setDefines == null)
                        throw new MissingMethodException(profileType.FullName,
                            "SetAndApplyScriptingDefines");
                    if (!TryGetStringArray(operation, "defines", out string[] defines))
                        throw new ArgumentException("defines must be a string array.");
                    setDefines.Invoke(profile, new object[] { defines });
                    break;
                case "set-property":
                    string propertyPath = GetString(operation, "propertyPath");
                    if (string.IsNullOrEmpty(propertyPath) ||
                        !operation.TryGetValue("value", out object value))
                        throw new ArgumentException(
                            "set-property requires propertyPath and value.");
                    var serialized = new SerializedObject(profile);
                    serialized.Update();
                    SerializedProperty property = serialized.FindProperty(propertyPath);
                    if (property == null)
                        throw new ArgumentException(
                            $"BuildProfile serialized property '{propertyPath}' was not found.");
                    VmAutomationComponentCommands.SetSerializedValue(property, value);
                    serialized.ApplyModifiedProperties();
                    break;
            }

            EditorUtility.SetDirty(profile);
            return new Dictionary<string, object>
            {
                { "action", action },
                { "assetPath", AssetDatabase.GetAssetPath(profile) },
                { "profile", ProfileInfo(profileType, profile,
                    profile == GetActiveProfile(profileType), AssetDatabase.GetAssetPath(profile)) },
            };
        }

        private static Dictionary<string, object> ProfileInfo(Type profileType,
            UnityEngine.Object profile, bool active, string assetPath)
        {
            return new Dictionary<string, object>
            {
                { "assetPath", assetPath ?? "" },
                { "name", profile.name ?? "" },
                { "active", active },
                { "buildTarget", GetProperty(profileType, profile, "buildTarget")?.ToString() ?? "" },
                { "subtarget", GetProperty(profileType, profile, "subtarget")?.ToString() ?? "" },
                { "platformId", GetProperty(profileType, profile, "platformId")?.ToString() ?? "" },
                { "overrideGlobalScenes", GetProperty(profileType, profile, "overrideGlobalScenes") ?? false },
                { "hasScriptingDefines", GetProperty(profileType, profile, "hasScriptingDefines") ?? false },
                { "scriptingDefines", GetProperty(profileType, profile, "scriptingDefines") ?? Array.Empty<string>() },
                { "scenes", ReadProfileScenes(profileType, profile) },
                { "canBuildLocally", InvokeBool(profileType, profile, "CanBuildLocally") },
            };
        }

        private static UnityEngine.Object GetActiveProfile(Type profileType)
        {
            MethodInfo getter = profileType.GetMethod("GetActiveBuildProfile",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            return getter?.Invoke(null, null) as UnityEngine.Object;
        }

        private static List<Dictionary<string, object>> GetInstalledPlatforms(Type profileType)
        {
            MethodInfo getter = profileType.GetMethod("GetInstalledPlatformModules",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (getter == null)
                throw new MissingMethodException(profileType.FullName,
                    "GetInstalledPlatformModules");
            if (!(getter.Invoke(null, null) is IEnumerable installedPlatforms))
                throw new InvalidOperationException(
                    "Unity did not return its installed Build Profile platforms.");

            var result = new List<Dictionary<string, object>>();
            foreach (object installedPlatform in installedPlatforms)
            {
                if (installedPlatform == null)
                    continue;
                Type installedPlatformType = installedPlatform.GetType();
                object displayName = GetFieldOrProperty(installedPlatformType,
                    installedPlatform, "displayName");
                object platformGuid = GetFieldOrProperty(installedPlatformType,
                    installedPlatform, "platformGuid");
                if (platformGuid == null)
                    continue;
                result.Add(new Dictionary<string, object>
                {
                    { "displayName", displayName?.ToString() ?? "" },
                    { "platformId", platformGuid.ToString() },
                });
            }
            return result.OrderBy(platform => platform["displayName"].ToString(),
                StringComparer.Ordinal).ToList();
        }

        private static Dictionary<string, object> ResolveInstalledPlatform(Type profileType,
            string platformId, out UnityEngine.GUID platformGuid)
        {
            if (string.IsNullOrWhiteSpace(platformId) || platformId.Length != 32 ||
                platformId.Any(character => !Uri.IsHexDigit(character)))
            {
                platformGuid = default;
                throw new ArgumentException("platformId must be a valid Unity platform GUID.");
            }
            platformGuid = new UnityEngine.GUID(platformId);
            string normalizedPlatformId = platformGuid.ToString();
            Dictionary<string, object> platform = GetInstalledPlatforms(profileType)
                .SingleOrDefault(candidate => string.Equals(
                    candidate["platformId"].ToString(), normalizedPlatformId,
                    StringComparison.OrdinalIgnoreCase));
            if (platform == null)
                throw new ArgumentException(
                    $"Build Profile platform '{platformId}' is not installed in this Unity Editor.");
            return platform;
        }

        private static object GetFieldOrProperty(Type type, object target, string name)
        {
            FieldInfo field = type.GetField(name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field != null)
                return field.GetValue(target);
            return type.GetProperty(name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(target);
        }

        private static MethodInfo RequireCreateBuildProfileMethod(Type profileType)
        {
            MethodInfo method = profileType.GetMethods(
                    BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)
                .SingleOrDefault(candidate =>
                {
                    if (candidate.Name != "CreateBuildProfile")
                        return false;
                    ParameterInfo[] parameters = candidate.GetParameters();
                    return parameters.Length == 3 &&
                           parameters[0].ParameterType == typeof(UnityEngine.GUID) &&
                           parameters[1].ParameterType == typeof(string);
                });
            return method ?? throw new MissingMethodException(profileType.FullName,
                "CreateBuildProfile");
        }

        private static string ValidateProfileName(string profileName)
        {
            if (string.IsNullOrWhiteSpace(profileName) ||
                !string.Equals(profileName, profileName.Trim(), StringComparison.Ordinal))
                throw new ArgumentException("profileName must be a non-empty trimmed asset name.");
            if (profileName == "." || profileName == ".." ||
                profileName.EndsWith(".asset", StringComparison.OrdinalIgnoreCase) ||
                profileName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
                profileName.IndexOf('/') >= 0 || profileName.IndexOf('\\') >= 0)
                throw new ArgumentException(
                    $"profileName '{profileName}' must be a valid asset name without the .asset extension.");
            return profileName;
        }

        private static string ExpectedProfileAssetPath(string profileName)
        {
            return $"Assets/Settings/Build Profiles/{profileName}.asset";
        }

        private static UnityEngine.Object LoadProfile(Type profileType, string assetPath)
        {
            UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(assetPath);
            return asset != null && profileType.IsInstanceOfType(asset) ? asset : null;
        }

        private static object GetProperty(Type type, object target, string name)
        {
            return type.GetProperty(name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(target);
        }

        private static void SetProperty(Type type, object target, string name, object value)
        {
            PropertyInfo property = type.GetProperty(name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property == null || !property.CanWrite)
                throw new MissingMemberException(type.FullName, name);
            property.SetValue(target, value);
        }

        private static bool InvokeBool(Type type, object target, string name)
        {
            object result = type.GetMethod(name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?.Invoke(target, null);
            return result is bool value && value;
        }

        private static List<Dictionary<string, object>> ReadProfileScenes(Type profileType,
            object profile)
        {
            if (!(GetProperty(profileType, profile, "scenes") is EditorBuildSettingsScene[] scenes))
                return new List<Dictionary<string, object>>();
            return scenes.Select(SceneInfo).ToList();
        }

        private static EditorBuildSettingsScene[] ReadScenes(Dictionary<string, object> operation)
        {
            if (!(operation.TryGetValue("scenes", out object value) && value is List<object> scenes))
                throw new ArgumentException("scenes must be an array.");
            return scenes.Select((item, index) =>
            {
                if (item is string path)
                {
                    ValidateScenePath(path, index);
                    return new EditorBuildSettingsScene(path, true);
                }
                if (!(item is Dictionary<string, object> scene))
                    throw new ArgumentException($"scenes[{index}] must be a path or object.");
                string scenePath = GetString(scene, "path");
                if (string.IsNullOrEmpty(scenePath))
                    throw new ArgumentException($"scenes[{index}].path is required.");
                ValidateScenePath(scenePath, index);
                return new EditorBuildSettingsScene(scenePath,
                    GetBool(scene, "enabled", true));
            }).ToArray();
        }

        private static void ValidateScenePath(string scenePath, int index)
        {
            if (!scenePath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase) ||
                AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null)
            {
                throw new ArgumentException(
                    $"scenes[{index}] path '{scenePath}' is not a Scene asset.");
            }
        }

        private static Dictionary<string, object> SceneInfo(EditorBuildSettingsScene scene)
        {
            return new Dictionary<string, object>
            {
                { "path", scene.path ?? "" },
                { "enabled", scene.enabled },
                { "guid", scene.guid.ToString() },
            };
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

        private static int GetInt(Dictionary<string, object> values, string key, int defaultValue)
        {
            return values != null && values.TryGetValue(key, out object value) && value != null
                ? Convert.ToInt32(value)
                : defaultValue;
        }

        private static List<object> GetList(Dictionary<string, object> values, string key)
        {
            return values != null && values.TryGetValue(key, out object value)
                ? value as List<object>
                : null;
        }

        private static bool TryGetStringArray(Dictionary<string, object> values, string key,
            out string[] result)
        {
            result = Array.Empty<string>();
            if (values == null || !values.TryGetValue(key, out object value) ||
                !(value is List<object> list))
                return false;
            if (list.Any(item => !(item is string)))
                return false;
            result = list.Cast<string>().Where(item => !string.IsNullOrEmpty(item)).ToArray();
            return true;
        }
    }
}
