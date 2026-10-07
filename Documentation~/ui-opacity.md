# UI opacity observations

UI Toolkit style snapshots expose both `resolvedStyle.opacity` and
`resolvedStyle.effectiveOpacity`. The former is the element's own native
resolved opacity. The latter multiplies that value by each native hierarchy
ancestor's resolved opacity. It observes the current hierarchy without a cache.
Editor and runtime style, query and tree commands use the same snapshot owner.
The input is Unity's [native resolved opacity](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/UIElements.IResolvedStyle-opacity.html);
the effective value is the derived product, not a cached renderer field.

A lock with local opacity 1 beneath a disabled slot with opacity 0.5 therefore
reports effective opacity 0.5. Moving the lock to an opaque parent or changing
the slot's opacity changes the next observation. Effective opacity is a style
product, not a visibility or pixel-alpha assertion: texture alpha, color tint,
clipping and attachment to a panel remain separate observations.

## Static Cost Ledger

The frozen BattleIdle witness has nine locks, each with at most 16 nodes from
its queried document root. The panel can add ancestors outside that root; replay
records their actual depth as part of the native test. The focused fixture has
one EditorWindow and three authored elements, at most four ancestry reads for
each of four observed states. It creates no assets or scene objects and releases
the window in `finally`.

For S requested style snapshots and maximum native ancestry depth D, observation
performs at most S times D property reads and multiplications, O(SD) time and
O(1) additional working space per snapshot. There is no child traversal, cache,
background task or gameplay-frame work. The frozen regression uses four states
and at most four ancestors: 16 reads, zero retained allocations; PASS. Integration
uses only the nine exact lock matches, budgets at most 288 ancestry reads at
depth 32, and must establish that depth bound in its native hierarchy receipt
before invoking the replay. A changed query scope or hierarchy invalidates this
integration budget. Broad tree inspections must budget their S and D separately.
