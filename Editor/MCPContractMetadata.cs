using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace VMUnityAutomation.Editor
{
    /// <summary>
    /// Owns the public, presence-only metadata vocabulary shared by tool discovery and Jobs.
    /// Authoring APIs may keep strongly typed booleans, but wire contracts expose only positive
    /// tags and exact side effects so false/default state is represented by absence.
    /// </summary>
    internal static class MCPContractMetadata
    {
        internal const int ToolMetadataSchemaVersion = 8;

        internal static class Tag
        {
            internal const string ReadOnly = "readOnly";
            internal const string Dangerous = "dangerous";
            internal const string LongRunning = "longRunning";
            internal const string RequiresPlayMode = "requiresPlayMode";
            internal const string Cleanup = "cleanup";
            internal const string CleanupDeclared = "cleanupDeclared";
            internal const string CleanupAvailable = "cleanupAvailable";
            internal const string IncrementalJob = "incrementalJob";
            internal const string OutputSchema = "outputSchema";
            internal const string Invalid = "invalid";
            internal const string CancellationRequested = "cancellationRequested";
            internal const string Reused = "reused";
            internal const string ChangingPlayMode = "changingPlayMode";
            internal const string Compiling = "compiling";
            internal const string Idle = "idle";
            internal const string Paused = "paused";
            internal const string Playing = "playing";
            internal const string Reloading = "reloading";
            internal const string Updating = "updating";
            internal const string CancelableBeforeStart = "cancelableBeforeStart";
            internal const string CaptureMayStillComplete = "captureMayStillComplete";
            internal const string Cleared = "cleared";
            internal const string CompletionRecovered = "completionRecovered";
            internal const string DeadlineExceeded = "deadlineExceeded";
            internal const string DryRun = "dryRun";
            internal const string Interactive = "interactive";
            internal const string ManifestModified = "manifestModified";
            internal const string ManifestRestoreFailed = "manifestRestoreFailed";
            internal const string ManifestRestored = "manifestRestored";
            internal const string ReconciledAfterReload = "reconciledAfterReload";
            internal const string RecoveredAfterReload = "recoveredAfterReload";
            internal const string RecoveredAcrossOwner = "recoveredAcrossOwner";
            internal const string RecoveredFromSaveException = "recoveredFromSaveException";
            internal const string ReloadResumeLimitExceeded = "reloadResumeLimitExceeded";
            internal const string ResumedAfterReload = "resumedAfterReload";
            internal const string StuckSuspected = "stuckSuspected";
            internal const string TimedOut = "timedOut";
        }

        private static readonly IReadOnlyDictionary<string, string> PresenceBooleanTags =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "readOnly", Tag.ReadOnly },
                { "dangerous", Tag.Dangerous },
                { "longRunning", Tag.LongRunning },
                { "requiresPlayMode", Tag.RequiresPlayMode },
                { "cleanup", Tag.Cleanup },
                { "cleanupDeclared", Tag.CleanupDeclared },
                { "cleanupAvailable", Tag.CleanupAvailable },
                { "incremental", Tag.IncrementalJob },
                { "incrementalJob", Tag.IncrementalJob },
                { "hasOutputSchema", Tag.OutputSchema },
                { "cancellationRequested", Tag.CancellationRequested },
                { "cancelRequested", Tag.CancellationRequested },
                { "reused", Tag.Reused },
                { "isChangingPlayMode", Tag.ChangingPlayMode },
                { "isCompiling", Tag.Compiling },
                { "isIdle", Tag.Idle },
                { "isPaused", Tag.Paused },
                { "isPlaying", Tag.Playing },
                { "isReloading", Tag.Reloading },
                { "isUpdating", Tag.Updating },
                { "cancelableBeforeStart", Tag.CancelableBeforeStart },
                { "captureMayStillComplete", Tag.CaptureMayStillComplete },
                { "cleared", Tag.Cleared },
                { "completionRecovered", Tag.CompletionRecovered },
                { "completionRecoveredFromLeafResults", Tag.CompletionRecovered },
                { "deadlineExceededBeforeCompletion", Tag.DeadlineExceeded },
                { "dryRun", Tag.DryRun },
                { "interactive", Tag.Interactive },
                { "manifestRestored", Tag.ManifestRestored },
                { "reconciledAfterReload", Tag.ReconciledAfterReload },
                { "recoveredAfterReload", Tag.RecoveredAfterReload },
                { "recoveredAcrossOwner", Tag.RecoveredAcrossOwner },
                { "recoveredFromSaveException", Tag.RecoveredFromSaveException },
                { "reloadResumeLimitExceeded", Tag.ReloadResumeLimitExceeded },
                { "resumedAfterReload", Tag.ResumedAfterReload },
                { "stuckSuspected", Tag.StuckSuspected },
                { "timedOut", Tag.TimedOut },
            };

        private static readonly IReadOnlyDictionary<string, string> SideEffectBooleanFields =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "mutatesAssets", "writesAssets" },
                { "mutatesRuntime", "changesRuntimeState" },
                { "mutatesProjectFiles", "writesProjectFiles" },
                { "mayReloadDomain", "reloadsDomain" },
            };

        internal static List<string> BuildToolTags(
            bool readOnly = false,
            bool dangerous = false,
            bool longRunning = false,
            bool requiresPlayMode = false,
            bool cleanup = false,
            bool incrementalJob = false,
            bool outputSchema = false,
            bool invalid = false)
        {
            var tags = new List<string>();
            Add(tags, Tag.ReadOnly, readOnly);
            Add(tags, Tag.Dangerous, dangerous);
            Add(tags, Tag.LongRunning, longRunning);
            Add(tags, Tag.RequiresPlayMode, requiresPlayMode);
            Add(tags, Tag.Cleanup, cleanup);
            Add(tags, Tag.IncrementalJob, incrementalJob);
            Add(tags, Tag.OutputSchema, outputSchema);
            Add(tags, Tag.Invalid, invalid);
            return Normalize(tags);
        }

        internal static List<string> BuildSideEffects(
            IEnumerable declared,
            bool readOnly = false,
            bool mutatesAssets = false,
            bool mutatesRuntime = false,
            bool mayReloadDomain = false)
        {
            var effects = ReadStrings(declared);
            Add(effects, "readsProjectState", readOnly);
            Add(effects, "writesAssets", mutatesAssets);
            Add(effects, "changesRuntimeState", mutatesRuntime);
            Add(effects, "reloadsDomain", mayReloadDomain);
            return Normalize(effects);
        }

        internal static List<string> ReadTags(IReadOnlyDictionary<string, object> metadata)
        {
            return metadata != null && metadata.TryGetValue("tags", out object value)
                ? ReadStrings(value as IEnumerable)
                : new List<string>();
        }

        internal static bool HasTag(IReadOnlyDictionary<string, object> metadata, string tag)
        {
            return ReadTags(metadata).Contains(tag, StringComparer.Ordinal);
        }

        internal static bool HasString(IEnumerable values, string expected)
        {
            return ReadStrings(values).Contains(expected, StringComparer.Ordinal);
        }

        internal static void SetTags(Dictionary<string, object> target, IEnumerable<string> tags)
        {
            if (target == null)
                return;

            List<string> normalized = Normalize(tags);
            if (normalized.Count == 0)
                target.Remove("tags");
            else
                target["tags"] = normalized;
        }

        internal static void AddTag(Dictionary<string, object> target, string tag)
        {
            if (target == null || string.IsNullOrWhiteSpace(tag))
                return;

            var tags = target.TryGetValue("tags", out object value)
                ? ReadStrings(value as IEnumerable)
                : new List<string>();
            tags.Add(tag);
            SetTags(target, tags);
        }

        internal static void AddOptionalString(
            Dictionary<string, object> target, string key, string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
                target[key] = value;
        }

        internal static void AddOptionalList(
            Dictionary<string, object> target, string key, IEnumerable values)
        {
            List<string> normalized = ReadStrings(values);
            if (normalized.Count > 0)
                target[key] = Normalize(normalized);
        }

        /// <summary>
        /// Converts authoring and lifecycle booleans whose false value is represented by
        /// absence into the shared public tags/effects vocabulary. Domain facts such as
        /// valid, visible, found, enabled, or fileExists are deliberately not included:
        /// their false value is business data rather than absent metadata.
        /// </summary>
        internal static void CompactTransportFlags(Dictionary<string, object> target)
        {
            if (target == null || target.Count == 0)
                return;

            var tags = target.TryGetValue("tags", out object tagValue)
                ? ReadStrings(tagValue as IEnumerable)
                : new List<string>();
            foreach (KeyValuePair<string, string> pair in PresenceBooleanTags)
            {
                if (!target.TryGetValue(pair.Key, out object value) || !(value is bool flag))
                    continue;

                target.Remove(pair.Key);
                if (flag)
                    tags.Add(pair.Value);
            }
            SetTags(target, tags);

            var sideEffects = target.TryGetValue("sideEffects", out object effectValue)
                ? ReadStrings(effectValue as IEnumerable)
                : new List<string>();
            foreach (KeyValuePair<string, string> pair in SideEffectBooleanFields)
            {
                if (!target.TryGetValue(pair.Key, out object value) || !(value is bool flag))
                    continue;

                target.Remove(pair.Key);
                if (flag)
                    sideEffects.Add(pair.Value);
            }

            List<string> normalizedEffects = Normalize(sideEffects);
            if (normalizedEffects.Count == 0)
                target.Remove("sideEffects");
            else
                target["sideEffects"] = normalizedEffects;
        }

        private static List<string> ReadStrings(IEnumerable values)
        {
            var result = new List<string>();
            if (values == null || values is string)
                return result;

            foreach (object value in values)
            {
                string text = value?.ToString()?.Trim();
                if (!string.IsNullOrEmpty(text))
                    result.Add(text);
            }
            return result;
        }

        private static List<string> Normalize(IEnumerable<string> values)
        {
            return (values ?? Enumerable.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToList();
        }

        private static void Add(ICollection<string> values, string value, bool include)
        {
            if (include)
                values.Add(value);
        }
    }
}
