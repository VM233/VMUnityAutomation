namespace VMUnityAutomation.Editor
{
    public sealed class VmImageResizeResult
    {
        [VmRequired, VmJsonProperty("sourcePath")]
        public string SourcePath { get; private set; }
        [VmRequired, VmJsonProperty("outputPath")]
        public string OutputPath { get; private set; }
        [VmRequired, VmJsonProperty("sourceWidth")]
        public int SourceWidth { get; private set; }
        [VmRequired, VmJsonProperty("sourceHeight")]
        public int SourceHeight { get; private set; }
        [VmRequired, VmJsonProperty("width")]
        public int Width { get; private set; }
        [VmRequired, VmJsonProperty("height")]
        public int Height { get; private set; }
        [VmRequired, VmJsonProperty("filter")]
        public VmImageResizeFilter Filter { get; private set; }
        [VmRequired, VmJsonProperty("sourceSha256")]
        public string SourceSha256 { get; private set; }
        [VmRequired, VmJsonProperty("outputSha256")]
        public string OutputSha256 { get; private set; }
        [VmRequired, VmJsonProperty("outputBytes")]
        public int OutputBytes { get; private set; }
        [VmRequired, VmJsonProperty("dryRun")]
        public bool DryRun { get; private set; }
        [VmRequired, VmJsonProperty("verified")]
        public bool Verified { get; private set; }

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
