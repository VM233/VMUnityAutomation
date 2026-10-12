# Complete persistent job results

JobHistory publishes immutable native job snapshots for the official CLI reader.
The terminal status describes execution. The result contains the owner's actual
acceptance evidence, including failed checks in a successfully completed review.
Snapshot publication must preserve that entire product and remove only the private
access token. A result cannot be replaced by a successful status because it is large.

The former 128 KiB snapshot shortcut discarded a 180,052-character DD2 description
review product. Canonical execution had finished and persisted the result, but the
public reader could no longer read Passed, Checks, Frames or retirement evidence.
Publication now takes a detached JSON copy without that lossy alternate product.
Record persistence, capability admission, retention, request identity and the
official jobs/get route keep their existing owners. No job is replayed by a read.
Previously discarded snapshots are not fabricated or rewritten: rerun the actual
review to obtain a new accepted product.

## Static Cost Ledger

Frozen witness: one 180,052-character terminal report. Focused regression inputs
are 131,073 and 180,052 ASCII padding characters plus a small nested report with
unicode, a fractional value and a failed final check. Three temporary records
cover publication, reload and detached mutation. Each is below 192 KiB UTF-8.
The capture has one serialization and one parse, each linear in the same product;
it has no per-history-record payload serialization or native Unity call. Each
ordinary history publication writes only the changed record. Existing index
traversal and retention remain unchanged (at most 2,000 history identities).
For these witnesses, the changed payload is below 192 KiB and transient text
copies below 768 KiB; parsed test objects remain below a 4 MiB budget. Three
fixture records use below 576 KiB on disk. PASS. The complete DD2 result is also
replayed through the native official CLI after package adoption.
