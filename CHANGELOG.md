# Changelog

All notable changes to this package are documented here.

## [0.3.50] - 2026-08-23

### Fixed

- Let `profiler/frame-data` select a bounded CPU hierarchy depth from `0`
  through `16` instead of silently truncating every frame at depth `3`, and
  publish the applied depth in the successful result.
- Normalize the later-added Editor and package-test assets through the
  repository's deterministic package GUID owner so the publish-time GUID
  audit covers every Unity-visible file.

## [0.3.49] - 2026-08-23

### Fixed

- Compile the new simple-selector rule counter against the package's Unity
  2021.3-compatible collection interfaces.

## [0.3.48] - 2026-08-23

### Fixed

- Report fully inlineable single-consumer simple USS class and ID selectors as
  unsuppressible errors, while retaining reasoned exceptions for real
  custom-property and multi-rule cascade contracts.

## [0.3.47] - 2026-08-22

### Fixed

- Describe `scene/save` as an in-place active-scene save by default and
  document its explicit `Assets/*.unity` save-as and overwrite boundaries.
- Compose exact `save` actions with direct-object grammar instead of
  publishing `Save for ...` catalog text.

## [0.3.46] - 2026-08-22

### Fixed

- Let `component/get-properties` opt into hidden native Unity serialized
  fields, return exact `propertyPath` values, and publish JSON-valued property
  results so built-in components such as `SortingGroup` can be discovered and
  configured without guessing private field names.
- Point `component/set-property` guidance back to the discovered property path.

## [0.3.45] - 2026-08-22

### Fixed

- Make the advertised `VMUnityAutomation.PackageSmoke` and
  `VMUnityAutomation.FullRegression` package-test selections include every
  current regression fixture, with a guard against uncategorized additions.
- Keep package-only selection guidance on `testing/run-package-tests` and stop
  publishing it on the generic `testing/run-tests` command.

## [0.3.44] - 2026-08-22

### Fixed

- Describe loaded-scene component add/remove commands with their exact
  hierarchy-path or instance-ID selectors and explicit `scene/save`
  persistence boundary.
- Compose single-token actions as direct-object sentences so catalog entries
  no longer publish misleading grammar such as `Add for ...`.

## [0.3.43] - 2026-08-22

### Fixed

- Publish the complete durable package-test job shape for both
  `testing/run-package-tests` and `testing/get-package-job`, including polling,
  access-token, compilation, result, and clear-state fields.

## [0.3.42] - 2026-08-22

### Fixed

- Publish the Game View overlay suppression, restoration, and paused-capture
  result fields in the closed `screenshot/game` output schema, keeping the
  catalog contract identical to successful command responses.

## [0.3.41] - 2026-08-22

### Fixed

- When an Automation identifier resolves to a discovered but invalid or
  duplicate `[VmProjectTool]`, return `invalid_project_tool` or
  `duplicate_project_tool` with the registration source and validation error
  instead of hiding the authoring failure behind `command_not_found`.
- Replace the 0.3.40 post-reload cache workaround after direct registry
  evidence showed the affected tool was already discovered and invalid.

## [0.3.40] - 2026-08-22

### Fixed

- Invalidate project-tool and Automation catalog caches on the first delayed
  Editor update after each assembly reload, so newly compiled or removed
  `[VmProjectTool]` contracts are discoverable without restarting Unity.

## [0.3.39] - 2026-08-22

### Fixed

- Preserve the transport request ID on durable workspace jobs so `jobs/get`
  can recover an admission-queued job by `requestId` when its start response
  is unavailable or unsafe to copy from wrapped terminal output.

## [0.3.38] - 2026-08-22

### Fixed

- Author VFX particle-system simulation space through its owning Context so
  Unity invalidates every owner and spaceable Slot, then publish with the
  official subasset-aware VFX save path.
- Reject and atomically roll back a data-object transaction when its semantic
  model changed but the serialized VFX asset bytes did not.

## [0.3.37] - 2026-08-22

### Fixed

- Publish the stable domain error codes each VFX Graph route can return,
  including graph transaction, simulation-space, component, settings,
  validation, and bake failures.

## [0.3.36] - 2026-08-22

### Added

- Report each VFX data object's supported simulation space and accepted enum
  values from `vfxgraph/info`.
- Add the atomic `set-data-object` VFX Graph transaction operation so particle
  systems can author semantic simulation space and typed data settings without
  editing serialized graph bytes.

## [0.3.35] - 2026-08-21

### Fixed

- Keep reload-resumable workspace jobs admission-queued until the first
  authorized status poll proves that their durable token reached the client.
  The poll publishes a durable background-thread acknowledgement which the
  main-thread runner adopts before any mutation or Domain Reload boundary.

## [0.3.34] - 2026-08-21

### Added

- Add `vfxgraph/component-control action=set-asset` for session-only assignment
  of a VFX asset to an exact loaded component, including before/after asset
  identity in runtime-state readback.

### Fixed

- Reject persistent VFX component transactions outside stable Edit Mode before
  taking rollback snapshots, returning `edit_mode_required` instead of a
  misleading transaction-and-rollback failure.

## [0.3.33] - 2026-08-21

### Fixed

- Author VFX Block enabled state through its Unity-owned activation Slot instead
  of attempting to write the read-only `VFXBlock.enabled` property. Both
  `add-node` and `set-node` now work across current VFX Graph versions while
  preserving `$activation` as the shared read/write identity.

## [0.3.32] - 2026-08-21

### Fixed

- Use the execution boundary's canonical `requires_play_mode` error in VFX
  component owners and publish the conditional runtime-state precondition for
  `vfxgraph/component-info`.

## [0.3.31] - 2026-08-21

### Fixed

- Publish `requires_play_mode` for every Play-Mode-only automation contract and
  include the exact VisualEffect step/simulation completion errors in
  `vfxgraph/component-control` catalog metadata.

## [0.3.30] - 2026-08-21

### Fixed

- Complete VisualEffect single-frame and bounded-simulation controls only after
  Unity processes the queued command in a subsequent VFX update. Results now
  publish before/after runtime state and observed time-delta evidence, and
  reject globally paused or normally playing targets where exact completion
  cannot be distinguished.

## [0.3.29] - 2026-08-21

### Fixed

- Resolve supported descendant and direct-child USS selectors through one shared
  static selector owner. Centered-overlay auditing now sees parent geometry and
  alignment supplied by scoped selectors such as `#Tree .slot`, while generated
  child auditing reuses the same selector contract instead of maintaining a
  separate parser.

## [0.3.28] - 2026-08-21

### Added

- Report fixed-size authored absolute overlays whose `left` and `top` exactly
  recalculate the center of a fixed-size parent that already owns both flex-axis
  alignments. The finding preserves `position: absolute` for real overlap,
  edge-owned anchors, and reasoned measured optical offsets while removing only
  duplicated centering math.

## [0.3.27] - 2026-08-21

### Added

- Report fixed Flex cross sizes on layout-only authored `VisualElement`
  containers whose visible in-flow children already establish the natural
  extent, while retaining visual, clipping, interaction, externally bounded,
  anchored, stretched, runtime-class, and reasoned suppression contracts.

## [0.3.26] - 2026-08-21

### Fixed

- Assign the new unbounded flex-shrink auditor source its package-owned
  deterministic Unity asset GUID.

## [0.3.25] - 2026-08-21

### Added

- Report `flex-shrink: 0` declarations that only win for authored elements under
  natural-size Flex parents with no finite main-axis extent, while retaining
  bounded, anchored, externally allocated, runtime-class, and reasoned
  suppression contracts.

## [0.3.24] - 2026-08-21

### Fixed

- Specialize numeric dynamic VFX Operators with the same unified, constrained,
  and uniform operand negotiation used by the VFX Graph editor before
  `connect-data` creates a Slot link. Connection results now publish the exact
  adopted endpoint types and whether the input was specialized.

## [0.3.23] - 2026-08-21

### Fixed

- Publish VFX Block Activation Slots through the reserved `$activation` input
  selector so graph inspection and authoring preserve data links targeting
  activation conditions instead of misreporting supported graphs as an
  unsupported VFX version.

## [0.3.22] - 2026-08-21

### Fixed

- Publish the loaded-scene Undo transaction, reference-remap evidence, and
  dirty-scene boundary for `component/move` instead of incorrectly describing
  the command as having no transaction.

## [0.3.21] - 2026-08-21

### Fixed

- Refresh the audited core and optional-provider route-manifest fingerprints
  whenever route contracts are generated, preventing a newly registered route
  from failing the Automation registry type initializer at runtime.

## [0.3.20] - 2026-08-21

### Added

- Add `component/move` for atomic loaded-scene component migration with exact
  source/target selectors, serialized-state preservation, scene-local reference
  remapping, one Undo transaction, and a closed CLI contract.

### Fixed

- Keep both importer and authoritative default-platform compression fields in
  the reviewed `sprite/pixel-check` output when regenerating route contracts.

## [0.3.19] - 2026-08-21

### Fixed

- Align built-in catalog error metadata with the execution boundary: read-only
  tools no longer advertise mutation-only project-binding failures, while
  `editor/execute-code` now declares every structured compilation and execution
  error it can return.

## [0.3.18] - 2026-08-21

### Fixed

- Accept Unity's default `Automatic` texture format as uncompressed when its
  authoritative platform compression is `Uncompressed`, and expose both the
  importer and platform compression in `sprite/pixel-check` results so
  pixel-art validation no longer reports a false compression warning.

## [0.3.17] - 2026-08-21

### Fixed

- Treat a Play Mode option request as a true no-op when both live and persisted
  `EditorSettings` already match, avoiding needless Project Settings rewrites
  and serialization-version churn.

## [0.3.16] - 2026-08-21

### Fixed

- Persist `editor/play-mode-options` through Unity's authoritative serialized
  `EditorSettings` owner and verify the on-disk Project Settings state before
  reporting success, so the change survives an Editor restart.
- Include the required `confirm: true` boundary field in every dangerous
  built-in and project-tool input schema, advertise its typed failure, and
  consume the acknowledgement before forwarding closed arguments to owners.

## [0.3.15] - 2026-08-21

### Fixed

- Do not fail a durable Play/Stop transition on its first post-reload tick when
  Unity has already reached the requested state but lengthy project startup
  consumed the wall-clock timeout; once observed at the target, finish the
  requested stable-frame confirmation.

## [0.3.14] - 2026-08-21

### Fixed

- Publish asset-refresh and Git-package update/resolve results through the
  canonical durable Job snapshot schema, including their real structured
  `error` product instead of incorrectly advertising it as a string.
- Include the immediate idempotency and ownership conflicts that durable
  workspace submissions can actually return in their catalog error codes.
- Publish completion evidence from the owner catalog: job-bearing results are
  durable admission products that must be polled, while immediate results are
  already completed owner evidence.

## [0.3.13] - 2026-08-21

### Fixed

- Publish `editor/play-mode` play/stop transitions as durable jobs before
  changing Editor state, so a normal Domain Reload no longer disconnects the
  caller before it receives a reconnectable job token.
- Preserve attached confirmation for pause, resume, and single-frame step while
  documenting the action-specific completion contract and idempotent durable
  transition input.
- Allow `stop` to pass an active workspace-job gate and safely supersede an
  in-flight `play`, preventing a Play-blocked package job from deadlocking its
  own Edit Mode recovery command.
- Register the Play Mode transition job as a first-class lifecycle owner so
  `jobs/get`, cancellation, cleanup, and typed job lookup share one authority.

## [0.3.12] - 2026-08-21

### Fixed

- Normalize Enter Play Mode option flags to `None` when disabling the feature,
  matching Unity's documented owner behavior instead of reporting a false
  persistence failure after the requested reload mode was already active.
- Reject contradictory requests that disable Enter Play Mode Options while also
  asking Unity to skip a reload, with guidance to enable the feature in the same
  call.

## [0.3.11] - 2026-08-21

### Fixed

- Merge the stable Edit Mode guidance into the existing package add/remove
  catalog descriptions instead of declaring duplicate switch labels.

## [0.3.10] - 2026-08-20

### Fixed

- Prevent package add/remove operations from starting outside stable Edit Mode,
  with a typed `edit_mode_required` error and actionable state details.
- Keep durable package update/resolve jobs queued until stable Edit Mode, publish
  `edit-mode-required` as the blocked reason, and explain that exiting Play Mode
  resumes the same job before any Package Manager mutation begins.
- Advertise `stableEditMode` in package-mutation contracts so CLI discovery
  exposes the real execution precondition before a caller submits work.

## [0.3.9] - 2026-08-20

### Added

- Add a typed `editor/play-mode-options` owner that reads and updates live Unity
  `EditorSettings`, preserves omitted flags, requires stable Edit Mode for mutation,
  and returns exact previous/current state for deterministic restoration.

### Fixed

- Publish the actual action, state-timeout, and step-timeout error codes of
  `editor/play-mode` instead of advertising only generic executor failures.
- Keep both VFX Graph transaction result variants in generated contracts and
  include the dry-run `assetHash` that the production owner returns.

## [0.3.8] - 2026-08-20

### Fixed

- Resolve component types supplied as `Namespace.Type, AssemblyName` across prefab,
  component, and transaction tools while preserving the explicit assembly as a binding
  constraint instead of silently selecting a same-named type elsewhere.
- Document short, full, and assembly-qualified component type forms in generated schemas.

## [0.3.7] - 2026-08-20

### Added

- Expose a thread-safe, read-only public boundary for the latest immutable persistent-job
  snapshot so a CLI package can poll durable automation while Unity's main thread is busy.

## [0.3.6] - 2026-08-20

### Fixed

- Treat Unity Package Manager cancellation as a bounded transient failure for durable Git
  package updates: accept an already-adopted target or retry once after Editor idleness.
- Persist each package update attempt and return the Package Manager error code, requested
  immutable target, observed package state, and attempt history when the retry still fails.

## [0.3.5] - 2026-08-20

### Fixed

- Execute VFX Graph dry runs against a unique imported copy and delete that
  copy before returning, so the authoritative graph is never mutated and no
  stale in-memory model can race the following real transaction.

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
