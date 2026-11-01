# Exact native Editor Overlays

Discover `editor/overlay` with the official Unity CLI catalog and read its typed
contract. Supply the exact existing `windowInstanceId` from `uitoolkit/windows`
and the registered `overlayId`. A MainToolbarElement's path is its Overlay id.
Omitting `displayed` observes; an explicit Boolean changes `Overlay.displayed`
and verifies native readback. `PreviousDisplayed` supports restoration.

The command requires stable Edit Mode. Missing windows or exact Overlay ids are
rejected before mutation. It creates, focuses and closes no windows and changes
no docking, other Overlays, Game View presets, assets or runtime state. Unity can
persist this native visibility in its user Editor layout. Temporary validation
must restore the returned prior state. Intentional toolbar configuration may
retain the requested state. Native display adoption does not prove rendered
visibility: use `uitoolkit/query` to read the exact current control and bounds.

The implementation uses public `EditorWindow.TryGetOverlay` and
`Overlay.displayed`, available throughout the package's Unity 2021.3+ support
range. It does not access the older internal OverlayCanvas API. Cost is one
identity lookup, Unity's linear search of that window's registered Overlays,
and at most one native state publication. No frame loop, job, retained state or
gameplay search is introduced. Focused native integration should observe,
hide/show/restore the selected Overlay and compare unrelated controls and window
inventory. The package fixtures cover both missing-owner rejection paths.

API references: [TryGetOverlay](https://docs.unity3d.com/2021.3/Documentation/ScriptReference/EditorWindow.TryGetOverlay.html)
and [displayed](https://docs.unity3d.com/2021.3/Documentation/ScriptReference/Overlays.Overlay-displayed.html).
