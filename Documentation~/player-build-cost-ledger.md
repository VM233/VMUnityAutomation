# Player build observation cost ledger

Admission: one build job and one native process lease. Observation duration is
0..86400 seconds, window discovery 0..60000 ms, log tail 1..2000 lines. The
observation scheduler performs one constant-time process observation per Editor
update, no sleeps, synchronous exit waits, retries or background Unity calls.
Only phase transitions and terminal results publish durable state. Cancellation
and domain retirement release the lease; neither replays launch nor kills a
process. The build receipt and exact launched identity remain in the checkpoint.

The existing synchronous Unity BuildPipeline call remains one main-thread call.
Native window capture remains one main-thread capture of the selected window;
its pixel cost is unchanged. Log readback examines at most the final 1 MiB of
one file and returns at most 2000 complete lines, with explicit truncation
metadata. UTF-8 decoding and splitting are O(1 MiB), with a fixed buffer and
bounded decoded string/line array. There is no full-log traversal or cache.

Lifecycle: queued -> building -> observing -> optional window discovery ->
optional terminating -> terminal. Early native nonzero exit fails observation;
an explicitly requested kill is reported separately. Reload fails an interrupted
job with retained evidence and an uncertain-outcome explanation; launch and kill
are never replayed. A cancellation during observation leaves the Player running.
PASS: waiting contributes no blocking main-thread time or per-update file writes.

Regression axes are independent: numeric admission boundaries, fractional
duration, sample deadline, delayed/absent window, native early exit, termination,
cancel/reload lease release, bounded shared-writer log tail, and public catalog
responsiveness throughout a real Player observation interval.

## Native build diagnostic capture

One synchronous BuildPipeline call owns one scoped native log subscription. Only
Error, Assert and Exception events are retained. Each event is examined once;
retention admits at most 64 complete entries and 65536 UTF-16 characters across
condition and stack text (at most 128 KiB retained character storage, excluding
fixed object overhead). Serialization visits at most 64 entries. Callback work
is O(1) per event, with no file or Unity API calls, no scanning or retries. A
lock owns cross-thread publication. The subscription is removed in finally on
success and failure. Overflow is explicit and cannot produce a passing receipt.
Native BuildPipeline CPU/build allocations are unchanged. PASS: fixed retention,
no extra build invocation and bounded diagnostic publication.

## Requested build selection lifetime

One build owns one constant-size selection scope: capture selected group and
Standalone target, set the group/desktop selection for the actual requested
BuildTarget, invoke BuildPipeline once, and restore both selections in finally.
There are no scans, loops, asynchronous callbacks or caches. The immutable receipt
retains before/applied values and restoration. This is independent of active
platform import/reload and persistent Build Profile adoption. PASS: O(1) time and
space outside the unchanged native build; success and exceptions both retire it.

## Extra scripting defines

The closed build/start schema admits at most64 strings of1..128 UTF-16 code
units. BuildPlayerOptions forwards that one array unchanged; one array copy
examines at most8192 characters/<32KiB transient and no new Unity call or frame
work. The immutable build receipt records the actual requested array. PASS: O(64)
constant bounded preparation, unchanged one native BuildPipeline invocation.
