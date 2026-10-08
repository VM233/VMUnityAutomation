# Native Play Mode frame stepping

`editor/play-mode` with `action=step` owns a bounded sequence of native Unity
frame steps. `frames` defaults to 1 and admits 1 through 300. Only this action
accepts that field. The owner pauses, schedules one native step after the current
Editor update, observes the actual `Time.frameCount` increase, and schedules its
successor at the next native `delayCall` boundary. A frame-count increase can be
visible while Unity is still finishing that frame; issuing a successor inside
that same update can lose its request. Completion
includes the requested count, frameBefore, frameAfter and framesAdvanced and
leaves Unity paused. A stopped or changing Play Mode, count overshoot or timeout
is an explicit failure with the observed frame interval.

This supports initialization and camera verification when an unfocused
Editor does not continuously render. It does not verify native-window pixels or
replace Simulator input. Send touch phases separately through the native
Simulator pointer route. Paused native steps are not running Input System player
updates, so they do not prove touch consumption. Verify a click only while the
running player actually advances and the resulting game state changes.

Static Cost Ledger: one request admits at most 300 native steps; each Editor
update uses constant work and schedules at most one step after observing its
predecessor. There is one persistent observation callback, at most one outstanding
one-shot scheduling callback and thirteen scalar observation fields, under 4 KiB.
No Asset/window scan, game callback injection or frame-history list is retained.
The existing command timeout bounds observation; completion, stop and timeout
remove both callbacks. Budget: at most 300 native step calls and constant owner
work per Editor update.
The sequence performs at most 300 delay-call invocations, with no elapsed-time
delay, retry or substitute player update. Failure evidence also records issued
step count, the last request frame and whether a scheduling callback was pending.
PASS.

Verification uses the official CLI against an existing production scene:
default one frame, a multi-frame interval, and rejected out-of-range input.
Frame counts and the paused terminal state are the evidence; wall-clock waits
alone are insufficient.
