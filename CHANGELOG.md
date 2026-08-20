# Changelog

All notable changes to this package are documented here.

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
