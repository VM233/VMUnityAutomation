# Game View resolution and preset lifetime

Use bounded official CLI catalog discovery for `gameview` and inspect the exact
command contract. Bind every invocation to the absolute requesting project path.
Presets are Unity preferences shared by all projects for the OS user. A project
path binding does not make them project-local.

`gameview/set-resolution` selects a matching existing FixedResolution. Builtin
matches precede custom matches. Width and height are integer pixels in 10..8192.
This command does not accept a label, create presets or call SaveToHDD. Missing
dimensions return `game_view_resolution_unavailable` with the temporary-session
workflow. Existing-window requirements never authorize opening a new Game View.

For arbitrary dimensions, capture `gameview/presets`, the original Game View state,
native window inventory and focus. Invoke `gameview/resolution-session` with
action Begin and width/height. Keep the returned sessionId capability. Resize uses
that identity and dimensions; at most one temporary custom entry exists regardless
of how many sizes are tested. Existing dimensions reuse their existing record.
Put End, with the sessionId and no dimensions, in the caller's finally block,
including failed or canceled verification. End restores the original native
selection object and zoom and removes the owned entry. Assembly reload, target
changes, Editor quit and view retirement invoke the same retirement owner.

The temporary session never saves the global preset table. Do not edit or save
the preset menu during it, since Unity's own menu save persists the entire native
table. Task names such as Codex/Test/Check are not permanent preset identities.
After End, compare the complete inventory fingerprint, preference-file bytes,
original selection and zoom, existing windows and focus. A restored size alone
does not prove cleanup. For absent Game View, use another existing suitable host
or report the missing visual evidence; do not create a view to satisfy a tool.

`gameview/remove-presets` is explicitly permanent OS-user-wide cleanup. Back up
the preference file, review attributable automation entries, then submit their
complete records from `gameview/presets`, expectedFingerprint and the catalog's
explicit confirmation. It admits at most 128 records, rejects stale/duplicate/
builtin/selected targets before mutation, removes in descending native indices,
remaps surviving selections by native identity, preserves zoom and saves through
Unity. Arbitrary labels, prefix matching and external YAML edits are not cleanup
contracts. Do not bulk-remove unknown user presets or operate other project
Editors solely because their preferences are shared.

The native APIs used here are present in the declared Unity 2021.3 and 6000.6
reference branches. The existing-window native regression runs in the requesting
Editor and creates no windows. Its exact supported-version runtime evidence must
be recorded independently from this source compatibility inspection.

## Static Cost Ledger

One admitted native group has at most 4096 entries, 32 existing Game Views and
128 exact removal records. Inventory performs at most 4096 record reads with
four property reads plus one group-size lookup each and a single linear SHA256
encoding. Native List removal moves at most 128*4096 elements; one identity map
restores at most 32 selections. Resize retains at most one transient entry and
performs at most two linear group scans. Synchronous Editor-thread budget is
100 ms per operation, managed peak <= 4 MiB. No Asset scan or window creation is
introduced. Test stimuli are 16 sequential dimensions with the same session.
PASS for these admitted axes; changing them requires a new ledger.
