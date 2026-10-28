# Player build observation

Discover the current build/start contract through the official Unity CLI facade.
The durable job owns BuildPipeline, optional native Player launch, observation,
window capture and optional termination. Retain its job identity and capability;
use the returned polling command to read the same job during a synchronous build.

The build receipt retains native Error, Assert and Exception messages and stacks
from the actual BuildPipeline invocation, along with the requested target and
output path. A missing native BuildReport is an explicit build_report_unavailable
failure, carrying Unity's diagnostic rather than a secondary null reference.
Thrown failures preserve exception type and evidence. Native diagnostic capture
is bounded and reports completeness; incomplete capture cannot pass. The scoped
log subscription is removed before result publication on every outcome.

After Unity finishes BuildPipeline, the completed build receipt is checkpointed
before launch. Player observation yields between Editor updates. Catalog and
other main-thread commands remain responsive during this phase. The native Unity
build itself still occupies the main thread; published job polling is independent.

runSeconds supports fractional seconds from 0 through 86400 (default 5).
Window discovery starts after sampling, with windowWaitMs from 0 through 60000
(default 5000). Sampling, window discovery and termination observation never
sleep on the Editor thread. The result publishes the exact launch identity,
phase, sampledAt and sampleElapsedSeconds, observed exit and screenshot. A native
nonzero exit before requested termination or a failed capture fails the job.
An explicitly requested kill is separately reported by terminatedAfter.

Cancellation during observation releases the process lease and leaves the Player
running unless termination had already been requested. Reload or shutdown fails
an interrupted job with retained build/launch evidence and an explicit uncertain
outcome; launch and kill are never replayed. An active job cannot be replaced by
clearStuck: that obsolete build argument has been removed. Poll or cancel its
existing identity instead.

playerArguments is a shell-free argument vector. playerLogPath selects an existing
absolute log directory and owns -logFile and readback. See
[argument limits](player-launch-arguments.md). Use player/launch for an existing
Windows build, and [player/quit](player-quit.md) for normal shutdown using the
returned exact OS identity when terminateAfter is false.

Log readback opens with read/write/delete sharing and reads at most the final
1 MiB as UTF-8. logTailLines admits 1..2000 (default 120); incomplete leading lines
are discarded. playerLogTailTruncated and playerLogBytesRead expose the bound.
No writer is closed to obtain a log tail. A native screenshot of a splash screen
or blank window does not prove application state.

See the [cost and lifecycle ledger](player-build-cost-ledger.md).
