using System.ComponentModel;

namespace VMUnityAutomation.Editor
{
    public sealed class VmGameViewRemovePresetsResult
    {
        public int RemovedCount { get; set; }
        public bool GlobalPreferencesSaved { get; set; }
        public VmGameViewPresetInventory Inventory { get; set; }
    }
}
