# Native RenderDoc capture

The rendering-cost investigation needs native D3D11 pipeline counters and a
replayable frame. Unity Profiler pass durations and submitted index counts do
not identify vertex, pixel or memory bottlenecks. This capability captures
evidence; it does not change game rendering or qualify performance acceptance.

Sources: Unity 6000.6 `UnityEditorInternal.RenderDoc` public bindings and
RenderDoc's versioned application API (`renderdoc_app.h`, API 1.6). Unity owns
device integration and capture begin/end; RenderDoc owns capture files. The
automation owner supplies admission, a bounded asynchronous lifetime, previous
capture-path restoration and an explicit artifact receipt.

Four routes provide status, Edit Mode device loading, one-frame capture and cancellation
from an explicitly identified existing Game View. Loading requires saved,
unmodified scenes and idle Edit Mode. Capture requires active Play Mode and a
loaded, supported RenderDoc device. It never creates/focuses a window, changes
resolution or starts playback. Output is a new `.rdc` file under project Logs.
Unity may launch its external RenderDoc target-control application as part of
the native integration; no automation controls that application's interface.

Budget: one active capture, one existing window, one Unity frame followed by at
most 30 seconds waiting for native file publication; O(1) main-thread polling,
no game collections, workers, persistent buffers or frame-history retention.
Native capture overhead is excluded from all performance windows. Existing
capture path is restored on success, failure, cancellation, reload and shutdown.
No rendering optimization or claimed hardware result is permitted if capture,
replay or counter availability fails. Replay timestamps are instrumented
observations and must not replace live frame timing.

Validation requires contract/path admission checks, actual load/status/capture,
a nonempty replayable artifact containing the intended game camera passes,
and restoration of the native capture path. Unsupported hardware counters must
be reported as unavailable rather than synthesized from mesh sizes.
