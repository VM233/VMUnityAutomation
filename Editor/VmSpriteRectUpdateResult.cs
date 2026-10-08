namespace VMUnityAutomation.Editor
{
    public sealed class VmSpriteRectUpdateResult
    {
        [VmRequired, VmJsonProperty("texturePath")] public string TexturePath { get; set; }
        [VmRequired, VmJsonProperty("textureGuid")] public string TextureGuid { get; set; }
        [VmRequired, VmJsonProperty("sourceWidth")] public int SourceWidth { get; set; }
        [VmRequired, VmJsonProperty("sourceHeight")] public int SourceHeight { get; set; }
        [VmRequired, VmJsonProperty("pixelsPerUnit")] public float PixelsPerUnit { get; set; }
        [VmRequired, VmJsonProperty("spriteMeshType")] public int SpriteMeshType { get; set; }
        [VmRequired, VmJsonProperty("updatedCount")] public int UpdatedCount { get; set; }
        [VmRequired, VmJsonProperty("sprites")] public VmSpriteRectIdentity[] Sprites { get; set; }
    }

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
