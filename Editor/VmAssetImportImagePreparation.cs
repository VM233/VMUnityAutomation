using System;
using System.Collections.Generic;
using UnityEngine;
using static VMUnityAutomation.Editor.VmAutomationAssetCommandUtility;

namespace VMUnityAutomation.Editor
{
    internal static class VmAssetImportImagePreparation
    {
        internal static VmPreparedPng Prepare(Dictionary<string, object> settings, string sourcePath,
            ref long remainingPixels, ref long remainingBytes, ref int remainingGridCells,
            out VmSpriteGridReport gridReport)
        {
            gridReport = null;
            bool resize = settings.ContainsKey("resize"), grid = settings.ContainsKey("spriteGrid"),
                crop = settings.ContainsKey("crop"), center = settings.ContainsKey("center");
            if (!resize && !grid && !crop && !center) return null;
            if ((resize ? 1 : 0) + (grid ? 1 : 0) + (crop ? 1 : 0) + (center ? 1 : 0) > 1)
                throw VmPngImage.Failure(center ? "invalid_image_center" : grid ? "invalid_sprite_grid" : "invalid_image_crop",
                    "center, crop, spriteGrid and resize are exclusive image operations.");
            VmPreparedPng image;
            if (center)
                image = VmPngCenterPreparation.Prepare(settings, sourcePath, remainingPixels, remainingBytes);
            else if (grid)
                image = PrepareGrid(settings, sourcePath, remainingPixels, remainingBytes,
                    ref remainingGridCells, out gridReport);
            else if (crop)
                image = VmPngCropPreparation.Prepare(settings, sourcePath, remainingPixels, remainingBytes);
            else
                image = VmPngResizePreparation.Prepare(ResizeRequest(settings, sourcePath), true,
                    remainingPixels, remainingBytes);
            remainingPixels -= image.WorkPixels;
            remainingBytes -= image.SourceBytes + (long)image.Bytes.Length;
            return image;
        }

        private static VmImageResizeRequest ResizeRequest(Dictionary<string, object> settings, string sourcePath)
        {
            if (!TryGetDictionary(settings, "resize", out var resize, out string error))
                throw new ArgumentException(error);
            foreach (string key in resize.Keys)
                if (key != "width" && key != "height" && key != "filter")
                    throw new ArgumentException($"Unknown resize field '{key}'.");
            var request = new VmImageResizeRequest { SourcePath = sourcePath };
            foreach (string key in new[] { "width", "height" })
            {
                if (!resize.TryGetValue(key, out object value)) continue;
                if (value == null || value is bool || !decimal.TryParse(value.ToString(),
                        System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture,
                        out decimal dimension) || dimension != decimal.Truncate(dimension) || dimension < 1 || dimension > 4096)
                    throw new ArgumentException($"resize.{key} must be an integer between 1 and 4096.");
                if (key == "width") request.Width = (int)dimension;
                else request.Height = (int)dimension;
            }
            if (resize.TryGetValue("filter", out object filter))
            {
                if (!(filter is string filterName) || !Enum.TryParse(filterName, false, out VmImageResizeFilter parsed))
                    throw new ArgumentException("resize.filter must be Bilinear or Nearest.");
                request.Filter = parsed;
            }
            return request;
        }

        private static VmPreparedPng PrepareGrid(Dictionary<string, object> settings, string sourcePath,
            long remainingPixels, long remainingBytes, ref int remainingGridCells, out VmSpriteGridReport report)
        {
            if (settings.ContainsKey("spriteSlice"))
                throw VmPngImage.Failure("invalid_sprite_grid", "spriteGrid owns slicing; do not also supply spriteSlice.");
            if (!TryGetDictionary(settings, "spriteGrid", out var values, out string error))
                throw VmPngImage.Failure("invalid_sprite_grid", error);
            var grid = new VmSpriteGridSettings(values);
            if (grid.CellCount > remainingGridCells)
                throw VmPngImage.Failure("image_size_limit", "Sprite grid batch exceeds 4096 cells before decoding.");
            RequireSetting(settings, "textureType", "Sprite");
            RequireSetting(settings, "spriteMode", "Multiple");
            RequireSetting(settings, "meshType", "FullRect");
            if (!settings.ContainsKey("pixelsPerUnit"))
                throw VmPngImage.Failure("invalid_sprite_grid", "spriteGrid requires explicit project-standard pixelsPerUnit.");
            string path = VmPngImage.AbsolutePngPath(sourcePath, "sourcePath", "invalid_sprite_grid");
            byte[] source = VmPngImage.ReadSource(path);
            VmPngImage.ReadDimensions(source, path, out int width, out int height);
            long workPixels = (long)width * height + (long)grid.Width * grid.Height;
            if (workPixels > remainingPixels || source.Length > remainingBytes)
                throw VmPngImage.Failure("image_size_limit", $"Sprite grid batch budget exceeded at '{path}' before decode.");
            Texture2D texture = VmPngImage.Decode(source, path, width, height);
            byte[] bytes;
            try
            {
                var layout = new VmSpriteGridLayout(texture.GetPixels32(), width, height, grid);
                bytes = VmPngImage.Encode(layout.Pixels, grid.Width, grid.Height, path);
                report = new VmSpriteGridReport(layout);
            }
            finally { UnityEngine.Object.DestroyImmediate(texture); }
            if ((long)source.Length + bytes.Length > remainingBytes)
                throw VmPngImage.Failure("image_size_limit", $"Sprite grid encoded-byte budget exceeded at '{path}'.");
            remainingGridCells -= grid.CellCount;
            var slice = new Dictionary<string, object>
            {
                { "frameWidth", grid.CellSize }, { "frameHeight", grid.CellSize },
                { "columns", grid.Columns }, { "frameCount", grid.CellCount },
                { "pivotX", 0.5 }, { "pivotY", 0.5 }, { "preserveSpriteIDs", true },
            };
            if (grid.BaseName != null) slice["baseName"] = grid.BaseName;
            settings["spriteSlice"] = slice;
            return new VmPreparedPng(width, height, grid.Width, grid.Height,
                VmPngImage.Hash(source), source.Length, bytes);
        }

        internal static void RequireSetting(Dictionary<string, object> settings, string key, string required,
            string code = "invalid_sprite_grid")
        {
            if (settings.TryGetValue(key, out object value) &&
                !string.Equals(value as string, required, StringComparison.OrdinalIgnoreCase))
                throw VmPngImage.Failure(code, $"Image preparation requires {key}={required}.");
            settings[key] = required;
        }
    }
}
