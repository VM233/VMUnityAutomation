using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace VMUnityAutomation.Editor
{
    internal static class VmAutomationEditorFrameStepper
    {
        internal const string WaitingPhase = "waiting-for-native-frames";

        internal static void Begin(VmAutomationWorkspaceJob job)
        {
            if (!StablePlayMode())
            {
                Fail(job, "play_mode_required", "Frame stepping requires stable Play Mode.");
                return;
            }
            job.TransactionState = new Dictionary<string, object>
            {
                { "frameBefore", Time.frameCount },
                { "lastRequestedAtFrame", Time.frameCount - 1 },
                { "stepsIssued", 0 },
                { "confirmedFrames", 0 },
                { "initiallyPaused", EditorApplication.isPaused },
                { "transitionRequested", true },
            };
            job.Phase = WaitingPhase;
            job.StatusMessage = "Paused; advancing the admitted native frame interval.";
            VmAutomationWorkspaceJobRunner.Persist(job);
            EditorApplication.isPaused = true;
        }

        internal static void Observe(VmAutomationWorkspaceJob job)
        {
            if (!StablePlayMode())
            {
                Fail(job, "play_mode_required", "Play Mode changed during frame stepping.");
                return;
            }
            int before = Convert.ToInt32(job.TransactionState["frameBefore"]);
            int advanced = Time.frameCount - before;
            int frames = Convert.ToInt32(job.Request["frames"]);
            if (advanced < 0 || advanced > frames)
            {
                Fail(job, "tool_execution_failed", "Native frame stepping exceeded or reset its admitted interval.");
                return;
            }
            if (advanced == frames && EditorApplication.isPaused)
            {
                int confirmations = Convert.ToInt32(job.TransactionState["confirmedFrames"]) + 1;
                job.TransactionState["confirmedFrames"] = confirmations;
                if (confirmations >= Convert.ToInt32(job.Request["stableFrames"]))
                {
                    var result = Evidence(job);
                    result["success"] = true;
                    result["stateConfirmed"] = true;
                    result["changed"] = !(bool)job.TransactionState["initiallyPaused"];
                    result["stableFrames"] = confirmations;
                    result["elapsedMs"] = Elapsed(job);
                    job.Result = result;
                    job.Status = "succeeded";
                    job.Phase = "succeeded";
                    job.StatusMessage = "The exact native frame interval was confirmed.";
                    job.CompletedAt = DateTime.UtcNow;
                    VmAutomationWorkspaceJobRunner.Persist(job);
                    return;
                }
            }
            if (Elapsed(job) >= Convert.ToInt32(job.Request["timeoutMs"]))
            {
                Fail(job, "play_mode_step_timeout", "Unity did not finish the admitted native frame interval before its timeout.");
                return;
            }
            if (advanced < frames && EditorApplication.isPaused &&
                     Time.frameCount > Convert.ToInt32(job.TransactionState["lastRequestedAtFrame"]))
            {
                job.TransactionState["lastRequestedAtFrame"] = Time.frameCount;
                job.TransactionState["stepsIssued"] = Convert.ToInt32(job.TransactionState["stepsIssued"]) + 1;
                VmAutomationWorkspaceJobRunner.Persist(job);
                EditorApplication.Step();
            }
            VmAutomationWorkspaceJobRunner.Persist(job);
        }

        internal static void InterruptAfterReload(VmAutomationWorkspaceJob job) =>
            Fail(job, "play_mode_step_interrupted_by_reload", "Domain Reload invalidated the native frame interval.");

        private static bool StablePlayMode() => EditorApplication.isPlaying &&
            EditorApplication.isPlayingOrWillChangePlaymode == EditorApplication.isPlaying;

        private static double Elapsed(VmAutomationWorkspaceJob job) =>
            Math.Round((DateTime.UtcNow - job.StartedAt.Value).TotalMilliseconds, 1);

        private static Dictionary<string, object> Evidence(VmAutomationWorkspaceJob job)
        {
            var state = job.TransactionState;
            int before = Convert.ToInt32(state["frameBefore"]);
            return new Dictionary<string, object>
            {
                { "action", "step" }, { "isPlaying", EditorApplication.isPlaying },
                { "isPaused", EditorApplication.isPaused }, { "wasPaused", state["initiallyPaused"] },
                { "frameBefore", before }, { "frameAfter", Time.frameCount },
                { "frames", job.Request["frames"] }, { "framesAdvanced", Time.frameCount - before },
                { "stepsIssued", state["stepsIssued"] }, { "lastRequestedAtFrame", state["lastRequestedAtFrame"] },
            };
        }

        private static void Fail(VmAutomationWorkspaceJob job, string code, string message)
        {
            job.Status = "failed";
            job.Phase = "failed";
            job.StatusMessage = message;
            job.Error = VmAutomationResponse.Error(message, code, false,
                job.TransactionState == null ? null : Evidence(job));
            job.CompletedAt = DateTime.UtcNow;
            VmAutomationWorkspaceJobRunner.Persist(job);
        }
    }
}
