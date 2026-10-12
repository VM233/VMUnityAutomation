using System;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;

namespace VMUnityAutomation.Editor
{
    internal static class VmPngImage
    {
        internal const int MaxDimension = 4096;
        internal const int MaxPixels = 4 * 1024 * 1024;
        internal const int MaxFileBytes = 32 * 1024 * 1024;

        internal static string AbsolutePngPath(string path, string argument, string argumentErrorCode)
        {
            if (string.IsNullOrWhiteSpace(path) || !Path.IsPathRooted(path))
                throw Failure(argumentErrorCode, $"{argument} must be an absolute PNG path.");
            string absolute;
            try { absolute = Path.GetFullPath(path); }
            catch (Exception exception) when (exception is ArgumentException ||
                                               exception is NotSupportedException || exception is IOException)
            {
                throw Failure(argumentErrorCode, $"Invalid {argument}: {exception.Message}");
            }
            if (!string.Equals(Path.GetExtension(absolute), ".png", StringComparison.OrdinalIgnoreCase))
                throw Failure("image_format_unsupported", $"{argument} must have the .png extension.");
            return absolute;
        }

        internal static byte[] ReadSource(string path)
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

        internal static void ReadDimensions(byte[] png, string path, out int width, out int height)
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

        internal static uint ReadBigEndian(byte[] bytes, int offset) =>
            ((uint)bytes[offset] << 24) | ((uint)bytes[offset + 1] << 16) |
            ((uint)bytes[offset + 2] << 8) | bytes[offset + 3];

        internal static void CheckSize(int width, int height, string path)
        {
            if (width < 1 || height < 1 || width > MaxDimension || height > MaxDimension ||
                (long)width * height > MaxPixels)
                throw Failure("image_size_limit",
                    $"Image '{path}' is {width}x{height}. Maximum side is {MaxDimension}, maximum pixel count is {MaxPixels}.");
        }

        internal static Texture2D Decode(byte[] bytes, string path, int width, int height)
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

        internal static byte[] ValidateAndCopy(byte[] bytes, string path, int width, int height)
        {
            Texture2D texture = Decode(bytes, path, width, height);
            UnityEngine.Object.DestroyImmediate(texture);
            return bytes;
        }

        internal static byte[] Encode(Color32[] pixels, int width, int height, string path)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
            try
            {
                texture.SetPixels32(pixels);
                byte[] encoded = ImageConversion.EncodeToPNG(texture);
                if (encoded == null || encoded.Length == 0 || encoded.Length > MaxFileBytes)
                    throw Failure("image_encode_failed", $"PNG encoding failed within the {MaxFileBytes}-byte limit for '{path}'.");
                return encoded;
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
        }

        internal static string Hash(byte[] bytes)
        {
            using (SHA256 sha256 = SHA256.Create())
                return BitConverter.ToString(sha256.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }

        internal static VmProjectToolException Failure(string code, string message) =>
            new VmProjectToolException(code, message);
    }
}
