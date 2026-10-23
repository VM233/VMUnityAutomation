using System.ComponentModel;

namespace VMUnityAutomation.Editor
{
    public sealed class VmEditorWindowFocusRequest
    {
        [VmRequired, VmJsonProperty("windowInstanceId")]
        [Description("Exact existing EditorWindow instanceId returned by uitoolkit/windows. No window is created.")]
        public string WindowInstanceId { get; set; }
    }
}
