#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build;

namespace VMUnityAutomation.Editor
{
    internal sealed class VmGameViewResolutionSessionBuildTargetChanged : IActiveBuildTargetChanged
    {
        public int callbackOrder => 0;

        public void OnActiveBuildTargetChanged(BuildTarget previousTarget, BuildTarget newTarget) =>
            VmGameViewResolutionSession.Retire();
    }
}
#endif
