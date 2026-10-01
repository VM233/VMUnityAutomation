# Native retained-frame counters

`profiler/frame-data` reads up to sixteen exact integer counter names from the
same native retained frame as its CPU hierarchy. Each entry publishes native
marker identity, whether a counter value exists in that frame, and its value
(null when absent). Decimal strings preserve every native int64 value. A missing
marker or unrecorded counter is not a zero value.
An omitted list returns an empty array. There is no marker catalog scan, cached
counter or reconstruction from CPU time or collider count.

`profiler/enable` accepts `profilePhysics2D` to control Unity's native Physics2D
category. The previous category switch is always returned with the other prior
settings, so capture owners restore it after exporting their retained frames.
Changing that category affects diagnostics only. It does not alter simulation.

The retained MarbleBattlers frame 111 reported 124.759 ms of FindNewContacts;
region snapshots exposed about 420 native shapes but did not publish broadphase
pairs, contact creation or destruction in that frame. This is an observation
gap, not evidence of a particular physics cause or a speedup.

Entry and owner remain the existing Profiler command owner. Unity's native
Profiler category and FrameDataView own recording and counter samples. The same
frame-data callback reads and publishes the complete product; its existing frame
view is disposed before return. Consumers and generated schemas adopt this one
product. Unexpected native getter failures retain the current execution boundary.
No extra route, recorder, frame scan, scene mutation, state cache or transport.

Static Cost Ledger before executable writes: <=16 marker-name lookups, <=16
flag reads, <=16 availability checks and <=16 integer reads per frame request,
<=64 native calls,
one sixteen-entry response below 8 KiB. The existing bounded hierarchy traversal
and native frame-view lifetime are unchanged. Category capture adds two constant
reads and at most one setter to the existing enable transaction, below 1 KiB.
Generator source discovery retains the 267-file package domain and adds one
closed counter-array shape; no new discovery axis. PASS. Acceptance is current
Main clean compilation, exact schema/code/dependency review and an owned bounded
capture proving absent versus recorded counter values and switch restoration.

Native API contracts are FrameDataView.GetMarkerId, HasCounterValue and
GetCounterValueAsLong, and Profiler.IsCategoryEnabled/SetCategoryEnabled. The
Physics2D counter names come from Unity's Physics2DProfilerModule, whose native
counter reads use the same long-valued product. Supported APIs are available in
the declared Unity 2021.3+ range; no version-based runtime fallback is introduced.
