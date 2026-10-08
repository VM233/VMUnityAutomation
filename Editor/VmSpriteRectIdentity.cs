namespace VMUnityAutomation.Editor
{
    public sealed class VmSpriteRectIdentity
    {
        [VmRequired, VmJsonProperty("name")] public string Name { get; set; }
        [VmRequired, VmJsonProperty("spriteId")] public string SpriteId { get; set; }
        [VmRequired, VmJsonProperty("localFileId")] public string LocalFileId { get; set; }
        [VmRequired, VmJsonProperty("x")] public float X { get; set; }
        [VmRequired, VmJsonProperty("y")] public float Y { get; set; }
        [VmRequired, VmJsonProperty("width")] public float Width { get; set; }
        [VmRequired, VmJsonProperty("height")] public float Height { get; set; }
        [VmRequired, VmJsonProperty("pivotX")] public float PivotX { get; set; }
        [VmRequired, VmJsonProperty("pivotY")] public float PivotY { get; set; }
    }
}
