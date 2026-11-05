using System.ComponentModel;

namespace VMUnityAutomation.Editor
{
    public sealed class VmEditorPointerRequest
    {
        [VmRequired, VmMinLength(1), VmJsonProperty("windowInstanceId")]
        [Description("Exact existing EditorWindow identity from uitoolkit/windows.")]
        public string WindowInstanceId { get; set; }

        [VmRequired, VmJsonProperty("phase")]
        public VmEditorPointerPhase Phase { get; set; }

        [VmRequired, VmJsonProperty("x")]
        [Description("Native panel X coordinate from uitoolkit/query world bounds.")]
        public float X { get; set; }

        [VmRequired, VmJsonProperty("y")]
        [Description("Native panel Y coordinate from uitoolkit/query world bounds.")]
        public float Y { get; set; }
    }
}
