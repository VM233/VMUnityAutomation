# Editor window capture

`screenshot/editor-window` resolves one existing EditorWindow. Its capture owner
selects the renderer before taking one image. In `auto` mode, a nonempty retained
UI tree or the Game View requires desktop composition. An IMGUI-only window uses
PrintWindow. `screen` and `print-window` explicitly select the corresponding
surface. There is no retry with another surface after a failed capture.

The UI tree belongs to the selected window. It is read at capture time and is not
cached or inferred from a tab title. This covers the retained Hierarchy in Unity
6000.6 as well as custom UI Toolkit windows. The existing capture owner raises
the native host for desktop composition, restores foreground and topmost state,
restores the selected tab, releases all native image handles, and publishes one
PNG and its geometry and pixel-analysis metadata. The caller must inspect the
image. `centerVisuallyBlank` describes pixels and is not visual acceptance.

The public contract declares file writes and Editor view changes, requires an
exact project binding, exposes all three capture modes, and describes the actual
Windows success result. Unsupported platforms remain an explicit domain error.

## Static Cost Ledger before implementation

The frozen witness is the existing Main Hierarchy window: five root children,
419 by 548 output pixels, 229,612 pixels total. The old automatic selection
returned a white PrintWindow PNG with one center color bucket. Only renderer
selection and its public contract change. Selection performs one root child-count
read and one type comparison. It adds no traversal, allocation, cache, polling,
capture, pixel loop, or retry to the existing single-capture lifecycle. Time and
space are O(1), with zero retained allocation and zero gameplay-frame work. PASS.

The four focused contract tests use at most one undisplayed EditorWindow and one
VisualElement at a time. They inspect the fixed 14-field result contract and
three mode values. They do not render, scan Assets, create scene objects, or use
Undo. The window is destroyed in `finally`. Test-owned live objects are bounded
by two, and each tested selection adds at most two property reads. PASS.

Integration acceptance repeats the original public capture, checks the
selected method and complete response schema, inspects the actual PNG, and
checks that the previously focused tab and scene state remain unchanged.

The first integration attempt at revision e8405a0 returned an all-black desktop
capture. A frozen Console control also returned all-black desktop pixels and a
white PrintWindow image. The retained-content selection therefore does not yet
establish working capture. Windows reports the Default input desktop, visible
non-minimized uncloaked hosts, no display affinity exclusion, and a 4480 by 1080
virtual desktop. Capture failures must publish the coordinates actually used by
the Unity process before further attribution.

The capture-geometry observation adds seven fixed fields: native host identity,
process identity, native window bounds, crop bounds, virtual desktop bounds,
Editor panel bounds and DPI scale. All are constant-count property or native
reads, with no additional image capture, pixel scan or retained state. Peak
additional managed geometry is under 4 KiB per call and zero per-frame work.
The existing frozen native host is 1936 by 1048, or 2,028,928 pixels, with one
8,115,712-byte GDI bitmap. The Hierarchy crop is 918,448 bytes. Observation does
not change these image allocations. PASS for the observation increment.
