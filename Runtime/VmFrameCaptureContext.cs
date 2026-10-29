using System;

namespace VMUnityAutomation.Runtime
{
    public sealed class VmFrameCaptureContext
    {
        public string CaptureID { get; }
        public string LifetimeID { get; }
        public string BuildGUID { get; }
        public string UnityVersion { get; }
        public bool IsEditor { get; }
        public bool IsDevelopmentBuild { get; }
        public float TimeScale { get; }
        public int Width { get; }
        public int Height { get; }
        public long ClockFrequency { get; }
        public DateTime StartedAtUtc { get; }

        public VmFrameCaptureContext(string captureID, string lifetimeID, string buildGUID,
            string unityVersion, bool isEditor, bool isDevelopmentBuild, float timeScale,
            int width, int height, long clockFrequency, DateTime startedAtUtc)
        {
            if (!Guid.TryParseExact(captureID, "D", out var capture))
                throw new ArgumentException("captureID must be a UUID in D format.", nameof(captureID));
            if (!Guid.TryParseExact(lifetimeID, "D", out var lifetime))
                throw new ArgumentException("lifetimeID must be a UUID in D format.", nameof(lifetimeID));
            if (buildGUID == null || buildGUID.Length > 128 || (!isEditor && buildGUID.Length == 0))
                throw new ArgumentException("Player buildGUID must have1 through128 characters; Editor may be empty.", nameof(buildGUID));
            if (string.IsNullOrEmpty(unityVersion) || unityVersion.Length > 128)
                throw new ArgumentException("unityVersion must have1 through128 characters.", nameof(unityVersion));
            if (float.IsNaN(timeScale) || float.IsInfinity(timeScale) || timeScale < 0)
                throw new ArgumentOutOfRangeException(nameof(timeScale));
            if (width < 1 || height < 1 || clockFrequency < 1 || startedAtUtc.Kind != DateTimeKind.Utc)
                throw new ArgumentException("Capture requires positive geometry/clock and a UTC start.");
            CaptureID = capture.ToString("D");
            LifetimeID = lifetime.ToString("D");
            BuildGUID = buildGUID;
            UnityVersion = unityVersion;
            IsEditor = isEditor;
            IsDevelopmentBuild = isDevelopmentBuild;
            TimeScale = timeScale;
            Width = width;
            Height = height;
            ClockFrequency = clockFrequency;
            StartedAtUtc = startedAtUtc;
        }
    }
}
