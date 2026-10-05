# JSON enum contracts

Typed Automation contracts use declared enum JSON names, including names supplied
by `VmJsonEnumValue`. Ordinary enums admit one declared name. Flags enums admit a
comma-and-space separated sequence of declared names; schema generation publishes
the same string pattern and an `x-vmAutomationFlags` annotation. Formatting uses
the CLR's declared flag decomposition and maps every member to its JSON name.
Unknown bits, numeric spellings and unknown member names remain contract errors.

This applies to typed request binding and result transport, including enums
nested in GamePrefab update/readback products. A flags combination does not need
an additional named composite member. No combinations are enumerated when
generating the schema.

Static Cost Ledger before executable writes: the regression fixture has four
enum types, each with at most four declared fields and a 64-bit underlying value.
Schema generation visits F fields once; formatting visits at most F decomposed
names with F lookups, and binding visits at most F supplied names with F lookups.
For production types F is fixed by the compiled enum metadata; no data-driven
world or asset axis is introduced. Work is O(F squared), allocation O(F), on the
existing synchronous contract owner. At the witnessed F=3, at most nine field
comparisons and three output names are added. Pass for the existing contract
transport budget. The input pattern repeats at most F names and never creates
the exponential set of possible combinations.

Focused coverage: ordinary enum rejection, composite flags transport/binding,
JSON member names, nullable and nested products, undefined bits, and signed
64-bit flags. Consumer acceptance repeats the actual combined-station GamePrefab
readback and production survival report through the official CLI.
