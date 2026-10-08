# Normal Player shutdown

`player/quit` requests normal closure of an existing Windows Unity Player. Supply
the exact absolute `executablePath`, `processId` and OS `startedAt` returned by
`player/launch` or `build/start` with `terminateAfter: false`. The latter now
publishes the actual OS start time instead of a timestamp taken before launch.

The command returns a durable `player-quit` job and access token immediately.
The first authorized `jobs/get` poll adopts it. Before sending one native
Windows `WM_CLOSE` request, the owner acquires a process handle and verifies the
path and creation time against that handle. A stale PID or mismatched identity
is rejected without closing any window. Only existing Unity Player executables
are eligible; the Editor and Steam client are outside this contract.

Poll the same job to its terminal state. A successful result contains the
verified identity, accepted close request, native exit code and exit time.
OS `WaitForExit(0)` supplies exit evidence without blocking the Editor. It does
not prove application-specific cleanup; inspect the Player's isolated log or
application receipt for that evidence. `timeoutMs` defaults to 15000 and must
be an integer from 100 through 60000. A missing/disabled main window or timeout
fails explicitly and leaves the process alone. There is no kill fallback.

Cancellation is available before the close-request boundary. Once requested,
closure cannot be retracted. All success and failure paths release the owned
handle. Before domain reload or Editor shutdown, the owner releases its handle;
an interrupted close request becomes a terminal uncertain outcome after reload.
It is never replayed and never described as a confirmed application exit.

## Static Cost Ledger

Frozen input: one existing Player, one PID, one UTC creation timestamp, one
absolute path of at most 4096 UTF-16 units, and timeout T <= 60000 ms. There is
one identity acquisition, one native main-window query, one window-owner PID
query, one enabled-state query and one native message post. Exit probes are separated
by at least 100 monotonic milliseconds, with one final deadline probe: at most
602 zero-time waits. Each Editor update performs fixed arithmetic; no process
list, asset scan, worker, sleep or blocking wait is introduced. The existing
workspace owner serializes jobs, so at most one quit process handle and less
than 64 KiB of additional owned request/session storage are retained. Disposal occurs
at the terminal or assembly/application lifecycle boundary. PASS.

The route generator retains its existing source/route bounds. This change adds
one route and two first-party C# files. Tests use one current-process identity
per case and fixed admission/cancellation/reload witnesses; no test sends a
close request to the Editor. A separate formal CLI smoke uses one real Player,
one identity-mismatch control and its exact normal shutdown.

## Unity Mono close behavior

The frozen first Player smoke returned exit code -2 and no Steam shutdown
receipt when using `Process.CloseMainWindow`. Mono's implementation calls
`TerminateProcess(handle, -2)` directly. The Windows request owner therefore
posts `WM_CLOSE` to an enabled window whose PID matches the held process identity;
it does not use that managed close API. Sources:
[Mono Process](https://github.com/mono/mono/blob/main/mcs/class/System/System.Diagnostics/Process.cs),
[native PostMessage](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-postmessagew).
