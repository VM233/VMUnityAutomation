# Background Play Mode execution

`editor/background-session` is a typed catalog tool. Discover its exact command
and schema through the current catalog. Begin requires stable Play Mode and
returns a sessionId capability; pass it to Inspect and End and always End in
finally. The bounded lifetime defaults to 600 seconds and cannot exceed 3600.

The owner enables `Application.runInBackground` and queues the native PlayerLoop.
It never opens or focuses an Editor window, changes Game View presets, saves
Player Settings, fabricates application focus, or injects input. Observe the
reported Frame against FrameBefore to prove actual progress while
ApplicationFocused and EditorActive reflect their native states.

Games that deliberately pause simulation when unfocused may explicitly consume
`VmEditorBackgroundExecution.Active` under UNITY_EDITOR in their activity owner.
Input System admission remains a separate input contract. Begin does not make
an isolated game session or alter its saves; use the game's actual verification
repository owner for gameplay work.

End, expiry, Play Mode exit, assembly reload and Editor quit restore the captured
runtime background value and unregister the frame pump. Session capabilities
are domain-local and cannot survive reload. Persistent settings are unchanged.

Static cost: one session per domain, O(1) begin/inspect/end and one O(1) native
update request per Editor update, no Asset scans, no per-frame allocations or
window iteration. Publication results allocate one bounded DTO per invocation.
