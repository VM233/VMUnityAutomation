#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace VMUnityAutomation.Editor
{
    internal static class MCPUIToolkitPixelGridAuditUtility
    {
        private const double GRID_EPSILON = 0.0001d;

        private static readonly HashSet<string> auditedProperties =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "left",
                "right",
                "top",
                "bottom",
                "gap",
                "row-gap",
                "column-gap",
                "margin",
                "margin-left",
                "margin-right",
                "margin-top",
                "margin-bottom",
                "padding",
                "padding-left",
                "padding-right",
                "padding-top",
                "padding-bottom"
            };

        private static readonly Regex pixelTokenRegex =
            new Regex(
                @"(?<![A-Za-z0-9_-])(?<value>-?(?:\d+(?:\.\d+)?|\.\d+))px\b",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        internal static Dictionary<string, string> FindOffGridDeclarations(
            IReadOnlyDictionary<string, string> declarations, int gridStep)
        {
            var result =
                new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (declarations == null || gridStep <= 0)
                return result;

            foreach (var declaration in declarations)
            {
                if (auditedProperties.Contains(declaration.Key) == false ||
                    ContainsOffGridPixelValue(declaration.Value, gridStep) == false)
                {
                    continue;
                }

                result[declaration.Key] = declaration.Value;
            }

            return result;
        }

        private static bool ContainsOffGridPixelValue(string value, int gridStep)
        {
            foreach (Match match in pixelTokenRegex.Matches(value ?? ""))
            {
                double pixels;
                if (double.TryParse(match.Groups["value"].Value, NumberStyles.Float,
                        CultureInfo.InvariantCulture, out pixels) == false)
                {
                    continue;
                }

                double gridUnits = pixels / gridStep;
                if (Math.Abs(gridUnits - Math.Round(gridUnits)) > GRID_EPSILON)
                    return true;
            }

            return false;
        }
    }
}
#endif
