# UI interaction structure

The existing `uitoolkit/audit-uxml-layout` owner also checks declared interaction
structure. This is an authoring check, not a proof of callback effects or runtime
animation. It reports its scope/group/navigation/field counts separately; zero
declared scopes is `NotDeclared`, not interaction acceptance.

The design follows the [W3C tabs pattern](https://www.w3.org/WAI/ARIA/apg/patterns/tabs/)
and [W3C control grouping guidance](https://www.w3.org/WAI/tutorials/forms/grouping/).
Tabs label associated content with one active destination. Related controls need
an explicit common group. These relationships transfer to Unity; the tool does
not infer product meaning from translated labels, name suffixes or synonyms.

## Contract and automatic coverage

Put a `ui-structure:` JSON comment immediately before its actual UXML element.
The parser accepts only the documented properties for that role, rejects missing
or ambiguous targets and never suppresses structural errors.

- `scope`: bounds one task. `context` with `state` identifies a work area within
  it, such as an editor or model preview. Context names are local product data.
- `navigation`: requires `state`, `context`, `content` (the unique content name)
  and `selectedClass`. Its Button children use `tab` with a unique `target`
  referring to an actual `panel`. One state has one navigation owner per context,
  and one panel has one entry. Navigation and content must be adjacent siblings.
  The initial selected tab and visible panel must agree.
- For a real publisher that replaces one body rather than keeping static child
  panels, `navigation` additionally declares `contentMode: "publication"`. Each
  `tab` targets that same actual content element and adds a distinct `value`
  identifying the section it publishes. That content declares `publication`
  with `state`, `context`, initial `value` and the actual publisher's `owner`.
  Exactly one selected tab must match the initial value. The body must be visible
  and immediately follow its navigation in the same parent/context. A second
  navigation cannot own that body even under a renamed state. Static panel tabs
  cannot use publication values. The report exposes `publicationNavigations`
  and `publicationDestinations` alongside the existing navigation count.
- `choice`, `inspection`, `pose`, `actions`: require `state` and `context`.
  A group's nearest context must match; a navigation tab cannot be hidden inside
  a different group role. New multi-button groups inside the scope must declare
  their responsibility, so inserting a second unclassified row fails by default.
- `field`: groups one native or derived Slider/SliderInt. Its text elements use
  `caption`, optional `value`, or `help`. Caption and numeric value belong to a
  common row header immediately above the input. This compact-field contract
  detects a reading appended below a slider or split into another container.
  New sliders in the scope must have such a field or use their native label.

Example (names and selected class are illustrative, not hardcoded by the tool):

```xml
<!-- ui-structure: {"role":"scope"} -->
<ui:VisualElement name="Editor">
  <!-- ui-structure: {"role":"context","state":"editing"} -->
  <ui:VisualElement>
    <!-- ui-structure: {"role":"navigation","state":"section","context":"editing","content":"Pages","selectedClass":"selected"} -->
    <ui:VisualElement>
      <!-- ui-structure: {"role":"tab","target":"BodyPage"} -->
      <ui:Button class="selected" text="Body"/>
      <!-- ui-structure: {"role":"tab","target":"FacePage"} -->
      <ui:Button text="Face"/>
    </ui:VisualElement>
    <ui:VisualElement name="Pages">
      <!-- ui-structure: {"role":"panel"} -->
      <ui:VisualElement name="BodyPage"/>
      <!-- ui-structure: {"role":"panel"} -->
      <ui:VisualElement name="FacePage" style="display: none;"/>
    </ui:VisualElement>
  </ui:VisualElement>
</ui:VisualElement>
```

The shared-publication form uses the real body, without manufacturing one panel
per destination:

```xml
<!-- Within a declared scope and context whose state is "notebook". -->
<!-- ui-structure: {"role":"navigation","state":"section","context":"notebook","content":"Body","selectedClass":"selected","contentMode":"publication"} -->
<ui:VisualElement>
  <!-- ui-structure: {"role":"tab","target":"Body","value":"Skills"} -->
  <ui:Button class="selected" text="Skills"/>
  <!-- ui-structure: {"role":"tab","target":"Body","value":"Care"} -->
  <ui:Button text="Care"/>
</ui:VisualElement>
<!-- ui-structure: {"role":"publication","state":"section","context":"notebook","value":"Skills","owner":"NotebookPublisher.Render"} -->
<ui:VisualElement name="Body">
  <!-- The real host's representative generated-content preview. -->
</ui:VisualElement>
```

`owner` identifies the source to review; a comment cannot prove its callbacks or
rendered content. Independently verify that producer, each actual destination,
selection independence, interruption and retirement through the real controls.
This mode does not claim ARIA conformance or runtime acceptance from metadata.

Use `requiredInteractionScopes` in
`ProjectSettings/VMUnityAutomationUIToolkitAudit.json` for persistent coverage:
each string is an `Assets/...uxml#UniqueElementName` identity. Removing its scope
marker, renaming it or making it ambiguous then fails normal CLI and import
audits. `automaticAudit.uxmlLayoutContracts` uses the same owner on changed UXML.
Configuration defaults and all other existing audit settings remain unchanged.

Inspect actual producers and test selection independence separately. Declarations
do not establish which state a callback writes. In particular, selecting a camera
inspection must preserve an unrelated editing section unless the product defines
that dependency. Test this through native controls when runtime work is allowed;
report it untested when the user excludes runtime acceptance.

## Bounded implementation and acceptance

Before implementation: freeze each document to at most 16384 XML elements,
128 ancestry levels, 1024 contracts and 128 distinct scoped XML component types.
Use one preorder index for scope/context/group/field ownership and one reverse
pass for unclassified button groups. Names, state owners and target entries use
indexed dictionaries; no pairwise group or whole-source searches. Native type
identity reuses the existing component identity cache, once per XML type. At most
16384 type-cache reads, 1024 contract parses and 16384 child-edge visits per pass;
ancestry/order checks are bounded by 128 per declared relation (131072 visits).
Two property reads per contract use a single-property stylesheet lookup, bounded
by 4096 indexed rules: at most 8388608 rule/property probes. Existing XML parsing
and index construction remain owned by the parent audit. Additional retained
state is budgeted below 32 MiB (16384 node records and indexes, at most 4 MiB of
contract text); no source text or style declaration cross-product is copied.
No asset loads, frame callbacks, native layouts, new workers or retained document
caches are added. Invocation-local arrays/dictionaries retire at return. Budget
exhaustion is an error, never a partial pass. Verdict: pass for these fixed axes.

Shared publication retains those bounds. At most1024 declared tab contracts add
one invocation-local set entry, one indexed body lookup and one selection lookup
each. Each publication adds one display-property lookup under the existing
4096-rule bound. Additional retained state is below256KiB within the32MiB ledger;
no document/source cross-product or new lifetime is introduced. Fourteen matched
controls retain static-panel behavior and reject duplicate values/owners, wrong
targets, missing publishers, context/state mismatch, hidden/detached bodies,
invalid selection and mixed modes; independent contexts reuse values legally.

Success requires matched negative controls for duplicate navigation, duplicate
targets, missing roles, detached content, mixed contexts, invalid selection and
split numeric fields, plus legitimate independent contexts and native/derived
controls. Replay the real corrected host through the official executor after
package adoption. A missed original witness or a false positive on a legitimate
independent group rejects this method; diagnose that cause before adding rules
or changing bounds. Static success cannot close visual/input acceptance.

The first offline control run retained one missed wrapper-as-reading witness:
the contract accepted a container with a nested Label as the reading itself.
Unity's [TextElement contract](https://docs.unity.com/en-us/engine/6000.0/script-reference/unityengine/uielements/textelement)
provides the actual text identity. Require caption/value/help roles to identify
that native type; do not infer them from a descendant or label string. Structural
type lookup now reads cached type metadata without constructing controls. The
ordinary class-style baseline remains its separate existing consumer; creating
native controls in an offline CLR is not a valid native UI test environment.

The warning-enabled package compile also exposed five deprecated call sites in
existing infrastructure. Unity's [6.6 source](https://github.com/Unity-Technologies/UnityCsReference/blob/6000.6/Modules/AssetDatabase/Editor/ScriptBindings/AssetDatabase.bindings.cs)
proves that the new export/import calls are the previous wrappers' exact native
destinations, including the empty organization identity. Older supported Editors
retain their version branch. The existing resolution-session retirement owner
now receives the documented build-target callback interface. Unity 6.6 no longer
subscribes to its deprecated assembly-start event: started arrays honestly remain
empty, while global lifecycle, all expected terminal assemblies, diagnostics and
reload remain mandatory. No start timestamps/counts are fabricated from finish.
