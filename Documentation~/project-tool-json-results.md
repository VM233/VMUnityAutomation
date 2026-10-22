# Logical JSON values in project-tool results

Project tools may publish Newtonsoft `JObject`, `JArray` and `JValue` values,
including DOM values nested inside ordinary CLR result objects, dictionaries,
lists and successful project-tool envelopes. Transport preserves their logical
JSON members, arrays, scalar values, empty containers and nulls.

DOM navigation metadata is not part of that product. `Parent`, `Root`, `Path`,
siblings and indexed CLR properties must never be reflected into a response.
The same object conversion owns error-envelope inspection, so a JSON object's
`success`, `error`, `message`, `errorCode` and `retryable` fields retain their
ordinary structured-error meaning. A non-object JSON value is not an error
dictionary. Non-JSON DOM node kinds are rejected explicitly.

The Automation response owner converts DOM containers directly to dictionaries
and lists. Callers do not flatten, stringify or clone JSON into a second result
contract. Existing CLR and supported Unity-value conversion remains owned by
the response formatter.

## Static cost ledger

Let N be the actual logical node count of one JSON DOM value and C its container
count. The worklist visits each container once and each logical child once:
at most N + C visits, C collection allocations, N retained output values and
C pending work items. Navigation properties are not edges. There is no nested
graph search, ancestor traversal, cache, subscription, native call, worker or
thread transition. Conversion uses an explicit stack and has no recursive call
depth dependent on DOM nesting. Work and additional storage are O(N).

Focused regression inputs are frozen at no more than 257 logical nodes per
test, including one 256-level object chain, nested snapshots and attached DOM
children. Root error-envelope inspection and final formatting can each convert
the DOM once: at most 1028 logical visits, 514 output values, 512 container
allocations and 256 pending work items. Fixture peak storage is budgeted at
512 KiB; no Unity scene or user data is modified. PASS.

Integration acceptance replays the original bounded project-tool snapshot
through the official CLI and its published schema. A successful in-process
conversion alone does not establish that transport/adoption path.
