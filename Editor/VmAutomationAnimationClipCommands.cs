using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using static VMUnityAutomation.Editor.VmAutomationAnimationCommandUtility;

namespace VMUnityAutomation.Editor
{
    public static class VmAutomationAnimationClipCommands
    {
    public static object CreateClip(Dictionary<string, object> args)
    {
        string path = args.ContainsKey("path") ? args["path"].ToString() : "";
        if (string.IsNullOrEmpty(path))
            return new { error = "path is required (e.g. 'Assets/Animations/Walk.anim')" };

        // Ensure directory
        string dir = Path.GetDirectoryName(path)?.Replace('\\', '/');
        if (!string.IsNullOrEmpty(dir) && !AssetDatabase.IsValidFolder(dir))
        {
            string[] parts = dir.Split('/');
            string current = parts[0];
            for (int i = 1; i < parts.Length; i++)
            {
                string next = current + "/" + parts[i];
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[i]);
                current = next;
            }
        }

        var clip = new AnimationClip();
        clip.name = Path.GetFileNameWithoutExtension(path);

        if (args.ContainsKey("loop"))
        {
            var settings = AnimationUtility.GetAnimationClipSettings(clip);
            settings.loopTime = Convert.ToBoolean(args["loop"]);
            AnimationUtility.SetAnimationClipSettings(clip, settings);
        }

        if (args.ContainsKey("frameRate"))
            clip.frameRate = Convert.ToSingle(args["frameRate"]);

        AssetDatabase.CreateAsset(clip, path);
        AssetDatabase.SaveAssets();

        return new Dictionary<string, object>
        {
            { "success", true },
            { "path", path },
            { "name", clip.name },
            { "length", clip.length },
            { "frameRate", clip.frameRate },
            { "isLooping", clip.isLooping },
        };
    }

    public static object GetClipInfo(Dictionary<string, object> args)
    {
        string path = args.ContainsKey("path") ? args["path"].ToString() : "";
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null)
            return new { error = $"Animation clip not found at '{path}'" };

        var bindings = AnimationUtility.GetCurveBindings(clip);
        var curves = new List<Dictionary<string, object>>();
        foreach (var binding in bindings)
        {
            var curve = AnimationUtility.GetEditorCurve(clip, binding);
            curves.Add(new Dictionary<string, object>
            {
                { "path", binding.path },
                { "propertyName", binding.propertyName },
                { "type", binding.type.Name },
                { "keyframeCount", curve.keys.Length },
            });
        }

        var objectReferenceBindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
        var objectReferenceCurves = new List<Dictionary<string, object>>();
        foreach (var binding in objectReferenceBindings)
        {
            var keyframes = AnimationUtility.GetObjectReferenceCurve(clip, binding);
            var keyframeInfos = new List<Dictionary<string, object>>();
            for (int i = 0; i < keyframes.Length; i++)
            {
                keyframeInfos.Add(GetObjectReferenceKeyframeInfo(i, keyframes[i]));
            }

            objectReferenceCurves.Add(new Dictionary<string, object>
            {
                { "path", binding.path },
                { "propertyName", binding.propertyName },
                { "type", binding.type != null ? binding.type.Name : null },
                { "typeFullName", binding.type != null ? binding.type.FullName : null },
                { "keyframeCount", keyframes.Length },
                { "keyframes", keyframeInfos.ToArray() },
            });
        }

        var settings = AnimationUtility.GetAnimationClipSettings(clip);

        return new Dictionary<string, object>
        {
            { "name", clip.name },
            { "path", path },
            { "length", clip.length },
            { "frameRate", clip.frameRate },
            { "isLooping", settings.loopTime },
            { "wrapMode", clip.wrapMode.ToString() },
            { "curveCount", curves.Count },
            { "curves", curves },
            { "objectReferenceCurveCount", objectReferenceCurves.Count },
            { "objectReferenceCurves", objectReferenceCurves },
            { "events", clip.events.Length },
            { "isHumanMotion", clip.humanMotion },
        };
    }

    public static object SetClipCurve(Dictionary<string, object> args)
    {
        string path = args.ContainsKey("clipPath") ? args["clipPath"].ToString() : "";
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null)
            return new { error = $"Animation clip not found at '{path}'" };

        string relativePath = args.ContainsKey("relativePath") ? args["relativePath"].ToString() : "";
        string propertyName = args.ContainsKey("propertyName") ? args["propertyName"].ToString() : "";
        string typeName = args.ContainsKey("type") ? args["type"].ToString() : "Transform";

        if (string.IsNullOrEmpty(propertyName))
            return new { error = "propertyName is required" };

        Type type = Type.GetType($"UnityEngine.{typeName}, UnityEngine") ??
                    Type.GetType($"UnityEngine.{typeName}, UnityEngine.CoreModule") ??
                    typeof(Transform);

        // Build keyframes
        var keyframes = new List<Keyframe>();
        if (args.ContainsKey("keyframes"))
        {
            var kfList = args["keyframes"] as List<object>;
            if (kfList != null)
            {
                foreach (var kfObj in kfList)
                {
                    var kf = kfObj as Dictionary<string, object>;
                    if (kf == null) continue;
                    float time = kf.ContainsKey("time") ? Convert.ToSingle(kf["time"]) : 0f;
                    float value = kf.ContainsKey("value") ? Convert.ToSingle(kf["value"]) : 0f;
                    keyframes.Add(new Keyframe(time, value));
                }
            }
        }

        var curve = new AnimationCurve(keyframes.ToArray());
        clip.SetCurve(relativePath, type, propertyName, curve);

        EditorUtility.SetDirty(clip);
        AssetDatabase.SaveAssets();

        return new Dictionary<string, object>
        {
            { "success", true },
            { "clipPath", path },
            { "relativePath", relativePath },
            { "propertyName", propertyName },
            { "keyframeCount", keyframes.Count },
        };
    }

    public static object SetObjectReferenceCurve(Dictionary<string, object> args)
    {
        string path = args.ContainsKey("clipPath") ? args["clipPath"].ToString() : "";
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null)
            return new { error = $"Animation clip not found at '{path}'" };

        string relativePath = args.ContainsKey("relativePath") ? args["relativePath"].ToString() : "";
        string propertyName = args.ContainsKey("propertyName") ? args["propertyName"].ToString() : "";
        string typeName = args.ContainsKey("type") ? args["type"].ToString() : "SpriteRenderer";

        if (string.IsNullOrEmpty(propertyName))
            return new { error = "propertyName is required" };

        Type type = ResolveUnityType(typeName, typeof(SpriteRenderer));
        if (type == null)
            return new { error = $"Could not resolve type '{typeName}'" };

        if (!args.ContainsKey("keyframes"))
            return new { error = "keyframes is required" };

        var kfList = args["keyframes"] as List<object>;
        if (kfList == null)
            return new { error = "keyframes must be an array" };

        var keyframes = new List<ObjectReferenceKeyframe>();
        for (int i = 0; i < kfList.Count; i++)
        {
            var kf = kfList[i] as Dictionary<string, object>;
            if (kf == null)
                return new { error = $"keyframes[{i}] must be an object" };

            if (!kf.ContainsKey("time"))
                return new { error = $"keyframes[{i}].time is required" };

            UnityEngine.Object value;
            try
            {
                value = ResolveObjectReferenceKeyframeValue(kf, type, propertyName);
            }
            catch (Exception e)
            {
                return new { error = $"Failed to resolve keyframes[{i}] object reference: {e.Message}" };
            }

            keyframes.Add(new ObjectReferenceKeyframe
            {
                time = Convert.ToSingle(kf["time"]),
                value = value,
            });
        }

        keyframes.Sort((a, b) => a.time.CompareTo(b.time));

        var binding = EditorCurveBinding.PPtrCurve(relativePath, type, propertyName);
        AnimationUtility.SetObjectReferenceCurve(clip, binding, keyframes.ToArray());

        EditorUtility.SetDirty(clip);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(path);

        return new Dictionary<string, object>
        {
            { "success", true },
            { "clipPath", path },
            { "relativePath", relativePath },
            { "propertyName", propertyName },
            { "type", type.Name },
            { "keyframeCount", keyframes.Count },
        };
    }

    // ─── Layers ───


    public static object GetCurveKeyframes(Dictionary<string, object> args)
    {
        string path = args.ContainsKey("clipPath") ? args["clipPath"].ToString() : "";
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null)
            return new { error = $"Animation clip not found at '{path}'" };

        string relativePath = args.ContainsKey("relativePath") ? args["relativePath"].ToString() : "";
        string propertyName = args.ContainsKey("propertyName") ? args["propertyName"].ToString() : "";

        if (string.IsNullOrEmpty(propertyName))
            return new { error = "propertyName is required" };

        var bindings = AnimationUtility.GetCurveBindings(clip);
        EditorCurveBinding? targetBinding = null;

        foreach (var binding in bindings)
        {
            if (binding.propertyName == propertyName &&
                binding.path == relativePath)
            {
                targetBinding = binding;
                break;
            }
        }

        if (!targetBinding.HasValue)
            return new { error = $"Curve not found for property '{propertyName}' at path '{relativePath}'" };

        var curve = AnimationUtility.GetEditorCurve(clip, targetBinding.Value);
        var keyframes = new List<Dictionary<string, object>>();

        for (int i = 0; i < curve.keys.Length; i++)
        {
            var kf = curve.keys[i];
            keyframes.Add(new Dictionary<string, object>
            {
                { "index", i },
                { "time", kf.time },
                { "value", kf.value },
                { "inTangent", kf.inTangent },
                { "outTangent", kf.outTangent },
                { "inWeight", kf.inWeight },
                { "outWeight", kf.outWeight },
                { "weightedMode", kf.weightedMode.ToString() },
            });
        }

        return new Dictionary<string, object>
        {
            { "clipPath", path },
            { "relativePath", relativePath },
            { "propertyName", propertyName },
            { "type", targetBinding.Value.type.Name },
            { "keyframeCount", keyframes.Count },
            { "keyframes", keyframes.ToArray() },
        };
    }

    public static object RemoveCurve(Dictionary<string, object> args)
    {
        string path = args.ContainsKey("clipPath") ? args["clipPath"].ToString() : "";
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null)
            return new { error = $"Animation clip not found at '{path}'" };

        string relativePath = args.ContainsKey("relativePath") ? args["relativePath"].ToString() : "";
        string propertyName = args.ContainsKey("propertyName") ? args["propertyName"].ToString() : "";
        string typeName = args.ContainsKey("type") ? args["type"].ToString() : "Transform";

        if (string.IsNullOrEmpty(propertyName))
            return new { error = "propertyName is required" };

        Type type = Type.GetType($"UnityEngine.{typeName}, UnityEngine") ??
                    Type.GetType($"UnityEngine.{typeName}, UnityEngine.CoreModule") ??
                    typeof(Transform);

        // Use AnimationUtility.SetEditorCurve to remove individual curve bindings safely.
        // clip.SetCurve(path, type, prop, null) fails on compound properties like localPosition.x
        // because Unity requires removing the entire m_LocalPosition at once via that API.
        var bindings = AnimationUtility.GetCurveBindings(clip);
        int removed = 0;
        foreach (var binding in bindings)
        {
            if (binding.path == relativePath && binding.type == type && binding.propertyName == propertyName)
            {
                AnimationUtility.SetEditorCurve(clip, binding, null);
                removed++;
            }
        }

        if (removed == 0)
        {
            // Fallback: try SetCurve for non-compound properties
            try { clip.SetCurve(relativePath, type, propertyName, null); removed = 1; }
            catch { return new { error = $"Curve binding not found: path='{relativePath}' type='{typeName}' property='{propertyName}'" }; }
        }

        EditorUtility.SetDirty(clip);
        AssetDatabase.SaveAssets();

        return new { success = true, clipPath = path, removedProperty = propertyName, removedCount = removed };
    }

    public static object AddKeyframe(Dictionary<string, object> args)
    {
        string path = args.ContainsKey("clipPath") ? args["clipPath"].ToString() : "";
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null)
            return new { error = $"Animation clip not found at '{path}'" };

        string relativePath = args.ContainsKey("relativePath") ? args["relativePath"].ToString() : "";
        string propertyName = args.ContainsKey("propertyName") ? args["propertyName"].ToString() : "";

        if (string.IsNullOrEmpty(propertyName))
            return new { error = "propertyName is required" };
        if (!args.ContainsKey("time") || !args.ContainsKey("value"))
            return new { error = "time and value are required" };

        float time = Convert.ToSingle(args["time"]);
        float value = Convert.ToSingle(args["value"]);

        // Find existing curve binding
        var bindings = AnimationUtility.GetCurveBindings(clip);
        EditorCurveBinding? targetBinding = null;

        foreach (var binding in bindings)
        {
            if (binding.propertyName == propertyName && binding.path == relativePath)
            {
                targetBinding = binding;
                break;
            }
        }

        AnimationCurve curve;
        EditorCurveBinding curveBinding;

        if (targetBinding.HasValue)
        {
            curveBinding = targetBinding.Value;
            curve = AnimationUtility.GetEditorCurve(clip, curveBinding);
        }
        else
        {
            // Create new curve binding
            string typeName = args.ContainsKey("type") ? args["type"].ToString() : "Transform";
            Type type = Type.GetType($"UnityEngine.{typeName}, UnityEngine") ??
                        Type.GetType($"UnityEngine.{typeName}, UnityEngine.CoreModule") ??
                        typeof(Transform);
            curveBinding = EditorCurveBinding.FloatCurve(relativePath, type, propertyName);
            curve = new AnimationCurve();
        }

        // Create keyframe with full tangent control
        var keyframe = new Keyframe(time, value);
        if (args.ContainsKey("inTangent"))
            keyframe.inTangent = Convert.ToSingle(args["inTangent"]);
        if (args.ContainsKey("outTangent"))
            keyframe.outTangent = Convert.ToSingle(args["outTangent"]);
        if (args.ContainsKey("inWeight"))
            keyframe.inWeight = Convert.ToSingle(args["inWeight"]);
        if (args.ContainsKey("outWeight"))
            keyframe.outWeight = Convert.ToSingle(args["outWeight"]);
        if (args.ContainsKey("weightedMode"))
        {
            WeightedMode wm;
            if (Enum.TryParse(args["weightedMode"].ToString(), true, out wm))
                keyframe.weightedMode = wm;
        }

        int idx = curve.AddKey(keyframe);

        AnimationUtility.SetEditorCurve(clip, curveBinding, curve);
        EditorUtility.SetDirty(clip);
        AssetDatabase.SaveAssets();

        return new Dictionary<string, object>
        {
            { "success", true },
            { "clipPath", path },
            { "propertyName", propertyName },
            { "keyframeIndex", idx },
            { "time", time },
            { "value", value },
            { "totalKeyframes", curve.keys.Length },
        };
    }

    public static object RemoveKeyframe(Dictionary<string, object> args)
    {
        string path = args.ContainsKey("clipPath") ? args["clipPath"].ToString() : "";
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null)
            return new { error = $"Animation clip not found at '{path}'" };

        string relativePath = args.ContainsKey("relativePath") ? args["relativePath"].ToString() : "";
        string propertyName = args.ContainsKey("propertyName") ? args["propertyName"].ToString() : "";
        int keyIndex = args.ContainsKey("keyframeIndex") ? Convert.ToInt32(args["keyframeIndex"]) : -1;

        if (string.IsNullOrEmpty(propertyName))
            return new { error = "propertyName is required" };
        if (keyIndex < 0)
            return new { error = "keyframeIndex is required (0-based)" };

        var bindings = AnimationUtility.GetCurveBindings(clip);
        EditorCurveBinding? targetBinding = null;
        foreach (var binding in bindings)
        {
            if (binding.propertyName == propertyName && binding.path == relativePath)
            {
                targetBinding = binding;
                break;
            }
        }

        if (!targetBinding.HasValue)
            return new { error = $"Curve not found for property '{propertyName}'" };

        var curve = AnimationUtility.GetEditorCurve(clip, targetBinding.Value);
        if (keyIndex >= curve.keys.Length)
            return new { error = $"Keyframe index {keyIndex} out of range (count: {curve.keys.Length})" };

        curve.RemoveKey(keyIndex);
        AnimationUtility.SetEditorCurve(clip, targetBinding.Value, curve);
        EditorUtility.SetDirty(clip);
        AssetDatabase.SaveAssets();

        return new { success = true, removedIndex = keyIndex, remainingKeyframes = curve.keys.Length };
    }

    // ─── Animation Events ───

    public static object AddAnimationEvent(Dictionary<string, object> args)
    {
        string path = args.ContainsKey("clipPath") ? args["clipPath"].ToString() : "";
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null)
            return new { error = $"Animation clip not found at '{path}'" };

        if (!args.ContainsKey("time") || !args.ContainsKey("functionName"))
            return new { error = "time and functionName are required" };

        var evt = new AnimationEvent();
        evt.time = Convert.ToSingle(args["time"]);
        evt.functionName = args["functionName"].ToString();

        if (args.ContainsKey("stringParameter"))
            evt.stringParameter = args["stringParameter"].ToString();
        if (args.ContainsKey("intParameter"))
            evt.intParameter = Convert.ToInt32(args["intParameter"]);
        if (args.ContainsKey("floatParameter"))
            evt.floatParameter = Convert.ToSingle(args["floatParameter"]);

        var events = clip.events.ToList();
        events.Add(evt);
        AnimationUtility.SetAnimationEvents(clip, events.ToArray());

        EditorUtility.SetDirty(clip);
        AssetDatabase.SaveAssets();

        return new Dictionary<string, object>
        {
            { "success", true },
            { "clipPath", path },
            { "functionName", evt.functionName },
            { "time", evt.time },
            { "totalEvents", clip.events.Length },
        };
    }

    public static object RemoveAnimationEvent(Dictionary<string, object> args)
    {
        string path = args.ContainsKey("clipPath") ? args["clipPath"].ToString() : "";
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null)
            return new { error = $"Animation clip not found at '{path}'" };

        int eventIndex = args.ContainsKey("eventIndex") ? Convert.ToInt32(args["eventIndex"]) : -1;
        if (eventIndex < 0)
            return new { error = "eventIndex is required (0-based)" };

        var events = clip.events.ToList();
        if (eventIndex >= events.Count)
            return new { error = $"Event index {eventIndex} out of range (count: {events.Count})" };

        string removedName = events[eventIndex].functionName;
        events.RemoveAt(eventIndex);
        AnimationUtility.SetAnimationEvents(clip, events.ToArray());

        EditorUtility.SetDirty(clip);
        AssetDatabase.SaveAssets();

        return new { success = true, removedFunction = removedName, remainingEvents = clip.events.Length };
    }

    public static object GetAnimationEvents(Dictionary<string, object> args)
    {
        string path = args.ContainsKey("clipPath") ? args["clipPath"].ToString() : "";
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null)
            return new { error = $"Animation clip not found at '{path}'" };

        var events = new List<Dictionary<string, object>>();
        for (int i = 0; i < clip.events.Length; i++)
        {
            var evt = clip.events[i];
            events.Add(new Dictionary<string, object>
            {
                { "index", i },
                { "time", evt.time },
                { "functionName", evt.functionName },
                { "stringParameter", evt.stringParameter },
                { "intParameter", evt.intParameter },
                { "floatParameter", evt.floatParameter },
            });
        }

        return new Dictionary<string, object>
        {
            { "clipPath", path },
            { "eventCount", events.Count },
            { "events", events.ToArray() },
        };
    }

    // ─── Clip Settings ───

    public static object SetClipSettings(Dictionary<string, object> args)
    {
        string path = args.ContainsKey("clipPath") ? args["clipPath"].ToString() : "";
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null)
            return new { error = $"Animation clip not found at '{path}'" };

        var settings = AnimationUtility.GetAnimationClipSettings(clip);

        if (args.ContainsKey("loopTime"))
            settings.loopTime = Convert.ToBoolean(args["loopTime"]);
        if (args.ContainsKey("loopBlend"))
            settings.loopBlend = Convert.ToBoolean(args["loopBlend"]);
        if (args.ContainsKey("loopBlendOrientation"))
            settings.loopBlendOrientation = Convert.ToBoolean(args["loopBlendOrientation"]);
        if (args.ContainsKey("loopBlendPositionY"))
            settings.loopBlendPositionY = Convert.ToBoolean(args["loopBlendPositionY"]);
        if (args.ContainsKey("loopBlendPositionXZ"))
            settings.loopBlendPositionXZ = Convert.ToBoolean(args["loopBlendPositionXZ"]);
        if (args.ContainsKey("keepOriginalOrientation"))
            settings.keepOriginalOrientation = Convert.ToBoolean(args["keepOriginalOrientation"]);
        if (args.ContainsKey("keepOriginalPositionY"))
            settings.keepOriginalPositionY = Convert.ToBoolean(args["keepOriginalPositionY"]);
        if (args.ContainsKey("keepOriginalPositionXZ"))
            settings.keepOriginalPositionXZ = Convert.ToBoolean(args["keepOriginalPositionXZ"]);
        if (args.ContainsKey("mirror"))
            settings.mirror = Convert.ToBoolean(args["mirror"]);
        if (args.ContainsKey("startTime"))
            settings.startTime = Convert.ToSingle(args["startTime"]);
        if (args.ContainsKey("stopTime"))
            settings.stopTime = Convert.ToSingle(args["stopTime"]);
        if (args.ContainsKey("level"))
            settings.level = Convert.ToSingle(args["level"]);

        AnimationUtility.SetAnimationClipSettings(clip, settings);

        if (args.ContainsKey("frameRate"))
            clip.frameRate = Convert.ToSingle(args["frameRate"]);

        EditorUtility.SetDirty(clip);
        AssetDatabase.SaveAssets();

        return new Dictionary<string, object>
        {
            { "success", true },
            { "clipPath", path },
            { "loopTime", settings.loopTime },
            { "loopBlend", settings.loopBlend },
            { "startTime", settings.startTime },
            { "stopTime", settings.stopTime },
            { "frameRate", clip.frameRate },
        };
    }

    // ─── Transition Management ───


    private static Type ResolveUnityType(string typeName, Type fallback = null)
    {
        if (string.IsNullOrEmpty(typeName))
            return fallback;

        Type type = Type.GetType(typeName)
                    ?? Type.GetType($"UnityEngine.{typeName}, UnityEngine")
                    ?? Type.GetType($"UnityEngine.{typeName}, UnityEngine.CoreModule")
                    ?? Type.GetType($"UnityEngine.UI.{typeName}, UnityEngine.UI");
        if (type != null)
            return type;

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                type = assembly.GetType(typeName);
                if (type != null)
                    return type;

                type = assembly.GetTypes().FirstOrDefault(t => t.Name == typeName);
                if (type != null)
                    return type;
            }
            catch (ReflectionTypeLoadException e)
            {
                type = e.Types.FirstOrDefault(t => t != null && t.Name == typeName);
                if (type != null)
                    return type;
            }
        }

        return fallback;
    }

    private static Dictionary<string, object> GetObjectReferenceKeyframeInfo(int index, ObjectReferenceKeyframe keyframe)
    {
        var value = keyframe.value;
        string assetPath = value != null ? AssetDatabase.GetAssetPath(value) : null;
        string guid = null;
        long localFileId = 0;

        if (value != null)
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(value, out guid, out localFileId);

        return new Dictionary<string, object>
        {
            { "index", index },
            { "time", keyframe.time },
            { "objectName", value != null ? value.name : null },
            { "objectType", value != null ? value.GetType().Name : null },
            { "assetPath", string.IsNullOrEmpty(assetPath) ? null : assetPath },
            { "guid", guid },
            { "localFileId", localFileId },
        };
    }

    private static UnityEngine.Object ResolveObjectReferenceKeyframeValue(
        Dictionary<string, object> keyframe, Type bindingType, string propertyName)
    {
        string unsupportedKey = keyframe.Keys.FirstOrDefault(key =>
            key != "time" && key != "assetPath" && key != "assetName" &&
            key != "objectType");
        if (!string.IsNullOrEmpty(unsupportedKey))
        {
            throw new ArgumentException(
                $"Unsupported object-reference keyframe field '{unsupportedKey}'. " +
                "Use time plus assetPath, assetName, and objectType.");
        }

        if (!keyframe.ContainsKey("assetPath"))
            return null;

        string assetPath = keyframe["assetPath"]?.ToString() ?? "";
        string assetName = keyframe.ContainsKey("assetName")
            ? keyframe["assetName"].ToString()
            : null;
        string objectTypeName = keyframe.ContainsKey("objectType")
            ? keyframe["objectType"].ToString()
            : null;

        Type objectType = ResolveUnityType(objectTypeName, GetExpectedObjectReferenceType(bindingType, propertyName));
        return LoadObjectReferenceAsset(assetPath, assetName, objectType);
    }

    private static Type GetExpectedObjectReferenceType(Type bindingType, string propertyName)
    {
        if (bindingType == typeof(SpriteRenderer) && propertyName == "m_Sprite")
            return typeof(Sprite);

        return typeof(UnityEngine.Object);
    }

    private static UnityEngine.Object LoadObjectReferenceAsset(string assetPath, string assetName, Type objectType)
    {
        if (string.IsNullOrEmpty(assetPath))
            return null;

        objectType = objectType ?? typeof(UnityEngine.Object);

        if (!string.IsNullOrEmpty(assetName))
        {
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(assetPath))
            {
                if (asset != null && objectType.IsAssignableFrom(asset.GetType()) && asset.name == assetName)
                    return asset;
            }

            throw new InvalidOperationException($"Could not find '{assetName}' of type '{objectType.Name}' at '{assetPath}'");
        }

        var mainAsset = AssetDatabase.LoadAssetAtPath(assetPath, objectType);
        if (mainAsset != null)
            return mainAsset;

        var assets = AssetDatabase.LoadAllAssetsAtPath(assetPath)
            .Where(asset => asset != null && objectType.IsAssignableFrom(asset.GetType()))
            .ToArray();
        if (assets.Length == 1)
            return assets[0];
        if (assets.Length > 1)
            throw new InvalidOperationException($"Multiple assets of type '{objectType.Name}' found at '{assetPath}'. Provide assetName.");

        throw new InvalidOperationException($"Could not load object reference asset at '{assetPath}'");
    }
    }
}
