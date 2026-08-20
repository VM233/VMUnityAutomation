using System.Collections.Generic;

namespace VMUnityAutomation.Editor
{
    /// <summary>
    /// Canonical route facade. Domain-specific owners implement the catalog,
    /// lifecycle, graph, component, settings and bake contracts.
    /// </summary>
    internal static class VmAutomationVFXGraphCommands
    {
        public static object Catalog(Dictionary<string, object> args)
        {
            if (!VmAutomationVFXReflection.IsAvailable)
                return CapabilityUnavailable();
            return VmAutomationVFXGraphCatalogCommands.Catalog(args);
        }

        public static object Create(Dictionary<string, object> args)
        {
            if (!VmAutomationVFXReflection.IsAvailable)
                return CapabilityUnavailable();
            return VmAutomationVFXGraphCreateCommands.Create(args);
        }

        public static object Info(Dictionary<string, object> args)
        {
            if (!VmAutomationVFXReflection.IsAvailable)
                return CapabilityUnavailable();
            return VmAutomationVFXGraphInfoCommands.Info(args);
        }

        public static object Transaction(Dictionary<string, object> args)
        {
            if (!VmAutomationVFXReflection.IsAvailable)
                return CapabilityUnavailable();
            return VmAutomationVFXGraphTransactionCommands.Transaction(args);
        }

        public static object Validate(Dictionary<string, object> args)
        {
            if (!VmAutomationVFXReflection.IsAvailable)
                return CapabilityUnavailable();
            return VmAutomationVFXGraphValidateCommands.Validate(args);
        }

        public static object ComponentInfo(Dictionary<string, object> args)
        {
            if (!VmAutomationVFXReflection.IsAvailable)
                return CapabilityUnavailable();
            return VmAutomationVFXComponentCommands.Info(args);
        }

        public static object ComponentTransaction(Dictionary<string, object> args)
        {
            if (!VmAutomationVFXReflection.IsAvailable)
                return CapabilityUnavailable();
            return VmAutomationVFXComponentCommands.Transaction(args);
        }

        public static object ComponentControl(Dictionary<string, object> args)
        {
            if (!VmAutomationVFXReflection.IsAvailable)
                return CapabilityUnavailable();
            return VmAutomationVFXComponentCommands.Control(args);
        }

        public static object SettingsInfo(Dictionary<string, object> args)
        {
            if (!VmAutomationVFXReflection.IsAvailable)
                return CapabilityUnavailable();
            return VmAutomationVFXSettingsCommands.Info(args);
        }

        public static object SettingsTransaction(Dictionary<string, object> args)
        {
            if (!VmAutomationVFXReflection.IsAvailable)
                return CapabilityUnavailable();
            return VmAutomationVFXSettingsCommands.Transaction(args);
        }

        public static object Bake(Dictionary<string, object> args)
        {
            if (!VmAutomationVFXReflection.IsAvailable)
                return CapabilityUnavailable();
            return VmAutomationVFXBakeCommands.Bake(args);
        }

        private static object CapabilityUnavailable()
        {
            string missing = string.Join(", ",
                VmAutomationVFXReflection.MissingRequiredTypeNames());
            return VmAutomationResponse.Error(
                "VFX Graph is not available. Install com.unity.visualeffectgraph." +
                (string.IsNullOrEmpty(missing) ? "" :
                    " Missing required types: " + missing + "."),
                "capability_unavailable");
        }
    }
}
