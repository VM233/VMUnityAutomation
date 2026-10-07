# Editor window capture

`screenshot/editor-window` resolves one existing EditorWindow. Its capture owner
selects the renderer before taking one image. In `auto` mode, a nonempty retained
UI tree or the Game View requires desktop composition. An IMGUI-only window uses
PrintWindow. `screen`, `print-window`, and `view` explicitly select the corresponding
surface. There is no retry with another surface after a failed capture.

`uitoolkit/builder-preview` accepts the same `captureMode` values and delegates
the selected surface to this owner. It defaults to `screen`. Explicit
`print-window` performs one native-host capture and runs the existing Builder
pixel analysis on that receipt. Capture failure still rejects the preview; a
mode choice does not waive target verification or authorize a second capture.

Builder surface selection adds one dictionary lookup and one fixed argument
field, O(1) time and space. It adds no native capture, pixel scan, frame wait,
traversal or retained state. The existing frozen 1497 by 880 Builder capture
budget remains unchanged; PASS for this increment.

## Direct Editor view capture

Explicit `view` captures the selected native GUIView's current render surface
with Unity's `GrabPixels(RenderTexture, Rect)` binding. This is a separate
capture surface selected before execution; `auto` remains unchanged and no
failed desktop or PrintWindow capture triggers it. The native host's
`actualView` must be the requested EditorWindow before a single immediate
repaint and one readback. Geometry records the EditorWindow and host view IDs,
local point-space rectangle, native backing scale and graphics UV origin.
Readback rows are normalized according to `SystemInfo.graphicsUVStartsAtTop`;
contentRect maps the actual root world bounds inside the host view, including
the native tab offset. It excludes OS chrome.
The previous selected tab and RenderTexture.active are restored in finally;
the temporary RenderTexture and Texture2D are released there too.

Unity exposes the binding in its [2021.3 reference source](https://github.com/Unity-Technologies/UnityCsReference/blob/2021.3/Editor/Mono/GUIView.bindings.cs)
and [6000.4 reference source](https://github.com/Unity-Technologies/UnityCsReference/blob/6000.4/Editor/Mono/GUIView.bindings.cs).
The package calls that owner rather than reconstructing UXML in a second panel.
A missing or mismatched host is `target_view_unverified`; rendering or readback
exceptions remain domain failures, with no alternate capture.

### Static Cost Ledger before implementation

The frozen BattleIdle witness is the same UI Builder host: at most 1496 by 879,
1,314,984 pixels (the native whole-window upper bound); the actual view is smaller.
One host lookup, one actualView identity comparison, one native repaint, one
GrabPixels and one synchronous ReadPixels occur on the Editor thread. No frame
poll, retry, traversal, scene object or retained cache is added. Image work is
O(width * height), using one 4-byte RenderTexture, one RGB Texture2D with at
most 6 bytes per pixel across CPU and GPU, one 3-byte raw copy and a PNG bounded
by 4 bytes per pixel plus 64 KiB. Top-origin backends swap half the RGB rows
using one 3 * width scratch row (4,488 bytes under the frozen bound) and at most
1.5 * width * height * 3 copied bytes. Peak is at most 22,424,752 bytes, below
24 MiB. Row normalization adds O(width * height) work without another readback.
The existing center sampler remains bounded by 128 * 128 sample positions.
Acceptance requires the same single Builder call and inspection of its PNG;
failure of that witness must remain a failure. PASS for this frozen increment.

The focused native readback witness owns one 160 by 120 EditorWindow with two
colored retained elements and one PNG. Its two stimuli change the same native
panel from red to green. Each public invocation takes one image and samples
two interior pixels; no asset scan, authored asset or scene is involved.
Each image uses at most 391,936 bytes under the same formula; all test-owned
objects and files retire in finally. PASS.

The UI tree belongs to the selected window. It is read at capture time and is not
cached or inferred from a tab title. This covers the retained Hierarchy in Unity
6000.6 as well as custom UI Toolkit windows. The existing capture owner raises
the native host for desktop composition, restores foreground and topmost state,
restores the selected tab, releases all native image handles, and publishes one
PNG and its geometry and pixel-analysis metadata. A desktop-composition capture
is published only when the exact native target is foreground both immediately
before and immediately after the pixel copy. A locked session, blocked focus
transition, or intervening foreground change returns `target_window_unverified`
and discards the pixels. Successful receipts expose `targetWindowVerified=true`.
The caller must inspect the image. `centerVisuallyBlank` describes pixels and is
not visual acceptance.

The public screenshot contract advertises `target_window_unverified` and
`blank_capture`. UI Builder preview consumes the same verification receipt and
preserves a native capture rejection in its domain error. Missing evidence
cannot establish document blankness.

The public contract declares file writes and Editor view changes, requires an
exact project binding, exposes all four capture modes, and describes the actual
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

Physical-coordinate readback in a per-monitor-aware context matches the Unity
capture receipt exactly. The earlier desktop-height difference is DPI
virtualization and does not establish a crop defect. Source inspection identifies
a separate ordering error: the selected view repaints while its native host can
still be occluded, and the host is raised only afterward. The capture transaction
must raise the host, repaint the selected view, flush composition, then read its
pixels. The previous focused tab and native window state retire in `finally`.
The change moves the existing single repaint and composition flush. It adds no
frame delay, retry, extra capture, traversal or allocation. The existing bounds
above remain unchanged. PASS for the ordering change, subject to the same frozen
Hierarchy and Console integration witnesses.

The 0.6.12 public replay produced a readable 419 by 548 Hierarchy image through
automatic desktop capture, and readable 1351 by 333 Console images through both
explicit surfaces. The files were inspected. The empty Console body correctly
has a single center color bucket while its tab and toolbar remain visible, so
that pixel statistic must not be treated as an error. The four schema, mode and
effect tests passed in workflow `2a73d4b07f92`. Workspace adoption reported zero
errors and warnings, a completed reload and all 161 expected assembly terminals.
The Editor process had restarted between the earlier failure and this replay.
These results establish the current end-to-end capture path, without isolating
the repaint order from that environmental change as the sole cause of the old
black image. No additional capture or fallback was added for the replay.

The 0.6.24 regression witness is a locked Windows session where the UI Builder
native window remained visible and non-minimized, `SetForegroundWindow` failed,
and desktop capture returned unrelated lock-screen wallpaper. Color complexity
then let the UI Builder analyzer report a false visual pass. Exact foreground
verification before and after the single existing BitBlt rejects that evidence
without adding a capture, retry, image buffer, pixel scan, or retained state.
The check is two constant-time native handle reads and comparisons per screen
capture, with zero gameplay-frame work. UI Builder also requires the verified
receipt before decoding or analyzing the PNG. PASS.

## Foreground rejection observation

A rejected capture must identify the foreground window actually compared with
the target. `captureGeometry.foregroundBeforeCapture` records the native handle,
process ID and a title bounded to 511 characters immediately before the pixel
copy. Screen captures that reach the copy also report `foregroundAfterCapture`.
PrintWindow captures omit these observations. The same sampled handle drives
verification; observation never substitutes a different window or renderer.

The frozen BattleIdle witness is one floating UI Builder host, 1497 by 880,
with `target_window_unverified` and `SetForegroundWindow failed`. Its current
receipt contains the target but cannot attribute the foreground mismatch.
Observation reuses the two existing handle samples and adds at most four native
reads and two bounded title buffers per request. The increment is O(1), under
8 KiB of managed observation data, no retained allocation, no extra capture,
pixel loop, retry or gameplay-frame work. The strict comparison and resource
cleanup remain unchanged. PASS for the observation increment.

Acceptance uses the existing capture contract fixture and repeats that exact
public UI Builder capture. If Windows still refuses the transition, the
observed identity establishes the rejection; it does not establish visual
acceptance or authorize relaxing the foreground requirement.
