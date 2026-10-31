using System.ComponentModel;

namespace VMUnityAutomation.Editor
{
    public sealed class VmAudioMixerCreateRequest
    {
        [VmRequired, VmMinLength(1), VmJsonProperty("assetPath")]
        [Description("New .mixer asset below an existing Assets folder. Existing files are rejected.")]
        public string AssetPath { get; set; }
    }
}
