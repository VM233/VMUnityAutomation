# Retained native CPU sample timeline

`profiler/frame-data` can return a `rawSamples` page from one explicit stopped
frame and thread. The request supplies `offset` and `limit` (1 through1024).
After the first page, supply the returned `identity` as `expectedIdentity`.
The command rejects active recording, implicit frame selection, stale identity,
and offsets beyond the native sample count. It never starts recording, clears
history, changes windows, or caches a second capture.

`rawTimeline` is null when not requested. A requested page preserves native
sample order and indices, marker ids, recursive child counts and flags. Start
and duration nanoseconds, frame times and native thread id use decimal strings
to preserve unsigned64 values in JSON consumers. `nextOffset` is null only at
the end. CPU timestamps share Unity's native monotonic clock within the capture;
they are not GPU timestamps, execution-unit counters or proof of GPU overlap.
Do not subtract a render-thread wait from a GPU scope using CPU timestamps alone.

Native samples can have a null name, including render-thread samples. Preserve
that null in the typed output instead of filtering the sample or inventing a
label. The name bound applies only to non-null strings. The retained frame423
render-thread export exposed the former unconditional length dereference.
This correction adds one fixed null comparison per sample and no new scan,
allocation, resource or wait; the existing page and memory bounds still pass.
Its regression covers null, the512-character boundary and rejected513-character
names, followed by the same retained render-thread export after adoption.

The native RawFrameDataView owns the immutable samples. The existing command
owns each disposed view and page; executor owns validation and errors. Identity
combines native thread id, frame start/duration and sample count, so a different
retained frame cannot silently replace a continued page. Retiring history remains
the caller's responsibility after all required pages have been exported.

Static Cost Ledger (before executable writes): one raw view per requested page,
at most1024 sequential samples, six native getters per sample (6144 total), and
constant header reads. No preceding-sample scan, hierarchy reconstruction,
recursive walk, thread enumeration or extra frame scan. Names are bounded at512
characters with explicit failure; sample payload plus dictionaries and serialized
text have a4MiB per-call budget. No runtime/player work, persistent cache, new
GPU resource or Job; existing hierarchy bounds are unchanged. PASS. Tests cover
admission, exact paging including empty/end pages and stale identities; actual
CLI export is compared against native hierarchy totals and sample calls.

The native fixture observes a newly published valid frame before stopping
recording, bounded by120 Editor updates and10 seconds (at most120 header reads).
Three coroutine yields did not establish native publication in the first run;
the rejected empty capture remains a test-harness counterexample. The fixture
does not infer a frame from elapsed time or relax any sample/parity assertion.

The public nanosecond getters and native recursive child counts exist in the
declared [Unity2021.3 reference API](https://github.com/Unity-Technologies/UnityCsReference/blob/2021.3/Modules/ProfilerEditor/Public/RawFrameDataView.bindings.cs).
