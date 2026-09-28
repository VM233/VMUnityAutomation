namespace VMUnityAutomation.Editor
{
    public sealed class VmTextCoreFontAssetRebuildReport
    {
        [VmJsonProperty("fontAssetPath")]
        public string FontAssetPath { get; set; }

        [VmJsonProperty("fontAssetGuid")]
        public string FontAssetGuid { get; set; }

        [VmJsonProperty("sourceFontPath")]
        public string SourceFontPath { get; set; }

        [VmJsonProperty("sourceFontGuid")]
        public string SourceFontGuid { get; set; }

        [VmJsonProperty("familyName")]
        public string FamilyName { get; set; }

        [VmJsonProperty("styleName")]
        public string StyleName { get; set; }

        [VmJsonProperty("pointSize")]
        public float PointSize { get; set; }

        [VmJsonProperty("atlasLocalId")]
        public long AtlasLocalId { get; set; }

        [VmJsonProperty("materialLocalId")]
        public long MaterialLocalId { get; set; }

        [VmJsonProperty("glyphCount")]
        public int GlyphCount { get; set; }

        [VmJsonProperty("characterCount")]
        public int CharacterCount { get; set; }
    }
}
