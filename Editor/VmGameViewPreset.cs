using System.ComponentModel;

namespace VMUnityAutomation.Editor
{
    public sealed class VmGameViewPreset
    {
        [VmRequired]
        public int Index { get; set; }
        [VmRequired]
        public bool Builtin { get; set; }
        [VmRequired]
        public string BaseText { get; set; }
        [VmRequired]
        public string SizeType { get; set; }
        [VmRequired]
        public int Width { get; set; }
        [VmRequired]
        public int Height { get; set; }
    }
}
