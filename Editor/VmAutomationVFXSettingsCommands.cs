using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace VMUnityAutomation.Editor
{
    internal static class VmAutomationVFXSettingsCommands
    {
        private sealed class PreferenceSnapshot
        {
            internal bool Exists;
            internal object Value;
        }

        private sealed class GraphCompilationSnapshot
        {
            internal VmAutomationVFXGraphSession Session;
            internal object Mode;
        }

        private static readonly Dictionary<string, string> ProjectFields =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "fixedTimeStep", "m_FixedTimeStep" },
                { "maxDeltaTime", "m_MaxDeltaTime" },
                { "maxScrubTime", "m_MaxScrubTime" },
                { "maxCapacity", "m_MaxCapacity" },
                { "batchEmptyLifetime", "m_BatchEmptyLifetime" },
                { "indirectShader", "m_IndirectShader" },
                { "copyBufferShader", "m_CopyBufferShader" },
                { "prefixSumShader", "m_PrefixSumShader" },
                { "sortShader", "m_SortShader" },
                { "stripUpdateShader", "m_StripUpdateShader" },
                { "runtimeResources", "m_RuntimeResources" },
            };

        private static readonly Dictionary<string, string> PreferenceKeys =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "displayExperimentalOperator", "experimentalOperatorKey" },
                { "displayExtraDebugInfo", "extraDebugInfoKey" },
                { "forceEditionCompilation", "forceEditionCompilationKey" },
                { "allowShaderExternalization", "allowShaderExternalizationKey" },
                { "generateShadersWithDebugSymbols",
                    "generateShadersWithDebugSymbolsKey" },
                { "verboseCompilation", "advancedLogsKey" },
                { "cameraBuffersFallback", "cameraBuffersFallbackKey" },
                { "multithreadUpdateEnabled", "multithreadUpdateEnabledKey" },
                { "instancingEnabled", "instancingEnabledKey" },
                { "showPackageIndexingBanner", "showPackageIndexingBannerKey" },
                { "authoringPrewarmStepCountPerSeconds",
                    "authoringPrewarmStepCountPerSecondsKey" },
                { "authoringPrewarmMaxTime", "authoringPrewarmMaxTimeKey" },
                { "visualEffectTargetListed", "visualEffectTargetListedKey" },
            };

        internal static object Info(Dictionary<string, object> args)
        {
            if (!ValidateKeys(args, new[] { "scope", "_agentId" },
                    out object keyError))
                return keyError;
            if (!VmAutomationVFXReflection.IsAvailable)
                return VmAutomationResponse.Error(
                    "VFX Graph is not available. Install com.unity.visualeffectgraph.",
                    "capability_unavailable");
            string scope = GetString(args, "scope").ToLowerInvariant();
            if (scope != "" && scope != "project" && scope != "user")
                return VmAutomationResponse.Error("scope must be project, user, or omitted.",
                    "invalid_arguments");
            try
            {
                return new Dictionary<string, object>
                {
                    { "success", true }, { "scope", scope },
                    { "projectSettingsPath", "ProjectSettings/VFXManager.asset" },
                    { "project", scope == "user"
                        ? (object)new List<object>() : ProjectSettingSummaries() },
                    { "user", scope == "project"
                        ? (object)new List<object>() : PreferenceSummaries() },
                };
            }
            catch (Exception exception)
            {
                return VmAutomationVFXError.Response(exception,
                    "vfx_settings_info_failed");
            }
        }

        internal static object Transaction(Dictionary<string, object> args)
        {
            if (!ValidateKeys(args, new[]
                {
                    "operations", "dryRun", "_agentId",
                }, out object keyError))
                return keyError;
            List<object> rawOperations = args != null &&
                args.TryGetValue("operations", out object raw)
                    ? VmAutomationVFXGraphMutationContext.AsList(raw) : null;
            if (rawOperations == null || rawOperations.Count == 0 ||
                rawOperations.Count > 64)
                return VmAutomationResponse.Error(
                    "operations must contain between 1 and 64 entries.",
                    "invalid_arguments");
            if (!VmAutomationVFXReflection.IsAvailable)
                return VmAutomationResponse.Error(
                    "VFX Graph is not available. Install com.unity.visualeffectgraph.",
                    "capability_unavailable");
            UnityEngine.Object manager;
            SerializedObject serialized;
            string projectBackup;
            Dictionary<string, PreferenceSnapshot> preferenceBackup;
            bool dryRun;
            try
            {
                manager = LoadManager();
                serialized = new SerializedObject(manager);
                projectBackup = EditorJsonUtility.ToJson(manager, true);
                preferenceBackup = CapturePreferences();
                dryRun = GetBool(args, "dryRun", false);
            }
            catch (Exception exception)
            {
                return VmAutomationVFXError.Response(exception,
                    "vfx_settings_transaction_failed");
            }
            bool reimportAll = false;
            bool compilationModeChanged = false;
            bool publicationStarted = false;
            List<GraphCompilationSnapshot> compilationSnapshots = null;
            var results = new List<Dictionary<string, object>>();
            try
            {
                for (int index = 0; index < rawOperations.Count; index++)
                {
                    Dictionary<string, object> operation =
                        VmAutomationVFXGraphMutationContext.AsDictionary(rawOperations[index]) ??
                        throw new ArgumentException(
                            $"operations[{index}] must be an object.");
                    ValidateOperation(operation, index);
                    string scope = GetString(operation, "scope").ToLowerInvariant();
                    string name = RequireString(operation, "name");
                    object value = Required(operation, "value");
                    string reimport = GetString(operation, "reimport", "none")
                        .ToLowerInvariant();
                    if (reimport != "none" && reimport != "all")
                        throw new ArgumentException(
                            $"operations[{index}].reimport must be none or all.");
                    if (scope == "project")
                        SetProjectSetting(serialized, name, value, index);
                    else if (scope == "user")
                    {
                        SetPreference(name, value, index);
                        compilationModeChanged |= name ==
                            "forceEditionCompilation" && !Equals(
                                preferenceBackup[name].Value,
                                ReadPreference(name));
                    }
                    else
                        throw new ArgumentException(
                            $"operations[{index}].scope must be project or user.");
                    reimportAll |= reimport == "all";
                    results.Add(new Dictionary<string, object>
                    {
                        { "index", index }, { "scope", scope },
                        { "name", name }, { "value", scope == "project"
                            ? ReadSerialized(serialized.FindProperty(
                                ProjectFields[name])) : ReadPreference(name) },
                        { "reimport", reimport },
                    });
                }
                if (dryRun)
                {
                    EditorJsonUtility.FromJsonOverwrite(projectBackup, manager);
                    RestorePreferences(preferenceBackup);
                }
                else
                {
                    if (compilationModeChanged)
                        compilationSnapshots = CaptureCompilationModes();
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(manager);
                    AssetDatabase.SaveAssets();
                    MarkPreferencesDirty();
                    if (compilationModeChanged)
                    {
                        publicationStarted = true;
                        ApplyCompilationMode(compilationSnapshots,
                            GetBoolPreference("forceEditionCompilation"),
                            reimportAll);
                    }
                    else if (reimportAll)
                    {
                        publicationStarted = true;
                        ReimportAllVFX();
                    }
                }
                return new Dictionary<string, object>
                {
                    { "success", true }, { "dryRun", dryRun },
                    { "operationCount", results.Count }, { "results", results },
                    { "reimportedAll", !dryRun && reimportAll },
                    { "settings", new Dictionary<string, object>
                        {
                            { "project", ProjectSettingSummaries() },
                            { "user", PreferenceSummaries() },
                        } },
                };
            }
            catch (Exception exception)
            {
                try
                {
                    EditorJsonUtility.FromJsonOverwrite(projectBackup, manager);
                    EditorUtility.SetDirty(manager);
                    AssetDatabase.SaveAssets();
                    RestorePreferences(preferenceBackup);
                    if (compilationSnapshots != null)
                        RestoreCompilationModes(compilationSnapshots,
                            publicationStarted && reimportAll);
                    else if (publicationStarted && reimportAll)
                        ReimportAllVFX();
                }
                catch (Exception rollbackException)
                {
                    return VmAutomationResponse.Error(
                        $"VFX settings transaction failed: {VmAutomationVFXReflection.Unwrap(exception).Message}. Rollback failed: {VmAutomationVFXReflection.Unwrap(rollbackException).Message}",
                        "vfx_transaction_rollback_failed");
                }
                Exception failure = VmAutomationVFXReflection.Unwrap(exception);
                return VmAutomationResponse.Error(
                    failure.Message, VmAutomationVFXError.Code(failure,
                        "vfx_settings_transaction_failed"), false,
                    new Dictionary<string, object>
                    {
                        { "failedOperationIndex", results.Count },
                        { "rolledBack", true },
                    });
            }
        }

        private static List<Dictionary<string, object>> ProjectSettingSummaries()
        {
            var serialized = new SerializedObject(LoadManager());
            var result = new List<Dictionary<string, object>>
            {
                new Dictionary<string, object>
                {
                    { "scope", "project" },
                    { "name", "currentRenderPipeline" },
                    { "type", typeof(string).FullName },
                    { "value", CurrentRenderPipeline() },
                    { "available", true },
                    { "mutable", false },
                    { "range", null },
                    { "requiresGraphReimport", false },
                    { "persistenceOwner", "GraphicsSettings.currentRenderPipeline" },
                },
            };
            result.AddRange(ProjectFields.Select(pair =>
            {
                SerializedProperty property = serialized.FindProperty(pair.Value);
                return new Dictionary<string, object>
                {
                    { "scope", "project" }, { "name", pair.Key },
                    { "serializedProperty", pair.Value },
                    { "type", ProjectValueType(pair.Key, property)?.FullName ??
                        "unavailable" },
                    { "value", ReadSerialized(property) },
                    { "available", property != null },
                    { "mutable", property != null },
                    { "range", ProjectRange(pair.Key) },
                    { "requiresGraphReimport", false },
                    { "persistenceOwner", "ProjectSettings/VFXManager.asset" },
                };
            }));
            return result;
        }

        private static List<Dictionary<string, object>> PreferenceSummaries()
        {
            return PreferenceKeys.Keys.Select(name =>
            {
                bool available = TryPreferenceDescriptor(name, out string key,
                    out Type type);
                return new Dictionary<string, object>
                {
                    { "scope", "user" }, { "name", name },
                    { "editorPrefsKey", key ?? "" },
                    { "type", type?.FullName ?? "unavailable" },
                    { "value", available ? ReadPreference(name) : null },
                    { "defaultValue", available
                        ? PreferenceDefault(name) : null },
                    { "available", available }, { "mutable", available },
                    { "documented", name != "showPackageIndexingBanner" },
                    { "range", PreferenceRange(name) },
                    { "enumValues", name == "cameraBuffersFallback"
                        ? (object)CameraFallbackNames() : new List<string>() },
                    { "requiresGraphReimport", name ==
                        "generateShadersWithDebugSymbols" || name ==
                        "forceEditionCompilation" },
                    { "persistenceOwner", "EditorPrefs" },
                };
            }).ToList();
        }

        private static void SetProjectSetting(SerializedObject serialized,
            string name, object rawValue, int index)
        {
            if (!ProjectFields.TryGetValue(name, out string propertyName))
                throw new ArgumentException(
                    $"operations[{index}].name '{name}' is not a mutable documented VFX project setting.");
            SerializedProperty property = serialized.FindProperty(propertyName) ??
                throw new MissingMemberException(
                    $"Installed Unity version does not expose {propertyName}.");
            switch (property.propertyType)
            {
                case SerializedPropertyType.Float:
                    property.doubleValue = (float)VmAutomationVFXValueCodec.ConvertTo(
                        rawValue, typeof(float),
                        $"operations[{index}].value");
                    break;
                case SerializedPropertyType.Integer:
                    property.longValue = (int)VmAutomationVFXValueCodec.ConvertTo(
                        rawValue, typeof(int),
                        $"operations[{index}].value");
                    break;
                case SerializedPropertyType.ObjectReference:
                    Type objectType = ProjectValueType(name, property) ??
                                      throw new MissingMemberException(
                                          $"VFX project setting '{name}' object type is unavailable.");
                    property.objectReferenceValue = (UnityEngine.Object)
                        VmAutomationVFXValueCodec.ConvertTo(rawValue, objectType,
                            $"operations[{index}].value");
                    break;
                default:
                    throw new InvalidOperationException(
                        $"Unsupported VFXManager property type {property.propertyType}.");
            }
            ValidateProjectValue(name, property);
        }

        private static Type ProjectValueType(string name,
            SerializedProperty property)
        {
            if (property == null)
                return null;
            switch (property.propertyType)
            {
                case SerializedPropertyType.Float:
                    return typeof(float);
                case SerializedPropertyType.Integer:
                    return typeof(int);
                case SerializedPropertyType.ObjectReference:
                    if (property.objectReferenceValue != null)
                        return property.objectReferenceValue.GetType();
                    if (name == "runtimeResources")
                        return VmAutomationVFXReflection.FindType(
                            "UnityEngine.VFX.VFXRuntimeResources");
                    return typeof(ComputeShader);
                default:
                    return null;
            }
        }

        private static string CurrentRenderPipeline()
        {
            Type library = VmAutomationVFXReflection.RequireType(
                VmAutomationVFXReflection.LibraryTypeName);
            object binder = VmAutomationVFXReflection.Get(library, "currentSRPBinder");
            return VmAutomationVFXReflection.Get(binder, "SRPAssetTypeStr")?.ToString() ??
                   "Built-in Render Pipeline";
        }

        private static void ValidateProjectValue(string name,
            SerializedProperty property)
        {
            double value = property.propertyType == SerializedPropertyType.Float
                ? property.doubleValue : property.longValue;
            switch (name)
            {
                case "fixedTimeStep":
                    if (value <= 0 || value > 1) throw new ArgumentOutOfRangeException(
                        name, "fixedTimeStep must be in (0, 1].");
                    break;
                case "maxDeltaTime":
                    if (value <= 0 || value > 10) throw new ArgumentOutOfRangeException(
                        name, "maxDeltaTime must be in (0, 10].");
                    break;
                case "maxScrubTime":
                    if (value < 0 || value > 3600) throw new ArgumentOutOfRangeException(
                        name, "maxScrubTime must be in [0, 3600].");
                    break;
                case "maxCapacity":
                    if (value < 1 || value > int.MaxValue)
                        throw new ArgumentOutOfRangeException(name,
                            "maxCapacity must be in [1, int.MaxValue].");
                    break;
                case "batchEmptyLifetime":
                    if (value < 0 || value > int.MaxValue)
                        throw new ArgumentOutOfRangeException(name,
                            "batchEmptyLifetime must be non-negative.");
                    break;
            }
        }

        private static void SetPreference(string name, object rawValue, int index)
        {
            if (!PreferenceKeys.ContainsKey(name))
                throw new ArgumentException(
                    $"operations[{index}].name '{name}' is not a documented VFX user preference.");
            if (!TryPreferenceDescriptor(name, out string key, out Type type))
                throw VmAutomationVFXError.Create("unsupported_vfx_version",
                    $"The installed VFX Graph version does not expose user preference '{name}'.");
            if (name == "cameraBuffersFallback")
                EditorPrefs.SetInt(key, ParseCameraFallback(rawValue));
            else if (type == typeof(bool))
                EditorPrefs.SetBool(key, (bool)VmAutomationVFXValueCodec.ConvertTo(
                    rawValue, typeof(bool), $"operations[{index}].value"));
            else if (type == typeof(int))
            {
                int value = (int)VmAutomationVFXValueCodec.ConvertTo(rawValue,
                    typeof(int), $"operations[{index}].value");
                if (name == "authoringPrewarmStepCountPerSeconds" &&
                    (value < 0 || value > 200))
                    throw new ArgumentOutOfRangeException(name,
                        "authoringPrewarmStepCountPerSeconds must be in [0, 200].");
                EditorPrefs.SetInt(key, value);
            }
            else
            {
                float value = (float)VmAutomationVFXValueCodec.ConvertTo(rawValue,
                    typeof(float), $"operations[{index}].value");
                if (value < 0f || value > 60f)
                    throw new ArgumentOutOfRangeException(name,
                        "authoringPrewarmMaxTime must be in [0, 60].");
                EditorPrefs.SetFloat(key, value);
            }
            MarkPreferencesDirty();
        }

        private static object ReadPreference(string name)
        {
            string key = ResolvePreferenceKey(name);
            if (name == "cameraBuffersFallback")
            {
                int value = EditorPrefs.GetInt(key, CameraFallbackDefaultValue());
                return CameraFallbackName(value);
            }
            object defaultValue = PreferenceDefault(name);
            Type type = PreferenceType(name);
            if (type == typeof(bool))
                return EditorPrefs.GetBool(key, (bool)defaultValue);
            if (type == typeof(int))
                return EditorPrefs.GetInt(key, (int)defaultValue);
            return EditorPrefs.GetFloat(key, (float)defaultValue);
        }

        private static Dictionary<string, PreferenceSnapshot> CapturePreferences()
        {
            return PreferenceKeys.Keys.Where(name =>
                    TryPreferenceDescriptor(name, out string _, out Type _))
                .ToDictionary(name => name, name =>
            {
                string key = ResolvePreferenceKey(name);
                return new PreferenceSnapshot
                {
                    Exists = EditorPrefs.HasKey(key),
                    Value = ReadPreference(name),
                };
            }, StringComparer.Ordinal);
        }

        private static void RestorePreferences(
            Dictionary<string, PreferenceSnapshot> backup)
        {
            if (backup.Count == 0)
                return;
            foreach (KeyValuePair<string, PreferenceSnapshot> pair in backup)
            {
                string key = ResolvePreferenceKey(pair.Key);
                if (pair.Value.Exists)
                    SetPreference(pair.Key, pair.Value.Value, -1);
                else
                    EditorPrefs.DeleteKey(key);
            }
            MarkPreferencesDirty();
        }

        private static string ResolvePreferenceKey(string name)
        {
            if (TryPreferenceDescriptor(name, out string key, out Type _))
                return key;
            throw VmAutomationVFXError.Create("unsupported_vfx_version",
                $"The installed VFX Graph version does not expose user preference '{name}'.");
        }

        private static bool TryPreferenceDescriptor(string name,
            out string key, out Type valueType)
        {
            key = null;
            valueType = null;
            if (!PreferenceKeys.TryGetValue(name, out string fieldName))
                return false;
            Type preferenceType = VmAutomationVFXReflection.FindType(
                VmAutomationVFXReflection.ViewPreferenceTypeName);
            if (preferenceType == null || preferenceType.GetMethod("SetDirty",
                    System.Reflection.BindingFlags.Static |
                    System.Reflection.BindingFlags.Public |
                    System.Reflection.BindingFlags.NonPublic) == null)
                return false;
            key = VmAutomationVFXReflection.Get(preferenceType, fieldName)?.ToString();
            if (string.IsNullOrEmpty(key))
                return false;
            valueType = name == "authoringPrewarmMaxTime" ? typeof(float) :
                name == "authoringPrewarmStepCountPerSeconds" ? typeof(int) :
                name == "cameraBuffersFallback"
                    ? VmAutomationVFXReflection.FindType(
                        VmAutomationVFXReflection.CameraBufferFallbackTypeName)
                    : typeof(bool);
            return valueType != null;
        }

        private static Type PreferenceType(string name)
        {
            if (name == "authoringPrewarmMaxTime") return typeof(float);
            if (name == "authoringPrewarmStepCountPerSeconds") return typeof(int);
            if (name == "cameraBuffersFallback")
                return VmAutomationVFXReflection.RequireType(
                    VmAutomationVFXReflection.CameraBufferFallbackTypeName);
            return typeof(bool);
        }

        private static object PreferenceDefault(string name)
        {
            switch (name)
            {
                case "multithreadUpdateEnabled":
                case "instancingEnabled":
                case "showPackageIndexingBanner": return true;
                case "authoringPrewarmStepCountPerSeconds": return 20;
                case "authoringPrewarmMaxTime": return 3f;
                case "cameraBuffersFallback": return CameraFallbackName(
                    CameraFallbackDefaultValue());
                default: return false;
            }
        }

        private static Dictionary<string, object> ProjectRange(string name)
        {
            switch (name)
            {
                case "fixedTimeStep": return Range(0, 1, false, true);
                case "maxDeltaTime": return Range(0, 10, false, true);
                case "maxScrubTime": return Range(0, 3600, true, true);
                case "maxCapacity": return Range(1, int.MaxValue, true, true);
                case "batchEmptyLifetime": return Range(0, int.MaxValue, true,
                    true);
                default: return null;
            }
        }

        private static Dictionary<string, object> PreferenceRange(string name)
        {
            if (name == "authoringPrewarmStepCountPerSeconds")
                return Range(0, 200, true, true);
            if (name == "authoringPrewarmMaxTime")
                return Range(0, 60, true, true);
            return null;
        }

        private static Dictionary<string, object> Range(object min, object max,
            bool minInclusive, bool maxInclusive)
        {
            return new Dictionary<string, object>
            {
                { "min", min }, { "max", max },
                { "minInclusive", minInclusive },
                { "maxInclusive", maxInclusive },
            };
        }

        private static object ReadSerialized(SerializedProperty property)
        {
            if (property == null) return null;
            switch (property.propertyType)
            {
                case SerializedPropertyType.Float: return property.doubleValue;
                case SerializedPropertyType.Integer: return property.longValue;
                case SerializedPropertyType.Boolean: return property.boolValue;
                case SerializedPropertyType.String: return property.stringValue;
                case SerializedPropertyType.ObjectReference:
                    return VmAutomationVFXValueCodec.Sanitize(
                        property.objectReferenceValue);
                case SerializedPropertyType.Enum:
                    return property.enumDisplayNames.Length > property.enumValueIndex &&
                           property.enumValueIndex >= 0
                        ? property.enumDisplayNames[property.enumValueIndex]
                        : property.intValue.ToString();
                default: return property.propertyType.ToString();
            }
        }

        private static UnityEngine.Object LoadManager()
        {
            return AssetDatabase.LoadAllAssetsAtPath(
                    "ProjectSettings/VFXManager.asset").FirstOrDefault() ??
                   throw new InvalidOperationException(
                       "ProjectSettings/VFXManager.asset is unavailable.");
        }

        private static void MarkPreferencesDirty()
        {
            Type type = VmAutomationVFXReflection.RequireType(
                VmAutomationVFXReflection.ViewPreferenceTypeName);
            VmAutomationVFXReflection.Invoke(type, "SetDirty");
        }

        private static List<GraphCompilationSnapshot> CaptureCompilationModes()
        {
            var snapshots = new List<GraphCompilationSnapshot>();
            foreach (string path in VFXAssetPaths())
            {
                VmAutomationVFXGraphSession session = OpenGraphForCompilationMode(path);
                snapshots.Add(new GraphCompilationSnapshot
                {
                    Session = session,
                    Mode = VmAutomationVFXReflection.Invoke(session.Graph,
                        "GetCompilationMode"),
                });
            }
            return snapshots;
        }

        private static void ApplyCompilationMode(
            IReadOnlyList<GraphCompilationSnapshot> snapshots, bool edition,
            bool reimport)
        {
            Type modeType = VmAutomationVFXReflection.RequireType(
                VmAutomationVFXReflection.CompilationModeTypeName);
            object mode = Enum.Parse(modeType, edition ? "Edition" : "Runtime");
            foreach (GraphCompilationSnapshot snapshot in snapshots)
                VmAutomationVFXReflection.Invoke(snapshot.Session.Graph,
                    "SetCompilationMode", mode, false);
            if (reimport)
                ReimportVFX(snapshots.Select(snapshot =>
                    snapshot.Session.AssetPath));
        }

        private static void RestoreCompilationModes(
            IReadOnlyList<GraphCompilationSnapshot> snapshots, bool reimport)
        {
            foreach (GraphCompilationSnapshot snapshot in snapshots)
                VmAutomationVFXReflection.Invoke(snapshot.Session.Graph,
                    "SetCompilationMode", snapshot.Mode, false);
            if (reimport)
                ReimportVFX(snapshots.Select(snapshot =>
                    snapshot.Session.AssetPath));
        }

        private static VmAutomationVFXGraphSession OpenGraphForCompilationMode(
            string path)
        {
            if (VmAutomationVFXGraphSession.TryOpen(path, out VmAutomationVFXGraphSession session,
                    out object openError))
                return session;
            throw new InvalidOperationException(
                $"Could not open VFX asset '{path}' while preparing compilation mode mutation: {MiniJson.Serialize(openError)}");
        }

        private static void ReimportAllVFX()
        {
            ReimportVFX(VFXAssetPaths());
        }

        private static void ReimportVFX(IEnumerable<string> paths)
        {
            foreach (string path in paths.Distinct(StringComparer.Ordinal))
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate |
                    ImportAssetOptions.ForceSynchronousImport);
        }

        private static List<string> VFXAssetPaths()
        {
            string[] guids = AssetDatabase.FindAssets("t:VisualEffectAsset");
            if (guids.Length > VmAutomationVFXLimits.ProjectAssets)
                throw VmAutomationVFXError.Create("response_too_large",
                    $"The project contains more than {VmAutomationVFXLimits.ProjectAssets} VisualEffectAsset records; project-wide VFX mutation is bounded.");
            List<string> paths = guids.Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => !string.IsNullOrEmpty(path))
                .Distinct(StringComparer.Ordinal).ToList();
            if (paths.Count > VmAutomationVFXLimits.ProjectAssets)
                throw VmAutomationVFXError.Create("response_too_large",
                    $"The project contains more than {VmAutomationVFXLimits.ProjectAssets} distinct VisualEffectAsset paths.");
            return paths;
        }

        private static List<string> CameraFallbackNames()
        {
            Type type = VmAutomationVFXReflection.FindType(
                VmAutomationVFXReflection.CameraBufferFallbackTypeName);
            return type?.IsEnum == true ? Enum.GetNames(type).ToList() :
                new List<string>();
        }

        private static int ParseCameraFallback(object raw)
        {
            Type type = VmAutomationVFXReflection.RequireType(
                VmAutomationVFXReflection.CameraBufferFallbackTypeName);
            object value = VmAutomationVFXValueCodec.ConvertTo(raw, type,
                "value");
            return Convert.ToInt32(value);
        }

        private static string CameraFallbackName(int value)
        {
            Type type = VmAutomationVFXReflection.FindType(
                VmAutomationVFXReflection.CameraBufferFallbackTypeName);
            return type?.IsEnum == true
                ? Enum.ToObject(type, value).ToString() : value.ToString();
        }

        private static int CameraFallbackDefaultValue()
        {
            Type type = VmAutomationVFXReflection.RequireType(
                VmAutomationVFXReflection.CameraBufferFallbackTypeName);
            if (!type.IsEnum || !Enum.GetNames(type).Contains("PreferMainCamera"))
                throw new MissingMemberException(type.FullName,
                    "PreferMainCamera");
            return Convert.ToInt32(Enum.Parse(type, "PreferMainCamera"));
        }

        private static bool GetBoolPreference(string name)
        {
            return Convert.ToBoolean(ReadPreference(name));
        }

        private static void ValidateOperation(Dictionary<string, object> operation,
            int index)
        {
            var allowed = new HashSet<string>(new[]
            {
                "scope", "name", "value", "reimport",
            }, StringComparer.Ordinal);
            string unknown = operation.Keys.FirstOrDefault(key =>
                !allowed.Contains(key));
            if (unknown != null)
                throw new ArgumentException(
                    $"operations[{index}] contains unsupported field '{unknown}'.");
            RequireString(operation, "scope");
            RequireString(operation, "name");
            Required(operation, "value");
        }

        private static bool ValidateKeys(Dictionary<string, object> args,
            IEnumerable<string> allowed, out object error)
        {
            var set = new HashSet<string>(allowed, StringComparer.Ordinal);
            string unknown = args?.Keys.FirstOrDefault(key => !set.Contains(key));
            if (unknown == null)
            {
                error = null;
                return true;
            }
            error = VmAutomationResponse.Error($"Unsupported argument '{unknown}'.",
                "invalid_arguments");
            return false;
        }

        private static string GetString(Dictionary<string, object> args, string key,
            string defaultValue = "")
        {
            return args != null && args.TryGetValue(key, out object value) &&
                   value != null ? value.ToString() : defaultValue;
        }

        private static string RequireString(Dictionary<string, object> args,
            string key)
        {
            string value = GetString(args, key);
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentException(key + " is required.");
            return value;
        }

        private static object Required(Dictionary<string, object> args, string key)
        {
            if (args == null || !args.TryGetValue(key, out object value))
                throw new ArgumentException(key + " is required.");
            return value;
        }

        private static bool GetBool(Dictionary<string, object> args, string key,
            bool defaultValue)
        {
            return args != null && args.TryGetValue(key, out object value) &&
                   value != null ? (bool)VmAutomationVFXValueCodec.ConvertTo(value,
                       typeof(bool), key) : defaultValue;
        }
    }
}
