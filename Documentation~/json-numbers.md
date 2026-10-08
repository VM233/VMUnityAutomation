# JSON number identity

The frozen MarbleBattlers request uses `57.21696670694649` (binary64 bits
`404c9bc590a753f7`). The current Unity Mono `Double.Parse`, `Utf8Parser`, and
Newtonsoft reader instead return `404c9bc590a753f8`. The immutable native report
therefore differs from its admitted request and website import correctly rejects it.

Entry points are CLI argument materialization and MiniJson persistence/copy reads.
`VmJsonNumber` owns conversion of numeric lexemes to finite IEEE-754 binary64.
The CLI's existing Newtonsoft reader owns JSON syntax, duplicate-key and document
validation; its numeric tokens supply source coordinates, not parsed numeric values.
MiniJson supplies its own numeric lexemes to the same converter. Persisted jobs,
request fingerprints, project tools and published reports consume that one numeric
product. There is no tolerance, request rewriting, report substitution or source alias.

Conversion forms an exact integer numerator and power-of-ten denominator, then
rounds their quotient to the nearest binary64 significand, with ties to even.
Normal and subnormal spacing and carry into the next exponent are explicit.
Overflow cannot publish a non-finite JSON number; underflow preserves signed zero.
Formatting emits `-0.0` for negative zero. The same formatter serves persistence
and canonical request fingerprints. Serialization and cloning propagate errors
at their owner boundary rather than publishing null or replacing an object with text.
At most 769 significant digits are retained, with a sticky bit for discarded nonzero
digits. Every binary64 rounding midpoint has at most 768 significant decimal digits:
its odd numerator is below 2^54 and its denominator is at most 2^1075. The retained
decimal interval therefore cannot contain a midpoint in its interior. A sticky tail
only changes the decision when the retained prefix is exactly a midpoint.

Static Cost Ledger before executable writes: input lexemes are traversed once;
integer tokens retain the existing Int32/Int64 representation without BigInteger
work. A floating token retains at most 769 digits. Powers of ten are at most 1093;
rounding shifts at most 1074 bits. Numerator, denominator and shifted operands
remain below 5120 bits. One division and at most two midpoint comparisons replace
the inaccurate platform float parse. Live storage is below 16 KiB per token, with
no cache, Unity API, I/O, thread dispatch or numeric Cartesian product. The frozen
16-pivot/4-offer request has 20 scientific numeric tokens; worst conversion work is
20 bounded divisions and below 320 KiB temporary allocation. General document
cost is additive in its existing admitted tokens and characters. CLI numeric source
positions are found in one forward source traversal, rather than rescanning the
document for each value. Tests use a fixed 1024-token oracle and 16 persistence
cycles, outside Play Mode. PASS; actual callback timing remains separately observed.

Focused acceptance compares IEEE bits with an independent Node binary64 oracle,
checks midpoint/subnormal/overflow cases, repeats persistence reads, and exercises
the official CLI request path. Compiler success alone does not prove number identity.

The existing deterministic package-GUID command also identified fourteen previously
noncanonical Editor/test metas. Its reconciliation changes only meta identity;
there are no package text references or MarbleBattlers serialized references to
their old GUIDs. Existing unrelated worktree files remain outside this publication.
