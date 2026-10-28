using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;

namespace VMUnityAutomation.Editor
{
    internal sealed class VmPlayerBuildSelection : IDisposable
    {
        private readonly BuildTargetGroup beforeGroup;
        private readonly BuildTarget beforeStandalone;
        private bool disposed;
        internal Dictionary<string, object> Receipt { get; }

        internal VmPlayerBuildSelection(BuildTarget target)
        {
            beforeGroup = EditorUserBuildSettings.selectedBuildTargetGroup;
            beforeStandalone = EditorUserBuildSettings.selectedStandaloneTarget;
            var group = BuildPipeline.GetBuildTargetGroup(target);
            Receipt = new Dictionary<string, object>
            {
                { "selectedGroupBefore", beforeGroup.ToString() },
                { "standaloneTargetBefore", beforeStandalone.ToString() },
                { "requestedTarget", target.ToString() },
                { "requestedGroup", group.ToString() },
                { "selectedGroupBackendBefore", PlayerSettings.GetScriptingBackend(NamedBuildTarget.FromBuildTargetGroup(
                    beforeGroup == BuildTargetGroup.Unknown ? BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget) : beforeGroup)).ToString() },
                { "requestedGroupBackend", PlayerSettings.GetScriptingBackend(NamedBuildTarget.FromBuildTargetGroup(group)).ToString() },
                { "restored", false },
            };
            try
            {
                EditorUserBuildSettings.selectedBuildTargetGroup = group;
                if (group == BuildTargetGroup.Standalone)
                    EditorUserBuildSettings.selectedStandaloneTarget = target;
                Receipt["selectedGroupDuringBuild"] = EditorUserBuildSettings.selectedBuildTargetGroup.ToString();
                Receipt["standaloneTargetDuringBuild"] = EditorUserBuildSettings.selectedStandaloneTarget.ToString();
            }
            catch { Dispose(); throw; }
        }

        public void Dispose()
        {
            if (disposed) return;
            EditorUserBuildSettings.selectedStandaloneTarget = beforeStandalone;
            EditorUserBuildSettings.selectedBuildTargetGroup = beforeGroup;
            Receipt["selectedGroupAfter"] = EditorUserBuildSettings.selectedBuildTargetGroup.ToString();
            Receipt["standaloneTargetAfter"] = EditorUserBuildSettings.selectedStandaloneTarget.ToString();
            Receipt["restored"] = true;
            disposed = true;
        }
    }
}
