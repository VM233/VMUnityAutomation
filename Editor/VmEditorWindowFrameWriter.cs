using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace VMUnityAutomation.Editor
{
    internal sealed class VmEditorWindowFrameWriter : IDisposable
    {
        private sealed class Work
        {
            internal VmEditorViewFrame Image;
            internal VmEditorWindowRecordingFrame Frame;
            internal string Path;
            internal Task Task;
        }
        private readonly List<Work> work = new List<Work>(4);
        private readonly Func<byte[], int, int, byte[]> encode;
        private readonly Action<string, byte[]> write;
        private readonly Func<double> seconds;
        private CancellationTokenSource lifetime = new CancellationTokenSource();
        private bool accepting = true;
        internal int PendingCount => work.Count;
        internal bool Full => work.Count == 4;
        internal CancellationToken Token { get; }
        internal double MaximumEncodingMilliseconds { get; private set; }

        internal VmEditorWindowFrameWriter(Func<byte[], int, int, byte[]> encode, Func<double> seconds,
            Action<string, byte[]> write = null)
        { this.encode = encode; this.seconds = seconds; this.write = write ?? File.WriteAllBytes; Token = lifetime.Token; }

        internal void Add(VmEditorViewFrame image, VmEditorWindowRecordingFrame frame, string path)
        {
            if (!accepting || lifetime == null) throw new InvalidOperationException("The frame writer has retired admission.");
            if (Full) throw new InvalidOperationException("Native recording exceeded four pending PNG frame buffers.");
            work.Add(new Work { Image = image, Frame = frame, Path = path });
            Pump();
        }

        internal void CompleteAdding() { accepting = false; Pump(); }

        internal void Pump()
        {
            foreach (var item in work.Where(item => item.Task != null && item.Task.IsCompleted).ToArray())
            {
                item.Task.GetAwaiter().GetResult();
                MaximumEncodingMilliseconds = Math.Max(MaximumEncodingMilliseconds, item.Frame.EncodingMilliseconds);
                work.Remove(item);
            }
            int active = work.Count(item => item.Task != null);
            foreach (var item in work.Where(item => item.Task == null))
            {
                if (active == 2) break;
                var token = lifetime.Token;
                item.Task = Task.Run(() =>
                {
                    try
                    {
                        token.ThrowIfCancellationRequested();
                        var timer = Stopwatch.StartNew();
                        byte[] png = encode(item.Image.Pixels, item.Image.Width, item.Image.Height);
                        item.Frame.EncodingMilliseconds = timer.Elapsed.TotalMilliseconds;
                        if (png.Length > 64 * 1024 * 1024) throw new InvalidOperationException("A recorded PNG exceeds 64 MiB.");
                        token.ThrowIfCancellationRequested();
                        write(item.Path, png);
                        item.Frame.CaptureCompletedSeconds = seconds();
                    }
                    finally { item.Image = null; }
                });
                active++;
            }
        }

        public void Dispose()
        {
            var owned = lifetime; lifetime = null;
            if (owned == null) return;
            accepting = false;
            owned.Cancel();
            var errors = new List<Exception>();
            foreach (var item in work)
            {
                try { item.Task?.GetAwaiter().GetResult(); }
                catch (OperationCanceledException error) when (error.CancellationToken == owned.Token && owned.IsCancellationRequested) { }
                catch (Exception error) { errors.Add(error); }
                item.Image = null;
            }
            work.Clear(); owned.Dispose();
            if (errors.Count > 0) throw new AggregateException("Recorded PNG workers failed during retirement.", errors);
        }
    }
}
