using System.ComponentModel;

namespace VMUnityAutomation.Editor
{
    public sealed class VmTextCoreFontAssetRebuildRequest
    {
        [VmJsonProperty("fontAssetPath")]
        [VmRequired]
        [Description("Assets-relative path to a dynamic TextCore FontAsset with one embedded primary Alpha8 atlas, a material and up to fifteen overflow slots, including unused null slots. Rebuilds from its current imported source font.")]
        public string FontAssetPath { get; set; }
    }
}
