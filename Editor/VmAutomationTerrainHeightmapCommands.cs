using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static VMUnityAutomation.Editor.VmAutomationTerrainCommandUtility;

namespace VMUnityAutomation.Editor
{
    public static class VmAutomationTerrainHeightmapCommands
    {
    public static object ImportHeightmap(Dictionary<string, object> args)
    {
        var terrain = FindTerrain(args);
        if (terrain == null) return new { error = "Terrain not found" };

        string path = args.ContainsKey("path") ? args["path"].ToString() : "";
        if (string.IsNullOrEmpty(path))
            return new { error = "path is required (asset path to a Texture2D or absolute path to .raw file)" };

        var data = terrain.terrainData;
        int res = data.heightmapResolution;
        Undo.RecordObject(data, "Import Heightmap");

        if (path.ToLower().EndsWith(".raw"))
        {
            // Import RAW
            string fullPath = path;
            if (!System.IO.Path.IsPathRooted(fullPath))
                fullPath = System.IO.Path.Combine(Application.dataPath, "..", fullPath);

            if (!System.IO.File.Exists(fullPath))
                return new { error = $"File not found: {fullPath}" };

            byte[] rawData = System.IO.File.ReadAllBytes(fullPath);
            bool is16Bit = args.ContainsKey("depth") && args["depth"].ToString() == "16";

            float[,] heights = new float[res, res];
            int idx = 0;

            for (int z = 0; z < res && idx < rawData.Length; z++)
            {
                for (int x = 0; x < res && idx < rawData.Length; x++)
                {
                    if (is16Bit && idx + 1 < rawData.Length)
                    {
                        ushort val = (ushort)(rawData[idx] | (rawData[idx + 1] << 8));
                        heights[z, x] = val / 65535f;
                        idx += 2;
                    }
                    else
                    {
                        heights[z, x] = rawData[idx] / 255f;
                        idx++;
                    }
                }
            }

            data.SetHeights(0, 0, heights);
        }
        else
        {
            // Import from Texture2D
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex == null)
                return new { error = $"Texture not found at '{path}'. Ensure texture is readable." };

            float[,] heights = new float[res, res];
            for (int z = 0; z < res; z++)
            {
                for (int x = 0; x < res; x++)
                {
                    float u = (float)x / (res - 1);
                    float v = (float)z / (res - 1);
                    heights[z, x] = tex.GetPixelBilinear(u, v).grayscale;
                }
            }
            data.SetHeights(0, 0, heights);
        }

        terrain.Flush();

        return new Dictionary<string, object>
        {
            { "success", true },
            { "path", path },
            { "resolution", res },
        };
    }

    /// <summary>Export the heightmap to a RAW file.</summary>
    public static object ExportHeightmap(Dictionary<string, object> args)
    {
        var terrain = FindTerrain(args);
        if (terrain == null) return new { error = "Terrain not found" };

        string path = args.ContainsKey("path") ? args["path"].ToString() : "";
        if (string.IsNullOrEmpty(path))
            return new { error = "path is required" };

        bool is16Bit = !args.ContainsKey("depth") || args["depth"].ToString() == "16";

        var data = terrain.terrainData;
        int res = data.heightmapResolution;
        float[,] heights = data.GetHeights(0, 0, res, res);

        string fullPath = path;
        if (!System.IO.Path.IsPathRooted(fullPath))
            fullPath = System.IO.Path.Combine(Application.dataPath, "..", fullPath);

        string dir = System.IO.Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir))
            System.IO.Directory.CreateDirectory(dir);

        using (var stream = System.IO.File.Create(fullPath))
        {
            for (int z = 0; z < res; z++)
            {
                for (int x = 0; x < res; x++)
                {
                    if (is16Bit)
                    {
                        ushort val = (ushort)(heights[z, x] * 65535);
                        stream.WriteByte((byte)(val & 0xFF));
                        stream.WriteByte((byte)((val >> 8) & 0xFF));
                    }
                    else
                    {
                        stream.WriteByte((byte)(heights[z, x] * 255));
                    }
                }
            }
        }

        return new Dictionary<string, object>
        {
            { "success", true },
            { "path", fullPath },
            { "resolution", res },
            { "depth", is16Bit ? "16-bit" : "8-bit" },
        };
    }

    // ─────────────────────────────────────────────
    //  STEEPNESS & NORMALS
    // ─────────────────────────────────────────────

    /// <summary>Get terrain steepness at a normalized position.</summary>
    public static object GetSteepness(Dictionary<string, object> args)
    {
        var terrain = FindTerrain(args);
        if (terrain == null) return new { error = "Terrain not found" };

        float normX = args.ContainsKey("x") ? Convert.ToSingle(args["x"]) : 0.5f;
        float normZ = args.ContainsKey("z") ? Convert.ToSingle(args["z"]) : 0.5f;

        var data = terrain.terrainData;
        float steepness = data.GetSteepness(normX, normZ);
        Vector3 normal = data.GetInterpolatedNormal(normX, normZ);

        return new Dictionary<string, object>
        {
            { "steepness", steepness },
            { "normal", Vec3Dict(normal) },
            { "x", normX },
            { "z", normZ },
        };
    }

    // ─────────────────────────────────────────────
    //  HELPERS
    // ─────────────────────────────────────────────

    }
}
