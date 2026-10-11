using System.ComponentModel;

namespace VMUnityAutomation.Editor
{
    public sealed class VmAssetReimportRequest
    {
        [VmRequired, VmMinLength(1), VmJsonProperty("assetPath")]
        [Description("One existing non-compilation asset or its .meta path below Assets/. Folders are not admitted.")]
        public string AssetPath { get; set; }

        [VmJsonProperty("forceUpdate")]
        [Description("Force the selected native importer to process its current file. Defaults to true.")]
        public bool ForceUpdate { get; set; } = true;
    }
}
