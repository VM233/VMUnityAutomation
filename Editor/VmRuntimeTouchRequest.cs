using System.ComponentModel;

namespace VMUnityAutomation.Editor
{
    public sealed class VmRuntimeTouchRequest
    {
        [VmRequired, VmJsonProperty("documentInstanceId")]
        [Description("Exact attached runtime UIDocument identity from uitoolkit/runtime-documents.")]
        public string DocumentInstanceId { get; set; }

        [VmRequired, VmJsonProperty("samples")]
        [Description("One or two native touch samples in this frame; distinct fingers in slots 0 through 9.")]
        public VmRuntimeTouchSample[] Samples { get; set; }
    }

}
