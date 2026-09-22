# Changelog

All notable changes to this package are documented here.

## [0.6.33] - 2026-09-23

### Fixed

- Allow Unity `UxmlElement` declarations to remain `partial` when partial-type
  rejection is enabled, preserving the source-generation contract while still
  rejecting ordinary hand-written partial types.
- Normalize later-added documentation, Editor, and test assets through the
  package's deterministic GUID owner so the publish-time audit passes.

## [0.6.32] - 2026-09-22

### Added

- Reject unnamed, noninteractive UXML `VisualElement` placeholders explicitly
  authored as layout balances, counterweights, spacers, or shims. Semantic
  containers must own alignment while independent edge controls use anchored
  positioning.

## [0.6.31] - 2026-09-21

### Added

- Report localized buttons with a fixed authored width so translations can size
  the control through `min-width` and horizontal padding, with a reasoned
  suppression for measured fixed artwork, clipping, or interaction contracts.

## [0.6.30] - 2026-09-21

### Added

- Report single-axis `ScrollView` elements that fix their non-scrolling cross
  axis even though authored content can size that axis naturally, while keeping
  fixed bounds on the scrolling axis valid.
- Cover both horizontal-list fixed heights and vertical-list fixed widths, with
  a reasoned suppression for measured cross-axis clipping contracts.

## [0.6.29] - 2026-09-21

### Added

- Reject three or more non-overlapping absolute-positioned siblings that form a
  horizontal or vertical sequence, including heterogeneous UI element types,
  because the parent should normally own that sequence through Flex flow.
- Preserve intentional overlay, edge-chrome, popup, and canvas layouts through
  the existing reasoned `allow-manual-sibling-layout` contract.

## [0.6.28] - 2026-09-21

### Fixed

- Keep the public UXML audit description explicit about both the required
  design-time preview and its declared runtime text owner, with package-smoke
  coverage for both contract terms.

## [0.6.27] - 2026-09-21

### Changed

- Reject blank runtime-owned UXML `Label` elements even when they declare an
  `allow-runtime-text` owner, requiring a representative authored value that is
  visible in UI Builder before the runtime producer replaces it.

## [0.6.26] - 2026-09-21

### Added

- Reject authored `background-image` declarations on UXML `Label` elements as
  hard errors, requiring icon artwork to use a dedicated `VisualElement` beside
  the text owner.

## [0.6.25] - 2026-09-21

### Added

- Report ineffective `scale-to-fit` and `scale-and-crop` USS declarations when
  every statically authored consumer has a fixed box matching its resolved
  background image aspect ratio, with conservative handling for unresolved or
  runtime-assigned content.

## [0.6.24] - 2026-09-21

### Fixed

- Reject Editor-window screen captures unless the exact requested native window
  is foreground before and after the pixel copy, preventing lock-screen or
  unrelated desktop pixels from passing UI Builder visual analysis.
- Require UI Builder visual analysis to consume an explicit verified-window
  capture receipt before decoding or judging screenshot pixels.

## [0.6.23] - 2026-09-21

### Fixed

- Give legacy UXML layout self-test labels explicit fixture text, so the new
  text-ownership rule does not add unrelated findings or crash aggregate
  self-tests that assert a single layout issue.

## [0.6.22] - 2026-09-21

### Fixed

- Publish the UXML text-ownership check in the route catalog description, so
  callers can discover that empty labels require a binding or an explicit
  runtime text owner.

## [0.6.21] - 2026-09-21

### Added

- Report empty UXML `Label` elements without a `text` binding as hard errors, so
  fixed localized copy cannot disappear from UI Builder while waiting for a
  runtime script assignment.
- Require computed runtime labels to declare their text owner with an adjacent,
  reasoned `allow-runtime-text` suppression that remains visible in audit output.

## [0.6.20] - 2026-09-20

### Fixed

- Validate Git packages installed through a `?path=` subdirectory against the
  registered commit ref and their non-empty UPM content fingerprint, whose value
  is a package-subtree hash rather than the repository commit SHA.

## [0.6.19] - 2026-09-20

### Fixed

- Isolate the fixed-font-size/auto-size USS self-test fixture from the separate
  only-child inheritance rule, and exercise the aggregate USS self-tests in the
  package smoke suite.

## [0.6.18] - 2026-09-20

### Added

- Add the read-only `code/policy-review` route for bounded Roslyn syntax policy
  checks over explicit, rooted, or Git-changed C# files.
- Add the read-only `package/dependency-policy-review` route for immutable Git
  pins, manifest-lock-resolved revision agreement, local or embedded dependency
  rejection, and bounded Unity meta ownership checks.
- Extend UI Toolkit audits with hard errors for grouped USS selectors, margin or
  padding shorthand, empty selector blocks, fixed font size combined with
  effective auto sizing, bound UXML literal fallbacks, and placeholder literals.

### Changed

- Publish error counts separately from warnings in UXML audit results.

## [0.6.17] - 2026-09-20

### Fixed

- Keep the new Build Profile creation branch compatible with the package's C#
  compilation scope by using distinct local names.

## [0.6.16] - 2026-09-20

### Added

- Let `build/profile` enumerate installed Unity platforms and create native
  Build Profile assets for an explicit installed platform GUID before applying
  the existing profile settings operations.

## [0.6.15] - 2026-09-20

### Fixed

- Publish `spriteMeshType` in the semantic importer command input schema so the
  catalog accepts the same field that the command validates, writes, and reads back.

## [0.6.14] - 2026-09-20

### Added

- Add the read-only `asset/sprite-mesh-review` project tool. It scans every
  Sprite `TextureImporter` below caller-selected `Assets` roots, requires
  `FullRect`, and publishes complete aggregate counts with bounded issue details.

### Changed

- Expose `spriteMeshType` through `asset/import-settings/get` and
  `asset/import-settings/set`, including semantic readback after writes.

## [0.6.13] - 2026-09-13

### Documentation

- Record inspected Hierarchy and Console capture acceptance, its focused test
  and compilation results, and the Editor-restart limit on causal attribution.

## [0.6.12] - 2026-09-13

### Fixed

- Repaint the selected Editor view after raising its native host and before
  flushing composition for desktop capture. Expose a missing or failed immediate
  repaint instead of silently capturing without it.

## [0.6.11] - 2026-09-13

### Added

- Publish native host, crop, desktop, panel and DPI coordinates with Editor
  window captures. All-black capture failures retain the same geometry and
  screen preparation diagnostics under the declared `blank_capture` error.

## [0.6.10] - 2026-09-13

### Fixed

- Select desktop composition for Editor windows with retained UI content,
  including Unity 6000.6 Hierarchy, instead of returning a white PrintWindow PNG.
- Expose the supported capture modes and the actual Windows result schema.
  Declare screenshot file writes and temporary Editor view changes. Remove
  undocumented capture-mode aliases and window-title rendering heuristics.

## [0.6.9] - 2026-09-13

### Fixed

- Preserve all Test Runner leaf results across assembly reloads, including
  passed, skipped and inconclusive tests and failures beyond the summary limit.
  Publish one result and one changed job per callback instead of rewriting all
  retained jobs. Detailed pages preserve original numeric duration values.
- Retire expired test result records with their owning jobs. The private reload
  cache starts empty on upgrade while durable summaries remain available.

## [0.6.8] - 2026-09-13

### Added

- Explicit Profiler capture retirement through `profiler/enable` with
  `clearFrames`, publishing the previous and resulting retained frame ranges.
  Stopping recording alone continues to preserve captured evidence.

## [0.6.7] - 2026-09-12

### Fixed

- Reuse validated persistent-job identity hashes and publish the running-state
  transition only when execution starts. Incremental progress keeps atomic
  durability without repeatedly allocating all identity hashes or rewriting
  an unchanged running state before each step.

## [0.6.6] - 2026-09-12

### Fixed

- Persist incremental job progress and public history one record at a time,
  removing full retained-history serialization from every Editor update.
  Migrate existing records without losing job identities or access capabilities.

## [0.6.5] - 2026-09-12

### Added

- Expose Editor sampling through `profiler/enable`, so editor-driven work can
  be attributed beyond the opaque `EditorLoop` total. Publish previous Profiler
  switches for exact restoration after a capture.

## [0.6.4] - 2026-09-12

### Fixed

- Use Unity's serial Edit Mode test runner without NUnit's unsupported
  `NonParallelizable` attribute in the cooperative cancellation fixture.

## [0.6.3] - 2026-09-12

### Added

- Capture a running project's job cancellation identity for cooperative Editor
  callbacks without serializing job history on every runtime slice. The check
  remains attached to its owner across other job steps and terminal transitions.

## [0.6.2] - 2026-09-12

### Fixed

- Publish owned effects for builds, tests, preferences, debugger execution,
  view changes and job cancellation. Require stable Edit Mode for Player
  builds and project binding for build polling that can clear job history.
- Preserve immutable declared effects through profile cloning and catalog
  publication instead of leaving these mutating commands without effects.

## [0.6.1] - 2026-09-10

### Fixed

- Feed the Prefab override regression fixture the same list representation as
  the public JSON command, so the test reaches the transaction handler.

## [0.6.0] - 2026-09-10

### Added

- Add `revertProperty` to `prefab-asset/transaction-edit`. Remove one serialized
  override using Unity's Prefab inheritance API, including vector child fields,
  while preserving unrelated properties. Reject properties without an inherited
  source at the transaction boundary.

## [0.5.1] - 2026-09-10

### Fixed

- Allow `asset/import` to resize an existing PNG in place with explicit resize
  dimensions and `overwrite=true`. Reuse the immutable preflight image and
  existing transaction, preserving GUID, local file ID, pivot and PPU without
  a staging PNG. Cover dry-run, admission and deferred rollback.

## [0.5.0] - 2026-09-10

### Added

- Add optional `resize` to `asset/import` defaults and entries. Prepare PNGs in
  memory, deduplicate and validate slices against resized content, write the final
  asset directly, and verify its dimensions/hash under the existing rollback
  transaction. No staging image is required and PPU remains an importer setting.
- Share bounded PNG preparation with `image/resize`. Cover alpha fidelity, dry-run,
  resized-content dedupe, identity-preserving replacement, slice admission,
  malformed inputs, batch limits and deferred rollback in focused tests.

## [0.4.1] - 2026-09-10

### Fixed

- Publish image resize dimensions, hashes, and verification through the typed
  JSON contract. Keep result setters private while including them in the schema
  and transport response, and cover both public contract boundaries in tests.

## [0.4.0] - 2026-09-10

### Added

- Add the typed `image/resize` package tool (`vm_pt_image_resize`) for local PNG
  preparation. It preserves aspect ratio and transparent-edge colors, supports
  nearest-neighbor sampling, validates bounded dimensions, and returns verified
  hashes without creating report or staging files. Unity import settings and PPU
  remain owned by the existing asset import workflow.

## [0.3.69] - 2026-09-09

### Fixed

- Open imported VFX Graph resources through `GetGraph` on Unity 6.6, where
  `GetOrCreateGraph` was removed. Earlier supported Unity versions retain their
  version-specific API. Graph and component inspection share this session owner.
- Cover repeated opening of an imported VFX Graph without changing its asset
  bytes or resource identity.

## [0.3.68] - 2026-09-09

### Fixed

- Persist completed compiler callbacks before reading Unity's native compilation
  outcome from a stable Editor update or the next assembly domain. A stale failure
  flag inside `compilationFinished` no longer rejects a repaired compilation.
- Package tests now report pipeline-level compilation failures during original
  manifest restoration, even when no per-assembly C# diagnostic was emitted.
- Normalize three test-fixture asset GUIDs with the package's deterministic GUID
  owner so the package metadata check passes.

## [0.3.67] - 2026-09-08

### Fixed

- Package smoke and full-regression selection now include the serialized-object
  and quaternion property fixtures, keeping the selection-coverage gate green.

## [0.3.66] - 2026-09-08

### Fixed

- Serialized ObjectReference writes now resolve the compatible object or Sprite
  sub-asset at an asset path instead of assigning an incompatible main asset and
  silently clearing the property. Object-reference readback includes stable GUID
  and local file ID selectors for exact round trips.

## [0.3.65] - 2026-09-08

### Fixed

- Shared serialized-property writes now assign Quaternion values atomically from
  an explicit `{x,y,z,w}` object. Prefab rotations can be saved and verified without
  intermediate component writes being independently normalized by Unity.

## [0.3.64] - 2026-09-08

### Fixed

- Applying an explicit sprite pivot now selects Custom alignment, so the imported
  Sprite uses the requested pivot instead of retaining a preset such as Center.
- Reference texture settings preserve sprite alignment together with the pivot.
  Other importer settings and the Single/Multiple import mode remain unchanged.

## [0.3.63] - 2026-09-03

### Fixed

- ScriptableObject inspection and post-write readback now use the shared serialized-value reader,
  preserving array entries, nested fields and object-reference identities instead of returning
  type names or display-only strings.
- Correct generated ScriptableObject, serialized-object and prefab-component readback value
  schemas to describe JSON values, including strings, booleans, null references and structured
  collections, instead of incorrectly requiring numbers.

## [0.3.62] - 2026-08-29

### Fixed

- Correct the expected clean-compilation identity set to use assembly output
  paths, preserving dotted names such as `Unity.2D.Animation.Runtime` instead
  of treating their final segment as a file extension.
- Accept Unity 6's observed UUM-95901 behavior only when the exact
  `CleanBuildCache` request has a full global lifecycle, every expected output
  has finished/not-required terminal coverage, and assembly reload completes.
  Started and finished callbacks remain separate diagnostics and are not
  required when Unity suppresses both.

## [0.3.61] - 2026-08-29

### Fixed

- Model Unity's UUM-95901 `CleanBuildCache` behavior with separate per-assembly
  started, finished, and not-required evidence. Durable compilation now requires
  every expected assembly to start and reach either terminal callback, avoiding
  the false zero-assembly failure while preserving positive rebuild proof.
- Publish precise callback sets and reuse the completed compiler-diagnostic
  product so clean-build warnings are no longer lost when Unity suppresses
  `assemblyCompilationFinished`.

## [0.3.60] - 2026-08-29

### Fixed

- Make durable clean-compilation jobs snapshot the expected Editor script
  assemblies, persist every per-assembly completion callback, and reject a
  zero-assembly or incomplete rebuild instead of accepting only the global
  compilation lifecycle and Domain Reload as success evidence.
- Publish expected, completed, and missing assembly counts and identities in
  workspace and code-affecting asset-transaction compilation evidence.

## [0.3.59] - 2026-08-29

### Fixed

- Let package-test manifest restoration finish after the exact original bytes,
  one resolve request, a clean compilation, an assembly reload, and a stable
  Editor are all observed, even when Unity 6.4 keeps the removed package test
  assembly discoverable until the current Editor session ends.

## [0.3.58] - 2026-08-28

### Fixed

- Make aggregate UXML layout self-tests assert their owned finding kind so new
  independent layout rules cannot invalidate unrelated expectations, and run
  the aggregate self-test suite in package smoke/regression tests.

## [0.3.57] - 2026-08-28

### Fixed

- Include the inline UXML flex-shrink regression fixture in both the default
  package-smoke and full-regression selections so the package-test coverage
  guard cannot silently omit the new auditor rule.

## [0.3.56] - 2026-08-28

### Fixed

- Require the resolved Git package cache's Unity-owned `_fingerprint` to match
  the requested full SHA before package resolve/update adoption can advance to
  compilation. A rewritten manifest and lockfile can no longer make an old
  cache directory look adopted merely because `PackageInfo.packageId` echoes
  the new URL.

## [0.3.55] - 2026-08-28

### Added

- Report inline `flex-shrink: 0` when exact authored pixel geometry proves that
  a direct Flex line, or its same-axis natural-size chain within a fixed owner,
  has non-negative free space. Preserve genuinely pressured, intrinsically
  unknown, runtime-owned, and reasoned-suppression layouts.

## [0.3.54] - 2026-08-25

### Fixed

- Update the aggregate USS self-test's exact finding counts for the intentional
  invariant-declaration error now emitted alongside the existing relational
  anchor warning.

## [0.3.53] - 2026-08-25

### Fixed

- Generalize the single-consumer class-anchor ownership error to every invariant
  base declaration. A class retained for a modifier, pseudo-state, or relational
  selector may keep only properties whose values actually change on that same
  target; unchanged visual, text, layout, and visibility declarations belong on
  the sole authored consumer inline.

## [0.3.52] - 2026-08-25

### Added

- Report instance-owned `display` and `margin` declarations as unsuppressible
  USS ownership errors when a class has one authored consumer, no runtime class
  reference, and only an unrelated selector contract keeping the class external.

## [0.3.51] - 2026-08-23

### Fixed

- Let `profiler/frame-data` and `profiler/analyze` read retained Profiler frames
  after recording is disabled, so freezing a capture no longer forces callers
  to restart recording and overwrite the exact frames they need to inspect.

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
