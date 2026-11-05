# Native Editor window recording

## Native capture and bounded PNG workers

The frozen 557x1293 UI witness spends 76..124 ms per synchronous view capture,
including PNG encoding and disk writes on the Editor thread. A 160 ms transition
then has only opacity 0 and 1 in consecutive native frames. This is observation
interference; changing the product clock, FPS, resolution or duration cannot
qualify it. Unity 2021.3 explicitly documents EncodeArrayToPNG as thread safe:
https://docs.unity3d.com/2021.3/Documentation/ScriptReference/ImageConversion.EncodeArrayToPNG.html

Read and normalize the same actual native view on the main thread. Hand only its
owned RGB bytes to at most two PNG/file workers, with at most four admitted frame
buffers. Preserve exact native timestamps, dimensions, all PNG pixels and the
original cadence. A full queue fails instead of dropping a frame or changing
cadence. End joins every admitted frame before FFmpeg; failure, reload and quit
cancel and join those exact workers before reporting resource release. No Unity
object or Editor API crosses the worker boundary.

Static Cost Ledger: the existing 4096-axis cap admits 16,777,216 pixels/frame.
Four RGB buffers retain 201,326,592 bytes; two PNG outputs are each capped 64 MiB.
One native ARGB target plus RGB texture CPU/GPU/readback allocations and those
buffers fit a 640 MiB logical-image budget. Opaque native codec allocation remains
a measured gate. At most 450 frame records, four pending jobs and two running
workers exist; each pixel is read/normalized once and encoded once, with no
per-frame Asset scan. Files/FFmpeg retain the existing 30-second/15fps budgets.

One bounded revision must pass pixel equivalence, queue admission, worker failure
and cancel/join controls, then replay the exact original UI request with its
unchanged stimulus, FPS, resolution and duration. Report native readback and PNG
times separately. If native capture still prevents natural intermediate frames,
stop this method and research that boundary; do not add workers or tune limits.

Discover `editor/record-window` through the official Unity CLI catalog. It is a general recorder for one existing visible Editor tab, including Game View in Edit Mode. It does not create/focus windows, change resolution, enter Play Mode, inject input, or implement a project's comparison policy.

Use `editor/focus-window` to prepare an existing view before capture. Its optional `maximized` flag sets the native EditorWindow state and reports `PreviousMaximized` and `Maximized`; omission preserves that state. Save the previous focused identity and maximization, verify the requested state before Begin, and restore them after capture. This changes the Editor layout only, preserving Game View resolution presets.

Floating views use their own existing native ContainerWindow's `ToggleMaximize` and `maximized` readback. `MaximizationTarget` identifies this path as `FloatingContainerWindow`; docked views report `EditorWindow`. This maximizes the requested Game View itself and does not maximize the main Editor window or resize exported pixels. No view is created or docked to achieve capture.

`Begin` takes `windowInstanceId`, an absolute new `outputPath` ending in `.mp4`, and an explicit installed FFmpeg `encoderPath`. Pass the exact decimal-string window identity returned by `editor/list-windows`; recording uses the same version-aware object identity owner as the other window commands, including 64-bit EntityIds. Optional `durationSeconds` is in (0,30], `framesPerSecond` in [1,15], and `maxDimension` in [256,4096], with its default kept at 2048. The dimension is native surface admission validation, not resizing. Output and its `.frames` directory must be new and outside Assets. Use the returned `SessionId` with `Inspect` or `End`; End completes capture early and lets encoding finish. A successful terminal result has `State=Succeeded`, `EncoderExitCode=0`, and `ResourcesReleased=true`.

Each frame uses the existing native Editor-view capture owner, including its Game View Canvas publication and visible-tab verification. PNG frames, a variable-duration FFmpeg concat descriptor, actual monotonic start/completion times and the final recording receipt remain beside the MP4. Captures that miss sampling deadlines retain their real elapsed durations; they are not relabeled as successful fixed-rate samples. Encoder output is 30 fps H.264; it holds captured pixels across the measured intervals.

Optional `relativeCadence=true` schedules the next sample one requested period after the actual previous sample. A consumer with 0.5-second static states can request 2 fps and this cadence, schedule its next state from the actual current hook time, and leave the native Editor idle between samples instead of spending that time encoding duplicate PNGs. The receipt reports the cadence and measured timestamps. Default absolute-grid sampling is unchanged.

The session owns its Editor update callback, bounded PNG workers and one encoder process. Automatic duration expiry and End stop capture, restore consumer state, drain every PNG, then encode. Failure, reload and Editor quit detach the callback, invoke restoration, cancel and join the PNG workers and retire the exact encoder. Reload produces an interrupted receipt; session IDs belong to one Editor lifetime. MP4 encoding has a 60 second bound. `FinishingFrames` is a nonterminal drain state; success requires zero pending images and retired resources. No extra transport, worker pipe or persistent scene component is installed.

Project workflows can compose the same owner with `VmEditorWindowRecording.Begin(request, beforeCapture, afterCapture)`. `beforeCapture` receives actual elapsed seconds on the Editor thread; `afterCapture` retires project-owned state/listeners on normal completion, failure and interruption. Hooks must be bounded, must not create a second recorder, and must restore the actual prior state. Project-specific toggle sequences, button selection and reference conventions belong in that project's tool, not this generic recorder.
