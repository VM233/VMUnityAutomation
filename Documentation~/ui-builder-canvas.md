# UI Builder canvas preview

`uitoolkit/builder-preview` loads the requested imported UXML through one exact
native UI Builder. The catalog owns the current input/output schema and effects.

## Document and window ownership

`instanceId` selects an existing exact Builder. Without it, the command reuses
the unique native Builder or creates one when none exists. Multiple Builders
require an identity from `uitoolkit/windows`. Every preview calls the native
document loader, including the same imported path after reimport. Path equality,
a nonblank screenshot or an existing editable graph cannot establish adoption.

Any unsaved document is rejected before loading. The command never saves,
discards or confirms a native dialog. `documentAdoption` retains the requested
asset and exact before/after document identity, path, GUID and native load result.
Unity2021.3 exposes a void LoadDocument method, so nativeLoadReturned is null;
Unity2022.1 onward exposes its actual boolean result. A compile-time version
branch selects that ABI. Missing APIs and rejected or changed documents fail
without another window, loader or capture surface.

Use `closeCreatedWindow=true` for an ephemeral authoring review. The request's
one terminal boundary retires update/reload/quitting subscriptions, its newly
created clean Builder and the original existing focus. `windowRetirement`
records the exact outcome. Reused windows remain open. A newly created dirty
window remains open and fails, preserving its unsaved document. The typed
`editor/close-window` command shares the native dirty-state/Close owner for later
explicit closure. See [Editor window lifecycle](editor-window-lifecycle.md).

`editor/ui-builder-document-state` reads an exact existing native Builder without
adopting an asset. Its typed snapshot reports window/document identity, asset
path and separate document/window unsaved flags. A generic Unity object dirty
flag or a window title does not establish either native unsaved state. Use the
catalog's current contract before a restart or other operation that must preserve
an unrelated document. Missing identities and foreign window types fail without
opening, focusing, loading, saving or closing anything. The read owns no session
or callback and does not grant permission to discard a dirty document.

## Canvas, framing and actual samples

Supply `themeStyleSheetPath` when the game uses a custom ThemeStyleSheet.
The native Builder toolbar applies that imported asset in initialization mode,
without writing a user or project theme override. The actual document theme and
the canvas root's stylesheet membership must both agree before accepting layout
or pixels. Missing assets reject before opening a window; changed native themes
invalidate the preview. `themePreview` records the actual publication and paired
restoration of the previous theme and the requested asset's native default flag.
Every terminal path restores that state before window retirement, including
capture failure, timeout, reload and shutdown. Omission keeps the current theme.
Canvas fit or nonblank pixels alone do not establish theme fidelity.

Prefer `panelSettingsPath` when matching a runtime panel. This exclusive option
reads the actual theme and referenceSpritePixelsPerUnit from that imported
PanelSettings asset. The native Builder panel's Sprite units are applied once,
verified with the theme, and restored at the same terminal boundary. This does
not change DPI, viewport zoom, source assets or user/project preferences. A
runtime panel using32 Sprite pixels per unit cannot be compared reliably against
an Editor canvas using100: native Sprite borders acquire an extra100/32 scale.
The receipt retains requested, previous and observed units plus restoration.

Supply `canvasWidth` and `canvasHeight` together with autoMatchGameView=false
for an explicit canvas. The native Match Game View toggle is cleared, then
normal native dimension ChangeEvents publish both requested values. Completion
reads them back. Missing/disabled controls and dimension mismatch fail. Omitting
dimensions retains automatic matching. No Game View is created or reopened.

Canvas changes and Fit framing are temporary. Before native document loading,
the request snapshots that asset's complete native settings and its exact
settings-file bytes in memory. A reused active document preserves its current
native settings as well. Every terminal path restores the actual bound native
settings, publishes them through the native viewport, restores the exact file
or its original absence, then retires the window. No backup file is created.
`canvasSettingsRestoration` distinguishes the active-document match, native
restoration and file restoration. A changed document is preserved; only the
request's original settings file is restored. Any restoration error fails the
request and remains explicit alongside an earlier failure.

`autoFrameViewport=true` sends one native NavigationSubmitEvent to Fit viewport.
Both actual document and viewport bounds must settle within one physical pixel
for stableFrames consecutive publications. The existing deadline includes native
document adoption. `requireContentFit` retains actual canvas and preview text
overlap checks. Requested zoom remains diagnostic: Unity exposes no stable
public zoom API.

For a sample inside a long or nested authoring ScrollView, supply its unique
authored `revealName`. Lookup stays inside the adopted DocumentRoot and rejects
missing/ambiguous names, more than2500 elements, more than64 ancestors, detached
or hidden targets, and targets larger than an actual ancestor viewport.
Each actual ancestor ScrollView.ScrollTo runs once, inner first, on its own
Editor publication. The next ancestor observes the already published inner
layout. Completion also requires settled target bounds wholly inside every
ancestor viewport. No element, root, source, clock or alternate UI is substituted.

`previewTarget` is null without a request. Otherwise its closed receipt retains
the exact name, ancestor count, completed scroll count and actual world bounds.
Those fields and the screenshot refer to the same adopted document/window.
A full-document fit alone does not prove visibility of an unrequested sample.

## Capture and validation

Capture uses the same native window reference and verifies document identity
before and after. The screenshot retains that window identity. Failure or
timeout gives no successful requested-document pixel conclusion. Capture mode
is chosen before execution and never retried on another surface.

Native ownership regressions cover dirty documents, same-path reimport and
publication-aware teardown. Their UnityTearDown joins the actual queued native
reimport before closing test-owned resources, then verifies that publication
does not recreate an orphan Builder. They never close a user's Builder.
Focused target tests use actual nested ScrollViews and reject missing,
ambiguous, hidden, oversized, detached and over-domain samples. Native game-host
integration must still prove the requested asset, sample, layout and pixels.

## Static cost ledger

Canvas restoration owns one native settings object snapshot and one file, each
bounded to16KiB, plus one round-trip verification snapshot. Peak scratch stays
below128KiB. Reflection binds a fixed set of native settings/document/viewport
members once per preview; restoration uses one native view publication and one
exact file write or deletion. There is no scan, retry, new timer, image or backup
file. The existing terminal boundary owns success, failure, timeout, reload and
shutdown restoration. Original/absent-file controls and a native-writer rejection
cover the new state contract; actual game-host previews must still prove native
adoption, complete settings restoration, layout and pixels.

The exact-document read adds one native identity resolution, one existing fixed
reflection binding, one document read and a five-field snapshot. It introduces
no scan, loop, callback, session, texture or retained state. Its negative controls
use one unshown foreign window, destroy only that fixture and preserve focus;
native integration reads an existing Builder without changing its document.

Existing preview inspection, native document binding and capture keep their
own bounds. Optional target navigation adds one bounded2500-element lookup,
at most64 ancestor references and ScrollTo calls, one per existing publication.
Each update adds at most64 clip tests and eight target-bound comparisons;
request scratch stays below8KiB and adds no image buffer or new timer/cache.
One fixed-shape receipt records the target. Existing request timeout and terminal
window/focus/reload retirement remain the sole lifecycle.

An optional theme scope adds one exact native toolbar binding and two theme/flag
snapshots per request, O(1) identity checks per existing publication, no theme
discovery scan, no new timers, image buffers or per-frame restyling. Supported
native enum identities are bound from the document property; the three-argument
toolbar initializer is the sole theme publication path.
An explicit runtime-panel preview adds one native panel property snapshot and
paired assignment, O(1) verification, no per-frame restyling or extra buffers.

Two focused fixtures run sequentially: one native panel with three nested
ScrollViews and one clipped target, and one native panel with invalid controls.
The detached2501-element rejecting control adds at most2MiB fixture scratch.
Native scroll publication is bounded64 updates plus16 visibility observations.
No gameplay clock, source save, scene, window fallback or timing gate changes.
