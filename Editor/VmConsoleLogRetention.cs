using System;
using System.Collections.Generic;
using UnityEngine;

namespace VMUnityAutomation.Editor
{
    internal sealed class VmConsoleLogRetention
    {
        private struct EvictedRange
        {
            internal DateTime First;
            internal DateTime Through;
        }

        private readonly Dictionary<LogType, EvictedRange> evicted =
            new Dictionary<LogType, EvictedRange>();

        internal DateTime CaptureStartedAt { get; private set; }

        internal VmConsoleLogRetention(DateTime captureStartedAt) => Reset(captureStartedAt);

        internal void Reset(DateTime captureStartedAt)
        {
            CaptureStartedAt = captureStartedAt;
            evicted.Clear();
        }

        internal void RecordEviction(LogType type, DateTime timestamp)
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

        internal bool IsComplete(DateTime? since, DateTime? until, Predicate<LogType> includesType,
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
