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

The command invokes Unity's public `BuildProfile` API and reads the resulting
asset back. It does not write Build Profile YAML or maintain a second platform
registry.
