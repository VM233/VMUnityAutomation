using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace VMUnityAutomation.Editor
{
    public static class VmAutomationGameObjectCommands
    {
        public static object Create(Dictionary<string, object> args)
        {
            string name = args.ContainsKey("name") ? args["name"].ToString() : "New GameObject";
            string primitiveType = args.ContainsKey("primitiveType") ? args["primitiveType"].ToString() : "Empty";

            GameObject go;
            if (primitiveType == "Empty" || string.IsNullOrEmpty(primitiveType))
            {
                go = new GameObject(name);
            }
            else if (Enum.TryParse<PrimitiveType>(primitiveType, out var pType))
            {
                go = GameObject.CreatePrimitive(pType);
                go.name = name;
            }
            else
            {
                return new { error = $"Unknown primitive type: {primitiveType}" };
            }

            // Set parent
            if (args.ContainsKey("parent"))
            {
                var parent = GameObject.Find(args["parent"].ToString());
                if (parent != null) go.transform.SetParent(parent.transform);
            }

            // Set transform
            if (args.ContainsKey("position"))
                go.transform.position = DictToVector3(args["position"] as Dictionary<string, object>);
            if (args.ContainsKey("rotation"))
                go.transform.eulerAngles = DictToVector3(args["rotation"] as Dictionary<string, object>);
            if (args.ContainsKey("scale"))
                go.transform.localScale = ScalarToUniformScale(args["scale"]);

            Undo.RegisterCreatedObjectUndo(go, $"Create {name}");

            var result = new Dictionary<string, object>
            {
                { "success", true },
                { "name", go.name },
                { "instanceId", VmObjectId.Get(go) },
            };
            VmAutomationTransformSerialization.AddWorld(result, go.transform);
            return result;
        }

        public static object Delete(Dictionary<string, object> args)
        {
            var go = FindGameObject(args);
            if (go == null) return new { error = "GameObject not found" };

            string name = go.name;
            Undo.DestroyObjectImmediate(go);
            return new { success = true, deleted = name };
        }

        public static object GetInfo(Dictionary<string, object> args)
        {
            var go = FindGameObject(args);
            if (go == null) return new { error = "GameObject not found" };

            var components = new List<Dictionary<string, object>>();
            foreach (var comp in go.GetComponents<Component>())
            {
                if (!VmAutomationComponentCommands.ShouldIncludeInComponentSummary(comp)) continue;
                components.Add(new Dictionary<string, object>
                {
                    { "type", comp.GetType().Name },
                    { "fullType", comp.GetType().FullName },
                    { "enabled", comp is Behaviour b ? (object)b.enabled : true },
                });
            }

            var children = new List<string>();
            for (int i = 0; i < go.transform.childCount; i++)
                children.Add(go.transform.GetChild(i).name);

            var result = new Dictionary<string, object>
            {
                { "name", go.name },
                { "instanceId", VmObjectId.Get(go) },
                { "active", go.activeSelf },
                { "activeInHierarchy", go.activeInHierarchy },
                { "isStatic", go.isStatic },
                { "tag", go.tag },
                { "layer", LayerMask.LayerToName(go.layer) },
                { "layerIndex", go.layer },
                { "components", components },
                { "children", children },
                { "childCount", go.transform.childCount },
                { "parent", go.transform.parent != null ? go.transform.parent.name : null },
                { "hierarchyPath", GetHierarchyPath(go) },
            };
            VmAutomationTransformSerialization.AddWorld(result, go.transform, includeScale: true);
            VmAutomationTransformSerialization.AddLocal(result, go.transform, scaleKey: "scale");
            return result;
        }

        public static object SetTransform(Dictionary<string, object> args)
        {
            var go = FindGameObject(args);
            if (go == null) return new { error = "GameObject not found" };

            bool local = args.ContainsKey("local") && (bool)args["local"];
            Undo.RecordObject(go.transform, "Set Transform");

            if (args.ContainsKey("position"))
            {
                var v = DictToVector3(args["position"] as Dictionary<string, object>);
                if (local) go.transform.localPosition = v;
                else go.transform.position = v;
            }

            if (args.ContainsKey("rotation"))
            {
                var v = DictToVector3(args["rotation"] as Dictionary<string, object>);
                if (local) go.transform.localEulerAngles = v;
                else go.transform.eulerAngles = v;
            }

            if (args.ContainsKey("scale"))
            {
                go.transform.localScale = ScalarToUniformScale(args["scale"]);
            }

            var result = new Dictionary<string, object>
            {
                { "success", true },
                { "name", go.name },
            };
            VmAutomationTransformSerialization.AddWorld(result, go.transform);
            VmAutomationTransformSerialization.AddVectorIfDifferent(result, "scale", go.transform.localScale, Vector3.one);
            return result;
        }

        public static object Duplicate(Dictionary<string, object> args)
        {
            var go = FindGameObject(args);
            if (go == null)
                return new { error = "GameObject not found" };

            string newName = args.ContainsKey("newName") ? args["newName"].ToString() : go.name + " (Copy)";
            var duplicate = UnityEngine.Object.Instantiate(go);
            duplicate.name = newName;
            if (go.transform.parent != null)
                duplicate.transform.SetParent(go.transform.parent);
            Undo.RegisterCreatedObjectUndo(duplicate, $"Duplicate {go.name}");

            return new Dictionary<string, object>
            {
                { "success", true },
                { "original", go.name },
                { "duplicate", duplicate.name },
                { "instanceId", VmObjectId.Get(duplicate) },
            };
        }

        public static object SetActive(Dictionary<string, object> args)
        {
            var go = FindGameObject(args);
            if (go == null)
                return new { error = "GameObject not found" };

            bool active = args.ContainsKey("active") ? Convert.ToBoolean(args["active"]) : true;
            Undo.RecordObject(go, "Set Active");
            go.SetActive(active);
            return new { success = true, gameObject = go.name, active };
        }

        public static object Reparent(Dictionary<string, object> args)
        {
            var go = FindGameObject(args);
            if (go == null)
                return new { error = "GameObject not found" };

            string parentPath = args.ContainsKey("newParent") ? args["newParent"].ToString() : "";
            bool worldPositionStays = !args.ContainsKey("worldPositionStays") ||
                                      Convert.ToBoolean(args["worldPositionStays"]);
            Undo.SetTransformParent(go.transform,
                string.IsNullOrEmpty(parentPath) ? null : GameObject.Find(parentPath)?.transform,
                worldPositionStays,
                "Reparent");

            return new Dictionary<string, object>
            {
                { "success", true },
                { "gameObject", go.name },
                { "newParent", string.IsNullOrEmpty(parentPath) ? "root" : parentPath },
                { "worldPositionStays", worldPositionStays },
            };
        }

        // ─── Helpers ───

        public static GameObject FindGameObject(Dictionary<string, object> args)
        {
            if (args.ContainsKey("instanceId"))
            {
                return VmObjectId.ToObject(args["instanceId"]) as GameObject;
            }

            if (args.ContainsKey("path") || args.ContainsKey("gameObjectPath"))
            {
                string path = args.ContainsKey("path") ? args["path"].ToString() : args["gameObjectPath"].ToString();
                // Try direct find first (active objects only)
                var go = GameObject.Find(path);
                if (go != null) return go;

                // Fallback: include inactive objects. FindObjectsByType's default
                // (without FindObjectsInactive) excludes inactive, so path-based
                // lookup silently fails for inactive GameObjects. Opt in explicitly.
                var allObjects = VmAutomationObjectSearch.Find<GameObject>(includeInactive: true);
                foreach (var obj in allObjects)
                {
                    if (obj.name == path || GetHierarchyPath(obj) == path)
                        return obj;
                }
            }

            return null;
        }

        public static string GetHierarchyPath(GameObject go)
        {
            string path = go.name;
            var parent = go.transform.parent;
            while (parent != null)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
            }
            return path;
        }

        public static Vector3 DictToVector3(Dictionary<string, object> dict)
        {
            if (dict == null) return Vector3.zero;
            float x = dict.ContainsKey("x") ? Convert.ToSingle(dict["x"]) : 0;
            float y = dict.ContainsKey("y") ? Convert.ToSingle(dict["y"]) : 0;
            float z = dict.ContainsKey("z") ? Convert.ToSingle(dict["z"]) : 0;
            return new Vector3(x, y, z);
        }

        private static Vector3 ScalarToUniformScale(object value)
        {
            return Vector3.one * Convert.ToSingle(value);
        }

        public static Dictionary<string, object> Vector3ToDict(Vector3 v)
        {
            return new Dictionary<string, object> { { "x", v.x }, { "y", v.y }, { "z", v.z } };
        }
    }
}
