using System.ComponentModel;

namespace VMUnityAutomation.Editor
{
    public sealed class VmEditorWindowFocusStateResult
    {
        [Description("Exact current native EditorWindow identity. Empty when Unity has no focused EditorWindow.")]
        public string WindowInstanceId { get; set; }

        [Description("Actual focused EditorWindow runtime type. Empty when there is no native focused window.")]
        public string WindowType { get; set; }

        [Description("Actual focused EditorWindow title. Empty when there is no native focused window.")]
        public string WindowTitle { get; set; }

        [Description("Exact native GUIView focus identity, independently of its optional EditorWindow. Empty when the native focused view is null.")]
        public string NativeViewInstanceId { get; set; }

        public string NativeViewType { get; set; }

        [Description("Whether the current platform supports the native main-thread keyboard focus observation.")]
        public bool SystemFocusAvailable { get; set; }

        [Description("Windows main-thread keyboard recipient as an exact decimal handle, zero when none. Empty on unsupported platforms.")]
        public string NativeKeyboardFocusHandle { get; set; }
    }
}
