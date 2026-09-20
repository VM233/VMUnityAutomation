namespace VMUnityAutomation.Editor
{
    public sealed class VmSpriteMeshReviewResult
    {
        [VmRequired, VmJsonProperty("passed")]
        public bool Passed { get; private set; }

        [VmRequired, VmJsonProperty("assetRoots")]
        public string[] AssetRoots { get; private set; }

        [VmRequired, VmJsonProperty("candidateTextureCount")]
        public int CandidateTextureCount { get; private set; }

        [VmRequired, VmJsonProperty("spriteCount")]
        public int SpriteCount { get; private set; }

        [VmRequired, VmJsonProperty("fullRectCount")]
        public int FullRectCount { get; private set; }

        [VmRequired, VmJsonProperty("tightCount")]
        public int TightCount { get; private set; }

        [VmRequired, VmJsonProperty("totalIssues")]
        public int TotalIssues { get; private set; }

        [VmRequired, VmJsonProperty("returnedIssues")]
        public int ReturnedIssues { get; private set; }

        [VmRequired, VmJsonProperty("truncated")]
        public bool Truncated { get; private set; }

        [VmRequired, VmJsonProperty("issues")]
        public VmSpriteMeshReviewIssue[] Issues { get; private set; }

        internal VmSpriteMeshReviewResult(string[] assetRoots,
            int candidateTextureCount, int spriteCount, int fullRectCount,
            int tightCount, int totalIssues, VmSpriteMeshReviewIssue[] issues)
        {
            Passed = totalIssues == 0;
            AssetRoots = assetRoots;
            CandidateTextureCount = candidateTextureCount;
            SpriteCount = spriteCount;
            FullRectCount = fullRectCount;
            TightCount = tightCount;
            TotalIssues = totalIssues;
            ReturnedIssues = issues.Length;
            Truncated = totalIssues > issues.Length;
            Issues = issues;
        }
    }
}
