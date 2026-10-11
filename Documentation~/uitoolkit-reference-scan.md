# UI element reference scan

## Investigation and Static Cost Ledger before executable writes

The DoomsdayDiary all-host audit admitted 21 UXML files and the legal
`Assets/GameResources` authoring root. Its serialized reference input is 1,937
files and 368,372,644 bytes; the largest file is the 21,169,780-byte Notebook
Body Sans font. The official request exceeded 120 seconds and the Editor main
thread could not service later commands. Preserve the exact request and first
timeout; a narrower root is not acceptance of this scope.

The existing name index performs three regular-expression scans of each
serialized file on the Editor thread and materializes every scalar candidate.
Its backtracking and allocation upper bounds are unknown: FAIL. The first
diagnostic runs those exact expressions under the installed Unity Mono runtime
outside the Editor, with a one-second per-match diagnostic timeout. Freeze the
two largest inputs: six regex passes, at most 12,000 ms total timeout checking
and startup allowance, one 21,169,780-byte source string at a time, no Unity
objects or asset mutation. Retain the first timeout or completed match count.
This diagnostic timeout is not a proposed production fallback.

Installed Unity 6000.6.4f1 Mono reproduces the expensive boundary: the font's
scalar pass takes 5,842 ms, and the first rig yields 197,522 scalar matches plus
27,263 list matches. Those candidates cannot be UI names in bulk, but the old
owner materializes each match and its token before testing membership. The
earlier .NET runtime observation does not substitute for this Mono witness.

## Production Static Cost Ledger before executable writes

Replace the three backtracking expressions with line-bounded quote/scalar
scanning. The name index owns a character trie built from the actual UXML
definitions; references consume ranges of source text directly. Its definition
count and longest-name cache are updated only by AddDefinition, before scanning
references. Terminal trie nodes own the original name and referenced bit. No
parallel definition/reference set or per-file cache remains.

Frozen input: 1,937 serialized files / 368,372,644 bytes, 21 UI hosts, and 271
distinct names of at most 26 characters. At most 7,047 trie nodes and 7,046
edges are created from those names. Serialized reference work is at most 16
forward character visits per input character (line split, quote ranges, scalar
key/value, whitespace and trie traversal), with no Cartesian product of files
and names, recursion, regex backtracking or candidate string allocation. Each
file is read once and its string is retired before the next file. The largest
UTF-16 source occupies less than 41 MiB; trie metadata is below 2 MiB. The
existing audit still owns its native asset and preview reads; this change adds
zero AssetDatabase calls, tasks, jobs or frame transitions. PASS against the
frozen 512 MiB source / 64 MiB reference-index working-set budget.

Regression inputs add at most three 2 MiB payloads in one 7 MiB fixture, below
32 MiB working storage. A small nonmatching control first initializes Mono's
character/runtime helpers; its first cold call reports 24 bytes, whereas the
measured production scan allocates zero. Test references before and after long blobs, repeated
quotes, whitespace, list/scalar fields, comments and empty fields. A bare line
after an empty YAML field must not become that field's reference. Native
acceptance uses the same full authoring scope, plus actual UI Prefab and runtime
consumers, and requires the full audit and self-tests to pass. The official
120-second request budget is verified by its terminal response; this ledger
does not infer elapsed time from average throughput.

The isolated lexical replay compiles the actual production index and scanner
with the installed Unity Mono compiler. It processes all 1,937 serialized files
and 368,372,644 bytes in 43,387 ms, with zero scan allocation. The font scan is
123 ms and the first rig scan is 60 ms. This is evidence for the source scan,
not a substitute for the official native full audit, compiler and package tests.
