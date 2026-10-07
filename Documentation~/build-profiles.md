# Build Profiles

`build/profile` is the Unity 6 owner for native Build Profile discovery and
authoring. Use `action: info` to read installed platform GUIDs, existing
profiles, the active profile, profile scenes and scripting defines.

Create a profile through a `transaction` containing a `create` operation with a
`profileName` and one exact `platformId` returned by `info`. Unity creates the
asset below `Assets/Settings/Build Profiles`. A duplicate asset path or a GUID
that is not installed fails before mutation.

Subsequent transactions can use the returned `assetPath` with `set-active`,
`set-scenes`, `set-scripting-defines`, or `set-property`. Keep
`set-scripting-defines` last because Unity can start compilation and a Domain
Reload when the defines apply. `set-global-scenes` continues to edit the global
build-scene owner rather than a profile.

For `set-active`, pass `assetPath: null` explicitly to select Unity's platform
profile. `info.activeProfile` and the operation's `profile` are null in this
state. A dry run preserves the current selection. Failed transactions restore
the original selection, including the platform profile; rollback failures are
reported separately.

Use `set-player-property` for a scalar serialized property on a profile's existing
Player Settings override. `info` publishes that native object's instance ID for
`serialized-object/get`. The transaction changes the native override and invokes
the profile's native `SerializePlayerSettings` before saving the profile asset.
Missing overrides fail admission; global Player Settings remain a separate owner.
`set-property` continues to address the Build Profile object itself. Native string
properties remain scalar values even when Unity reports their array flag.

The command invokes Unity's native Build Profile API and reads the resulting
objects back. It does not write Build Profile YAML or maintain a second platform
registry.

## Player override Static Cost Ledger

Transactions admit at most 128 operations. A player-property operation resolves
one existing profile and its existing native override, one property and one
primitive value. Strings admit at most 4,096 characters. No asset-wide owner
search, recursive value traversal, profile switching or allocation of a second
Player Settings object occurs. Native Undo stores the existing profile and its
override once per operation, then native serialization persists that owner.
The Unity settings graph is fixed by the installed Editor; the command introduces
no input-dependent serialized graph or new cache. Work is synchronous on the
Editor main thread, with at most 128 native setter/serialization calls and one
asset-save transaction. Budget: 128 operations, 512 KB of input strings; pass.
