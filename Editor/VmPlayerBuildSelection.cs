using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Compilation;

namespace VMUnityAutomation.Editor
{
    internal sealed class VmPlayerBuildSelection : IDisposable
    {
        private readonly BuildTargetGroup beforeGroup;
        private readonly BuildTarget beforeStandalone;
        private readonly bool beforeDevelopment;
        private readonly StandaloneBuildSubtarget beforeSubtarget;
        private readonly Action restoreCompilation;
        private readonly bool changesSubtarget;
        private bool buildInvoked;
        private bool disposed;
        internal Dictionary<string, object> Receipt { get; }

        internal static int ReadSubtarget(BuildTarget target, IDictionary<string, object> args)
        {
            bool standalone = BuildPipeline.GetBuildTargetGroup(target) == BuildTargetGroup.Standalone;
            if (!args.TryGetValue("standaloneSubtarget", out var value))
                return standalone ? (int)StandaloneBuildSubtarget.Player : 0;
            if (!standalone)
                throw new ArgumentException("standaloneSubtarget requires a Standalone build target.");
            return (value as string) switch
            {
                "Player" => (int)StandaloneBuildSubtarget.Player,
                "Server" => (int)StandaloneBuildSubtarget.Server,
                _ => throw new ArgumentException("standaloneSubtarget must be Player or Server."),
            };
        }

        internal VmPlayerBuildSelection(BuildTarget target, bool developmentBuild, int subtarget = (int)StandaloneBuildSubtarget.Player)
            : this(target, developmentBuild, subtarget, () => CompilationPipeline.RequestScriptCompilation()) { }

        internal VmPlayerBuildSelection(BuildTarget target, bool developmentBuild, int subtarget, Action restoreCompilation)
        {
            this.restoreCompilation = restoreCompilation;
            beforeGroup = EditorUserBuildSettings.selectedBuildTargetGroup;
            beforeDevelopment = EditorUserBuildSettings.development;
            beforeStandalone = EditorUserBuildSettings.selectedStandaloneTarget;
            beforeSubtarget = EditorUserBuildSettings.standaloneBuildSubtarget;
            var group = BuildPipeline.GetBuildTargetGroup(target);
            changesSubtarget = group == BuildTargetGroup.Standalone && beforeSubtarget != (StandaloneBuildSubtarget)subtarget;
            Receipt = new Dictionary<string, object>
            {
                { "selectedGroupBefore", beforeGroup.ToString() },
                { "standaloneTargetBefore", beforeStandalone.ToString() },
                { "requestedTarget", target.ToString() },
                { "developmentBefore", beforeDevelopment },
                { "requestedDevelopment", developmentBuild },
                { "requestedGroup", group.ToString() },
                { "standaloneSubtargetBefore", beforeSubtarget.ToString() },
                { "requestedStandaloneSubtarget", group == BuildTargetGroup.Standalone ? ((StandaloneBuildSubtarget)subtarget).ToString() : null },
                { "selectedGroupBackendBefore", PlayerSettings.GetScriptingBackend(NamedBuildTarget.FromBuildTargetGroup(
                    beforeGroup == BuildTargetGroup.Unknown ? BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget) : beforeGroup)).ToString() },
                { "requestedGroupBackend", PlayerSettings.GetScriptingBackend(NamedBuildTarget.FromBuildTargetGroup(group)).ToString() },
                { "restored", false },
                { "editorCompilationRestoreRequested", false },
            };
            try
            {
                EditorUserBuildSettings.selectedBuildTargetGroup = group;
                EditorUserBuildSettings.development = developmentBuild;
                Receipt["developmentDuringBuild"] = EditorUserBuildSettings.development;
                if (group == BuildTargetGroup.Standalone)
                {
                    EditorUserBuildSettings.selectedStandaloneTarget = target;
                    EditorUserBuildSettings.standaloneBuildSubtarget = (StandaloneBuildSubtarget)subtarget;
                }
                Receipt["standaloneSubtargetDuringBuild"] = EditorUserBuildSettings.standaloneBuildSubtarget.ToString();
                Receipt["selectedGroupDuringBuild"] = EditorUserBuildSettings.selectedBuildTargetGroup.ToString();
                Receipt["standaloneTargetDuringBuild"] = EditorUserBuildSettings.selectedStandaloneTarget.ToString();
            }
            catch { Dispose(); throw; }
        }

        internal void MarkBuildInvoked() => buildInvoked = true;

        public void Dispose()
        {
            if (disposed) return;
            EditorUserBuildSettings.development = beforeDevelopment;
            Receipt["developmentAfter"] = EditorUserBuildSettings.development;
            EditorUserBuildSettings.selectedStandaloneTarget = beforeStandalone;
            EditorUserBuildSettings.standaloneBuildSubtarget = beforeSubtarget;
            EditorUserBuildSettings.selectedBuildTargetGroup = beforeGroup;
            Receipt["selectedGroupAfter"] = EditorUserBuildSettings.selectedBuildTargetGroup.ToString();
            Receipt["standaloneTargetAfter"] = EditorUserBuildSettings.selectedStandaloneTarget.ToString();
            Receipt["standaloneSubtargetAfter"] = EditorUserBuildSettings.standaloneBuildSubtarget.ToString();
            Receipt["restored"] = true;
            disposed = true;
            if (buildInvoked && changesSubtarget)
            {
                restoreCompilation();
                Receipt["editorCompilationRestoreRequested"] = true;
            }
        }
    }
}
