# Play Mode options

Use the current catalog's `editor/play-mode-options` contract to inspect or
configure the connected project's reload behavior in stable Edit Mode. An empty
request reads the current state. Explicit mutations return previous/current
values so the caller can restore its original checkpoint.

The native EditorSettings setters own publication to Unity. The command then
marks and saves that same EditorSettings object and independently reads the
serialized file. Success requires the public getters and the persisted fields
to match the request. A backing-field write alone does not prove live adoption.
Disabling the feature keeps both reloads and clears dormant skip flags, as
declared by the catalog. No scene, window or runtime state is created.

`VmTestPlayModeOptionsTests.CommandPublishesNativeOptionsAndVerifiesTheSavedOwner`
covers all five meaningful enabled/reload combinations, restores its original
native values, and retains the enabled/None failure witness. Each fixed test
performs one command plus paired restoration; there is no new asset scan or
data-dependent loop. The existing readback owner loads the single native
EditorSettings file. Additional managed scratch is below 16 KiB per case.
