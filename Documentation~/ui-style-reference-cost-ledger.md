# UI style reference coherence review

Before implementation: inspect at most8 explicit Assets-relative USS/UXML paths,
each at most4096 characters. A USS owns one StyleSheet; a UXML owns at mostone
inline StyleSheet. Imported sheets/templates are not recursively followed.

Each sheet admits at most256 cached asset references and256 serialized references.
Copy the cached reference vector before constructing SerializedObject, then read
the native serialized vector. Compare CLR nullness, Unity liveness, concrete
managed type and the shared cross-version object identity. This distinguishes a
cached destroyed or incorrectly typed wrapper from the currently serialized asset.
It does not infer which selector won or repair/invalidate any sheet.

The worst case is2048 comparisons,8 explicit asset loads/SerializedObjects,
4096 identity reads and4096 GUID/local-ID lookups when all records are requested.
No scene/object traversal, asset import/save, pooling, random draw, event, cache,
per-frame work or second transport is introduced. Results retain at most2048
paired scalar descriptors;128KiB reference scratch and4MiB response budgets apply.
Descriptors omit repeated names/paths, bound type names to512 characters, and
use at most20-digit object/local IDs plus32-character GUIDs. Input paths occur
once per sheet, giving a conservative3MiB JSON ceiling within that budget.
Oversized arrays, unresolved paths or unsupported engine field shapes reject;
results are never truncated. All calls run on the native Editor main thread.

Tests use bounded temporary in-memory StyleSheets/Textures: equal references,
different identities, destroyed cached wrappers, missing cached/native partners,
empty arrays and the256-reference boundary. They leave no project asset files.
The consumer must additionally replay its actual bad presentation through the
formal catalog and compare the real runtime resource audit. PASS.
