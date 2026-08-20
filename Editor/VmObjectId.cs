using System.Globalization;
using UnityEngine;
using UnityEditor;

#if !UNITY_6000_5_OR_NEWER
// The legacy APIs are the deliberate compatibility path through Unity 6.4.
// Unity 6.5 turns them into errors, where the EntityId branch below takes over.
#pragma warning disable 0618
#endif

namespace VMUnityAutomation.Editor
{
    /// <summary>
    /// Cross-version object-identity helpers.
    ///
    /// Unity 6.5 (6000.5) deprecates the legacy InstanceID APIs (CS0619) in favour
    /// of EntityId, and EntityId's int cast is itself deprecated — entity ids no
    /// longer fit in an int. On 6.5 the raw id is a 64-bit value (~5.7e17) that
    /// exceeds JavaScript's safe-integer range (2^53), so it cannot travel as a
    /// JSON number through CLI serialization without precision loss.
    ///
    /// Ids are therefore exposed as decimal STRINGS. On 6.5 the value is
    /// EntityId.ToULong(); pre-6.5 (down to the supported 2021.3 LTS) it is the
    /// classic int InstanceID. The JSON "instanceId" wire field is a string on
    /// every Unity version.
    /// </summary>
    public static class VmObjectId
    {
        /// <summary>Stable per-object id as a decimal string (JSON "instanceId" wire field).</summary>
        public static string Get(Object obj)
        {
#if UNITY_6000_5_OR_NEWER
            return EntityId.ToULong(obj.GetEntityId())
                .ToString(CultureInfo.InvariantCulture);
#else
            return obj.GetInstanceID()
                .ToString(CultureInfo.InvariantCulture);
#endif
        }

        /// <summary>
        /// Resolve the object previously identified by <see cref="Get"/>. Accepts the
        /// wire value (string) or a boxed numeric id; returns null if unresolvable.
        /// </summary>
        public static Object ToObject(object id)
        {
            if (id == null) return null;
            string s = id as string ?? id.ToString();
#if UNITY_6000_5_OR_NEWER
            return ulong.TryParse(s, out var raw)
                ? EditorUtility.EntityIdToObject(EntityId.FromULong(raw))
                : null;
#else
            return int.TryParse(s, out var iid)
                ? EditorUtility.InstanceIDToObject(iid)
                : null;
#endif
        }

        /// <summary>Whether the asset preview for the given object is still loading.</summary>
        public static bool IsLoadingPreview(Object obj)
        {
#if UNITY_6000_5_OR_NEWER
            return AssetPreview.IsLoadingAssetPreview(obj.GetEntityId());
#else
            return AssetPreview.IsLoadingAssetPreview(obj.GetInstanceID());
#endif
        }

        /// <summary>Whether a SerializedProperty currently holds a non-null object reference.</summary>
        public static bool HasObjectRef(SerializedProperty sp)
        {
#if UNITY_6000_5_OR_NEWER
            return sp.objectReferenceEntityIdValue != EntityId.None;
#else
            return sp.objectReferenceInstanceIDValue != 0;
#endif
        }
    }
}

#if !UNITY_6000_5_OR_NEWER
#pragma warning restore 0618
#endif
