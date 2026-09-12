# Test results across assembly reload

`testing/run-tests` and the package-test workflow use the same Test Runner owner.
Unity produces one immutable leaf result at `TestFinished`, then a canonical leaf
collection at `RunFinished`. `VmAutomationTestJobSession` owns the Editor-session
records. It publishes each result before publishing that job's completed count.
The terminal collection replaces the callback-order collection before terminal
status is published. `testing/get-job` reads the same results for details,
failure filtering, pagination and summaries after an assembly reload.

Job membership, one job's metadata and individual leaf results have separate
SessionState keys. A leaf callback updates one result and one job, without
serializing other jobs or previous results. The Test Runner owner retires the
keys when its existing 30-minute completed-job retention expires. SessionState
lasts for the Editor session. Durable job history continues to own summaries
after that session ends.

Version 0.6.8 retained only the first 50 failures in its reload cache. Witness
job `0b8b8a80d796` completed 10 tests successfully, but after the package workflow
reloaded, `testing/get-job` reported zero detailed results. The private v2 cache
starts empty on upgrade, while existing durable history remains available.
Finish active tests before upgrading. Previously discarded details cannot be
reconstructed. Missing v2 result records are persistence errors, never an empty
successful result list. No legacy reload cache is read as a complete product.

## Static Cost Ledger before implementation

Frozen acceptance inputs are the 10-test Hierarchy fixture and four persistence
tests, each owning at most 100 synthetic leaves. Synthetic fields total at most
512 UTF-16 characters per result. There is one active Test Runner and all
SessionState work executes on the Editor main thread, with no Asset scan, scene
operation, hierarchy query or per-frame polling added.

For T leaves and J retained jobs, a run publishes T incremental leaf records,
then T canonical records once, and at most 2T + 4 fixed-size job metadata records
(start/finish callbacks and lifecycle boundaries). Membership publication is J
IDs only at admission/retirement. No T-by-T or T-by-J serialization occurs.
Reload performs J metadata reads and the sum of their completed leaf counts in
result reads. Retiring a job performs T + 1 key erasures. Existing public-history
publication remains one changed summary per state event.

The 10-leaf production witness adds at most 20 result serializations. Each
100-leaf synthetic fixture has at most 200 result writes, 200 reads and 201 key
erasures, including a replaced collection. Four fixtures therefore have upper
bounds of 800 result writes, 800 reads and 804 erasures, with no Cartesian
product. At most 100 live synthetic result objects, 100 stored strings and one
transient serialized result are retained per fixture, under 512 KiB including
metadata and dictionary overhead. The budget is linear publication, under
512 KiB for this frozen fixture, and zero gameplay-frame work. PASS.

Verification covers all leaf statuses, more than 50 failures, canonical order,
reload-equivalent reconstruction, failure/detail pagination, exact numeric and
stack-trace preservation, cleanup and a missing-record failure. A real package
test followed by its automatic manifest-restoration reload is the integration
acceptance path.
