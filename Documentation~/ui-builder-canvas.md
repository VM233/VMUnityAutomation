# UI Builder canvas preview

`uitoolkit/builder-preview` adopts the requested authoring host in one native UI Builder.
It binds the actual window once. `instanceId` selects an existing exact Builder;
without it, the command reuses the unique native Builder or creates one only if
none exists. Multiple native Builders require an identity from `uitoolkit/windows`.
A custom window titled UI Builder cannot become this document owner.

Owned preview retirement Static Cost Ledger (2026-10-25): optional
`closeCreatedWindow` adds one exact window retirement at the existing terminal
request boundary. It reads the same bound native document once, preserves any
unsaved changes, and calls Close once only for the window this request created.
Existing windows remain open. No second selection, traversal, loop, polling,
cache, Game View, source save/discard or native dialog confirmation is added.
One fixed-shape retirement product records the original window identity,
mutually exclusive outcome and any actual failure. Existing update/reload
subscriptions and original focus retire in the same Finish boundary; Editor
quitting uses it too. Four actual game hosts use sequential native previews,
at most one extra Builder and the existing bounded capture allocation at a time.
The focused ownership fixture retains its two existing assets/one native window,
adds clean-created close, existing-window preservation, dirty-created preservation
and already-closed outcomes; all paths close only test-owned resources in finally.
Schema checks use the same generated contract. PASS before executable writes;
native and formal integration outcomes remain to be qualified after adoption.

Set `closeCreatedWindow=true` for an ephemeral authoring review. After capture
or any failure, timeout, reload or Editor shutdown, the same request owner
retires its newly created native Builder and restores the previous focus.
`windowRetirement` records the original window identity, its exact outcome and
any retirement error. Adoption, layout, window and screenshot fields describe
the observed preview before retirement. A reused window stays open. If a newly
created window has unsaved changes, it remains open and the request fails
without saving, discarding or confirming a dialog. An existing preview failure
keeps its original error; the retirement product retains the additional failure.
The default is false for interactive previews that should remain open.

The existing typed `editor/close-window` tool can close an exact existing Builder
identified by `windowInstanceId` from `uitoolkit/windows`. Its native document
unsaved-state and Close owner is shared with preview retirement. It never selects
another window, opens a window, changes focus, saves or discards a document.
Dirty windows return `editor_window_unsaved` and stay open; successful closure
retains its existing typed `Closed` result. See [Editor window lifecycle](editor-window-lifecycle.md).

`opened` means verified document adoption. `documentAdoption` records the exact
window, requested asset identity and before/after document identities, paths,
GUIDs and unsaved state. `loadAttempted` records the actual native call. From
Unity 2022.1, `nativeLoadReturned` is its actual boolean result. Unity 2021.3
exposes a void method, so this field remains null after that call; adoption
still requires the same native document, requested asset and path. Every preview
loads the requested imported asset through that native owner, including when its
path, GUID and instance already match. Those identities do not prove that an
existing editable Builder graph adopted a subsequent reimport. Any unsaved
document, including the same requested path, is rejected before loading; the
command never saves, discards or confirms a modal dialog. Successful adoption
therefore has `loadAttempted=true`; preflight rejection retains false/null.

Unity's generic OpenAsset callback can return true after its native document
load returned false. The command therefore uses the actual native load once,
checks the exact adopted document and, where the native ABI provides one, its
boolean return value. It exposes rejection at that boundary. Rejection does not wait for canvas readiness or capture an old
document. Missing native members return `ui_builder_api_unavailable`; the package's
Unity 2021.3.18f1 minimum remains unchanged. Both native return signatures were
verified in the exact official 2021.3.18f1, 2022.1.0f1 and 2022.2.0f1 release
assemblies. `UNITY_2022_1_OR_NEWER` selects one return ABI at compile time;
the `UnityEditor.UIBuilderModule` assembly identity is shared. There is no
runtime version/assembly alias search or retry path.

Layout and pixels use the same bound window and native document. A destroyed
window or changed document fails; no replacement is selected. Capture accepts
the same actual window reference, checks document identity before and after,
and publishes `screenshot.windowInstanceId`. The timeout includes native window
and document adoption. A ready-layout timeout reports the observed update count
and consecutive stable ready updates; successful adoption alone does not imply
that these layout requirements completed. A timeout produces no requested-
document pixel conclusion. The update and assembly-reload subscriptions close
through this same request owner. Focus returns to the previous existing window.

Use `canvasWidth` and `canvasHeight` together for an explicit design canvas;
values are integer pixels in the catalog's admitted range. `autoMatchGameView`
defaults to false when dimensions are supplied and must remain false for that
mode. Omitting dimensions retains the existing Match Game View option.

The command first clears UI Builder's existing `match-game-view` toggle through
its native ChangeEvent, then sends normal value changes to `canvas-width` and
`canvas-height`. The dimension controls must become editable. Missing controls
or controls that remain disabled produce
`ui_builder_canvas_controls_unavailable`. The result's actual
`canvasAdjustment.finalCanvasSize` must match the request; otherwise it fails
with `ui_builder_canvas_size_mismatch`. A single dimension or a simultaneous
Match Game View request is rejected before opening the asset. Preview readiness,
content fit, text overlap, viewport framing and capture keep their existing
completion contracts. No Game View is created or reopened.

Automatic matching uses the same native toggle with a true value. No reflected
document-settings fallback or Game View creation is involved. Explicit-size
completion reads back both the disabled matching mode and the actual dimensions.

Same-path adoption repair Static Cost Ledger: one native LoadDocument per preview,
after one unchanged dirty-state preflight; there is no source-hash cache, extra
window, loop, timer or retry. The focused ownership fixture keeps its two assets
and one native Builder, adds one synchronous reimport and one same-path load,
and rejects the same dirty document without a native call. Its four total native
loads and two direct named-element queries remain sequential in the existing
2,500-control domain (at most5,000 additional query visits); source content is
below2KiB, with no additional live asset/window peak. Budget Pass. The formal
reimport-to-same-path preview and actual element/style readback qualify adoption;
path equality and screenshot nonblank analysis alone do not.

Static Cost Ledger: three bounded query traversals of the existing authoring tree,
frozen at 2,500 controls (7,500 visits maximum), at most three native ChangeEvents
and one size readback. There is one adjustment per request and no new scan, cache or
retry loop. Dimensions are metadata on the native canvas, not an allocated image;
window capture retains its existing limits. The original clipped character host,
requested dimensions and actual native readback are the integration witness.
Admission cases cover the incomplete pair and mutually exclusive modes. PASS
within the existing serial Editor-thread and preview request budgets.

The native ChangeEvent regression attaches its three controls to one test-owned
Editor panel before stimulation and closes it in finally. Detached fields do not
dispatch value changes and cannot represent UI Builder's production lifecycle.
Earlier dimension-control witnesses are separate from document adoption.
The current DD2 Settings request failed with the Notebook still active; it remains
unverified until an exact bound-document replay succeeds.

Fit viewport receives one native NavigationSubmitEvent. The previous pointer
pair did not activate the control in the frozen Editor witness: zoom stayed at
80 percent and document bounds exceeded the viewport while framing was reported
true. The new completion check compares the actual document and viewport bounds
with at most one physical pixel of rounding tolerance. It reports
`ui_builder_viewport_clipped` when the document remains outside. This adds one
pooled native event and eight scalar comparisons; no new traversal or retry axis.

Readiness includes the native layout publication after Fit. Document and viewport
bounds must both remain within one physical pixel of their previous observation
for `stableFrames` consecutive updates, and requested framing must already fit.
Loaded UXML alone cannot finish the request while the viewport is still moving.
The original portrait Quick Actions witness was captured after 175.82 ms with
its bottom at 946.7533 outside the viewport bottom of 819; the same native Fit
request is the integration regression. No extra activation, delay or alternate
capture surface is used.

Static Cost Ledger: the existing bounded preview request performs at most 16
additional scalar comparisons per native update, keeps two Rect values for its
own lifetime, and adds no loop, scan or allocation. Four focused geometry cases
use constant inputs. PASS within the existing timeout and Editor-thread budget.

The document-identity regression uses two imported test-owned UXML assets and one
test-owned native Builder. It checks normal adoption, refusal before a dirty
document is switched, preserved dirty state, a later clean adoption, and rejection
after the actual document changes. It is ignored when any user Builder already
exists. An ignored or compiled test is not native runtime acceptance.

The native reimport queues an Editor publication. The ownership fixture observes
an acknowledgement queued after that native callback before continuing adoption.
UnityTearDown owns asynchronous resource retirement, including a test that ends
before the queued reimport has published. It joins that publication before close,
then checks the next native publication for orphan windows. A single yielded update
does not itself prove this causal boundary. Closing synchronously before the queued
native reload can create a replacement Builder in Unity6000.6.

Static Cost Ledger for that correction: the same two assets below2KiB, four loads
and one Builder remain the peak. Two publication boundaries add no polling or
duration; two native window censuses over the observed16-window domain require at
most64 scalar/type comparisons and4KiB evidence. The exact-identity close route
uses the existing typed Editor-close route, shares the native dirty-state/Close owner with preview retirement, performs one
read and at most one Close, and does not create or choose a window. Two selector
passes over16 windows, one type binding and a fixed three-field product remain
below4KiB scratch on the Editor thread. PASS before executable changes.

Asynchronous fixture retirement Static Cost Ledger (2026-10-25): retain the original
two-asset/four-load case and add one real one-asset/one-load early-exit case. The
fixtures are sequential, with at most one Builder, two assets below2KiB and2500
controls alive. One acknowledged native delayCall is owned at a time. At most five
joins including an interrupted body's pending join, each<=240 Editor updates, plus
terminal checks give<=1280 scalar checks. At most six16-window censuses add<=192
scalar/type comparisons. At most12 trace lines of512 characters give6KiB evidence
and retained fixture scratch stays below8KiB. Unity's actual async TearDown follows
inner test execution and joins the native producer; no time delay, log filtering,
alternate transport or retry is introduced. PASS before executable changes.

Incremental static cost: two linear selector passes over one live-window snapshot
(19-window DD2 witness: at most 228 string tests), one exact native type binding,
two native method bindings and four property bindings per request, at most one
ShowWindow and one native document Load, and constant before/after metadata. The
preview no longer rescans all EditorWindows or repaints unrelated runtime
UIDocuments on each update. Bound capture adds no image buffer, alternate capture
or window scan. The original layout deadline/geometry loops and actual native IO
latency remain separate qualification obligations; this delta is not a wall-clock
or whole-preview runtime certificate. The frozen DD2 Settings failure is retained
as failure until a fresh official-path replay produces matching adoption and pixels.
