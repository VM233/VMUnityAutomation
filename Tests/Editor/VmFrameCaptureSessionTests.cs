using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using NUnit.Framework;
using VMUnityAutomation.Runtime;

namespace VMUnityAutomation.Editor.Tests
{
    public sealed class VmFrameCaptureSessionTests
    {
        private static readonly DateTime Utc = new DateTime(2026, 10, 29, 0, 0, 0, DateTimeKind.Utc);

        [Test]
        public void BaselineIsNotAnIntervalAndProgressDoesNotPublishStatisticsOrHistogram()
        {
            var capture = Create(3);
            capture.Observe(100, 5000, 1, 1280, 720, Utc);
            var result = capture.Get(true);
            Assert.That(result.Progress.ObservedIntervals, Is.Zero);
            Assert.That(result.Progress.BaselineFrame, Is.EqualTo(100));
            Assert.That(result.Progress.Terminal, Is.False);
            Assert.That(result.Statistics, Is.Null);
            Assert.That(result.Histogram, Is.Null);
        }

        [Test]
        public void CompletedIntervalsConserveNativeBoundsClockDurationAndHistogramCounts()
        {
            var capture = Create(3);
            capture.Observe(100, 5000, 1, 1280, 720, Utc);
            capture.Observe(101, 15000, 1, 1280, 720, Utc);
            capture.Observe(102, 35000, 1, 1280, 720, Utc);
            capture.Observe(103, 85000, 1, 1280, 720, Utc);
            var result = capture.Get(true);
            Assert.That(result.Ok, Is.True);
            Assert.That(result.Progress.Status, Is.EqualTo("completed"));
            Assert.That(result.Progress.Terminal, Is.True);
            Assert.That(result.Progress.LastFrame - result.Progress.BaselineFrame, Is.EqualTo(3));
            Assert.That(result.Statistics.SumMs, Is.EqualTo(80));
            Assert.That(result.Statistics.MeanMs, Is.EqualTo(80d / 3));
            Assert.That(result.Statistics.MaximumMs, Is.EqualTo(50));
            Assert.That(result.Histogram.Sum(), Is.EqualTo(3));
            Assert.That(result.Statistics.P90.LowerExclusiveMs, Is.LessThan(50));
            Assert.That(result.Statistics.P90.UpperInclusiveMs, Is.GreaterThanOrEqualTo(50));
        }

        [TestCase(102)]
        [TestCase(100)]
        [TestCase(99)]
        public void MissingDuplicateOrOutOfOrderFramesCannotComplete(int observedFrame)
        {
            var capture = Create(1);
            capture.Observe(100, 5000, 1, 1280, 720, Utc);
            capture.Observe(observedFrame, 15000, 1, 1280, 720, Utc);
            var result = capture.Get();
            Assert.That(result.Ok, Is.False);
            Assert.That(result.ErrorCode, Is.EqualTo("frame_capture_frame_discontinuity"));
            Assert.That(result.Progress.Status, Is.EqualTo("failed"));
            Assert.That(result.Progress.ObservedIntervals, Is.Zero);
            Assert.That(result.Statistics.MeanMs, Is.Null);
            Assert.That(result.Progress.RejectedSample.Frame, Is.EqualTo(observedFrame));
            Assert.That(result.Progress.LastFrame, Is.EqualTo(100));
        }

        [TestCase(5000)]
        [TestCase(4999)]
        public void NonIncreasingClockCannotCreateAZeroFrameTime(long timestamp)
        {
            var capture = Create(1);
            capture.Observe(100, 5000, 1, 1280, 720, Utc);
            capture.Observe(101, timestamp, 1, 1280, 720, Utc);
            Assert.That(capture.Get().ErrorCode, Is.EqualTo("frame_capture_clock_discontinuity"));
            Assert.That(capture.Get().Statistics.MaximumMs, Is.Null);
        }

        [TestCase(2, 1280, 720)]
        [TestCase(1, 1281, 720)]
        [TestCase(1, 1280, 721)]
        public void ChangedTimeScaleOrScreenGeometryRetainsAnIncompletePrefix(float timeScale, int width, int height)
        {
            var capture = Create(2);
            capture.Observe(100, 5000, 1, 1280, 720, Utc);
            capture.Observe(101, 15000, 1, 1280, 720, Utc);
            capture.Observe(102, 25000, timeScale, width, height, Utc);
            Assert.That(capture.Get().ErrorCode, Is.EqualTo("frame_capture_context_changed"));
            Assert.That(capture.Get().Progress.ObservedIntervals, Is.EqualTo(1));
            Assert.That(capture.Get().Statistics.SumMs, Is.EqualTo(10));
            Assert.That(capture.Get().Progress.RejectedSample.TimeScale, Is.EqualTo(timeScale));
            Assert.That(capture.Get().Progress.RejectedSample.Width, Is.EqualTo(width));
            Assert.That(capture.Get().Progress.RejectedSample.Height, Is.EqualTo(height));
        }

        [Test]
        public void LongStallAndOverflowRemainVisibleWithoutFiniteOverflowQuantiles()
        {
            var capture = Create(10);
            long timestamp = 5000;
            capture.Observe(100, timestamp, 1, 1280, 720, Utc);
            for (int i = 1; i <= 10; i++)
            {
                timestamp += i == 10 ? 12000000 : 16000;
                capture.Observe(100 + i, timestamp, 1, 1280, 720, Utc);
            }
            var result = capture.Get(true);
            Assert.That(result.Statistics.MaximumMs, Is.EqualTo(12000));
            Assert.That(result.Statistics.SumMs, Is.EqualTo(12144));
            Assert.That(result.Statistics.OverflowCount, Is.EqualTo(1));
            Assert.That(result.Histogram.Sum(), Is.EqualTo(9));
            Assert.That(result.Statistics.P90.UpperInclusiveMs, Is.GreaterThanOrEqualTo(16));
            Assert.That(result.Statistics.P99.LowerExclusiveMs, Is.EqualTo(10000));
            Assert.That(result.Statistics.P99.UpperInclusiveMs, Is.Null);
        }

        [Test]
        public void VeryLargeNativeCounterDoesNotLoseItsSmallIntegerInterval()
        {
            var capture = Create(1);
            long baseline = 9100000000000000;
            capture.Observe(100, baseline, 1, 1280, 720, Utc);
            capture.Observe(101, baseline + 1, 1, 1280, 720, Utc);
            Assert.That(capture.Get().Statistics.MaximumMs, Is.EqualTo(.001));
            Assert.That(capture.Get().Statistics.P90.LowerExclusiveMs, Is.LessThan(.001));
            Assert.That(capture.Get().Statistics.P90.UpperInclusiveMs, Is.GreaterThanOrEqualTo(.001));
        }

        [Test]
        public void QuantileUpperBoundContainsEveryFractionalMaximum()
        {
            var capture = Create(100);
            long timestamp = 1000;
            capture.Observe(0, timestamp, 1, 1280, 720, Utc);
            for (int i = 1; i <= 100; i++)
            {
                timestamp += 1000 + i;
                capture.Observe(i, timestamp, 1, 1280, 720, Utc);
            }
            var result = capture.Get();
            Assert.That(result.Statistics.P50.UpperInclusiveMs, Is.GreaterThanOrEqualTo(1.05));
            Assert.That(result.Statistics.P90.UpperInclusiveMs, Is.GreaterThanOrEqualTo(1.09));
            Assert.That(result.Statistics.P99.UpperInclusiveMs, Is.GreaterThanOrEqualTo(1.099));
        }

        [Test]
        public void CancelBeforeBaselineKeepsUnknownTimingsAndTheTerminalProductIsImmutable()
        {
            var capture = Create(10);
            capture.Cancel(Utc);
            var first = capture.Get(true);
            string json = JsonConvert.SerializeObject(first);
            capture.Cancel(Utc.AddHours(1));
            capture.Interrupt("disabled", Utc.AddHours(1));
            Assert.That(JsonConvert.SerializeObject(first), Is.EqualTo(json));
            Assert.That(JsonConvert.SerializeObject(capture.Get(true)), Is.EqualTo(json));
            Assert.That(first.Progress.Status, Is.EqualTo("canceled"));
            Assert.That(first.Statistics.MeanMs, Is.Null);
            Assert.That(first.Statistics.P90, Is.Null);
            Assert.Throws<NotSupportedException>(() => ((IList<int>)first.Histogram)[0] = 1);
            Assert.Throws<InvalidOperationException>(() => capture.Observe(1, 1, 1, 1280, 720, Utc));
        }

        [TestCase("application-exit")]
        [TestCase("disabled")]
        [TestCase("destroyed")]
        public void InterruptedLifecycleCannotReportCompletion(string reason)
        {
            var capture = Create(2);
            capture.Observe(100, 5000, 1, 1280, 720, Utc);
            capture.Observe(101, 15000, 1, 1280, 720, Utc);
            capture.Interrupt(reason, Utc);
            Assert.That(capture.Get().Ok, Is.False);
            Assert.That(capture.Get().Progress.Status, Is.EqualTo("interrupted"));
            Assert.That(capture.Get().Progress.ObservedIntervals, Is.EqualTo(1));
            Assert.That(capture.Get().Statistics.MaximumMs, Is.EqualTo(10));
        }

        [TestCase(0)]
        [TestCase(120001)]
        public void FrameBudgetIsRejectedBeforeObservation(int frameIntervals) =>
            Assert.Throws<ArgumentOutOfRangeException>(() => Create(frameIntervals));

        private static VmFrameCaptureSession Create(int frameIntervals) =>
            new VmFrameCaptureSession(new VmFrameCaptureContext(Guid.NewGuid().ToString("D"),
                Guid.NewGuid().ToString("D"), "actual-build", "6000.4", false, false, 1, 1280, 720, 1000000, Utc), frameIntervals);
    }
}
