using System.ComponentModel;

namespace VMUnityAutomation.Editor
{
    public sealed class VmImageResizeRequest
    {
        [VmRequired, VmMinLength(1), VmJsonProperty("sourcePath")]
        [Description("Absolute path to the source 8-bit PNG. The source is retained.")]
        public string SourcePath { get; set; }

        [VmRequired, VmMinLength(1), VmJsonProperty("outputPath")]
        [Description("Absolute output PNG path in an existing directory outside this project's Assets and Packages.")]
        public string OutputPath { get; set; }

        [VmRange(1, 4096), VmJsonProperty("width")]
        [Description("Target width, or maximum width when height is also supplied. Aspect ratio is preserved.")]
        public int? Width { get; set; }

        [VmRange(1, 4096), VmJsonProperty("height")]
        [Description("Target height, or maximum height when width is also supplied. Supply at least one dimension.")]
        public int? Height { get; set; }

        [VmJsonProperty("filter")]
        [Description("Bilinear (default) interpolates premultiplied alpha. Nearest preserves pixel-art samples.")]
        public VmImageResizeFilter Filter { get; set; } = VmImageResizeFilter.Bilinear;

        [VmJsonProperty("overwrite")]
        [Description("Allow replacing the explicit output file. Defaults to false. Never allows overwriting the source.")]
        public bool Overwrite { get; set; }

        [VmJsonProperty("dryRun")]
        [Description("Validate paths, header, dimensions and limits without decoding or writing. Defaults to false.")]
        public bool DryRun { get; set; }
    }
}
