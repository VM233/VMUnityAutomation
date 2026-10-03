using UnityEditor;

namespace VMUnityAutomation.Editor.Tests
{
    [FilePath("VMUnityAutomationSerializedSingletonTest.asset", FilePathAttribute.Location.PreferencesFolder)]
    public sealed class VmSerializedSingletonPreferences : ScriptableSingleton<VmSerializedSingletonPreferences>
    {
    }
}
