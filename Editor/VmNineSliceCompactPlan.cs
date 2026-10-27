namespace VMUnityAutomation.Editor
{
    public sealed class VmNineSliceCompactPlan
    {
        [VmRequired, VmJsonProperty("texturePath")] public string TexturePath { get; set; }
        [VmRequired, VmJsonProperty("sourceSha256")] public string SourceSha256 { get; set; }
        [VmRequired, VmJsonProperty("eligible")] public bool Eligible { get; set; }
        [VmRequired, VmJsonProperty("reason")] public string Reason { get; set; }
        [VmRequired, VmJsonProperty("sourceWidth")] public int SourceWidth { get; set; }
        [VmRequired, VmJsonProperty("sourceHeight")] public int SourceHeight { get; set; }
        [VmRequired, VmJsonProperty("compactWidth")] public int CompactWidth { get; set; }
        [VmRequired, VmJsonProperty("compactHeight")] public int CompactHeight { get; set; }
        [VmRequired, VmJsonProperty("removedPixels")] public int RemovedPixels { get; set; }
        [VmRequired, VmJsonProperty("border")] public int[] Border { get; set; }
        [VmRequired, VmJsonProperty("originalBorder")] public float[] OriginalBorder { get; set; }
        [VmRequired, VmJsonProperty("reconstructionVerified")] public bool ReconstructionVerified { get; set; }
        [VmRequired, VmJsonProperty("applied")] public bool Applied { get; set; }
        [VmRequired, VmJsonProperty("textureGuid")] public string TextureGuid { get; set; }
        [VmRequired, VmJsonProperty("localFileId")] public string LocalFileId { get; set; }
        [VmRequired, VmJsonProperty("spriteId")] public string SpriteId { get; set; }
    }
}
