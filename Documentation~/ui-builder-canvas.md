# UI Builder canvas preview

`uitoolkit/builder-preview` opens the requested authoring host in UI Builder.
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

Static Cost Ledger: three bounded query traversals of the existing authoring tree,
frozen at 2,500 controls (7,500 visits maximum), at most three native ChangeEvents
and one size readback. There is one adjustment per request and no new scan, cache or
retry loop. Dimensions are metadata on the native canvas, not an allocated image;
window capture retains its existing limits. The original clipped character host,
requested dimensions and actual native readback are the integration witness.
Admission cases cover the incomplete pair and mutually exclusive modes. PASS
within the existing serial Editor-thread and preview request budgets.

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
