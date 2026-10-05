# Native UI Toolkit hierarchy

Inspection, queries, generated child skins and numeric tree paths all describe
`VisualElement.hierarchy`. `ScrollView.Children()` describes its authored content
instead, so it omits the viewport, scrollers, sliders and repeat buttons. The
inspection owner must not mix content indexes with native hierarchy indexes.
Named paths also search native descendants. This is observation only: authored
content ownership and runtime input handling stay with the native controls.

Discover the bounded tree/query/generated-children contracts through the catalog.
Use a document or window owner, then retain the returned numeric path only while
that native hierarchy remains unchanged. Tree depth and node limits still apply.
No legacy content-index path is accepted as an alternate route.

## Static Cost Ledger

The patch changes the child source, not traversal axes, result limits or recursive
algorithms. A tree call visits at most its declared `maxNodes` and depth. The
regression graphs each contain one ScrollView, one Label and at most 64 native
elements; four cases require fewer than 2,048 node visits and no asset writes.
The affected live witness is the DoomsdayDiary UI Builder host, bounded by 2,500
retained authoring elements and generated parts, queried one target at a time.
Generated-child acceptance is scoped to one ScrollView or Scroller, depth six,
under 128 descendants. All calls run on the Editor main thread; result allocation
is bounded by those frozen witness graphs. Budget: pass.

The regression covers scroller discovery, named and numeric round trips, the
existing tree node bound and preserving ScrollView content ownership. It does
not claim visual or touch acceptance.
