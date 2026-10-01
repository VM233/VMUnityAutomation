# Native 2D overlap membership

The existing circle and box overlap routes return the native active colliders
inside the requested region. Their `count` is the full native query result;
`colliders` contains at most the declared `maxResults` (capped at 500), and
`truncated` records omitted results. A region query is not a whole-world census.

Every 2D collider descriptor includes a `physics2D` snapshot with the exact
collider identity, hierarchy path, layer, trigger flag, native active shape count,
world bounds and render-frame identity. Its `attachedRigidbody` is null for a
collider without an explicit body; otherwise it records the native body identity,
type, simulated flag, position and linear velocity. Multiple colliders on the
same GameObject retain distinct collider identities. 3D descriptors retain
their existing contract and do not carry a 2D snapshot.

This closes the attribution gap in the retained MarbleBattlers first-summon
capture: 85 region colliders had repeated names and no native shape or body
information, while Physics2D.FindNewContacts took 124.759 ms across three
recorded branches in frame 111. Collider count alone cannot establish the native
shape count, body velocity, membership owner or cause of that cost. This change
adds observation; it does not claim a physics speedup.

Entry: existing physics/overlap-sphere and physics/overlap-box. The native query
owns membership; VmAutomationPhysicsCommands owns each returned observation.
The descriptor and closed generated schema share that owner. Capture happens
on Unity's main thread in the original query callback; consumers receive one
complete result. No state write, simulation, recorder, cache, retry or new route.
Unexpected native getter errors retain the executor's existing exception boundary.

Static Cost Ledger before executable writes: retain the existing native query,
ordering and output cap. Reuse each hierarchy path already produced by the
existing sorting pass, through a per-call key/value pair; do not add a second
hierarchy traversal or infer an unmeasured hierarchy-depth bound. Add a constant
descriptor for at most 500 returned 2D colliders, with at most 20 native
scalar/vector/object reads per result: <=1,700 in the frozen 85-collider region,
<=10,000 at the output cap. The extra sort-product storage is two references per
native hit (1,360 bytes for the frozen region on a 64-bit Editor); descriptors
are <=4 MiB transient managed dictionaries plus references to the existing
path strings. Path strings retain the existing query's input-dependent storage;
there are zero additional parent visits. No new participant/shape enumeration,
Cartesian pairs, per-tick consumer or retained memory. Read the supported native
linear-velocity property in the sole Unity 6
compile branch, and the declared Unity 2021.3 property in the older branch.
PASS for the added work in the bounded output contract. Native hit enumeration,
sorting and hierarchy depth remain the existing query's cost axes.

Schema generation retains the current package's 267 Editor source files,
6,263,452 source bytes and largest 1,318,808-byte generated file. This change adds
two closed nested object definitions to the two existing overlap result shapes;
it adds no generator loop or source-discovery axis. Generated output, schema
review and the current Main compile/native queries provide the acceptance proof.
