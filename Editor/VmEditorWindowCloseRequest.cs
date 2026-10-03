using System.ComponentModel;

namespace VMUnityAutomation.Editor
{
    public sealed class VmEditorWindowCloseRequest
    {
        [VmRequired, VmJsonProperty("windowInstanceId")]
        [Description("Existing EditorWindow instanceId from uitoolkit/windows.")]
        public string WindowInstanceId { get; set; }
    }
}
