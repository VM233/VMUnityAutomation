namespace VMUnityAutomation.Editor
{
    public sealed class VmAssetReimportResult
    {
        [VmJsonProperty("assetPath")]
        public string AssetPath { get; set; }

        [VmJsonProperty("guidBefore")]
        public string GuidBefore { get; set; }

        [VmJsonProperty("guidAfter")]
        public string GuidAfter { get; set; }

        [VmJsonProperty("assetType")]
        public string AssetType { get; set; }

        [VmJsonProperty("forceUpdate")]
        public bool ForceUpdate { get; set; }

        [VmJsonProperty("imported")]
        public bool Imported { get; set; }
    }
}
