using System;
using System.Collections.Generic;
using System.Globalization;

namespace VMUnityAutomation.Editor
{
    internal sealed class VmSpriteGridSettings
    {
        internal const int MaximumAxis = 64;
        internal const int MaximumBatchCells = 4096;
        internal int Columns { get; }
        internal int Rows { get; }
        internal int CellSize { get; }
        internal int BackgroundAlphaMax { get; }
        internal string BaseName { get; }
        internal int CellCount => Columns * Rows;
        internal int Width => Columns * CellSize;
        internal int Height => Rows * CellSize;

        internal VmSpriteGridSettings(Dictionary<string, object> values)
        {
            foreach (string key in values.Keys)
                if (key != "columns" && key != "rows" && key != "cellSize" &&
                    key != "backgroundAlphaMax" && key != "baseName")
                    throw VmPngImage.Failure("invalid_sprite_grid", $"Unknown spriteGrid field '{key}'.");
            Columns = Integer(values, "columns", 1, MaximumAxis);
            Rows = Integer(values, "rows", 1, MaximumAxis);
            CellSize = Integer(values, "cellSize", 1, VmPngImage.MaxDimension);
            BackgroundAlphaMax = values.ContainsKey("backgroundAlphaMax")
                ? Integer(values, "backgroundAlphaMax", 0, 254) : 0;
            if (values.TryGetValue("baseName", out object name))
            {
                if (!(name is string text) || string.IsNullOrWhiteSpace(text))
                    throw VmPngImage.Failure("invalid_sprite_grid", "spriteGrid.baseName must be a nonempty string.");
                BaseName = text;
            }
            VmPngImage.CheckSize(Width, Height, "spriteGrid output");
        }

        private static int Integer(Dictionary<string, object> values, string key, int minimum, int maximum)
        {
            if (!values.TryGetValue(key, out object value) || value == null || value is bool || value is string)
                throw VmPngImage.Failure("invalid_sprite_grid", $"spriteGrid.{key} must be an integer.");
            decimal number;
            try { number = Convert.ToDecimal(value, CultureInfo.InvariantCulture); }
            catch (Exception exception) when (exception is InvalidCastException || exception is FormatException ||
                                               exception is OverflowException)
            {
                throw VmPngImage.Failure("invalid_sprite_grid", $"spriteGrid.{key} is not a finite integer.");
            }
            if (number != decimal.Truncate(number) || number < minimum || number > maximum)
                throw VmPngImage.Failure("invalid_sprite_grid",
                    $"spriteGrid.{key} must be between {minimum} and {maximum} as an integer.");
            return (int)number;
        }
    }
}
