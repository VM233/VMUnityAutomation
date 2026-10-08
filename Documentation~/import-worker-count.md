# Native import worker count

`asset/import-worker-count` sets `AssetDatabase.DesiredWorkerCount` for the
currently bound Editor session and calls `AssetDatabase.ForceToDesiredWorkerCount`.
The input is an explicit positive worker count from 1 through 128. The native
API can start missing workers and shut down idle surplus workers. Busy worker
retirement and OS process lifetime must be observed separately; a successful
result does not claim that a particular process has exited.

The entry is the typed project tool, its only resource owner is Unity's Asset
Database, and the result reads that owner's desired count after reconciliation
was requested. The invocation requires stable Main Edit Mode and exact project
binding. It writes no persistent Editor setting or project file, does not import
assets or compile scripts, and cannot target another Editor or arbitrary PID.
The session-only value retires with the Editor. Callers choose the count for
their workload; the command does not implement a calibration policy.

The API exists in the package's Unity 2021.3 minimum and in Unity 6000.6. Errors
identify an invalid count, unstable Editor or native desired-count mismatch.
Input validation and Main Editor admission occur before the native setter.

## Static Cost Ledger

PASS before executable writes. One explicit scalar input, two desired-count
reads, one setter and one native force call, with no asset enumeration, event
scan, hierarchy traversal, process search, polling, cache or nested data axis.
The scalar domain is 1..128; one result has four scalar fields and is under
1 KiB. Work runs once on Unity's main thread. Native process creation/shutdown
belongs to Unity, and the receipt reports the observed call duration rather
than predicting its completion time or claiming an OS memory saving.

The current consumer's focused validation first requests one worker from a
stable Main Editor, then confirms the result and exact previously identified
Main-child lifetimes. A second identical request proves idempotent count
readback. Invalid zero input must reject before state changes. No increase,
test scene, extra Editor, actor, Asset scan or package-wide test suite is needed.
