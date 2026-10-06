# Native UI Toolkit pointer input

`uitoolkit/runtime-pointer` publishes one pooled native pointer event to an attached
runtime UIDocument. Use its exact `instanceId` from `uitoolkit/runtime-documents`,
Down, Move or Up, and panel coordinates from runtime-query world bounds. Unity owns
hit testing, pointer capture, Clickable and Slider behavior. Inspect the product's
state after dispatch to verify its effect. The Editor may be paused.

This entry stimulates the UI Toolkit layer. Device Simulator touch publication and
Input System player-buffer consumption remain separately observable through their
own commands. It does not invoke callbacks or assign control values.

When changing a built-in route, run `Documentation~/generate-route-contracts.py
--write` and review its contract and audited fingerprint changes together. The
runtime provider rejects a route manifest whose fingerprint was not regenerated.

Pointer admission reports a missing attached document, an out-of-bounds point or
a point hit-tested to another document with the exact errors published in the
catalog. Builder preview similarly reports when its native Fit control is absent.

## Static Cost Ledger

Each invocation resolves one native object identity and performs one native panel
Pick. There is no Asset scan, duplicated UI traversal, loop, retained event, cache,
callback or frame pump. It pools, sends and disposes one event on the Editor main
thread and returns seven scalars below 1 KiB. Native control callbacks own any game
effects. Bounds and attachment are checked before dispatch. Result: pass.

UI Builder preview uses the same native event dispatcher for its existing Fit
viewport control after document layout settles. One named native query and two
pooled events occur once per preview. No zoom reflection, repeated fitting, Asset
write or alternative document is introduced. Result: pass.
