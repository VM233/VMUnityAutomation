using System;
using UnityEngine;

namespace VMUnityAutomation.Runtime
{
    // The listener filter is a passive DSP consumer. It never changes Unity's data.
    public sealed class VmAudioRecordingTap : MonoBehaviour
    {
        private readonly object gate = new object();
        private float[] samples;
        private int count, channels;
        private bool retired;
        private string failure;
        public void Begin(int capacity, int channelCount)
        {
            samples = new float[capacity]; channels = channelCount;
        }
        public bool Complete { get { lock (gate) return failure != null || count == samples.Length; } }
        public int Count { get { lock (gate) return count; } }
        private void OnAudioFilterRead(float[] data, int channelCount)
        {
            lock (gate)
            {
                if (retired || samples == null || failure != null || count == samples.Length) return;
                if (channels != channelCount) { failure = "Native DSP channel layout changed during recording."; return; }
                int size = Math.Min(data.Length, samples.Length - count);
                Array.Copy(data, 0, samples, count, size);
                count += size;
            }
        }
        public float[] Retire(out int size, out string error)
        {
            lock (gate)
            {
                retired = true;
                size = count; error = failure;
                var result = samples; samples = null;
                return result;
            }
        }
    }
}
