# VM Unity Automation

`com.vm233.unity-automation` is the transport-neutral Unity Editor automation
core and runtime observation owner used by [VMUnityPipeline](https://github.com/VM233/VMUnityPipeline).
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

The minimum supported Editor is Unity 2021.3.18f1, as declared in `package.json`.
The package requires Unity Test Framework 1.4.6 or newer for native test-run
cancellation. Build Profiles remain an optional native Unity 6 capability;
older Editors return `capability_unavailable` and can use the other commands.

## Public boundaries

`VmAutomationCatalog` provides bounded discovery and exact command contracts.
`VmAutomationExecutor` owns invocation validation, project binding, effects,
errors, and durable job admission. Project and package extensions use
`[VmProjectTool]` and the typed project-tool interfaces. The current route
names, schemas, and effects are published by the catalog.

- [Feature references](Documentation~/index.md)
- [Continuous runtime frame observation](Documentation~/continuous-frame-capture.md)
- [Release history](CHANGELOG.md)
