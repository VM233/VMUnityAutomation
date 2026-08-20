using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace VMUnityAutomation.Editor
{
    public static class MCPSelectionCommands
    {
        public static object GetSelection(Dictionary<string, object> args)
        {
            var selected = new List<Dictionary<string, object>>();
            foreach (var obj in Selection.gameObjects)
            {
                selected.Add(new Dictionary<string, object>
                {
                    { "name", obj.name },
                    { "instanceId", VmObjectId.Get(obj) },
                    { "path", MCPGameObjectCommands.GetHierarchyPath(obj) },
                });
            }

            return new Dictionary<string, object>
            {
                { "count", selected.Count },
                { "selected", selected },
                { "activeObject", Selection.activeGameObject != null ? Selection.activeGameObject.name : null },
            };
        }

        public static object SetSelection(Dictionary<string, object> args)
        {
            var gameObjects = new List<GameObject>();

            if (args.ContainsKey("paths"))
            {
                var paths = args["paths"] as List<object>;
                if (paths != null)
                {
                    foreach (var p in paths)
                    {
                        var go = GameObject.Find(p.ToString());
                        if (go != null) gameObjects.Add(go);
                    }
                }
            }

            if (args.ContainsKey("path"))
            {
                var go = GameObject.Find(args["path"].ToString());
                if (go != null) gameObjects.Add(go);
            }

            if (args.ContainsKey("instanceId"))
            {
                var go = VmObjectId.ToObject(args["instanceId"]) as GameObject;
                if (go != null) gameObjects.Add(go);
            }

            Selection.objects = gameObjects.Cast<UnityEngine.Object>().ToArray();
            if (gameObjects.Count > 0)
                Selection.activeGameObject = gameObjects[0];

            return new Dictionary<string, object>
            {
                { "success", true },
                { "selectedCount", gameObjects.Count },
            };
        }

        public static object FocusSceneView(Dictionary<string, object> args)
        {
            var go = MCPGameObjectCommands.FindGameObject(args);

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
                sceneView.pivot = MCPGameObjectCommands.DictToVector3(args["position"] as Dictionary<string, object>);
            }

            if (args.ContainsKey("rotation"))
            {
                var euler = MCPGameObjectCommands.DictToVector3(args["rotation"] as Dictionary<string, object>);
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
                { "pivot", MCPGameObjectCommands.Vector3ToDict(sceneView.pivot) },
                { "rotation", MCPGameObjectCommands.Vector3ToDict(sceneView.rotation.eulerAngles) },
                { "size", sceneView.size },
                { "orthographic", sceneView.orthographic },
            };
        }

    }
}
