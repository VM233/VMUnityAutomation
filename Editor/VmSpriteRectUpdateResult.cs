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
}
