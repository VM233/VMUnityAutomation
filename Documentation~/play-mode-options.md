# Play Mode options

Use the current catalog's `editor/play-mode-options` contract to inspect or
configure the connected project's reload behavior in stable Edit Mode. An empty
request reads the current state. Explicit mutations return previous/current
values so the caller can restore its original checkpoint.

The four supported states are full Domain and Scene reload (`enabled=false`),
or enabled options skipping Domain reload, Scene reload, or both. Enabled
options with no skipped reload are rejected before mutation; use the explicit
full-reload state. Partial updates resolve against the current native state.
The command saves the authoritative EditorSettings object and independently
reads its serialized file. Success requires public getters and persisted fields
to match the request. No scene, window or runtime state is created.

`VmTestPlayModeOptionsTests.CommandPublishesSupportedOptionsAndVerifiesTheSavedOwner`
covers the four supported combinations and restores its original native values.
`EnabledWithoutAnySkippedReloadIsRejectedBeforeMutation` preserves the
enabled/None failure witness and checks unchanged native state and file bytes. Each fixed test
performs one command plus paired restoration; there is no new asset scan or
data-dependent loop. The existing readback owner loads the single native
EditorSettings file. Additional managed scratch is below 16 KiB per case.

The domain-reload test guard is also checked from disabled/None and
enabled/Scene states, including paired restoration. The fixture asserts the
native starting state before exercising the guard.
