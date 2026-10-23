# Rebuild an existing uGUI preview

After Editor authoring or a resolution change, a uGUI view may retain old layout
geometry or text UVs from an earlier dynamic font atlas. Use the typed `ui/rebuild`
tool on the exact existing loaded UI GameObject identity from `scene/hierarchy`.
Discover its current CLI identifier with the bounded catalog (`ui/rebuild`).

Pass `rootInstanceId` and the exact project binding through `vm_automation_call`.
The owner invalidates native text generators, rebuilds the scoped layout, prepares
the current text with Unity generation settings, then rebuilds graphics and
requests a PlayerLoop update and native repaint. Glyph preparation precedes
geometry publication so an atlas rebuild does not leave earlier UVs displayed.

The command changes native preview caches. It does not create objects, toggle
active states, replace font/sprite/material references, edit text values, save
assets or scenes, enter Play Mode, open windows or change Game View presets.
Native layout owners still compute their ordinary driven RectTransform values.
Use an existing visible Game View and capture its native surface for pixel
acceptance; the returned counts prove the scoped rebuild, not visual similarity.

## Static Cost Ledger

Admission is completed before invalidating caches. One exact object identity
avoids a scene-wide path search. A subtree admits at most 4096 Transform nodes;
uGUI's Graphic component multiplicity permits at most one Graphic per node.
Each node is visited once, with one component lookup and one parent edge. Active
assigned-font text admits at most 65536 total characters, font sizes in 1..512,
and 4194304 summed character-count times maximum-font-size-squared work units.
Rejection reports `ui_rebuild_limit` before the rebuild begins.

There is one scoped native layout rebuild, at most 4096 glyph preparation calls
and 8192 graphic rebuild calls on the Editor thread. Managed traversal storage is
linear and below 256 KiB excluding Unity-owned generators, mesh and font caches.
Native glyph requests are bounded by the admitted work units; native cache
allocation and font rasterization remain owned by Unity. The synchronous target
budget is one second. No Asset scan, global Canvas force-update or window
creation is introduced. PASS for these admitted axes; changing them requires
reviewing this ledger and the published limits.
