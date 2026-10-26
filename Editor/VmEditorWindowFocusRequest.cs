using System.ComponentModel;

namespace VMUnityAutomation.Editor
{
    public sealed class VmEditorWindowFocusRequest
    {
        [VmRequired, VmJsonProperty("windowInstanceId")]
        [Description("Exact existing EditorWindow instanceId returned by uitoolkit/windows. No window is created.")]
        public string WindowInstanceId { get; set; }

        [VmJsonProperty("maximized")]
        [Description("Optional native EditorWindow maximized state. True maximizes the existing docked view, false restores it; omission preserves the current state. Game View resolution presets are unchanged.")]
        public bool? Maximized { get; set; }
    }
}
