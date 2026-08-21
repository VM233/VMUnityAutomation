# VFX Graph tools

VM Unity Automation provides semantic, typed automation for Unity Visual Effect Graph
without taking a compile-time dependency on `com.unity.visualeffectgraph`. The
routes use the VFX Graph editor and runtime APIs discovered in the installed
package. If VFX Graph is absent, discovery reports the capability as unavailable
and direct calls fail with `capability_unavailable`.

The implementation follows Unity's VFX Graph model: systems contain ordered
Contexts, Contexts contain Blocks, horizontal property links connect Slots, and
vertical flow links connect Contexts. Blackboard parameters, parameter-node
occurrences, categories, custom attributes, groups, sticky notes, component
state, project settings, and bake products are separate semantic owners.

Reference material:

- [Graph logic and philosophy](https://docs.unity3d.com/Packages/com.unity.visualeffectgraph@17.0/manual/GraphLogicAndPhilosophy.html)
- [Contexts](https://docs.unity3d.com/Packages/com.unity.visualeffectgraph@17.0/manual/Contexts.html), [Blocks](https://docs.unity3d.com/Packages/com.unity.visualeffectgraph@17.0/manual/Blocks.html), and [Operators](https://docs.unity3d.com/Packages/com.unity.visualeffectgraph@17.0/manual/Operators.html)
- [Blackboard](https://docs.unity3d.com/Packages/com.unity.visualeffectgraph@17.0/manual/Blackboard.html) and [Subgraphs](https://docs.unity3d.com/Packages/com.unity.visualeffectgraph@17.0/manual/Subgraph.html)
- [Component API](https://docs.unity3d.com/Packages/com.unity.visualeffectgraph@17.0/manual/ComponentAPI.html)
- [Point Cache bake tool](https://docs.unity3d.com/Packages/com.unity.visualeffectgraph@17.0/manual/point-cache-bake-tool.html)
- [Visual Effect preferences](https://docs.unity3d.com/Packages/com.unity.visualeffectgraph@17.0/manual/VisualEffectPreferences.html)

## Route map

| Route | Owner and purpose |
| --- | --- |
| `vfxgraph/catalog` | Discover asset kinds, templates, node/parameter descriptors, compatible Blocks, and installed binder/callback extension types. |
| `vfxgraph/create` | Create a Graph, Block Subgraph, or Operator Subgraph, optionally from an official template. |
| `vfxgraph/info` | Read a bounded semantic graph snapshot: topology, settings, recursive Slots, parameters, Blackboard state, UI state, diagnostics, and dependencies. |
| `vfxgraph/transaction` | Apply an ordered, atomic graph-authoring transaction with aliases, rollback, and adopted identity readback. |
| `vfxgraph/validate` | Inspect diagnostics, reimport, or compile and return bounded runtime/shader/dependency manifests. |
| `vfxgraph/component-info` | Inspect VisualEffect components in loaded Scenes or a Prefab, including overrides and supported runtime state. |
| `vfxgraph/component-transaction` | Persist asset assignment, component/renderer settings, and exposed-property overrides in a Scene or Prefab. |
| `vfxgraph/component-control` | Control one loaded VisualEffect component and its event/override state at runtime. |
| `vfxgraph/settings-info` | Inspect supported project VFX settings and per-user VFX preferences. |
| `vfxgraph/settings-transaction` | Atomically update supported project/user settings with explicit reimport policy. |
| `vfxgraph/bake` | Bake a signed-distance field or deterministic mesh/texture Point Cache asset. |

Use `vm_catalog_list` to find a bounded set of candidates, then use
`vm_catalog_get` for the exact schema and current `catalogRevision` before calling
one through `vm_automation_call`. The schemas are closed and version-specific; this document
describes ownership and workflow, not a replacement for schema discovery.

## Complete surface ownership

VFX-specific state is covered by the routes above. Adjacent domains keep their
existing canonical owner instead of being duplicated:

| VFX workflow | Canonical owner |
| --- | --- |
| Graph, Block Subgraph, and Operator Subgraph creation | `vfxgraph/create`; generic asset routes own move, duplicate, rename, and delete. |
| Systems, Contexts, Blocks, Operators, data links, and flow links | `vfxgraph/catalog`, `vfxgraph/info`, `vfxgraph/transaction`, and `vfxgraph/validate`. |
| Blackboard parameters, occurrences, categories, custom attributes, groups, sticky notes, and UI bounds | `vfxgraph/info` and `vfxgraph/transaction`. |
| Subgraph assets and references | VFX Graph routes; Unity performs compatibility and cycle validation. |
| Custom HLSL source files | Text/asset tools own source contents; VFX transactions own graph settings and Slots that reference them. |
| Shader Graph output assets | `shadergraph/*` owns Shader Graph authoring; VFX transactions assign the typed asset reference. |
| VisualEffect and VFXRenderer state | VFX component routes; generic component/prefab routes remain the owner for unrelated component fields. |
| Property/Event Binders, Output Event Handlers, and Spawner Callbacks | `vfxgraph/catalog` discovers installed extension types; generic script and component routes create and configure implementations. |
| Timeline VFX tracks and clips | `timeline/*`. |
| Project VFX settings and user preferences | VFX settings routes. |
| SDF and Point Cache products | `vfxgraph/bake`. |
| Six-way lighting textures and vector-field source assets | Texture/import/asset tools; VFX transactions assign the imported asset. |

## Discovery and identity

Never select a model by display name. Discovery returns a stable `catalogId`
for constructible Contexts, Blocks, Operators, and parameter types. Use
`contextCatalogId` while discovering Blocks to limit results to Blocks that
Unity reports as compatible with the selected Context.

Persisted graph models use their serialized local file ID, represented as a
string. A parameter occurrence is distinct from its Blackboard definition and
uses `<parameterLocalId>:<nodeId>`. Recursive Slots use a direction plus the
returned sibling-index `selector`, such as `[0][2]`; the human-readable Slot
path is diagnostic text and is not used as a fallback identity. Blocks expose
their separately owned Activation Slot as the reserved input selector
`$activation`, so inspection, value edits, and data links all address the same
Unity-owned Slot.

Within one graph transaction, `add-*` operations can publish an `alias` for
later operations. Aliases are request-local. A successful save returns
post-import IDs for aliases and an `idRemap` for any pre-existing model whose
serialized ID Unity changed during adoption, such as a Block moved to another
Context. Subsequent requests must use those adopted IDs.

Every paged collection reports its total, offset, returned count, truncation
state, and next offset. Node settings, input Slots, output Slots, parameter
occurrences, diagnostics, dependencies, shader data, runtime systems, events,
and component overrides have independent page controls.

For Blocks, the `enabled` field used by `add-node` and `set-node` is authored
through Unity's activation Slot. Inspection exposes that same owner as the
reserved `$activation` input selector; the read-only `VFXBlock.enabled`
convenience property is never treated as a writable serialization boundary.

## Creating and inspecting assets

`vfxgraph/create` accepts an `Assets/...` path and one of:

- `graph` (`.vfx`)
- `block-subgraph` (`.vfxblock`)
- `operator-subgraph` (`.vfxoperator`)

Use `vfxgraph/catalog` with `kind=template` to select an official graph
template. Overwrite is opt-in. Creation verifies the imported main asset,
resource, graph model, asset kind, and required Subgraph context before success.
If replacement fails, the previous bytes and `.meta` identity are restored.

`vfxgraph/info` is the authoritative semantic read route. It can return:

- graph kind/version, compilation mode, resource settings, events, and
  dependencies;
- data objects, Contexts, Blocks, Operators, parameters, and occurrences;
- typed node settings and flat recursive input/output Slot records;
- exact data-link and flow-link endpoints;
- parameter categories, custom attributes, and attribute usages;
- groups, sticky notes, UI bounds, and current diagnostics;
- an optional bounded serialized view for low-level diagnosis.

## Atomic graph authoring

`vfxgraph/transaction` supports up to the schema-declared operation limit. The
operation families are:

- `add-node`, `remove-node`, and `set-node` for Contexts, Blocks, Operators,
  and supported node state;
- `set-slot` for typed values, coordinate space, and authored collapse state;
- `connect-data` / `disconnect-data` for horizontal Slot links;
- `connect-flow` / `disconnect-flow` for vertical Context links;
- `move-block` for ordered Context membership;
- `add-parameter`, `set-parameter`, `add-parameter-node`, and
  `remove-parameter-node`;
- `add-category`, `set-category`, `remove-category`, and `move-category`;
- `add-custom-attribute`, `set-custom-attribute`,
  `remove-custom-attribute`, and `move-custom-attribute`;
- `add-group`, `set-group`, `remove-group`, `add-sticky-note`,
  `set-sticky-note`, and `remove-sticky-note`;
- `set-ui-bounds`, `set-graph-setting`, and `set-asset-setting`.

Category deletion requires an explicit parameter disposition. Removing a
custom attribute that is still used requires explicit usage removal. Unity's
own compatibility rules decide whether Blocks, Slots, flow links, and Subgraph
references are legal; the route never substitutes a nearby candidate.

Before `connect-data` links a master input on a numeric dynamic Operator, it
applies the same operand-type negotiation as the VFX Graph editor. Unified,
constrained-unified, and uniform Operators therefore specialize from their
catalog default (often `System.Single`) to the source type before Unity validates
the link. Each successful connection result reports `fromType`, `toType`, and
`dynamicInputSpecialized`, so dry-runs expose the adopted type directly.

The transaction validates closed operation shapes before mutation where
possible, captures the graph backup before the first write, applies operations
in order, saves/imports once, and reads the adopted graph back. Failure reports
the exact operation index and restores the original serialized asset. `dryRun`
copies the graph into a unique temporary asset, applies every semantic graph
operation to that isolated copy, and deletes the copy before returning. The
authoritative graph is never mutated; post-save local IDs, importer compilation,
and shader generation remain explicitly deferred.

## Values and asset references

Supported values include null, booleans, signed and unsigned numbers, strings,
enums, vectors, color, quaternion, matrix, rect, bounds, animation curve,
gradient, Unity asset references, and recursively described VFX structs. Values
are converted using invariant culture and the actual VFX parameter or Slot
type. Compound parameter defaults are flattened by Unity for component
overrides and remain inspectable as typed child properties.

Persistent Unity asset references use:

```json
{
  "assetPath": "Assets/VFX/Noise.asset",
  "type": "UnityEngine.Texture2D"
}
```

The optional `type` must match the loaded asset. A wrong or missing asset fails
at the exact value path instead of becoming null.

## Validation

`vfxgraph/validate` has three modes:

- `inspect`: read current reporters without mutation;
- `reimport`: synchronously import and reopen the asset;
- `compile`: reimport and invoke the installed VFX compiler surface.

The result separates compile diagnostics from bounded runtime manifests. It can
include particle-system names, events, exposed properties, shader descriptors,
bounded shader-source excerpts, asset dependencies, compile output, and the
instancing-disabled reason. A compile diagnostic with error severity makes the
request fail with `vfx_compile_failed`; a logged exception is never converted to
success.

Runtime manifests depend on the installed render pipeline and what Unity emits
for that graph. An empty manifest is valid when the current project has no
compatible SRP/runtime VFX compilation product.

## Components and runtime control

Component routes use an exact target. A loaded-Scene target can use a Scene path
plus hierarchy identity and zero-based VisualEffect component index, or exact
instance identity. A Prefab target uses a Prefab path plus hierarchy identity
and component index. Broad name search is not a fallback.

`vfxgraph/component-info` returns assigned asset, enable state, seed/reseed,
initial event, playback state, renderer-owned sorting/probe/mask/bounds and
instancing settings, exposed overrides versus graph defaults, and bounded
runtime system/spawner/event state where the installed API supports it.

`vfxgraph/component-transaction` persists asset assignment, supported component
and renderer settings, and typed set/reset operations for exposed overrides in
stable Edit Mode. Calling it from Play Mode returns `edit_mode_required` before
capturing or mutating component state.
Prefab publication uses prefab-content editing; Scene publication dirties only
the affected Scene. The ordered transaction rolls back on failure and supports
`dryRun`.

`vfxgraph/component-control` operates on a loaded component. Supported actions
include session-only VFX asset assignment, play, stop, pause, resume,
reinitialize, advance one frame, bounded simulation, send event with a typed
event payload, and set/reset runtime overrides. Runtime asset assignment uses
`action=set-asset` plus an exact `assetPath`; it is useful for isolated previews
on dynamically created loaded components and never persists Scene or Prefab
authoring state. The route otherwise mutates runtime state only.
Advance-one-frame and simulation are deferred until Unity has processed the
queued command in a subsequent `VisualEffect.Update`; the result includes the
before/after state and observed time delta instead of reporting the pre-update
state as success. Those two actions require globally running Play Mode and a
paused target component, which isolates the requested step from normal VFX
playback. `timeoutMs` bounds completion observation from 100 to 10000 ms.

`GraphicsBuffer` values are reported as a supported VFX property type but their
live contents are intentionally not read or persisted: Unity provides no
symmetric getter and a JSON request cannot safely own the buffer lifetime.

## Settings

`vfxgraph/settings-info` reports available project settings from
`VFXManager.asset` and documented per-user VFX preferences. Each descriptor
includes type, value, scope, persistence owner, supported choices/range, and
whether a graph reimport is required.

`vfxgraph/settings-transaction` accepts only those discovered setting IDs. It
uses serialized project settings or the installed VFX preference API as the
owning surface, reads values back after the write, and restores previous values
on failure. Reimporting all VFX assets is never implicit; operations that need
it require an explicit `reimport` policy.

## Baking

`vfxgraph/bake` is discriminated by `kind`:

- `sdf` uses Unity's `MeshToSDFBaker` to publish a Texture3D. Resolution,
  estimated voxel count, sign passes, box, threshold, and offset are bounded.
  Unsupported graphics execution fails before output mutation.
- `point-cache-mesh` samples a Mesh with deterministic seed support, selectable
  distribution, and optional normal/color/UV properties.
- `point-cache-texture` samples qualifying Texture2D pixels with deterministic
  randomization and optional color. A non-readable source importer is made
  readable only for the bake and is restored in `finally`.

Point Cache output can be ASCII or binary. All source/output paths are project
asset paths. Output overwrite is explicit; failed replacement restores both
bytes and `.meta`, and success reopens the Texture3D or PointCacheAsset to
return its dimensions/property manifest.

GPU SDF execution is environment-dependent. Schema, bounds, capability, and
failure behavior can be tested headlessly, but a successful SDF bake requires
an authorized Unity process with compatible graphics hardware/API.

## Compatibility and failure behavior

VM Unity Automation keeps VFX Graph optional so it can retain the package's Unity
2021.3 minimum. A centralized reflection adapter probes required APIs. A missing
required API disables the affected operation with `unsupported_vfx_version`;
the implementation does not guess private serialized fields or silently fall
back to a different node, Slot, setting, or asset.

Common stable error codes include `capability_unavailable`,
`unsupported_vfx_version`, `invalid_arguments`, `asset_not_found`,
`catalog_item_not_found`, `model_not_found`, `slot_not_found`,
`setting_not_found`, `value_type_mismatch`, `block_incompatible`,
`data_link_incompatible`, `flow_link_incompatible`, `subgraph_cycle`,
`parameter_name_conflict`, `custom_attribute_in_use`, `component_not_found`,
`property_not_found`, `event_not_found`, `requires_play_mode`,
`play_mode_paused`, `play_mode_ended`, `vfx_component_pause_required`,
`vfx_update_not_observed`,
`vfx_compile_failed`, `vfx_transaction_failed`,
`vfx_transaction_rollback_failed`, `graphics_api_unsupported`,
`bake_limit_exceeded`, `vfx_bake_failed`, and `vfx_bake_rollback_failed`.

Exact numeric bounds are published in each route's current input schema. A
request beyond a bound fails; authored content is never silently clamped.
