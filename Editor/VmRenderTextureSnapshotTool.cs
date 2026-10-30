using System;
using System.ComponentModel;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace VMUnityAutomation.Editor
{
    [VmProjectTool("texture/snapshot-render-texture",
        Description = "Snapshot one exact created live 2D RGBA/BGRA8 RenderTexture, including explicit MSAA resolve. Maximum1024x1024. Read existing pixels without rendering a camera; preserve RGBA bytes and report alpha coverage/bounds, format, frame and PNG SHA256. Create-new PNG only under existing project Temp/Tools directories; no reparse ancestors or overwrite. Synchronous bounded GPU readback; not a per-frame profiler or visual certification.",
        MutatesProjectFiles = true,
        SideEffects = VmProjectToolSideEffect.ReadsProjectState | VmProjectToolSideEffect.WritesProjectFiles |
                      VmProjectToolSideEffect.CreatesTemporaryObjects | VmProjectToolSideEffect.CapturesArtifacts,
        ErrorCodes = new[] { "render_snapshot_source", "render_snapshot_limit", "render_snapshot_format", "render_snapshot_path", "render_snapshot_io" },
        Preconditions = new[] { "stable editor", "exact created native RenderTexture", "existing output parent" },
        CompletionEvidence = "Create-new PNG, exact native source metadata and alpha coverage with SHA256; prior active target restored and temporary objects released.",
        TransactionScope = "single-output-file", TransactionAtomicity = VmTransactionMechanics.Atomicity.VerifiedFileRollback,
        TransactionIsolation = VmTransactionMechanics.Isolation.RequestSerialized,
        TransactionDurability = VmTransactionMechanics.Durability.EditorSession,
        TransactionRollbackKind = VmTransactionMechanics.RollbackKind.NewAssetDelete,
        TransactionCommitEvidence = new[] { "created-png", "png-sha256", "native-source-metadata" })]
    public sealed class VmRenderTextureSnapshotTool : IVmProjectTool<VmRenderTextureSnapshotRequest, VmRenderTextureSnapshotResult>
    {
        public VmRenderTextureSnapshotResult Execute(VmRenderTextureSnapshotRequest request)
        {
            var source = VmObjectId.ToObject(request.RenderTextureInstanceId) as RenderTexture;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating ||
                EditorApplication.isPlaying != EditorApplication.isPlayingOrWillChangePlaymode ||
                source == null || !source.IsCreated())
                throw Failure("source", "An exact created RenderTexture in stable Editor state is required.");
            if (source.width < 1 || source.height < 1 || source.width > 1024 || source.height > 1024)
                throw Failure("limit", "The source must be at most1024x1024.");
            if (source.dimension != TextureDimension.Tex2D || source.volumeDepth != 1 || !Supported(source.graphicsFormat))
                throw Failure("format", "Only one 2D RGBA/BGRA8 UNorm or sRGB color target is supported.");
            string path = OutputPath(request.OutputPath);
            RenderTexture previous = RenderTexture.active;
            RenderTexture resolved = null;
            Texture2D pixels = null;
            bool ownsFile = false;
            bool complete = false;
            try
            {
                var descriptor = source.descriptor;
                descriptor.depthBufferBits = 0;
                descriptor.msaaSamples = 1;
                descriptor.bindMS = false;
                descriptor.useMipMap = false;
                descriptor.autoGenerateMips = false;
                descriptor.enableRandomWrite = false;
                resolved = RenderTexture.GetTemporary(descriptor);
                // Native resolve/copy retains the source format and alpha, with no shader conversion.
                if (source.antiAliasing > 1) source.ResolveAntiAliasedSurface(resolved);
                else Graphics.CopyTexture(source, resolved);
                RenderTexture.active = resolved;
                pixels = new Texture2D(source.width, source.height, TextureFormat.RGBA32, false, !source.sRGB);
                pixels.ReadPixels(new Rect(0, 0, source.width, source.height), 0, 0, false);
                pixels.Apply(false, false);
                var colors = pixels.GetPixels32();
                int visible = 0, opaque = 0, minX = source.width, minY = source.height, maxX = -1, maxY = -1;
                for (int index = 0; index < colors.Length; index++)
                {
                    byte alpha = colors[index].a;
                    if (alpha == 255) opaque++;
                    if (alpha == 0) continue;
                    visible++;
                    int x = index % source.width, y = index / source.width;
                    minX = Math.Min(minX, x); minY = Math.Min(minY, y);
                    maxX = Math.Max(maxX, x); maxY = Math.Max(maxY, y);
                }
                byte[] png = pixels.EncodeToPNG();
                if (png == null || png.Length > 8 * 1024 * 1024)
                    throw Failure("limit", "PNG encoding did not produce an admitted image of at most8MiB.");
                string hash;
                using (var sha = SHA256.Create()) hash = BitConverter.ToString(sha.ComputeHash(png)).Replace("-", "").ToLowerInvariant();
                using (var output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    ownsFile = true;
                    output.Write(png, 0, png.Length);
                    output.Flush(true);
                }
                var result = new VmRenderTextureSnapshotResult
                {
                    RenderTextureInstanceId = VmObjectId.Get(source), OutputPath = path.Replace('\\', '/'),
                    Width = source.width, Height = source.height, GraphicsFormat = source.graphicsFormat.ToString(),
                    Srgb = source.sRGB, MsaaSamples = source.antiAliasing, Frame = Time.frameCount,
                    NonzeroAlphaPixels = visible, OpaquePixels = opaque,
                    AlphaBoundsBottomLeft = visible == 0 ? new int[0] : new[] { minX, minY, maxX - minX + 1, maxY - minY + 1 },
                    PngBytes = png.Length, Sha256 = hash
                };
                complete = true;
                return result;
            }
            catch (IOException failure) { throw Failure("io", failure.Message); }
            catch (UnauthorizedAccessException failure) { throw Failure("io", failure.Message); }
            finally
            {
                RenderTexture.active = previous;
                if (pixels != null) UnityEngine.Object.DestroyImmediate(pixels);
                if (resolved != null) RenderTexture.ReleaseTemporary(resolved);
                if (ownsFile && !complete)
                {
                    File.Delete(path);
                    if (File.Exists(path)) throw Failure("io", "Owned incomplete output deletion did not complete.");
                }
            }
        }

        private static bool Supported(GraphicsFormat format) =>
            format == GraphicsFormat.R8G8B8A8_UNorm || format == GraphicsFormat.R8G8B8A8_SRGB ||
            format == GraphicsFormat.B8G8R8A8_UNorm || format == GraphicsFormat.B8G8R8A8_SRGB;

        private static string OutputPath(string input)
        {
            if (string.IsNullOrWhiteSpace(input) || input.Length > 2048 || !Path.IsPathRooted(input))
                throw Failure("path", "OutputPath must be an absolute PNG path, at most2048 characters.");
            string path;
            try { path = Path.GetFullPath(input); }
            catch (Exception failure) when (failure is ArgumentException || failure is NotSupportedException || failure is PathTooLongException)
            { throw Failure("path", failure.Message); }
            string project = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            var comparison = Application.platform == RuntimePlatform.WindowsEditor ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
            bool contained = path.StartsWith(Path.Combine(project, "Temp") + Path.DirectorySeparatorChar, comparison) ||
                             path.StartsWith(Path.Combine(project, "Tools") + Path.DirectorySeparatorChar, comparison);
            if (!contained || !string.Equals(Path.GetExtension(path), ".png", StringComparison.OrdinalIgnoreCase) ||
                File.Exists(path) || Directory.Exists(path) || !Directory.Exists(Path.GetDirectoryName(path)))
                throw Failure("path", "Create-new PNG output must be beneath an existing project Temp/Tools directory.");
            var parent = new DirectoryInfo(Path.GetDirectoryName(path));
            int count = 0;
            for (; parent != null; parent = parent.Parent)
            {
                if (++count > 64 || (parent.Attributes & FileAttributes.ReparsePoint) != 0)
                    throw Failure("path", "Output has a reparse ancestor or exceeds64 ancestors.");
            }
            return path;
        }

        private static VmProjectToolException Failure(string code, string message) =>
            new VmProjectToolException("render_snapshot_" + code, message);
    }

    public sealed class VmRenderTextureSnapshotRequest
    {
        [VmRequired, VmMinLength(1), Description("Exact decimal native RenderTexture identity; no asset/path guessing.")]
        public string RenderTextureInstanceId { get; set; }
        [VmRequired, VmMinLength(1), Description("Create-new absolute PNG path in an existing project Temp or Tools directory.")]
        public string OutputPath { get; set; }
    }

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
