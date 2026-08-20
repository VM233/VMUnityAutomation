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

Consumers normally install `com.vm233.unity-pipeline`, which pins this package by
full remote Git SHA. If a package needs the authoring API directly, pin an immutable
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

- `VmAutomationCatalog` owns deterministic, bounded discovery and exact contract
  lookup. A catalog page defaults to 10 and is capped at 50.
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
- `editor/play-mode` publishes a durable job token before `play` or `stop`
  changes Editor state. This keeps the transition observable through normal
  Domain Reload; poll `jobs/get` until the requested state is confirmed.
  `stop` remains callable while another workspace job is blocked on Edit Mode
  and supersedes an unfinished `play`, so recovery cannot deadlock behind the
  blocked job.
  `pause`, `resume`, and `step` remain attached confirmation calls.
- Package add/remove commands reject Play Mode with typed state details. Durable
  package update/resolve jobs remain queued with an `edit-mode-required` blocked
  reason and resume automatically after the Editor reaches stable Edit Mode.
- `[VmProjectTool]`, `IVmProjectTool<TRequest, TResult>`, and
  `IVmPersistentProjectTool` are the project/package extension API.
- Project-tool catalog entries retain their real owning UPM package. Tools from
  a project assembly use the stable `project:<module>` identity instead of
  being misattributed to this package.
- Package extensions declare ownership once with the assembly-level
  `VmProjectToolPackageAttribute`; discovery reads it without Unity API calls,
  so background catalog commands remain thread-safe.
- `VmProjectToolJobStep` publishes every continuation state needed after a Domain
  Reload. No retained tool instance is treated as durable state.
- `VmAutomationSettings` owns only transport-neutral response/history and tool
  defaults. Team settings live in
  `ProjectSettings/VMUnityAutomationSettings.json`.

The existing domain implementations retain their audited route names, input/output
schemas, stable error codes, side effects, transaction metadata, and job evidence.
CLI consumers discover one command at a time and invoke through the Pipeline facade;
the full catalog is never injected into an Agent context.

## Persistence

Durable state is written below `Library/VMUnityAutomation`. It is local to the
absolute Unity project and is never committed. Workspace, test, build, package,
asset-transaction, and project-tool jobs publish stable IDs plus access tokens for
explicit get/cancel/cleanup calls.

## Source provenance

The first release migrated the audited production automation owners into this
transport-neutral package. The retired HTTP listener, request transport, agent
sessions, port registry, dashboard, toolbar, and server preferences were
intentionally excluded. Subsequent behavior changes are owned here.

See [configuration and ownership](Documentation~/configuration.md) and
[VFX Graph coverage](Documentation~/vfx-graph-tools.md).
