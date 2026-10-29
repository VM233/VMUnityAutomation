# Continuous native frame capture

The transport-neutral Runtime owner measures one contiguous interval of Unity
frames. VM Unity Pipeline exposes it through Runtime-only CLI commands; the
owner opens no transport and does not change frame rate, time scale, resolution,
gameplay, or project settings. An instance exists only after capture admission.

## Measurement contract

A caller supplies a fresh capture UUID and the expected build GUID. The first
LateUpdate establishes a native `Time.frameCount` and `Stopwatch` baseline. Each
subsequent LateUpdate must advance exactly one native frame and have a strictly
increasing monotonic timestamp. The interval is end-to-end wall time between
these frame boundaries, including stalls and waits. It is not a GPU timing or a
measurement of CPU work alone. The requested 1–120,000 intervals contain no gaps;
an explicit incomplete result retains the observed prefix on cancellation,
disable, destruction, exit, frame discontinuity, or changed clock/geometry.

Capture identity includes the service lifetime UUID, capture UUID, build GUID,
Unity version, Editor/Player and development-build flags, native frame bounds,
time scale and screen dimensions. A captured interval proves only that exact
process lifetime and interval; callers must separately retain the official CLI
instance/build identity, battle context and full runtime Console interval.

One current session is retained. Start with the same UUID and identical inputs
observes the already admitted session; a conflicting active session is rejected.
After a terminal result a new UUID replaces it. Get/cancel require both UUIDs.
Terminal reports are immutable; canceled/failed/interrupted captures never report
completed. The disabled component has no per-frame sampling callback. No prior
session cache or file output is created.

## Static Cost Ledger (frozen before implementation)

| Axis | Hard bound and work |
| --- | --- |
| Sessions owned by the service | 1 current session, active or terminal |
| Native frame intervals | 120,000, with one baseline callback |
| Frame callback | O(1): one monotonic and one UTC clock read, frame/clock/geometry validation, arithmetic and at most one histogram increment; no scans, allocation, reflection or native asset/inventory calls on a valid nonterminal frame |
| Histogram | 100,001 int bins, 0.1 ms upper bounds through10,000 ms:400,004 bytes; overflow is a separate retained count |
| Replacement peak owned by the service | One old terminal plus one newly constructed session before assignment:2×400,004=800,008 histogram bytes, below1 MiB; afterwards only the new current session is retained |
| Terminal reduction | One100,001-bin pass resolves three ranks (P50/P90/P99), at most300,003 constant rank comparisons; one reduction on any terminal transition |
| Terminal retention | Same frozen histogram exposed only through a read-only collection; no second histogram copy and no raw-frame list |
| First rejection | One fixed five-scalar rejected sample alongside last valid bounds, no failure history or unbounded text |
| Progress polling | Constant scalar snapshot; no histogram reduction or copy |
| Explicit histogram output | 100,001 integers each at most120,000: at most700,007 compact JSON bytes plus bounded metadata, below1 MiB; no automatic inclusion in progress |
| Metadata | UUIDs fixed36 characters; supplied build GUID1–128 UTF-16 characters; other metadata is actual Unity scalar/version data, no arbitrary text payload |
| Threads and ownership | Unity main thread only; service creates/disables/destroys its own component, no thread, subscription, profiler handle, global setting or second transport |
| Focused pure tests | 19 declared cases, each at most256 callback invocations:19×256=4,864 bounded invocations; at most19×100,001=1,900,019 reduction visits, two full histogram sums and three full histogram serializations. At most19 retained400,004-byte histograms before collection, below8 MiB, plus under4 MiB JSON/DTO allowance |
| Verdict | Pass for the declared input domain; changed bins, frames, callbacks, reduction or retained products invalidate this ledger |

For bin i>0 the measured quantile lies in `(0.1*(i-1),0.1*i]` ms.
Maximum-threshold decisions use the upper bound. An overflow quantile has a
10,000 ms lower bound and unknown histogram upper bound. Exact observed maximum,
sum, count, mean and overflow count remain separate. Zero samples produce null
statistics, never a passing zero. Quantiles are not invented exact raw values.

The Runtime assembly uses Unity2021.3-compatible APIs and has no dependency on
the official Pipeline assembly. Runtime CLI registration and build stripping
remain the facade's responsibility. Native Editor tests cover the real owner;
formal CLI and a normal Release Player must validate adoption and measurement.
