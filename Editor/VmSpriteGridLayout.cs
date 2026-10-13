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
            int index = row * Settings.Columns + column;
            var region = new RectInt(XPartitions[column], sourceHeight - YPartitions[row + 1],
                XPartitions[column + 1] - XPartitions[column], YPartitions[row + 1] - YPartitions[row]);
            var subject = new VmPngSubject(source, sourceWidth, region, Settings.BackgroundAlphaMax,
                "sprite_grid_empty_cell", $"spriteGrid cell {index}");
            RectInt cellPlacement = subject.CenterIn(new RectInt(0, 0, Settings.CellSize, Settings.CellSize),
                "sprite_grid_subject_too_large", $"spriteGrid cell {index}");
            int left = subject.Bounds.x, top = sourceHeight - subject.Bounds.yMax;
            int width = subject.Bounds.width, height = subject.Bounds.height;
            int offsetX = cellPlacement.x, offsetY = cellPlacement.y;
            int targetX = column * Settings.CellSize + offsetX, targetY = row * Settings.CellSize + offsetY;
            subject.CopyTo(source, sourceWidth, Pixels, Settings.Width,
                new RectInt(targetX, Settings.Height - targetY - height, width, height));
            double errorX = (offsetX * 2 + width - Settings.CellSize) / 2d;
            double errorY = (offsetY * 2 + height - Settings.CellSize) / 2d;
            MaxCenterErrorPixels = Math.Max(MaxCenterErrorPixels, Math.Max(Math.Abs(errorX), Math.Abs(errorY)));
            ArtworkPixels += subject.ArtworkPixels;
            BackgroundPixelsRemoved += subject.BackgroundPixelsRemoved;
            Cells.Add(new Dictionary<string, object>
            {
                { "index", index }, { "sourceRect", new[] { left, top, width, height } },
                { "targetRect", new[] { targetX, targetY, width, height } },
                { "centerErrorX", errorX }, { "centerErrorY", errorY }, { "artworkPixels", subject.ArtworkPixels },
            });
        }

    }
}
