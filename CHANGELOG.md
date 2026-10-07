# Changelog

## [0.6.167] - 2026-10-08

- Observe the exact authored X position curve in loaded-asset publication tests.
  Unity may generate all three position bindings; their count is not a file
  publication condition. All byte/GUID/local-file-ID assertions remain unchanged.

## [0.6.166] - 2026-10-08

- Release Unity cached file handles at raw asset publication and snapshot
  restoration boundaries. Loaded serialized assets on Windows can be overwritten
  and rolled back without mapped-file sharing failures; preserve GUID/meta identity.
- Cover loaded native AnimationClip immediate/deferred overwrite and rollback,
  alongside the real multi-atlas TextCore font import witness.

## [0.6.165] - 2026-10-08

- Read up to sixteen exact 64-bit method addresses through the selected native
  Profiler frame. Preserve native method/source information and missing names;
  reject numeric, malformed, duplicate and oversized address requests.
- Publish the same closed method-result schema through the existing catalog.
  This adds diagnostic observation without changing capture or simulation.

## [0.6.164] - 2026-10-07

- Select exact repeated Prefab components by zero-based componentIndex when
  reading serialized properties. Report index and matching component count;
  unavailable indices fail without reading another component.
- Publish component ordinals in Prefab find results and verify distinct native
  component values, hidden-field reads and unchanged authoring state.

## [0.6.163] - 2026-10-07

- Pass the native backing pixel rectangle to Editor view GrabPixels so scaled
  windows capture their full surface and match the published content geometry.
- Preserve the 150% DPI red/green and blue-control regression witness alongside
  the 100% UI Builder integration.

## [0.6.162] - 2026-10-07

### Changed

- Attribute the native colored-control regression with its sampled pixel,
  root/control geometry, backing scale, source UV origin and observed blue
  pixel bounds. The ordinary window counterexample remains a failed test until
  its source-coordinate contract is established.

## [0.6.161] - 2026-10-07

### Fixed

- Normalize native Editor view readback rows using the graphics backend's UV
  origin and map the retained content through its actual root bounds, including
  native tab margins. The first direct readback exposed inverted PNGs and an
  incorrect full-host content rectangle; the colored native control regression
  now consumes the published rectangle.
- Use the existing cross-version object identity owner for view receipts.

## [0.6.160] - 2026-10-07

### Added

- Select `view` to read the requested native Editor view's render surface with
  Unity's GUIView owner. This explicit surface captures retained UI without OS
  chrome; it does not depend on a desktop foreground transition. Each request
  performs one repaint and one readback with exact native view identity,
  restores the selected tab and render target, and releases image resources.
- Expose the same surface through UI Builder preview. Automatic surface
  selection and exact desktop foreground verification remain unchanged.

## [0.6.159] - 2026-10-07

### Fixed

- Use the existing UI argument reader when selecting Builder's default capture
  surface, correcting the unsupported overload in the first integration revision.

## [0.6.158] - 2026-10-07

### Added

- Select the existing screenshot owner's `captureMode` directly in UI Builder
  preview, including explicit native PrintWindow capture with the same target
  verification and pixel analysis. Screen capture remains the default.
- Report effective ancestor opacity in Editor and runtime UI Toolkit style
  snapshots, so opaque local styles no longer hide dimming by a parent.

## [0.6.157] - 2026-10-07

### Fixed

- Admit native string Player Settings in Build Profile transactions. Unity
  reports strings as arrays; they remain scalar values for template selection
  and the existing inactive-profile persistence regression.

## [0.6.156] - 2026-10-07

### Added

- Native Build Profile Player Settings scalar authoring through
  `set-player-property`, including native profile serialization, Undo ownership,
  input limits and override instance identity for actual readback. This preserves
  inactive overrides and avoids writing ineffective global settings or cached
  YAML strings when a profile owns the build settings.

## [0.6.155] - 2026-10-07

### Fixed

- Validate every built-in and typed project-tool input against its exact catalog
  schema before request registration, defaults, Undo or durable job admission.
  Unknown properties and JSON type coercion now fail with `invalid_arguments`
  and a JSON path instead of silently succeeding.
- Publish the optional project binding accepted by read-only commands, retain
  explicit boolean confirmation, and include null in nullable enum contracts.
- Publish bounded schema-evaluation capacity and the distinct
  `input_validation_limit` admission error.
- Retain round-trip floating-point precision and normalize decimal scale in
  canonical JSON used for request identity and input uniqueness.

## [0.6.154] - 2026-10-07

### Fixed

- Isolate deliberate compute compiler errors by kernel so each native program
  exercises its own diagnostic without the other program's invalid function.

## [0.6.153] - 2026-10-07

### Fixed

- Explicitly declare native compute program acquisition as a runtime-state
  operation so its typed support contract passes project-tool registration.

## [0.6.152] - 2026-10-07

### Fixed

- Separate the unchanged package-test contract fixture from the Game View
  regression fixture so each touched C# file owns one top-level type.

## [0.6.151] - 2026-10-07

### Added

- Typed native compute-kernel support inspection requests one device-specific
  program and reports its exact support state and supported thread-group sizes,
  separately from cached read-only compiler diagnostics.

## [0.6.150] - 2026-10-07

### Fixed

- Register the broken compute fixture's complete native compiler log sequence
  before requesting its programs. The repeated first-kernel error precedes the
  second-kernel error; all native count and truncation checks remain unchanged.

## [0.6.149] - 2026-10-07

### Fixed

- Resolve only existing Game View instances for information, resolution and
  scale commands, rejecting missing or ambiguous views without opening or focusing.
- Keep zoom initialization out of the read-only information query and publish
  exact error codes with focused native window and focus preservation regressions.

## [0.6.148] - 2026-10-07

### Fixed

- Request both deliberately broken compute fixture programs and expect the
  proven repeated first-kernel compiler error at the second native request.
  Retain the full native diagnostic count and explicit truncation assertions.

## [0.6.147] - 2026-10-07

### Fixed

- Publish the native pointer attachment, bounds and document ownership errors,
  plus the Builder framing error, in their exact public catalog contracts.

## [0.6.146] - 2026-10-07

### Fixed

- Regenerate the native pointer output contract, Builder framing result and
  audited core route fingerprint together so the catalog accepts the new route.

## [0.6.145] - 2026-10-07

### Fixed

- Register live package-request jobs with their workspace owner so public job
  Get adopts queued requests and Cancel respects their native issuance boundary.
  Explicit job types and jobId-only access use the same capability enforcement.

## [0.6.144] - 2026-10-07

### Fixed

- Request the deliberate broken compute fixture's native program once. A second
  kernel support request re-emits errors for the first kernel. Retain both exact
  expected compiler errors and the native count, error and truncation checks.

## [0.6.143] - 2026-10-07

### Added

- Publish native pointer phases through attached runtime UIDocuments, including
  paused Play Mode. Unity owns hit testing, capture and control behavior; product
  effects remain separately observable from Input System player consumption.
- Fit UI Builder previews with its native Fit viewport control after document
  layout settles, so full page content is visible without a private zoom API.

## [0.6.142] - 2026-10-07

### Changed

- Check the deliberate compute fixture's log scope after import and each native
  kernel request so unexpected diagnostics identify their first producer call.
  Keep the exact expected errors and native count/truncation assertions.

## [0.6.141] - 2026-10-07

### Fixed

- Register both deliberate compute error expectations before fixture import.
  Cached native errors may be emitted during import, before an explicit kernel
  support request. Preserve the same diagnostic and truncation assertions.

## [0.6.140] - 2026-10-07

### Fixed

- Declare the two deliberate native compiler errors expected by the broken
  compute fixture. Match its exact asset, identifier and kernel, retain native
  diagnostic and truncation assertions, and keep other errors unexpected.

## [0.6.139] - 2026-10-07

### Fixed

- Exercise native compute kernel loading before asserting cached compiler
  diagnostics in the focused fixtures. Import alone does not prove that the
  device-specific kernel has been requested. Keep the public diagnostics tool
  read-only and retain error-count and truncation checks.

## [0.6.138] - 2026-10-06

### Added

- Read imported ComputeShader compiler messages through the existing typed
  shader diagnostics entry. Report native error severities, full message count
  and bounded truncation with an explicit asset type; leave support unknown for
  ComputeShader because it has no native asset-wide support flag.
- Cover valid compute readback and a broken two-kernel fixture retaining error
  evidence under a one-message output limit. The diagnostic entry does not
  compile, dispatch, clear messages or change project state.

## [0.6.137] - 2026-10-06

### Fixed

- Compile Build Profile commands on Unity 2022 by selecting the Editor GUID
  type when the Unity 6 runtime GUID type is unavailable. Editors without
  native Build Profiles keep returning `capability_unavailable` before any
  profile operation, covered by a focused regression.
- Compile sprite slicing and VFX creation rollback against Unity 2022 APIs,
  preserving sprite IDs and using the native asset GUID lookup for rollback.
- Declare Test Framework 1.4.6 as the minimum dependency for native test-run
  cancellation; it supports Unity 2019.4 and newer.

## [0.6.136] - 2026-10-06

### Added

- Identify the foreground window sampled before and after desktop-composition
  capture in the structured capture geometry. Rejected UI Builder captures keep
  this evidence so focus failures can be attributed without another capture or
  an external probe. Exact target verification remains required.
- Publish closed optional foreground observations and cover their schema and
  preservation through UI Builder rejection.

## [0.6.135] - 2026-10-06

### Fixed

- Match the memory-breakdown output contract to the native command's category
  object and Boolean package-presence flag. Publish closed category and optional
  asset-detail schemas from the generator, with a focused catalog regression.
  This changes schema publication only; no loaded-asset scan or profiling state
  is added to calibration.

## [0.6.134] - 2026-10-06

### Fixed

- Verify the Player launch mutation and project-binding policy using the
  canonical execution route returned by the catalog. The previous assertion
  passed the discovery name to an execution-route API; production launch was
  unaffected.

## [0.6.133] - 2026-10-06

### Added

- Launch an existing Windows Unity Player through the typed `player/launch`
  contract, with an explicit argument vector, owned log destination and actual
  OS process identity. The caller owns normal application shutdown.
- Pass bounded Player startup arguments through the existing build job and
  validate them before admitting build side effects. Log selection and log
  readback consume the same explicit `playerLogPath`.
- Cover Windows native argument parsing, invalid switch/count/length domains
  and catalog mutation/binding publication.

## [0.6.132] - 2026-10-06

### Added

- Allow Prefab component property inspection to opt into hidden native fields,
  including enabled state, and return exact serialized property paths. Preserve
  visible-only defaults and unload native authoring contents without saving.
- Cover default and hidden discovery, disabled-state readback, typed catalog
  publication, asset-byte preservation and loaded-scene preservation.

All notable changes to this package are documented here.

## [0.6.131] - 2026-10-05

### Fixed

- Typed JSON contracts admit and transport valid flags combinations using declared
  JSON member names. Binding, schemas and nested result serialization share the
  same contract; undefined bits and undeclared values remain errors.
- Align the remaining recently added package asset identities with the existing
  deterministic GUID owner and preserve their internal references.

### Added

- Focused enum contract regressions for flags, JSON names, nullable nested
  products, signed high bits and ordinary enum rejection.

## [0.6.130] - 2026-10-05

### Fixed

- Compiler pagination executor regression reads the public transport array
  through its collection contract rather than casting it to the owner's private
  generic list shape. Native pagination behavior is unchanged.

## [0.6.129] - 2026-10-05

### Fixed

- Compiler diagnostics expose every retained message through bounded ordinary and
  obsolete-warning pages. Continuation pages require the producer's snapshot
  revision, which survives reload and rejects mixed compilation products.
- Retention overflow reports incomplete diagnostics instead of publishing partial
  counts as complete. The catalog declares both incomplete and changed-snapshot
  errors; unsupported fields and invalid pagination arguments are rejected.
- Preserve native frame-interval field descriptions when regenerating contracts.

## [0.6.128] - 2026-10-05

### Fixed

- Native hierarchy regression verifies physical content ownership separately from
  Unity's logical `parent`, which can report the ScrollView itself.

## [0.6.127] - 2026-10-05

### Fixed

- UI Toolkit tree inspection, generated child queries, resources and element
  paths observe the native hierarchy, including ScrollView scrollers and slider
  subparts, rather than the control's redirected authored content container.
- Report native child counts consistently with returned numeric paths.

## [0.6.126] - 2026-10-05

### Changed

- Withdraw the unverified running-frame request action. Native Editor player-loop
  requests did not advance the witness while unpaused; paused steps are explicitly
  excluded as proof of running Input System touch consumption.
- Separate input observation result types into individual source files.

## [0.6.125] - 2026-10-05

### Added

- Native running-frame intervals preserve Input System player eligibility while
  consuming Simulator touches, then pause and report the actual frame interval.
- Input observations expose native play eligibility and project-wide asset identity.

### Fixed

- Attribute the optional Input System tool assembly to the Automation package.

## [0.6.124] - 2026-10-05

### Added

- Read-only Input System runtime state observations distinguish game focus,
  device admission, action state and the currently selected input buffer.

### Fixed

- Name Simulator application focus accurately; application focus alone does not
  establish Input System game focus or player consumption.

## [0.6.123] - 2026-10-05

### Added

- Simulator pointer results expose native touch consumption and player focus,
  allowing dispatch, screen admission and game response to be distinguished.

## [0.6.122] - 2026-10-05

### Added

- Bounded native Play Mode frame intervals with actual frame-count completion,
  cancellation/timeout cleanup and complete step response metadata.

## [0.6.121] - 2026-10-05

### Fixed

- Republish optional-capability routes, metadata and catalog revision when native
  package readiness changes after domain initialization. Cold Addressables
  detection no longer removes its commands for the rest of the Editor domain.
- Add absent/present/removed capability publication regression coverage.

## [0.6.120] - 2026-10-05

### Fixed

- Declare imported Shader diagnostics as read-only so the native catalog admits
  its typed contract. Add a catalog-registration regression test.

## [0.6.119] - 2026-10-05

### Added

- Add typed native Shader compiler diagnostics for hand-written shaders and
  compiled Shader Graph assets. Report source file, line, platform, severity and
  ShaderUtil error state independently of Shader.isSupported and Console entries.
- Add focused native asset-boundary and compiler-state regression tests.

## [0.6.118] - 2026-10-04

### Fixed

- Recognize explicitly qualified Unity Entities SystemBase declarations that require
  source-generated partial types in code policy review, while retaining the ordinary
  partial declaration prohibition and one-owned-type-per-file check.
- Add focused positive and negative namespace qualification regression cases.

## [0.6.117] - 2026-10-04

### Fixed

- Admit explicit null, false, zero and empty-string values in asset transaction
  serialized-set operations while still rejecting an omitted value and null or
  blank target paths. Explicit null can clear a native ObjectReference.
- Add focused required-field admission regression tests for these JSON values.

## [0.6.116] - 2026-10-04

### Fixed

- Preserve the native screenshot error, code and verification receipt in UI Builder
  preview failures. Unobserved or inconclusive document blankness is null.
- Declare Builder view/file mutations and its required UXML path, publish precise
  preview failures, and advertise native foreground-verification errors.
- Clarify the jobs/get discovery contract so CLI callers select the facade's
  existing background polling recipe during import, compilation and reload.

## [0.6.115] - 2026-10-04

### Added

- Add typed native Editor window closure in stable Edit Mode, rejecting unsaved
  windows and proving destruction without accepting Save/Discard dialogs.

## [0.6.114] - 2026-10-04

### Fixed

- Save native PlayerSettings after setting identity, version or background execution
  fields so a successful command persists beyond the current Editor process.

## [0.6.113] - 2026-10-03

### Fixed

- Assign the hit-tested DeviceView as each synthesized mouse event's explicit
  target; VisualElement.SendEvent alone forwards to the panel dispatcher.

## [0.6.112] - 2026-10-03

### Fixed

- Declare the Simulator pointer tool's runtime mutation and Play Mode requirement
  so native project-tool registration accepts its public contract.

## [0.6.111] - 2026-10-03

### Added

- Add typed native Device Simulator pointer phases with window identity and hit
  validation, using Unity's own touch transform and input publication chain.

## [0.6.110] - 2026-10-03

### Added

- Add typed native PlayerSettings display inspection and configuration for default
  orientation, screen dimensions and autorotation flags through the public catalog.
- Validate all requested display fields before native writes and report the saved
  settings; cover portrait configuration, omitted flags and rejection without mutation.

## [0.6.109] - 2026-10-03

### Changed

- Move scalar Int32/Int64 serialization into its own value owner, preserving
  full-precision Int64 strings and native persistence while keeping component
  commands within the default code policy size limit.
- Cover native save/unload/import/readback for integer boundaries and JSON
  precision, and ensure overflow fails before assigning a property.

## [0.6.108] - 2026-10-03

### Fixed

- Read and persist Vector2Int and Vector3Int through the shared serialized-property
  owner, including PanelSettings reference resolutions.
- Reject missing, extra, fractional or out-of-range integer coordinates before
  applying any change; report exact coordinate objects in native readback.

## [0.6.107] - 2026-10-03

### Added

- Register an existing LocalizationSettings asset through localization/settings
  using settingsAssetPath, allowing fresh projects to initialize localization
  entirely through the official Unity CLI.
- Validate the settings asset and requested Locales before changing project
  registration; updates use the same asset for Editor and runtime settings.

## [0.6.106] - 2026-10-03

### Added

- Select project ScriptableSingleton settings through serialized-object get/set,
  with live SerializedObject readback, native Save(true) persistence, and an
  explicit settingsPath in successful responses.
- Reject conflicting selectors, unsupported singleton types, missing file paths,
  and global preferences outside the bound project before creating the instance.
- Verify typed and instance-selected settings writes across native singleton
  destruction and reload, including persistence-boundary errors.

## [0.6.105] - 2026-10-03

### Fixed

- Migrate the material integer regression to the asynchronous preview consumer,
  preserving all four integer-storage and asset-reload cases.
- Report authoritative package-test compiler errors before waiting for an
  unavailable assembly product, including when a previous assembly output exists.

## [0.6.104] - 2026-10-03

### Fixed

- Await native asset and material previews through Editor updates instead of
  blocking the main thread and returning generic icons. Preview timeout,
  interruption and encoding failures are explicit; callbacks and graphics
  resources are retired on every completion path.
- Honor asset-preview dimensions, preserve aspect ratio with transparent padding,
  bound dimensions in the input schema and restore the active render target.

## [0.6.103] - 2026-10-03

### Fixed

- Resolve Git packages installed with a repository `?path=` when Unity records
  a content fingerprint distinct from the selected commit. Dependency review
  consumes the same resolution owner; root-package fingerprints still require
  the selected commit and stale registered revisions remain rejected.

## [0.6.102] - 2026-10-03

### Removed

- Withdraw the Git update metadata output extension after self-update exposed
  a publication failure across its producer revision boundary. Keep the
  established native completion observation, registration clock, target proof
  and compilation receipt; no missing-product adapter is retained.

## [0.6.101] - 2026-10-03

### Fixed

- Start Git update's unchanged 300-second registration window at original native
  completion. Preserve the original request across reload and remove automatic
  cancellation retry and file-based inferred native completion.
- Share the native addition completion producer with package add. Compare
  add/remove registration only after refresh and compile; publish the native
  Git update product and expose its uncertainty and registration failure codes.

## [0.6.100] - 2026-10-03

### Fixed

- Move the three package-request input producers into the existing specialized
  schema catalog so the general catalog satisfies its unchanged type-size
  policy. Input shapes and durable package-request behavior are unchanged.

## [0.6.99] - 2026-10-03

### Fixed

- Publish durable jobs before issuing registry search, package add and package
  remove requests. Poll native completion independently of the original CLI
  response; persist mutation completion before refresh, clean compilation and
  reload. Retain the original native request through assembly reload, and expose
  an uncertain outcome after an Editor process restart without repeating it.
- Project only the requested registry result page, expose exact integer paging
  bounds, and remove the three request-owned Editor update callbacks.

## [0.6.98] - 2026-10-03

### Removed

- Withdraw the package-test activation compilation change from 0.6.97. The same
  native consumer resource timeout reproduced without the concurrent clean
  request, so it did not establish the waiting-time root cause. Restore the
  preceding activation and restoration lifecycle; deadlines are unchanged.

## [0.6.96] - 2026-10-02

### Fixed

- Sample Player window captures after the requested run interval and before
  termination. Publish the sample timestamp and elapsed launch time, propagate
  capture failure to the build job, and dispose the owned process handle.

## [0.6.95] - 2026-10-02

### Fixed

- Qualify compilation mode readback without importing Unity's `Assembly` type
  into the reflection command owner. This resolves the five ambiguous type
  references found by the native consumer compilation of 0.6.94.

## [0.6.94] - 2026-10-02

### Added

- Expose Unity's native Editor C# compilation mode in `editor/state` and durable
  workspace compilation results. `asset/refresh` can explicitly select `Debug`
  or `Release` before its clean rebuild, persist that intent across reload, and
  reject a mode changed before terminal verification.

## [0.6.93] - 2026-10-02

### Fixed

- Keep Build Profile selection regression tests on Unity's current platform.
  Resolve its native platform GUID before creating or activating the fixture,
  so installed platform ordering cannot trigger a cross-platform reimport.

## [0.6.92] - 2026-10-02

### Fixed

- Accept an explicit null asset path in Build Profile set-active transactions to
  select Unity's platform profile. Restore null original profiles on transaction
  rollback and report rollback failures instead of hiding them.
- Publish the platform selection in the input schema and cover selection,
  dry-run state preservation, and native profile readback.
- Publish nullable selection results and transaction failure codes in the
  generated contract. Normalize the two material integer regression asset GUIDs
  to the package's deterministic owner.

## [0.6.91] - 2026-10-02

### Fixed

- Publish complete closed texture, model, audio and common importer settings in
  readback and mutation result schemas, including platform overrides and
  version-dependent audio preload fields. Regression coverage compares real
  importer products with the generated catalog alternatives.

## [0.6.90] - 2026-10-01

### Fixed

- Pass the material integer regression fixture's property selection in the same
  deserialized JSON array form accepted by the public command, and assert command
  success before inspecting its result.

## [0.6.89] - 2026-10-01

### Fixed

- Read and write declared integer shader properties with Unity's native integer
  Material APIs in graphics material inspection and material property commands.
  Integer values preserve their exact signed range through save and reload, and
  inspection no longer emits float-property errors or unreadable placeholders.

## [0.6.88] - 2026-10-01

### Fixed

- Replace ambiguous name-only retained-frame counters with native category/name
  queries. Publish Unity's verbatim formatted value instead of implying that a
  same-name marker belongs to the intended physics category.
- Select and identify the native 2D or 3D collision matrix in the existing read
  and layer mutation commands. A 3D matrix no longer stands in for 2D diagnostics.
  Correct the matrix schema to the native map of layer names to name lists.

## [0.6.87] - 2026-10-01

### Fixed

- Export exact native integer counters alongside the existing retained-frame CPU
  hierarchy. Missing samples stay explicit, and decimal strings preserve int64
  values. Add the native Physics2D category switch to capture and restoration.

## [0.6.86] - 2026-10-01

### Fixed

- Include native 2D shape count, collider identity, hierarchy, bounds, layer,
  trigger and attached-body observations in circle/box overlap results. Closed
  schemas distinguish the 2D product from existing 3D descriptors. This enables
  physics-cost attribution without inferring shape or body state from names.
- Regenerate the existing Prefab add-component output alternatives from their
  synchronous and durable-job producers, including the persistence/reload fields.

## [0.6.85] - 2026-10-01

### Fixed

- Declare the project-tool owner's unexpected exception code in every generated
  project-tool contract. Immediate and cooperative exception regressions verify
  that the catalog advertises the same code the invocation returns.

## [0.6.84] - 2026-10-01

### Fixed

- Normalize the executor's scalar and JSON project bindings before comparison and
  request fingerprinting. Equivalent absolute path spellings preserve one
  invocation identity; relative or conflicting roots fail before owner execution.
- Admit Prefab component and referenced types before editing. Missing types fail
  without scheduling an asset refresh or a type-wait loop. Remove implicit import
  and type-wait arguments; use explicit durable refresh/compile jobs instead.
- Preserve exception type and original stack in unexpected project-tool failures,
  including cooperative job steps. Expected typed rejections do not create Console
  exceptions. Add focused binding, admission, persistence and error regressions.
- Align the native frame-history source metadata with the package's deterministic
  GUID owner; the package-wide metadata check no longer rejects it.
- State the existing manifest prerequisite in the package resolve contract and
  publish its `package_manifest_target_mismatch` rejection code.

## [0.6.83] - 2026-10-01

### Fixed

- Describe whole-Variant reversion using Unity's default override contract.
  Root alignment defaults remain intact; ordinary root scale is reverted.
- Focused persistence tests distinguish Unity-classified default rotation from
  ordinary scale, and verify preservation or restoration after reopening.

## [0.6.82] - 2026-10-01

### Fixed

- Filtered Prefab Variant reverts restore removed components and, on Unity
  2022.1+, removed GameObjects. Component filters preserve whole-object overrides.
- Revert the Variant's own authoring contents relative to its base and publish
  through the shared Prefab mutation session, preserving unrelated overrides
  and loaded scenes.

## [0.6.81] - 2026-10-01

### Added

- Reload the sole loaded saved scene through the scene workspace command, with
  an explicit save/discard decision for dirty state. This avoids treating an
  idempotent open or a rejected last-scene close as a completed reload.

## [0.6.80] - 2026-10-01

### Fixed

- Correct the frame-history source metadata's invalid 33-character GUID and
  declare its MonoImporter settings. No profiling or gameplay behavior changes.

## [0.6.79] - 2026-10-01

### Fixed

- Keep native frame-history binding in its own source file with package-owned
  metadata, satisfying the one-top-level-type source contract.

## [0.6.78] - 2026-10-01

### Added

- Bound retained Profiler history through the existing `profiler/enable` contract's
  optional native frame capacity. Publish previous capacity for restoration,
  declare the Editor preference effect, and reject invalid capacity before mutation.
- Document short captures, evidence export, explicit retirement and restoration.
  Frame capacity is a frame count, not a memory byte limit.

## [0.6.77] - 2026-10-01

### Fixed

- Capture rendering counters through one producer shared by statistics and
  analysis. Remove the duplicate reflection reader and its silent catch that
  omitted the entire rendering result on Unity 6000.4 and newer.
- Select the native counter API at compile time: batches before Unity 6000.4,
  total indirect draw calls from 6000.4 onward. The contract declares their
  availability and no longer advertises the unsupported indirectDrawCalls field.
- Publish all common counters as required statistics fields and propagate
  unexpected read failures through the normal executor error boundary.

## [0.6.76] - 2026-10-01

### Fixed

- Convert native UnityStats seconds to milliseconds in the performance summary
  and calculate estimated FPS as the reciprocal of seconds. The previous
  summary underreported milliseconds and inflated estimated FPS by 1000.
- Retain the native timing as `frameTimeSeconds` and declare timing units in
  the rendering statistics and analysis contracts. Retained Profiler frame
  data keeps its existing explicit millisecond units.

## [0.6.75] - 2026-09-30

### Fixed

- Publish the installed-package list directly from native registration, using
  the same metadata authority as info, resolved status and lint roots. Remove
  its unnecessary UPM request/update subscription and deferred publication.
- Verify the original adjacent list timeout independently from the metadata
  reads; retain actual UPM request lifecycles for search and package mutations.

## [0.6.74] - 2026-09-30

### Fixed

- Read installed package details, resolved status and lint roots from the native
  registration product. Remove main-thread sleep loops waiting for Package
  Manager requests, which could block the Editor indefinitely.
- Adopt one registration snapshot for a complete resolved-status request and
  remove unused synchronous list/add/remove/search implementations. Existing
  asynchronous UPM commands retain their request and completion lifecycle.

## [0.6.73] - 2026-09-30

### Fixed

- Restrict automation state recovery, scheduling and execution to the main Editor
  process. Asset import workers no longer publish or prune shared job history.
- Add explicit `jobs/repair-history` reconstruction from canonical workspace
  execution records. Preserve valid bytes, original capabilities and indexed
  membership; reject incomplete owners before writes. Persist an idempotent
  receipt before an optional native domain reload, without replaying jobs.
- Move workspace snapshot publication out of scheduler initialization so both
  normal publication and explicit reconstruction consume the same product.

## [0.6.72] - 2026-09-29

### Changed

- Give the Unity value formatter its own source file so the response publication
  owner passes the same one-type source policy as its consumers. Preserve the
  existing response script identity and the formatter's behavior.

## [0.6.71] - 2026-09-29

### Fixed

- Preserve root execution-success fields declared by the owner output schema
  during response publication. Restore the code policy review's success field
  and require it in the generated contract.
- Exercise both clean and policy-finding reports through the production executor,
  verifying execution success independently from policy acceptance.

## [0.6.70] - 2026-09-29

### Fixed

- Share the existing dot-prefixed and tilde name classification between package
  metadata lint and dependency policy review. Ignore hidden source files, their
  metadata and hidden descendants without excluding visible ownership failures.
- Extend the four explicit Library/Temp root regressions with hidden folders,
  nested hidden content and malformed ignored metadata.
- Normalize the asset refresh regression script metadata through the package's
  deterministic GUID owner.

## [0.6.69] - 2026-09-29

### Fixed

- Let the loaded-scene metadata regression own a single temporary scene while
  Unity Test Runner supplies an untitled bootstrap scene. Unload the test scene
  before deleting its assets; Test Runner retains the original scene restoration.

## [0.6.68] - 2026-09-29

### Fixed

- Resolve targeted refresh metadata paths to their owning asset before scene
  protection, deduplication, dependency ordering and import. Preserve folder GUIDs
  instead of importing metadata as a separate asset, and report canonical paths.
- Apply compilation import options and loaded-scene protection through metadata
  paths. Add focused identity, deduplication and scene-protection regressions.

## [0.6.67] - 2026-09-29

### Added

- Separate request cloning, input validation, project steps, history publication
  and atomic job-file persistence in ordinary Editor Profiler captures. The
  markers require no deep profiling and preserve the existing job lifecycle.

## [0.6.66] - 2026-09-29

### Fixed

- Align `scriptableobject/set-field` input with the serialized JSON values its
  handler accepts, including strings, booleans and structured values. Require
  the asset path and field so incomplete targets fail during contract admission.
- Keep the contract generator and focused input-schema regression in agreement.

## [0.6.65] - 2026-09-29

### Fixed

- Return font atlas and material local IDs as invariant decimal strings, retaining
  all 64 identity bits through JSON numeric serialization.

## [0.6.64] - 2026-09-29

### Fixed

- Require the font asset path in the typed rebuild schema, resolve file IO from
  the bound Unity project, and bound snapshot metadata and source point size.
- Verify internal names against the immutable requested asset name after import.

## [0.6.63] - 2026-09-29

### Added

- Add typed `textcore/font-asset/rebuild` for an existing dynamic TextCore font.
  Reload its imported source face, clear stale glyphs and atlas pixels, synchronize
  embedded names, and verify persisted font, source, atlas and material identities.
  The bounded single-atlas transaction retains authoring settings and fallback
  references, with verified asset and meta byte rollback on publication failure.

## [0.6.62] - 2026-09-29

### Fixed

- Classify meta-review exclusions below each explicit root. A resolved package
  inside the project's Library directory is now actually scanned, instead of
  returning a successful zero-record review because of its ancestor name.
- Add positive and missing-meta regressions for explicit Library and Temp roots,
  retaining excluded child directories in both cases.
- Align the new UI audit source metas and test admission fixture meta with the
  package's deterministic GUID owner.

## [0.6.61] - 2026-09-29

### Fixed

- Avoid reading and parsing UI audit configuration on every idle Editor update.
  The existing configuration observation still manages watcher enablement, and
  queued imports, filesystem changes and theme changes retain their audit path.

## [0.6.60] - 2026-09-29

### Fixed

- Preserve the test admission capability contract in the schema generator's
  exact output override so regeneration cannot discard the declared field.
- Assert required-field membership against the schema vocabulary's native
  collection instead of assuming its concrete generic list type.

## [0.6.59] - 2026-09-29

### Fixed

- Declare the private job access token returned by `testing/run-tests` in the
  closed started-job output schema, so separate CLI sessions can discover the
  exact polling capability contract.
- Cover admission, clear-stuck and active-job-conflict schema variants in a
  focused contract regression and document private capability handling.

## [0.6.58] - 2026-09-28

### Added

- Add `textcore/font-asset/create` to create a dynamic TextCore font asset from an
  imported TTF or OTF. It persists the atlas and material as sub-assets, reads
  back the source and dynamic mode, and removes the new asset if creation fails.

### Fixed

- Restore deterministic package GUIDs for three existing UXML audit sources so
  the package metadata check passes for this release.

## [0.6.57] - 2026-09-27

### Fixed

- Keep job-record loading read-only across compilation and Domain Reload. An
  older index reader no longer deletes records published by a newer job-history
  epoch, which could leave `jobs/get` with indexed but missing files.

## [0.6.56] - 2026-09-27

### Fixed

- Allow `selection/set` to select project assets by `Assets/...` path, and
  include assets in `selection/get` readback. Scene object selection
  remains available for hierarchy paths.

## [0.6.55] - 2026-09-27

### Fixed

- Do not label an inline `opacity: 1` reset as redundant engine styling when
  its UXML element or an ancestor is authored disabled. Disabled theme styling
  can otherwise fade read-only content; the layout audit now retains this
  reset and covers both disabled and enabled cases in its self-tests.

## [0.6.54] - 2026-09-27

### Fixed

- Remove the retired ScrollView shrink suppression marker from the UXML audit
  report so the corrected audit compiles in consumers.

## [0.6.53] - 2026-09-27

### Fixed

- Remove the static `ineffective-scroll-axis-flex-shrink` warning. A direct
  child of `ScrollView` can shrink when the authored content container is
  constrained; the warning incorrectly advised removing a necessary layout
  declaration. UI Builder preview geometry now provides the layout evidence.

## [0.6.52] - 2026-09-27

### Fixed

- Measure UI Builder canvas fit using the visible `ScrollView` viewport instead
  of offscreen scroll content, and fail the preview when text from one
  scrollable preview entry overlaps the next. Report the affected element paths
  in the public preview contract so a nonempty sample cannot pass as a usable
  layout.

## [0.6.51] - 2026-09-25

### Fixed

- Treat an empty template instance or a generic `Label` placeholder as a
  missing generated UI Builder preview. Follow referenced entry templates and
  accept authored text or an imported image instead of counting nodes alone.
- Check generated preview image GUIDs and file IDs against imported Sprite or
  Texture2D assets. Report unresolved images even when another preview element
  exists in the same generated host.

## [0.6.50] - 2026-09-25

### Added

- Discover UI Builder preview hosts from UI Prefab `UIDocument` references and
  serialized UXML entry, slot, and RenderTexture producers. Visible generated
  hosts with no authored preview now fail the UXML audit without per-page
  settings or preview markers. UI Prefab imports trigger a full preview review.

## [0.6.49] - 2026-09-25

### Added

- Audit runtime-generated text previews in UI Builder using
  `required-text-entries` markers and independent `minTextEntries` project
  requirements. Empty containers and blank template instances now fail even
  when image previews elsewhere on the page are complete.

## [0.6.48] - 2026-09-25

### Fixed

- On USS and TSS changes, run only the default theme stylesheet graph check
  across unchanged UXML files. Keep the full layout audit scoped to UXML files
  that changed, so unrelated authoring findings do not flood the Console.

## [0.6.47] - 2026-09-25

### Fixed

- Report `duplicate-theme-stylesheet` when a UXML loads a USS directly and
  the project default theme imports the same USS, including nested imports.
  Watch `.tss` and `.uss` changes so the automatic UXML audit rechecks affected
  pages.
- Log UXML audit error findings as Console errors and include them in the
  automatic audit status count.

## [0.6.46] - 2026-09-25

### Added

- Allow projects to require UI Builder previews by UXML path and element name in
  audit settings, so deleting both an inline marker and its image still fails.

## [0.6.45] - 2026-09-25

### Added

- Audit required UI Builder image previews in the opened host UXML. A
  `required-images` marker now fails when its runtime-replaced sample has fewer
  authored preview images than declared.

## [0.6.44] - 2026-09-25

### Fixed

- Compile Unity 6.6 VFX Graphs without requiring an open VFX authoring view.
  Preserve the compiler output in validation results and avoid the unregistered
  authoring GUID failure.

## [0.6.43] - 2026-09-25

### Fixed

- Add the Unity 6.6 VFX Graph compiler entry point in place of the removed
  `RecompileIfNeeded` method and an imported graph compile test. The authoring
  view dependency in this entry point is corrected in 0.6.44.

## [0.6.42] - 2026-09-24

### Added

- Report display-only visibility classes as `programmatic-display-class`
  errors. Runtime owners must set `style.display` directly; selection states
  that also style other properties remain supported.

## [0.6.41] - 2026-09-24

### Fixed

- Apply compound class and ID token parsing in the cascade resolver as well as
  the style audit index, so font-reset findings and self-tests use the same
  exact selector matching.

## [0.6.40] - 2026-09-24

### Fixed

- Parse every class and ID token in compound USS selectors. Text-style audits
  now match only their intended elements, and shared-font reset self-tests pass.

## [0.6.39] - 2026-09-24

### Added

- Report `shared-font-definition-reset` as an error when a text selector sets
  `-unity-font-definition` to `none` or `initial` over a concrete font supplied
  by loaded styles. An explicit replacement font remains valid.

## [0.6.38] - 2026-09-24

### Fixed

- Audit opaque and text `Button` elements with authored hover feedback for a
  distinct visible `:active` state. Pagination buttons now receive
  `missing-button-press-feedback` when hover styling has no press counterpart.

## [0.6.37] - 2026-09-24

### Fixed

- Treat a panel UXML's reference to another panel UXML as an error even when
  the imported template has no instance. Follow indirect template imports too.

## [0.6.36] - 2026-09-24

### Added

- Reject embedding one UIDocument panel UXML inside another, including through
  reusable templates. The UXML layout audit identifies panel roots from prefab
  UIDocument references and reports `nested-ui-panel-uxml` as an error.

## [0.6.35] - 2026-09-24

### Added

- Report transparent composite icon buttons that lack visible, distinct
  `:hover` and `:active` USS feedback. The audit ignores state declarations
  blocked by inline styles and states that repeat the normal or hover value.

## [0.6.34] - 2026-09-24

### Added

- Report fixed pixel heights on content-only UXML `Label` elements in normal
  flow, including heights supplied by authored USS. Text now determines its own
  height; spacing belongs to margins or padding, and a minimum can use
  `min-height`. Measured visual, clipping, or interaction contracts retain a
  reasoned `allow-fixed-content-label-height` suppression.

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
