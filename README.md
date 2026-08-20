# VM Unity Automation

`com.vm233.unity-automation` is the transport-neutral Unity Editor automation
core used by [VMUnityPipeline](https://github.com/VM233/VMUnityPipeline).
It contains the production command owners, rich contracts, reflected project-tool
registry, request isolation, and reload-resumable jobs. It does not open a socket,
start an HTTP server, register MCP tools, or add an Editor dashboard/toolbar.

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
  and structured errors before/after calling a production owner.
- `[VmProjectTool]`, `IVmProjectTool<TRequest, TResult>`, and
  `IVmPersistentProjectTool` are the project/package extension API.
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

The first release mechanically migrated the production automation owners from
`VMUnityMCP` revision
`3441d9e63486d51e3bdccf872cc1c5bcdd1ac23c`. The MCP HTTP server, request transport,
agent sessions, port registry, dashboard, toolbar, and server preferences were
intentionally excluded. Subsequent behavior changes are owned here.

See [configuration and ownership](Documentation~/configuration.md) and
[VFX Graph coverage](Documentation~/vfx-graph-tools.md).
