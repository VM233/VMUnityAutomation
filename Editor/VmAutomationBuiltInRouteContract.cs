using System;
using System.Collections.Generic;

namespace VMUnityAutomation.Editor
{
    /// <summary>Metadata owned by an optional provider, alongside its handler and lifecycle.</summary>
    internal sealed class VmAutomationBuiltInRouteContract
    {
        internal VmAutomationBuiltInRouteContract(string route, string package, string description,
            VmAutomationToolProfile profile, Dictionary<string, object> inputSchema,
            Dictionary<string, object> outputSchema, IReadOnlyList<string> errorCodes)
        {
            if (string.IsNullOrWhiteSpace(route) || string.IsNullOrWhiteSpace(package) ||
                string.IsNullOrWhiteSpace(description))
                throw new ArgumentException("A provider must declare its route, package and description.");
            Route = route;
            Package = package;
            Description = description;
            Profile = profile ?? throw new ArgumentNullException(nameof(profile));
            InputSchema = inputSchema ?? throw new ArgumentNullException(nameof(inputSchema));
            OutputSchema = outputSchema ?? throw new ArgumentNullException(nameof(outputSchema));
            ErrorCodes = errorCodes ?? throw new ArgumentNullException(nameof(errorCodes));
        }

        internal string Route { get; }
        internal string Package { get; }
        internal string Description { get; }
        internal VmAutomationToolProfile Profile { get; }
        internal Dictionary<string, object> InputSchema { get; }
        internal Dictionary<string, object> OutputSchema { get; }
        internal IReadOnlyList<string> ErrorCodes { get; }
    }
}
