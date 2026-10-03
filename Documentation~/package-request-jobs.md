# Package request jobs

The frozen `packages/search` request for `com.unity.pipeline`, offset 0 and
limit 1 in MarbleBattlers on 2026-10-03 timed out at the official CLI's
30-second response boundary. The handler retained its response callback until
the asynchronous native request completed. No durable job identity reached the
caller. The observed error establishes that publication boundary, not the cause
of registry latency. Add and remove use the same callback ownership.

Entries remain the three existing package contracts. WorkspaceJobRunner owns
admission, identity, persistence, client adoption, serial execution, compilation
and final publication. PackageRequestJobRunner owns one native UPM request and
its completion product. The initial response is the existing durable job
snapshot, including capability and `jobs/get`. The original response callback
and per-request Editor update subscriptions are removed.

Transaction metadata declares native-operation atomicity, workspace-exclusive
issuance, reload-resumable job durability and no rollback. It makes no claim of
an Automation file rollback or of resuming an unobserved native request.

The request issues once after durable publication and client adoption. Search
reads the online registry, projects only the requested page and publishes its
native completion result. Add and remove require stable Edit Mode and preserve
the completed native result before entering the existing asset refresh, clean
compilation and assembly reload chain. Final verification compares the named
package's manifest, lock and registered product with the native result. A native
error is retained as a terminal domain failure with its native error code.

Cancellation is available before native request issuance. Unity's UPM request
API cannot cancel an issued operation; the job keeps owning and observing it.
The concrete native request is held by a ScriptableSingleton, using Unity's
native request serialization to preserve its operation identity across assembly
reload. No native handle is reconstructed, reflected or reissued. The durable
job keeps its original identity. An Editor process restart loses the old native
operation; it exposes `package_request_outcome_uncertain_after_reload` and the
exact request identity, rather than inferring success from current files.
After completion persistence, refresh and compilation resume through the
existing workspace lifecycle. Search completion requires no refresh or rebuild.

Catalog consumers discover the current job schema and poll the original job.
Terminal search data is in `result`; mutation results include the existing
compilation receipt plus native package completion and verified package state.
Other consumers are not automatically upgraded. Registered package info, list,
status and lint continue reading the native registration product directly.

## Static Cost Ledger before executable writes

PASS for the frozen consumer domain: 200 workspace records, zero active
workspace jobs, 90 registered packages, 70 manifest dependencies, and one
requested search row. The existing serial workspace selector and identity
lookups are reused: at most 200 record visits per selector and at most 200 per
identity lookup. No second scheduler, transport, worker, timer or subscription
is added. One native request is alive at a time, in one Unity-owned serialized
Editor object containing one job identity and three concrete request slots.
Only the slot for the admitted operation is populated. No disk Save or manual
serialization of a live request is performed. Pending observations read at
most its three native status fields and allocate no page or package snapshots.

Search results are the native PackageInfo array. Offset selects an array slice;
only the requested 1..200 items are projected, without a full-result dictionary
or copy. Maximum added projection is 200 dictionaries with four scalar/string
references, under 128 KiB excluding strings already owned by UPM. The frozen
one-row request is below 4 KiB. Native registry transfer and pre-existing
metadata string storage remain UPM-owned and are not claimed bounded by this
change. Result persistence occurs once at completion, never once per frame.

Mutations freeze the named manifest/lock declaration on native completion and
read it again after compile, with one registered snapshot: at most
2 * (90 + 70) + 90 = 410
package/key visits in this frozen domain. The selected package result is below
16 KiB; no Asset scan or additional compilation is introduced. These reads occur
on the main Editor thread at a completion boundary, not inside calibration
ticks. Existing workspace persistence and compilation domains are unchanged.

Focused verification covers receipt-before-native-issuance, native failure,
search paging including offset past end, lost completion across reload,
persisted completion across reload, and mutation final-product mismatch.
Native acceptance must repeat the exact original search through the official
CLI and its original durable job, check precise changed-source policy and
dependency/meta reviews, and preserve the concurrent calibration checkpoint.

The focused fixture has three paging cases over three existing registered
metadata records (at most six projected rows), three route-contract cases, one
queued/canceled job with one identity reuse, one lost-request witness, one
completion serialization case, and two mutation-completion cases. It issues no
native package mutations, imports or calibration rows. State comparisons visit
the ten immutable package facts once. The actual original online search is the
separate native acceptance request. The schema generator visits the existing
401 built-in descriptors (ceiling 433) and 271 Editor source files (ceiling 512), within a
64 MiB input and 256 MiB worker budget; only the three package output shapes
and registry fingerprints change. No Unity main-thread generation work is added.

Unity documents [ScriptableSingleton assembly-reload persistence](https://docs.unity3d.com/2021.3/Documentation/ScriptReference/ScriptableSingleton_1.html).
Its [native Request implementation](https://github.com/Unity-Technologies/UnityCsReference/blob/2021.3/Modules/PackageManager/Editor/Managed/Requests/Request.cs)
owns operation identity serialization and native operation release. The package
uses those mechanisms directly and never accesses their private fields.

The native changed-source review of 0.6.99 found its general input catalog at
1,501 lines, beyond the unchanged 1,500-line policy. The three package-request
schema producers move into the existing specialized input catalog. Their
authoritative shapes, dispatch order and generated contracts remain the same.
Static Cost Ledger: PASS; this relocation adds no loop, allocation, lookup or
runtime call. The existing first specialized dispatch owns these three inputs;
there is one producer per route and no forwarding wrapper or duplicate schema.

The frozen Git update `db286e841c0f4c8a8924d09e2760546d` issued at
02:44:34.8286328 UTC and observed native completion at 02:50:39.1461883 UTC
on 2026-10-03. It then failed the registration deadline in the same observation,
because that deadline used the earlier issuance time. Manifest and lock already
matched; registered metadata still identified the preceding revision. UPM's
automatic resolve and assembly reload subsequently registered the target.
The native wait was 364.318 seconds. Its latency cause remains unproven.

Git update registration now uses the persisted native completion time; resolve
uses its own issuance time because it has no native Request completion object.
The registration allowance remains 300 seconds. Git updates share the existing
Unity-owned native request object and completion product with package additions.
The old static handle, cancellation retry and target-based inferred native
completion are removed. A missing original operation after process restart
publishes the existing uncertain-outcome error. Package add/remove persist their
native product before refresh and compare registered state with that product
only at final verification, rather than rejecting a still-old registration in
the completion callback.

Static Cost Ledger before these executable writes: PASS. One original UPM
operation, one native completion product under 16 KiB, the existing 200-record
workspace domain and one serialized request slot remain unchanged. Deadline
evaluation reads one existing timestamp and performs one subtraction. Git
update target checks retain their existing bounded package state reads; the
registration event invokes the same observation owner rather than a second
producer. Add/remove retain a five-field declaration snapshot independently of
the delayed registered metadata; final verification rejects declaration drift.
Their state reads decrease to the 410 visits above. Comparing the five frozen
fields adds five scalar comparisons. No
additional scheduler, request, retry, compilation, loop, actor or main-thread
calibration work is introduced. Focused regression covers long native waits,
both exact 300-second clock boundaries, and the delayed registration product.
Five clock cases, one native addition product and one declaration-drift case
extend the eleven-case fixture to eighteen cases. They issue no native request.

Self-update `aad24f269ecd45438f3fcf155a09d46e` from 0.6.100 to 0.6.101
completed compilation but rejected final publication with a null reference.
The preceding producer had persisted native completion and its timestamp,
without the newly proposed Git completion metadata dictionary. The proposed
Git output extension and its private snapshot writer are withdrawn. Git
updates retain the existing completion observation, timestamp and verified
target product; the final publisher consumes that same established product.
Search/add/remove keep their own native metadata product. No substitute
metadata, old-data adapter, missing-field guard or dual output is introduced.
The native acceptance must complete the self-update lifecycle across this
producer revision boundary as well as the focused fixture.
