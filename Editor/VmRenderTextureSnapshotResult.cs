using System.ComponentModel;

namespace VMUnityAutomation.Editor
{
    public sealed class VmRenderTextureSnapshotResult
    {
        public string RenderTextureInstanceId { get; set; }
        public string OutputPath { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public string GraphicsFormat { get; set; }
        public bool Srgb { get; set; }
        public int MsaaSamples { get; set; }
        public int Frame { get; set; }
        public int NonzeroAlphaPixels { get; set; }
        public int OpaquePixels { get; set; }
        [Description("Empty for fully transparent output; otherwise integer x,y,width,height in bottom-left PNG pixel coordinates.")]
        public int[] AlphaBoundsBottomLeft { get; set; }
        public int PngBytes { get; set; }
        public string Sha256 { get; set; }
    }
}
