using UnityEditor;
using UnityEngine;

namespace VMUnityAutomation.Editor.Tests
{
    [FilePath(SettingsPath, FilePathAttribute.Location.ProjectFolder)]
    public sealed class VmSerializedSingletonTestSettings : ScriptableSingleton<VmSerializedSingletonTestSettings>
    {
        internal const string SettingsPath = "ProjectSettings/VMUnityAutomationSerializedSingletonTest.asset";

        [SerializeField] private string resourceFolder = "Assets/Original";

        internal string ResourceFolder => resourceFolder;
    }
}
