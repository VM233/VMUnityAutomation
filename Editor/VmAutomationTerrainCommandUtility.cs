using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace VMUnityAutomation.Editor
{
    internal static class VmAutomationTerrainCommandUtility
    {
    internal static Terrain FindTerrain(Dictionary<string, object> args)
    {
        if (args.ContainsKey("name"))
        {
            var go = GameObject.Find(args["name"].ToString());
            return go != null ? go.GetComponent<Terrain>() : null;
        }

        if (args.ContainsKey("instanceId"))
        {
            var go = VmObjectId.ToObject(args["instanceId"]) as GameObject;
            return go != null ? go.GetComponent<Terrain>() : null;
        }

        return Terrain.activeTerrain;
    }

    internal static Terrain FindTerrainByName(string name)
    {
        var go = GameObject.Find(name);
        return go != null ? go.GetComponent<Terrain>() : null;
    }

    internal static float GetFalloff(float distance, float radius, string type)
    {
        float t = distance / radius;
        switch (type)
        {
            case "linear": return 1f - t;
            case "smooth": return Mathf.SmoothStep(1f, 0f, t);
            case "sharp": return 1f - t * t;
            case "flat": return 1f;
            default: return Mathf.SmoothStep(1f, 0f, t);
        }
    }

    internal static void EnsureDirectoryExists(string assetPath)
    {
        string dir = System.IO.Path.GetDirectoryName(assetPath)?.Replace('\\', '/');
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
    }

    internal static Dictionary<string, object> Vec3Dict(Vector3 v)
    {
        return new Dictionary<string, object> { { "x", v.x }, { "y", v.y }, { "z", v.z } };
    }

    internal static Color ParseColor(Dictionary<string, object> c)
    {
        float r = c.ContainsKey("r") ? Convert.ToSingle(c["r"]) : 1f;
        float g = c.ContainsKey("g") ? Convert.ToSingle(c["g"]) : 1f;
        float b = c.ContainsKey("b") ? Convert.ToSingle(c["b"]) : 1f;
        float a = c.ContainsKey("a") ? Convert.ToSingle(c["a"]) : 1f;
        return new Color(r, g, b, a);
    }
    }
}
