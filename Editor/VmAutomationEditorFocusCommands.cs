using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using UnityEditor;

namespace VMUnityAutomation.Editor
{
    internal static class VmAutomationEditorFocusCommands
    {
        internal static object Clear()
        {
#if UNITY_EDITOR_WIN
            EditorWindow previous = EditorWindow.focusedWindow;
            IntPtr nativeBefore = GetFocus();
            SetFocus(IntPtr.Zero);
            IntPtr nativeAfter = GetFocus();
            EditorWindow current = EditorWindow.focusedWindow;
            bool confirmed = nativeAfter == IntPtr.Zero && current == null;
            var result = new Dictionary<string, object>
            {
                { "success", confirmed },
                { "stateConfirmed", confirmed },
                { "previousWindowInstanceId", previous == null ? "" : VmObjectId.Get(previous) },
                { "currentWindowInstanceId", current == null ? "" : VmObjectId.Get(current) },
                { "previousNativeKeyboardWindow", nativeBefore.ToInt64().ToString(CultureInfo.InvariantCulture) },
                { "currentNativeKeyboardWindow", nativeAfter.ToInt64().ToString(CultureInfo.InvariantCulture) },
            };
            if (!confirmed)
            {
                result.Add("errorCode", "focus_clear_unconfirmed");
                result.Add("error", "Native keyboard focus or Unity EditorWindow focus remains after SetFocus(NULL).");
            }
            return result;
#else
            return VmAutomationResponse.Error("Clearing native Editor focus is supported only on Windows.",
                "platform_not_supported");
#endif
        }

#if UNITY_EDITOR_WIN
        [DllImport("user32.dll")]
        private static extern IntPtr GetFocus();

        [DllImport("user32.dll")]
        private static extern IntPtr SetFocus(IntPtr window);
#endif
    }
}
