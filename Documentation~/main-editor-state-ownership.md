# Main Editor state ownership

The frozen MarbleBattlers refresh `e7e8447aa1ad4aadbfaf32b0a17e4494`
failed after reload because the history index referenced 81 absent records.
All 81 identities still have complete canonical workspace execution records.
The imported worker log proves that the workspace recovery constructor runs
inside `AssetImportWorkerHW0`; its native context reports MainProcess:False.
The persistent runner also runs there and, after finding an in-flight job,
publishes all its 200 retained job snapshots. That second process prunes
workspace records from the shared history directory. The main Editor retains
its older in-memory membership and subsequently republishes that index without
rewriting its unchanged records. This creates durable dangling references.

Automation state is owned by the main Editor process. Import workers own
asset conversion and never restore jobs, mark execution interrupted, publish
history, register command scheduling, restore test state, configure watchers
or save console diagnostics. Every InitializeOnLoad state owner adopts this
process role before doing its first state operation. The native role contract
is AssetDatabase.IsAssetImportWorkerProcess, present in the declared Unity
2021.3 support baseline. This is process eligibility, not an error fallback.

The history repair command performs an explicit reconstruction transaction.
The published index remains authoritative for membership and order. Existing
records are validated and preserved byte-for-byte. Missing records may be
restored only from their matching canonical workspace execution owner,
including its original identity, access capability, status, result and error.
All required owners must be present before any record is written. The repair
does not purge the index, synthesize success, replay a job, change job execution
state, restore an old aggregate or silently omit a missing member. Unsupported
missing owners fail explicitly with no write. The result reports restored and
preserved counts. Ordinary history loads remain strict.

The public workspace snapshot producer moves out of the scheduler so this
explicit repair can consume native execution state before scheduler recovery.
Normal publication and repair use that one producer. Static scheduler failure
must not be bypassed by a duplicate snapshot implementation.

## Static Cost Ledger before executable writes

One process-role owner reads the immutable native role once per domain;
eleven InitializeOnLoad owners and the executor read that value. On an
import worker they perform zero automation-state reads/writes/subscriptions.
There is no new loop, cache, timer or runtime-gameplay work.

Explicit repair admits at most 2,000 indexed history identities and the existing
200 workspace jobs. One identity map and two linear index passes replace any
Cartesian join: at most 4,400 dictionary/identity visits, 200 identity hashes,
2,000 validation hashes and 2,000 record reads. Each validated history snapshot
is bounded by the existing 128 KiB character product contract. Reconstruction
admits an index up to 256 KiB, at most 200 canonical execution owners, records
up to 512 KiB each and 32 MiB of indexed record bytes in total. Missing products
are serialized and byte-budgeted during validation, before any write. The frozen witness restores
81 records, reads 119 existing records and scans 200 execution identities.
No Asset scan, Physics2D work, native job execution or simulation is performed.
The complete missing-record strings are immutable publication products. Their
UTF-16 payload is at most 64 MiB; sequential validation and publication buffers
add at most 2 MiB. Existing canonical products are already owned by the loaded
workspace store. Re-adopting the complete history adds its existing strict
parser's bounded 32 MiB input. Reserve 128 MiB of additional working memory,
outside gameplay; do not parallelize reads or writes. The transaction runs only on the
main Editor, outside Play Mode, and returns a structured completion receipt.
It adds zero work to normal progress persistence. PASS for this domain.

The repair UUID owns an immutable receipt under Library/VMUnityAutomation.
Optional native domain reload is requested only after that receipt is durable;
the status action returns the same receipt across reload without replaying work.

Validation preserves the corrupt index and canonical execution snapshots,
reproduces the old two-publisher corruption with the production record store,
checks complete reconstruction/capability preservation, unknown owners,
invalid identities and unchanged valid bytes, and then repeats the original
official refresh identity across native worker reload. Package pin, resolved
revision, compilation/reload and dependency review are independent gates.
