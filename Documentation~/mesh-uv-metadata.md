# Native mesh UV metadata

`graphics/mesh-info` reports the number of present UV channels across Unity's
eight channels, indices 0 through 7. A mesh containing only channels 0, 5 and 7 reports
three. Presence is read from the native vertex attribute layout, rather than
allocating the mesh's UV arrays. This also includes channels used for authored
deformation and other vertex payloads.

The catalog owns the current input and output schema. The public-command native
tests are `VmGraphicsMeshUVTests`; their mesh assets are owned by each test and
retire in `finally`. The inspection performs exactly eight attribute queries,
with constant extra storage and no per-vertex UV readback.
