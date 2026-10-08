using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace VMUnityAutomation.Editor
{
    internal static class VmAutomationEditorFrameStepper
    {
        internal static void Begin(Dictionary<string, object> args, Action<object> resolve)
        {
            if (!EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode != EditorApplication.isPlaying)
            {
                resolve(VmAutomationResponse.Error("Cannot step because Unity is not in stable Play Mode.", "play_mode_required"));
                return;
            }
            int frames = args.TryGetValue("frames", out var count) ? Convert.ToInt32(count) : 1;
            int timeoutMs = args.TryGetValue("timeoutMs", out var timeout) ? Convert.ToInt32(timeout) : 10000;
            int stableFrames = args.TryGetValue("stableFrames", out var stable) ? Convert.ToInt32(stable) : 1;
            if (frames < 1 || frames > 300 || timeoutMs < 100 || stableFrames < 1)
            {
                resolve(VmAutomationResponse.Error("Step requires frames in [1,300], timeoutMs >=100 and stableFrames >=1.", "invalid_arguments"));
                return;
            }
            int frameBefore = Time.frameCount;
            int requestedAtFrame = frameBefore;
            bool wasPaused = EditorApplication.isPaused;
            double startedAt = EditorApplication.timeSinceStartup;
            int confirmations = 0;
            int stepsIssued = 0;
            bool stepScheduled = false;
            EditorApplication.isPaused = true;

            Dictionary<string, object> Evidence(bool includeScheduling = false)
            {
                var result = new Dictionary<string, object>
                {
                    { "action", "step" }, { "isPlaying", EditorApplication.isPlaying }, { "isPaused", EditorApplication.isPaused },
                    { "wasPaused", wasPaused }, { "frameBefore", frameBefore }, { "frameAfter", Time.frameCount },
                    { "frames", frames }, { "framesAdvanced", Time.frameCount - frameBefore },
                };
                if (includeScheduling)
                {
                    result["stepsIssued"] = stepsIssued;
                    result["lastRequestedAtFrame"] = requestedAtFrame;
                    result["stepScheduled"] = stepScheduled;
                }
                return result;
            }
            void Complete(object result)
            {
                EditorApplication.update -= Tick;
                EditorApplication.delayCall -= IssueStep;
                stepScheduled = false;
                resolve(result);
            }
            void ScheduleStep()
            {
                stepScheduled = true;
                EditorApplication.delayCall += IssueStep;
            }
            void IssueStep()
            {
                stepScheduled = false;
                if (!EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode != EditorApplication.isPlaying)
                {
                    Complete(VmAutomationResponse.Error("Play Mode changed during frame stepping.", "play_mode_required", false, Evidence(true)));
                    return;
                }
                requestedAtFrame = Time.frameCount;
                stepsIssued++;
                EditorApplication.Step();
            }
            void Tick()
            {
                int advanced = Time.frameCount - frameBefore;
                double elapsedMs = (EditorApplication.timeSinceStartup - startedAt) * 1000d;
                if (!EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode != EditorApplication.isPlaying)
                {
                    Complete(VmAutomationResponse.Error("Play Mode changed during frame stepping.", "play_mode_required", false, Evidence(true)));
                    return;
                }
                if (advanced > frames)
                {
                    Complete(VmAutomationResponse.Error("Native frame stepping exceeded the requested interval.", "tool_execution_failed", false, Evidence(true)));
                    return;
                }
                if (advanced == frames && EditorApplication.isPaused)
                {
                    confirmations++;
                    if (confirmations >= stableFrames)
                    {
                        var result = Evidence();
                        result["success"] = true;
                        result["stateConfirmed"] = true;
                        result["changed"] = !wasPaused;
                        result["stableFrames"] = confirmations;
                        result["elapsedMs"] = Math.Round(elapsedMs, 1);
                        Complete(result);
                        return;
                    }
                }
                else if (advanced < frames && !stepScheduled && EditorApplication.isPaused && Time.frameCount > requestedAtFrame)
                {
                    ScheduleStep();
                }
                if (elapsedMs >= timeoutMs)
                    Complete(VmAutomationResponse.Error($"Unity did not complete {frames} native frame steps within {timeoutMs} ms.",
                        "play_mode_step_timeout", true, Evidence(true)));
            }
            EditorApplication.update += Tick;
            ScheduleStep();
        }
    }
}
