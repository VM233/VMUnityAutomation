using System;
using System.Collections.Generic;

namespace VMUnityAutomation.Editor
{
    /// <summary>
    /// Canonical typed registration for one executable built-in route. Execution,
    /// schema, description, effect profile, and deferred lifecycle are resolved through
    /// this descriptor so route names cannot drift across independent runtime registries.
    /// </summary>
    internal sealed class MCPBuiltInRouteDescriptor
    {
        internal delegate object ImmediateHandler(Dictionary<string, object> arguments);
        internal delegate void DeferredHandler(Dictionary<string, object> arguments,
            Action<object> resolve, Action<object> progress);

        private MCPBuiltInRouteDescriptor(string route, ImmediateHandler immediate,
            DeferredHandler deferred)
        {
            if (string.IsNullOrWhiteSpace(route))
                throw new ArgumentException("A built-in route requires a non-empty name.", nameof(route));
            if ((immediate == null) == (deferred == null))
                throw new ArgumentException(
                    $"Route '{route}' must register exactly one immediate or deferred handler.",
                    nameof(route));
            Route = route.Trim('/');
            Immediate = immediate;
            Deferred = deferred;
        }

        internal string Route { get; }
        internal bool IsDeferred => Deferred != null;
        internal ImmediateHandler Immediate { get; }
        internal DeferredHandler Deferred { get; }
        internal string Description => MCPToolDescriptionCatalog.Get(Route);
        internal MCPToolProfile Profile => MCPToolProfileCatalog.Get(Route);
        internal Dictionary<string, object> InputSchema => MCPToolInputSchemaCatalog.Get(Route);

        internal Dictionary<string, object> OutputSchema
        {
            get
            {
                if (MCPGeneratedRouteContracts.TryGetOutput(Route, out var schema))
                    return schema;
                throw new InvalidOperationException(
                    $"Registered route '{Route}' does not declare an output contract.");
            }
        }

        internal static MCPBuiltInRouteDescriptor CreateImmediate(
            string route, ImmediateHandler handler)
        {
            return new MCPBuiltInRouteDescriptor(route, handler, null);
        }

        internal static MCPBuiltInRouteDescriptor CreateDeferred(
            string route, DeferredHandler handler)
        {
            return new MCPBuiltInRouteDescriptor(route, null, handler);
        }
    }
}
