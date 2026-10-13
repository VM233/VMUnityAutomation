using System;
using UnityEngine;

namespace VMUnityAutomation.Editor
{
    internal sealed class VmPngSubject
    {
        internal RectInt Bounds { get; }
        internal int ArtworkPixels { get; }
        internal int BackgroundPixelsRemoved { get; }

        internal VmPngSubject(Color32[] pixels, int sourceWidth, RectInt region,
            int backgroundAlphaMax, string emptyCode, string context)
        {
            int left = region.xMax, bottom = region.yMax, right = -1, top = -1;
            int nontransparent = 0;
            for (int y = region.y; y < region.yMax; y++)
                for (int x = region.x; x < region.xMax; x++)
                {
                    byte alpha = pixels[y * sourceWidth + x].a;
                    if (alpha != 0) nontransparent++;
                    if (alpha <= backgroundAlphaMax) continue;
                    left = Math.Min(left, x);
                    bottom = Math.Min(bottom, y);
                    right = Math.Max(right, x);
                    top = Math.Max(top, y);
                    ArtworkPixels++;
                }
            if (right < left)
                throw VmPngImage.Failure(emptyCode, $"{context} has no artwork above alpha {backgroundAlphaMax}.");
            Bounds = new RectInt(left, bottom, right - left + 1, top - bottom + 1);
            int retained = 0;
            for (int y = Bounds.y; y < Bounds.yMax; y++)
                for (int x = Bounds.x; x < Bounds.xMax; x++)
                    if (pixels[y * sourceWidth + x].a != 0) retained++;
            BackgroundPixelsRemoved = nontransparent - retained;
        }

        internal RectInt CenterIn(RectInt canvas, string tooLargeCode, string context)
        {
            if (Bounds.width > canvas.width || Bounds.height > canvas.height)
                throw VmPngImage.Failure(tooLargeCode,
                    $"{context} is {Bounds.width}x{Bounds.height}; canvas is {canvas.width}x{canvas.height}. No resizing is allowed.");
            return new RectInt(canvas.x + (canvas.width - Bounds.width) / 2,
                canvas.y + (canvas.height - Bounds.height) / 2, Bounds.width, Bounds.height);
        }

        internal void CopyTo(Color32[] source, int sourceWidth, Color32[] target,
            int targetWidth, RectInt placement)
        {
            for (int row = 0; row < Bounds.height; row++)
                Array.Copy(source, (Bounds.y + row) * sourceWidth + Bounds.x,
                    target, (placement.y + row) * targetWidth + placement.x, Bounds.width);
        }
    }
}
