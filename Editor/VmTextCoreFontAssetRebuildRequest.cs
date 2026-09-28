using System.ComponentModel;

namespace VMUnityAutomation.Editor
{
    public sealed class VmTextCoreFontAssetRebuildRequest
    {
        [VmJsonProperty("fontAssetPath")]
        [VmRequired]
        [Description("Assets-relative path to an existing dynamic TextCore FontAsset with one embedded Alpha8 atlas and material. Rebuilds from its current imported source font.")]
        public string FontAssetPath { get; set; }
    }
}
