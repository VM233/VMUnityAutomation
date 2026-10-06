# Editor window lifecycle

Discover the current `editor/close-window` contract through the official Unity
CLI facade. Supply an exact `windowInstanceId` returned by `uitoolkit/windows`.
The command is bound to the caller's absolute project path.

The owner requires stable Edit Mode and calls Unity's `EditorWindow.Close`.
Windows with unsaved changes are rejected before closure; the command never
accepts Save/Discard dialogs. A successful result reports `Closed=true` after
the native window has been destroyed. Inspect the window inventory afterward
when restoring a validation workspace.

This operation closes an Editor view. It does not close a project, Scene,
runtime UI panel, or game session.

## Existing Game View commands

`gameview/info`, `gameview/set-resolution` and `gameview/set-scale` resolve exactly
one already open Game View. They never create or focus a view. No open Game View
returns `game_view_unavailable`; multiple instances return `game_view_ambiguous`.
The information query also leaves zoom initialization untouched. Explicit scale
changes initialize zoom on the existing view before applying the requested scale.

Use the existing Device Simulator or another suitable view for verification when
Game View is absent. A missing-window response does not request window creation.

The regression uses a workspace with no Game View, invokes all three production
handlers and checks the native window inventory and focused window are unchanged.
It creates no test windows and does not alter the user's layout.

## Static Cost Ledger

Resolution uses one native loaded-object query for the exact Game View type and
reads its array length and, for one match, its first item. The DoomsdayDiary witness
has zero Game View instances and 15 total Editor windows. No managed traversal,
polling, retained state or gameplay-frame work is added. Native query results are
bounded by loaded Game View instances; the zero-window witness allocates only the
empty query result. Three focused cases query the 15-window inventory before and
after, with no new objects or windows. PASS for this frozen regression.
