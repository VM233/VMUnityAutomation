using System;
using System.Collections.Generic;
using UnityEngine;

namespace VMUnityAutomation.Editor
{
    internal sealed class VmSpriteGridLayout
    {
        internal Color32[] Pixels { get; }
        internal int[] XPartitions { get; }
        internal int[] YPartitions { get; }
        internal List<Dictionary<string, object>> Cells { get; } = new List<Dictionary<string, object>>();
        internal int BackgroundPixelsRemoved { get; private set; }
        internal int ArtworkPixels { get; private set; }
        internal double MaxCenterErrorPixels { get; private set; }
        internal VmSpriteGridSettings Settings { get; }

        internal VmSpriteGridLayout(Color32[] source, int width, int height, VmSpriteGridSettings settings)
        {
            Settings = settings;
            var columns = new int[width];
            var rows = new int[height];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    if (source[(height - y - 1) * width + x].a > settings.BackgroundAlphaMax)
                    {
                        columns[x]++;
                        rows[y]++;
                    }
            XPartitions = Partition(columns, settings.Columns, "column");
            YPartitions = Partition(rows, settings.Rows, "row");
            Pixels = new Color32[settings.Width * settings.Height];
            for (int row = 0; row < settings.Rows; row++)
                for (int column = 0; column < settings.Columns; column++)
                    Place(source, width, height, column, row);
        }

        private static int[] Partition(int[] projection, int count, string axis)
        {
            var edges = new int[count + 1];
            edges[count] = projection.Length;
            for (int index = 1; index < count; index++)
            {
                int lower = (int)Math.Ceiling((double)projection.Length * (4 * index - 1) / (4 * count));
                int upper = projection.Length * (4 * index + 1) / (4 * count);
                int best = -1;
                int distance = int.MaxValue;
                for (int coordinate = Math.Max(1, lower); coordinate <= Math.Min(projection.Length - 1, upper); coordinate++)
                {
                    int candidateDistance = Math.Abs(coordinate * count - projection.Length * index);
                    if (projection[coordinate] == 0 && candidateDistance < distance)
                    {
                        best = coordinate;
                        distance = candidateDistance;
                    }
                }
                if (best < 0)
                    throw VmPngImage.Failure("sprite_grid_inseparable",
                        $"No empty artwork {axis} near nominal divider {index}/{count}.");
                edges[index] = best;
            }
            return edges;
        }

        private void Place(Color32[] source, int sourceWidth, int sourceHeight, int column, int row)
        {
            int left = XPartitions[column + 1], top = YPartitions[row + 1], right = -1, bottom = -1;
            int nontransparent = 0, artwork = 0;
            for (int y = YPartitions[row]; y < YPartitions[row + 1]; y++)
                for (int x = XPartitions[column]; x < XPartitions[column + 1]; x++)
                {
                    byte alpha = source[(sourceHeight - y - 1) * sourceWidth + x].a;
                    if (alpha != 0) nontransparent++;
                    if (alpha <= Settings.BackgroundAlphaMax) continue;
                    left = Math.Min(left, x);
                    top = Math.Min(top, y);
                    right = Math.Max(right, x);
                    bottom = Math.Max(bottom, y);
                    artwork++;
                }
            int index = row * Settings.Columns + column;
            if (right < left)
                throw VmPngImage.Failure("sprite_grid_empty_cell", $"spriteGrid cell {index} has no artwork.");
            int width = right - left + 1, height = bottom - top + 1;
            if (width > Settings.CellSize || height > Settings.CellSize)
                throw VmPngImage.Failure("sprite_grid_subject_too_large",
                    $"spriteGrid cell {index} is {width}x{height}; cellSize is {Settings.CellSize}. No resizing is allowed.");
            int offsetX = (Settings.CellSize - width) / 2, offsetY = (Settings.CellSize - height) / 2;
            int targetX = column * Settings.CellSize + offsetX, targetY = row * Settings.CellSize + offsetY;
            int copiedNontransparent = 0;
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    Color32 pixel = source[(sourceHeight - top - y - 1) * sourceWidth + left + x];
                    Pixels[(Settings.Height - targetY - y - 1) * Settings.Width + targetX + x] = pixel;
                    if (pixel.a != 0) copiedNontransparent++;
                }
            double errorX = (offsetX * 2 + width - Settings.CellSize) / 2d;
            double errorY = (offsetY * 2 + height - Settings.CellSize) / 2d;
            MaxCenterErrorPixels = Math.Max(MaxCenterErrorPixels, Math.Max(Math.Abs(errorX), Math.Abs(errorY)));
            ArtworkPixels += artwork;
            BackgroundPixelsRemoved += nontransparent - copiedNontransparent;
            Cells.Add(new Dictionary<string, object>
            {
                { "index", index }, { "sourceRect", new[] { left, top, width, height } },
                { "targetRect", new[] { targetX, targetY, width, height } },
                { "centerErrorX", errorX }, { "centerErrorY", errorY }, { "artworkPixels", artwork },
            });
        }

    }
}
