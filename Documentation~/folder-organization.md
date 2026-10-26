# Scoped folder organization review

`asset/review-folder-organization` reads the team's policy at
`ProjectSettings/VMUnityAutomationFolderAudit.json`. If it is absent, the explicit
built-in policy inventories Assets itself (8 direct files / 16 subfolders), then
recursively reviews existing Assets/Scripts (24/32), Assets/Editor (24/24), and
Assets/Resources, Assets/GameResources, Assets/Game Resources (64/32 each).
These are five exact directory probes, not a recursive search for similarly
named folders inside plugins. `ConfigurationSource` reports `defaults` or
`project`. An authored file completely replaces defaults; invalid files fail.

Only admitted recursive roots are traversed. A shallow Assets root counts its
physical immediate entries, including vendor root directory entries, without
entering them. Crowding calls for reviewing project-owned classification, never
moving or restructuring external plugins. Packages, Library, build and external
trees stay outside the default recursive scope. Directory links are never
followed; a selected linked root is rejected. Authored excludes identify external
subtrees embedded inside otherwise owned script/resource scopes.

```json
{
  "schemaVersion": 1,
  "folders": [
    { "path": "Assets", "recursive": false, "maxDirectFiles": 8, "maxDirectSubfolders": 16 },
    { "path": "Assets/Scripts", "maxDirectFiles": 24, "maxDirectSubfolders": 24 },
    { "path": "Assets/Editor", "maxDirectFiles": 24, "maxDirectSubfolders": 24 },
    { "path": "Assets/GameResources", "maxDirectFiles": 64, "maxDirectSubfolders": 32 }
  ],
  "excludePaths": []
}
```

Each limit applies to immediate children, not the entire recursive subtree.
`recursive` is an optional boolean, default true. False admits only that folder,
not arbitrary descendant requests. An explicit recursive ancestor still owns
descendant coverage; a shallow nested rule changes only its own thresholds.
Unity `.meta` files do not count as additional content. Exclusions are directory
prefixes with path-segment boundaries; excluded subtrees do not contribute to
the parent count and are not traversed. More specific configured folders own
their own thresholds. Overlapping roots are visited once.

The request's optional `paths` narrows the review to existing directories inside
the configured scope. It cannot broaden the policy. `maxIssues` limits returned
findings, not the scan; counts cover the complete selected scope. Missing or
invalid authored configuration, roots, requested paths and work-budget overflow are
explicit failures. An empty or incomplete scan cannot report a clean pass.

A threshold finding asks for a responsibility/content classification review; it
does not establish that a homogeneous catalog must be split or authorize moving
files. Reports include the actual immediate counts, effective limits, source
policy folder and a bounded filename sample. When reorganizing Unity assets,
use the native asset owner to preserve GUIDs and references. Do not raise limits
or exclude an owned folder merely to hide a finding.

The current project may compose this owner into its normal validation command.
Agents should invoke the configured review after changes within those folders,
including asset-only changes. This tool is a read-only audit, not a background
file watcher and not an automatic file mover.

## Static Cost Ledger

Frozen bounds: 64 KiB configuration with JSON depth <=8 and duplicate-field rejection, 16 policy folders, 64 excluded directories,
64 requested directories, 1024 characters and 64 segments per relative path,
4096 visited directories, 32768 enumerated entries, 8 filename examples per
directory and 1..5000 returned findings. Thresholds are authored positive
integers (files <=10000, subfolders <=1000). Ordinary project content outside
configured roots is never enumerated. No asset contents or Unity objects are
loaded.

Default resolution adds at most six existence probes and no recursive discovery.
Each selected root and its ancestors have at most 64 link checks (<=5120 for 80
root/request paths). The scan uses one filesystem enumeration per visited
directory and one attribute query per admitted entry: <=4096 enumerations and
<=32768 entry queries. Directory-only exclusion matching is at most
4096*64=262144 prefix comparisons; effective-policy lookup is at most
2*4096*16=131072 comparisons for effective thresholds and recursive coverage.
No file-by-file Cartesian scan is added. Repeated
roots share the same visited set. Example retention performs at most 8 ordinal
comparisons per content file (<=262144 comparisons). Final issue ordering is
bounded by 4096 log2(4096) comparisons. Budget: pass.

The explicit CLI invocation runs synchronously on the Editor main thread;
there is no per-frame, import callback or runtime cost. The main-thread work
budget is the finite filesystem/query and comparison budget above. Scan
overflow fails with a narrow-scope instruction rather than silently truncating
coverage. Memory is bounded by the directory stack/visited set, <=32768 retained
example names and <=4096 findings, within a 64 MiB audit-state budget; file data
is not read. No cache, subscription, task, window or asset mutation survives a
call. Tests use a unique owned temporary directory and remove it in teardown.

Metadata-owner correction: before publication, a single committed-Git metadata
query captures existing identities (at most 2048 records). At most 2048 working
metadata files are compared once, with hash-set duplicate detection and a
dictionary for published identity/rename lookup; no record Cartesian product.
Only newly introduced GUIDs enter the replacement map. Reference publication
visits at most 8192 text files, at most 4 MiB each and 64 MiB combined, in this
authority package. One bounded GUID-token regex pass per text file performs
constant-time map lookups, rather than scanning each file once per replacement.
The map holds at most 2048 GUIDs; metadata records are at most 64 KiB each. No existing published identity may change. Tests use
one isolated Git package with five metadata cases and bounded native Git/PowerShell
calls. The unchanged published metadata and the pre-existing dirty file remain
outside the feature commit. Budget: pass.

Regression fixtures create at most 4100 directories or 32769 files for budget
rejection, in independent cases (no product of those axes). Link regression uses
one native PowerShell junction creation on Windows, bounded at ten seconds,
and removes that link before the owned fixture tree. Policy and boundary tests
otherwise visit at most four directories and twelve files each. Native package
integration uses the same source fixture and actual public CLI tool.
