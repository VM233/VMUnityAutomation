using System.ComponentModel;

namespace VMUnityAutomation.Editor
{
    public sealed class VmGameViewPresetInventory
    {
        public string Group { get; set; }
        public string Fingerprint { get; set; }
        public int BuiltinCount { get; set; }
        public VmGameViewPreset[] Presets { get; set; }
    }
}
