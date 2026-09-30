# Registered package metadata

The frozen `packages/info` request for com.vm233.unity-automation in
MarbleBattlers on 2026-09-30 timed out after 30 seconds. Its immediate handler
starts Client.List and sleeps on the main Editor thread until that request
completes. Native Package Manager progress needs that thread to keep processing
updates, so this read can block the Editor indefinitely. The same loop is
reachable through packages/status with includeResolved and packages/lint-metas
with name or all. Four unregistered synchronous list/add/remove/search methods
retain the same mechanism despite having no source, route, test or document
consumer; remove them rather than keeping a second execution path.

Installed metadata belongs to PackageInfo.GetAllRegisteredPackages. The info,
resolved-status and metadata-root producers consume that native registration
snapshot directly. It is the actual installed product, including while another
package is being resolved. They do not start a list operation, cache a prior
snapshot, wait, retry, substitute manifest state or suppress a missing package.
The existing asynchronous list/search/add/remove routes retain their genuine
UPM request lifecycle and completion owner. Schema and normal response fields
remain unchanged. Package status adopts one registration product for the entire
request and looks up names in that product.

## Static Cost Ledger before executable writes

The frozen project has 70 registered packages and at most 70 manifest entries.
Info visits at most 70 registrations and projects one matching metadata object.
Status with resolved metadata performs one native snapshot read, builds at most
70 name keys and performs at most 70 lookups: 140 visits, replacing repeated UPM
requests and waits. Lint root selection visits at most 70 registered packages;
its existing asset traversal, result bound and metadata validation do not change.
Compatible-version and dependency projections already belong to the selected
UPM metadata product and are unchanged (the affected Git package has three
dependencies). There is no Cartesian product, asset import, native execution,
Physics work, subscription, per-frame work or new cache. Additional snapshot
and name-map scratch is below 64 KiB for this frozen registration domain.
No blocking sleep or process wait remains in the package command owner.
PASS for removing the unbounded main-thread wait; verify native info, resolved
status, lint by name and the asynchronous list route after adoption.
