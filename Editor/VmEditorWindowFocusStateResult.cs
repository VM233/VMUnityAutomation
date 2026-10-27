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
    }
}
