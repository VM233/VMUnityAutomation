using UnityEditor;

namespace VMUnityAutomation.Editor
{
    [InitializeOnLoad]
    internal static class VmAutomationEditorProcess
    {
        // The native process role is immutable for this process lifetime.
        internal static bool OwnsAutomationState { get; } =
            !AssetDatabase.IsAssetImportWorkerProcess();
    }
}
