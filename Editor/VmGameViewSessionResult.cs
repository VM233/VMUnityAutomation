using System.ComponentModel;

namespace VMUnityAutomation.Editor
{
    public sealed class VmGameViewSessionResult
    {
        public string SessionId { get; set; }
        public bool Active { get; set; }
        public bool TemporaryPresetOwned { get; set; }
        public VmGameViewState View { get; set; }
        public VmGameViewPresetInventory Inventory { get; set; }
    }
}
