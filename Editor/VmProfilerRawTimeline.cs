using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEditorInternal;

namespace VMUnityAutomation.Editor
{
    internal static class VmProfilerRawTimeline
    {
        internal const int MaximumPageSize = 1024;

        internal static Dictionary<string, object> InputSchema()
        {
            return new Dictionary<string, object>
            {
                { "type", "object" }, { "additionalProperties", false },
                { "required", new[] { "offset", "limit" } },
                { "properties", new Dictionary<string, object>
                    {
                        { "offset", new Dictionary<string, object> { { "type", "integer" }, { "minimum", 0 }, { "maximum", int.MaxValue } } },
                        { "limit", new Dictionary<string, object> { { "type", "integer" }, { "minimum", 1 }, { "maximum", MaximumPageSize } } },
                        { "expectedIdentity", new Dictionary<string, object> { { "type", "string" }, { "minLength", 1 }, { "maxLength", 96 } } },
                    }
                },
                { "description", "Page native CPU samples from an explicit stopped frame. Continue nonzero offsets with the returned identity. Nanoseconds are lossless decimal strings, not GPU timestamps." },
            };
        }

        internal static void ValidateRequest(bool recording, bool explicitFrame, int offset, int limit, string expectedIdentity)
        {
            if (recording) throw new InvalidOperationException("Raw Profiler timelines require recording to be stopped.");
            if (!explicitFrame) throw new ArgumentException("Raw Profiler timelines require an explicit frameIndex.");
            if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset));
            if (limit < 1 || limit > MaximumPageSize) throw new ArgumentOutOfRangeException(nameof(limit));
            if (offset > 0 && string.IsNullOrEmpty(expectedIdentity))
                throw new ArgumentException("Continued raw sample pages require expectedIdentity from the first page.");
        }

        internal static int PageCount(int total, int offset, int limit, string identity, string expectedIdentity)
        {
            if (offset < 0 || offset > total) throw new ArgumentOutOfRangeException(nameof(offset));
            if (limit < 1 || limit > MaximumPageSize) throw new ArgumentOutOfRangeException(nameof(limit));
            if (expectedIdentity != null && !string.Equals(identity, expectedIdentity, StringComparison.Ordinal))
                throw new InvalidOperationException("The retained native frame identity changed between raw sample pages.");
            return Math.Min(limit, total - offset);
        }

        internal static void ValidateSampleName(string name)
        {
            if (name != null && name.Length > 512)
                throw new InvalidOperationException("Raw Profiler sample name exceeds512 characters.");
        }

        internal static Dictionary<string, object> Read(Dictionary<string, object> args, int frameIndex, int threadIndex)
        {
            if (!args.TryGetValue("rawSamples", out object value)) return null;
            var request = (Dictionary<string, object>)value;
            int offset = Convert.ToInt32(request["offset"]), limit = Convert.ToInt32(request["limit"]);
            string expected = request.TryGetValue("expectedIdentity", out object requestedIdentity) ? (string)requestedIdentity : null;
            ValidateRequest(ProfilerDriver.enabled, args.ContainsKey("frameIndex"), offset, limit, expected);
            using (var data = ProfilerDriver.GetRawFrameDataView(frameIndex, threadIndex))
            {
                if (!data.valid) throw new InvalidOperationException($"No native raw data for frame {frameIndex}, thread {threadIndex}.");
                string thread = data.threadId.ToString(CultureInfo.InvariantCulture);
                string start = data.frameStartTimeNs.ToString(CultureInfo.InvariantCulture);
                string duration = data.frameTimeNs.ToString(CultureInfo.InvariantCulture);
                int total = data.sampleCount;
                string identity = string.Join(":", thread, start, duration, total.ToString(CultureInfo.InvariantCulture));
                int count = PageCount(total, offset, limit, identity, expected);
                var samples = new List<Dictionary<string, object>>(count);
                for (int index = offset; index < offset + count; index++)
                {
                    string name = data.GetSampleName(index);
                    ValidateSampleName(name);
                    samples.Add(new Dictionary<string, object>
                    {
                        { "index", index }, { "name", name }, { "markerId", data.GetSampleMarkerId(index) },
                        { "startTimeNs", data.GetSampleStartTimeNs(index).ToString(CultureInfo.InvariantCulture) },
                        { "durationNs", data.GetSampleTimeNs(index).ToString(CultureInfo.InvariantCulture) },
                        { "childrenCountRecursive", data.GetSampleChildrenCountRecursive(index) },
                        { "flags", (int)data.GetSampleFlags(index) },
                    });
                }
                return new Dictionary<string, object>
                {
                    { "identity", identity }, { "threadId", thread }, { "frameStartTimeNs", start }, { "frameDurationNs", duration },
                    { "sampleCount", total }, { "offset", offset }, { "count", count },
                    { "nextOffset", offset + count < total ? (object)(offset + count) : null }, { "samples", samples },
                };
            }
        }
    }
}
