using System;
using System.Collections.Generic;
using System.Linq;

namespace VMUnityAutomation.Editor
{
    /// <summary>
    /// Produces concise intent text for routes that do not need a specialized description.
    /// Both module subjects and action grammar are audited vocabularies; unknown modules fail
    /// instead of publishing a route-name placeholder.
    /// </summary>
    internal static class VmAutomationToolDescriptionComposer
    {
        private static readonly IReadOnlyDictionary<string, string> ModuleSubjects =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "agents", "active Automation agent sessions" },
                { "animation", "Animator controller and animation clip assets" },
                { "asmdef", "Unity assembly definition assets" },
                { "asset", "Unity project assets" },
                { "audio", "scene audio sources and global audio state" },
                { "audio-mixer", "Audio Mixer assets" },
                { "component", "components on loaded GameObjects" },
                { "console", "the Unity Console" },
                { "constraint", "Unity scene constraints" },
                { "debugger", "the attached Unity debugger" },
                { "editor", "Unity Editor state and commands" },
                { "editorprefs", "Unity Editor preferences" },
                { "gameobject", "GameObjects in loaded scenes" },
                { "graphics", "renderers, meshes, materials, lighting, and image evidence" },
                { "input", "Input System action assets" },
                { "lighting", "scene lights, probes, and environment lighting" },
                { "lod", "LOD Groups on scene GameObjects" },
                { "mppm", "Multiplayer Play Mode player instances" },
                { "navigation", "NavMesh data, agents, and obstacles" },
                { "particle", "Particle Systems in loaded scenes" },
                { "physics", "Unity physics queries and global physics state" },
                { "ping", "the VM Unity Automation bridge connection" },
                { "playerprefs", "Unity PlayerPrefs values" },
                { "prefab", "Prefab instances and Prefab assets" },
                { "prefab-asset", "Prefab asset hierarchies and variant overrides" },
                { "project", "the current Unity project" },
                { "renderer", "Renderer components in loaded scenes" },
                { "scenario", "Multiplayer Play Mode scenarios" },
                { "scene", "Unity scenes" },
                { "sceneview", "the Unity Scene view" },
                { "screenshot", "Unity Editor and scene screenshots" },
                { "script", "C# script assets" },
                { "scriptableobject", "ScriptableObject assets and types" },
                { "search", "loaded scene objects and references" },
                { "selection", "the Unity Editor selection" },
                { "settings", "Unity project and runtime settings" },
                { "shadergraph", "Shader Graph and VFX Graph assets" },
                { "spriteatlas", "Sprite Atlas assets" },
                { "taglayer", "Unity tags, layers, and static flags" },
                { "terrain", "TerrainData assets and Terrain objects" },
                { "texture", "texture and Sprite import assets" },
                { "ui", "uGUI objects in loaded scenes" },
                { "undo", "the Unity Editor undo history" },
            };

        private static readonly IReadOnlyDictionary<string, string> TokenLabels =
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { "ai", "AI" },
                { "asmdef", "assembly definition" },
                { "gameobject", "GameObject" },
                { "id", "ID" },
                { "lod", "LOD" },
                { "mppm", "Multiplayer Play Mode" },
                { "ref", "reference" },
                { "spriteatlas", "Sprite Atlas" },
                { "ui", "UI" },
                { "uss", "USS" },
                { "uxml", "UXML" },
                { "vfx", "VFX" },
            };

        internal static string Compose(string route)
        {
            route = (route ?? "").Trim('/');
            int slashIndex = route.IndexOf('/');
            string module = slashIndex > 0 ? route.Substring(0, slashIndex) : route;
            string operation = slashIndex > 0 ? route.Substring(slashIndex + 1) : "status";
            if (!ModuleSubjects.TryGetValue(module, out string subject))
            {
                throw new InvalidOperationException(
                    $"Route '{route}' has no audited description subject for module '{module}'.");
            }

            return ComposeAction(operation, subject);
        }

        private static string ComposeAction(string operation, string subject)
        {
            switch (operation)
            {
                case "info":
                    return $"Inspect {subject}.";
                case "list":
                    return $"List {subject}.";
                case "status":
                    return $"Read the current status of {subject}.";
                case "create":
                    return $"Create {subject}.";
                case "delete":
                    return $"Delete selected {subject}.";
                case "clear":
                    return $"Clear {subject}.";
                case "history":
                    return $"Read {subject} history.";
                case "events":
                    return $"List events from {subject}.";
                case "enable":
                    return $"Enable or disable {subject}.";
                case "playback":
                    return $"Control playback for {subject}.";
                case "open":
                    return $"Open {subject} in its Unity editor.";
                case "bake":
                    return $"Bake {subject}.";
                case "duplicate":
                    return $"Duplicate selected {subject}.";
                case "reparent":
                    return $"Reparent selected {subject}.";
                case "unpack":
                    return $"Unpack selected {subject}.";
                case "flatten":
                    return $"Flatten {subject}.";
                case "noise":
                    return $"Apply procedural noise to {subject}.";
                case "resize":
                    return $"Resize {subject}.";
                case "smooth":
                    return $"Smooth {subject}.";
                case "raise-lower":
                    return $"Raise or lower {subject}.";
                case "scene-stats":
                    return $"Read scene statistics from {subject}.";
            }

            foreach (var prefix in new[]
                     {
                         (Prefix: "get-", Verb: "Read", Joiner: "from"),
                         (Prefix: "set-", Verb: "Set", Joiner: "on"),
                         (Prefix: "add-", Verb: "Add", Joiner: "to"),
                         (Prefix: "remove-", Verb: "Remove", Joiner: "from"),
                         (Prefix: "create-", Verb: "Create", Joiner: "in"),
                         (Prefix: "update-", Verb: "Update", Joiner: "in"),
                         (Prefix: "find-", Verb: "Find", Joiner: "in"),
                         (Prefix: "check-", Verb: "Check", Joiner: "for"),
                         (Prefix: "list-", Verb: "List", Joiner: "for"),
                         (Prefix: "import-", Verb: "Import", Joiner: "into"),
                         (Prefix: "export-", Verb: "Export", Joiner: "from"),
                         (Prefix: "apply-", Verb: "Apply", Joiner: "to"),
                         (Prefix: "revert-", Verb: "Revert", Joiner: "in"),
                         (Prefix: "paint-", Verb: "Paint", Joiner: "on"),
                         (Prefix: "place-", Verb: "Place", Joiner: "on"),
                         (Prefix: "scatter-", Verb: "Scatter", Joiner: "on"),
                         (Prefix: "activate-", Verb: "Activate", Joiner: "in"),
                         (Prefix: "deactivate-", Verb: "Deactivate", Joiner: "in"),
                     })
            {
                if (!operation.StartsWith(prefix.Prefix, StringComparison.Ordinal))
                    continue;
                string target = Humanize(operation.Substring(prefix.Prefix.Length));
                return $"{prefix.Verb} {target} {prefix.Joiner} {subject}.";
            }

            return $"{Capitalize(Humanize(operation))} for {subject}.";
        }

        private static string Humanize(string value)
        {
            return string.Join(" ", value.Split(new[] { '-' },
                    StringSplitOptions.RemoveEmptyEntries)
                .Select(token => TokenLabels.TryGetValue(token, out string label)
                    ? label
                    : token));
        }

        private static string Capitalize(string value)
        {
            return string.IsNullOrEmpty(value)
                ? value
                : char.ToUpperInvariant(value[0]) + value.Substring(1);
        }
    }
}
