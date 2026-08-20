using System;

namespace VMUnityAutomation.Editor
{
    [Flags]
    public enum VmProjectToolSideEffect
    {
        None = 0,
        ReadsProjectState = 1 << 0,
        WritesAssets = 1 << 1,
        WritesScene = 1 << 2,
        ChangesRuntimeState = 1 << 3,
        AdvancesEditorFrames = 1 << 4,
        AdvancesLogicTicks = 1 << 5,
        CreatesTemporaryObjects = 1 << 6,
        CapturesArtifacts = 1 << 7,
        PerformsExternalIO = 1 << 8,
        ReloadsDomain = 1 << 9,
        ExecutesArbitraryCode = 1 << 10,
        WritesProjectFiles = 1 << 11,
    }
}
