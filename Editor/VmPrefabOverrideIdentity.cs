using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace VMUnityAutomation.Editor
{
    internal static class VmPrefabOverrideIdentity
    {
        internal static string Capture(PropertyModification modification, GameObject root) =>
            MiniJson.Serialize(new Dictionary<string, object>
            {
                ["target"] = ObjectKey(modification.target, root),
                ["path"] = modification.propertyPath,
                ["value"] = modification.value,
                ["reference"] = ObjectKey(modification.objectReference, root)
            });

        private static string ObjectKey(Object value, GameObject root)
        {
            if (value == null) return "null";
            if (EditorUtility.IsPersistent(value))
            {
                if (!AssetDatabase.TryGetGUIDAndLocalFileIdentifier(value, out string guid, out long localID))
                    throw new InvalidOperationException("Persistent override reference has no native asset identity.");
                return guid + ":" + localID.ToString(CultureInfo.InvariantCulture);
            }
            GameObject host;
            int componentIndex = -1;
            if (value is Component component)
            {
                host = component.gameObject;
                var components = host.GetComponents<Component>();
                if (components.Length > 256)
                    throw new InvalidOperationException("Override reference exceeds the 256-component budget.");
                componentIndex = Array.IndexOf(components, component);
            }
            else if (value is GameObject gameObject) host = gameObject;
            else throw new InvalidOperationException("Override reference is neither an asset nor a local Prefab object.");
            var indices = new List<int>();
            var current = host.transform;
            while (current != root.transform)
            {
                if (current == null || indices.Count == 64)
                    throw new InvalidOperationException("Override reference is outside the selected Prefab or exceeds depth 64.");
                indices.Add(current.GetSiblingIndex());
                current = current.parent;
            }
            indices.Reverse();
            return "local:" + string.Join("/", indices.Select(index => index.ToString(CultureInfo.InvariantCulture))) +
                   ":" + value.GetType().FullName + ":" + componentIndex.ToString(CultureInfo.InvariantCulture);
        }
    }
}
