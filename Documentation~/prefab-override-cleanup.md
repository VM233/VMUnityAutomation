# Missing Prefab override cleanup

`prefab-asset/cleanup-missing-overrides` selects one exact prefab instance root
inside an asset. The default empty `prefabPath` selects an asset-root Variant;
an explicit path also supports a nested instance in an ordinary Prefab. It does
not visit other instances. Optional `propertyPaths` is an exact field-name set,
not a prefix or wildcard. Omit it to inspect every override on that instance.

Dry-run does not publish. Publication removes only selected missing fields,
uses the existing native Prefab mutation transaction, then reloads that same
instance. It verifies removal and preservation of every retained override,
including source identities and local object references, before commit. Failure
rolls back through the same transaction owner. Scalar `value` in removal evidence
is text, including numeric-looking text; its typed output schema owns that fact.

## Static Cost Ledger before executable writes

One request admits one existing asset and one exact instance path. N<=2048 native
overrides, at most32 selected field names, local-reference ancestry D<=64 and
component count per referenced GameObject B<=256 bound the added work. Before
and after snapshots visit at most2N rows, not N squared; the preserved signature
multiset has at most N entries. Reference identity work is O(N*(D+B)), with at most
1,310,720 ancestry/component entries across two snapshots. Text payloads are
bounded to1 Mi UTF-16 characters before publication. Budget: fewer than2 million
reference/ancestry/component entries and64 MiB additional live managed storage.
The existing one-prefab native load/save is unchanged; no Asset scan, physics,
gameplay frame, hierarchy-wide cleanup, thread or cache is added. PASS for these
input bounds; no machine-independent wall-clock claim is made.

The frozen native fixture has at most3 Prefab assets,20 overrides, hierarchy
depth3, four components per node, four cleanup calls and one preview scene per
case. Four cases require at most80 row inspections each, with at most480 local
reference entries and128 KiB additional managed storage. Native fixture assets
and preview scenes retire in finally without changing the loaded user scene.
