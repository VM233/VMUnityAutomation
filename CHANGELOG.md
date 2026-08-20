# Changelog

All notable changes to this package are documented here.

## [0.1.4] - 2026-08-20

### Changed

- Rename the public JSON data-product keyword to `x-vmAutomationContract` so
  Pipeline schemas no longer expose the retired MCP transport name.

## [0.1.3] - 2026-08-20

### Fixed

- Make catalog package identity a build-time constant so bounded discovery remains safe
  on the official Pipeline background command thread.

## [0.1.2] - 2026-08-20

### Fixed

- Assign package-owned deterministic Unity GUIDs to every migrated asset so the new
  automation package can coexist with the retiring MCP package during cutover.

## [0.1.1] - 2026-08-20

### Changed

- Prefix built-in automation identifiers with `vm_auto_` so they cannot collide with the small official CLI facade.
- Stabilize lower-camel JSON names for invocation results and errors.
- Preserve the requested command identity in project-binding failures.

## [0.1.0] - 2026-08-20

### Added

- Transport-neutral automation route and project-tool owners migrated from the
  audited `VMUnityMCP` 10.1.2 source revision.
- Bounded rich catalog with exact command lookup and deterministic revision hash.
- Single execution boundary with absolute project binding, idempotent request IDs,
  stable errors, confirmation, preconditions, workspace isolation, Unity Undo
  ownership, action history, and deferred callback adaptation.
- Existing reload-resumable workspace, test, build, package, asset-transaction, and
  project-tool job owners under `Library/VMUnityAutomation`.
- Renamed project-tool authoring API in the `VMUnityAutomation.Editor` namespace.

### Removed

- MCP HTTP listener and route dispatcher.
- MCP agent sessions, network request queue, port/instance registry, health and ping
  routes, dashboard, toolbar, self-test UI, context generator, update checker, and
  server preferences.
