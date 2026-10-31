using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace VMUnityAutomation.Editor
{
    [InitializeOnLoad]
    public static class VmAudioRecording
    {
        private static VmAudioRecordingTap tap;
        private static VmAudioRecordingResult current;
        private static double deadline;
        static VmAudioRecording()
        {
            AssemblyReloadEvents.beforeAssemblyReload += Interrupt;
            EditorApplication.quitting += Interrupt;
            EditorApplication.playModeStateChanged += state =>
            { if (state == PlayModeStateChange.ExitingPlayMode) Interrupt(); };
        }
        public static VmAudioRecordingResult Execute(VmAudioRecordingRequest request)
        {
            if (request.Action == "Begin") return Begin(request);
            if (current == null || current.SessionId != request.SessionId)
                throw new VmProjectToolException("audio_recording_identity", "The native audio session identity does not match.");
            switch (request.Action)
            {
                case "Inspect": break;
                case "End": Finish(true, ""); break;
                case "Cancel": Finish(false, "Canceled by its capability holder."); break;
                default: throw new VmProjectToolException("audio_recording_arguments", "Action must be Begin, Inspect, End or Cancel.");
            }
            if (tap != null) current.SampleCount = tap.Count;
            return current;
        }
        private static VmAudioRecordingResult Begin(VmAudioRecordingRequest request)
        {
            if (current != null && !current.ResourcesReleased)
                throw new VmProjectToolException("audio_recording_active", "Retire the active native audio recording first.");
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new VmProjectToolException("audio_recording_context", "Audio recording requires stable Play Mode.");
            var listener = VmObjectId.ToObject(request.ListenerInstanceId) as AudioListener;
            if (listener == null || !listener.isActiveAndEnabled || listener.GetComponent<VmAudioRecordingTap>() != null)
                throw new VmProjectToolException("audio_recording_context", "Identify one existing active listener without another recording filter.");
            var mode = AudioSettings.GetConfiguration().speakerMode;
            int channels = mode == AudioSpeakerMode.Mono ? 1 : mode == AudioSpeakerMode.Quad ? 4 :
                mode == AudioSpeakerMode.Surround ? 5 : mode == AudioSpeakerMode.Mode5point1 ? 6 : mode == AudioSpeakerMode.Mode7point1 ? 8 : 2;
            int rate = AudioSettings.outputSampleRate;
            double count = Math.Ceiling(request.DurationSeconds * rate) * channels;
            string output = request.OutputPath;
            if (double.IsNaN(count) || double.IsInfinity(count) || request.DurationSeconds < .01 || request.DurationSeconds > 15 ||
                rate <= 0 || count > 4000000 || !Path.IsPathRooted(output ?? "") ||
                !output.EndsWith(".wav", StringComparison.OrdinalIgnoreCase))
                throw new VmProjectToolException("audio_recording_arguments", "Provide an absolute new WAV and a duration within the15-second/4-million-sample budget.");
            output = Path.GetFullPath(output);
            if (output.StartsWith(Path.GetFullPath("Assets") + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase) ||
                File.Exists(output) || !Directory.Exists(Path.GetDirectoryName(output)))
                throw new VmProjectToolException("audio_recording_arguments", "WAV must be new, outside Assets and below an existing directory.");
            current = new VmAudioRecordingResult { SessionId = Guid.NewGuid().ToString("N"), State = "Recording",
                OutputPath = output, SampleRate = rate, Channels = channels, Error = "", ListenerInstanceId = request.ListenerInstanceId,
                ListenerVolumeAtStart = AudioListener.volume };
            try
            {
                tap = listener.gameObject.AddComponent<VmAudioRecordingTap>();
                tap.hideFlags = HideFlags.HideAndDontSave;
                tap.Begin((int)count, channels);
                deadline = EditorApplication.timeSinceStartup + request.DurationSeconds + 5;
                EditorApplication.update += Update;
                return current;
            }
            catch { Finish(false, "Native listener filter admission failed."); throw; }
        }
        private static void Update()
        {
            if (tap == null) { Finish(false, "The native listener was destroyed."); return; }
            if (tap.Complete) Finish(true, "");
            else if (EditorApplication.timeSinceStartup >= deadline) Finish(false, "Native DSP samples did not reach the requested duration before the observation deadline.");
        }
        private static void Interrupt() => Finish(false, "Interrupted by Play exit, domain reload or Editor shutdown.");
        private static void Finish(bool publish, string reason)
        {
            if (current == null || current.ResourcesReleased) return;
            EditorApplication.update -= Update;
            var actual = tap; tap = null;
            try
            {
                if (actual == null) throw new InvalidOperationException(reason);
                float[] samples = actual.Retire(out int size, out string error);
                current.SampleCount = size;
                current.CapturedSeconds = size / (double)(current.Channels * current.SampleRate);
                if (error != null) throw new InvalidOperationException(error);
                if (!publish) { current.State = "Canceled"; current.Error = reason; return; }
                if (size == 0) throw new InvalidOperationException("No native DSP samples were observed.");
                double squares = 0, peak = 0;
                for (int i = 0; i < size; i++)
                {
                    float sample = samples[i];
                    if (float.IsNaN(sample) || float.IsInfinity(sample)) throw new InvalidOperationException("Native DSP published nonfinite PCM.");
                    squares += sample * (double)sample; peak = Math.Max(peak, Math.Abs(sample));
                }
                current.Peak = peak; current.Rms = Math.Sqrt(squares / size);
                WriteWave(current.OutputPath, samples, size, current.SampleRate, current.Channels);
                current.OutputBytes = new FileInfo(current.OutputPath).Length;
                current.State = "Succeeded";
            }
            catch (Exception failure) { current.State = "Failed"; current.Error = failure.ToString(); }
            finally
            {
                if (actual != null) UnityEngine.Object.DestroyImmediate(actual);
                current.ResourcesReleased = true;
            }
        }
        internal static void WriteWave(string path, float[] samples, int size, int rate, int channels)
        {
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
            using (var writer = new BinaryWriter(stream))
            {
                int bytes = size * sizeof(float);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + bytes);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt ")); writer.Write(16);
                writer.Write((short)3); writer.Write((short)channels); writer.Write(rate); writer.Write(rate * channels * 4);
                writer.Write((short)(channels * 4)); writer.Write((short)32);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(bytes);
                byte[] block = new byte[16384];
                for (int offset = 0; offset < bytes; offset += block.Length)
                {
                    int count = Math.Min(block.Length, bytes - offset);
                    Buffer.BlockCopy(samples, offset, block, 0, count); writer.Write(block, 0, count);
                }
            }
        }
    }
}
