using System.ComponentModel;

namespace VMUnityAutomation.Editor
{
    public sealed class VmUiRebuildRequest
    {
        [VmRequired, VmJsonProperty("rootInstanceId")]
        [Description("Exact existing loaded UI GameObject instanceId from scene/hierarchy. Rebuilds its current subtree without creating objects or changing serialized properties.")]
        public string RootInstanceId { get; set; }
    }
}
