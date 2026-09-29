# Catalog, execution, and ownership

Package asset GUIDs are deterministically owned by
`Migration~/Set-DeterministicPackageGuids.ps1`. Run it after importing or renaming
package assets, and run it with `-Check` before publishing an immutable revision.

`package/dependency-policy-review` checks the descendants of each explicitly
requested `metaRoots` directory. A resolved package root under Library is a valid
project-relative root; its ancestors do not exclude its content. Cache, build and
documentation exclusions apply only below the selected root. Check
`scannedMetaRecords` against the requested scope as well as `passed`, and retain
the returned missing, orphan, duplicate-GUID or folder-meta findings.

## Bounded discovery

`VmAutomationCatalog` is the canonical catalog for built-in automation routes and
valid project/package tools. It provides:

- deterministic ordinal ordering;
- a revision hash over the full rich metadata product;
- optional category, text, tag, and side-effect filters;
- offset pagination with a default limit of 10 and hard limit of 50;
- exact lookup by route, CLI command name, or project-tool name;
- closed input/output schemas, errors, side effects, preconditions, completion
  evidence, and transaction metadata.

`com.vm233.unity-pipeline` exposes this through `vm_catalog_status`,
`vm_catalog_list`, and `vm_catalog_get`. Clients must not enumerate an unbounded
catalog or cache a contract across a revision change.

## Replacing a TextCore source font

After replacing and importing a source TTF or OTF, use the typed
`textcore/font-asset/rebuild` contract for the existing dynamic FontAsset. Its exact
schema and limits are published by the catalog. The operation reads the current
source face and clears derived glyph data; it preserves the font GUID and embedded
atlas/material local IDs, so existing locale styles and fallback references remain
valid. Internal font, atlas and material names follow the current asset filename.
The reported atlas and material local IDs are decimal strings, preserving exact
64-bit identities for JSON clients.

The source and font files are bounded at 64 MiB and 32 MiB respectively. The target
must have one embedded Alpha8 atlas up to 4096 by 4096, face index zero, and at most
65536 glyphs and characters. The transaction takes an asset/meta byte snapshot,
persists only the target asset, imports it, and verifies the resulting face,
identities, names and empty tables. A failed publication restores and verifies the
snapshot. It does not rewrite locale defaults or fallback lists.

Invalid project tools remain excluded from the executable catalog, but an
invocation using their project-tool name, direct route, or generated `vm_pt_`
name returns `invalid_project_tool` with the exact registration source and
validation error. Duplicate registrations similarly return
`duplicate_project_tool` instead of a misleading `command_not_found`.

## Invocation

### Targeted asset refresh

`asset/refresh` accepts asset paths and their `.meta` paths. A metadata path is
resolved to its owning asset before loaded-scene checks, deduplication, dependency
ordering and import. Supplying both forms imports that asset once. The
`importedPaths` result records canonical asset paths, never standalone metadata.
Compilation assets retain the compilation import policy when addressed through
their metadata, and a loaded scene remains protected through either form.

Omitting `assetPaths` performs the full synchronous refresh. Targeted refreshes
do not turn an empty normalized selection into a full refresh. Both forms use
the same durable workspace job, compile evidence and reload lifecycle.

### Execution boundary

`VmAutomationExecutor.ExecuteAsync` is the only executable boundary. It accepts an
exact catalog identifier plus a JSON object and returns one structured result.

`selection/set` accepts scene hierarchy paths and project asset paths beginning
with `Assets/`. `selection/get` reports both scene objects and selected assets;
asset paths can be passed back to `selection/set` when a built-in Editor menu
requires a selected source asset.

Before a production owner runs, the executor validates:

1. exact command resolution;
2. timeout bounds;
3. request-ID/input fingerprint consistency;
4. absolute `expectedProjectPath` for every mutation;
5. stable Play Mode when declared;
6. `confirm=true` for dangerous commands;
7. workspace exclusivity while a durable mutation is active.

Request identity is owned by the executor and request registry. It is not added
to a command's closed argument object unless that command explicitly declares
`idempotencyKey`, in which case the durable owner receives the request identity
and a request-derived default key when the caller omitted one.

Immediate eligible mutations receive a request-owned Unity Undo group. Deferred
callbacks are adapted to a `Task` and never advertised as synchronously undoable.
Handler exceptions and legacy error-shaped results are normalized into stable CLI
errors. A timeout explicitly reports that the Editor operation may still complete,
so clients must inspect published state before considering a retry.

## Project tools

Use `[VmProjectTool]` on one static method or concrete type. Prefer
`IVmProjectTool<TRequest, TResult>` so the registry derives strict schemas from the
same CLR contract used for execution. A long-running class tool implements
`IVmPersistentProjectTool`; each `VmProjectToolJobStep` carries all state required by
the next step.

Tool metadata must declare one coherent effect owner, stable errors, preconditions,
completion evidence, and a complete transaction contract when applicable. Duplicate
tool names, ambiguous generic interfaces, undeclared dictionary schemas, incomplete
transactions, and output-schema drift are configuration failures.

## Defaults and persistence

Explicit command arguments always win. Optional user defaults cover only result
limits, prefab diff detail, action-history retention, and job-history retention.
Portable team defaults live in
`ProjectSettings/VMUnityAutomationSettings.json` and currently contain additional
execute-code namespaces, default Physics dimension, and screenshot directory.

All durable state lives below `Library/VMUnityAutomation`. Reload-resumable workspace
jobs remain admission-queued until the first authorized `jobs/get` poll publishes a
client-adoption marker. The main-thread runner persists that acknowledgement before it
may mutate or reload Unity. Domain Reload recovery is owned by the job that published
the state; the CLI transport does not replay an ambiguous mutation. Clean-compilation
jobs also persist their pre-request expected Editor assembly set and the actual
per-assembly completion set. Job success requires complete set coverage in addition to
the compilation lifecycle and assembly reload signals.

`compilationFinished` persists the completed callback product and enters
`awaiting-compilation-outcome`. Unity's native compilation flag is read only from
a stable Editor update or the next assembly domain, because the callback can still
expose the previous compilation's failure. Manifest restoration after package tests
also checks that native outcome before reporting success.
