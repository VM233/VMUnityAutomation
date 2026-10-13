using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using static VMUnityAutomation.Editor.VmAutomationAssetCommandUtility;

namespace VMUnityAutomation.Editor
{
    internal static class VmPngCropPreparation
    {
        internal static VmPreparedPng Prepare(Dictionary<string, object> settings, string sourcePath,
            long remainingPixels, long remainingBytes)
        {
            if (!TryGetDictionary(settings, "crop", out var values, out string error))
                throw VmPngImage.Failure("invalid_image_crop", error);
            foreach (string key in values.Keys)
                if (key != "x" && key != "y" && key != "width" && key != "height")
                    throw VmPngImage.Failure("invalid_image_crop", $"Unknown crop field '{key}'.");
            var rect = new RectInt(Dimension(values, "x", 0), Dimension(values, "y", 0),
                Dimension(values, "width", 1), Dimension(values, "height", 1));
            string path = VmPngImage.AbsolutePngPath(sourcePath, "sourcePath", "invalid_image_crop");
            byte[] source = VmPngImage.ReadSource(path);
            VmPngImage.ReadDimensions(source, path, out int width, out int height);
            if ((long)rect.x + rect.width > width || (long)rect.y + rect.height > height)
                throw VmPngImage.Failure("invalid_image_crop",
                    $"Bottom-left crop {rect} is outside PNG '{path}' ({width}x{height}).");
            long workPixels = (long)width * height + (long)rect.width * rect.height;
            if (workPixels > remainingPixels || source.Length > remainingBytes)
                throw VmPngImage.Failure("image_size_limit", $"Crop batch budget exceeded at '{path}' before decode.");

            Texture2D texture = VmPngImage.Decode(source, path, width, height);
            byte[] bytes;
            try
            {
                Color32[] pixels = texture.GetPixels32();
                var cropped = new Color32[rect.width * rect.height];
                for (int row = 0; row < rect.height; row++)
                    Array.Copy(pixels, (rect.y + row) * width + rect.x, cropped, row * rect.width, rect.width);
                bytes = VmPngImage.Encode(cropped, rect.width, rect.height, path);
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
            if ((long)source.Length + bytes.Length > remainingBytes)
                throw VmPngImage.Failure("image_size_limit", $"Crop encoded-byte budget exceeded at '{path}'.");
            return new VmPreparedPng(width, height, rect.width, rect.height,
                VmPngImage.Hash(source), source.Length, bytes, crop: rect);
        }

        private static int Dimension(Dictionary<string, object> values, string key, int minimum)
        {
            if (!values.TryGetValue(key, out object value) || value == null || value is bool || value is string ||
                !decimal.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), NumberStyles.Float,
                    CultureInfo.InvariantCulture, out decimal parsed) || parsed != decimal.Truncate(parsed) ||
                parsed < minimum || parsed > VmPngImage.MaxDimension)
                throw VmPngImage.Failure("invalid_image_crop",
                    $"crop.{key} must be an integer from {minimum} through {VmPngImage.MaxDimension}.");
            return (int)parsed;
        }
    }
}
