using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace VMUnityAutomation.Editor
{
    public static class VmAutomationSelectionCommands
    {
        public static object GetSelection(Dictionary<string, object> args)
        {
            var selected = new List<Dictionary<string, object>>();
            foreach (var obj in Selection.objects)
            {
                if (obj == null) continue;
                string assetPath = AssetDatabase.GetAssetPath(obj);
                selected.Add(new Dictionary<string, object>
                {
                    { "name", obj.name },
                    { "instanceId", VmObjectId.Get(obj) },
                    { "path", !string.IsNullOrEmpty(assetPath)
                        ? assetPath
                        : obj is GameObject gameObject
                            ? VmAutomationGameObjectCommands.GetHierarchyPath(gameObject)
                            : "" },
                });
            }

            return new Dictionary<string, object>
            {
                { "count", selected.Count },
                { "selected", selected },
                { "activeObject", Selection.activeObject != null ? Selection.activeObject.name : null },
            };
        }

        public static object SetSelection(Dictionary<string, object> args)
        {
            var objects = new List<UnityEngine.Object>();

            if (args.ContainsKey("paths"))
            {
                var paths = args["paths"] as List<object>;
                if (paths != null)
                {
                    foreach (var p in paths)
                    {
                        var obj = ResolvePath(p?.ToString());
                        if (obj != null) objects.Add(obj);
                    }
                }
            }

            if (args.ContainsKey("path"))
            {
                var obj = ResolvePath(args["path"]?.ToString());
                if (obj != null) objects.Add(obj);
            }

            if (args.ContainsKey("instanceId"))
            {
                var obj = VmObjectId.ToObject(args["instanceId"]);
                if (obj != null) objects.Add(obj);
            }

            Selection.objects = objects.ToArray();
            Selection.activeObject = objects.Count > 0 ? objects[0] : null;

            return new Dictionary<string, object>
            {
                { "success", true },
                { "selectedCount", objects.Count },
            };
        }

        private static UnityEngine.Object ResolvePath(string path)
        {
            if (string.IsNullOrEmpty(path)) return null;
            string normalized = path.Replace('\\', '/');
            return normalized.StartsWith("Assets/", StringComparison.Ordinal)
                ? AssetDatabase.LoadMainAssetAtPath(normalized)
                : GameObject.Find(path);
        }

        public static object FocusSceneView(Dictionary<string, object> args)
        {
            var go = VmAutomationGameObjectCommands.FindGameObject(args);

            var sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null)
                return new { error = "No active Scene View found" };

            if (go != null)
            {
                Selection.activeGameObject = go;
                sceneView.FrameSelected();
            }

            if (args.ContainsKey("position"))
            {
                sceneView.pivot = VmAutomationGameObjectCommands.DictToVector3(args["position"] as Dictionary<string, object>);
            }

            if (args.ContainsKey("rotation"))
            {
                var euler = VmAutomationGameObjectCommands.DictToVector3(args["rotation"] as Dictionary<string, object>);
                sceneView.rotation = Quaternion.Euler(euler);
            }

            if (args.ContainsKey("size"))
                sceneView.size = Convert.ToSingle(args["size"]);

            if (args.ContainsKey("orthographic"))
                sceneView.orthographic = Convert.ToBoolean(args["orthographic"]);

            sceneView.Repaint();

            return new Dictionary<string, object>
            {
                { "success", true },
                { "pivot", VmAutomationGameObjectCommands.Vector3ToDict(sceneView.pivot) },
                { "rotation", VmAutomationGameObjectCommands.Vector3ToDict(sceneView.rotation.eulerAngles) },
                { "size", sceneView.size },
                { "orthographic", sceneView.orthographic },
            };
        }

    }
}
