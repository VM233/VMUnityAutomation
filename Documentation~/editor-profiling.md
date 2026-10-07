# Editor work in Profiler captures

## Memory category contracts

The `profiler/memory-breakdown` owner returns an object keyed by its nine asset
categories, with integer counts/bytes and numeric MiB totals. Optional `topAssets`
entries contain the name and sizes, with optional detail and asset path strings.
`memoryProfilerPackageInstalled` is a Boolean. The route-contract generator
publishes this same closed shape for the catalog and CLI.

Entry: the existing memory-breakdown command. Producer and lifetime owner:
`VmAutomationMemoryProfilerCommands`. Product: its category dictionary. The
generator owns schema publication; catalog and CLI callers consume it. This
correction changes no scan, profiling state, allocation owner or gameplay path.

Static Cost Ledger before executable writes: PASS. Schema construction adds a
fixed nine-category object, each with four properties and a five-property asset
item schema. At most 128 schema properties and their required-name lists are
constructed, below 256 KiB per contract. There is no input-dependent scan,
native call, retained state or new thread. Existing generator traversal retains
352 C# sources, below 7 MiB; only two reviewed output-property overrides change.
The focused catalog regression visits nine categories and five asset properties,
with at most 128 checks and below 256 KiB of schema scratch. It never enumerates
loaded assets, changes the Profiler or runs a battle. Native memory-breakdown
scanning is outside this fix and is not used for validation during calibration.

For durable project-job stage markers, see [Persistent job CPU stages](persistent-job-profiling.md).
For same-frame native physics counters, see [Retained frame counters](profiler-frame-counters.md).
For exact native method-address queries, see [Retained-frame methods](profiler-method-addresses.md).

## Rendering timing units

`profiler/stats` publishes the native `UnityStats.frameTime` and `renderTime`
in seconds. `profiler/analyze` retains that native value as `frameTimeSeconds`,
converts it to `frameTimeMs`, and derives `estimatedFps` as its reciprocal.
This estimate describes the current main-thread timing; it is not a measured
whole-run FPS distribution or a guarantee about rendering, waits or stalls.
The two-decimal millisecond and one-decimal FPS values are display summaries.
Retained Profiler frame data continues to use its explicit `frameTimeMs` unit.
Unity's [reference statistics window](https://github.com/Unity-Technologies/UnityCsReference/blob/2022.3/Editor/Mono/GameviewGUI.cs)
uses the same seconds-to-milliseconds and reciprocal conversions.

Entry: the existing `profiler/stats` and `profiler/analyze` contracts. Native
Unity statistics own the counters and timing; `GetRenderingStats` captures the
single current product and the analyzer consumes it before publishing its
conversion. CLI consumers adopt the published units without guessing from
numeric size. There is no cached timing, extra capture, simulation, thread or
lifecycle.

Unity 6000.4 changed the public counter API. `batches` exists only before that
version; `totalIndirectDrawCalls` exists from that version onward. Their schema
fields are optional and describe this native version boundary. No synthetic
batch count or absent-property fallback is produced. Both supported compilation
branches read the common counters directly. The two existing native timing
properties retain their reflection-based reads; this repair does not move their
observation owner to a different frame-timing API. Unexpected native read errors
propagate through the existing executor instead of silently omitting rendering.

The 0.6.76 native witness returned stats while playing but omitted the whole
analyzer rendering product: the removed batches property threw inside a silent
catch. The original witness is distinct from the independently verified timing
conversion. It requires removal of the duplicated analyzer property reader and
its swallowed failure, plus explicit compile-time counter selection.

Static Cost Ledger before executable writes: the shared snapshot performs 25
common integer reads, two timing reads, one screen read, one version-specific
integer read and one Play-state read = 30 constant native/property reads. This
replaces the old 30-candidate reflection scan and the analyzer's separate eight
reflection reads. There are two reflection lookups for the existing timing
fields, no data-dependent axis, retained state, added scene scan, timer or wait.
One fixed snapshot/conversion dictionary stays below 4 KiB. Tests read one
snapshot and both exact contracts; private source-linked acceptance has four
literal timing inputs in each of the old/current compilation branches (eight
cases), two Edit-state snapshots and the frozen control, at most 400 mocked
scalar reads in total, with zero native frame advancement or capture.
Native acceptance must reproduce the current 6000.6 counter shape and published
seconds/milliseconds/FPS relationship in one brief Play session with deep
profiling disabled. PASS for the changed observation domain. Existing analyzer
scene and retained-frame work is unchanged; this repair does not establish a
performance bound for that separate work.

`profiler/enable` owns Unity Profiler recording. A capture of editor-driven automation needs `profileEditor: true`. Without it, a long Editor update can appear only as an opaque `EditorLoop` sample even when `profiler/frame-data` reads the maximum supported hierarchy depth. Increasing that read depth cannot create the missing recorded samples.

Use ordinary recording with `profileEditor: true` and `deepProfiling: false` to inspect built-in and explicitly instrumented Editor samples. Set optional `frameHistoryLength` to the number of frames to retain for a bounded capture. Its integer domain is 1 through the installed Unity version's `ProfilerUserSettings.kMaxFrameCount`; the catalog publishes that native upper bound. Omit it to preserve the current capacity. The response publishes the resulting capacity and all previous settings, so the caller can restore them after collecting evidence. The existing omitted `enabled` behavior still starts recording.

Disabling recording retains captured frames. After exporting the required evidence, use `enabled: false, clearFrames: true` to retire the capture through Unity's `ProfilerDriver.ClearAllFrames`. Clearing is explicit and is never implied by stopping recording. The receipt includes the previous frame range, whether clearing was requested, and the resulting retained frame range. A new capture may instead request `enabled: true, clearFrames: true` to clear its predecessor before recording starts. Restore `frameHistoryLength` from `previous` before leaving the capture session: the native setter changes the Editor preference as well as current retention. Reducing capacity can discard old frames. Export needed evidence before changing it. No managed garbage collection is forced.

Capacity limits retained frame count, not bytes. A 128-frame capture can still contain expensive samples. Record process memory before capture, after stopping, and after explicit retirement; do not interpret `Profiler.maxUsedMemory` (the sender buffer limit) as a limit on retained Editor history.

Frame-history entry: the existing `profiler/enable` contract. Unity's `ProfilerUserSettings` owns capacity and its persisted preference; `ProfilerDriver` owns recording and retained frames. The command validates the requested capacity before any mutation, captures the prior native settings, applies explicit changes, and publishes a fixed result. The generated schema and CLI consumer read the same product. The caller owns the capture window, evidence export, retirement and restoration. There is no new timer, cache, transport, frame scan or gameplay state.

The settings class is internal across the declared Unity support range. One binding reads its exact native capacity property and maximum constant by reflection; it does not invoke a guessed method or keep a second capacity state. Missing members fail at the binding boundary. The binding's immutable metadata lives until domain reload. Unity 2021.3/2022.3 reference source and installed 2022.3/6000.4/6000.6 metadata establish this member shape.

Frame-history Static Cost Ledger before executable writes: one exact type, property and field lookup per domain, at most two extra native scalar reads and one capacity setter per invocation, with two added integer response fields. No data-dependent loop, retained capture product or additional thread; metadata and fixed response remain below 4 KiB. Native history is bounded by the requested frame count, whose maximum comes from Unity's constant rather than a second version table. Native per-frame storage is outside this command's allocation domain; frame count is explicitly not a byte budget. Focused tests cover capacities 1 and 128, native maximum, invalid zero and over-maximum, preserving an absent capacity, exact schema/effects, and finally-block restoration. They do not advance frames or enable deep profiling. PASS for the command and test domain.

The calibration diagnostic caller admits one frozen four-arm replay, uses 128 retained frames and ordinary Editor sampling, reads at most 128 frame summaries and five hierarchies of at most 512 entries, and exports at most 16 MiB of JSON. It stops and clears the capture, restores settings, retires the calibration lease and leaves clean Edit Mode. Collection and native memory costs must be measured separately from the unprofiled calibration; this diagnostic does not establish a gameplay performance fix.

Capture-retirement Static Cost Ledger: the existing invocation adds four scalar frame-index reads and at most one native cleanup call. There are no managed frame scans, materialized frame products, per-frame API calls, retained dictionaries or extra threads. Fixed response storage remains below 2 KiB. The native owner retires its configured bounded history in one main-thread operation. The current acceptance witness holds exactly 2,000 frames and has recording disabled. Native cleanup is a deliberate between-job maintenance operation with the existing 30-second command deadline, and its duration and process memory must be measured before attributing a memory improvement. PASS for the bounded call domain. Tests cover explicit clearing with recording stopped, absent clearing preserving history, and schema publication. They do not run during an active evidence capture.

Entry: `profiler/enable`. Recording state owner: Unity's `ProfilerDriver`; frame-history capacity owner: Unity's `ProfilerUserSettings`. Producer: the command reads prior native settings, applies only the requested optional changes, and returns previous and resulting states plus the retained frame range. Consumers: profiling callers and the same source-generated output schema. No cached profile state, extra transport, repeated enable operation or automatic deep profiling is introduced. The CLI caller owns the capture window and restoration.

Static Cost Ledger before executable writes: six Boolean property reads and at most three property writes per invocation, one fixed three-field previous-state dictionary and one fixed seven-field result dictionary, below 2 KiB. No scan, loop, new thread or persistent state. The focused regression disables recording, changes the Editor-sampling switch and verifies exact previous-state publication and restoration in a finally block. A second regression reads the single exact catalog contract. Two tests, no rendered battle or deep profiling. Existing schema generation traverses the same fixed package source domain and adds four Boolean output fields and one input field. PASS.
