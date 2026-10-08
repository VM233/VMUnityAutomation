using System.ComponentModel;

namespace VMUnityAutomation.Editor
{
    public sealed class VmSpriteRectUpdateRequest
    {
        [VmRequired, VmMinLength(1), VmJsonProperty("texturePath")]
        [Description("Existing Multiple Sprite texture below Assets/.")]
        public string TexturePath { get; set; }

        [VmRequired, VmMinItems(1), VmJsonProperty("rects")]
        [Description("At most 256 named existing Sprite updates. Source-pixel rectangles use a bottom-left origin.")]
        public VmSpriteRectUpdate[] Rects { get; set; }
    }
}
