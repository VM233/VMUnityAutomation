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

## Canvas, framing and actual samples

Supply `canvasWidth` and `canvasHeight` together with autoMatchGameView=false
for an explicit canvas. The native Match Game View toggle is cleared, then
normal native dimension ChangeEvents publish both requested values. Completion
reads them back. Missing/disabled controls and dimension mismatch fail. Omitting
dimensions retains automatic matching. No Game View is created or reopened.

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

Existing preview inspection, native document binding and capture keep their
own bounds. Optional target navigation adds one bounded2500-element lookup,
at most64 ancestor references and ScrollTo calls, one per existing publication.
Each update adds at most64 clip tests and eight target-bound comparisons;
request scratch stays below8KiB and adds no image buffer or new timer/cache.
One fixed-shape receipt records the target. Existing request timeout and terminal
window/focus/reload retirement remain the sole lifecycle.

Two focused fixtures run sequentially: one native panel with three nested
ScrollViews and one clipped target, and one native panel with invalid controls.
The detached2501-element rejecting control adds at most2MiB fixture scratch.
Native scroll publication is bounded64 updates plus16 visibility observations.
No gameplay clock, source save, scene, window fallback or timing gate changes.
