# Live Mono method addresses

`profiler/managed-runtime` is a read-only Windows Editor command. An empty
request observes the current process and managed-domain identity. A resolution
request requires that exact `expectedRuntimeId` and one to sixteen unique
lowercase 64-bit `methodAddresses`. Observe the identity before a native capture,
and resolve its addresses before compiling, reloading the domain or restarting
the Editor. A stale identity returns `managed_runtime_changed` before lookup.

This command queries Mono's public JIT information table in the selected Editor.
It does not attach a debugger, enable profiling, JIT a requested method, evaluate
code or register another transport. A native address or trampoline has
`resolved: false` and null method fields. A resolved method includes its native
code range, metadata token and Mono-owned namespace, class, method and image
names. Generic names identify the metadata owner; they are not reconstructed
closed generic signatures. Source lines and native-module PDB symbols belong to
their existing owners and are not inferred here.

Entry and publication: the official CLI/Pipeline invokes the single Automation
route and its source-generated closed contract. Mono owns code membership and
metadata. The handler produces one immutable response. Its runtime ID combines
the native process ID/start ticks and an immutable GUID minted once for the
loaded managed domain. Unity can reuse an AppDomain number after reload, so
`managedDomainId` is descriptive and does not own lifetime admission. Reloading
the domain retires its GUID; disabling domain reload preserves it. There is no
cross-domain cache or persisted generation counter. Capture callers consume and retain the response with
their original native evidence. Other Editor platforms explicitly return
`capability_unavailable`.

Static Cost Ledger before executable writes: at most sixteen exact addresses,
sixteen native JIT table lookups and 144 metadata/range reads. No assembly,
method, object, asset, frame or trampoline enumeration, recursion or invocation
of a requested method. Mono owns each indexed native table lookup. Four native
UTF-8 names per resolved address are each limited to 1,024 bytes, giving at most
65,536 byte reads and 128 KiB of managed name characters. Output plus scratch
is below 512 KiB. One synchronous Editor invocation with the existing 30-second
deadline; no background thread or retained product. Native pointers are read
only after the JIT owner returns a method, never from caller-provided memory.
PASS for this finite input domain.

Lifetime repair Static Cost Ledger before executable writes: PASS. One GUID
allocation per loaded domain, below 128 bytes, plus one fixed string formatted
per query. Runtime identity remains at most 63 characters: ten PID digits,
nineteen process-start tick digits, two separators and 32 GUID digits. No
additional native lookup, scan, per-frame callback, thread or persisted state.
Schema construction replaces one literal pattern. The focused regression checks
the original recycled-domain identity through public input admission; native
acceptance retains a real identity before a proven reload and rejects it after
reload, even if Unity reuses its domain number. Existing address bounds remain
unchanged. A failed pre-repair native request accepted domain 5 from an older
capture after domain 5 was reused; its response is retained as failure evidence.

Validation uses a no-inline method's real current JIT address, a native/unmapped
address, stale identity rejection and strict contract admission. Five focused
direct tests plus one real executor regression in the existing Editor test assembly;
no battle, deep profiling or broad
test suite. After adoption, the unchanged calibration witness is replayed and
its captured managed addresses are resolved in the same domain lifetime.

The executor adds its `_agentId` context after validating authored arguments.
Identity versus resolution is selected solely by the schema's `methodAddresses`
field, never by the total internal argument dictionary count. The executor
regression covers empty identity, exact resolution and stale identity through
that same production admission chain. The additional three bounded read calls
retain no product or state beyond their test scope.

The command's published schema closes properties once at the top level, where
the catalog adds its common execution metadata. Identity/resolution variants
only constrain presence of the two authored fields. Closing each variant's
business properties separately would reject the catalog's `expectedProjectPath`.
The executor regression supplies that exact absolute binding on every call.
