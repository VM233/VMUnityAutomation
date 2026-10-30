# Runtime player UI review

The runtime review inspects an attached UI Toolkit tree in the actual game. It supplements the existing authoring audits and explicit visual-check assertions; it does not infer design quality from a passing geometry report. The runtime core has no Editor dependency and is callable by a Player integration. The typed Editor facade uses the official Pipeline catalog and exact UIDocument identity.

The selected root's native panel coordinates are authoritative. Screen pixels and panel units can differ after resolution changes; hit-size policy uses the explicit physical-pixels-per-panel-unit ratio. Offscreen scroll content is distinguished from unintended viewport escape. Hidden ancestor trees do not become visible merely because a descendant's own visible property is true.

## Static cost ledger before implementation

- Native panel tree: at most8192 elements, depth64. Selected tree: at most4096 elements. Limit violations produce incomplete/failed review, never truncated success.
- One center hit test per currently visible enabled control, at most128. A native pick can traverse at most the admitted8192-node panel, so the bound is1048576 native node visits and128 Pick calls, without pairwise overlap enumeration. The finding states center obstruction, not impossibility of every possible click.
- Visibility, scrolling, clipping, editable-field ownership and opacity each use at most4096 times64 ancestor visits:1310720 visits in total. Hit-owner ancestry adds at most128 times64 visits. Expectation visibility and two opacity reads add at most1024 times3 times64 visits. Paths are bounded by the same depth. Native text measurement: at most4096 calls, one per admitted nonempty TextElement; editable field internals are excluded from fixed-label overflow policy.
- Semantic sources: at most512 actual document-owned components and1024 immutable expectations. Each expectation points to a real element and comes from a current game-state owner. No global registry or polling cache is retained.
- At most1024 retained findings; exceeding the finding budget fails completeness. Total scratch/output budget32MiB, one source tree and one report live. No Asset scan, Scene write, hidden Game View or permanent preferences are created.
- One coherent synchronous main-thread inspection, with a500ms hard elapsed budget. Budget failure is explicit and cannot qualify acceptance. This is an on-demand diagnostic, not a per-frame gameplay update. Native fixture and actual game timings must be recorded before release.
- Static admission: pass for these bounded count/memory contracts. The runtime fixture must prove actual native layout, hit testing, source semantics and success/failure boundaries; no performance or visual acceptance is asserted by this ledger.

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
owner's name. Sources must collectively return at most1024 expectations for a
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
