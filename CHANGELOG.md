# Changelog

All notable changes to this package are documented here.

## [0.3.4] - 2026-08-20

### Fixed

- Reload the authoritative unchanged VFX Graph bytes after a successful dry
  run, because VFX Graph's in-memory backup restore can retain newly created
  models and poison the following real transaction.
- Return the verified original asset hash from VFX Graph dry runs.

## [0.3.3] - 2026-08-20

### Fixed

- Remove executor-owned `expectedProjectPath` metadata after project-binding
  validation so strict built-in owners receive only their declared business
  arguments.

## [0.3.2] - 2026-08-20

### Fixed

- Mark `expectedProjectPath` as required in every mutating command's published
  input schema, matching the executor's project-binding safety contract.

## [0.3.1] - 2026-08-20

### Fixed

- Keep executor-owned request identity out of closed owner argument objects unless
  the selected contract explicitly declares persistent idempotency support.
- Stop injecting an undeclared `_requestId` and synthetic `idempotencyKey` into
  strict immediate commands such as VFX Graph inspection.
- Remove the unused Unity package import request-ID serialization field; durable
  workspace jobs continue to receive request identity through their declared
  idempotency contract.

## [0.3.0] - 2026-08-20

### Changed

- Remove the remaining retired-transport names from public debug results, JSON
  schema extensions, generated contracts, temporary files, and documentation.
- Rename Automation-owned temporary artifacts without changing their lifecycle
  or cleanup ownership.

## [0.2.4] - 2026-08-20

### Removed

- Remove the last profile, schema, description, and configuration metadata for
  the eleven retired transport-only routes.

## [0.2.3] - 2026-08-20

### Fixed

- Centralize shared asset-description and prefab text I/O helpers after the
  source split, and restore their required Editor and file-system imports.

## [0.2.2] - 2026-08-20

### Fixed

- Restore explicit shared-utility ownership, imports, and visibility across the
  prefab, UI Toolkit, asset import, project-tool, and UXML source splits.

## [0.2.1] - 2026-08-20

### Fixed

- Restore the project-tool descriptor name after the transport-neutral rename.
- Preserve UXML model visibility and internal helper accessibility after the
  responsibility-based source split.

## [0.2.0] - 2026-08-20

### Changed

- Complete the transport-neutral API migration by renaming the remaining
  legacy-transport-prefixed C# types, files, contracts, logs, and durable automation identities
  to the `VmAutomation` vocabulary while preserving Unity asset GUIDs.
- Split oversized command owners into component-focused services for assets,
  animation clips, prefab components/variants/transactions, Shader Graph documents,
  terrain heightmaps, UI Toolkit authoring/runtime inspection, and UXML layout audit.
- Make the descriptor registry the only built-in route authority and regenerate all
  395 route contracts from it with zero unresolved output schemas.

### Removed

- Delete the final health, instance, agent-session, ping, and legacy catalog route
  contracts that belonged to the retired socket transport.
- Remove generator fallbacks to the retired dispatcher and deferred-route registry.

## [0.1.7] - 2026-08-20

### Fixed

- Preserve each reflected project tool's resolved owner package when adapting it
  into the transport-neutral automation catalog, so CLI package filters distinguish
  package extensions and project-local tools from built-in automation commands.

## [0.1.6] - 2026-08-20

### Fixed

- Resolve project-tool package ownership from an explicit assembly declaration
  using reflection, keeping background catalog discovery free of main-thread-only
  Unity Package Manager calls.

## [0.1.5] - 2026-08-20

### Fixed

- Publish the real owning UPM package for package-provided project tools and a
  stable `project:<module>` identity for project-local tools, so bounded CLI
  discovery no longer attributes every extension to VM Unity Automation.

## [0.1.4] - 2026-08-20

### Changed

- Rename the public JSON data-product keyword to `x-vmAutomationContract` so
  Pipeline schemas no longer expose the retired socket transport name.

## [0.1.3] - 2026-08-20

### Fixed

- Make catalog package identity a build-time constant so bounded discovery remains safe
  on the official Pipeline background command thread.

## [0.1.2] - 2026-08-20

### Fixed

- Assign package-owned deterministic Unity GUIDs to every migrated asset so the new
  automation package can coexist with the retiring socket package during cutover.

## [0.1.1] - 2026-08-20

### Changed

- Prefix built-in automation identifiers with `vm_auto_` so they cannot collide with the small official CLI facade.
- Stabilize lower-camel JSON names for invocation results and errors.
- Preserve the requested command identity in project-binding failures.

## [0.1.0] - 2026-08-20

### Added

- Transport-neutral automation route and project-tool owners migrated from the
  audited predecessor source revision.
- Bounded rich catalog with exact command lookup and deterministic revision hash.
- Single execution boundary with absolute project binding, idempotent request IDs,
  stable errors, confirmation, preconditions, workspace isolation, Unity Undo
  ownership, action history, and deferred callback adaptation.
- Existing reload-resumable workspace, test, build, package, asset-transaction, and
  project-tool job owners under `Library/VMUnityAutomation`.
- Renamed project-tool authoring API in the `VMUnityAutomation.Editor` namespace.

### Removed

- Retired HTTP listener and route dispatcher.
- Retired agent sessions, network request queue, port/instance registry, health and ping
  routes, dashboard, toolbar, self-test UI, context generator, update checker, and
  server preferences.
