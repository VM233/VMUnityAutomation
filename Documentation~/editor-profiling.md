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

Use ordinary recording with `profileEditor: true` and `deepProfiling: false` to inspect built-in and explicitly instrumented Editor samples. The response includes the three previous Profiler switches so a caller can restore the original state after collecting and reading retained frames. Leave an optional switch absent to preserve it. The existing omitted `enabled` behavior still starts recording.

Disabling recording retains captured frames. After exporting the required evidence, use `enabled: false, clearFrames: true` to retire the capture through Unity's `ProfilerDriver.ClearAllFrames`. Clearing is explicit and is never implied by stopping recording. The receipt includes the previous frame range, whether clearing was requested, and the resulting retained frame range. A new capture may instead request `enabled: true, clearFrames: true` to clear its predecessor before recording starts. The command does not force a managed garbage collection or change the Editor's frame-history preference.

Capture-retirement Static Cost Ledger: the existing invocation adds four scalar frame-index reads and at most one native cleanup call. There are no managed frame scans, materialized frame products, per-frame API calls, retained dictionaries or extra threads. Fixed response storage remains below 2 KiB. The native owner retires its configured bounded history in one main-thread operation. The current acceptance witness holds exactly 2,000 frames and has recording disabled. Native cleanup is a deliberate between-job maintenance operation with the existing 30-second command deadline, and its duration and process memory must be measured before attributing a memory improvement. PASS for the bounded call domain. Tests cover explicit clearing with recording stopped, absent clearing preserving history, and schema publication. They do not run during an active evidence capture.

Entry: `profiler/enable`. Sole state owner: Unity's `ProfilerDriver`. Producer: the command reads the three prior switches, applies only the requested optional changes, and returns previous and resulting states plus the retained frame range. Consumers: profiling callers and the same source-generated output schema. No cached profile state, extra transport, repeated enable operation or automatic deep profiling is introduced. The CLI caller owns the capture window and restoration.

Static Cost Ledger before executable writes: six Boolean property reads and at most three property writes per invocation, one fixed three-field previous-state dictionary and one fixed seven-field result dictionary, below 2 KiB. No scan, loop, new thread or persistent state. The focused regression disables recording, changes the Editor-sampling switch and verifies exact previous-state publication and restoration in a finally block. A second regression reads the single exact catalog contract. Two tests, no rendered battle or deep profiling. Existing schema generation traverses the same fixed package source domain and adds four Boolean output fields and one input field. PASS.
