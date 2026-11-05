using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.TestTools;

namespace VMUnityAutomation.Editor.Tests
{
    public sealed class VmEditorWindowFrameWriterTests
    {
        private static VmEditorViewFrame Image() => new VmEditorViewFrame
        { Width = 2, Height = 2, Pixels = new byte[] { 255, 0, 0, 0, 255, 0, 0, 0, 255, 211, 89, 17 } };

        [UnityTest]
        public IEnumerator NativeArrayPngWorkerRetainsEveryPixelAndNativeTimestamp()
        {
            var clock = Stopwatch.StartNew();
            byte[] encoded = null;
            var image = Image(); var expected = (byte[])image.Pixels.Clone();
            var frame = new VmEditorWindowRecordingFrame { NativeCaptureCompletedSeconds = .125 };
            using (var writer = new VmEditorWindowFrameWriter((pixels, width, height) =>
                ImageConversion.EncodeArrayToPNG(pixels, GraphicsFormat.R8G8B8_UNorm, (uint)width, (uint)height),
                () => clock.Elapsed.TotalSeconds, (_, bytes) => encoded = bytes))
            {
                writer.Add(image, frame, "owned-frame"); writer.CompleteAdding();
                yield return Until(() => { writer.Pump(); return writer.PendingCount == 0; });
                Assert.That(frame.NativeCaptureCompletedSeconds, Is.EqualTo(.125));
                Assert.That(frame.CaptureCompletedSeconds, Is.GreaterThan(0));
                var decoded = new Texture2D(2, 2, TextureFormat.RGB24, false);
                try
                {
                    Assert.That(decoded.LoadImage(encoded), Is.True);
                    var pixels = decoded.GetPixels32();
                    for (int index = 0; index < pixels.Length; index++)
                    {
                        Assert.That(pixels[index].r, Is.EqualTo(expected[index * 3]));
                        Assert.That(pixels[index].g, Is.EqualTo(expected[index * 3 + 1]));
                        Assert.That(pixels[index].b, Is.EqualTo(expected[index * 3 + 2]));
                    }
                }
                finally { UnityEngine.Object.DestroyImmediate(decoded); }
            }
        }

        [UnityTest]
        public IEnumerator FourFramesAdmitOnlyTwoRunningWorkersAndNeverDropAnImage()
        {
            using (var gate = new ManualResetEventSlim())
            {
                int entered = 0, written = 0;
                using (var writer = new VmEditorWindowFrameWriter((pixels, width, height) =>
                { Interlocked.Increment(ref entered); gate.Wait(); return new byte[] { 1 }; }, () => 1,
                    (_, bytes) => Interlocked.Increment(ref written)))
                {
                    try
                    {
                        for (int i = 0; i < 4; i++) writer.Add(Image(), new VmEditorWindowRecordingFrame(), i.ToString());
                        yield return Until(() => Volatile.Read(ref entered) == 2);
                        Assert.That(writer.PendingCount, Is.EqualTo(4));
                        Assert.Throws<InvalidOperationException>(() => writer.Add(Image(), new VmEditorWindowRecordingFrame(), "overflow"));
                        writer.CompleteAdding();
                        Assert.Throws<InvalidOperationException>(() => writer.Add(Image(), new VmEditorWindowRecordingFrame(), "late"));
                        gate.Set();
                        yield return Until(() => { writer.Pump(); return writer.PendingCount == 0; });
                        Assert.That(written, Is.EqualTo(4)); Assert.That(entered, Is.EqualTo(4));
                    }
                    finally { gate.Set(); }
                }
            }
        }

        [UnityTest]
        public IEnumerator RetirementCancelsQueuedFramesAndJoinsTheExactIncompleteWorkers()
        {
            using (var gate = new ManualResetEventSlim())
            {
                int entered = 0, written = 0;
                var writer = new VmEditorWindowFrameWriter((pixels, width, height) =>
                { Interlocked.Increment(ref entered); gate.Wait(); return new byte[] { 1 }; }, () => 1,
                    (_, bytes) => Interlocked.Increment(ref written));
                Task retirement = null;
                try
                {
                    for (int i = 0; i < 4; i++) writer.Add(Image(), new VmEditorWindowRecordingFrame(), i.ToString());
                    yield return Until(() => Volatile.Read(ref entered) == 2);
                    var token = writer.Token;
                    retirement = Task.Run(writer.Dispose);
                    yield return Until(() => token.IsCancellationRequested);
                    Assert.That(retirement.IsCompleted, Is.False);
                    gate.Set();
                    yield return Until(() => retirement.IsCompleted);
                    retirement.GetAwaiter().GetResult();
                    Assert.That(entered, Is.EqualTo(2)); Assert.That(written, Is.Zero);
                    Assert.That(writer.PendingCount, Is.Zero);
                }
                finally { gate.Set(); retirement?.GetAwaiter().GetResult(); writer.Dispose(); }
            }
        }

        [UnityTest]
        public IEnumerator WorkerFailureRemainsVisibleAtPumpAndRetirement()
        {
            var writer = new VmEditorWindowFrameWriter((pixels, width, height) => throw new IOException("fixture-png-failure"), () => 1);
            try
            {
                writer.Add(Image(), new VmEditorWindowRecordingFrame(), "failed");
                Exception observed = null;
                yield return Until(() =>
                {
                    try { writer.Pump(); }
                    catch (IOException error) { observed = error; }
                    return observed != null;
                });
                Assert.That(observed.Message, Is.EqualTo("fixture-png-failure"));
                Assert.Throws<AggregateException>(writer.Dispose);
                Assert.That(writer.PendingCount, Is.Zero);
            }
            finally { writer.Dispose(); }
        }

        private static IEnumerator Until(Func<bool> complete)
        {
            var timer = Stopwatch.StartNew();
            while (!complete() && timer.Elapsed.TotalSeconds < 3) yield return null;
            Assert.That(complete(), Is.True);
        }
    }
}
