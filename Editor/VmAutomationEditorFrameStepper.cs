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
            bool advanceRunning = args.TryGetValue("action", out var action) &&
                                  action.ToString().Trim().Equals("advance", StringComparison.OrdinalIgnoreCase);
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
            EditorApplication.isPaused = !advanceRunning;

            Dictionary<string, object> Evidence() => new()
            {
                { "action", advanceRunning ? "advance" : "step" }, { "isPlaying", EditorApplication.isPlaying }, { "isPaused", EditorApplication.isPaused },
                { "wasPaused", wasPaused }, { "frameBefore", frameBefore }, { "frameAfter", Time.frameCount },
                { "frames", frames }, { "framesAdvanced", Time.frameCount - frameBefore },
            };
            void Complete(object result)
            {
                EditorApplication.update -= Tick;
                if (advanceRunning && EditorApplication.isPlaying)
                    EditorApplication.isPaused = true;
                resolve(result);
            }
            void Tick()
            {
                int advanced = Time.frameCount - frameBefore;
                double elapsedMs = (EditorApplication.timeSinceStartup - startedAt) * 1000d;
                if (!EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode != EditorApplication.isPlaying)
                {
                    Complete(VmAutomationResponse.Error("Play Mode changed during frame stepping.", "play_mode_required", false, Evidence()));
                    return;
                }
                if (!advanceRunning && advanced > frames)
                {
                    Complete(VmAutomationResponse.Error("Native frame stepping exceeded the requested interval.", "tool_execution_failed", false, Evidence()));
                    return;
                }
                if (advanceRunning && advanced >= frames)
                    EditorApplication.isPaused = true;
                if (advanced >= frames && EditorApplication.isPaused)
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
                else if (advanced < frames && Time.frameCount > requestedAtFrame)
                {
                    requestedAtFrame = Time.frameCount;
                    if (advanceRunning)
                        EditorApplication.QueuePlayerLoopUpdate();
                    else
                        EditorApplication.Step();
                }
                if (elapsedMs >= timeoutMs)
                    Complete(VmAutomationResponse.Error($"Unity did not complete {frames} native frame steps within {timeoutMs} ms.",
                        "play_mode_step_timeout", true, Evidence()));
            }
            EditorApplication.update += Tick;
            if (advanceRunning)
                EditorApplication.QueuePlayerLoopUpdate();
            else
                EditorApplication.Step();
        }
    }
}
