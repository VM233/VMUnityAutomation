using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditor;

namespace VMUnityAutomation.Editor
{
    internal static class VmAutomationPlayModeJobRunner
    {
        internal const string JobType = "play-mode-transition";
        internal const string Operation = "editor/play-mode";
        internal const string WaitingForTargetPhase =
            "waiting-for-play-mode";

        private const string SucceededStatus = "succeeded";
        private const string FailedStatus = "failed";

        internal static bool RequiresDurableTransition(string action)
        {
            return string.Equals(action, "play",
                       StringComparison.Ordinal) ||
                   string.Equals(action, "stop",
                       StringComparison.Ordinal);
        }

        internal static bool IsStopRequest(
            IReadOnlyDictionary<string, object> arguments)
        {
            return string.Equals(
                GetString(arguments, "action", "play")
                    .Trim().ToLowerInvariant(),
                "stop",
                StringComparison.Ordinal);
        }

        internal static object Start(Dictionary<string, object> arguments)
        {
            var normalized = arguments == null
                ? new Dictionary<string, object>()
                : new Dictionary<string, object>(arguments);
            string action = GetString(normalized, "action", "play")
                .Trim().ToLowerInvariant();
            if (RequiresDurableTransition(action) == false)
            {
                return VmAutomationResponse.Error(
                    $"Action '{action}' is not a durable Play Mode " +
                    "transition.",
                    "invalid_play_mode_action");
            }

            normalized["action"] = action;
            normalized["timeoutMs"] = Math.Max(
                100, GetInt(normalized, "timeoutMs", 10000));
            normalized["stableFrames"] = Math.Max(
                1, GetInt(normalized, "stableFrames", 2));
            SupersedeOpposingTransitions(action);
            return VmAutomationWorkspaceJobRunner
                .StartPlayModeTransition(normalized);
        }

        internal static bool ExecutePhase(VmAutomationWorkspaceJob job)
        {
            if (job == null || job.JobType != JobType)
            {
                return false;
            }

            switch (job.Phase)
            {
                case VmAutomationWorkspaceJobRunner
                    .WaitingForEditorPhase:
                    BeginTransition(job);
                    return true;
                case WaitingForTargetPhase:
                    ObserveTransition(job);
                    return true;
                default:
                    throw new InvalidOperationException(
                        $"Play Mode job '{job.JobId}' has unknown " +
                        $"phase '{job.Phase}'.");
            }
        }

        internal static void RecoverAfterReload(
            VmAutomationWorkspaceJob job)
        {
            if (job == null || job.JobType != JobType)
            {
                return;
            }

            if (job.TransactionState == null)
            {
                if (job.Phase == VmAutomationWorkspaceJobRunner
                        .WaitingForEditorPhase)
                {
                    job.StatusMessage =
                        "Recovered before the Play Mode transition was " +
                        "requested; the durable job remains queued.";
                    VmAutomationWorkspaceJobRunner.Persist(job);
                    return;
                }

                Fail(job, VmAutomationResponse.Error(
                    "The durable Play Mode transition reloaded without " +
                    "its persisted transition state.",
                    "play_mode_transition_state_missing",
                    false));
                return;
            }

            job.TransactionState["confirmedFrames"] = 0;
            job.StatusMessage =
                "Recovered after Domain Reload; confirming the requested " +
                "Play Mode state.";
            VmAutomationWorkspaceJobRunner.Persist(job);
        }

        internal static bool HasCrossedMutationBoundary(
            VmAutomationWorkspaceJob job)
        {
            return job?.TransactionState != null &&
                   GetBool(job.TransactionState,
                       "transitionRequested");
        }

        private static void BeginTransition(
            VmAutomationWorkspaceJob job)
        {
            string action = GetString(job.Request, "action", "play");
            bool targetPlaying = action == "play";
            job.TransactionState = new Dictionary<string, object>
            {
                { "initiallyPlaying", EditorApplication.isPlaying },
                { "initiallyPaused", EditorApplication.isPaused },
                { "startedAt", DateTime.UtcNow.ToString("O") },
                { "confirmedFrames", 0 },
                { "transitionRequested", true },
            };
            job.Phase = WaitingForTargetPhase;
            job.StatusMessage = targetPlaying
                ? "Requested Play Mode; waiting for stable confirmation."
                : "Requested Edit Mode; waiting for stable confirmation.";

            // Persist the continuation before changing Play Mode because the
            // following assignment may unload this assembly domain.
            VmAutomationWorkspaceJobRunner.Persist(job);
            ApplyTarget(targetPlaying);
        }

        private static void SupersedeOpposingTransitions(
            string requestedAction)
        {
            foreach (VmAutomationWorkspaceJob job in
                     VmAutomationWorkspaceJobStore.GetAll())
            {
                if (job.IsTerminal || job.JobType != JobType ||
                    string.Equals(
                        GetString(job.Request, "action", "play"),
                        requestedAction,
                        StringComparison.Ordinal))
                {
                    continue;
                }

                job.Status = "canceled";
                job.Phase = "canceled";
                job.StatusMessage =
                    $"Superseded by a newer '{requestedAction}' Play Mode " +
                    "transition.";
                job.CompletedAt = DateTime.UtcNow;
                VmAutomationWorkspaceJobRunner.Persist(job);
            }
        }

        private static void ObserveTransition(
            VmAutomationWorkspaceJob job)
        {
            string action = GetString(job.Request, "action", "play");
            bool targetPlaying = action == "play";
            ApplyTarget(targetPlaying);

            bool targetReached = IsTargetReached(targetPlaying);
            int confirmedFrames = targetReached
                ? GetInt(job.TransactionState,
                    "confirmedFrames", 0) + 1
                : 0;
            job.TransactionState["confirmedFrames"] =
                confirmedFrames;

            int stableFrames = GetInt(
                job.Request, "stableFrames", 2);
            double elapsedMs = GetElapsedMilliseconds(job);
            if (confirmedFrames >= stableFrames)
            {
                bool initiallyPlaying = GetBool(
                    job.TransactionState, "initiallyPlaying");
                bool initiallyPaused = GetBool(
                    job.TransactionState, "initiallyPaused");
                job.Status = SucceededStatus;
                job.Phase = SucceededStatus;
                job.StatusMessage =
                    "The requested Play Mode state was confirmed.";
                job.Result = new Dictionary<string, object>
                {
                    { "action", action },
                    { "stateConfirmed", true },
                    { "isPlaying", EditorApplication.isPlaying },
                    { "isPaused", EditorApplication.isPaused },
                    {
                        "changed",
                        initiallyPlaying != EditorApplication.isPlaying ||
                        initiallyPaused != EditorApplication.isPaused
                    },
                    { "stableFrames", confirmedFrames },
                    { "elapsedMs", Math.Round(elapsedMs, 1) },
                };
                job.CompletedAt = DateTime.UtcNow;
                VmAutomationWorkspaceJobRunner.Persist(job);
                return;
            }

            int timeoutMs = GetInt(job.Request, "timeoutMs", 10000);
            if (!targetReached && elapsedMs >= timeoutMs)
            {
                Fail(job, VmAutomationResponse.Error(
                    $"Unity did not reach the requested Play Mode state " +
                    $"for '{action}' within {timeoutMs} ms.",
                    "play_mode_state_timeout",
                    true,
                    new Dictionary<string, object>
                    {
                        { "action", action },
                        { "targetPlaying", targetPlaying },
                        { "isPlaying", EditorApplication.isPlaying },
                        { "isPaused", EditorApplication.isPaused },
                        {
                            "isPlayingOrWillChangePlaymode",
                            EditorApplication
                                .isPlayingOrWillChangePlaymode
                        },
                    }));
                return;
            }

            VmAutomationWorkspaceJobRunner.Persist(job);
        }

        private static void ApplyTarget(bool targetPlaying)
        {
            if (targetPlaying)
            {
                if (EditorApplication.isPlaying == false &&
                    EditorApplication
                        .isPlayingOrWillChangePlaymode == false)
                {
                    EditorApplication.isPlaying = true;
                }

                return;
            }

            if (EditorApplication.isPlaying ||
                EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.isPlaying = false;
            }
        }

        private static bool IsTargetReached(bool targetPlaying)
        {
            bool isChangingPlayMode =
                EditorApplication.isPlayingOrWillChangePlaymode !=
                EditorApplication.isPlaying;
            return targetPlaying
                ? EditorApplication.isPlaying &&
                  isChangingPlayMode == false
                : EditorApplication.isPlaying == false &&
                  EditorApplication
                      .isPlayingOrWillChangePlaymode == false;
        }

        private static double GetElapsedMilliseconds(
            VmAutomationWorkspaceJob job)
        {
            string startedAt = GetString(
                job.TransactionState, "startedAt", "");
            return DateTime.TryParse(
                startedAt,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out DateTime parsed)
                ? Math.Max(0d,
                    (DateTime.UtcNow - parsed.ToUniversalTime())
                    .TotalMilliseconds)
                : double.MaxValue;
        }

        private static void Fail(
            VmAutomationWorkspaceJob job,
            object error)
        {
            job.Status = FailedStatus;
            job.Phase = FailedStatus;
            job.StatusMessage = "Play Mode transition failed.";
            job.Error = VmAutomationResponse.ToDictionary(error) ??
                        VmAutomationResponse.Error(
                            "Play Mode transition failed.",
                            "play_mode_transition_failed",
                            false);
            job.CompletedAt = DateTime.UtcNow;
            VmAutomationWorkspaceJobRunner.Persist(job);
        }

        private static string GetString(
            IReadOnlyDictionary<string, object> values,
            string key,
            string defaultValue)
        {
            return values != null &&
                   values.TryGetValue(key, out object value) &&
                   value != null
                ? value.ToString()
                : defaultValue;
        }

        private static int GetInt(
            IReadOnlyDictionary<string, object> values,
            string key,
            int defaultValue)
        {
            return int.TryParse(
                GetString(values, key, ""),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int result)
                ? result
                : defaultValue;
        }

        private static bool GetBool(
            IReadOnlyDictionary<string, object> values,
            string key)
        {
            if (values == null ||
                values.TryGetValue(key, out object value) == false ||
                value == null)
            {
                return false;
            }

            return value is bool result
                ? result
                : bool.TryParse(value.ToString(), out result) &&
                  result;
        }
    }
}
