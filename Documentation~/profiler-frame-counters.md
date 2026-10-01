# Native retained-frame counters

`profiler/frame-data` accepts up to sixteen `counterQueries`, each containing
an exact native `category` and `name`. It publishes those identities and Unity's
verbatim `formattedValue` from that retained frame. This is a native display
string, not a lossless integer serialization; consumers must retain its units
and formatting. Missing counters retain the native missing-value string/null.
An omitted query list produces an empty array.

`profiler/enable` accepts `profilePhysics2D` to control Unity's native Physics2D
category. The previous category switch is always returned with the other prior
settings, so capture owners restore it after exporting their retained frames.
Changing that category affects diagnostics only. It does not alter simulation.

Name-only marker lookup is unsuitable here: Physics and Physics2D both contain
`Dynamic Bodies`. The retired name-only product could select the other category
and publish an apparently valid zero. Category-qualified reads use the public
`ProfilerDriver.GetFormattedCounterValue(frame, category, name)` API directly.
There is no marker catalog scan, caller-inferred marker identity, cached counter
or reconstruction from CPU time or collider count.

Counters reflect Unity's own publication phase; an Editor-loop manual simulation
is not guaranteed to publish counters in the same phase as its CPU samples. A
zero simulation counter must not negate a recorded Physics2D.Simulate CPU sample.

Entry and owner remain the existing Profiler command owner. Unity's native
Profiler category and FrameDataView own recording and counter samples. The same
frame-data callback reads and publishes the complete product; its existing frame
view is disposed before return. Consumers and generated schemas adopt this one
product. Unexpected native getter failures retain the current execution boundary.
No extra route, recorder, frame scan, scene mutation, state cache or transport.

Static Cost Ledger before executable writes: at most sixteen category/name
queries, each identity at most 128 characters, and sixteen native formatted-value
calls. The response has at most sixteen entries; identities use <=8 KiB and native
display strings are returned verbatim. No managed catalog materialization or
data-dependent marker scan. Existing bounded CPU traversal and frame lifetime
are unchanged. Category capture still adds two constant reads and at most one
setter. Generator retains the previously audited 267-file source domain. PASS.
Acceptance is current Main compilation, closed schema/code/dependency review,
and a native recording distinguishing the two Dynamic Bodies categories and
restoring all owned capture settings.

The category-qualified API exists throughout the declared Unity 2021.3+ range:
[Unity reference binding](https://github.com/Unity-Technologies/UnityCsReference/blob/2021.3/Modules/ProfilerEditor/Public/ProfilerAPI.bindings.cs).
