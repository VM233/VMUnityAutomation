using System;
using UnityEngine;

namespace VMUnityAutomation.Editor
{
    internal static class VmImageResampler
    {
        internal static Color32[] Resize(Color32[] source, int sourceWidth,
            int sourceHeight, int width, int height, VmImageResizeFilter filter)
        {
            var output = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                double sourceY = (y + 0.5) * sourceHeight / height - 0.5;
                for (int x = 0; x < width; x++)
                {
                    double sourceX = (x + 0.5) * sourceWidth / width - 0.5;
                    output[y * width + x] = filter == VmImageResizeFilter.Nearest
                        ? source[(int)Math.Floor(sourceY + 0.5) * sourceWidth +
                                 (int)Math.Floor(sourceX + 0.5)]
                        : Bilinear(source, sourceWidth, sourceHeight, sourceX, sourceY);
                }
            }
            return output;
        }

        private static Color32 Bilinear(Color32[] source, int width, int height,
            double x, double y)
        {
            // The image boundary extends its edge pixels, including during enlargement.
            x = Math.Max(0, Math.Min(width - 1, x));
            y = Math.Max(0, Math.Min(height - 1, y));
            int left = (int)Math.Floor(x);
            int bottom = (int)Math.Floor(y);
            int right = Math.Min(left + 1, width - 1);
            int top = Math.Min(bottom + 1, height - 1);
            double fx = x - left;
            double fy = y - bottom;
            double alpha = 0, red = 0, green = 0, blue = 0;
            Accumulate(source[bottom * width + left], (1 - fx) * (1 - fy),
                ref alpha, ref red, ref green, ref blue);
            Accumulate(source[bottom * width + right], fx * (1 - fy),
                ref alpha, ref red, ref green, ref blue);
            Accumulate(source[top * width + left], (1 - fx) * fy,
                ref alpha, ref red, ref green, ref blue);
            Accumulate(source[top * width + right], fx * fy,
                ref alpha, ref red, ref green, ref blue);
            return alpha == 0
                ? new Color32(0, 0, 0, 0)
                : new Color32(RoundByte(red / alpha), RoundByte(green / alpha),
                    RoundByte(blue / alpha), RoundByte(alpha));
        }

        private static void Accumulate(Color32 pixel, double weight,
            ref double alpha, ref double red, ref double green, ref double blue)
        {
            double contribution = weight * pixel.a;
            alpha += contribution;
            red += contribution * pixel.r;
            green += contribution * pixel.g;
            blue += contribution * pixel.b;
        }

        private static byte RoundByte(double value) =>
            (byte)Math.Round(value, MidpointRounding.AwayFromZero);
    }
}
