using System.Collections.Generic;
using UnityEngine;
using static VMUnityAutomation.Editor.VmAutomationAssetCommandUtility;

namespace VMUnityAutomation.Editor
{
    internal static class VmPngCenterPreparation
    {
        internal static VmPreparedPng Prepare(Dictionary<string, object> settings, string sourcePath,
            long remainingPixels, long remainingBytes)
        {
            const string code = "invalid_image_center";
            if (!TryGetDictionary(settings, "center", out var values, out string error))
                throw VmPngImage.Failure(code, error);
            foreach (string key in values.Keys)
                if (key != "width" && key != "height" && key != "sourceRect" && key != "backgroundAlphaMax")
                    throw VmPngImage.Failure(code, $"Unknown center field '{key}'.");
            int outputWidth = VmPngImage.Integer(values, "width", 1, 4096, "center", code);
            int outputHeight = VmPngImage.Integer(values, "height", 1, 4096, "center", code);
            int threshold = values.ContainsKey("backgroundAlphaMax")
                ? VmPngImage.Integer(values, "backgroundAlphaMax", 0, 254, "center", code) : 0;
            VmPngImage.CheckSize(outputWidth, outputHeight, "center output");
            string path = VmPngImage.AbsolutePngPath(sourcePath, "sourcePath", code);
            byte[] source = VmPngImage.ReadSource(path);
            VmPngImage.ReadDimensions(source, path, out int width, out int height);
            var region = new RectInt(0, 0, width, height);
            if (values.ContainsKey("sourceRect"))
            {
                if (!TryGetDictionary(values, "sourceRect", out var rectangle, out error))
                    throw VmPngImage.Failure(code, error);
                region = VmPngImage.Rectangle(rectangle, "center.sourceRect", code);
            }
            if ((long)region.x + region.width > width || (long)region.y + region.height > height)
                throw VmPngImage.Failure(code, $"center.sourceRect {region} is outside '{path}' ({width}x{height}).");
            long workPixels = (long)width * height + (long)outputWidth * outputHeight;
            if (workPixels > remainingPixels || source.Length > remainingBytes)
                throw VmPngImage.Failure("image_size_limit", $"Center batch budget exceeded at '{path}' before decode.");
            if (settings.ContainsKey("spriteSlice"))
                throw VmPngImage.Failure(code, "center owns Single Sprite import; do not supply spriteSlice.");
            VmAssetImportImagePreparation.RequireSetting(settings, "textureType", "Sprite", code);
            VmAssetImportImagePreparation.RequireSetting(settings, "spriteMode", "Single", code);
            VmAssetImportImagePreparation.RequireSetting(settings, "meshType", "FullRect", code);
            if (!settings.ContainsKey("pixelsPerUnit"))
                throw VmPngImage.Failure(code, "center requires explicit project-standard pixelsPerUnit.");
            Texture2D texture = VmPngImage.Decode(source, path, width, height);
            byte[] bytes;
            Dictionary<string, object> report;
            try
            {
                Color32[] pixels = texture.GetPixels32();
                var subject = new VmPngSubject(pixels, width, region, threshold, "image_empty_subject", "center source");
                RectInt placement = subject.CenterIn(new RectInt(0, 0, outputWidth, outputHeight),
                    "image_subject_too_large", "center subject");
                var output = new Color32[outputWidth * outputHeight];
                subject.CopyTo(pixels, width, output, outputWidth, placement);
                bytes = VmPngImage.Encode(output, outputWidth, outputHeight, path);
                report = new Dictionary<string, object>
                {
                    { "regionRect", Describe(region) }, { "sourceRect", Describe(subject.Bounds) },
                    { "targetRect", Describe(placement) }, { "backgroundAlphaMax", threshold },
                    { "artworkPixels", subject.ArtworkPixels }, { "backgroundPixelsRemoved", subject.BackgroundPixelsRemoved },
                    { "centerErrorX", (placement.x * 2 + placement.width - outputWidth) / 2d },
                    { "centerErrorY", (placement.y * 2 + placement.height - outputHeight) / 2d },
                };
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
            if ((long)source.Length + bytes.Length > remainingBytes)
                throw VmPngImage.Failure("image_size_limit", $"Center encoded-byte budget exceeded at '{path}'.");
            return new VmPreparedPng(width, height, outputWidth, outputHeight,
                VmPngImage.Hash(source), source.Length, bytes, center: report);
        }

        private static Dictionary<string, object> Describe(RectInt rect) => new()
        {
            { "x", rect.x }, { "y", rect.y }, { "width", rect.width }, { "height", rect.height },
        };
    }
}
