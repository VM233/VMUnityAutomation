using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace VMUnityAutomation.Editor
{
    /// <summary>
    /// Commands for interacting with the Unity Undo system.
    /// </summary>
    public static class MCPUndoCommands
    {
        public static object PerformUndo(Dictionary<string, object> args)
        {
            return MCPRequestUndoCoordinator.PerformUndo(args);
        }

        public static object PerformRedo(Dictionary<string, object> args)
        {
            return MCPRequestUndoCoordinator.PerformRedo(args);
        }

        public static object GetUndoHistory(Dictionary<string, object> args)
        {
            int limit = Math.Max(1, Math.Min(GetInt(args, "limit", 50), 200));
            List<MCPActionRecord> records = MCPActionHistory.GetAll()
                .Where(record => record.RequestId > 0)
                .OrderByDescending(record => record.Timestamp)
                .Take(limit)
                .ToList();
            return new Dictionary<string, object>
            {
                { "success", true },
                { "count", records.Count },
                { "total", MCPActionHistory.Count },
                { "actions", records.Select(record => (object)record.ToDict()).ToList() },
            };
        }

        public static object ClearUndo(Dictionary<string, object> args)
        {
            if (!GetBool(args, "confirm"))
            {
                return MCPResponse.Error(
                    "confirm=true is required because clearing Unity Undo history is global and irreversible.",
                    "confirmation_required");
            }

            string objectPath = GetString(args, "objectPath");
            if (!string.IsNullOrEmpty(objectPath))
            {
                GameObject gameObject = GameObject.Find(objectPath);
                if (gameObject == null)
                    return MCPResponse.Error($"GameObject '{objectPath}' was not found.",
                        "gameobject_not_found");
                Undo.ClearUndo(gameObject);
                MCPRequestUndoCoordinator.InvalidateAll("undo_history_cleared");
                return new Dictionary<string, object>
                {
                    { "success", true },
                    { "scope", "object" },
                    { "objectPath", objectPath },
                };
            }

            Undo.ClearAll();
            MCPRequestUndoCoordinator.InvalidateAll("undo_history_cleared");
            return new Dictionary<string, object>
            {
                { "success", true },
                { "scope", "global" },
            };
        }

        private static string GetString(Dictionary<string, object> values, string key)
        {
            return values != null && values.TryGetValue(key, out object value) && value != null
                ? value.ToString()
                : "";
        }

        private static int GetInt(Dictionary<string, object> values, string key, int fallback)
        {
            return values != null && values.TryGetValue(key, out object value) && value != null &&
                   int.TryParse(value.ToString(), out int result)
                ? result
                : fallback;
        }

        private static bool GetBool(Dictionary<string, object> values, string key)
        {
            if (values == null || !values.TryGetValue(key, out object value) || value == null)
                return false;
            return value is bool result
                ? result
                : bool.TryParse(value.ToString(), out result) && result;
        }
    }
}
