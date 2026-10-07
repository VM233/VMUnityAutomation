# Retained-frame method addresses

`profiler/frame-data` accepts up to sixteen `methodAddresses`. Each address is
an exact lowercase `0x` prefix followed by sixteen hexadecimal digits. Addresses
are strings so JSON consumers do not lose 64-bit precision. The retained frame's
native `FrameDataView.ResolveMethodInfo` publishes the address, method name,
source file name and source line in `resolvedMethods`. Missing native names stay
null. They are not inferred from a neighboring address, another process or a
different capture. An omitted request produces an empty array.

The caller must establish the capture's process and runtime lifetime before
querying addresses. Unity resolves symbols owned by the selected Profiler
session; an address from ETW is not thereby guaranteed to have a symbol in that
session. This interface adds observation, not a JIT or responsiveness repair.
No frames or recording settings are created or modified by this read. Existing
frame-range and unavailable-data outcomes apply to the whole result.

Entry and owner: the existing Profiler frame-data command. Producer: Unity's
native FrameDataView and its symbol metadata. Immutable product: one frame's
timings, counters and requested method information. Publication: the existing
executor/catalog and generated exact output schema. Consumers: bounded CLI
diagnostic callers. The existing using scope disposes the frame view before
return. Native failures use the existing command execution boundary. No cache,
P/Invoke, route, transport, assembly scan, pointer dereference or recorder.

Static Cost Ledger before executable writes: one optional array, at most sixteen
18-character addresses (576 bytes UTF-16), sixteen integer parses and sixteen
native method-info getters. At most sixteen fixed four-field result dictionaries
plus Unity's native symbol strings; serialization remains under the executor's
existing response-byte admission. No catalog, assembly or stack enumeration and
no data-dependent search added to managed code. The native symbol implementation
and strings belong to the existing Unity Profiler, as do existing native timing
getters. This read occurs outside simulation and adds no per-frame work. The
generator traverses the frozen 307-source / 6,572,657-byte Editor domain, below
the existing 8 MiB source budget; the change adds less than 8 KiB of source and
one closed four-property row schema. PASS for this added observation domain;
this ledger does not claim a bound on Unity's native symbol implementation.

Validation requires exact input admission (including rejection of numeric or
oversized addresses), generated closed schema, clean current-Main compilation,
package/code policy review, and a real native retained-frame read with capture
settings restored and frames retired. Native symbol availability is recorded
separately from successful contract execution.

The API is public in the declared Unity 2021.3+ support range:
[Unity reference binding](https://github.com/Unity-Technologies/UnityCsReference/blob/2021.3/Modules/ProfilerEditor/Public/FrameDataView.bindings.cs).
