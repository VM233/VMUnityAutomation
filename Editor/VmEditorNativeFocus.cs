using System;
using System.Reflection;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEngine;

namespace VMUnityAutomation.Editor
{
    internal static class VmEditorNativeFocus
    {
        internal static VmEditorWindowFocusStateResult Read()
        {
            var window = EditorWindow.focusedWindow;
            var viewType = typeof(EditorWindow).Assembly.GetType("UnityEditor.GUIView", true);
            var property = viewType.GetProperty("focusedView",
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);
            if (property == null)
                throw new VmProjectToolException("editor_window_focus_unavailable", "Unity has no native GUIView focus observation.");
            var view = (ScriptableObject)property.GetValue(null, null);
            var result = new VmEditorWindowFocusStateResult
            {
                WindowInstanceId = window == null ? "" : VmObjectId.Get(window),
                WindowType = window == null ? "" : window.GetType().FullName,
                WindowTitle = window == null ? "" : window.titleContent.text,
                NativeViewInstanceId = view == null ? "" : VmObjectId.Get(view),
                NativeViewType = view == null ? "" : view.GetType().FullName,
                NativeKeyboardFocusHandle = "",
            };
#if UNITY_EDITOR_WIN
            result.SystemFocusAvailable = true;
            result.NativeKeyboardFocusHandle = GetFocus().ToInt64().ToString(System.Globalization.CultureInfo.InvariantCulture);
#endif
            return result;
        }

        internal static void Clear()
        {
#if UNITY_EDITOR_WIN
            var handle = GetFocus();
            if (handle != IntPtr.Zero)
            {
                uint thread = GetWindowThreadProcessId(handle, out uint process);
                if (thread != GetCurrentThreadId() || process != GetCurrentProcessId())
                    throw new VmProjectToolException("editor_window_focus_ownership_mismatch", "The native focus recipient does not belong to this Editor main thread.");
            }
            SetFocus(IntPtr.Zero);
#else
            throw new VmProjectToolException("capability_unavailable", "Native null window-focus restoration is supported on Windows Editors only.");
#endif
        }

#if UNITY_EDITOR_WIN
        [DllImport("user32.dll")]
        private static extern IntPtr GetFocus();

        [DllImport("user32.dll")]
        private static extern IntPtr SetFocus(IntPtr window);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr window, out uint process);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentProcessId();
#endif
    }
}
