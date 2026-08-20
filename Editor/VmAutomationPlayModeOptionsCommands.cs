using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

namespace VMUnityAutomation.Editor
{
    internal static class VmAutomationPlayModeOptionsCommands
    {
        private const string EditModeRequiredErrorCode =
            "edit_mode_required";
        private const string UpdateFailedErrorCode =
            "play_mode_options_update_failed";
        private const string EditorSettingsRelativePath =
            "ProjectSettings/EditorSettings.asset";
        private const string EnabledPropertyName =
            "m_EnterPlayModeOptionsEnabled";
        private const string OptionsPropertyName =
            "m_EnterPlayModeOptions";

        internal static object Execute(Dictionary<string, object> arguments)
        {
            arguments ??= new Dictionary<string, object>();

            bool hasEnabled = TryGetOptionalBool(
                arguments, "enabled", out bool enabled);
            bool hasDisableDomainReload = TryGetOptionalBool(
                arguments, "disableDomainReload",
                out bool disableDomainReload);
            bool hasDisableSceneReload = TryGetOptionalBool(
                arguments, "disableSceneReload",
                out bool disableSceneReload);
            bool mutates = hasEnabled || hasDisableDomainReload ||
                           hasDisableSceneReload;

            Dictionary<string, object> previous = CaptureState();
            if (mutates == false)
            {
                return new Dictionary<string, object>
                {
                    { "changed", false },
                    { "previous", previous },
                    { "current", previous },
                };
            }

            if (EditorApplication.isPlaying ||
                EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return VmAutomationResponse.Error(
                    "Play Mode options can only be changed while Unity is " +
                    "in stable Edit Mode.",
                    EditModeRequiredErrorCode,
                    false,
                    new Dictionary<string, object>
                    {
                        { "isPlaying", EditorApplication.isPlaying },
                        {
                            "isPlayingOrWillChangePlaymode",
                            EditorApplication.isPlayingOrWillChangePlaymode
                        },
                    });
            }

            bool nextEnabled = hasEnabled
                ? enabled
                : EditorSettings.enterPlayModeOptionsEnabled;
            EnterPlayModeOptions nextOptions =
                EditorSettings.enterPlayModeOptions;
            if (hasDisableDomainReload)
            {
                nextOptions = SetFlag(
                    nextOptions,
                    EnterPlayModeOptions.DisableDomainReload,
                    disableDomainReload);
            }

            if (hasDisableSceneReload)
            {
                nextOptions = SetFlag(
                    nextOptions,
                    EnterPlayModeOptions.DisableSceneReload,
                    disableSceneReload);
            }

            if (nextEnabled == false)
            {
                if ((hasDisableDomainReload && disableDomainReload) ||
                    (hasDisableSceneReload && disableSceneReload))
                {
                    return VmAutomationResponse.Error(
                        "Disabled Enter Play Mode Options cannot also " +
                        "request skipped reloads. Set 'enabled' to true " +
                        "in the same call before enabling either flag.",
                        "invalid_arguments",
                        false);
                }

                nextOptions = EnterPlayModeOptions.None;
            }

            PersistedState previousPersisted = CapturePersistedState();
            if ((bool)previous["enabled"] == nextEnabled &&
                (int)previous["optionsValue"] == (int)nextOptions &&
                previousPersisted.Enabled == nextEnabled &&
                previousPersisted.OptionsValue == (int)nextOptions)
            {
                return new Dictionary<string, object>
                {
                    { "changed", false },
                    { "previous", previous },
                    { "current", previous },
                };
            }

            PersistState(nextEnabled, nextOptions);

            Dictionary<string, object> current = CaptureState();
            PersistedState persisted = CapturePersistedState();
            if ((bool)current["enabled"] != nextEnabled ||
                (int)current["optionsValue"] != (int)nextOptions ||
                persisted.Enabled != nextEnabled ||
                persisted.OptionsValue != (int)nextOptions)
            {
                return VmAutomationResponse.Error(
                    "Unity did not persist the requested Play Mode options.",
                    UpdateFailedErrorCode,
                    false,
                    new Dictionary<string, object>
                    {
                        { "requestedEnabled", nextEnabled },
                        { "requestedOptionsValue", (int)nextOptions },
                        { "observed", current },
                        {
                            "persisted",
                            new Dictionary<string, object>
                            {
                                { "enabled", persisted.Enabled },
                                { "optionsValue", persisted.OptionsValue },
                            }
                        },
                    });
            }

            return new Dictionary<string, object>
            {
                {
                    "changed",
                    !StatesEqual(previous, current) ||
                    previousPersisted.Enabled != persisted.Enabled ||
                    previousPersisted.OptionsValue != persisted.OptionsValue
                },
                { "previous", previous },
                { "current", current },
            };
        }

        private static void PersistState(
            bool enabled,
            EnterPlayModeOptions options)
        {
            UnityEngine.Object settings = GetLiveEditorSettings();
            var serialized = new SerializedObject(settings);
            serialized.Update();
            SerializedProperty enabledProperty =
                serialized.FindProperty(EnabledPropertyName);
            SerializedProperty optionsProperty =
                serialized.FindProperty(OptionsPropertyName);
            if (enabledProperty == null || optionsProperty == null)
            {
                throw new InvalidOperationException(
                    "Unity's EditorSettings serialization contract does " +
                    "not expose the Enter Play Mode option fields.");
            }

            enabledProperty.boolValue = enabled;
            optionsProperty.intValue = (int)options;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            // ProjectSettings objects are not normal AssetDatabase assets.
            // Mark and save the authoritative EditorSettings owner so a CLI
            // success survives an Editor restart, then verify the serialized
            // file independently below.
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
        }

        private static UnityEngine.Object GetLiveEditorSettings()
        {
            MethodInfo getter = typeof(EditorSettings).GetMethod(
                "GetEditorSettings",
                BindingFlags.Static | BindingFlags.NonPublic);
            if (getter == null ||
                !(getter.Invoke(null, null) is UnityEngine.Object settings))
            {
                throw new InvalidOperationException(
                    "Unity's authoritative EditorSettings owner is unavailable.");
            }

            return settings;
        }

        private static PersistedState CapturePersistedState()
        {
            string projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
            if (string.IsNullOrEmpty(projectRoot))
            {
                throw new InvalidOperationException(
                    "Could not resolve the Unity project root.");
            }

            string path = Path.Combine(projectRoot, EditorSettingsRelativePath);
            UnityEngine.Object[] loaded =
                InternalEditorUtility.LoadSerializedFileAndForget(path) ??
                Array.Empty<UnityEngine.Object>();
            try
            {
                foreach (UnityEngine.Object item in loaded)
                {
                    if (!(item is EditorSettings))
                    {
                        continue;
                    }

                    var serialized = new SerializedObject(item);
                    serialized.Update();
                    SerializedProperty enabledProperty =
                        serialized.FindProperty(EnabledPropertyName);
                    SerializedProperty optionsProperty =
                        serialized.FindProperty(OptionsPropertyName);
                    if (enabledProperty == null || optionsProperty == null)
                    {
                        break;
                    }

                    return new PersistedState(
                        enabledProperty.boolValue,
                        optionsProperty.intValue);
                }
            }
            finally
            {
                foreach (UnityEngine.Object item in loaded)
                {
                    if (item != null)
                    {
                        UnityEngine.Object.DestroyImmediate(item);
                    }
                }
            }

            throw new InvalidOperationException(
                "Could not read back Enter Play Mode options from " +
                EditorSettingsRelativePath + ".");
        }

        private static Dictionary<string, object> CaptureState()
        {
            bool enabled = EditorSettings.enterPlayModeOptionsEnabled;
            EnterPlayModeOptions options =
                EditorSettings.enterPlayModeOptions;
            bool disableDomainReload =
                HasFlag(options,
                    EnterPlayModeOptions.DisableDomainReload);
            bool disableSceneReload =
                HasFlag(options,
                    EnterPlayModeOptions.DisableSceneReload);
            return new Dictionary<string, object>
            {
                { "enabled", enabled },
                { "optionsValue", (int)options },
                { "disableDomainReload", disableDomainReload },
                { "disableSceneReload", disableSceneReload },
                {
                    "domainReloadEnabled",
                    enabled == false || disableDomainReload == false
                },
                {
                    "sceneReloadEnabled",
                    enabled == false || disableSceneReload == false
                },
            };
        }

        private static EnterPlayModeOptions SetFlag(
            EnterPlayModeOptions options,
            EnterPlayModeOptions flag,
            bool value)
        {
            return value
                ? options | flag
                : options & ~flag;
        }

        private static bool HasFlag(
            EnterPlayModeOptions options,
            EnterPlayModeOptions flag)
        {
            return (options & flag) == flag;
        }

        private static bool StatesEqual(
            IReadOnlyDictionary<string, object> left,
            IReadOnlyDictionary<string, object> right)
        {
            return (bool)left["enabled"] == (bool)right["enabled"] &&
                   (int)left["optionsValue"] ==
                   (int)right["optionsValue"];
        }

        private static bool TryGetOptionalBool(
            IReadOnlyDictionary<string, object> arguments,
            string key,
            out bool value)
        {
            if (arguments.TryGetValue(key, out object rawValue) == false ||
                rawValue == null)
            {
                value = false;
                return false;
            }

            if (rawValue is bool boolValue)
            {
                value = boolValue;
                return true;
            }

            throw new ArgumentException($"'{key}' must be a boolean.");
        }

        private readonly struct PersistedState
        {
            internal bool Enabled { get; }
            internal int OptionsValue { get; }

            internal PersistedState(bool enabled, int optionsValue)
            {
                Enabled = enabled;
                OptionsValue = optionsValue;
            }
        }
    }
}
