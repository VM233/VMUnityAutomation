using System;
using UnityEngine;
using static VMUnityAutomation.Editor.VmPngImage;

namespace VMUnityAutomation.Editor
{
    internal static class VmPngResizePreparation
    {
        internal static VmPreparedPng Prepare(VmImageResizeRequest request, bool decode,
            long remainingPixels, long remainingBytes)
        {
            string path = AbsolutePngPath(request.SourcePath, "sourcePath", "invalid_resize_arguments");
            ValidateDimensions(request);
            byte[] source = ReadSource(path);
            ReadDimensions(source, path, out int sourceWidth, out int sourceHeight);
            ResolveSize(sourceWidth, sourceHeight, request, out int width, out int height);
            long workPixels = (long)sourceWidth * sourceHeight + (long)width * height;
            if (workPixels > remainingPixels || source.Length > remainingBytes)
                throw Failure("image_size_limit", $"Resize batch budget exceeded at '{path}' before decode.");
            byte[] output = !decode ? null : width == sourceWidth && height == sourceHeight
                ? ValidateAndCopy(source, path, sourceWidth, sourceHeight)
                : EncodeResized(source, path, sourceWidth, sourceHeight, width, height, request.Filter);
            if ((long)source.Length + (output?.Length ?? 0) > remainingBytes)
                throw Failure("image_size_limit", $"Resize batch encoded-byte budget exceeded at '{path}'.");
            return new VmPreparedPng(sourceWidth, sourceHeight, width, height,
                Hash(source), source.Length, output, request.Filter);
        }

        internal static void ValidateDimensions(VmImageResizeRequest request)
        {
            if (!request.Width.HasValue && !request.Height.HasValue)
                throw Failure("invalid_resize_arguments", "Supply width, height, or both.");
            if (request.Width.HasValue && (request.Width < 1 || request.Width > MaxDimension) ||
                request.Height.HasValue && (request.Height < 1 || request.Height > MaxDimension))
                throw Failure("invalid_resize_arguments", $"Dimensions must be between 1 and {MaxDimension} pixels.");
            if (request.Filter != VmImageResizeFilter.Bilinear && request.Filter != VmImageResizeFilter.Nearest)
                throw Failure("invalid_resize_arguments", "filter must be Bilinear or Nearest.");
        }

        internal static void ResolveSize(int sourceWidth, int sourceHeight,
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

        private static byte[] EncodeResized(byte[] bytes, string path, int sourceWidth,
            int sourceHeight, int width, int height, VmImageResizeFilter filter)
        {
            Texture2D source = Decode(bytes, path, sourceWidth, sourceHeight);
            try
            {
                Color32[] pixels = VmImageResampler.Resize(source.GetPixels32(), sourceWidth,
                    sourceHeight, width, height, filter);
                return Encode(pixels, width, height, path);
            }
            finally { UnityEngine.Object.DestroyImmediate(source); }
        }
    }
}
