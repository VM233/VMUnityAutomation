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
