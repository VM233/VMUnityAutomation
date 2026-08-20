using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace VMUnityAutomation.Editor
{
    internal static class MCPTimelineCommands
    {
        private const string TimelineAssetTypeName = "UnityEngine.Timeline.TimelineAsset";
        private const string TrackAssetTypeName = "UnityEngine.Timeline.TrackAsset";
        private const string TimelineClipTypeName = "UnityEngine.Timeline.TimelineClip";

        private sealed class TrackReadBudget
        {
            internal readonly int MaxTracks;
            internal readonly int MaxClipsPerTrack;
            internal readonly int MaxMarkersPerTrack;
            internal int ReturnedTracks;
            internal bool Truncated;

            internal TrackReadBudget(int maxTracks, int maxClipsPerTrack,
                int maxMarkersPerTrack)
            {
                MaxTracks = Math.Max(1, Math.Min(1000, maxTracks));
                MaxClipsPerTrack = Math.Max(1, Math.Min(500, maxClipsPerTrack));
                MaxMarkersPerTrack = Math.Max(1, Math.Min(500, maxMarkersPerTrack));
            }
        }

        public static object Info(Dictionary<string, object> args)
        {
            if (!TryLoadTimeline(args, out UnityEngine.Object timeline, out Type timelineType,
                    out object error))
                return error;

            var budget = new TrackReadBudget(
                GetInt(args, "maxTracks", 250),
                GetInt(args, "maxClipsPerTrack", 100),
                GetInt(args, "maxMarkersPerTrack", 100));
            IList rootTracks = Enumerate(Invoke(timelineType, timeline, "GetRootTracks"))
                .Cast<object>().ToList();
            var tracks = ReadTracks(rootTracks, budget);
            var response = new Dictionary<string, object>
            {
                { "success", true },
                { "assetPath", AssetDatabase.GetAssetPath(timeline) },
                { "name", timeline.name ?? "" },
                { "duration", GetProperty(timelineType, timeline, "duration") },
                { "fixedDuration", GetProperty(timelineType, timeline, "fixedDuration") },
                { "durationMode", GetProperty(timelineType, timeline, "durationMode")?.ToString() ?? "" },
                { "rootTrackCount", rootTracks.Count },
                { "returnedTrackCount", budget.ReturnedTracks },
                { "tracksTruncated", budget.Truncated },
                { "tracks", tracks },
            };
            if (GetBool(args, "includeSerialized", false))
            {
                response["serializedGraph"] = MCPAssetGraphUtility.InspectAsset(
                    AssetDatabase.GetAssetPath(timeline), IsTimelineObject,
                    GetInt(args, "maxObjects", 250),
                    GetInt(args, "maxProperties", 60));
            }
            return response;
        }

        public static object Transaction(Dictionary<string, object> args)
        {
            if (!TryLoadTimeline(args, out UnityEngine.Object timeline, out Type timelineType,
                    out object error))
                return error;
            List<object> rawOperations = GetList(args, "operations");
            if (rawOperations == null || rawOperations.Count == 0)
                return MCPResponse.Error("operations must contain at least one operation.",
                    "invalid_arguments");

            var operations = new List<Dictionary<string, object>>();
            for (int index = 0; index < rawOperations.Count; index++)
            {
                if (!(rawOperations[index] is Dictionary<string, object> operation))
                    return MCPResponse.Error($"operations[{index}] must be an object.",
                        "invalid_arguments");
                string action = GetString(operation, "action").ToLowerInvariant();
                if (action != "create-track" && action != "delete-track" &&
                    action != "rename-track" && action != "set-track-property" &&
                    action != "create-clip" && action != "delete-clip" &&
                    action != "set-clip")
                {
                    return MCPResponse.Error(
                        $"operations[{index}].action must be create-track, delete-track, rename-track, set-track-property, create-clip, delete-clip, or set-clip.",
                        "invalid_arguments");
                }
                try
                {
                    ValidateOperationKeys(operation, action);
                }
                catch (Exception exception)
                {
                    return MCPResponse.Error(exception.Message,
                        "invalid_arguments");
                }
                operations.Add(operation);
            }

            bool dryRun = GetBool(args, "dryRun", false);
            if (dryRun)
            {
                var validated = new List<Dictionary<string, object>>();
                try
                {
                    foreach (Dictionary<string, object> operation in operations)
                        validated.Add(ValidateOperation(timeline, timelineType, operation));
                }
                catch (Exception exception)
                {
                    return MCPResponse.Error(exception.GetBaseException().Message,
                        "timeline_transaction_invalid");
                }
                return new Dictionary<string, object>
                {
                    { "success", true },
                    { "dryRun", true },
                    { "assetPath", AssetDatabase.GetAssetPath(timeline) },
                    { "operationCount", operations.Count },
                    { "operations", validated },
                    { "timeline", new Dictionary<string, object>
                        {
                             { "name", timeline.name ?? "" },
                             { "duration", GetProperty(timelineType, timeline, "duration") },
                            { "rootTrackCount", Enumerate(
                                Invoke(timelineType, timeline, "GetRootTracks")).Count() },
                        }
                    },
                };
            }

            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("VM Unity Automation Edit Timeline");
            Undo.RegisterCompleteObjectUndo(
                AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(timeline)),
                "VM Unity Automation Edit Timeline");
            var results = new List<Dictionary<string, object>>();
            try
            {
                foreach (Dictionary<string, object> operation in operations)
                    results.Add(ApplyOperation(timeline, timelineType, operation));
                EditorUtility.SetDirty(timeline);
                AssetDatabase.SaveAssets();
                Undo.CollapseUndoOperations(undoGroup);
                var response = new Dictionary<string, object>
                {
                    { "success", true },
                    { "dryRun", false },
                    { "operationCount", results.Count },
                    { "results", results },
                };
                if (GetBool(args, "includeAfter", false))
                {
                    var infoArgs = new Dictionary<string, object>
                    {
                        { "assetPath", AssetDatabase.GetAssetPath(timeline) },
                        { "maxTracks", GetInt(args, "maxTracks", 250) },
                        { "maxClipsPerTrack", GetInt(args, "maxClipsPerTrack", 100) },
                        { "maxMarkersPerTrack", GetInt(args, "maxMarkersPerTrack", 100) },
                    };
                    response["after"] = Info(infoArgs);
                }
                return response;
            }
            catch (Exception exception)
            {
                Undo.RevertAllDownToGroup(undoGroup);
                return MCPResponse.Error(exception.GetBaseException().Message,
                    "timeline_transaction_failed");
            }
        }

        private static Dictionary<string, object> ValidateOperation(UnityEngine.Object timeline,
            Type timelineType, Dictionary<string, object> operation)
        {
            string action = GetString(operation, "action").ToLowerInvariant();
            var result = new Dictionary<string, object> { { "action", action } };
            if (action == "create-track")
            {
                Type trackType = FindAssignableType(GetString(operation, "trackType"),
                    MCPAssetGraphUtility.FindType(TrackAssetTypeName));
                if (trackType == null)
                    throw new ArgumentException(
                        $"Timeline track type '{GetString(operation, "trackType")}' was not found.");
                string parentId = GetString(operation, "parentTrackLocalId");
                if (!string.IsNullOrEmpty(parentId))
                    ResolveTrack(timeline, timelineType, parentId, allowEmpty: false);
                result["trackType"] = trackType.FullName;
                result["name"] = GetString(operation, "name");
                result["parentTrackLocalId"] = parentId;
                return result;
            }

            UnityEngine.Object track = ResolveTrack(timeline, timelineType,
                GetString(operation, "trackLocalId"), allowEmpty: false);
            result["trackLocalId"] = LocalId(track);
            result["trackType"] = track.GetType().FullName;
            if (action == "rename-track")
            {
                string name = GetString(operation, "name");
                if (string.IsNullOrEmpty(name))
                    throw new ArgumentException("name is required for rename-track.");
                result["before"] = track.name ?? "";
                result["requested"] = name;
                return result;
            }
            if (action == "set-track-property")
            {
                string propertyPath = GetString(operation, "propertyPath");
                if (string.IsNullOrEmpty(propertyPath) ||
                    !operation.TryGetValue("value", out object value))
                    throw new ArgumentException(
                        "set-track-property requires propertyPath and value.");
                var serialized = new SerializedObject(track);
                SerializedProperty property = serialized.FindProperty(propertyPath);
                if (property == null)
                    throw new ArgumentException(
                        $"Timeline track property '{propertyPath}' was not found.");
                object before = MCPComponentCommands.GetSerializedValue(property, 2, 32);
                MCPComponentCommands.SetSerializedValue(property, value);
                result["propertyPath"] = propertyPath;
                result["before"] = before;
                result["requested"] = value;
                return result;
            }
            if (action == "delete-track")
                return result;

            List<object> clips = Enumerate(Invoke(track.GetType(), track, "GetClips")).ToList();
            if (action == "create-clip")
            {
                Type clipAssetType = FindType(GetString(operation, "clipAssetType"));
                if (clipAssetType == null)
                    throw new ArgumentException(
                        $"Timeline clip asset type '{GetString(operation, "clipAssetType")}' was not found.");
                result["clipAssetType"] = clipAssetType.FullName;
                ValidateClipValues(operation);
                return result;
            }

            int clipIndex = GetInt(operation, "clipIndex", -1);
            if (clipIndex < 0 || clipIndex >= clips.Count)
                throw new ArgumentException(
                    $"clipIndex {clipIndex} is outside track clip range 0..{clips.Count - 1}.");
            result["clipIndex"] = clipIndex;
            if (action == "set-clip")
                ValidateClipValues(operation);
            return result;
        }

        private static void ValidateClipValues(Dictionary<string, object> operation)
        {
            Type clipType = MCPAssetGraphUtility.FindType(TimelineClipTypeName);
            if (clipType == null)
                throw new InvalidOperationException("TimelineClip type is unavailable.");
            foreach (string key in new[]
                     {
                         "displayName", "start", "duration", "clipIn", "timeScale",
                         "easeInDuration", "easeOutDuration",
                     })
            {
                if (!operation.TryGetValue(key, out object value))
                    continue;
                PropertyInfo property = clipType.GetProperty(key,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (property == null || !property.CanWrite)
                    throw new MissingMemberException(clipType.FullName, key);
                if (value != null && !property.PropertyType.IsInstanceOfType(value))
                    Convert.ChangeType(value, property.PropertyType);
            }
        }

        private static Dictionary<string, object> ApplyOperation(UnityEngine.Object timeline,
            Type timelineType, Dictionary<string, object> operation)
        {
            string action = GetString(operation, "action").ToLowerInvariant();
            if (action == "create-track")
            {
                Type trackType = FindAssignableType(GetString(operation, "trackType"),
                    MCPAssetGraphUtility.FindType(TrackAssetTypeName));
                if (trackType == null)
                    throw new ArgumentException(
                        $"Timeline track type '{GetString(operation, "trackType")}' was not found.");
                UnityEngine.Object parent = ResolveTrack(timeline, timelineType,
                    GetString(operation, "parentTrackLocalId"), allowEmpty: true);
                MethodInfo createTrack = timelineType.GetMethod("CreateTrack",
                    BindingFlags.Instance | BindingFlags.Public, null,
                    new[]
                    {
                        typeof(Type), MCPAssetGraphUtility.FindType(TrackAssetTypeName),
                        typeof(string)
                    }, null);
                if (createTrack == null)
                    throw new MissingMethodException(timelineType.FullName, "CreateTrack");
                object created = createTrack.Invoke(timeline,
                    new object[] { trackType, parent, GetString(operation, "name") });
                return new Dictionary<string, object>
                {
                    { "action", action },
                    { "track", TrackInfo(created) },
                };
            }

            UnityEngine.Object track = ResolveTrack(timeline, timelineType,
                GetString(operation, "trackLocalId"), allowEmpty: false);
            if (action == "delete-track")
            {
                bool deleted = Convert.ToBoolean(Invoke(timelineType, timeline,
                    "DeleteTrack", track));
                if (!deleted)
                    throw new InvalidOperationException("Timeline rejected the track deletion.");
                return new Dictionary<string, object>
                {
                    { "action", action },
                    { "trackLocalId", GetString(operation, "trackLocalId") },
                    { "deleted", true },
                };
            }

            if (action == "rename-track")
            {
                string name = GetString(operation, "name");
                if (string.IsNullOrEmpty(name))
                    throw new ArgumentException("name is required for rename-track.");
                string before = track.name;
                track.name = name;
                EditorUtility.SetDirty(track);
                return new Dictionary<string, object>
                {
                    { "action", action },
                    { "trackLocalId", LocalId(track) },
                    { "before", before },
                    { "after", track.name },
                };
            }

            if (action == "set-track-property")
            {
                string propertyPath = GetString(operation, "propertyPath");
                if (string.IsNullOrEmpty(propertyPath) ||
                    !operation.TryGetValue("value", out object value))
                    throw new ArgumentException(
                        "set-track-property requires propertyPath and value.");
                var serialized = new SerializedObject(track);
                serialized.Update();
                SerializedProperty property = serialized.FindProperty(propertyPath);
                if (property == null)
                    throw new ArgumentException(
                        $"Timeline track property '{propertyPath}' was not found.");
                object before = MCPComponentCommands.GetSerializedValue(property, 2, 32);
                MCPComponentCommands.SetSerializedValue(property, value);
                serialized.ApplyModifiedProperties();
                EditorUtility.SetDirty(track);
                return new Dictionary<string, object>
                {
                    { "action", action },
                    { "trackLocalId", LocalId(track) },
                    { "propertyPath", propertyPath },
                    { "before", before },
                    { "after", MCPComponentCommands.GetSerializedValue(
                        new SerializedObject(track).FindProperty(propertyPath), 2, 32) },
                };
            }

            IList clips = Enumerate(Invoke(track.GetType(), track, "GetClips")).Cast<object>()
                .ToList();
            if (action == "create-clip")
            {
                Type clipAssetType = FindType(GetString(operation, "clipAssetType"));
                if (clipAssetType == null)
                    throw new ArgumentException(
                        $"Timeline clip asset type '{GetString(operation, "clipAssetType")}' was not found.");
                MethodInfo createClip = track.GetType().GetMethod("CreateClip",
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null, new[] { typeof(Type) }, null);
                if (createClip == null)
                    throw new MissingMethodException(track.GetType().FullName, "CreateClip(Type)");
                object clip = createClip.Invoke(track, new object[] { clipAssetType });
                ApplyClipValues(clip, operation);
                return new Dictionary<string, object>
                {
                    { "action", action },
                    { "trackLocalId", LocalId(track) },
                    { "clip", ClipInfo(clip, clips.Count) },
                };
            }

            int clipIndex = GetInt(operation, "clipIndex", -1);
            if (clipIndex < 0 || clipIndex >= clips.Count)
                throw new ArgumentException(
                    $"clipIndex {clipIndex} is outside track clip range 0..{clips.Count - 1}.");
            object selectedClip = clips[clipIndex];
            if (action == "delete-clip")
            {
                bool deleted = Convert.ToBoolean(Invoke(timelineType, timeline,
                    "DeleteClip", selectedClip));
                if (!deleted)
                    throw new InvalidOperationException("Timeline rejected the clip deletion.");
                return new Dictionary<string, object>
                {
                    { "action", action },
                    { "trackLocalId", LocalId(track) },
                    { "clipIndex", clipIndex },
                    { "deleted", true },
                };
            }

            Dictionary<string, object> beforeClip = ClipInfo(selectedClip, clipIndex);
            ApplyClipValues(selectedClip, operation);
            return new Dictionary<string, object>
            {
                { "action", action },
                { "trackLocalId", LocalId(track) },
                { "clipIndex", clipIndex },
                { "before", beforeClip },
                { "after", ClipInfo(selectedClip, clipIndex) },
            };
        }

        private static void ValidateOperationKeys(Dictionary<string, object> operation,
            string action)
        {
            string[] clipValueKeys =
            {
                "displayName", "start", "duration", "clipIn", "timeScale",
                "easeInDuration", "easeOutDuration",
            };
            string[] allowed;
            switch (action)
            {
                case "create-track":
                    allowed = new[]
                    {
                        "action", "trackType", "name", "parentTrackLocalId",
                    };
                    break;
                case "delete-track":
                    allowed = new[] { "action", "trackLocalId" };
                    break;
                case "rename-track":
                    allowed = new[] { "action", "trackLocalId", "name" };
                    break;
                case "set-track-property":
                    allowed = new[]
                    {
                        "action", "trackLocalId", "propertyPath", "value",
                    };
                    break;
                case "create-clip":
                    allowed = new[]
                    {
                        "action", "trackLocalId", "clipAssetType",
                    }.Concat(clipValueKeys).ToArray();
                    break;
                case "delete-clip":
                    allowed = new[] { "action", "trackLocalId", "clipIndex" };
                    break;
                default:
                    allowed = new[]
                    {
                        "action", "trackLocalId", "clipIndex",
                    }.Concat(clipValueKeys).ToArray();
                    break;
            }

            var allowedSet = new HashSet<string>(allowed, StringComparer.Ordinal);
            string unexpected = operation.Keys.FirstOrDefault(key =>
                !allowedSet.Contains(key));
            if (!string.IsNullOrEmpty(unexpected))
                throw new ArgumentException(
                    $"Unsupported field '{unexpected}' for Timeline action '{action}'. " +
                    $"Allowed fields: {string.Join(", ", allowed.OrderBy(item => item))}.");
        }

        private static List<Dictionary<string, object>> ReadTracks(IEnumerable rootTracks,
            TrackReadBudget budget)
        {
            var tracks = new List<Dictionary<string, object>>();
            foreach (object track in rootTracks)
            {
                Dictionary<string, object> info = TrackInfo(track, budget);
                if (info != null)
                    tracks.Add(info);
            }
            return tracks;
        }

        private static Dictionary<string, object> TrackInfo(object track)
        {
            return TrackInfo(track, new TrackReadBudget(250, 100, 100));
        }

        private static Dictionary<string, object> TrackInfo(object track,
            TrackReadBudget budget)
        {
            if (track == null)
                return null;
            if (budget.ReturnedTracks >= budget.MaxTracks)
            {
                budget.Truncated = true;
                return null;
            }
            budget.ReturnedTracks++;
            var trackObject = track as UnityEngine.Object;
            List<object> clips = Enumerate(Invoke(track.GetType(), track, "GetClips")).ToList();
            List<object> children = Enumerate(Invoke(track.GetType(), track, "GetChildTracks"))
                .ToList();
            List<object> markers = Enumerate(Invoke(track.GetType(), track, "GetMarkers")).ToList();
            List<Dictionary<string, object>> childInfos = children
                .Select(child => TrackInfo(child, budget))
                .Where(child => child != null)
                .ToList();
            bool clipsTruncated = clips.Count > budget.MaxClipsPerTrack;
            bool markersTruncated = markers.Count > budget.MaxMarkersPerTrack;
            if (clipsTruncated || markersTruncated ||
                childInfos.Count < children.Count)
                budget.Truncated = true;
            return new Dictionary<string, object>
            {
                { "localId", LocalId(trackObject) },
                { "name", trackObject?.name ?? "" },
                { "type", track.GetType().FullName },
                { "muted", GetProperty(track.GetType(), track, "muted") ?? false },
                { "locked", GetProperty(track.GetType(), track, "locked") ?? false },
                { "clipCount", clips.Count },
                { "clips", clips.Take(budget.MaxClipsPerTrack)
                    .Select((clip, index) => ClipInfo(clip, index)).ToList() },
                { "clipsTruncated", clipsTruncated },
                { "markerCount", markers.Count },
                { "markers", markers.Take(budget.MaxMarkersPerTrack)
                    .Select(MarkerInfo).ToList() },
                { "markersTruncated", markersTruncated },
                { "childTrackCount", children.Count },
                { "children", childInfos },
            };
        }

        private static Dictionary<string, object> ClipInfo(object clip, int index)
        {
            if (clip == null)
                return null;
            Type type = clip.GetType();
            object asset = GetProperty(type, clip, "asset");
            return new Dictionary<string, object>
            {
                { "index", index },
                { "displayName", GetProperty(type, clip, "displayName")?.ToString() ?? "" },
                { "start", GetProperty(type, clip, "start") },
                { "duration", GetProperty(type, clip, "duration") },
                { "end", GetProperty(type, clip, "end") },
                { "clipIn", GetProperty(type, clip, "clipIn") },
                { "timeScale", GetProperty(type, clip, "timeScale") },
                { "easeInDuration", GetProperty(type, clip, "easeInDuration") },
                { "easeOutDuration", GetProperty(type, clip, "easeOutDuration") },
                { "assetType", asset?.GetType().FullName ?? "" },
                { "assetName", (asset as UnityEngine.Object)?.name ?? "" },
            };
        }

        private static Dictionary<string, object> MarkerInfo(object marker)
        {
            if (marker == null)
                return null;
            return new Dictionary<string, object>
            {
                { "type", marker.GetType().FullName },
                { "time", GetProperty(marker.GetType(), marker, "time") },
            };
        }

        private static void ApplyClipValues(object clip, Dictionary<string, object> operation)
        {
            if (clip == null)
                throw new InvalidOperationException("Timeline did not create a clip.");
            Type clipType = MCPAssetGraphUtility.FindType(TimelineClipTypeName) ?? clip.GetType();
            SetOptionalProperty(clipType, clip, operation, "displayName");
            SetOptionalProperty(clipType, clip, operation, "start");
            SetOptionalProperty(clipType, clip, operation, "duration");
            SetOptionalProperty(clipType, clip, operation, "clipIn");
            SetOptionalProperty(clipType, clip, operation, "timeScale");
            SetOptionalProperty(clipType, clip, operation, "easeInDuration");
            SetOptionalProperty(clipType, clip, operation, "easeOutDuration");
        }

        private static void SetOptionalProperty(Type type, object target,
            Dictionary<string, object> values, string key)
        {
            if (!values.TryGetValue(key, out object value))
                return;
            PropertyInfo property = type.GetProperty(key,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (property == null || !property.CanWrite)
                throw new MissingMemberException(type.FullName, key);
            object converted = value;
            if (value != null && !property.PropertyType.IsInstanceOfType(value))
                converted = Convert.ChangeType(value, property.PropertyType);
            property.SetValue(target, converted);
        }

        private static UnityEngine.Object ResolveTrack(UnityEngine.Object timeline,
            Type timelineType, string localId, bool allowEmpty)
        {
            if (string.IsNullOrEmpty(localId))
            {
                if (allowEmpty)
                    return null;
                throw new ArgumentException("trackLocalId is required.");
            }

            UnityEngine.Object track = AssetDatabase.LoadAllAssetsAtPath(
                    AssetDatabase.GetAssetPath(timeline))
                .FirstOrDefault(item => item != null &&
                                        IsTimelineObject(item) &&
                                        string.Equals(LocalId(item), localId,
                                            StringComparison.Ordinal));
            if (track == null)
                throw new ArgumentException($"Timeline track '{localId}' was not found.");
            return track;
        }

        private static bool TryLoadTimeline(Dictionary<string, object> args,
            out UnityEngine.Object timeline, out Type timelineType, out object error)
        {
            timeline = null;
            timelineType = MCPAssetGraphUtility.FindType(TimelineAssetTypeName);
            if (timelineType == null)
            {
                error = MCPResponse.Error(
                    "Timeline is unavailable. Install com.unity.timeline.",
                    "capability_unavailable");
                return false;
            }
            string assetPath = GetString(args, "assetPath");
            if (string.IsNullOrEmpty(assetPath))
            {
                error = MCPResponse.Error("assetPath is required.", "invalid_arguments");
                return false;
            }
            timeline = AssetDatabase.LoadMainAssetAtPath(assetPath);
            if (timeline == null || !timelineType.IsInstanceOfType(timeline))
            {
                error = MCPResponse.Error(
                    $"Timeline asset '{assetPath}' was not found.",
                    "asset_not_found");
                return false;
            }
            error = null;
            return true;
        }

        private static bool IsTimelineObject(UnityEngine.Object value)
        {
            return MCPAssetGraphUtility.IsTypeOrNamespace(value,
                "UnityEngine.Timeline.", "UnityEditor.Timeline.");
        }

        private static Type FindAssignableType(string name, Type baseType)
        {
            Type type = FindType(name);
            return type != null && baseType != null && baseType.IsAssignableFrom(type)
                ? type
                : null;
        }

        private static Type FindType(string name)
        {
            if (string.IsNullOrEmpty(name))
                return null;
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    Type exact = assembly.GetType(name, false);
                    if (exact != null)
                        return exact;
                    Type shortName = assembly.GetTypes().FirstOrDefault(type =>
                        string.Equals(type.Name, name, StringComparison.Ordinal));
                    if (shortName != null)
                        return shortName;
                }
                catch (ReflectionTypeLoadException exception)
                {
                    Type loaded = exception.Types?.FirstOrDefault(type => type != null &&
                        (string.Equals(type.FullName, name, StringComparison.Ordinal) ||
                         string.Equals(type.Name, name, StringComparison.Ordinal)));
                    if (loaded != null)
                        return loaded;
                }
                catch
                {
                    // Optional package assembly may be mid-reload.
                }
            }
            return null;
        }

        private static object Invoke(Type type, object target, string method, params object[] args)
        {
            MethodInfo match = type.GetMethods(BindingFlags.Instance | BindingFlags.Public |
                                               BindingFlags.NonPublic)
                .Where(candidate => candidate.Name == method)
                .FirstOrDefault(candidate => candidate.GetParameters().Length ==
                                             (args?.Length ?? 0));
            if (match == null)
                throw new MissingMethodException(type.FullName, method);
            return match.Invoke(target, args);
        }

        private static object GetProperty(Type type, object target, string name)
        {
            return type.GetProperty(name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?.GetValue(target);
        }

        private static IEnumerable<object> Enumerate(object value)
        {
            if (!(value is IEnumerable enumerable))
                yield break;
            foreach (object item in enumerable)
                yield return item;
        }

        private static string LocalId(UnityEngine.Object value)
        {
            return value != null &&
                   AssetDatabase.TryGetGUIDAndLocalFileIdentifier(value, out string _, out long id)
                ? id.ToString()
                : "";
        }

        private static string GetString(Dictionary<string, object> values, string key)
        {
            return values != null && values.TryGetValue(key, out object value) && value != null
                ? value.ToString()
                : "";
        }

        private static int GetInt(Dictionary<string, object> values, string key, int defaultValue)
        {
            return values != null && values.TryGetValue(key, out object value) && value != null
                ? Convert.ToInt32(value)
                : defaultValue;
        }

        private static bool GetBool(Dictionary<string, object> values, string key,
            bool defaultValue)
        {
            return values != null && values.TryGetValue(key, out object value) && value != null
                ? Convert.ToBoolean(value)
                : defaultValue;
        }

        private static List<object> GetList(Dictionary<string, object> values, string key)
        {
            return values != null && values.TryGetValue(key, out object value)
                ? value as List<object>
                : null;
        }
    }
}
