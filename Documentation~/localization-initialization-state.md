# Localization initialization observation

`localization/initialization-state` reads the already cached initialization
handle on the active Localization Settings. It does not request initialization,
read the selected Locale, wait for completion, reset settings, or release handles.
Discover its current registered name and schema through the official catalog.

The snapshot distinguishes no settings, a default handle, an invalid retained
handle, and a valid operation. It includes the current blocking operation and
the Addressables initialization dependency when the installed Localization
operation publishes those fields. An unsupported internal contract is an
explicit capability error; it does not start an alternative operation.

AssetBundle unload tracking is reported separately when that installed
ResourceManager provides it. Absence is represented by unavailable/null values,
not by zero pending work. No bundle keys or loaded asset contents are returned.

## Static cost ledger

One active-settings lookup and one cached-handle field read. At most three
operation snapshots (root, current stage, Addressables dependency), with no
dependency-graph traversal, downloads, wait, acquisition, or resource release.
The installed unload tracker is inspected only after its count is checked;
at most128 entries and128 native `isDone` reads. Asset paths are bounded at4096
characters and type names at512; oversized values fail explicitly. No exception
messages, bundle names, virtual progress getters or dependency graphs are read.
The complete result is bounded below64 KiB. All work stays on the existing
Editor executor thread; there is no subscription, retained mutable state or
cache. The input is empty and the output has a strict schema. PASS before code.

Regression must prove an unstarted query leaves the handle unstarted, invalid
handles remain distinguishable, completed failure remains failure, pending
operation inspection does not execute it, and handle reference counts do not
change. The formal executor path must be exercised in the affected project.
