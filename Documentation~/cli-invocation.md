# CLI invocation and failure boundaries

## Input admission

The executor validates the invocation-owned JSON object against the exact catalog
`inputSchema` before private routing fields, request registration, owner defaults,
Undo or job admission. Unknown properties, incorrect JSON types, missing required
fields and violated schema constraints return `invalid_arguments` with
`details.stage=input-validation`, a JSON path and the violated keyword. Strings
are not converted into numbers or booleans, and enum spelling is exact.

An invalid request does not claim its request ID or admit a durable job. Correct
the arguments using the current exact contract; transport success alone does not
mean admission succeeded. For example, `asset/refresh` declares
`codeOptimization: "Debug"`; `compilationMode: "debug"` is an unknown property.

Exact schemas publish `x-vmAutomationInputValidation` with the evaluator's work
and input-depth capacities. Exhaustion returns `input_validation_limit` before
execution. Local JSON references and union branches consume the same per-call
budget; pattern evaluation also has a bounded time capacity.

## Project binding

`VmAutomationExecutor.ExecuteAsync` owns absolute project binding for every
caller. The Pipeline facade forwards its scalar `expected_project_path` and the
JSON argument object without comparing path strings itself. Native separators,
trailing separators and dot segments are normalized. Windows compares paths
without case sensitivity; other platforms compare them ordinally. Drive-relative
and other relative paths are rejected.

If both bindings are supplied, equivalent paths are accepted; different roots
return `argument_conflict`. A consistent root belonging to another checkout
returns `project_mismatch`. Validation completes before a handler or request
registration can mutate anything. The accepted JSON binding is replaced in an
invocation-owned copy with the connected checkout's canonical path before request
fingerprinting. Equivalent spellings therefore preserve one request identity.

## Durable polling

Automation's `jobs/get` is a transport-neutral owner route. In Unity CLI, calling
it through `vm_automation_call` enters the main-thread queue. Durable submissions
expose the facade's `polling.command` and `polling.arguments`; execute that recipe
with the same absolute project binding and original job identity/capability.
It selects the published background snapshot reader during import, compilation
and reload. A transport failure is not proof that the mutation failed, and does
not authorize submitting it again. Inspect the same durable job after reconnect.

## Prefab component admission

The add-component, configure-component and transaction-edit owners require
component types already imported and compiled in an idle Editor domain. Component
types referenced by transaction operations and reference assignments are admitted
before opening Prefab contents. A missing or non-component type returns
`component_type_not_found` with `assetPath`, `componentType` and
`stage=component-type-admission`. An active import or compilation returns
`editor_not_stable`.

These failures have no delayed refresh, type-wait timer or later edit. Run the
explicit durable `asset/refresh` job after writing source, retain its token, use
the background `vm_job_status` facade to release and observe it, and require clean
compilation plus domain reload before submitting a separate Prefab edit.

The former `refreshAssets`, `waitForType`, `waitForTypes`,
`typeResolveTimeoutMs` and `typeResolveStableMs` Prefab arguments are removed.
Discover the current schema instead of replaying obsolete argument objects.
Add-component keeps its mutation-prepared persistence evidence for reconciling
an interrupted admitted mutation; type discovery has no resumable wait phase.

## Project-tool exception evidence

Immediate calls and cooperative job steps use the same exception boundary.
`VmProjectToolException` publishes the owner's typed rejection without creating
a Console exception. Unexpected exceptions publish `project_tool_exception`,
`toolName`, `exceptionType` and `stackTrace`, and log the original cause in Unity.
Job failure snapshots retain this same structured error. A generic null-reference
message without its failing source location is insufficient diagnostic evidence.
Every generated project-tool contract advertises the registry-owned unexpected
exception code alongside the tool's declared domain rejections.

## Package resolution

`packages/resolve` admits exact Git targets already declared in the project
manifest. Configure the immutable manifest pins first. A mismatched declaration
returns `package_manifest_target_mismatch` before a job is created. This command
resolves and verifies those targets; `packages/update-git` is the distinct owner
for updating one Git dependency. Both publish durable adoption evidence.

## Focused regression scope and static cost

The regression input is frozen to three component entry points, five removed
schema fields and three single-node Prefab fixtures. The schema matrix performs
exactly 3 x 5 = 15 property membership checks. The missing-type tests invoke six
entries with one type and one reference-type case with two types; they never open
a Prefab. Each positive fixture performs one component mutation and one explicit
unload/reopen readback, with at most two authoring roots alive concurrently.
At most three fixture assets exist across the suite, with only one active at a
time; fixture assets and preview scenes are retired in `finally`.

All native object work runs on the Unity Editor main thread. The tests add no
asset discovery, recursive hierarchy traversal, background mutation or cache.
Path cases normalize at most two strings per invocation. Exception fixtures
invoke one registered owner per case. Type admission uses the existing component
resolver once per distinct declared type instead of repeating resolution on
Editor updates. The resolver's implementation and accepted type naming contract
remain unchanged. Removed loops have no replacement polling or timing axis.
Static cost for the new bounded test matrix: PASS.
