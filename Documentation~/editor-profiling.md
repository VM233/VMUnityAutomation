# Editor work in Profiler captures

For durable project-job stage markers, see [Persistent job CPU stages](persistent-job-profiling.md).

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
Unity statistics own the timing; the analyzer owns conversion and its immutable
response. CLI consumers adopt the published units without guessing from numeric
size. There is no cached timing, extra capture, simulation, thread or lifecycle.

Static Cost Ledger before executable writes: replace two constant scalar
expressions and retain one raw timing scalar in the existing response. There
are no added loop axes, Unity calls, scene scans, reflection calls, allocations
beyond one dictionary entry, or main-thread waits. Incremental retained storage
is under 256 bytes. Source-generated schemas add two field descriptions and one
map description within the existing registered-source domain. Acceptance covers
known 60/100/20 Hz conversion cases, zero timing and the actual native published
seconds/milliseconds/FPS relationship, with no deep profiling. PASS. Existing
analyzer scene and retained-frame work is unchanged; this unit correction does
not establish a bound or performance claim for that separate work.

`profiler/enable` owns Unity Profiler recording. A capture of editor-driven automation needs `profileEditor: true`. Without it, a long Editor update can appear only as an opaque `EditorLoop` sample even when `profiler/frame-data` reads the maximum supported hierarchy depth. Increasing that read depth cannot create the missing recorded samples.

Use ordinary recording with `profileEditor: true` and `deepProfiling: false` to inspect built-in and explicitly instrumented Editor samples. The response includes the three previous Profiler switches so a caller can restore the original state after collecting and reading retained frames. Leave an optional switch absent to preserve it. The existing omitted `enabled` behavior still starts recording.

Disabling recording retains captured frames. After exporting the required evidence, use `enabled: false, clearFrames: true` to retire the capture through Unity's `ProfilerDriver.ClearAllFrames`. Clearing is explicit and is never implied by stopping recording. The receipt includes the previous frame range, whether clearing was requested, and the resulting retained frame range. A new capture may instead request `enabled: true, clearFrames: true` to clear its predecessor before recording starts. The command does not force a managed garbage collection or change the Editor's frame-history preference.

Capture-retirement Static Cost Ledger: the existing invocation adds four scalar frame-index reads and at most one native cleanup call. There are no managed frame scans, materialized frame products, per-frame API calls, retained dictionaries or extra threads. Fixed response storage remains below 2 KiB. The native owner retires its configured bounded history in one main-thread operation. The current acceptance witness holds exactly 2,000 frames and has recording disabled. Native cleanup is a deliberate between-job maintenance operation with the existing 30-second command deadline, and its duration and process memory must be measured before attributing a memory improvement. PASS for the bounded call domain. Tests cover explicit clearing with recording stopped, absent clearing preserving history, and schema publication. They do not run during an active evidence capture.

Entry: `profiler/enable`. Sole state owner: Unity's `ProfilerDriver`. Producer: the command reads the three prior switches, applies only the requested optional changes, and returns previous and resulting states plus the retained frame range. Consumers: profiling callers and the same source-generated output schema. No cached profile state, extra transport, repeated enable operation or automatic deep profiling is introduced. The CLI caller owns the capture window and restoration.

Static Cost Ledger before executable writes: six Boolean property reads and at most three property writes per invocation, one fixed three-field previous-state dictionary and one fixed seven-field result dictionary, below 2 KiB. No scan, loop, new thread or persistent state. The focused regression disables recording, changes the Editor-sampling switch and verifies exact previous-state publication and restoration in a finally block. A second regression reads the single exact catalog contract. Two tests, no rendered battle or deep profiling. Existing schema generation traverses the same fixed package source domain and adds four Boolean output fields and one input field. PASS.
