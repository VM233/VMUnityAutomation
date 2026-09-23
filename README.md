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

`VmAutomationCatalog` provides bounded discovery and exact command contracts.
`VmAutomationExecutor` owns invocation validation, project binding, effects,
errors, and durable job admission. Project and package extensions use
`[VmProjectTool]` and the typed project-tool interfaces. The current route
names, schemas, and effects are published by the catalog.

Focused references:

- [Configuration, catalog, and ownership](Documentation~/configuration.md)
- [UI Toolkit authoring audits](Documentation~/uitoolkit-audits.md)
- [Command effects](Documentation~/command-effects.md)
- [Editor window capture](Documentation~/editor-window-capture.md)
- [Test result sessions](Documentation~/test-result-session.md)
- [Editor profiling](Documentation~/editor-profiling.md)
- [Build profiles](Documentation~/build-profiles.md)
- [Image resizing](Documentation~/image-resize.md) and [Sprite mesh review](Documentation~/sprite-mesh-review.md)
- [Cooperative project tools](Documentation~/cooperative-project-tools.md)
- [Incremental job persistence](Documentation~/incremental-job-persistence.md)
- [VFX Graph coverage](Documentation~/vfx-graph-tools.md)

## Persistence

Command effects are owned by the route profile, including build output writes,
process launch, preferences and job cancellation. Player builds require stable
Edit Mode. `build/get-job` accepts optional history cleanup and therefore requires
the same explicit project binding as other mutating contracts. See
[command effect ownership](Documentation~/command-effects.md).

Durable state, including pending client-adoption markers, is written below
`Library/VMUnityAutomation`. It is local to the absolute Unity project and is never
committed. Workspace, test, build, package, asset-transaction, and project-tool jobs
publish stable IDs plus access tokens for explicit get/cancel/cleanup calls.

See [CHANGELOG](CHANGELOG.md) for release history.
