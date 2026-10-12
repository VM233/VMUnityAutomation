using System;
using UnityEditor;
using UnityEngine;

namespace VMUnityAutomation.Editor
{
    [InitializeOnLoad]
    public static class VmEditorBackgroundExecution
    {
        private static string sessionId;
        private static bool originalRunInBackground;
        private static int frameBefore;
        private static DateTime deadlineUtc;

        public static bool Active => sessionId != null;

        static VmEditorBackgroundExecution()
        {
            AssemblyReloadEvents.beforeAssemblyReload += Retire;
            EditorApplication.quitting += Retire;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        internal static VmEditorBackgroundSessionResult Execute(VmEditorBackgroundSessionRequest request)
        {
            if (request.Action == VmEditorBackgroundSessionAction.Begin)
            {
                if (!EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode != EditorApplication.isPlaying)
                    throw new VmProjectToolException("play_mode_required", "Background execution requires stable Play Mode.");
                if (Active)
                    throw new VmProjectToolException("background_session_active", "End the active background session before beginning another.");
                if (!string.IsNullOrEmpty(request.SessionId))
                    throw new VmProjectToolException("invalid_arguments", "Begin creates its own sessionId.");
                originalRunInBackground = Application.runInBackground;
                frameBefore = Time.frameCount;
                deadlineUtc = DateTime.UtcNow.AddSeconds(request.DurationSeconds ?? 600);
                Application.runInBackground = true;
                sessionId = Guid.NewGuid().ToString("N");
                EditorApplication.update += Tick;
                EditorApplication.QueuePlayerLoopUpdate();
                return Observe();
            }
            if (!Active || request.SessionId != sessionId)
                throw new VmProjectToolException("background_session_identity", "Inspect and End require the active sessionId capability.");
            if (request.DurationSeconds.HasValue)
                throw new VmProjectToolException("invalid_arguments", "Only Begin accepts durationSeconds.");
            if (request.Action == VmEditorBackgroundSessionAction.End) Retire();
            return Observe();
        }

        private static VmEditorBackgroundSessionResult Observe() => new()
        {
            Active = Active, SessionId = sessionId, OriginalRunInBackground = originalRunInBackground,
            RunInBackground = Application.runInBackground, ApplicationFocused = Application.isFocused,
            EditorActive = UnityEditorInternal.InternalEditorUtility.isApplicationActive,
            FrameBefore = frameBefore, Frame = Time.frameCount, DeadlineUtc = deadlineUtc.ToString("O")
        };

        private static void Tick()
        {
            if (DateTime.UtcNow >= deadlineUtc) Retire();
            else EditorApplication.QueuePlayerLoopUpdate();
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode) Retire();
        }

        private static void Retire()
        {
            if (!Active) return;
            Application.runInBackground = originalRunInBackground;
            EditorApplication.update -= Tick;
            sessionId = null;
        }
    }
}
