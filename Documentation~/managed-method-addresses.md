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
metadata. The handler produces one immutable response. Its derived runtime ID
combines the native process ID/start ticks and current AppDomain ID; no cache or
new persistent lifetime state exists. A domain reload or process restart
retires the old identity. Capture callers consume and retain the response with
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

Validation uses a no-inline method's real current JIT address, a native/unmapped
address, stale identity rejection and strict contract admission. Four focused
tests in the existing Editor test assembly; no battle, deep profiling or broad
test suite. After adoption, the unchanged calibration witness is replayed and
its captured managed addresses are resolved in the same domain lifetime.
