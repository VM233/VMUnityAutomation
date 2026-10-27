using System.ComponentModel;

namespace VMUnityAutomation.Editor
{
    public sealed class VmNineSliceReviewRequest
    {
        [VmRequired, VmMinItems(1), VmJsonProperty("texturePaths")]
        [Description("1..256 explicit PNG Sprite asset paths under Assets/. Review does not modify pixels, importers or consumers.")]
        public string[] TexturePaths { get; set; }
    }
}
