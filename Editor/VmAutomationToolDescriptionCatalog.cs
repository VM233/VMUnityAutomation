using System;

namespace VMUnityAutomation.Editor
{
    internal static class VmAutomationToolDescriptionCatalog
    {
        internal static string Get(string route)
        {
            switch (route)
            {
                case "asset/list":
                    return "List assets below a Unity project folder with bounded pagination and an optional type filter.";
                case "compilation/errors":
                    return "Read each Unity assembly's latest compiler errors and warnings with bounded pagination and a separate obsolete-API warning summary. Unity callback batches and the current Editor-log compilation interval are aggregated before incremental compilation replaces diagnostics only for assemblies that recompiled; incomplete capture is rejected explicitly.";
                case "packages/info":
                    return "Read detailed Unity Package Manager metadata for one installed package.";
                case "packages/list":
                    return "List installed Unity packages with bounded pagination.";
                case "packages/update-git":
                    return "Start a durable Git-package update job pinned to a full commit SHA. The job remains admission-queued until the first authorized jobs/get poll confirms that its token reached the client, then waits for stable Edit Mode before mutating Package Manager state. It verifies manifest, lockfile, registered package identity, and the resolved cache fingerprint, refreshes assets, requests a clean script compilation, records every expected and completed script assembly, observes assembly reload, and only succeeds when the complete rebuild is proven.";
                case "packages/resolve":
                    return "Start a durable Package Manager resolve job for explicit full-SHA Git package targets. The job remains admission-queued until the first authorized jobs/get poll confirms that its token reached the client, then waits for stable Edit Mode before mutating Package Manager state. It verifies manifest, lockfile, registered package identity, and the resolved cache fingerprint, refreshes assets, and proves a complete clean rebuild through per-assembly completion evidence plus assembly reload.";
                case "packages/status":
                    return "Read Package Manager manifest and lock status for one package or all Git packages.";
                case "packages/lint-metas":
                    return "Lint a Unity package root for missing .meta files.";
                case "wait/editor-idle":
                    return "Wait until the Unity Editor is idle after compilation, domain reload, package refresh, or asset import.";
                case "editor/state":
                    return "Read the current Unity Editor Play Mode, pause, play-mode transition, compilation, asset-update, active scene, platform, and project state. isPlaying, isPaused, and isChangingPlayMode are always explicit booleans.";
                case "editor/play-mode":
                    return "Enter, pause, resume, step one frame, or stop Play Mode. Play and stop publish a durable job token and remain admission-queued until the first authorized jobs/get poll confirms that the token reached the client; only then may they change state. Stop remains available when another workspace job is waiting for Edit Mode and supersedes an in-flight play transition. Pause, resume, and step return after Unity confirms the requested state.";
                case "editor/play-mode-options":
                    return "Read or configure Unity's Enter Play Mode Options through the live EditorSettings owner. Omit every option to inspect current state; mutations require stable Edit Mode and return exact previous/current values for restoration. Disabling the feature follows Unity's documented behavior by normalizing the option flags to None, which enables both reloads.";
                case "testing/list-tests":
                    return "List discoverable Unity tests with mode and name filters.";
                case "testing/run-tests":
                    return "Start a Unity Test Runner job and return a job ID for polling.";
                case "testing/get-job":
                    return "Poll a Unity Test Runner job, including progress, failures, and optional result details. EditMode tests can delay main-thread queue polling while they execute.";
                case "testing/run-package-tests":
                    return "Start a persistent Git-package test job that temporarily enables package testables, survives domain reloads, restores manifest.json exactly, and returns a jobAccessToken for reconnect recovery. VM Unity Automation defaults to its VMUnityAutomation.PackageSmoke category; request VMUnityAutomation.FullRegression explicitly for the full suite. Poll its returned jobId with jobs/get and jobType package-test.";
                case "testing/get-package-job":
                    return "Inspect or clear the current package-test workflow state. Normal polling uses jobs/get with the package test's jobId and jobType package-test; after reconnect, supply the jobAccessToken returned at start.";
                case "profiler/enable":
                    return "Enable or disable the Unity Profiler and optional deep profiling.";
                case "profiler/stats":
                    return "Read current Unity rendering statistics such as batches, draw calls, triangles, and frame time.";
                case "project-auditor/audit":
                    return "Run Unity Project Auditor through its public API with the saved Project Auditor rule settings, then return a deterministic bounded page of the new report. This does not read the Project Auditor window's previous report or UI filters.";
                case "profiler/memory":
                    return "Read current allocated, reserved, managed-heap, graphics-driver, and temporary allocator memory.";
                case "profiler/frame-data":
                    return "Read a bounded, caller-depth CPU timing hierarchy from a retained Unity Profiler frame, including after recording is disabled.";
                case "profiler/analyze":
                    return "Analyze current memory, rendering, and recorded Profiler frame data with optimization findings.";
                case "profiler/memory-status":
                    return "Read Memory Profiler availability and a quick current memory summary.";
                case "profiler/memory-breakdown":
                    return "Scan loaded assets and summarize runtime memory by asset category.";
                case "profiler/memory-top-assets":
                    return "List the largest loaded assets by runtime memory usage.";
                case "profiler/memory-snapshot":
                    return "Capture a Memory Profiler snapshot and wait for confirmed completion when com.unity.memoryprofiler is installed.";
                case "profiler/memory-snapshot-status":
                    return "Poll the current Memory Profiler snapshot job after a long capture outlives the initiating request.";
                case "editor/execute-code":
                    return "Start an owner-scoped persistent Job that compiles and executes a C# method body in the Unity Editor, with exact-argument idempotency, bounded result serialization, optional typed Unity structs, cancellation, and explicit cleanup code.";
                case "scene/hierarchy":
                    return "Read the active scene hierarchy, optionally returning compact matches filtered by component type.";
                case "scene/instantiate-prefab":
                    return "Instantiate a prefab asset into the currently open scene.";
                case "scene/workspace":
                    return "List loaded scenes, open a scene additively or singly, close a loaded scene with an explicit dirty-scene policy, or set the active scene.";
                case "prefab/create-variant":
                    return "Create a Prefab Variant from an existing Prefab asset and return its saved asset identity.";
                case "prefab-asset/add-component":
                    return "Add and optionally initialize a component on a prefab asset, then verify its serialized state after saving. Waits for a newly compiled script type when needed.";
                case "prefab-asset/configure-component":
                    return "Ensure and configure one component on a prefab asset GameObject, including serialized properties and ObjectReferences, in one atomic save.";
                case "prefab-asset/add-gameobject":
                    return "Create a child GameObject inside a prefab asset with an explicit or parent-inherited Layer.";
                case "prefab-asset/instantiate-child-prefab":
                    return "Instantiate a prefab asset as a child inside another prefab asset.";
                case "prefab-asset/hierarchy":
                    return "Get the full hierarchy tree of a prefab asset directly from disk.";
                case "prefab-asset/get-properties":
                    return "Read serialized properties from a component on a GameObject inside a prefab asset.";
                case "prefab-asset/set-property":
                    return "Set a serialized property on a component inside a prefab asset.";
                case "prefab-asset/set-reference":
                    return "Set an ObjectReference property on a component inside a prefab asset.";
                case "prefab-asset/move-gameobject":
                    return "Move or reorder a GameObject inside a prefab asset.";
                case "prefab-asset/move-component":
                    return "Atomically move a component between GameObjects inside one prefab asset while preserving serialized data and remapping references to the moved component.";
                case "prefab-asset/remove-component":
                    return "Remove a component from a GameObject inside a prefab asset.";
                case "prefab-asset/remove-gameobject":
                    return "Remove a child GameObject from inside a prefab asset.";
                case "prefab-asset/find":
                    return "Find GameObjects inside a prefab asset by name/path, component type, and serialized property value.";
                case "prefab-asset/transaction-edit":
                    return "Apply ordered prefab edits in one transaction with configurable immediate or frame-batched execution.";
                case "prefab-asset/cleanup-missing-overrides":
                    return "Remove Prefab Variant property overrides whose serialized target field no longer exists.";
                case "component/add":
                    return "Add one component to an exact loaded-scene GameObject selected by hierarchy path or instance ID. This mutates the loaded scene; call scene/save explicitly to persist the change.";
                case "component/get-properties":
                    return "List serialized fields on one loaded-scene component. Set includeHidden to true for native Unity backing fields such as SortingGroup sorting data; pass the returned propertyPath to component/set-property.";
                case "component/remove":
                    return "Remove one indexed component from an exact loaded-scene GameObject selected by hierarchy path or instance ID. This mutates the loaded scene; call scene/save explicitly to persist the change.";
                case "scene/save":
                    return "Save the active loaded scene to its current asset path. Provide an Assets/*.unity path to save as a different scene, and set overwrite only when intentionally replacing an existing scene asset.";
                case "component/set-reference":
                    return "Assign one or more component ObjectReference properties with configurable immediate or frame-batched execution.";
                case "component/move":
                    return "Atomically move a component between GameObjects in one loaded scene while preserving serialized state and remapping scene-local references.";
                case "component/set-property":
                    return "Set a serialized component property, including inherited Behaviour.enabled, on a scene GameObject.";
                case "serialized-object/get":
                    return "Read serialized properties from a scene object, component, or asset via SerializedObject.";
                case "serialized-object/set":
                    return "Set one serialized property on a scene object, component, or asset via SerializedObject. SerializeReference values use '$managedReferenceType' when their concrete type cannot be inferred.";
                case "asset/refresh":
                    return "Start a durable AssetDatabase refresh job. It remains admission-queued until the first authorized jobs/get poll confirms that its token reached the client. The same job records the refresh return, requests a clean script compilation, persists every expected and completed script assembly, observes assembly reload, and rejects a zero-assembly or incomplete rebuild instead of reporting success.";
                case "asset/import":
                    return "Preflight and import one or more external assets with shared TextureImporter defaults, image-content deduplication, configurable execution, per-item results, and rollback.";
                case "asset/import-settings/get":
                    return "Read semantic TextureImporter, ModelImporter, or AudioImporter settings without exposing Unity's internal serialized fields.";
                case "asset/import-settings/set":
                    return "Validate and update semantic TextureImporter, ModelImporter, or AudioImporter settings, optional platform overrides, and reimport behavior.";
                case "asset/rename":
                    return "Safely rename a Unity asset using AssetDatabase while preserving its .meta GUID, synchronizing Single Sprite names, and renaming matching Multiple Sprite prefixes without changing Sprite IDs.";
                case "asset/move":
                    return "Preflight and move one or more Unity assets with configurable execution, GUID preservation, Sprite internal-name synchronization when filenames change, Multiple Sprite ID preservation, and rollback.";
                case "asset/export-unitypackage":
                    return "Export one or more Unity assets to a .unitypackage file using AssetDatabase.ExportPackage.";
                case "asset/import-unitypackage":
                    return "Start a reload-safe, non-interactive .unitypackage import. Poll jobs/get with the returned jobId and jobType until the AssetDatabase completion callback is confirmed.";
                case "asset/create-folder":
                    return "Create or ensure an Assets folder hierarchy through AssetDatabase, with dry-run support.";
                case "asset/copy":
                    return "Copy one or more Unity asset files with parent-folder creation, overwrite snapshots, and rollback.";
                case "asset/dependencies":
                    return "Read paginated outgoing dependencies and incoming references for an asset.";
                case "asset/transaction":
                    return "Start a durable asset transaction Job that remains admission-queued until the first authorized jobs/get poll confirms that its token reached the client, then prepares byte snapshots before mutation, survives domain reload, and reports committed, rolled_back, rollback_failed, or outcome_uncertain with readback evidence.";
                case "console/query":
                    return "Query recent Unity Console entries with time, source, message, stack, and last-Play filters.";
                case "debug/attach-unity":
                    return "Inspect Unity managed debugger attachment state and return Automation debug capability boundaries.";
                case "debug/set-breakpoint":
                    return "Request a managed source breakpoint. Currently reports that this requires an external debugger adapter.";
                case "debug/stack-trace":
                    return "Return the current Automation request stack trace. Paused managed frames require an external debugger adapter.";
                case "debug/variables":
                    return "Request variables for a paused managed frame. Currently reports that this requires an external debugger adapter.";
                case "debug/evaluate":
                    return "Evaluate C# code in the Unity Editor context. Paused frame evaluation requires an external debugger adapter.";
                case "animation/transition-info":
                    return "Read full Animator transition details including conditions, exit time, duration, and offset.";
                case "animation/update-state":
                    return "Modify an existing Animator state, including motion, speed, tag, graph position, and default state.";
                case "animation/update-transition":
                    return "Modify an existing Animator transition, including settings and condition edits.";
                case "animation/connect-states":
                    return "Create transitions between every pair of the provided Animator states.";
                case "animation/validate-controller":
                    return "Validate Animator parameters, states, motions, required transitions, and pairwise state connections.";
                case "uitoolkit/audit-uss-styles":
                    return "Audit USS selectors that serve exactly one authored UXML element, hard-error fully inlineable single-consumer simple selectors even when an allow-single-use marker is present, hard-error invariant base declarations left in a one-consumer class only because a modifier, pseudo-state, or relational selector retains that class as an anchor, hard-error repeated declaration bundles across independently assignable simple classes, redundant authored classes that merely alias a component's inherent class below a named scope, page-scoped style families that cross a reusable component root to skin runtime-generated direct children, declarations that repeat the concrete component's effective baseline, flex-shrink declarations with no finite parent main-axis extent, layout-only flex parents that repeat a cross size already established by authored or runtime-generated in-flow children, fixed-size absolute overlays whose left/top exactly recalculate a fixed-size centered parent's placement, and non-default declarations whose overly broad target is reset to the Unity engine initial value by an ancestor-scoped branch, while preserving real placement, bounded flex layout, visual and interaction regions, edge anchors, measured optical offsets, skin-variant, pseudo-state, and runtime-state contracts.";
                case "uitoolkit/audit-uxml-layout":
                    return "Audit authored UXML for tooltip attributes, unconsumed element names, fully fixed flex partitions, layout-only sibling groups manually reconstructed with absolute offsets, fixed cross sizes that override natural in-flow Flex content, ineffective flex-shrink on default vertical ScrollView content or statically non-negative Flex lines, fixed cross-axis content wrappers inside single-axis ScrollViews, layout-only manually centered containers, removable single-child centering wrappers, visually inert centered-label stretching or growth, repeated inline layout variants, and inline declarations already owned by loaded USS or Unity engine initial styles.";
                case "uitoolkit/windows":
                    return "List open Unity Editor windows with UI Toolkit root metadata.";
                case "uitoolkit/tree":
                    return "Read a UI Toolkit visual tree from an EditorWindow.";
                case "uitoolkit/query":
                    return "Query UI Toolkit elements by name, className, typeName, or text.";
                case "uitoolkit/style":
                    return "Read inline and resolved style for a UI Toolkit element.";
                case "uitoolkit/repaint":
                    return "Trigger repaint on a UI Toolkit EditorWindow or element.";
                case "uitoolkit/asset-inspect":
                    return "Inspect UXML and USS assets for VisualElement names, types, unconditional class defaults, contextual selectors, and pseudo-state rules.";
                case "uitoolkit/runtime-documents":
                    return "List runtime UIDocuments with root visual element metadata.";
                case "uitoolkit/runtime-tree":
                    return "Read a runtime UIDocument UI Toolkit visual tree.";
                case "uitoolkit/runtime-query":
                    return "Query runtime UIDocument UI Toolkit elements by VisualElementPath, name, class, type, or text.";
                case "uitoolkit/runtime-style":
                    return "Read inline, resolved, and background style data for a runtime UI Toolkit element.";
                case "uitoolkit/diagnose-runtime":
                    return "Diagnose runtime UI Toolkit elements with VisualElementPath lookup, style, parent/children, background, and pixel-grid data.";
                case "uitoolkit/visual-check":
                    return "Run runtime UI Toolkit visual checks such as pixel-grid, background scale, and expected size.";
                case "uitoolkit/locate-element":
                    return "Locate an Editor or runtime UI Toolkit element and return its VisualElementPath, world bounds, crop rect, and context.";
                case "uitoolkit/capture-element":
                    return "Capture an Editor or runtime UI Toolkit element by taking its containing window screenshot and cropping to the element bounds.";
                case "uitoolkit/compare-element":
                    return "Capture a UI Toolkit element and compare the cropped image against a reference image.";
                case "uitoolkit/generated-children":
                    return "Inspect generated UI Toolkit child elements such as arrows, checkmarks, scrollers, TabView internals, and unnamed unity-* subparts.";
                case "uitoolkit/resource-audit":
                    return "Audit UI Toolkit elements for resolved background assets, generated child visuals, highlighted-state misuse, and scale metadata.";
                case "uitoolkit/runtime-repaint":
                    return "Trigger repaint for a runtime UIDocument or one of its elements.";
                case "uitoolkit/refresh":
                    return "Refresh UI Toolkit assets, repaint runtime and Editor panels, and return after stable Editor frames.";
                case "uitoolkit/assert-layout":
                    return "Assert UI Toolkit runtime layout constraints such as edge touching, containment, and size.";
                case "uitoolkit/builder-preview":
                    return "Open a UXML asset in UI Builder, expand an undersized canvas through Match Game View, wait for the preview to settle, and optionally capture the window.";
                case "uitoolkit/edit-uxml":
                    return "Structurally edit UXML elements by VisualElementPath or authored name, then synchronously reimport the asset.";
                case "uitoolkit/edit-uss":
                    return "Add, remove, or update USS selectors and declarations, then synchronously reimport the asset.";
                case "uitoolkit/authoring-transaction":
                    return "Apply UXML and USS edits across multiple files with atomic file snapshots and rollback.";
                case "packages/add":
                    return "Add a Unity package by registry name, Git URL, local path, or tarball in stable Edit Mode and wait for Package Manager completion. Play Mode and Play Mode transitions are rejected before the package request begins.";
                case "packages/remove":
                    return "Remove a Unity package dependency in stable Edit Mode and wait for Package Manager completion. Play Mode and Play Mode transitions are rejected before the package request begins.";
                case "packages/search":
                    return "Search Unity Package Manager registry packages with bounded results.";
                case "screenshot/game":
                    return "Capture the current Game View during active or paused Play Mode, suppress and restore Game View Gizmos and Stats by default or preserve them when they are the evidence subject, fail without creating an image in Edit Mode, and return only after the PNG is fully written and decodable.";
                case "screenshot/crop":
                    return "Crop an existing screenshot or image file to a PNG.";
                case "screenshot/scene":
                    return "Capture the current Scene View once and return the PNG as a file, base64 payload, or both.";
                case "graphics/asset-preview":
                    return "Render Unity's asset preview for any supported asset type, including prefabs, as a base64 PNG.";
                case "gameview/info":
                    return "Read the Unity Editor Game View resolution, selected size, scale, and minimum scale.";
                case "gameview/set-resolution":
                    return "Set the Unity Editor Game View to a custom resolution.";
                case "gameview/set-scale":
                    return "Set the Unity Editor Game View zoom scale to an explicit value or the current minimum slider scale.";
                case "graphics/image-alpha-bounds":
                    return "Inspect a PNG or texture asset and return alpha-based visible pixel bounds.";
                case "graphics/rect-gap":
                    return "Measure the gap or overlap between two rectangles along an edge pair.";
                case "graphics/annotate-rects":
                    return "Draw rectangle overlays on a screenshot or image file for visual verification.";
                case "graphics/compare-images":
                    return "Compare two screenshots or image files, optionally within crop rects, and return pixel-difference bounds plus an optional diff image.";
                case "sprite/sheet-info":
                    return "Inspect a sliced sprite sheet and return texture and sprite metadata.";
                case "sprite/pixel-check":
                    return "Check Sprite/Texture import settings, dimensions, pivot, border, and pixel-art suitability.";
                case "sprite/replace-and-slice":
                    return "Replace a sprite sheet image file and slice it into numbered sprites.";
                case "sprite/slice-sheet":
                    return "Slice an existing sprite sheet into numbered sprites while preserving existing sprite IDs by name.";
                case "sprite/update-animation-clip":
                    return "Update an AnimationClip SpriteRenderer.m_Sprite object-reference curve from a sprite sheet.";
                case "sprite/replace-slice-update-clip":
                    return "Replace a sprite sheet, slice it, then update an AnimationClip from the generated sprites.";
                case "texture/apply-sprite-preset":
                    return "Apply high-level TextureImporter/Sprite settings such as pixel sprite preset, PPU, pivot, border, and reference settings without changing Single/Multiple mode unless a reference owns it.";
                case "texture/info":
                    return "Inspect a texture asset, runtime format and memory, and its TextureImporter settings, including sprite PPU, pivot, and border when applicable.";
                case "texture/set-import":
                    return "Set TextureImporter type and import settings, including Sprite and NormalMap configuration, then reimport once.";
                case "texture/find-duplicates":
                    return "Audit project image assets for duplicate file bytes or identical decoded RGBA pixels, even when PNG/JPEG encoding differs.";
                case "texture/import-image":
                    return "Import an external image from a URL or local path into Assets, optionally dedupe, then apply sprite import settings.";
                case "texture/check-import-settings":
                    return "Check TextureImporter settings against a reference texture or a pixel-sprite preset without modifying assets.";
                case "texture/check-ui-import-settings":
                    return "Check UI pixel-art image import settings, including pixel sprite defaults plus optional expected dimensions, border, and max texture size.";
                case "textcore/sprite-asset/upsert-images":
                    return "Transactionally upsert bounded named PNG images into one existing TextCore SpriteAsset and its external Multiple-Sprite PNG atlas. Preserves SpriteAsset, atlas, material, and existing Sprite identities; appends deterministic padded rows; and verifies persisted importer plus character/glyph readback.";
                case "textmeshpro/font-asset/upsert-bitmap-glyphs":
                    return "Transactionally upsert bounded PNG images as private-use Unicode glyphs in one existing static, embedded Alpha8 SDFAA TextMeshPro font atlas. Preserves the font, atlas, and material asset identities; rejects dirty or unsupported targets; and verifies persisted glyph-table readback.";
                case "build/start":
                    return "Start a persistent Player build job, optionally run the executable, and return immediately with a job ID. Poll build/get-job for the final BuildReport; no post-build asset refresh is required.";
                case "build/get-job":
                    return "Poll the current or latest persistent Player build job and return its final BuildReport and optional run result.";
                case "build/profile":
                    return "Inspect or transactionally edit Unity 6 Build Profiles, active profile, scenes, scripting defines, and global build-scene settings.";
                case "jobs/list":
                    return "List paginated persistent VM Unity Automation job history owned by the current agent.";
                case "jobs/get":
                    return "Get one persistent VM Unity Automation job snapshot by jobId, or recover the same workspace job by its original requestId and jobType, with owner enforcement. The first authorized poll acknowledges token delivery and releases a newly admitted workspace job for execution.";
                case "jobs/cancel":
                    return "Request owner- or capability-token-checked cancellation of a persistent VM Unity Automation job and report the actual cancellation mode.";
                case "jobs/cleanup":
                    return "Run the explicit persisted cleanup contract of a terminal execute-code or project-tool job. Cleanup is itself durable and status is read through jobs/get.";
                case "material/properties/get":
                    return "Read a Material's shader, typed shader properties, textures, keywords, render queue, and instancing settings through Unity's public Material API.";
                case "material/properties/set":
                    return "Transactionally set typed Material shader properties, texture references and transforms, keywords, render queue, and instancing settings.";
                case "shadergraph/info":
                    return "Inspect a Shader Graph's compiled shader properties plus authoritative node, edge, and blackboard-property counts.";
                case "shadergraph/get-properties":
                    return "Read compiled shader properties and Shader Graph texture-property metadata such as Per Renderer Data, Main Texture, tiling/offset, and texel-size generation.";
                case "shadergraph/get-nodes":
                    return "Read only the semantic nodes referenced by Shader Graph GraphData, excluding slots, properties, targets, and other serialized helper objects.";
                case "shadergraph/get-edges":
                    return "Read Shader Graph connections with exact output/input node IDs and slot IDs from GraphData.";
                case "shadergraph/set-node-property":
                    return "Safely set a scalar field on a serialized Shader Graph object, with field/type validation, synchronous import, readback verification, and rollback.";
                case "physics/raycast":
                    return "Raycast through Physics or Physics2D using one dimension-selectable contract, with deterministic bounded multi-hit results.";
                case "physics/overlap-sphere":
                    return "Run a 3D sphere or 2D circle overlap query with deterministic bounded collider results.";
                case "physics/overlap-box":
                    return "Run a 3D or 2D box overlap query with deterministic bounded collider results.";
                case "vfxgraph/catalog":
                    return "Discover installed VFX Graph asset kinds, templates, contexts, blocks, operators, parameter types, property/event binders, output-event handlers, and spawner callbacks through stable catalog IDs.";
                case "vfxgraph/create":
                    return "Create a VFX Graph, block subgraph, or operator subgraph from an installed template with extension validation, meta-preserving overwrite, import verification, and rollback.";
                case "vfxgraph/info":
                    return "Inspect a VFX Graph or subgraph semantically, including contexts, blocks, operators, parameters and occurrences, typed slots, data and flow links, blackboard metadata, UI layout, dependencies, events, compilation mode, asset settings, and diagnostics.";
                case "vfxgraph/transaction":
                    return "Apply an atomic ordered semantic transaction to one VFX Graph or subgraph, covering models, slots, data and flow links, blocks, parameters, blackboard metadata, UI layout, graph compilation/settings, and graph-asset settings, with isolated-copy dry-run and byte-level rollback.";
                case "vfxgraph/validate":
                    return "Inspect, synchronously reimport, or explicitly compile a VFX Graph or subgraph and report bounded diagnostics, systems, events, exposed properties, generated shaders, dependencies, and instancing constraints.";
                case "vfxgraph/component-info":
                    return "Inspect exact VisualEffect components in loaded scenes or prefab assets, including assigned assets, component and VFXRenderer settings, bounds, seed, exposed-property defaults/overrides, and optionally paged Play Mode system, spawner, and output-event state.";
                case "vfxgraph/component-transaction":
                    return "Apply an atomic persistent transaction to one exact VisualEffect component in a loaded scene or prefab asset, including asset assignment, component settings, rendering settings, and typed exposed-property overrides.";
                case "vfxgraph/component-control":
                    return "Control one exact VisualEffect component in Play Mode: assign its session-only VFX asset, play, stop, pause, resume, reinitialize, step or simulate with observed VisualEffect-update completion, send typed events, and set or reset runtime exposed-property overrides.";
                case "vfxgraph/settings-info":
                    return "Inspect documented VFX Graph project settings and per-user editor preferences without depending on VFX package compile-time references.";
                case "vfxgraph/settings-transaction":
                    return "Apply an atomic ordered transaction to documented VFX Graph project settings and per-user preferences, with dry-run, rollback, and explicit graph reimport policy.";
                case "vfxgraph/bake":
                    return "Bake VFX authoring data through Unity's installed VFX Graph implementation: mesh signed-distance fields and mesh- or texture-derived point caches, with bounded workloads and meta-preserving rollback.";
                case "audio-mixer/info":
                    return "Inspect an AudioMixer's groups, snapshots, effects, and exposed parameter values, with a bounded raw serialized diagnostic available only when requested.";
                case "audio-mixer/transaction":
                    return "Manage AudioMixer groups, snapshots, effects, exposed parameters and persistent snapshot values, or apply a separate batch of editor-session runtime overrides.";
                case "addressables/info":
                    return "List Addressables settings, groups, schemas, labels, and paginated entries when com.unity.addressables is installed.";
                case "addressables/transaction":
                    return "Transactionally manage Addressables groups, copied schemas, the default group, labels, entries, addresses, and entry-label assignments.";
                case "addressables/build":
                    return "Start a persistent Addressables content build job and return a job ID for jobs/get or jobs/cancel.";
                case "timeline/info":
                    return "Inspect a Timeline asset's tracks, clips, markers, and duration, with a bounded raw serialized diagnostic available only when requested.";
                case "timeline/transaction":
                    return "Apply an undoable Timeline transaction that creates, deletes, renames, or configures tracks and clips.";
                case "cinemachine/info":
                    return "Inspect Cinemachine cameras, brains, and extensions in loaded scenes or a prefab, with optional bounded serialized properties.";
                case "cinemachine/transaction":
                    return "Apply an undoable Cinemachine scene or prefab transaction for properties, object targets, and enabled state.";
                case "animation/set-object-reference-curve":
                    return "Set AnimationClip ObjectReference keyframes, such as SpriteRenderer.m_Sprite.";
                case "localization/status":
                    return "Inspect Unity Localization package, settings, locale, and table collection status.";
                case "localization/locales":
                    return "List project Locales registered with Unity Localization.";
                case "localization/create-locale":
                    return "Create a Locale asset and optionally register it with Localization Settings.";
                case "localization/set-selected-locale":
                    return "Set the currently selected Unity Localization Locale.";
                case "localization/collections":
                    return "List String and Asset Table Collections with their Locale tables.";
                case "localization/create-collection":
                    return "Create a String or Asset Table Collection for selected Locales.";
                case "localization/entries":
                    return "Read paginated String or Asset Table entries across Locale tables.";
                case "localization/upsert-entry":
                    return "Create or update one or more localized String, Smart String, or Asset Table entries with configurable execution.";
                case "localization/remove-entry":
                    return "Remove a localization entry from one Locale table or the entire collection.";
                case "localization/validate":
                    return "Find missing, empty, and duplicate localization entries across Locale tables.";
                case "localization/settings":
                    return "Read or update Localization Settings, project Locale, and selected Locale.";
                case "localization/variables":
                    return "List Smart String persistent variable groups and values.";
                case "localization/upsert-variable":
                    return "Create or update a Smart String persistent variable and optionally create its group asset.";
                case "localization/remove-variable":
                    return "Remove a Smart String persistent variable from a registered group.";
                case "queue/info":
                    return "Inspect queue capacity, active work, and per-agent depth.";
                case "queue/status":
                    return "Read one owned queue ticket and its terminal result.";
                case "queue/cancel":
                    return "Cancel one owned queued request; executing Unity work is not preempted.";
                case "undo/perform":
                    return "Undo one exact request-owned Unity Undo group by actionId or requestId; reject when intervening work makes targeted undo unsafe.";
                case "undo/redo":
                    return "Redo only the exact request most recently undone through undo/perform.";
                case "undo/history":
                    return "List persisted Automation action identities and their directional Undo availability; this is not Unity's global Undo stack.";
                case "undo/clear":
                    return "Irreversibly clear object-scoped or global Unity Undo history with confirm=true.";
                case "search/scene":
                    return "Search loaded scene GameObjects with composable name, component, tag, layer, and shader filters plus stable pagination.";
                default:
                    return VmAutomationToolDescriptionComposer.Compose(route);
            }
        }
    }
}
