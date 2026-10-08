# Player launch arguments

`player/launch` and `build/start` accept an explicit Player argument vector.
The official CLI's Editor launch arguments do not replace that Player contract;
discover and invoke the Automation route to pass arguments to the executable.

`player/launch` launches an existing Windows Unity Player once and returns its
actual OS process identity and log destination. It does not build, sample, wait,
capture, terminate, or own a background job. The caller adopts the live process
and can request normal application shutdown through the durable
[`player/quit` contract](player-quit.md). `build/start` reuses the
same argument encoder with its existing observation and process lifecycle.
Both contracts expose `playerArguments` as an argument vector. `playerLogPath`
is the sole owner of `-logFile`; specifying that switch in the vector is invalid.
The typed catalog remains the input/output authority.
Discovery names identify tools; effect and binding checks consume the canonical
execution route returned in the catalog contract.

The encoder uses the Windows CRT quoting rules: quotes delimit every argument,
backslashes before quotes and at the closing delimiter are doubled. No shell
is invoked. The implementation uses APIs present in the package's declared
Unity 2021.3 baseline. Native OS parsing, invalid admission and actual Player
log creation are independent acceptance evidence.

## Static Cost Ledger

Admission freezes at 64 caller arguments, 4096 UTF-16 code units per argument,
4096 per executable/log path and 32760 total quoted command-line code units,
including the executable and separators. Raw request strings occupy at most
540672 bytes; references and the result vector occupy below 2 KiB. Exact quoted
length calculation visits at most 270336 raw code units per invocation. Accepted encoding
visits at most 32760 code units once and emits at most 32760; its fixed-size
StringBuilder and output string occupy below 132 KiB together. Peak additional
managed storage including admitted request strings is below 1 MiB. There is
one native Process.Start and one process-identity read per launch, no loop over
processes, wait, retry, retained cache or main-thread update callback. Rejection
occurs before launching a process. A build encodes once at admission and once
when adopting its saved launch request: at most 540672 raw length observations,
65520 accepted encoding observations and two transient strings, released in
separate phases. Array decoding visits at most 64 entries per phase. Peak
storage remains below 1 MiB. Pass.

Tests use a fixed eight-argument Windows parser witness and five invalid-domain
witnesses, one native parse and no process per unit test. The Player smoke test
uses one existing frozen QA binary and one isolated log file; successful launch
does not itself establish successful startup, shutdown or leak freedom.

Sources: [Windows CRT command-line parsing](https://learn.microsoft.com/en-us/cpp/c-language/parsing-c-command-line-arguments).
