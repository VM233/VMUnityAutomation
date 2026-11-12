using System;
using System.Collections.Generic;

namespace VMUnityAutomation.Editor
{
    /// <summary>
    /// Canonical typed registration for one executable built-in route. Execution,
    /// schema, description, effect profile, and deferred lifecycle are resolved through
    /// this descriptor so route names cannot drift across independent runtime registries.
    /// </summary>
    internal sealed class VmAutomationBuiltInRouteDescriptor
    {
        internal delegate object ImmediateHandler(Dictionary<string, object> arguments);
        internal delegate void DeferredHandler(Dictionary<string, object> arguments,
            Action<object> resolve, Action<object> progress);

        private VmAutomationBuiltInRouteDescriptor(string route, ImmediateHandler immediate,
            DeferredHandler deferred, VmAutomationBuiltInRouteContract contract = null)
        {
            if (string.IsNullOrWhiteSpace(route))
                throw new ArgumentException("A built-in route requires a non-empty name.", nameof(route));
            if ((immediate == null) == (deferred == null))
                throw new ArgumentException(
                    $"Route '{route}' must register exactly one immediate or deferred handler.",
                    nameof(route));
            Route = route.Trim('/');
            if (contract != null && !string.Equals(contract.Route, Route, StringComparison.Ordinal))
                throw new ArgumentException("Provider contract and executable route must agree.", nameof(contract));
            Contract = contract;
            Immediate = immediate;
            Deferred = deferred;
        }

        internal string Route { get; }
        internal bool IsDeferred => Deferred != null;
        internal ImmediateHandler Immediate { get; }
        internal DeferredHandler Deferred { get; }
        internal VmAutomationBuiltInRouteContract Contract { get; }
        internal string Package => Contract == null ? "com.vm233.unity-automation" : Contract.Package;
        internal string Description => Contract == null ? VmAutomationToolDescriptionCatalog.Get(Route) : Contract.Description;
        internal VmAutomationToolProfile Profile => Contract == null ? VmAutomationToolProfileCatalog.Get(Route) : Contract.Profile;
        internal Dictionary<string, object> InputSchema => Contract == null ? VmAutomationToolInputSchemaCatalog.Get(Route) : Contract.InputSchema;

        internal Dictionary<string, object> OutputSchema
        {
            get
            {
                if (Contract != null)
                    return Contract.OutputSchema;
                if (VmAutomationGeneratedRouteContracts.TryGetOutput(Route, out var schema))
                    return schema;
                throw new InvalidOperationException(
                    $"Registered route '{Route}' does not declare an output contract.");
            }
        }

        internal static VmAutomationBuiltInRouteDescriptor CreateImmediate(
            string route, ImmediateHandler handler)
        {
            return new VmAutomationBuiltInRouteDescriptor(route, handler, null);
        }

        internal static VmAutomationBuiltInRouteDescriptor CreateDeferred(
            string route, DeferredHandler handler)
        {
            return new VmAutomationBuiltInRouteDescriptor(route, null, handler);
        }

        internal static VmAutomationBuiltInRouteDescriptor CreateImmediate(string route,
            VmAutomationBuiltInRouteContract contract, ImmediateHandler handler)
        {
            return new VmAutomationBuiltInRouteDescriptor(route, handler, null,
                contract ?? throw new ArgumentNullException(nameof(contract)));
        }

        internal static VmAutomationBuiltInRouteDescriptor CreateDeferred(string route,
            VmAutomationBuiltInRouteContract contract, DeferredHandler handler)
        {
            return new VmAutomationBuiltInRouteDescriptor(route, null, handler,
                contract ?? throw new ArgumentNullException(nameof(contract)));
        }
    }
}
