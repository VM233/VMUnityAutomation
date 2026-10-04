# Native Play Mode frame stepping

`editor/play-mode` with `action=step` owns a bounded sequence of native Unity
frame steps. `frames` defaults to 1 and admits 1 through 300. Only this action
accepts that field. The owner pauses, queues one native step, observes the actual
`Time.frameCount` increase, and only then queues the next step. Completion
includes the requested count, frameBefore, frameAfter and framesAdvanced and
leaves Unity paused. A stopped or changing Play Mode, count overshoot or timeout
is an explicit failure with the observed frame interval.

This supports initialization and camera/input verification when an unfocused
Editor does not continuously render. It does not verify native-window pixels or
replace Simulator input. Send touch phases separately through the native
Simulator pointer route, and inspect the production game after consuming frames.

Static Cost Ledger: one request admits at most 300 native steps; each Editor
update uses constant work and schedules at most one step after observing its
predecessor. There is one retained callback and at most ten scalar observation fields,
under 4 KiB. No Asset/window scan, game callback injection or frame-history list
is retained. The existing command timeout bounds observation; completion, stop
and timeout each remove the callback. Budget: at most 300 native step calls,
constant owner work per Editor update. PASS.

Verification uses the official CLI against an existing production scene:
default one frame, a multi-frame interval, and rejected out-of-range input.
Frame counts and the paused terminal state are the evidence; wall-clock waits
alone are insufficient.
