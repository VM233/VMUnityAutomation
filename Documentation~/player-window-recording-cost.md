# Player recording static cost ledger

Admission freezes one exact live Unity Player process/window, one installed FFmpeg
child and one explicitly selected client rectangle. Region width/height are each
2..4096 and area is at most 4,194,304 pixels; duration is (0,30] seconds and native
capture rate is 1..60 Hz. Maximum capture work is 1800 frames / 7,549,747,200 pixels
on FFmpeg's worker. Input queue is four frames (64 MiB raw BGRA at the area cap).
Encoding is streaming; Unity retains only process handles, one result and a clock.
No frame arrays, asset scan, domain product or gameplay subscription exists.

Unity's update checks one retained process and one owned client rectangle, O(1)
per Editor update. Native capture and encoding never run on the Editor main
thread. The child has duration + 60 seconds total lifetime; End sends FFmpeg's
normal q command and allows 60 seconds to finalize. Reload, quit, target exit,
window replacement, client resize or failure retires the exact child and leases.
One session per Editor lifetime; a later completed result may be replaced only by
an explicit Begin. Output and receipt are outside Assets and never overwrite.

Validation: one typed catalog contract, six pure admission counterexamples and exact-identity native Player
recording, frame inspection and ffprobe. No global preferences or other Editor
instances are accessed. Budget: pass.
