# Prefab Variant override reversion

Discover `vm_auto_prefab_asset_revert_variant_override` and read its current
catalog schema before invocation. Supply the absolute expected project path and
the Variant asset path. `revertAll` restores the complete source state; otherwise
`targetGameObject` filters by the exact GameObject name and `targetComponentType`
filters by the exact component type's short name. Ensure names are unique in the
authored hierarchy when selecting by name.

Filtered operations include property overrides, added components, removed
components, added GameObjects and removed GameObjects. Whole GameObjects are
only eligible when no component filter is supplied. Removed GameObjects are
available on Unity 2022.1 and newer; earlier supported Editors use their native
component/property override model. Unmatched overrides are preserved.

The handler loads the Variant's authoring contents in a preview scene relative
to its inherited source. The shared Prefab mutation session owns save, unload
and transactional rollback, saving back to the requested Variant. It does not open, unload,
save or dirty the caller's loaded scenes. The response reports the number of
reverted overrides, or `all`; zero means no override matched. Reopen the saved
Prefab to verify inherited values and unrelated overrides after a restoration.
