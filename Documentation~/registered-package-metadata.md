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
The registered installed-package list consumes the same native product;
search/add/remove use the [durable native request lifecycle](package-request-jobs.md).
Their initial response is a job receipt; registered metadata responses remain
unchanged. Package status adopts one registration product for the entire
request and looks up names in that product.

## Git revision adoption on older Editors

Unity 2022's cache package.json does not contain `_fingerprint`. Git target
adoption therefore reads the resolved commit from the registered package's
native `PackageInfo.git.hash`. The manifest revision, lock hash, registered
identifier and native commit must agree. An existing cache fingerprint remains
subject to its root-package or Git-subpath checks; a conflicting fingerprint
cannot be hidden by a matching native commit.

The resolution receipt and native package-state capture include `resolvedGitHash`
alongside the observed fingerprint. Neither value is synthesized or written to
the cache. The same adoption owner handles package update and resolve jobs.

Unity's [GitInfo documentation](https://docs.unity3d.com/cn/current/ScriptReference/PackageManager.GitInfo.html)
defines hash as the resolved commit; its
[2022.3 PackageInfo implementation](https://github.com/Unity-Technologies/UnityCsReference/blob/2022.3/Modules/PackageManager/Editor/Managed/PackageInfo.cs)
exposes that native Git product.

The change adds one scalar native property read per expected package to the
existing registration snapshot and cache-manifest read. It adds no request,
enumeration or event subscription. Focused regression adds two missing-fingerprint
cases (root and subpath, each checking exact/missing/stale native commits) and
two conflicting or missing native-commit cases. Existing stale-fingerprint checks remain.
Static Cost Ledger: PASS.

## Static Cost Ledger before executable writes

The frozen native startup reports 90 registered packages and 70 manifest entries.
Info visits at most 90 registrations and projects one matching metadata object.
Status with resolved metadata performs one native snapshot read, builds at most
90 name keys and performs at most 70 lookups: 160 visits, replacing repeated UPM
requests and waits. Lint root selection visits at most 90 registered packages;
its existing asset traversal, result bound and metadata validation do not change.
Compatible-version and dependency projections already belong to the selected
UPM metadata product and are unchanged (the affected Git package has three
dependencies). There is no Cartesian product, asset import, native execution,
Physics work, subscription, per-frame work or new cache. Additional snapshot
and name-map scratch is below 64 KiB for this frozen registration domain.
No blocking sleep or process wait remains in the package command owner.
The installed list projects 90 existing metadata records, sorts at most 90 names
(fewer than 630 comparisons) and publishes at most 200 rows under its existing
page/response bounds. PASS; verify native info, resolved status, lint by name
and registered installed list after adoption.

The adjacent installed-list request on the same native build also timed out.
Its UPM backend request completed in 281 ms, but the deferred publication did
not return within the CLI request. The earlier assumption that this read needs
an asynchronous UPM lifecycle is withdrawn. Installed registration already
exists and owns the requested fact; list must read that same product directly.
No registry search or package mutation is replaced with an installed snapshot.
