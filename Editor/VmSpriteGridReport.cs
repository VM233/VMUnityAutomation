using System.Collections.Generic;

namespace VMUnityAutomation.Editor
{
    internal sealed class VmSpriteGridReport
    {
        private readonly VmSpriteGridSettings Settings;
        private readonly int[] XPartitions;
        private readonly int[] YPartitions;
        private readonly List<Dictionary<string, object>> Cells;
        private readonly int ArtworkPixels;
        private readonly int BackgroundPixelsRemoved;
        private readonly double MaxCenterErrorPixels;

        internal VmSpriteGridReport(VmSpriteGridLayout layout)
        {
            Settings = layout.Settings;
            XPartitions = layout.XPartitions;
            YPartitions = layout.YPartitions;
            Cells = layout.Cells;
            ArtworkPixels = layout.ArtworkPixels;
            BackgroundPixelsRemoved = layout.BackgroundPixelsRemoved;
            MaxCenterErrorPixels = layout.MaxCenterErrorPixels;
        }

        internal Dictionary<string, object> Describe(VmPreparedPng image, bool verified)
        {
            return new Dictionary<string, object>
            {
                { "columns", Settings.Columns }, { "rows", Settings.Rows }, { "cellSize", Settings.CellSize },
                { "backgroundAlphaMax", Settings.BackgroundAlphaMax }, { "spriteCount", Settings.CellCount },
                { "sourceWidth", image.SourceWidth }, { "sourceHeight", image.SourceHeight },
                { "width", image.Width }, { "height", image.Height }, { "artworkPixels", ArtworkPixels },
                { "backgroundPixelsRemoved", BackgroundPixelsRemoved }, { "maxCenterErrorPixels", MaxCenterErrorPixels },
                { "xPartitions", XPartitions }, { "yPartitions", YPartitions }, { "cells", Cells },
                { "sourceSha256", image.SourceHash }, { "outputSha256", image.OutputHash },
                { "outputBytes", image.Bytes.Length }, { "verified", verified },
            };
        }
    }
}
