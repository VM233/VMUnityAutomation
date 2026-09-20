using System.ComponentModel;

namespace VMUnityAutomation.Editor
{
    public sealed class VmSpriteMeshReviewRequest
    {
        [VmJsonProperty("assetRoots")]
        [Description("Project-relative folders below Assets. Defaults to the complete Assets tree.")]
        public string[] AssetRoots { get; set; }

        [VmRange(1, 5000), VmJsonProperty("maxIssues")]
        [Description("Maximum issue records returned. Aggregate counts always cover the complete scan.")]
        public int MaxIssues { get; set; } = 200;
    }
}
