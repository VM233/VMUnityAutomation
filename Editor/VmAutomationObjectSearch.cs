using System;
using UnityEngine;

namespace VMUnityAutomation.Editor
{
    /// <summary>
    /// Keeps scene-object discovery compatible with the Unity 2021.3 minimum
    /// without publishing obsolete FindObjectsSortMode warnings in newer Editors.
    /// Callers that need deterministic ordering must sort their results explicitly.
    /// </summary>
    internal static class VmAutomationObjectSearch
    {
        public static T[] Find<T>(bool includeInactive = false) where T : UnityEngine.Object
        {
#if UNITY_6000_4_OR_NEWER
            return UnityEngine.Object.FindObjectsByType<T>(
                includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude);
#elif UNITY_2022_2_OR_NEWER
            return UnityEngine.Object.FindObjectsByType<T>(
                includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
#else
            return UnityEngine.Object.FindObjectsOfType<T>(includeInactive);
#endif
        }

        public static UnityEngine.Object[] Find(Type type, bool includeInactive = false)
        {
            if (type == null)
                return Array.Empty<UnityEngine.Object>();

#if UNITY_6000_4_OR_NEWER
            return UnityEngine.Object.FindObjectsByType(
                type,
                includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude);
#elif UNITY_2022_2_OR_NEWER
            return UnityEngine.Object.FindObjectsByType(
                type,
                includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
#else
            return UnityEngine.Object.FindObjectsOfType(type, includeInactive);
#endif
        }
    }
}
