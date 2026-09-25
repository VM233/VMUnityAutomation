# UI Toolkit authoring audits

Discover the current `uitoolkit/audit-uxml-layout` and
`uitoolkit/audit-uss-styles` contracts through `vm_catalog_get`. Pass the exact
edited files as `paths`, include their authoring and runtime consumer roots,
and use `runSelfTests=true` for a release gate. A finding at either warning or
error severity makes `passed=false`; inspect the structured `kind`, location,
and message rather than treating command transport success as an audit pass.

## Default theme imports

`duplicate-theme-stylesheet` is an error when a UXML `<Style>` loads a USS
already imported by the project default `.tss`, including through nested
`@import` statements. Keep the shared style in the theme or in the UXML, with
one owner for each loaded stylesheet. The automatic audit checks the full UXML
contract for changed UXML files and rechecks the theme stylesheet graph when a
USS or `.tss` changes. Open an edited USS in its actual host UXML in UI Builder
and check the Console as part of visual validation.

## Required UI Builder previews

The UXML layout audit automatically links each UI Prefab's `UIDocument` to its
serialized entry/template generators, slot distributors, and RenderTexture
targets. It reports `missing-generated-ui-builder-preview` when a visible
generated host has no authored content (or no authored image for image targets).
It skips hosts hidden by an authored `display: none` ancestor. This discovery
does not require a per-page container list, a UXML marker, or an existing
preview sample. The automatic audit runs after UXML and UI Prefab imports;
Prefab changes rescan all indexed UXML documents because they can change the
set of generated hosts.

Keep runtime-replaced samples in the actual host UXML and verify that the
producer clears them before binding live data. Open the host in UI Builder to
confirm the sample renders; the static audit establishes authored presence,
not pixel visibility or resolved image content.

Mark a host container whose design preview must display images with a comment
immediately before it:

```xml
<!-- ui-builder-preview: runtime-replaced required-images=1 by CreaturePreviewModifier.OnInitialize -->
<ui:VisualElement name="CreaturePreview">
    <ui:VisualElement class="ui-builder-preview-content"
        style="background-image: url(&quot;project://database/Assets/...&quot;);"/>
</ui:VisualElement>
```

`required-images` is a positive count of preview descendants with the exact
`ui-builder-preview-content` class and an inline `background-image: url(...)`.
`missing-ui-builder-preview-image` is an error if the following element is
absent or its count is too low. The count is deliberately about authored
images; inspect the actual UI Builder host to confirm assets resolve and remain
visible. The runtime owner named in the marker must clear preview elements
before binding authoritative content. Ordinary decoration and runtime images
do not satisfy this design-time contract.

For a stricter sample-count requirement beyond automatic host discovery, add
an independent project setting:

```json
"requiredBuilderPreviews": [
  {
    "path": "Assets/UI/Creature Details.uxml",
    "elementName": "CreaturePreview",
    "minImages": 1
  }
]
```

Place it in `ProjectSettings/VMUnityAutomationUIToolkitAudit.json`. The UXML
audit and automatic imported-asset audit then require exactly one named target
in that file and at least `minImages` authored preview images beneath it. A
missing target fails with `missing-ui-builder-preview-target`; missing images
fail with `missing-ui-builder-preview-image`. The configured requirement is
checked even when its UXML comment is removed.

Text generated at runtime needs its own design-time samples. Put each sample
inside an element with the exact `ui-builder-preview-content` class. A sample
counts as one text entry when it contains a `Label` with authored nonblank
`text`, or a template `Instance` with a nonblank `text` AttributeOverride. For
example, a property list can preview its actual row template:

```xml
<!-- ui-builder-preview: runtime-replaced required-text-entries=1 by DetailsPanel.OnGenerateVisualElement -->
<ui:VisualElement name="Properties">
    <ui:Instance template="PropertyRow" class="ui-builder-preview-content">
        <AttributeOverrides element-name="Title" text="Health"/>
        <AttributeOverrides element-name="Content" text="20/20"/>
    </ui:Instance>
</ui:VisualElement>
```

Set `minTextEntries` on the same `requiredBuilderPreviews` target to keep the
requirement independent of the inline marker. Both `minImages` and
`minTextEntries` may be present, and at least one must be positive.
`missing-ui-builder-preview-text-entry` reports an empty or incomplete list;
an empty template instance does not count. The named runtime owner must remove
these sample elements before it creates real entries. Inspect the opened host
in UI Builder to confirm template styles, localization, and visibility.

## Shared text fonts

`shared-font-definition-reset` is an error when a USS rule resets
`-unity-font-definition` to `none` or `initial` on an authored text element and
another loaded rule supplies a concrete font. Remove the reset to inherit the
shared font, or assign an explicit replacement font asset. The audit follows
the winning USS declaration and ignores an element with an inline font override.
Compound selectors are matched against all of their class and ID tokens.

## Programmatic visibility

`programmatic-display-class` is an error when a USS class exists solely to
switch `display`. This includes authored `hidden`/`visible` and `show-*`/`hide-*`
classes, plus classes referenced by runtime class APIs that own no other style.
Author the initial `display` on the UXML element and set that element's
`style.display` from its runtime owner. A state class that also owns visual
properties, such as selection color and size, remains a valid style contract.

## Button pointer feedback

`missing-button-press-feedback` reports any authored `Button` with visible
custom `:hover` styling but no distinct visible `:active` styling. This also
covers opaque text buttons such as pagination arrows. Buttons with no custom
hover styling can use their Unity theme states.

`missing-composite-button-feedback` reports a transparent `Button` with no
own text and at least one visual child when its UXML has no distinct visible
`:hover` or `:active` style. Both pointer states need their own feedback.
The audit does not count a declaration that repeats the normal state, repeats
the hover state for `:active`, or is overridden by an inline declaration.
Use a property such as `opacity` for hover and `scale` for press when the
button's background is authored inline.

## Panel UXML boundaries

`nested-ui-panel-uxml` is an error when a UXML used as a prefab UIDocument
imports another UIDocument panel's UXML. The audit follows template imports
through reusable UXML files, including unused declarations, so an intermediate
template does not hide a cross-panel reference. Reusable card and entry
templates remain valid. Give each panel its own UIDocument and use the panel
manager to open it. This project boundary check runs across indexed panel roots
even when `paths` narrows the other layout checks.

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
