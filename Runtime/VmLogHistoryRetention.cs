using System;
using System.Collections.Generic;
using UnityEngine;

namespace VMUnityAutomation.Runtime
{
    public sealed class VmLogHistoryRetention
    {
        private struct EvictedRange
        {
            internal DateTime First;
            internal DateTime Through;
        }

        private readonly Dictionary<LogType, EvictedRange> evicted =
            new Dictionary<LogType, EvictedRange>();

        public DateTime CaptureStartedAt { get; private set; }

        public VmLogHistoryRetention(DateTime captureStartedAt) => Reset(captureStartedAt);

        public void Reset(DateTime captureStartedAt)
        {
            CaptureStartedAt = captureStartedAt;
            evicted.Clear();
        }

        public void RecordEviction(LogType type, DateTime timestamp)
        {
            if (evicted.TryGetValue(type, out EvictedRange range))
            {
                if (timestamp < range.First) range.First = timestamp;
                if (timestamp > range.Through) range.Through = timestamp;
            }
            else
                range = new EvictedRange { First = timestamp, Through = timestamp };
            evicted[type] = range;
        }

        public bool IsComplete(DateTime? since, DateTime? until, Predicate<LogType> includesType,
            out DateTime? evictedFrom, out DateTime? evictedThrough)
        {
            bool complete = (!since.HasValue || since.Value >= CaptureStartedAt) &&
                            (!until.HasValue || until.Value >= CaptureStartedAt);
            evictedFrom = null;
            evictedThrough = null;
            foreach (KeyValuePair<LogType, EvictedRange> entry in evicted)
            {
                EvictedRange range = entry.Value;
                if (!includesType(entry.Key) || since.HasValue && range.Through < since.Value ||
                    until.HasValue && range.First > until.Value)
                    continue;
                complete = false;
                if (!evictedFrom.HasValue || range.First < evictedFrom.Value)
                    evictedFrom = range.First;
                if (!evictedThrough.HasValue || range.Through > evictedThrough.Value)
                    evictedThrough = range.Through;
            }
            return complete;
        }
    }
}
