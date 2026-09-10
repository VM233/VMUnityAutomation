# VM Unity Automation

`com.vm233.unity-automation` is the transport-neutral Unity Editor automation
core used by [VMUnityPipeline](https://github.com/VM233/VMUnityPipeline).
It contains the production command owners, rich contracts, reflected project-tool
registry, request isolation, and reload-resumable jobs. It does not open a socket,
start an HTTP server, register a second tool transport, or add an Editor dashboard/toolbar.

The Agent-facing path is:

```text
unity shell --protocol ndjson
  -> com.unity.pipeline
    -> com.vm233.unity-pipeline
      -> com.vm233.unity-automation
        -> Unity Editor production owners
```

## Installation

Consumers normally install `com.vm233.unity-pipeline` and pin this package by
full remote Git SHA in the project manifest. Its package dependency declares the
minimum compatible version. If a package needs the authoring API directly, pin an immutable
revision in `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.vm233.unity-automation":
      "https://github.com/VM233/VMUnityAutomation.git#<full-commit-sha>"
  }
}
```

Local `file:` dependencies, embedded copies, symlinks, and mutable branch pins are
not supported.

## Public boundaries

- [`image/resize`](Documentation~/image-resize.md) prepares a local PNG at the
  requested pixel dimensions through `vm_pt_image_resize`. It preserves aspect
  ratio and alpha, writes one explicit output, and returns readback evidence.
  Sprite import and PPU validation remain in `asset/import`.
- `texture/apply-sprite-preset` selects Custom alignment for an explicit `pivot`;
  copying a reference preserves its alignment and pivot together. Verify the
  resulting Sprite pivot with `sprite/pixel-check`, which reads imported sprites.
- `VmAutomationCatalog` owns deterministic, bounded discovery and exact contract
  lookup. A catalog page defaults to 10 and is capped at 50.
- `scriptableobject/info` reads serialized field values through the same reader as
  `serialized-object/get`, including collection entries and object-reference identities.
  Its nested values use depth 4 and array-element 256 limits with explicit truncation metadata;
  use `serialized-object/get` for a specific property path or caller-selected limits.
  Readback value schemas across ScriptableObject, serialized-object and component inspection
  use the recursive JSON value contract; values are not restricted to numbers.
- Serialized Quaternion properties accept an object with all four numeric `x`, `y`,
  `z`, and `w` components. Set the complete property, such as `m_LocalRotation`, in
  one write so Unity saves a complete rotation before persistence verification.
- `VmAutomationExecutor` is the only route/project-tool invocation boundary. It
  validates the absolute project binding, request identity, preconditions,
  confirmation, workspace isolation, Unity Undo ownership, callback completion,
  and structured errors before/after calling a production owner. Request identity
  remains executor metadata; only contracts that declare `idempotencyKey` receive
  the corresponding persistent-job metadata inside their owner invocation.
- `editor/play-mode-options` reads or updates the live Unity
  `EditorSettings.enterPlayModeOptions` owner. Mutations require stable Edit Mode
  and return both previous and current state so temporary validation settings can
  be restored exactly without editing `ProjectSettings` behind the Editor.
  Disabling the feature follows Unity's documented behavior by normalizing its
  option flags to `None`; callers restore a prior fast-play configuration from
  the returned `previous` state by enabling it with those flags.
- Reload-resumable workspace operations publish a durable job token and remain
  admission-queued until the first authorized `jobs/get` poll acknowledges
  that the client received it. Only then may `asset/refresh`, package mutation,
  asset transaction, or `editor/play-mode` cross a mutation or Domain Reload
  boundary. Continue polling until the requested state is confirmed.
  `stop` remains callable while another workspace job is blocked on Edit Mode
  and supersedes an unfinished `play`, so recovery cannot deadlock behind the
  blocked job.
  `pause`, `resume`, and `step` remain attached confirmation calls.
- Every requested clean compilation snapshots Unity's expected Editor script
  assemblies before the request and persists separate
  `assemblyCompilationStarted`, `assemblyCompilationFinished`, and (on Unity
  2022.2+) `assemblyCompilationNotRequired` products. This models Unity issue
  UUM-95901 without relabeling a not-required callback as a completed compile.
  The exact `CleanBuildCache` request, global compilation lifecycle, complete
  per-output terminal callback coverage, and Domain Reload are all required;
  started/finished callbacks remain separately observable because affected Unity
  versions can suppress both and emit `assemblyCompilationNotRequired` instead.
  Missing terminal coverage fails the job, and the result explicitly reports
  when that public-callback limitation was observed.
  The finish callback persists this cycle's evidence before `awaiting-compilation-outcome`
  reads Unity's native failure state in a stable Editor update or the next assembly
  domain. This prevents an earlier failed compilation from rejecting a repaired build.
  Package tests also reject compilation failures while restoring their manifest,
  including failures outside the per-assembly C# diagnostic stream.
- Package add/remove commands reject Play Mode with typed state details. Durable
  package update/resolve jobs remain queued with an `edit-mode-required` blocked
  reason and resume automatically after the Editor reaches stable Edit Mode.
  Git adoption requires the manifest ref, lock hash, registered identifier, and
  Unity-owned resolved-cache `_fingerprint` to match the same full commit SHA.
- `[VmProjectTool]`, `IVmProjectTool<TRequest, TResult>`, and
  `IVmPersistentProjectTool` are the project/package extension API.
- Project-tool catalog entries retain their real owning UPM package. Tools from
  a project assembly use the stable `project:<module>` identity instead of
  being misattributed to this package.
- Package extensions declare ownership once with the assembly-level
  `VmProjectToolPackageAttribute`; discovery reads it without Unity API calls,
  so background catalog commands remain thread-safe.
- An invocation that names a discovered but invalid or duplicate
  `[VmProjectTool]` returns the exact registration source and validation error
  as `invalid_project_tool` or `duplicate_project_tool`; it is not collapsed
  into `command_not_found`.
- `VmProjectToolJobStep` publishes every continuation state needed after a Domain
  Reload. No retained tool instance is treated as durable state.
- `VmAutomationSettings` owns only transport-neutral response/history and tool
  defaults. Team settings live in
  `ProjectSettings/VMUnityAutomationSettings.json`.
- `uitoolkit/audit-uss-styles` reports a fully inlineable simple class or ID
  selector with one authored consumer as an unsuppressible error. A reasoned
  `allow-single-use` marker remains available only when the selector owns a
  real non-inline contract such as a custom-property or multi-rule cascade.
  A multi-rule anchor does not exempt invariant base declarations when the class
  has one authored consumer and no runtime reference. Only properties whose
  values actually change on that same target may remain in the class; unchanged
  visual, text, layout, and visibility declarations are unsuppressible errors
  and belong on the sole consumer inline.
- `uitoolkit/audit-uxml-layout` reports inline `flex-shrink: 0` when authored
  fixed-size geometry proves that the relevant Flex line has no negative free
  space. It remains conservative for intrinsic or runtime-owned sizing and
  accepts a reasoned suppression for an external layout contract.
- `profiler/frame-data` reads one retained CPU hierarchy while Profiler recording
  is active or stopped, so callers can freeze the ring buffer before inspecting
  exact frames. Caller-selected `maxDepth` from `0` through `16`, `maxItems`, and
  `minTimeMs` keep the returned timing page bounded.

The existing domain implementations retain their audited route names, input/output
schemas, stable error codes, side effects, transaction metadata, and job evidence.
CLI consumers discover one command at a time and invoke through the Pipeline facade;
the full catalog is never injected into an Agent context.

## Persistence

Durable state, including pending client-adoption markers, is written below
`Library/VMUnityAutomation`. It is local to the absolute Unity project and is never
committed. Workspace, test, build, package, asset-transaction, and project-tool jobs
publish stable IDs plus access tokens for explicit get/cancel/cleanup calls.

## Source provenance

The first release migrated the audited production automation owners into this
transport-neutral package. The retired HTTP listener, request transport, agent
sessions, port registry, dashboard, toolbar, and server preferences were
intentionally excluded. Subsequent behavior changes are owned here.

See [configuration and ownership](Documentation~/configuration.md) and
[VFX Graph coverage](Documentation~/vfx-graph-tools.md).
