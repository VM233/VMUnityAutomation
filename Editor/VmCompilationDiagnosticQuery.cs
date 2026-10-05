using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace VMUnityAutomation.Editor
{
    internal sealed class VmCompilationDiagnosticQuery
    {
        internal const int DefaultCount = 50;
        internal const int MaximumCount = 200;
        internal int Count { get; }
        internal int Offset { get; }
        internal int DeprecatedOffset { get; }
        internal string Severity { get; }
        internal string SnapshotRevision { get; }

        private VmCompilationDiagnosticQuery(int count, int offset,
            int deprecatedOffset, string severity, string snapshotRevision)
        {
            Count = count;
            Offset = offset;
            DeprecatedOffset = deprecatedOffset;
            Severity = severity;
            SnapshotRevision = snapshotRevision;
        }

        internal static Dictionary<string, object> CreateInputSchema()
        {
            var properties = VmAutomationToolSchemaFactory.Props(
                VmAutomationToolSchemaFactory.Prop("count", "integer",
                    "Page size for each diagnostic array. Defaults to 50; admits 1 through 200."),
                VmAutomationToolSchemaFactory.Prop("offset", "integer",
                    "Matching messages skipped from newest to oldest. Defaults to 0."),
                VmAutomationToolSchemaFactory.Prop("deprecatedOffset", "integer",
                    "Obsolete warnings skipped from newest to oldest. Defaults to 0."),
                VmAutomationToolSchemaFactory.EnumProp("severity",
                    "Filter ordinary entries; aggregate counts remain unfiltered. Defaults to all.",
                    "all", "error", "warning"),
                VmAutomationToolSchemaFactory.Prop("snapshotRevision", "string",
                    "Revision returned by the previous page. Required for either nonzero offset; a changed product is rejected."));
            ((Dictionary<string, object>)properties["count"])["minimum"] = 1;
            ((Dictionary<string, object>)properties["count"])["maximum"] = MaximumCount;
            foreach (string name in new[] { "offset", "deprecatedOffset" })
            {
                ((Dictionary<string, object>)properties[name])["minimum"] = 0;
                ((Dictionary<string, object>)properties[name])["maximum"] = int.MaxValue;
            }
            ((Dictionary<string, object>)properties["snapshotRevision"])["minLength"] = 1;
            var schema = VmAutomationToolSchemaFactory.Schema(properties);
            schema["anyOf"] = new List<object>
            {
                new Dictionary<string, object>
                {
                    ["properties"] = new Dictionary<string, object>
                    {
                        ["offset"] = new Dictionary<string, object> { ["maximum"] = 0 },
                        ["deprecatedOffset"] = new Dictionary<string, object> { ["maximum"] = 0 },
                    },
                },
                new Dictionary<string, object> { ["required"] = new[] { "snapshotRevision" } },
            };
            return schema;
        }

        internal static bool TryParse(Dictionary<string, object> arguments,
            out VmCompilationDiagnosticQuery query, out object error)
        {
            try
            {
                var properties = (Dictionary<string, object>)CreateInputSchema()["properties"];
                string unsupported = arguments.Keys.FirstOrDefault(name =>
                    name != "_agentId" && !properties.ContainsKey(name));
                if (unsupported != null)
                    throw new ArgumentException($"Unsupported compilation diagnostic argument '{unsupported}'.");
                int count = ReadInteger(arguments, "count", DefaultCount, 1, MaximumCount);
                int offset = ReadInteger(arguments, "offset", 0, 0, int.MaxValue);
                int deprecatedOffset = ReadInteger(arguments, "deprecatedOffset", 0, 0, int.MaxValue);
                string severity = "all";
                if (arguments.TryGetValue("severity", out object severityValue))
                {
                    if (severityValue is not string text ||
                        (text != "all" && text != "error" && text != "warning"))
                        throw new ArgumentException("severity must be all, error or warning.");
                    severity = text;
                }
                string revision = null;
                if (arguments.TryGetValue("snapshotRevision", out object revisionValue))
                {
                    if (revisionValue is not string text || string.IsNullOrWhiteSpace(text))
                        throw new ArgumentException("snapshotRevision must be a nonempty string.");
                    revision = text;
                }
                if ((offset > 0 || deprecatedOffset > 0) && revision == null)
                    throw new ArgumentException("Continuation pages require the preceding snapshotRevision.");
                query = new VmCompilationDiagnosticQuery(count, offset, deprecatedOffset,
                    severity, revision);
                error = null;
                return true;
            }
            catch (ArgumentException exception)
            {
                query = null;
                error = VmAutomationResponse.Error(exception.Message, "invalid_arguments");
                return false;
            }
        }

        private static int ReadInteger(Dictionary<string, object> arguments,
            string name, int defaultValue, int minimum, int maximum)
        {
            if (!arguments.TryGetValue(name, out object value))
                return defaultValue;
            if (value is not (sbyte or byte or short or ushort or int or uint or long or ulong or
                float or double or decimal))
                throw new ArgumentException($"{name} must be an integer in [{minimum}, {maximum}].");
            if (value is double doubleValue &&
                (double.IsNaN(doubleValue) || double.IsInfinity(doubleValue) ||
                 Math.Truncate(doubleValue) != doubleValue) ||
                value is float floatValue &&
                (float.IsNaN(floatValue) || float.IsInfinity(floatValue) ||
                 Math.Truncate(floatValue) != floatValue))
                throw new ArgumentException($"{name} must be an integer in [{minimum}, {maximum}].");
            decimal number;
            try { number = Convert.ToDecimal(value, CultureInfo.InvariantCulture); }
            catch (OverflowException)
            {
                throw new ArgumentException($"{name} must be an integer in [{minimum}, {maximum}].");
            }
            if (number < minimum || number > maximum || decimal.Truncate(number) != number)
                throw new ArgumentException($"{name} must be an integer in [{minimum}, {maximum}].");
            return (int)number;
        }
    }
}
