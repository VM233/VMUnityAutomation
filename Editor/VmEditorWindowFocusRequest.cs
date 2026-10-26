using System.ComponentModel;

namespace VMUnityAutomation.Editor
{
    public sealed class VmEditorWindowFocusRequest
    {
        [VmRequired, VmJsonProperty("windowInstanceId")]
        [Description("Exact existing EditorWindow instanceId returned by uitoolkit/windows. No window is created.")]
        public string WindowInstanceId { get; set; }

        [VmJsonProperty("maximized")]
        [Description("Optional native maximized state. Sets the existing docked EditorWindow or floating view's own native ContainerWindow, and verifies readback. False restores normal state; omission preserves the current state. Game View resolution presets are unchanged.")]
        public bool? Maximized { get; set; }
    }
}
