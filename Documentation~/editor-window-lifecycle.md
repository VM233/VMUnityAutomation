# Editor window lifecycle

## Observe native focus without activating a tab

Discover `editor/window-focus-state` through the official CLI facade and invoke
its typed empty request with the current absolute project binding. The result's
`WindowInstanceId`, `WindowType` and `WindowTitle` describe the single native
`EditorWindow.focusedWindow`. All three are empty strings when Unity has no
focused Editor window. The query creates, activates, focuses and retains nothing.

Pair this product with `uitoolkit/windows` before and after a validation lifetime.
The window inventory and the focus identity are separate observations. Do not
activate a window merely to obtain the previous focus from a mutation result.

## Activate an existing tab

Discover `editor/focus-window` through the official CLI facade. Query
`uitoolkit/windows` first, then pass the exact `windowInstanceId` with the current
absolute project binding. The owner resolves that existing object and calls
`EditorWindow.Focus`, activating its docked tab. A successful result reports the
actual target identity/type and previous focused identity/type/title/position.
`Focused=true` requires the native focused window to equal the target.

Omitting `maximized` preserves the target's existing native state. An explicit
Boolean sets the existing docked window or the floating view's own native
container, verifies its readback, and returns `PreviousMaximized` for restoration.
The optional maximization changes native view geometry; Game View resolution
presets and project assets remain unchanged. A missing identity is
`editor_window_not_found`; failed focus or maximization has its distinct catalog
error. No window is created or closed and Play Mode is unchanged.

This command is an explicit Editor-view mutation. Restore an authorized focus
change with the returned previous identity and any requested maximization with
its returned prior value. The owner keeps no retained session state. An ordinary
focus call performs one identity lookup, Focus and readback. Optional maximization
adds one native state operation and another Focus/readback; floating views use
their existing native container. There is no traversal, polling or extra render.

## Close an existing window

Discover the current `editor/close-window` contract through the official Unity
CLI facade. Supply an exact `windowInstanceId` returned by `uitoolkit/windows`.
The command is bound to the caller's absolute project path.

The owner requires stable Edit Mode and calls Unity's `EditorWindow.Close`.
Windows or native UI Builder documents with unsaved changes are rejected before closure; the command never
accepts Save/Discard dialogs. A successful result reports `Closed=true` after
the native window has been destroyed. Inspect the window inventory afterward
when restoring a validation workspace.

This operation closes an Editor view. It does not close a project, Scene,
runtime UI panel, or game session.

Native Builder preview retirement uses this same Close owner. It reads the
Builder's actual open document in addition to EditorWindow.hasUnsavedChanges,
which alone need not reflect the native document's state. No second close route
or source-save/discard path is introduced.

## Existing Game View commands

`gameview/info`, `gameview/set-resolution` and `gameview/set-scale` resolve exactly
one already open Game View. They never create or focus a view. No open Game View
returns `game_view_unavailable`; multiple instances return `game_view_ambiguous`.
The information query also leaves zoom initialization untouched. Explicit scale
changes initialize zoom on the existing view before applying the requested scale.

Resolution selection reuses an existing fixed-size preset; arbitrary dimensions
use a [temporary resolution session](game-view-presets.md). Capture the complete
preset table before verification and compare it after session retirement, in
addition to the original selection, scale, focus and window inventory. Game View
presets are global user preferences, not project-owned test artifacts.

Use the existing Device Simulator or another suitable view for verification when
Game View is absent. A missing-window response does not request window creation.

The regression uses a workspace with no Game View, invokes all three production
handlers and checks the native window inventory and focused window are unchanged.
It creates no test windows and does not alter the user's layout.

## Static Cost Ledger

Window discovery uses one native loaded-object query for the exact Game View type and
reads its array length and, for one match, its first item. The DoomsdayDiary witness
has zero Game View instances and 15 total Editor windows. No managed traversal,
polling, retained state or gameplay-frame work is added. Native query results are
bounded by loaded Game View instances; the zero-window witness allocates only the
empty query result. Three focused cases query the 15-window inventory before and
after, with no new objects or windows. PASS for this frozen discovery regression.
Preset operations have their separate input and cost contract in game-view-presets.md.

The read-only focus-state owner performs one native focused-window read and
constant scalar projection, with no enumeration or retained allocation. Its
nonmutation regression admits at most 64 existing windows (current witness 15),
reads two native inventories, retains at most 128 object references and permits
at most 8192 conservative equivalence comparisons. It creates no windows and
leaves focus, layout, Play Mode and all project/global preferences unchanged.
PASS for this frozen focus-state regression; existing gameplay and localization
step budgets are unchanged.
