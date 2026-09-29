# Persistent job CPU stages

Ordinary Editor profiling separates durable project-job execution into
`VMUnityAutomation.Job.Clone`, `VMUnityAutomation.ProjectTool.Step`,
`VMUnityAutomation.ProjectTool.ValidateInput`,
`VMUnityAutomation.JobHistory.Record` and `VMUnityAutomation.JobStore.Publish`.
Use `profileEditor: true` and `deepProfiling: false`. The nested stage times
distinguish request isolation, input validation, the handler, history publication
and atomic disk persistence. Inclusive times overlap; do not sum parent and child
samples. Capture a short window and retire exported frames through
`profiler/enable` with explicit `clearFrames`.

The entry is the existing persistent runner. Execution state remains owned by
that runner, immutable public snapshots by JobHistory and atomic JSON files by
JobRecordStore. Profiler markers read those existing boundaries without changing
requests, progress cadence, publication, cancellation, cleanup or durability.
Unity's Profiler is the sole observation owner, and the existing frame-data
command consumes its retained samples. Markers retain no job data.

Static Cost Ledger before executable writes: five immutable domain-lifetime
marker handles, one stack-only Auto scope per existing invocation of these five
boundaries, and no added loop, reflection, dictionary, serialization, file access,
Unity-object lookup, asset scan, thread or retained state. A normal pending step
enters one project step, one input validation, four JSON-clone boundaries, one
history record and two record-store publications: nine scopes. The frozen
200-execution/200-history input and existing linear store traversal are unchanged.
Additional managed allocation is zero after domain initialization; each active
scope is one small value type on the calling stack. PASS for the unchanged work
domain. Marker timing identifies the next owner to investigate; it is not a
responsiveness fix or proof of a frame-time limit.
