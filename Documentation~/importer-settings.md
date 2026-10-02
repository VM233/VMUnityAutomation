# Semantic importer settings

`asset/import-settings/get` reads Unity's current importer. The returned
`settings` is a closed alternative for common importer metadata, a texture, a
model, or audio. Texture and audio alternatives include optional platform
settings. Audio normalization and preload fields appear only when supported by
the active Unity API. The catalog is the authoritative field and type contract.

`asset/import-settings/set` publishes the same settings product in its `before`
and `after` fields. A dry run publishes `before` and the requested changes without
writing. Reimport and later `get` readback establish persisted importer values;
they do not prove playback, rendering, or other runtime behavior.

The importer command is the unique settings producer. Reviewed family shapes
in the route-contract generator describe its product, including additions made
by the texture/model/audio helpers. Generated catalog code is regenerated from
that source. No consumer may replace missing fields or infer them from importer
names. Normal import failures retain the existing command error boundary.

## Static Cost Ledger

Pass. Four fixed settings alternatives contain at most 28 scalar fields and one
fixed platform object. Audio sample objects contain six fields. Generation uses
the existing route scan; this change adds no scan, dynamic input axis, native
import call, gameplay allocation, or Unity lifecycle. Three regression cases
each consume one 2 by 2 test texture, at most two native settings products, and at
most 29 key comparisons per product. Each owned test Asset is deleted in its
existing teardown. Published catalog and live importer readbacks are compared
using the exact closed schema.
