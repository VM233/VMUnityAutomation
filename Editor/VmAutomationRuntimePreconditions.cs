using System.Collections.Generic;
using UnityEditor;

namespace VMUnityAutomation.Editor
{
    internal static class VmAutomationRuntimePreconditions
    {
        internal const string PlayModeRequiredErrorCode = "requires_play_mode";

        internal static bool IsStablePlayMode => IsStablePlayModeState(
            EditorApplication.isPlaying,
            EditorApplication.isPlayingOrWillChangePlaymode);

        internal static bool IsStablePlayModeState(bool isPlaying,
            bool isPlayingOrWillChangePlaymode)
        {
            return isPlaying && isPlayingOrWillChangePlaymode == isPlaying;
        }

        internal static Dictionary<string, object> CreatePlayModeStateDetails()
        {
            return new Dictionary<string, object>
            {
                { "requiresPlayMode", true },
                { "isPlaying", EditorApplication.isPlaying },
                { "isPlayingOrWillChangePlaymode", EditorApplication.isPlayingOrWillChangePlaymode },
                { "isPaused", EditorApplication.isPaused },
            };
        }

        public static bool TryRequirePlayMode(string route, string purpose,
            out Dictionary<string, object> error)
        {
            if (IsStablePlayMode)
            {
                error = null;
                return true;
            }

            Dictionary<string, object> details = CreatePlayModeStateDetails();
            details["route"] = route;
            error = VmAutomationResponse.Error(
                $"{route} requires stable Play Mode because {purpose}.",
                PlayModeRequiredErrorCode,
                false,
                details);
            return false;
        }
    }
}
