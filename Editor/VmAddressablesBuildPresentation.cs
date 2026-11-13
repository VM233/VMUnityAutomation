using System;
using System.Reflection;

namespace VMUnityAutomation.Editor
{
    internal sealed class VmAddressablesBuildPresentation : IDisposable
    {
        private const BindingFlags StaticProperties = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
        private readonly PropertyInfo informed;
        private readonly PropertyInfo autoOpen;
        private readonly bool informedBefore;
        private readonly bool autoOpenBefore;

        private VmAddressablesBuildPresentation(Type configType)
        {
            informed = ReportProperty(configType, "UserHasBeenInformedAboutBuildReportSettingPreBuild");
            autoOpen = ReportProperty(configType, "AutoOpenAddressablesReport");
            informedBefore = (bool)informed.GetValue(null);
            autoOpenBefore = (bool)autoOpen.GetValue(null);
        }

        internal static IDisposable BeginIfAvailable()
        {
            if (!VmAutomationCapabilityRegistry.IsCapabilityAvailable("addressables"))
                return null;
            Type builder = VmAutomationAssetGraphUtility.FindType(
                "UnityEditor.AddressableAssets.Build.DataBuilders.BuildScriptBase");
            // Older Addressables packages have no report presentation feature.
            if (builder.GetMethod("NotifyUserAboutBuildReport", BindingFlags.Instance | BindingFlags.NonPublic) == null)
                return null;
            return Begin(VmAutomationAssetGraphUtility.FindType(
                "UnityEditor.AddressableAssets.Settings.ProjectConfigData"));
        }

        internal static IDisposable Begin(Type configType)
        {
            var scope = new VmAddressablesBuildPresentation(configType);
            try
            {
                scope.informed.SetValue(null, true);
                scope.autoOpen.SetValue(null, false);
                return scope;
            }
            catch
            {
                scope.Dispose();
                throw;
            }
        }

        private static PropertyInfo ReportProperty(Type configType, string name)
        {
            PropertyInfo property = configType.GetProperty(name, StaticProperties)
                ?? throw new MissingMemberException(configType.FullName, name);
            if (property.PropertyType != typeof(bool) || !property.CanRead || !property.CanWrite)
                throw new InvalidOperationException($"Addressables report setting '{name}' must be a readable, writable Boolean.");
            return property;
        }

        public void Dispose()
        {
            try { informed.SetValue(null, informedBefore); }
            finally { autoOpen.SetValue(null, autoOpenBefore); }
        }
    }
}
