using System;
using System.Collections.Generic;
using System.Globalization;

namespace VMUnityAutomation.Editor
{
    internal sealed class VmPlayerBuildObservationOptions
    {
        public double RunSeconds { get; }
        public int WindowWaitMs { get; }
        public int LogTailLines { get; }

        public VmPlayerBuildObservationOptions(Dictionary<string, object> arguments)
        {
            RunSeconds = Read(arguments, "runSeconds", 5, 0, 86400, false);
            WindowWaitMs = (int)Read(arguments, "windowWaitMs", 5000, 0, 60000, true);
            LogTailLines = (int)Read(arguments, "logTailLines", 120, 1, 2000, true);
        }

        private static double Read(Dictionary<string, object> arguments, string key,
            double defaultValue, double minimum, double maximum, bool integer)
        {
            if (!arguments.TryGetValue(key, out object input)) return defaultValue;
            if (input == null || input is bool || input is string ||
                !double.TryParse(Convert.ToString(input, CultureInfo.InvariantCulture),
                    NumberStyles.Float, CultureInfo.InvariantCulture, out double value) ||
                double.IsNaN(value) || double.IsInfinity(value) || value < minimum || value > maximum ||
                (integer && value != Math.Truncate(value)))
                throw VmPlayerLaunchArguments.Invalid(
                    $"{key} must be {(integer ? "an integer" : "a finite number")} in [{minimum}, {maximum}].");
            return value;
        }
    }
}
