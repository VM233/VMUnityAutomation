using System.Collections.Generic;

namespace VMUnityAutomation.Runtime
{
    public sealed class VmFrameCaptureResult
    {
        public bool Ok { get; }
        public string ErrorCode { get; }
        public string Message { get; }
        public VmFrameCaptureProgress Progress { get; }
        public VmFrameCaptureStatistics Statistics { get; }
        public IReadOnlyList<int> Histogram { get; }
        public double HistogramBinWidthMs => VmFrameCaptureSession.BinWidthMs;
        public double HistogramMaximumMs => VmFrameCaptureSession.MaximumBinnedMs;
        public string Clock => "Stopwatch between contiguous Unity LateUpdate frame boundaries";

        internal VmFrameCaptureResult(VmFrameCaptureProgress progress,
            VmFrameCaptureStatistics statistics, IReadOnlyList<int> histogram)
        {
            Progress = progress;
            Statistics = statistics;
            Histogram = histogram;
            Ok = progress.Status != "failed" && progress.Status != "interrupted";
            ErrorCode = progress.FailureCode;
        }

        private VmFrameCaptureResult(string errorCode, string message)
        {
            ErrorCode = errorCode;
            Message = message;
        }

        public static VmFrameCaptureResult Rejected(string errorCode, string message) =>
            new VmFrameCaptureResult(errorCode, message);
    }
}
