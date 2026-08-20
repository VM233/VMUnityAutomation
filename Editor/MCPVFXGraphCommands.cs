using System.Collections.Generic;

namespace VMUnityAutomation.Editor
{
    /// <summary>
    /// Canonical route facade. Domain-specific owners implement the catalog,
    /// lifecycle, graph, component, settings and bake contracts.
    /// </summary>
    internal static class MCPVFXGraphCommands
    {
        public static object Catalog(Dictionary<string, object> args)
        {
            if (!MCPVFXReflection.IsAvailable)
                return CapabilityUnavailable();
            return MCPVFXGraphCatalogCommands.Catalog(args);
        }

        public static object Create(Dictionary<string, object> args)
        {
            if (!MCPVFXReflection.IsAvailable)
                return CapabilityUnavailable();
            return MCPVFXGraphCreateCommands.Create(args);
        }

        public static object Info(Dictionary<string, object> args)
        {
            if (!MCPVFXReflection.IsAvailable)
                return CapabilityUnavailable();
            return MCPVFXGraphInfoCommands.Info(args);
        }

        public static object Transaction(Dictionary<string, object> args)
        {
            if (!MCPVFXReflection.IsAvailable)
                return CapabilityUnavailable();
            return MCPVFXGraphTransactionCommands.Transaction(args);
        }

        public static object Validate(Dictionary<string, object> args)
        {
            if (!MCPVFXReflection.IsAvailable)
                return CapabilityUnavailable();
            return MCPVFXGraphValidateCommands.Validate(args);
        }

        public static object ComponentInfo(Dictionary<string, object> args)
        {
            if (!MCPVFXReflection.IsAvailable)
                return CapabilityUnavailable();
            return MCPVFXComponentCommands.Info(args);
        }

        public static object ComponentTransaction(Dictionary<string, object> args)
        {
            if (!MCPVFXReflection.IsAvailable)
                return CapabilityUnavailable();
            return MCPVFXComponentCommands.Transaction(args);
        }

        public static object ComponentControl(Dictionary<string, object> args)
        {
            if (!MCPVFXReflection.IsAvailable)
                return CapabilityUnavailable();
            return MCPVFXComponentCommands.Control(args);
        }

        public static object SettingsInfo(Dictionary<string, object> args)
        {
            if (!MCPVFXReflection.IsAvailable)
                return CapabilityUnavailable();
            return MCPVFXSettingsCommands.Info(args);
        }

        public static object SettingsTransaction(Dictionary<string, object> args)
        {
            if (!MCPVFXReflection.IsAvailable)
                return CapabilityUnavailable();
            return MCPVFXSettingsCommands.Transaction(args);
        }

        public static object Bake(Dictionary<string, object> args)
        {
            if (!MCPVFXReflection.IsAvailable)
                return CapabilityUnavailable();
            return MCPVFXBakeCommands.Bake(args);
        }

        private static object CapabilityUnavailable()
        {
            string missing = string.Join(", ",
                MCPVFXReflection.MissingRequiredTypeNames());
            return MCPResponse.Error(
                "VFX Graph is not available. Install com.unity.visualeffectgraph." +
                (string.IsNullOrEmpty(missing) ? "" :
                    " Missing required types: " + missing + "."),
                "capability_unavailable");
        }
    }
}
