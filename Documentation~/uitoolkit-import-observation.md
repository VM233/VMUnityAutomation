# Automatic audit import observation

Unity's `AssetPostprocessor.OnPostprocessAllAssets` publishes imported and moved
asset paths. `VmAutomationUIToolkitAuditPostprocessor` forwards that product to
the automatic audit coordinator, which owns scoped pending paths, content
fingerprints, debounce, audit results and Editor update subscriptions. The
existing USS, UXML, UI Prefab and theme consumers retain their audit semantics.
Automatic checks run after Unity imports a saved asset, including saves from UI
Builder and external edits imported by AssetDatabase. An unimported disk write
is not an imported authoring product.

Prefab imports qualify against the actual UXML name-reference and asset-reference
consumers. A domain-owned index retains whether each prefab contributed either
product, so removing the last binding, deleting a prefab or moving it out of scope
still audits its former consumers. Pure 3D geometry, stock bounds and material
changes do not run a full layout audit. Names and folders never classify a prefab
as UI. UXML/schema scope changes rebuild this index; unchanged idle updates do
not scan assets. The native import callback also forwards deletions and former
paths of moved assets.

## Prefab impact method and bounded acceptance

Source: Unity's documented `OnPostprocessAllAssets` imported/deleted/moved arrays,
and this package's real `BuildElementNameReferenceIndex`, generated-preview and
panel-nesting consumers. The confirmed failure is unconditional full UXML audit
for every imported prefab. Unlike delay tuning or disabling automatic audits,
classification preserves both previous and current consumer membership.

Static Cost Ledger: one baseline per Editor domain, UXML definition change or
asset-scope change, bounded to5000 prefabs,512 UXML files,128MiB aggregate text,
4MiB per file and8192 names/1MiB name characters. One shared name trie is reused for all prefab text;
scanning is linear in source characters and GUID matches. At the frozen consumer
census1197 prefabs/24,836,737 bytes and29 UXML/543,143 bytes/506 names, it reads
each file once. Retained memory is one boolean per prefab plus the bounded name
trie and UXML GUID set; text is retired per file. Each later prefab import reads
only that changed file, plus its existing SHA fingerprint; no native asset loads,
dependency graph traversal, worker or idle filesystem polling is added. Existing
layout audit scopes and limits remain unchanged. Bounds are explicit errors,
never a truncated successful classification.

Success: real 3D-only imports leave layout runCount unchanged, actual serialized
name/UXML references still trigger the full original audit, and binding removal,
deletion and moves retain that trigger. Failure: a contributing prefab is omitted
or unrelated repeated imports scan all layouts. Stop on either failure and revise
the owning classifier from the exact retained input; do not change the debounce,
audit roots or layout acceptance gates.

The additional native fixture owns one UXML and one BoxCollider/UIDocument prefab,
at most eight prefab saves/import changes plus one move and deletion. Six layout
waits admit at most1440 Editor updates; unchanged geometry waits admit60. It
restores the same exact original configuration and retires its own folder. The
name-trie control scans three fixed serialized strings and two definitions.

The first native run retains two240-update timeouts, including the unchanged
stylesheet fixture. Real production prefab imports subsequently reach the owner
(five actual full audits and changed3D imports around1.4s); the name-trie control
passes. Thus these timeouts do not establish a classifier rejection. The fixture
requested ForceUpdate without synchronous native import completion. Unity's
documented ForceSynchronousImport supplies that missing admission boundary.
Use both flags for the original imports, retain the240-update bounds and report
actual elapsed/config/status on any failure. Stop if this does not repair the
exact import witness; do not extend waits or alter production debounce.

The coordinator must not add a recursive `FileSystemWatcher` over Assets. Mono's
managed watcher can recursively enumerate directories and repeatedly inspect its
tracked file set even when no relevant asset changed. That duplicates native
import observation and makes unrelated assets part of automatic UI audit cost.
The public automatic status describes its `changeSource` as `asset-import`.
Configuration remains independently observed once per second; an import adopts
the current configuration before queueing its paths, so enabling and importing
within that interval cannot lose the notification.

Native evidence `5f1542b67c50467ab82207a6ff017611` froze a 749.9111 ms logic tick
in MarbleBattlers. Retired CPU recording `e6de3b38b62e4bfdbebc7886b23aafab`
covers the complete QPC window with zero lost events. The owning main thread has
33 CPU samples; another thread has 632, and 622 of its managed stack samples
resolve to `System.IO.DefaultWatcher.DoFiles` and its monitor. The current audit
owner reports enabled automatic audits and an active recursive Assets watcher.
This establishes the unwanted polling work; it does not by itself attribute all
wall time or every historic calibration stall to this owner.

## Static Cost Ledger before executable writes

PASS for the changed observation boundary. The repair removes the recursive
filesystem tree axis, background thread, concurrent event queue, watcher
construction and disposal. It adds no filesystem enumeration, timer, worker,
cache, native call or per-frame allocation. Native imported/moved path traversal,
scope matching, content fingerprints and actual audit loops are unchanged. The
configuration read at import already exists and supplies the adopted enable
state; ordinary idle updates retain at most one existing configuration read per
second. No simulation clock, sample order, warmup, threshold or performance gate
changes.

The focused native fixture owns one folder and two files, each below 4 KiB. It
admits at most six exact AssetDatabase imports, four configuration publications
and one folder deletion. Original configuration preservation is capped at
64 KiB. Each fixture audit indexes one USS, one UXML with two class consumers and
zero C# sources. Three bounded waits each admit at most 240 Editor updates;
unchanged/disabled controls admit at most 40 additional updates. The wait domain
is therefore at most 760 updates, with no blocking sleep or background work.
Status polling retains only the current small response; fixture byte/string
storage stays below 128 KiB excluding Unity's native importer and existing audit
parser. Cleanup restores the exact original configuration and removes only the
fixture-owned asset folder. Reload/quit detaches the single Editor subscription.

Validation uses the native import entry, repeated changed and unchanged imports,
disabled imports, and re-enabling immediately before import. The fixture imports
its stylesheet before the UXML that references it. The exact captured
calibration input must be replayed through its production entry, with unchanged
clocks and gates and a fresh runtime capture. A passing replay alone does not
prove that all original wall time was repaired.
