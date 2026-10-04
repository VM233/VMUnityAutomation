# Native Play Mode frame stepping

`editor/play-mode` with `action=step` owns a bounded sequence of native Unity
frame steps. `frames` defaults to 1 and admits 1 through 300. The owner pauses,
queues one native step, observes the actual
`Time.frameCount` increase, and only then queues the next step. Completion
includes the requested count, frameBefore, frameAfter and framesAdvanced and
leaves Unity paused. A stopped or changing Play Mode, count overshoot or timeout
is an explicit failure with the observed frame interval.

Paused native steps support initialization and camera verification. Input System
does not treat paused Editor frames as running player input; these steps must not
be used to prove a Simulator touch reached the game UI.

`action=advance` instead keeps the player unpaused, requests the native Editor
player loop, observes at least the requested actual frame interval, then pauses.
This action accepts the same count bound. Its report contains the actual interval,
including any continuously rendered extra frames. It never injects input or
updates Input System independently. Send Simulator touch phases separately, use
an unpaused interval to consume each phase, and inspect the resulting game state.
Neither action verifies native-window pixels.

Static Cost Ledger: one request admits at most 300 native steps; each Editor
update uses constant work and schedules at most one step after observing its
predecessor. There is one retained callback and at most ten scalar observation fields,
under 4 KiB. No Asset/window scan, game callback injection or frame-history list
is retained. The existing command timeout bounds observation; completion, stop
and timeout each remove the callback. Budget: at most 300 native step calls,
constant owner work per Editor update. The unpaused interval retains the same
single callback and at most 300 native player-loop requests, each successor
following an observed frame. There is no repeated enqueue while waiting for a
frame. Completion/stop/timeout release the callback; a playing terminal interval
is paused. PASS.

Verification uses the official CLI against an existing production scene:
default one frame, a multi-frame interval, and rejected out-of-range input.
Frame counts and the paused terminal state are the evidence; wall-clock waits
alone are insufficient.
