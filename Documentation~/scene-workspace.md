# Scene workspace reload

`scene/workspace` owns loaded-scene transitions. Its `reload` action reloads
the sole loaded, saved scene from disk through `EditorSceneManager.OpenScene`.
Select it with `path` or `name`. A dirty scene requires an explicit `save=true`
or `discardChanges=true` decision. These choices are mutually exclusive.
Reload rejects Play Mode, an unsaved scene, and a workspace with more than one
scene slot before changing scene state. Opening an already loaded scene remains
idempotent; closing the last loaded scene is not a reload operation.

The command publishes the existing workspace result with `openedScene` and
`mode=single`. Scene persistence and object retirement remain Unity's owners.
The execution boundary owns project binding and dangerous-operation confirmation;
the handler owns the precise scene selector, dirty-state decision and transition.
There is no second transport, generated scene, temporary scene or external YAML
write. Other projects are outside this operation.

Static Cost Ledger before executable writes: reload admits exactly one scene
slot before existing selector enumeration. It performs one selector pass and one
workspace-result pass, each over one scene, plus one native open operation.
There is no GameObject scan, added cache, per-frame work or retained state.
Unity reload work is proportional to the same saved scene already being opened;
this operation adds no second scene or object graph. The result contains one
scene descriptor and is below 4 KiB. PASS for the single-scene domain.

Validation uses the original dirty Main scene produced by removing two owned
test helpers. Reload must leave the same scene active and clean, preserve its
disk bytes, and restore its authored components. Package adoption, clean native
compilation, exact source review and dependency/meta review are separate gates.
