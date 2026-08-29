# Catalog, execution, and ownership

Package asset GUIDs are deterministically owned by
`Migration~/Set-DeterministicPackageGuids.ps1`. Run it after importing or renaming
package assets, and run it with `-Check` before publishing an immutable revision.

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

Invalid project tools remain excluded from the executable catalog, but an
invocation using their project-tool name, direct route, or generated `vm_pt_`
name returns `invalid_project_tool` with the exact registration source and
validation error. Duplicate registrations similarly return
`duplicate_project_tool` instead of a misleading `command_not_found`.

## Invocation

`VmAutomationExecutor.ExecuteAsync` is the only executable boundary. It accepts an
exact catalog identifier plus a JSON object and returns one structured result.

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
