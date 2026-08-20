using System;
using System.Collections.Generic;
using UnityEditor;

namespace VMUnityAutomation.Editor
{
    internal static class VmAutomationPlayModeOptionsCommands
    {
        private const string EditModeRequiredErrorCode =
            "edit_mode_required";
        private const string UpdateFailedErrorCode =
            "play_mode_options_update_failed";

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

            EditorSettings.enterPlayModeOptionsEnabled = nextEnabled;
            EditorSettings.enterPlayModeOptions = nextOptions;

            Dictionary<string, object> current = CaptureState();
            if ((bool)current["enabled"] != nextEnabled ||
                (int)current["optionsValue"] != (int)nextOptions)
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
                    });
            }

            return new Dictionary<string, object>
            {
                { "changed", !StatesEqual(previous, current) },
                { "previous", previous },
                { "current", current },
            };
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
    }
}
