namespace VMUnityAutomation.Editor
{
    public sealed class VmImageResizeResult
    {
        [VmRequired, VmJsonProperty("sourcePath")]
        public string SourcePath { get; }
        [VmRequired, VmJsonProperty("outputPath")]
        public string OutputPath { get; }
        [VmRequired, VmJsonProperty("sourceWidth")]
        public int SourceWidth { get; }
        [VmRequired, VmJsonProperty("sourceHeight")]
        public int SourceHeight { get; }
        [VmRequired, VmJsonProperty("width")]
        public int Width { get; }
        [VmRequired, VmJsonProperty("height")]
        public int Height { get; }
        [VmRequired, VmJsonProperty("filter")]
        public VmImageResizeFilter Filter { get; }
        [VmRequired, VmJsonProperty("sourceSha256")]
        public string SourceSha256 { get; }
        [VmRequired, VmJsonProperty("outputSha256")]
        public string OutputSha256 { get; }
        [VmRequired, VmJsonProperty("outputBytes")]
        public int OutputBytes { get; }
        [VmRequired, VmJsonProperty("dryRun")]
        public bool DryRun { get; }
        [VmRequired, VmJsonProperty("verified")]
        public bool Verified { get; }

        internal VmImageResizeResult(string sourcePath, string outputPath,
            int sourceWidth, int sourceHeight, int width, int height,
            VmImageResizeFilter filter, string sourceSha256, string outputSha256,
            int outputBytes, bool dryRun)
        {
            SourcePath = sourcePath;
            OutputPath = outputPath;
            SourceWidth = sourceWidth;
            SourceHeight = sourceHeight;
            Width = width;
            Height = height;
            Filter = filter;
            SourceSha256 = sourceSha256;
            OutputSha256 = outputSha256;
            OutputBytes = outputBytes;
            DryRun = dryRun;
            Verified = !dryRun;
        }
    }
}
