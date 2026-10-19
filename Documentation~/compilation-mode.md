# Editor compilation mode

`editor/state` reads Unity's current `CompilationPipeline.codeOptimization`.
`asset/refresh` accepts an optional `codeOptimization` of `Debug` or `Release`.
Omitting it preserves the current native mode. The durable request owns the
intent; Unity owns the mode and compiled assemblies. The existing workspace
job applies an explicit mode after publishing its compilation admission, then
requests the clean rebuild immediately before the validated asset import and
verifies the requested mode after reload. Compiler callbacks are owned before
import can trigger compilation; returning from import preserves their actual
phase instead of queuing a second clean cycle after an automatic reload.
Its terminal compilation result publishes the observed mode. A mode changed
by another Editor action fails with `code_optimization_changed`; the job does
not silently reapply it or accept a rebuild in another mode.

Changing mode is an Editor compilation operation, not a gameplay change or a
method warmup. Debug permits C# debugging; Release enables C# optimization.
Callers must retain the original job across reload, await its complete assembly
and reload evidence, and only then admit runtime work. Performance measurements
must record their mode and retain the same inputs, clocks and performance gates.

Entry: `asset/refresh`; state owner: Unity's `CompilationPipeline`; producer:
the existing workspace job; product: its persisted request and terminal compile
receipt; consumers: `editor/state`, the generated schema and calibration callers.
The existing asset refresh, package update/resolve and compilation lifecycle
remain one chain. No extra job kind, retry, transport, cached mode or timer.

Static Cost Ledger before executable writes: one optional enum decode and at
most one native mode assignment before the existing clean compilation request;
one mode read at terminal verification and one read per explicit state query.
One optional request scalar and one terminal result scalar, below 1 KiB. No new
loop, scene scan, allocation buffer, frame work or thread. The existing bounded
schema generator gains one input enum and one state scalar. Native validation
uses one Release clean rebuild, exact-mode readback, and one frozen 16-row
900-tick cold replay with Profiler recording disabled. It preserves the 100 ms
logic tick and 250 ms callback gates, retires the owned Play session, and checks
source and Console. PASS for the changed command domain; a performance benefit
is an unproved hypothesis until the cold replay completes.

Workspace progress distinguishes the native request, compilation start and
finish callbacks, and assembly reload. A submitted request does not prove that
compilation started. After a transport interruption, poll the original receipt;
do not submit another refresh identity.

Static Cost Ledger for compilation admission ordering: one existing native
clean request and one import per refresh, with the admission callback executed
only after import validation. Import completion, compiler completion and reload
each publish O(1) existing lifecycle state. Progress adds four booleans and three
nullable timestamps, below1KiB per read; no new loop, task, frame wait, buffer,
cache or persisted mode. Existing asset-path and assembly domains are unchanged.
Focused controls cover rejection before admission, import-return phase
preservation, reload-before/after compiler completion and immutable progress.
Native acceptance requires the public refresh path, actual callbacks, zero
errors/warnings and one observed compilation/reload cycle. Static bounds pass;
native acceptance is pending until the published package is adopted.
