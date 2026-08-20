using System;
using System.Collections.Generic;
using System.Linq;

namespace VMUnityAutomation.Editor
{
    /// <summary>
    /// Explicit effect and lifecycle contract for every built-in route.
    /// Missing routes and duplicate declarations are configuration errors; callers never
    /// inherit a guessed mutating profile.
    /// </summary>
    internal static class VmAutomationToolProfileCatalog
    {
        private static readonly IReadOnlyDictionary<string, VmAutomationToolProfile> Profiles = Build();

        internal static VmAutomationToolProfile Get(string route)
        {
            route = (route ?? "").Trim('/');
            if (Profiles.TryGetValue(route, out VmAutomationToolProfile profile))
                return profile;

            int slashIndex = route.IndexOf('/');
            if (slashIndex > 0)
            {
                string familyRoute = route.Substring(0, slashIndex + 1) + "*";
                if (Profiles.TryGetValue(familyRoute, out profile))
                    return profile;
            }

            throw new InvalidOperationException(
                $"Registered route '{route}' does not declare a canonical tool profile.");
        }

        private static IReadOnlyDictionary<string, VmAutomationToolProfile> Build()
        {
            var profiles = new Dictionary<string, VmAutomationToolProfile>(StringComparer.Ordinal);

            Add(profiles, VmAutomationToolProfile.Create(readOnly: true),
                "_meta/capabilities",
                "_meta/tools",
                "context",
                "context/*",
                "ping",
                "agents/list",
                "addressables/info",
                "animation/clip-info",
                "animation/controller-info",
                "animation/get-blend-tree",
                "animation/get-curve-keyframes",
                "animation/get-events",
                "animation/transition-info",
                "animation/validate-controller",
                "asmdef/info",
                "asmdef/list",
                "asset/dependencies",
                "asset/import-settings/get",
                "asset/list",
                "audio/info",
                "audio-mixer/info",
                "build/get-job",
                "cinemachine/info",
                "compilation/errors",
                "component/get-properties",
                "component/get-referenceable",
                "console/query",
                "constraint/info",
                "debug/stack-trace",
                "debug/variables",
                "debugger/event-details",
                "debugger/events",
                "editor/state",
                "editorprefs/get",
                "gameobject/info",
                "gameview/info",
                "graphics/annotate-rects",
                "graphics/asset-preview",
                "graphics/compare-images",
                "graphics/image-alpha-bounds",
                "graphics/lighting-summary",
                "graphics/material-info",
                "graphics/mesh-info",
                "graphics/rect-gap",
                "graphics/renderer-info",
                "input/info",
                "instance/assert-project",
                "instance/current",
                "instance/list",
                "instance/resolve",
                "jobs/get",
                "jobs/list",
                "lighting/info",
                "localization/collections",
                "localization/entries",
                "localization/locales",
                "localization/status",
                "localization/validate",
                "localization/variables",
                "lod/info",
                "material/properties/get",
                "mppm/list-players",
                "navigation/info",
                "packages/info",
                "packages/lint-metas",
                "packages/list",
                "packages/status",
                "particle/info",
                "physics/collision-matrix",
                "physics/overlap-box",
                "physics/overlap-sphere",
                "physics/raycast",
                "playerprefs/get",
                "prefab/info",
                "prefab-asset/compare-variant",
                "prefab-asset/find",
                "prefab-asset/get-properties",
                "prefab-asset/hierarchy",
                "prefab-asset/variant-info",
                "profiler/memory",
                "profiler/memory-snapshot-status",
                "profiler/memory-status",
                "profiler/stats",
                "project/info",
                "queue/info",
                "queue/status",
                "scenario/info",
                "scenario/list",
                "scenario/status",
                "scene/hierarchy",
                "scene/info",
                "sceneview/info",
                "screenshot/crop",
                "screenshot/editor-window",
                "screenshot/scene",
                "script/read",
                "scriptableobject/info",
                "scriptableobject/list-types",
                "search/missing-references",
                "search/scene",
                "search/scene-stats",
                "selection/get",
                "serialized-object/get",
                "settings/physics",
                "settings/player",
                "settings/quality",
                "settings/render-pipeline",
                "settings/time",
                "shadergraph/get-edges",
                "shadergraph/get-node-types",
                "shadergraph/get-nodes",
                "shadergraph/get-properties",
                "shadergraph/info",
                "shadergraph/list",
                "shadergraph/list-shaders",
                "shadergraph/list-subgraphs",
                "shadergraph/list-vfx",
                "shadergraph/status",
                "sprite/pixel-check",
                "sprite/sheet-info",
                "spriteatlas/info",
                "spriteatlas/list",
                "taglayer/info",
                "terrain/export-heightmap",
                "terrain/get-height",
                "terrain/get-heights-region",
                "terrain/get-steepness",
                "terrain/get-tree-instances",
                "terrain/info",
                "terrain/list",
                "testing/get-job",
                "testing/get-package-job",
                "texture/check-import-settings",
                "texture/check-ui-import-settings",
                "texture/find-duplicates",
                "texture/info",
                "timeline/info",
                "ui/info",
                "uitoolkit/assert-layout",
                "uitoolkit/asset-inspect",
                "uitoolkit/capture-element",
                "uitoolkit/compare-element",
                "uitoolkit/diagnose-runtime",
                "uitoolkit/generated-children",
                "uitoolkit/locate-element",
                "uitoolkit/query",
                "uitoolkit/resource-audit",
                "uitoolkit/runtime-documents",
                "uitoolkit/runtime-query",
                "uitoolkit/runtime-style",
                "uitoolkit/runtime-tree",
                "uitoolkit/style",
                "uitoolkit/tree",
                "uitoolkit/visual-check",
                "uitoolkit/windows",
                "undo/history",
                "vfxgraph/catalog",
                "vfxgraph/component-info",
                "vfxgraph/info",
                "vfxgraph/settings-info");

            Add(profiles, VmAutomationToolProfile.Create(readOnly: true, longRunning: true),
                "packages/search",
                "profiler/analyze",
                "profiler/frame-data",
                "profiler/memory-breakdown",
                "profiler/memory-top-assets",
                "project-auditor/audit",
                "testing/list-tests",
                "uitoolkit/audit-uss-styles",
                "uitoolkit/audit-uxml-layout",
                "uitoolkit/builder-preview",
                "wait/editor-idle");

            Add(profiles, VmAutomationToolProfile.Create(readOnly: true, longRunning: true,
                    requiresPlayMode: true),
                "screenshot/game");

            Add(profiles, VmAutomationToolProfile.Create(),
                "agents/log",
                "console/clear",
                "debug/attach-unity",
                "debug/evaluate",
                "debug/set-breakpoint",
                "debugger/enable",
                "editorprefs/delete",
                "editorprefs/set",
                "gameview/set-resolution",
                "gameview/set-scale",
                "jobs/cancel",
                "playerprefs/delete",
                "playerprefs/set",
                "queue/cancel",
                "sceneview/set-camera",
                "selection/focus-scene-view",
                "selection/set",
                "shadergraph/open",
                "shadergraph/open-vfx",
                "uitoolkit/repaint",
                "undo/clear");

            Add(profiles, VmAutomationToolProfile.Create(dangerous: true),
                "playerprefs/delete-all");

            Add(profiles, VmAutomationToolProfile.Create(longRunning: true),
                "build/start",
                "testing/run-tests");

            Add(profiles, VmAutomationToolProfile.Create(mutatesAssets: true),
                "animation/add-event",
                "animation/add-keyframe",
                "animation/add-layer",
                "animation/add-parameter",
                "animation/add-state",
                "animation/add-transition",
                "animation/connect-states",
                "animation/create-blend-tree",
                "animation/create-clip",
                "animation/create-controller",
                "animation/remove-curve",
                "animation/remove-event",
                "animation/remove-keyframe",
                "animation/remove-layer",
                "animation/remove-parameter",
                "animation/remove-state",
                "animation/remove-transition",
                "animation/set-clip-curve",
                "animation/set-clip-settings",
                "animation/set-object-reference-curve",
                "animation/update-state",
                "animation/update-transition",
                "asset/copy",
                "asset/create-folder",
                "asset/create-material",
                "asset/create-prefab",
                "asset/export-unitypackage",
                "asset/import",
                "asset/import-settings/set",
                "asset/move",
                "asset/rename",
                "input/add-action",
                "input/add-binding",
                "input/add-composite-binding",
                "input/add-map",
                "input/create",
                "input/remove-action",
                "input/remove-map",
                "localization/create-collection",
                "localization/create-locale",
                "localization/remove-entry",
                "localization/remove-variable",
                "localization/settings",
                "localization/upsert-entry",
                "localization/upsert-variable",
                "material/properties/set",
                "prefab/apply-overrides",
                "prefab/create-variant",
                "prefab-asset/add-component",
                "prefab-asset/add-gameobject",
                "prefab-asset/apply-variant-override",
                "prefab-asset/cleanup-missing-overrides",
                "prefab-asset/configure-component",
                "prefab-asset/instantiate-child-prefab",
                "prefab-asset/move-component",
                "prefab-asset/move-gameobject",
                "prefab-asset/remove-component",
                "prefab-asset/remove-gameobject",
                "prefab-asset/revert-variant-override",
                "prefab-asset/set-property",
                "prefab-asset/set-reference",
                "prefab-asset/transfer-variant-overrides",
                "scenario/create",
                "scene/save",
                "scriptableobject/create",
                "scriptableobject/set-field",
                "serialized-object/set",
                "settings/set-player",
                "shadergraph/add-node",
                "shadergraph/connect",
                "shadergraph/create",
                "shadergraph/disconnect",
                "shadergraph/remove-node",
                "shadergraph/set-node-property",
                "sprite/replace-and-slice",
                "sprite/replace-slice-update-clip",
                "sprite/slice-sheet",
                "sprite/update-animation-clip",
                "spriteatlas/add",
                "spriteatlas/create",
                "spriteatlas/remove",
                "spriteatlas/settings",
                "taglayer/add-tag",
                "taglayer/set-layer",
                "taglayer/set-static",
                "taglayer/set-tag",
                "texture/apply-sprite-preset",
                "texture/import-image",
                "texture/reimport",
                "texture/set-import",
                "uitoolkit/edit-uss",
                "uitoolkit/edit-uxml");

            Add(profiles, VmAutomationToolProfile.Create(mutatesAssets: true, dangerous: true),
                "asset/delete",
                "spriteatlas/delete");

            Add(profiles, VmAutomationToolProfile.Create(mutatesAssets: true,
                    mayReloadDomain: true),
                "asmdef/add-references",
                "asmdef/create",
                "asmdef/create-ref",
                "asmdef/remove-references",
                "asmdef/set-platforms",
                "asmdef/update-settings",
                "build/profile",
                "script/create",
                "script/update");

            Add(profiles, VmAutomationToolProfile.Create(mutatesAssets: true,
                    longRunning: true),
                "addressables/build");

            Add(profiles, VmAutomationToolProfile.Create(mutatesAssets: true,
                    longRunning: true, mayReloadDomain: true),
                "asset/import-unitypackage",
                "asset/refresh",
                "packages/add",
                "packages/remove",
                "packages/resolve",
                "packages/update-git",
                "testing/run-package-tests",
                "uitoolkit/refresh");

            Add(profiles, VmAutomationToolProfile.Create(mutatesRuntime: true),
                "animation/assign-controller",
                "audio/create-source",
                "audio/set-global",
                "component/add",
                "component/remove",
                "component/set-property",
                "component/set-reference",
                "constraint/add",
                "gameobject/create",
                "gameobject/delete",
                "gameobject/duplicate",
                "gameobject/reparent",
                "gameobject/set-active",
                "gameobject/set-transform",
                "lighting/create",
                "lighting/create-light-probe-group",
                "lighting/create-reflection-probe",
                "lighting/set-environment",
                "localization/set-selected-locale",
                "lod/create",
                "mppm/activate-player",
                "mppm/deactivate-player",
                "navigation/add-agent",
                "navigation/add-obstacle",
                "navigation/set-destination",
                "particle/create",
                "particle/playback",
                "particle/set-emission",
                "particle/set-main",
                "particle/set-shape",
                "physics/set-collision-layer",
                "physics/set-gravity",
                "prefab/revert-overrides",
                "prefab/unpack",
                "profiler/enable",
                "renderer/set-material",
                "scenario/activate",
                "scenario/start",
                "scenario/stop",
                "scene/instantiate-prefab",
                "settings/quality-level",
                "settings/set-physics",
                "settings/set-time",
                "ui/create-canvas",
                "ui/create-element",
                "ui/set-image",
                "ui/set-text",
                "uitoolkit/runtime-repaint");

            Add(profiles, VmAutomationToolProfile.Create(mutatesRuntime: true,
                    requiresPlayMode: true),
                "vfxgraph/component-control");

            Add(profiles, VmAutomationToolProfile.Create(mutatesRuntime: true,
                    longRunning: true, mayReloadDomain: true),
                "editor/play-mode");

            Add(profiles, VmAutomationToolProfile.Create(mutatesRuntime: true,
                    longRunning: true),
                "profiler/memory-snapshot");

            Add(profiles, VmAutomationToolProfile.Create(mutatesAssets: true,
                    mutatesRuntime: true),
                "navigation/bake",
                "terrain/add-detail-prototype",
                "terrain/add-layer",
                "terrain/add-tree-prototype",
                "terrain/clear-detail",
                "terrain/clear-trees",
                "terrain/create",
                "terrain/create-grid",
                "terrain/fill-layer",
                "terrain/flatten",
                "terrain/import-heightmap",
                "terrain/noise",
                "terrain/paint-detail",
                "terrain/paint-layer",
                "terrain/place-trees",
                "terrain/raise-lower",
                "terrain/remove-layer",
                "terrain/remove-tree-prototype",
                "terrain/resize",
                "terrain/scatter-detail",
                "terrain/set-height",
                "terrain/set-heights-region",
                "terrain/set-holes",
                "terrain/set-neighbors",
                "terrain/set-settings",
                "terrain/smooth");

            Add(profiles, VmAutomationToolProfile.Create(mutatesAssets: true,
                    mutatesRuntime: true, dangerous: true),
                "editor/execute-menu-item",
                "navigation/clear",
                "scene/new",
                "scene/open",
                "scene/workspace",
                "undo/perform",
                "undo/redo");

            Add(profiles, VmAutomationToolProfile.Create(mutatesAssets: true,
                    mutatesRuntime: true, dangerous: true, longRunning: true),
                "jobs/cleanup");

            Add(profiles, VmAutomationToolProfile.Create(mutatesAssets: true,
                    mutatesRuntime: true, dangerous: true, longRunning: true,
                    mayReloadDomain: true),
                "editor/execute-code");

            Add(profiles, VmAutomationToolProfile.Create(mutatesAssets: true,
                    longRunning: true, mayReloadDomain: true,
                    transaction: VmAutomationTransactionProfile.Create(
                        "asset-files-and-meta", VmTransactionMechanics.Atomicity.VerifiedRollback,
                        VmTransactionMechanics.Isolation.WorkspaceExclusive,
                        VmTransactionMechanics.Durability.ReloadResumableJob,
                        VmTransactionMechanics.RollbackKind.DurableByteSnapshot,
                        "asset-and-meta-sha256",
                        "declared-postconditions", "clean-compilation-for-code-assets")),
                "asset/transaction");

            Add(profiles, VmAutomationToolProfile.Create(mutatesAssets: true,
                    transaction: VmAutomationTransactionProfile.Create(
                        "single-prefab-asset", VmTransactionMechanics.Atomicity.StagedSingleAsset,
                        VmTransactionMechanics.Isolation.RequestOwnedPrefabSession,
                        VmTransactionMechanics.Durability.EditorSession,
                        VmTransactionMechanics.RollbackKind.DiscardUncommittedPrefabSession,
                        "prefab-save-result",
                        "persisted-component-readback")),
                "prefab-asset/transaction-edit");

            Add(profiles, VmAutomationToolProfile.Create(mutatesAssets: true,
                    transaction: VmAutomationTransactionProfile.Create(
                        "addressables-settings", VmTransactionMechanics.Atomicity.BestEffortEditorSession,
                        VmTransactionMechanics.Isolation.RequestUndoGroup,
                        VmTransactionMechanics.Durability.EditorSession,
                        VmTransactionMechanics.RollbackKind.UnityUndo,
                        "asset-save-result")),
                "addressables/transaction");

            Add(profiles, VmAutomationToolProfile.Create(mutatesAssets: true,
                    transaction: VmAutomationTransactionProfile.Create(
                        "single-asset-serialized-graph", VmTransactionMechanics.Atomicity.BestEffortEditorSession,
                        VmTransactionMechanics.Isolation.RequestUndoGroup,
                        VmTransactionMechanics.Durability.EditorSession,
                        VmTransactionMechanics.RollbackKind.UnityUndo,
                        "asset-save-result")),
                "timeline/transaction");

            Add(profiles, VmAutomationToolProfile.Create(mutatesAssets: true,
                    transaction: VmAutomationTransactionProfile.Create(
                        "single-vfx-graph", VmTransactionMechanics.Atomicity.VerifiedSingleAssetRollback,
                        VmTransactionMechanics.Isolation.RequestSerialized,
                        VmTransactionMechanics.Durability.EditorSession,
                        VmTransactionMechanics.RollbackKind.AtomicByteSnapshot,
                        "asset-and-meta-sha256", "persisted-semantic-readback")),
                "vfxgraph/transaction");

            Add(profiles, VmAutomationToolProfile.Create(mutatesAssets: true,
                    transaction: VmAutomationTransactionProfile.Create(
                        "single-vfx-output", VmTransactionMechanics.Atomicity.VerifiedFileRollback,
                        VmTransactionMechanics.Isolation.RequestSerialized,
                        VmTransactionMechanics.Durability.EditorSession,
                        VmTransactionMechanics.RollbackKind.AtomicByteSnapshot,
                        "asset-and-meta-sha256", "imported-type-readback")),
                "vfxgraph/create");

            Add(profiles, VmAutomationToolProfile.Create(mutatesAssets: true,
                    longRunning: true,
                    transaction: VmAutomationTransactionProfile.Create(
                        "single-vfx-bake-output", VmTransactionMechanics.Atomicity.VerifiedFileRollback,
                        VmTransactionMechanics.Isolation.RequestSerialized,
                        VmTransactionMechanics.Durability.EditorSession,
                        VmTransactionMechanics.RollbackKind.AtomicByteSnapshot,
                        "asset-and-meta-sha256", "bake-manifest-readback")),
                "vfxgraph/bake");

            Add(profiles, VmAutomationToolProfile.Create(mutatesAssets: true,
                    longRunning: true),
                "vfxgraph/validate");

            Add(profiles, VmAutomationToolProfile.Create(mutatesAssets: true,
                    mutatesRuntime: true,
                    transaction: VmAutomationTransactionProfile.Create(
                        "single-vfx-component", VmTransactionMechanics.Atomicity.BestEffortEditorSession,
                        VmTransactionMechanics.Isolation.RequestSerialized,
                        VmTransactionMechanics.Durability.EditorSession,
                        VmTransactionMechanics.RollbackKind.UnityUndoOrValueRestore,
                        "component-json-readback", "prefab-or-scene-publication")),
                "vfxgraph/component-transaction");

            Add(profiles, VmAutomationToolProfile.Create(mutatesAssets: true,
                    transaction: VmAutomationTransactionProfile.Create(
                        "vfx-project-and-user-settings", VmTransactionMechanics.Atomicity.BestEffortEditorSession,
                        VmTransactionMechanics.Isolation.RequestSerialized,
                        VmTransactionMechanics.Durability.EditorSession,
                        VmTransactionMechanics.RollbackKind.UnityUndoOrValueRestore,
                        "settings-readback", "explicit-reimport-result")),
                "vfxgraph/settings-transaction");

            Add(profiles, VmAutomationToolProfile.Create(mutatesAssets: true,
                    transaction: VmAutomationTransactionProfile.Create(
                        "uxml-and-uss-files", VmTransactionMechanics.Atomicity.VerifiedFileRollback,
                        VmTransactionMechanics.Isolation.RequestSerialized,
                        VmTransactionMechanics.Durability.EditorSession,
                        VmTransactionMechanics.RollbackKind.InMemoryTextSnapshot,
                        "atomic-file-publication", "asset-import-result")),
                "uitoolkit/authoring-transaction");

            Add(profiles, VmAutomationToolProfile.Create(mutatesAssets: true,
                    transaction: VmAutomationTransactionProfile.Create(
                        "single-tmp-font-asset",
                        VmTransactionMechanics.Atomicity.VerifiedSingleAssetRollback,
                        VmTransactionMechanics.Isolation.RequestSerialized,
                        VmTransactionMechanics.Durability.EditorSession,
                        VmTransactionMechanics.RollbackKind.AtomicByteSnapshot,
                        "asset-and-meta-sha256", "persisted-glyph-readback")),
                "textmeshpro/font-asset/upsert-bitmap-glyphs");

            Add(profiles, VmAutomationToolProfile.Create(mutatesAssets: true,
                    transaction: VmAutomationTransactionProfile.Create(
                        "textcore-sprite-asset-and-atlas",
                        VmTransactionMechanics.Atomicity.VerifiedFileRollback,
                        VmTransactionMechanics.Isolation.RequestSerialized,
                        VmTransactionMechanics.Durability.EditorSession,
                        VmTransactionMechanics.RollbackKind.AtomicByteSnapshot,
                        "asset-and-atlas-sha256", "guid-and-local-id-readback",
                        "persisted-sprite-glyph-readback")),
                "textcore/sprite-asset/upsert-images");

            Add(profiles, VmAutomationToolProfile.Create(mutatesAssets: true,
                    mutatesRuntime: true, dangerous: true,
                    transaction: VmAutomationTransactionProfile.Create(
                        "audio-mixer-persistent-or-runtime", VmTransactionMechanics.Atomicity.BestEffortEditorSession,
                        VmTransactionMechanics.Isolation.RequestUndoGroup,
                        VmTransactionMechanics.Durability.EditorSession,
                        VmTransactionMechanics.RollbackKind.UnityUndoOrValueRestore,
                        "asset-save-or-runtime-readback")),
                "audio-mixer/transaction");

            Add(profiles, VmAutomationToolProfile.Create(mutatesAssets: true,
                    mutatesRuntime: true, dangerous: true,
                    transaction: VmAutomationTransactionProfile.Create(
                        "cinemachine-scene-or-prefab", VmTransactionMechanics.Atomicity.BestEffortEditorSession,
                        VmTransactionMechanics.Isolation.RequestUndoGroup,
                        VmTransactionMechanics.Durability.EditorSession,
                        VmTransactionMechanics.RollbackKind.UnityUndo,
                        "prefab-save-or-scene-dirty-state")),
                "cinemachine/transaction");

            string[] missing = VmAutomationBuiltInRouteDescriptorRegistry.Routes
                .Where(route => !HasProfile(profiles, route))
                .OrderBy(route => route, StringComparer.Ordinal)
                .ToArray();
            if (missing.Length > 0)
            {
                throw new InvalidOperationException(
                    "Built-in routes missing canonical tool profiles: " +
                    string.Join(", ", missing));
            }

            return profiles;
        }

        private static bool HasProfile(
            IReadOnlyDictionary<string, VmAutomationToolProfile> profiles, string route)
        {
            if (profiles.ContainsKey(route))
                return true;
            int slashIndex = route.IndexOf('/');
            return slashIndex > 0 &&
                   profiles.ContainsKey(route.Substring(0, slashIndex + 1) + "*");
        }

        private static void Add(Dictionary<string, VmAutomationToolProfile> profiles,
            VmAutomationToolProfile profile, params string[] routes)
        {
            foreach (string route in routes)
            {
                if (profiles.ContainsKey(route))
                {
                    throw new InvalidOperationException(
                        $"Tool profile route '{route}' is declared more than once.");
                }

                profiles.Add(route, profile.Clone());
            }
        }
    }
}
