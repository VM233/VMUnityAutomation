using System;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;

namespace VMUnityAutomation.Editor
{
    [VmProjectTool("image/resize",
        Description = "Resize one local 8-bit PNG before Unity import. Preserve aspect ratio and alpha, write only the explicit output PNG, and return dimensions and verified hashes. Never modify PPU or import settings.",
        MutatesProjectFiles = true,
        SideEffects = VmProjectToolSideEffect.PerformsExternalIO |
                      VmProjectToolSideEffect.CreatesTemporaryObjects,
        ErrorCodes = new[] { "invalid_resize_arguments", "image_source_not_found",
            "image_output_exists", "image_output_is_unity_asset", "image_size_limit",
            "image_format_unsupported", "image_read_failed", "image_decode_failed",
            "image_encode_failed", "image_write_failed", "image_verification_failed" },
        Preconditions = new[] { "absolute-source-and-output-paths", "existing-output-directory",
            "distinct-source-and-output", "output-outside-assets-and-packages" },
        CompletionEvidence = "Executed results include exact source/output dimensions, SHA-256 and byte count. verified=true requires a matching on-disk readback. dryRun validates headers only and reports verified=false.")]
    public sealed class VmImageResizeTool : IVmProjectTool<VmImageResizeRequest, VmImageResizeResult>
    {
        internal const int MaxDimension = 4096;
        internal const int MaxPixels = 4 * 1024 * 1024;
        internal const int MaxFileBytes = 32 * 1024 * 1024;

        public VmImageResizeResult Execute(VmImageResizeRequest request)
        {
            if (request == null)
                throw Failure("invalid_resize_arguments", "A resize request is required.");
            string sourcePath = AbsolutePngPath(request.SourcePath, "sourcePath");
            string outputPath = AbsolutePngPath(request.OutputPath, "outputPath");
            if (string.Equals(sourcePath, outputPath, StringComparison.OrdinalIgnoreCase))
                throw Failure("invalid_resize_arguments", "Source and output must be different files.");
            ValidateDimensions(request);
            ValidateOutput(outputPath, request.Overwrite);
            byte[] sourceBytes = ReadSource(sourcePath);
            ReadDimensions(sourceBytes, sourcePath, out int sourceWidth, out int sourceHeight);
            ResolveSize(sourceWidth, sourceHeight, request, out int width, out int height);
            string sourceHash = Hash(sourceBytes);
            if (request.DryRun)
                return new VmImageResizeResult(sourcePath, outputPath, sourceWidth, sourceHeight,
                    width, height, request.Filter, sourceHash, "", 0, true);

            byte[] outputBytes = width == sourceWidth && height == sourceHeight
                ? ValidateAndCopy(sourceBytes, sourcePath, sourceWidth, sourceHeight)
                : EncodeResized(sourceBytes, sourcePath, sourceWidth, sourceHeight,
                    width, height, request.Filter);
            string outputHash = Hash(outputBytes);
            try
            {
                using (var stream = new FileStream(outputPath,
                           request.Overwrite ? FileMode.Create : FileMode.CreateNew,
                           FileAccess.Write, FileShare.None))
                {
                    stream.Write(outputBytes, 0, outputBytes.Length);
                    stream.Flush();
                }
            }
            catch (Exception exception) when (exception is IOException ||
                                               exception is UnauthorizedAccessException)
            {
                throw Failure("image_write_failed",
                    $"Could not write '{outputPath}'. The output may be incomplete: {exception.Message}");
            }

            byte[] readback = ReadSource(outputPath);
            ReadDimensions(readback, outputPath, out int actualWidth, out int actualHeight);
            if (actualWidth != width || actualHeight != height || Hash(readback) != outputHash)
                throw Failure("image_verification_failed", $"Output readback differs at '{outputPath}'.");
            return new VmImageResizeResult(sourcePath, outputPath, sourceWidth, sourceHeight,
                width, height, request.Filter, sourceHash, outputHash, outputBytes.Length, false);
        }

        private static void ValidateDimensions(VmImageResizeRequest request)
        {
            if (!request.Width.HasValue && !request.Height.HasValue)
                throw Failure("invalid_resize_arguments", "Supply width, height, or both.");
            if (request.Width.HasValue && (request.Width < 1 || request.Width > MaxDimension) ||
                request.Height.HasValue && (request.Height < 1 || request.Height > MaxDimension))
                throw Failure("invalid_resize_arguments", $"Dimensions must be between 1 and {MaxDimension} pixels.");
            if (request.Filter != VmImageResizeFilter.Bilinear && request.Filter != VmImageResizeFilter.Nearest)
                throw Failure("invalid_resize_arguments", "filter must be Bilinear or Nearest.");
        }

        private static void ResolveSize(int sourceWidth, int sourceHeight,
            VmImageResizeRequest request, out int width, out int height)
        {
            double scale = request.Width.HasValue
                ? (double)request.Width.Value / sourceWidth
                : (double)request.Height.Value / sourceHeight;
            if (request.Height.HasValue)
                scale = Math.Min(scale, (double)request.Height.Value / sourceHeight);
            width = Math.Max(1, (int)Math.Round(sourceWidth * scale, MidpointRounding.AwayFromZero));
            height = Math.Max(1, (int)Math.Round(sourceHeight * scale, MidpointRounding.AwayFromZero));
            CheckSize(width, height, "output");
        }

        private static string AbsolutePngPath(string path, string argument)
        {
            if (string.IsNullOrWhiteSpace(path) || !Path.IsPathRooted(path))
                throw Failure("invalid_resize_arguments", $"{argument} must be an absolute PNG path.");
            string absolute;
            try { absolute = Path.GetFullPath(path); }
            catch (Exception exception) when (exception is ArgumentException ||
                                               exception is NotSupportedException || exception is IOException)
            {
                throw Failure("invalid_resize_arguments", $"Invalid {argument}: {exception.Message}");
            }
            if (!string.Equals(Path.GetExtension(absolute), ".png", StringComparison.OrdinalIgnoreCase))
                throw Failure("image_format_unsupported", $"{argument} must have the .png extension.");
            return absolute;
        }

        private static void ValidateOutput(string path, bool overwrite)
        {
            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            foreach (string protectedName in new[] { "Assets", "Packages" })
            {
                string root = Path.Combine(projectRoot, protectedName) + Path.DirectorySeparatorChar;
                if (path.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                    throw Failure("image_output_is_unity_asset",
                        $"Output '{path}' is a Unity asset. Resize outside Assets/Packages, then use asset/import.");
            }
            if (!Directory.Exists(Path.GetDirectoryName(path)))
                throw Failure("invalid_resize_arguments", $"Output directory does not exist for '{path}'.");
            if (File.Exists(path) && !overwrite)
                throw Failure("image_output_exists", $"Output '{path}' exists. Set overwrite=true to replace it.");
            if (File.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
                throw Failure("invalid_resize_arguments", $"Output '{path}' is a symbolic link.");
            DirectoryInfo directory = new DirectoryInfo(Path.GetDirectoryName(path));
            for (int depth = 0; directory != null; depth++, directory = directory.Parent)
            {
                if (depth >= 128)
                    throw Failure("invalid_resize_arguments", "Output directory ancestry exceeds 128 entries.");
                if ((directory.Attributes & FileAttributes.ReparsePoint) != 0)
                    throw Failure("invalid_resize_arguments", $"Output directory '{directory.FullName}' is a symbolic link or junction.");
            }
        }

        private static byte[] ReadSource(string path)
        {
            try
            {
                using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    if (stream.Length > MaxFileBytes)
                        throw Failure("image_size_limit", $"PNG '{path}' exceeds the {MaxFileBytes}-byte limit.");
                    var bytes = new byte[(int)stream.Length];
                    int offset = 0;
                    while (offset < bytes.Length)
                    {
                        int read = stream.Read(bytes, offset, bytes.Length - offset);
                        if (read == 0)
                            throw new EndOfStreamException($"PNG '{path}' ended before its declared length.");
                        offset += read;
                    }
                    return bytes;
                }
            }
            catch (FileNotFoundException)
            {
                throw Failure("image_source_not_found", $"PNG source was not found: '{path}'.");
            }
            catch (Exception exception) when (exception is IOException ||
                                               exception is UnauthorizedAccessException)
            {
                throw Failure("image_read_failed", $"Could not read '{path}': {exception.Message}");
            }
        }

        private static void ReadDimensions(byte[] png, string path, out int width, out int height)
        {
            byte[] signature = { 137, 80, 78, 71, 13, 10, 26, 10 };
            if (png.Length < 33)
                throw Failure("image_format_unsupported", $"PNG header is incomplete: '{path}'.");
            for (int index = 0; index < signature.Length; index++)
                if (png[index] != signature[index])
                    throw Failure("image_format_unsupported", $"File is not a PNG: '{path}'.");
            if (ReadBigEndian(png, 8) != 13 || png[12] != 'I' || png[13] != 'H' ||
                png[14] != 'D' || png[15] != 'R' || png[24] != 8)
                throw Failure("image_format_unsupported", $"An 8-bit PNG with an IHDR header is required: '{path}'.");
            uint rawWidth = ReadBigEndian(png, 16);
            uint rawHeight = ReadBigEndian(png, 20);
            if (rawWidth > MaxDimension || rawHeight > MaxDimension)
                throw Failure("image_size_limit", $"PNG '{path}' dimensions exceed {MaxDimension} pixels.");
            width = (int)rawWidth;
            height = (int)rawHeight;
            CheckSize(width, height, path);
        }

        private static uint ReadBigEndian(byte[] bytes, int offset) =>
            ((uint)bytes[offset] << 24) | ((uint)bytes[offset + 1] << 16) |
            ((uint)bytes[offset + 2] << 8) | bytes[offset + 3];

        private static void CheckSize(int width, int height, string path)
        {
            if (width < 1 || height < 1 || width > MaxDimension || height > MaxDimension ||
                (long)width * height > MaxPixels)
                throw Failure("image_size_limit",
                    $"Image '{path}' is {width}x{height}. Maximum side is {MaxDimension}, maximum pixel count is {MaxPixels}.");
        }

        private static Texture2D Decode(byte[] bytes, string path, int width, int height)
        {
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            try
            {
                if (!ImageConversion.LoadImage(texture, bytes, false) ||
                    texture.width != width || texture.height != height)
                    throw Failure("image_decode_failed", $"PNG decoder did not produce the admitted dimensions for '{path}'.");
                return texture;
            }
            catch
            {
                UnityEngine.Object.DestroyImmediate(texture);
                throw;
            }
        }

        private static byte[] ValidateAndCopy(byte[] bytes, string path, int width, int height)
        {
            Texture2D texture = Decode(bytes, path, width, height);
            UnityEngine.Object.DestroyImmediate(texture);
            return bytes;
        }

        private static byte[] EncodeResized(byte[] bytes, string path, int sourceWidth,
            int sourceHeight, int width, int height, VmImageResizeFilter filter)
        {
            Texture2D source = Decode(bytes, path, sourceWidth, sourceHeight);
            Texture2D output = null;
            try
            {
                Color32[] pixels = VmImageResampler.Resize(source.GetPixels32(), sourceWidth,
                    sourceHeight, width, height, filter);
                output = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
                output.SetPixels32(pixels);
                byte[] encoded = ImageConversion.EncodeToPNG(output);
                if (encoded == null || encoded.Length == 0 || encoded.Length > MaxFileBytes)
                    throw Failure("image_encode_failed", $"PNG encoding failed within the {MaxFileBytes}-byte limit for '{path}'.");
                return encoded;
            }
            finally
            {
                if (output != null) UnityEngine.Object.DestroyImmediate(output);
                UnityEngine.Object.DestroyImmediate(source);
            }
        }

        private static string Hash(byte[] bytes)
        {
            using (SHA256 sha256 = SHA256.Create())
                return BitConverter.ToString(sha256.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }

        private static VmProjectToolException Failure(string code, string message) =>
            new VmProjectToolException(code, message);
    }
}
