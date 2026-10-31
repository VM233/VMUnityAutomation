# Runtime player UI review

## Explicit visual geometry

Set `GeometryOnly=true` for a dedicated declared-geometry audit of a component in a layered screen. It requires nonempty geometry contracts only and publishes `MeasurementScope=DeclaredGeometry`, zero hit/text measurements and no semantic coverage. Its Passed result qualifies those relations only. The default `Presentation` scope retains the full native experience audit, including genuinely or intentionally covered controls; do not present a narrow geometry report as full-screen acceptance.

Each contract declares its physical tolerance (`TolerancePixels`, default0.25px); it is converted through the native panel scale, independently of text measurement tolerance. Ancestor window/UI transforms are retained when comparing contact points. A rigid-control check rejects only the control's own transform.

The existing facade accepts `GeometryContracts` with exact native `ElementPath`, optional `ReferencePath`, `Owner` and `Kind`. `Anchors` compares transformed normalized anchors plus local-unit offsets; use it for joined frame strokes and exact gaps. `Insets` compares all four image-canvas corners with the declared left/top/right/bottom inset of its reference. `Untransformed` rejects local scale/rotation/translation of frame-attached controls while retaining legitimate ancestor/window scaling. Run the same contracts with the actual normal, hover, held-down and disabled native states.

`PixelScale` requires an unsliced Sprite background explicitly filling its canvas and compares its complete source dimensions with `PixelUnits` local UI units per source pixel. It preserves transparent padding and rejects unsupported drawing modes instead of treating a silhouette or cropped region as the image canvas. It does not judge optical silhouette alignment; inspect the pixels too.

These contracts share the1024 total expectation budget. Each uses at most four point comparisons and eight native transforms, with no texture readback, nested tree scan or runtime timer; complexity remains O(V+E). `GeometryChecks` reports evaluated contracts. Zero checks is no geometric qualification. Caller-supplied geometry cannot satisfy `RequireSemanticContracts`; eligibility still requires actual game-owned expectations.

The runtime review inspects an attached UI Toolkit tree in the actual game. It supplements the existing authoring audits and explicit visual-check assertions; it does not infer design quality from a passing geometry report. The runtime core has no Editor dependency and is callable by a Player integration. The typed Editor facade uses the official Pipeline catalog and exact UIDocument identity.

The selected root's native panel coordinates are authoritative. Screen pixels and panel units can differ after resolution changes; hit-size policy uses the explicit physical-pixels-per-panel-unit ratio. Offscreen scroll content is distinguished from unintended viewport escape. DisplayNone hides an entire subtree; inherited visibility follows the engine's resolved value, including an explicitly visible descendant.

The existing geometry tolerance applies to both available width and height.
Wrapped text is measured at content width plus that tolerance, before Unity
quantizes the measurement to pixels; unwrapped width and measured height use
the same budget. It does not increase the configured tolerance. Native paired
controls must admit a fractional width difference within that budget and
reject text that still overflows beyond it. Overflow findings retain the exact
measured size, content size and tolerance for pixel review. Measurement count
and the static cost ledger below remain unchanged.

Clipping reads the native parent VisualElement.worldClip, including USS and
engine border/padding behavior. This nonpublic engine getter is explicitly
retained in Runtime/link.xml for Player stripping; an absent getter is an
unsupported capability, never an inline-style approximation. Native source
contracts: [Unity2021.3](https://github.com/Unity-Technologies/UnityCsReference/blob/2021.3/ModuleOverrides/com.unity.ui/Core/VisualElement.cs)
and [Unity6](https://github.com/Unity-Technologies/UnityCsReference/blob/6000.2/Modules/UIElements/Core/VisualElement.cs).
The review uses the native axis-aligned clip rectangle; rounded silhouettes,
transformed pixel edges and textured contrast still need image review.

## Static cost ledger before implementation

- Native panel tree: at most8192 elements, depth64. Selected tree: at most4096 elements. Limit violations produce incomplete/failed review, never truncated success.
- One center hit test per currently visible enabled control, at most128. A native pick can traverse at most the admitted8192-node panel, so the bound is1048576 native node visits and128 Pick calls, without pairwise overlap enumeration. The finding states center obstruction, not impossibility of every possible click.
- Visibility, scrolling, clipping, editable-field ownership and opacity each use at most4096 times64 ancestor visits:1310720 visits in total. Hit-owner ancestry adds at most128 times64 visits. Expectation visibility and two opacity reads add at most1024 times3 times64 visits. Paths are bounded by the same depth. Native text measurement: at most4096 calls, one per admitted nonempty TextElement; editable field internals are excluded from fixed-label overflow policy.
- Semantic sources: at most512 explicitly selected component identities, one native identity lookup and at most64 parent-transform visits each (32768 total). No recursive GameObject/component search is performed. Sources collectively admit at most1024 expectations before iteration. Each expectation points to a real element and comes from a current game-state owner. No global registry or polling cache is retained. Source callbacks must be read-only, bounded snapshot producers; their own game-owned cost ledger remains required.
- At most1024 retained findings; exceeding the finding budget fails completeness. Total scratch/output budget32MiB, one source tree and one report live. No Asset scan, Scene write, hidden Game View or permanent preferences are created.
- One coherent synchronous main-thread inspection, with a500ms hard elapsed budget. Budget failure is explicit and cannot qualify acceptance. This is an on-demand diagnostic, not a per-frame gameplay update. Native fixture and actual game timings must be recorded before release.
- Static admission: pass for these bounded count/memory contracts. The runtime fixture must prove actual native layout, hit testing, source semantics and success/failure boundaries; no performance or visual acceptance is asserted by this ledger.

The wrapping fixture reuses its existing native Editor panel and fixed Label,
with three layout frame barriers and two review calls. It reads the actual
content width at the inclusive tolerance boundary before asserting the
strict-width counterexample, then retains the same tolerance for the positive
and overflowing controls. The existing fixture teardown owns the panel; no
runtime document, RenderTexture, Game View or saved Scene is created. Static
admission passes for this constant-size fixture; native execution remains its
qualification. Scaled game panels require their own actual-view replay.

## Acceptance boundaries

Measured rules cover ancestor visibility/opacity, finite layout, unintended viewport escape, native fixed-text overflow, configured hit size, center hit obstruction and supplied semantic expectations. Solid-color contrast can only be measured where its full background is known; textured/scene-backed pixels require image inspection. Title meaning, visual hierarchy, icon meaning, art direction, face quality and motion feel remain manual gates.

Semantic sources must consume actual eligibility/account/language/save owners. UI state is the observed value, not the source of the expected value. A report with no semantic sources cannot certify action eligibility or state/feedback consistency. Static Localization checks remain separate from real loaded-table and UI assertions.

Native fixtures must pair controls that differ only in the tested mechanism: unclipped/clipped text, clear/occluded control, adequate/small hit target, visible/hidden ancestor, readable/ancestor-dimmed requirement and current/stale actual model value. Attach fixtures to a real native panel and restore every temporary object and panel in finally. A detached mocked rectangle is not runtime validation.

## Integration

Find `uitoolkit/review-player-experience` in the current official catalog and read
its exact typed contract. Supply the exact active document identity from
runtime-documents, an immutable source fingerprint and the actual scenario.
The optional element path scopes the review; it does not hide findings in a
selected tree. LocaleContext describes the caller's context and never proves
which Localization table has actually loaded.

Game components can implement `IVmUiAuditSource`. Capture fresh expectations
from authoritative inventory, eligibility, account, language or save owners.
Point each expectation at its actual current element and retain the business
owner's name. Pass the exact component identities in SemanticSourceInstanceIds;
they must belong to the selected UIDocument's GameObject hierarchy. Sources must collectively return at most1024 expectations for a
document; filter results only after enforcing that admission bound. Set
RequireSemanticContracts when validating business-state consistency.

A Player test harness can call `VmUiExperienceAudit.Review` directly with its
attached root, target-pixels/panel-units scale and captured expectations. The
core has no Editor or CLI transport dependency. Save its full report alongside
the source/build fingerprint and real images or motion capture.

Run the `VmUiExperienceAuditTests` fixture in the declared
`VMUnityAutomation.Editor.Tests` assembly through the native package-test job.
The fixture restores its temporary panel and prior focus. Use matched negative
controls as well as a passing tree; changing the threshold to conceal a failed
case does not qualify the review.

## Declared layout intent

Containers may declare ui-audit-center-x / ui-audit-center-y, ui-audit-align-left / right / top / bottom, ui-audit-row / column (ordered non-overlapping flow), and ui-audit-nonempty-rows. These are diagnostic intent classes, not style rules. Direct displayed flow children are checked against the parent's native content rectangle, including padding, border and transforms. Absolute overlays are excluded from flow alignment. Plain text uses native measured line bounds and resolved TextAnchor; buttons and images use their frame. A stretched top-aligned separator therefore fails even when its container is centered. Nonempty rows require visible descendant text, image or control; a container's frame alone is insufficient. Empty containers with no rows are valid. Optical glyph/sprite centering and motion still require image review.

Cost addition: selected<=4096; one bounded ancestry propagation (<=4096*64 visits) and one direct-child pass, at most4095 child edges and six comparisons each; at most4096 additional native text measurements. No pairwise sibling comparisons. Existing depth64, finding1024 and coherent500ms hard limits remain unchanged.

Best-fit text on Unity6.3+ reads the engine computed auto-size mode and uses the raw native text handle measurement with actual width/height constraints. Public MeasureTextSize clamps AtMost results and unconstrained measurement chooses the maximum best-fit font. Neither can establish overflow alone. Missing engine metadata fails capability. Native fixtures must admit fitted text and reject overflow at the minimum font. Retain the reflected engine accessors for Player stripping. Cost remains one measurement per text check, with a bounded metadata read and invocation per best-fit text; existing count/time bounds apply.

Native text layout: the actual Advanced Text generator receives its real width and height constraints, and the audit reads its unclamped preferred size. Unity owns auto-sizing and fixed-font layout. SDF measurement retains the public native API. Runtime diagnose exposes generator and cached-width facts; diagnostics identified Advanced Text in the original Chinese title, rejecting the initial SDF-cache hypothesis. No geometry tolerance changes. Reflection metadata reads are O(1), at most seven per text; the existing4096-text/500ms review limits remain. Strict text overflow, minimum-font overflow and actual scaled game views are independent acceptance controls.
