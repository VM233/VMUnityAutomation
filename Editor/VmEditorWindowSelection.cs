using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static VMUnityAutomation.Editor.VmAutomationUICommandArguments;

namespace VMUnityAutomation.Editor
{
    internal static class VmEditorWindowSelection
    {
        internal static EditorWindow Resolve(Dictionary<string, object> args,
            out string error, out string errorCode)
        {
            error = "";
            errorCode = "";
            string query = GetString(args, "window");
            string alias = GetString(args, "typeOrTitle");
            if (query.Length != 0 && alias.Length != 0 &&
                !string.Equals(query, alias, StringComparison.OrdinalIgnoreCase))
                return Fail("window and typeOrTitle must identify the same target.",
                    "invalid_arguments", out error, out errorCode);
            if (query.Length == 0) query = alias;
            string type = GetString(args, "windowType");
            string typeAlias = GetString(args, "type");
            if (type.Length != 0 && typeAlias.Length != 0 &&
                !string.Equals(type, typeAlias, StringComparison.OrdinalIgnoreCase))
                return Fail("windowType and type must identify the same target.",
                    "invalid_arguments", out error, out errorCode);
            if (type.Length == 0) type = typeAlias;
            string title = GetString(args, "title");
            object identity = null;
            if (args != null && args.ContainsKey("instanceId") &&
                !TryGetObjectId(args, "instanceId", out identity))
                return Fail("instanceId must be a nonzero decimal Unity object identity string.",
                    "invalid_arguments", out error, out errorCode);
            if (identity == null && IsObjectIdString(query))
            {
                identity = query;
                query = "";
            }
            if (identity != null)
            {
                var window = VmObjectId.ToObject(identity) as EditorWindow;
                if (window == null)
                    return Fail($"EditorWindow instanceId '{identity}' was not found.",
                        "editor_window_not_found", out error, out errorCode);
                if (!Matches(window, query, type, title, true))
                    return Fail($"EditorWindow instanceId '{identity}' does not match the supplied selectors.",
                        "editor_window_selector_mismatch", out error, out errorCode);
                return window;
            }
            if (query.Length == 0 && type.Length == 0 && title.Length == 0)
            {
                var focused = EditorWindow.focusedWindow;
                return focused != null ? focused : Fail("No focused EditorWindow. Pass instanceId, window, windowType, or title.",
                    "editor_window_not_found", out error, out errorCode);
            }
            return Select(Resources.FindObjectsOfTypeAll<EditorWindow>(), query, type,
                title, out error, out errorCode);
        }

        internal static EditorWindow Select(IReadOnlyList<EditorWindow> windows,
            string query, string type, string title, out string error, out string errorCode)
        {
            error = "";
            errorCode = "";
            // Exact and partial matching are the declared selector modes. Every match
            // in a mode participates; title or full type never wins by enumeration order.
            for (int pass = 0; pass != 2; ++pass)
            {
                EditorWindow selected = null;
                int count = 0;
                for (int index = 0; index < windows.Count; ++index)
                {
                    var window = windows[index];
                    if (window == null || !Matches(window, query, type, title, pass == 0)) continue;
                    selected = window;
                    ++count;
                }
                if (count == 1) return selected;
                if (count > 1)
                    return Fail($"Ambiguous EditorWindow selector: {count} windows match window='{query}', windowType='{type}', title='{title}'. Pass instanceId from uitoolkit/windows.",
                        "editor_window_ambiguous", out error, out errorCode);
            }
            return Fail($"No EditorWindow matched window='{query}', windowType='{type}', title='{title}'.",
                "editor_window_not_found", out error, out errorCode);
        }

        private static bool Matches(EditorWindow window, string query, string type,
            string title, bool exact)
        {
            string actualTitle = window.titleContent?.text ?? "";
            string simpleType = window.GetType().Name;
            string fullType = window.GetType().FullName ?? "";
            return (query.Length == 0 || Match(actualTitle, query, exact) ||
                    Match(simpleType, query, exact) || Match(fullType, query, exact)) &&
                (type.Length == 0 || Match(simpleType, type, exact) || Match(fullType, type, exact)) &&
                (title.Length == 0 || Match(actualTitle, title, exact));
        }

        private static bool Match(string value, string query, bool exact) => exact
            ? string.Equals(value, query, StringComparison.OrdinalIgnoreCase)
            : value.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;

        private static EditorWindow Fail(string message, string code,
            out string error, out string errorCode)
        {
            error = message;
            errorCode = code;
            return null;
        }
    }
}
