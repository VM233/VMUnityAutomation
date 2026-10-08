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

    public sealed class VmSpriteRectUpdate
    {
        [VmRequired, VmMinLength(1), VmJsonProperty("name")]
        public string Name { get; set; }
        [VmRequired, VmJsonProperty("x")] public float X { get; set; }
        [VmRequired, VmJsonProperty("y")] public float Y { get; set; }
        [VmRequired, VmJsonProperty("width")] public float Width { get; set; }
        [VmRequired, VmJsonProperty("height")] public float Height { get; set; }
        [VmRequired, VmRange(0, 1), VmJsonProperty("pivotX")] public float PivotX { get; set; }
        [VmRequired, VmRange(0, 1), VmJsonProperty("pivotY")] public float PivotY { get; set; }
    }
}
