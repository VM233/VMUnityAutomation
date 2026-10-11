# Loaded scene object selection

Scene GameObject path selection includes active and inactive loaded objects in one
search. It accepts a full hierarchy path or a unique exact name, with ordinal
matching. An optional leading slash identifies the same hierarchy path.
Ambiguous names or paths are rejected before mutation; use an exact instance
identity when loaded scenes contain identical paths.
Ambiguity uses the executor's existing `command_exception` error boundary and
includes the rejected selector in its message. Missing explicit parents/sources
retain their commands' existing structured owner error responses.

`scene/hierarchy` restricts `parentPath` to its reported active Scene. Other scene
object consumers share the same loaded-object selector: GameObject operations,
Prefab and particle parents, serialized scene references, constraints, LODs,
graphics, navigation, UGUI, selection, terrain and object-scoped Undo.
An explicitly supplied missing parent/source is an error, and is resolved before
creation, instantiation, reparenting or component addition. Omitting the optional
parent/source retains the route's ordinary root/no-source semantics.

The native object-search owner selects APIs at compile time: Unity 2021.3 uses
[`FindObjectsOfType`](https://docs.unity3d.com/2021.3/Documentation/ScriptReference/Object.FindObjectsOfType.html),
Unity 2022.2 through 6000.3 uses
[`FindObjectsByType`](https://docs.unity3d.com/2022.2/Documentation/ScriptReference/Object.FindObjectsByType.html)
with sort mode, and Unity 6000.4 onward uses its unsorted overload. There is no
active-only first attempt or a second inactive fallback.

Static Cost Ledger before executable writes: PASS for the frozen acceptance
graph. The clean DD2 Main Scene contains 32 GameObjects. Focused fixtures add at
most eight owned objects across two additive Scenes and restore the original
active Scene. At most sixty-four selector/consumer invocations scan at most forty
loaded scene objects with depth at most fifty: 128,000 hierarchy segments. Each
search retains one native reference array and one matching identity; ambiguity
does not retain a list. Peak explicit reference payload stays below 8 KiB and
fixture string/response scratch below 128 KiB. No frame advance, player data,
scene save, cached selector, background thread or extra Editor is used. Prefab
qualification creates and deletes one uniquely owned temporary Asset folder. This
frozen test bound does not claim a performance bound for arbitrary larger scenes.

The original public witness is an inactive `City World` in DD2's saved Main
Scene. Native acceptance repeats that same `parentPath` through the official
CLI after immutable package adoption. Focused tests cover inactive roots and
children, active-Scene isolation, duplicate-name rejection, exact identity,
inactive parenting, missing-parent non-mutation and inactive constraint sources.
