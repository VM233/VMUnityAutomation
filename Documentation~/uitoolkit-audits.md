# UI Toolkit authoring audits

Discover the current `uitoolkit/audit-uxml-layout` and
`uitoolkit/audit-uss-styles` contracts through `vm_catalog_get`. Pass the exact
edited files as `paths`, include their authoring and runtime consumer roots,
and use `runSelfTests=true` for a release gate. A finding at either warning or
error severity makes `passed=false`; inspect the structured `kind`, location,
and message rather than treating command transport success as an audit pass.

## Composite icon buttons

`missing-composite-button-feedback` reports a transparent `Button` with no
own text and at least one visual child when its UXML has no distinct visible
`:hover` or `:active` style. Both pointer states need their own feedback.
The audit does not count a declaration that repeats the normal state, repeats
the hover state for `:active`, or is overridden by an inline declaration.
Use a property such as `opacity` for hover and `scale` for press when the
button's background is authored inline.

## Content-sized labels

`fixed-content-label-height` reports a positive pixel `height` on an authored
`Label` in normal flow when the label has no independent visual, clipping, or
interaction box. It examines inline declarations and authored USS. Localized,
runtime-bound, and invariant text all need room to determine their own height.

Remove `height` for natural sizing. Use margin or padding for spacing, or
`min-height` if the text must be able to grow beyond a minimum. A measured fixed
visual, clipping, or interaction region may be documented immediately before
the label with a reasoned suppression:

```xml
<!-- uxml-layout-audit: allow-fixed-content-label-height Fixed interaction region measured against the artwork. -->
<ui:Label name="ArtworkCaption" style="height: 38px;" />
```

The route does not flag absolute-positioned overlay labels or labels with an
authored visual or clipping box. The suppression appears in the report when
`includeSuppressed=true`.
