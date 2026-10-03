# Serialized field values

`scriptableobject/set-field` accepts the serialized JSON domain supported by the
field owner: primitive values, collections, and structured values. Its catalog
schema requires the asset path, field path, value and project binding. The
destination's Unity serialized type determines which JSON shape is valid.

Use exact catalog discovery before invocation. Inspect persisted values through
`scriptableobject/info` or the typed serialized-object inspection contract;
transport success alone does not prove that the destination accepted the value.

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
