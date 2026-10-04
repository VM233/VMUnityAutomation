# Optional capability publication

Optional package readiness can change after the first catalog query in an Editor
domain. The capability owner publishes one availability mask. Catalog routes,
metadata and revision are rebuilt together when that product changes; a cold
package lookup is not retained for the rest of the domain. Invocation still uses
the same capability owner and native handler.

## Static Cost Ledger (before implementation)

This revision has eight optional capabilities, fewer than 900 registered routes
in the consuming project and 188 compiled assemblies. A catalog read evaluates
eight capability predicates. Type checks visit at most 188 loaded assemblies plus
two declared assembly candidates per predicate (1,520 checks); no Asset scan or
gameplay-frame work is introduced. Unchanged availability retains metadata and
the revision. A change rebuilds at most 900 route descriptors, at most 7,200
capability/route comparisons and one serialized revision payload. Main-thread
publication replaces one cache; retained metadata stays within its existing
catalog size. Acceptance budget: bounded capability checks per read, one rebuild
per changed mask, no repeated per-route assembly reflection. PASS.

Focused regressions cover absent-to-present, present-to-absent and unchanged
availability, using the actual route and metadata producer. The CLI integration
witness is Addressables discovery after the same consuming Editor reload.
