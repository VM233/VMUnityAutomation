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
- [Project binding, Prefab admission, and CLI failures](Documentation~/cli-invocation.md)
- [Serialized field values](Documentation~/serialized-field-values.md)
- [Semantic importer settings](Documentation~/importer-settings.md)
- [Material property types](Documentation~/material-property-types.md)
- [UI Toolkit authoring audits](Documentation~/uitoolkit-audits.md)
- [Command effects](Documentation~/command-effects.md)
- [Editor window capture](Documentation~/editor-window-capture.md)
- [Test result sessions](Documentation~/test-result-session.md)
- [Editor profiling](Documentation~/editor-profiling.md)
- [Native physics membership](Documentation~/physics-membership.md)
- [Native collision matrices](Documentation~/physics-collision-dimensions.md)
- [Build profiles](Documentation~/build-profiles.md)
- [Image resizing](Documentation~/image-resize.md) and [Sprite mesh review](Documentation~/sprite-mesh-review.md)
- [Cooperative project tools](Documentation~/cooperative-project-tools.md)
- [Incremental job persistence](Documentation~/incremental-job-persistence.md)
- [Main Editor state ownership and history reconstruction](Documentation~/main-editor-state-ownership.md)
- [Scene workspace reload](Documentation~/scene-workspace.md)
- [Prefab Variant reversion](Documentation~/prefab-variant-revert.md)
- [Registered package metadata](Documentation~/registered-package-metadata.md)
- [VFX Graph coverage](Documentation~/vfx-graph-tools.md)

See [CHANGELOG](CHANGELOG.md) for release history.
