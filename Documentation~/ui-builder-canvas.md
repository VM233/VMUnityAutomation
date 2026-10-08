# UI Builder canvas preview

`uitoolkit/ui-builder-preview` opens the requested authoring host in UI Builder.
Use `canvasWidth` and `canvasHeight` together for an explicit design canvas;
values are integer pixels in the catalog's admitted range. `autoMatchGameView`
defaults to false when dimensions are supplied and must remain false for that
mode. Omitting dimensions retains the existing Match Game View option.

The command sends normal value changes to UI Builder's native `canvas-width`
and `canvas-height` controls. Those controls must be available and editable,
with Match Game View disabled. Missing controls produce
`ui_builder_canvas_controls_unavailable`. The result's actual
`canvasAdjustment.finalCanvasSize` must match the request; otherwise it fails
with `ui_builder_canvas_size_mismatch`. A single dimension or a simultaneous
Match Game View request is rejected before opening the asset. Preview readiness,
content fit, text overlap, viewport framing and capture keep their existing
completion contracts. No Game View is created or reopened.

Static Cost Ledger: two bounded query traversals of the existing authoring tree,
frozen at 2,500 controls (5,000 visits maximum), two native ChangeEvents and one
size readback. There is one adjustment per request and no new scan, cache or
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
