# Native Play Mode frame stepping

`editor/play-mode` with `action=step` admits 1 through 300 native Unity frames.
It publishes a durable `play-mode-transition` Job before doing any work. Poll
the returned capability once to release execution, then observe that Job until
terminal. Main-thread Pipeline commands synchronously unwrap their returned
Task, so an attached deferred wait prevents the native player and inspector
loop from reaching the callbacks on which frame stepping depends.

The Job pauses the player and issues one `EditorApplication.Step` per observed
predecessor. It persists the requested frame before the call and returns from
each Editor update, allowing Unity to finish that frame. Completion reports
frameBefore, frameAfter, framesAdvanced and stepsIssued and leaves Unity paused.
Changing Play Mode, Domain Reload, count overshoot and timeout are explicit
failures with the observed interval. A newer stop request cancels the Job.

Static Cost Ledger: one Job admits at most 300 native steps; each Editor update
uses constant work and issues at most one request after observing its predecessor.
One workspace Job owns fewer than sixteen scalar observation fields, under 4 KiB.
There are no scheduling callbacks, frame-history lists, Asset/window scans,
game callback injection, retries or substitute player updates. The configured
timeout bounds observation. Budget: at most 300 native step calls and constant
owner work per Editor update. PASS.

Verify the official admission/poll path in an existing production scene with
one and multiple frames, exact observed intervals, and rejected out-of-range
input. This does not verify native-window pixels or touch consumption; paused
steps do not run Input System player updates. Simulator input requires actual
running-player advancement and an observed product change.
