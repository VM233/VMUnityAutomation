# Existing Sprite rectangle updates

`sprite/update-rects` updates named subassets of an existing Multiple Sprite
texture through Unity's Sprite Editor data provider. Coordinates use source
pixels with a bottom-left origin. The request supplies a bounded list of names,
rectangles and normalized pivots. Other slices remain untouched. The command
preserves texture GUID, Sprite IDs, local file IDs and borders, sets Full Rect,
reimports once, and verifies the persisted native values and identities.

The current catalog owns the exact request, result and error contracts.
Invalid names, duplicate entries, non-finite values and out-of-bounds rectangles
are rejected before any mutation. A persisted mismatch is an explicit failure.

## Static Cost Ledger

Frozen bounds before implementation: one existing texture, at most 256 existing
Sprite rectangles and at most 256 requested updates. Dictionary passes are
linear: at most 1,536 records over preflight, apply and persisted verification;
no Cartesian product or project scan. Native calls are two provider reads, one
provider apply, one SaveAndReimport, two LoadAllAssetsAtPath identity reads and
at most 512 local-file-ID lookups. Managed request/result/maps stay below 1 MiB
at the admitted 256-character name limit. Native import work belongs to the
existing TextureImporter; no pixel copy, persistent cache, job or extra thread.
All Unity calls run on the main thread. Budget: 256 slices and 1 MiB managed
metadata; PASS. Tests use one 64 x 32 texture, two slices, and at most three
updates per invocation; teardown removes only that test's uniquely named asset.
