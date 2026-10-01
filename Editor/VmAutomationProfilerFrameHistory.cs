using System;
using System.Reflection;
using UnityEditorInternal;

namespace VMUnityAutomation.Editor
{
    internal static class VmAutomationProfilerFrameHistory
    {
        private static readonly Type SettingsType = typeof(ProfilerDriver).Assembly.GetType(
            "UnityEditor.Profiling.ProfilerUserSettings", true);
        private static readonly PropertyInfo CapacityProperty = SettingsType.GetProperty(
            "frameCount", BindingFlags.Public | BindingFlags.Static)
            ?? throw new MissingMemberException(SettingsType.FullName, "frameCount");
        internal static readonly int MaximumFrames = (int)(SettingsType.GetField(
            "kMaxFrameCount", BindingFlags.Public | BindingFlags.Static)
            ?? throw new MissingFieldException(SettingsType.FullName, "kMaxFrameCount")).GetRawConstantValue();

        internal static int FrameCount
        {
            get => (int)CapacityProperty.GetValue(null);
            set => CapacityProperty.SetValue(null, value);
        }
    }
}
