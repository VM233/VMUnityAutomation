# Material property types

`graphics/material-info`, `material/properties-get` and
`material/properties-set` consume the property's native `ShaderPropertyType`.
Color, vector, texture, float/range and integer properties retain their distinct
storage contracts. Unknown property types fail at the command owner boundary.

`ShaderPropertyType.Int` uses `Material.GetInteger` and `Material.SetInteger`.
The legacy `GetInt`/`SetInt` APIs operate on float storage and cannot preserve the
full signed integer range. This applies to both material assets and inspection
of a runtime renderer's material. JSON output keeps the existing property shape
and contains the exact integer value.

The focused `VmMaterialIntegerPropertyTests` fixture checks both inspection
products and property mutation with zero, signed values beyond float's exact
integer range and `Int32.MaxValue`. It also checks the independent native value
after saving, unloading, importing and reloading the asset; a float property is
the matched control. Temporary materials are deleted by the test owner.

Native API references: [Material.GetInteger](https://docs.unity3d.com/2021.3/Documentation/ScriptReference/Material.GetInteger.html)
and [Material.GetInt](https://docs.unity3d.com/2021.3/Documentation/ScriptReference/Material.GetInt.html).
