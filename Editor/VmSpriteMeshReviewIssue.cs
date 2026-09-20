namespace VMUnityAutomation.Editor
{
    public sealed class VmSpriteMeshReviewIssue
    {
        [VmRequired, VmJsonProperty("code")]
        public string Code { get; private set; }

        [VmRequired, VmJsonProperty("assetPath")]
        public string AssetPath { get; private set; }

        [VmRequired, VmJsonProperty("actualMeshType")]
        public string ActualMeshType { get; private set; }

        [VmRequired, VmJsonProperty("expectedMeshType")]
        public string ExpectedMeshType { get; private set; }

        internal VmSpriteMeshReviewIssue(string assetPath, string actualMeshType)
        {
            Code = "sprite_mesh_not_full_rect";
            AssetPath = assetPath;
            ActualMeshType = actualMeshType;
            ExpectedMeshType = "FullRect";
        }
    }
}
