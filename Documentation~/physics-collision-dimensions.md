# Native collision matrix dimensions

The existing `physics/collision-matrix` and `physics/set-collision-layer`
commands select `dimension` (`2D` or `3D`, default `3D`) and publish that
dimension with their native result. Physics2D and Physics matrices are separate
authoritative states. Reading the 3D matrix cannot establish a 2D simulation's
filtering. The selected API owns both lookup and mutation; no inferred masks,
parallel matrix state or extra route is introduced.

Entry and producer remain VmAutomationPhysicsCommands. Unity's selected physics
system owns the matrix and any contact-reset side effects. CLI consumers adopt
the one result. Layer validation retains the existing command boundary.

Static Cost Ledger before executable writes: matrix indices are the fixed Unity
layer domain of 32 x32 =1,024 pairs; at most 1,056 layer-name reads and 1,024
native ignore-state reads. Response has at most 32 lists of at most 32 names.
Setter performs at most two layer-name lookups and one native write. No new
scene/object scan, cache or Cartesian axis. Existing costs are unchanged by
selecting the native physics system. PASS. Validate both native dimensions and
exact restoration of an owned Play-mode-only 2D layer mutation.
