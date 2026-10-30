# Native mesh metadata

## Static cost ledger

The selected mesh admits at most4096 submeshes,512 blend shapes and512 UTF-16
code units per shape name. Inspection makes8 UV presence queries,5 other vertex
attribute queries, one native binding-count query, up to8192 submesh metadata
queries and512 name queries. These independent axes are added, never multiplied.
No vertex, triangle, normal, color, tangent or bind-pose array is read back.
Names retain at most262144 UTF-16 units (512KiB excluding object headers);
other output storage is constant. Exceeding a bound returns
`mesh_metadata_limit`, without a partial successful report. All calls run once on
the Editor main thread. Mesh selection uses the existing asset or scene owner;
this ledger covers metadata extraction from that selected native mesh. Pass.

Native fixture ledger:14 sequential cases (6 UV,4 binding/readability,3 limits,
one generated schema check),
each owns one3-vertex mesh. The largest shape fixture has513 channels, one frame
each,3 deltas per frame (1539 vectors); the largest index-layout fixture has4097
empty submeshes and is rejected before traversal. At most two selector calls per
case, no Cartesian scenario expansion and no retained fixture assets. Pass.

`boneCount` is the mesh's bind-pose count for every selection path.
`isSkinned` means that it has bind poses and native blend-weight/index attributes;
it does not depend on whether selection passed through a renderer.
`blendShapeNames` preserves the native index order, including namespaced names.
Unity2022.2+ exposes `Mesh.bindposeCount`; older supported Editors use the
engine's exact private `GetBindposeCount` getter, with no array allocation or
alternate data path. The version boundary is checked against Unity's reference
source for2021.3,2022.1 and2022.2.

`graphics/mesh-info` reports the number of present UV channels across Unity's
eight channels, indices 0 through 7. A mesh containing only channels 0, 5 and 7 reports
three. Presence is read from the native vertex attribute layout, rather than
allocating the mesh's UV arrays. This also includes channels used for authored
deformation and other vertex payloads.

The catalog owns the current input and output schema. The public-command native
tests are `VmGraphicsMeshUVTests`; their mesh assets are owned by each test and
retire in `finally`. Skin fixtures compare the same mesh through asset and scene
selection, with and without skin bindings, and retain metadata after CPU data
is released. Shape-name and submesh limits have explicit negative controls.
