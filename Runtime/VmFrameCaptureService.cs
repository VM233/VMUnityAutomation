using System;
using System.Diagnostics;
using UnityEngine;

namespace VMUnityAutomation.Runtime
{
    [DisallowMultipleComponent]
    public sealed class VmFrameCaptureService : MonoBehaviour
    {
        private static VmFrameCaptureService instance;
        private string lifetimeID;
        private VmFrameCaptureSession session;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetLifetime() => instance = null;

        public static VmFrameCaptureResult Start(string captureID, int frameIntervals,
            string expectedBuildGUID)
        {
            if (!Application.isPlaying)
                return VmFrameCaptureResult.Rejected("frame_capture_requires_play", "A live Unity runtime is required.");
            if (!Guid.TryParseExact(captureID, "D", out var identity))
                return VmFrameCaptureResult.Rejected("frame_capture_invalid_identity", "capture_id must be a UUID in D format.");
            if (frameIntervals < 1 || frameIntervals > VmFrameCaptureSession.MaximumIntervals)
                return VmFrameCaptureResult.Rejected("frame_capture_invalid_budget", "frame_intervals must be from1 through120000.");
            if (expectedBuildGUID != Application.buildGUID)
                return VmFrameCaptureResult.Rejected("frame_capture_build_mismatch", "expected_build_guid differs from the actual runtime build.");
            string normalized = identity.ToString("D");
            if (instance != null && instance.session != null)
            {
                var current = instance.session;
                if (current.Context.CaptureID == normalized)
                    return current.RequestedIntervals == frameIntervals
                        ? current.Get() : VmFrameCaptureResult.Rejected("frame_capture_identity_conflict", "The admitted UUID has a different frame budget.");
                if (!current.IsTerminal)
                    return VmFrameCaptureResult.Rejected("frame_capture_active", "Cancel or complete the current capture before starting another.");
            }
            var context = new VmFrameCaptureContext(normalized,
                instance == null ? Guid.NewGuid().ToString("D") : instance.lifetimeID,
                Application.buildGUID, Application.unityVersion, Application.isEditor,
                UnityEngine.Debug.isDebugBuild, Time.timeScale, Screen.width, Screen.height,
                Stopwatch.Frequency, DateTime.UtcNow);
            if (instance == null)
            {
                var host = new GameObject("VM Runtime Frame Capture");
                instance = host.AddComponent<VmFrameCaptureService>();
                instance.lifetimeID = context.LifetimeID;
                DontDestroyOnLoad(host);
            }
            instance.session = new VmFrameCaptureSession(context, frameIntervals);
            instance.enabled = true;
            return instance.session.Get();
        }

        public static VmFrameCaptureResult Get(string captureID, string lifetimeID,
            bool includeHistogram = false)
        {
            var rejected = Identify(captureID, lifetimeID);
            return rejected ?? instance.session.Get(includeHistogram);
        }

        public static VmFrameCaptureResult Cancel(string captureID, string lifetimeID)
        {
            var rejected = Identify(captureID, lifetimeID);
            if (rejected != null) return rejected;
            instance.session.Cancel(DateTime.UtcNow);
            instance.enabled = false;
            return instance.session.Get();
        }

        private static VmFrameCaptureResult Identify(string captureID, string lifetimeID)
        {
            if (instance == null || instance.session == null)
                return VmFrameCaptureResult.Rejected("frame_capture_not_found", "No capture is owned by this runtime service.");
            if (instance.lifetimeID != lifetimeID || instance.session.Context.CaptureID != captureID)
                return VmFrameCaptureResult.Rejected("frame_capture_identity_mismatch", "Capture and lifetime must match the original admission receipt.");
            return null;
        }

        private void LateUpdate()
        {
            session.Observe(Time.frameCount, Stopwatch.GetTimestamp(), Time.timeScale,
                Screen.width, Screen.height, DateTime.UtcNow);
            if (session.IsTerminal) enabled = false;
        }

        private void OnApplicationQuit() => Interrupt("application-exit");

        private void OnDisable() => Interrupt("disabled");

        private void OnDestroy()
        {
            Interrupt("destroyed");
            if (instance == this) instance = null;
        }

        private void Interrupt(string reason)
        {
            if (session != null && !session.IsTerminal)
                session.Interrupt(reason, DateTime.UtcNow);
            enabled = false;
        }
    }
}
