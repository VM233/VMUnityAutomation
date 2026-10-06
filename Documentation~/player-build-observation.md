# Player build observation

Use the official Unity CLI facade to discover `vm_auto_build_start` and its
current input contract. Its durable Player build job owns build output, optional
Player launch, observation, window capture, and optional termination.

When `run` is enabled, `runSeconds` is the observation interval after launch.
An enabled window capture occurs after that interval and before termination.
The run result publishes `sampledAt` and `sampleElapsedSeconds` alongside the
launch timestamp, Player log, process state, and screenshot. Window capture
failure fails the job even when the build itself succeeded. The process handle
is released after publishing those values; disabling termination leaves the
Player running.

`playerArguments` is an argument vector passed directly to the native Player,
without a shell. `playerLogPath` optionally selects the absolute log destination
and owns both the `-logFile` switch and returned log readback. The default log
path is preserved when that field is omitted. Arguments are validated before
build admission. See [argument limits and quoting](player-launch-arguments.md).
Use the typed `player/launch` contract to launch an existing Windows build with
arguments without rebuilding; that contract returns immediately and hands the
live process to the caller.

Poll the job through its declared route for a terminal BuildReport. Unity's
synchronous build can prevent main-thread Pipeline queries while it is running;
a query timeout is not build completion. A screenshot of a splash screen or
blank pixels cannot prove the requested application state, even when native
capture returned success.
