using System.ComponentModel;

namespace VMUnityAutomation.Editor
{
    public sealed class VmGameViewRemovePresetsRequest
    {
        [VmRequired, VmMinLength(1), VmJsonProperty("expectedFingerprint")]
        public string ExpectedFingerprint { get; set; }

        [VmRequired, VmMinItems(1), VmJsonProperty("presets")]
        [Description("At most 128 exact custom records from gameview/presets. Builtin, selected, duplicate or stale records reject before mutation.")]
        public VmGameViewPreset[] Presets { get; set; }
    }
}
