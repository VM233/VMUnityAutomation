# Native Player window recording

Discover `player/record-window` in the live Automation catalog and read its typed
contract. Begin supplies the exact Windows Unity Player executable, PID and
round-trip UTC creation time returned by Player launch, an explicit client-pixel
rectangle, a new absolute MP4 output outside Assets, and an installed FFmpeg
executable supporting `gdigrab`'s `hwnd` input. Unity does not install an encoder.

The rectangle is relative to that owned window's client origin. It must fit the
actual client and satisfy the capture budget in the schema. One recorder runs per
Editor lifetime. The Player need not be focused or moved; this tool sends no input.
Native transparent-window capture can include composited pixels, so inspect the
resulting frames before claiming visual acceptance.

Keep Begin's session capability privately. Inspect and End require that capability
and the same exact Player identity. End asks FFmpeg to finalize normally; Inspect
must report `Succeeded`, exit code zero, nonzero output bytes and released
resources. Then inspect the MP4's actual frames and cadence with a media reader.
Process completion alone proves no gameplay behavior or visual result.

The adjacent `.recording.json` receipt identifies the HWND, client dimensions,
selected region and execution state. The encoder log is retained. Existing output,
receipt or log rejects admission. Domain reload, Editor quit, Player exit, window
replacement, client-size change or capture/finalization failure retires the exact
encoder and process leases. Interrupted recordings are explicitly incomplete;
recording never quits or kills the Player. No Asset or global preference changes.

The streaming capture budget is documented in
[the static cost ledger](player-window-recording-cost.md).
