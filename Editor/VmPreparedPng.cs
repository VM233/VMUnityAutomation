using System.Collections.Generic;
using UnityEngine;

namespace VMUnityAutomation.Editor
{
    internal sealed class VmPreparedPng
    {
        internal int SourceWidth { get; }
        internal int SourceHeight { get; }
        internal int Width { get; }
        internal int Height { get; }
        internal VmImageResizeFilter? Filter { get; }
        internal RectInt? Crop { get; }
        internal Dictionary<string, object> Center { get; }
        internal string SourceHash { get; }
        internal string OutputHash { get; }
        internal int SourceBytes { get; }
        internal byte[] Bytes { get; }
        internal long WorkPixels => (long)SourceWidth * SourceHeight + (long)Width * Height;

        internal VmPreparedPng(int sourceWidth, int sourceHeight, int width, int height,
            string sourceHash, int sourceBytes, byte[] bytes, VmImageResizeFilter? filter = null,
            RectInt? crop = null, Dictionary<string, object> center = null)
        {
            SourceWidth = sourceWidth;
            SourceHeight = sourceHeight;
            Width = width;
            Height = height;
            SourceHash = sourceHash;
            SourceBytes = sourceBytes;
            Bytes = bytes;
            Filter = filter;
            Crop = crop;
            Center = center;
            OutputHash = bytes == null ? "" : VmPngImage.Hash(bytes);
        }

        internal void VerifyFile(string path)
        {
            byte[] readback = VmPngImage.ReadSource(path);
            VmPngImage.ReadDimensions(readback, path, out int width, out int height);
            if (width != Width || height != Height || VmPngImage.Hash(readback) != OutputHash)
                throw VmPngImage.Failure("image_verification_failed", $"Output readback differs at '{path}'.");
        }
    }
}
