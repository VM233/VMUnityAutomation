# Prefab property discovery

`prefab-asset/get-properties` inspects one component in native Prefab authoring
contents and unloads those contents without saving. The default returns visible
serialized properties. Set `includeHidden` to true to discover native backing
fields such as `m_Enabled` and `m_ObjectHideFlags`. Each record includes the exact
`propertyPath` accepted by `prefab-asset/set-property`. Reading hidden fields does
not change their editability or authorize a write.

This closes the inspection gap found while diagnosing a disabled SkeletonMecanim
after a skeleton migration. Use the returned native value to verify enabled state
after save/import/reload; visible-only output cannot establish that state.

Static Cost Ledger before executable writes: one requested Prefab, one exact
GameObject and component, one native top-level serialized-property iteration.
The existing value formatter and response size admission remain the owners of
nested values and output bounds. No second field traversal, asset scan, cache or
Editor callback is added. Frozen Warrior witness: 28 visible fields plus native
hidden backing fields, fewer than 64 property records. One extra Boolean read,
one Boolean branch per iterator step and one property-path string per returned
record. Tests use one native MeshRenderer Prefab per case, no project scan, and
pair preview scene, authoring contents and temporary asset lifecycles. PASS for
the migration witness and the unchanged command resource domain.
