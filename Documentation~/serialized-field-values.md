# Serialized field values

`scriptableobject/set-field` accepts the serialized JSON domain supported by the
field owner: primitive values, collections, and structured values. Its catalog
schema requires the asset path, field path, value and project binding. The
destination's Unity serialized type determines which JSON shape is valid.

Use exact catalog discovery before invocation. Inspect persisted values through
`scriptableobject/info` or the typed serialized-object inspection contract;
transport success alone does not prove that the destination accepted the value.
