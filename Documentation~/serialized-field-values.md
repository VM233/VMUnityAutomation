# Serialized field values

`scriptableobject/set-field` accepts the serialized JSON domain supported by the
field owner: primitive values, collections, and structured values. Its catalog
schema requires the asset path, field path, value and project binding. The
destination's Unity serialized type determines which JSON shape is valid.

Asset transaction `serialized-set` operations require the `value` key to be present.
An explicit JSON null is a value and can clear an ObjectReference; an omitted key
is an admission error. Null or blank asset paths and property paths remain invalid.
Scalar zero, false and empty strings are also present values, with type conversion
owned by the destination serialized property.

The required-field fix keeps the existing three field checks per serialized-set
operation and introduces no asset scans, allocations or new iteration axes. Its
focused admission fixture has eight fixed cases, each with one transaction
operation, at most three required-field checks and one temporary Material asset;
native asset creation and deletion are paired. This static cost is bounded and passes.

Use exact catalog discovery before invocation. Inspect persisted values through
`scriptableobject/info` or the typed serialized-object inspection contract;
transport success alone does not prove that the destination accepted the value.

## Scalar integers

Scalar integers use a dedicated value owner behind the shared property
dispatcher. Int32 values remain JSON numbers. Signed Int64 values are read as
invariant decimal strings so values beyond JavaScript's exact integer range
retain their precision; writes accept the existing numeric or decimal-string
input. Conversion failures occur before assigning the native property.

UInt32 values use their unsigned domain and remain exact JSON numbers, including
all 32 native layer bits (`4294967295`). Negative and overflowing inputs are
rejected before assignment; `-1` is not an unsigned serialized value. Unity
2022.2 and later use `numericType` and `uintValue`; Unity 2021.3 uses its declared
type and `longValue` API. This avoids Unity silently clamping a signed assignment
to an unsigned property. The focused fixture covers save/unload/import/reload,
both UInt32 domain limits and a native Collider2D exclusion mask.

## Integer vectors

The shared serialized-property owner used by serialized-object and component
commands reads Vector2Int as numeric x/y and Vector3Int as numeric x/y/z. Writes
require exactly those coordinates, each an exact signed 32-bit integer. Missing,
extra, fractional and out-of-range coordinates fail before the property changes.
For example, a PanelSettings reference resolution accepts
`{"x": 1920, "y": 1080}`; inspect the same property after saving and import.
There is no coordinate iteration or asset scan: each operation reads at most
three coordinates and allocates one bounded coordinate dictionary.

## Project ScriptableSingleton settings

`serialized-object/get` and `serialized-object/set` accept `scriptableSingletonType`
to select a concrete `UnityEditor.ScriptableSingleton<T>` by type name or full name.
This selector is exclusive with instance, asset and GameObject selectors. The
singleton must declare a `FilePathAttribute` whose resolved file is inside the
bound project; global Editor preferences and singletons without persistence are
rejected before their instance is created.

Read the named serialized property before writing. For example, a project settings
owner can be selected with:

```json
{
  "scriptableSingletonType": "VMCommonPreset.Editor.CommonPresetProjectSettings",
  "propertyPath": "presetAssetFolder"
}
```

To write, add `value` and the exact `expectedProjectPath` required by the current
set contract. Changes are applied to the live singleton and persisted through
Unity's protected native `Save(true)` API. `settingsPath` identifies the project
settings file, while `assetPath` is empty for non-AssetDatabase settings. Reading
or writing a singleton selected by `instanceId` follows the same persistence
contract. Asset transactions continue to require AssetDatabase targets.

Invalid types, conflicting selectors, missing file paths and paths outside the
project return distinct stable errors. A persistence failure reports
`serialized_object_persistence_failed` and `stateApplied=true`, because the live
serialized value was already applied; it does not claim a successful disk save.
