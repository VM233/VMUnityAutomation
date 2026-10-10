# Automatic audit import observation

Unity's `AssetPostprocessor.OnPostprocessAllAssets` publishes imported and moved
asset paths. `VmAutomationUIToolkitAuditPostprocessor` forwards that product to
the automatic audit coordinator, which owns scoped pending paths, content
fingerprints, debounce, audit results and Editor update subscriptions. The
existing USS, UXML, UI Prefab and theme consumers retain their audit semantics.
Automatic checks run after Unity imports a saved asset, including saves from UI
Builder and external edits imported by AssetDatabase. An unimported disk write
is not an imported authoring product.

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
