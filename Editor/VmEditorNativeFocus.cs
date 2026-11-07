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

#if UNITY_EDITOR_WIN
        [DllImport("user32.dll")]
        private static extern System.IntPtr GetFocus();
#endif
    }
}
